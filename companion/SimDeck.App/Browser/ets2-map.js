'use strict';
// Static road geometry travels over authenticated HTTP only when the truck leaves the cached area.
const etsMapState={slice:null,busy:false,attempt:0,loadedAt:0,span:1600,position:null,status:'Подготовка карты ETS2…',resize:null};
function mountEtsMap(parent){
 const box=node('div','etsRoadMap'),canvas=node('canvas');canvas.id='etsRoadCanvas';canvas.setAttribute('aria-label','Дороги ETS2 и положение грузовика');canvas.style.cssText='width:100%;height:260px;display:block;background:#101820';
 const tools=node('div','etsMapTools'),hint=node('small','hint');hint.id='etsMapHint';
 for(const[label,ratio]of [['＋',.5],['−',2]]){const b=node('button','',label);b.type='button';b.onclick=()=>{etsMapState.span=Math.max(800,Math.min(3200,etsMapState.span*ratio));drawEtsMap();};tools.append(b);}
 box.append(canvas,tools,hint);parent.append(box);
 etsMapState.resize?.disconnect();etsMapState.resize=new ResizeObserver(drawEtsMap);etsMapState.resize.observe(canvas);drawEtsMap();
}
function validEtsSlice(j){
 if(!j||!Number.isFinite(j.x)||!Number.isFinite(j.z)||!Number.isFinite(j.span)||j.span<800||j.span>16000||!Array.isArray(j.roads)||j.roads.length>8000||!Array.isArray(j.cities)||j.cities.length>5000)return false;
 return j.roads.every(r=>Array.isArray(r.p)&&r.p.length>=4&&r.p.length<=128&&r.p.length%2===0&&r.p.every(p=>Number.isFinite(p)&&Math.abs(p)<=1000000));
}
async function updateEtsMap(data){
 const n=data?.ets2Navigation;if(profileId!=='ets2')return;
 if(Number.isFinite(n?.worldX)&&Number.isFinite(n?.worldZ))etsMapState.position=n;
 drawEtsMap();
 if(!session||document.hidden||!etsMapState.position||etsMapState.busy||performance.now()-etsMapState.attempt<2500)return;
 const p=etsMapState.position,s=etsMapState.slice;
 if(s&&performance.now()-etsMapState.loadedAt<60000&&Math.abs(s.x-p.worldX)<s.span/4&&Math.abs(s.z-p.worldZ)<s.span/4)return;
 const current=session;etsMapState.busy=true;etsMapState.attempt=performance.now();
 const abort=new AbortController(),timer=setTimeout(()=>abort.abort(),10000);
 try{
  const r=await fetch(`/ets2-map?x=${p.worldX}&z=${p.worldZ}&span=6400`,{signal:abort.signal,cache:'no-store'});
  if(!r.ok)throw Error(r.status===503?'Карта готовится на ПК…':'Карта пока недоступна');
  if(Number(r.headers.get('content-length'))>5000000)throw Error('Карта слишком большая');
  const text=await r.text();if(text.length>5000000)throw Error('Карта слишком большая');const j=JSON.parse(text);
  if(!validEtsSlice(j))throw Error('Некорректная карта');
  if(session===current&&profileId==='ets2'){etsMapState.slice=j;etsMapState.loadedAt=performance.now();etsMapState.status='';}
 }catch(e){if(session===current)etsMapState.status=e.name==='AbortError'?'Подготовка карты задержалась':e.message;}
 finally{clearTimeout(timer);etsMapState.busy=false;drawEtsMap();}
}
function drawEtsMap(){
 const canvas=document.getElementById('etsRoadCanvas');if(!canvas)return;
 const w=canvas.clientWidth,h=260,dpr=Math.min(2,devicePixelRatio||1);if(w<1)return;
 if(canvas.width!==w*dpr||canvas.height!==h*dpr){canvas.width=w*dpr;canvas.height=h*dpr;}
 const c=canvas.getContext('2d');c.setTransform(dpr,0,0,dpr,0,0);c.clearRect(0,0,w,h);
 const s=etsMapState.slice,n=etsMapState.position,x=n?.worldX??s?.x??0,z=n?.worldZ??s?.z??0,k=w/etsMapState.span;
 const point=(a,b)=>[w/2+(a-x)*k,h/2+(b-z)*k];c.lineCap='round';c.lineJoin='round';
 c.save();c.translate(w/2-x*k,h/2-z*k);c.scale(k,k);
 for(const r of s?.roads||[]){if(!r.path){r.path=new Path2D();for(let i=0;i<r.p.length;i+=2){if(i===0)r.path.moveTo(r.p[i],r.p[i+1]);else r.path.lineTo(r.p[i],r.p[i+1]);}}c.strokeStyle='#526575';c.lineWidth=Math.max(4/k,r.w||10)+2/k;c.stroke(r.path);}
 for(const r of s?.roads||[]){c.strokeStyle='#a8bac6';c.lineWidth=Math.max(2/k,r.w||10);c.stroke(r.path);}c.restore();
 c.fillStyle='#d5e0e6';c.font='12px system-ui';c.textAlign='center';for(const city of s?.cities||[]){const p=point(city.x,city.z);if(p[0]>30&&p[0]<w-30&&p[1]>20&&p[1]<h-15)c.fillText(city.name,p[0],p[1]);}
 if(n){c.save();c.translate(w/2,h/2);c.rotate(-(Number.isFinite(n.heading)?n.heading:0)*Math.PI*2);c.beginPath();c.moveTo(0,-14);c.lineTo(-10,11);c.lineTo(10,11);c.closePath();c.fillStyle='#ffc449';c.fill();c.strokeStyle='white';c.lineWidth=2;c.stroke();c.restore();}
 c.textAlign='left';c.fillStyle='#d5e0e6';c.fillText('С ↑',10,18);
 const hint=document.getElementById('etsMapHint');if(hint)hint.textContent=s?'Дороги из ETS2 · позиция грузовика · без линии GPS-маршрута':etsMapState.status;
}
