'use strict';
let accCupFilter=-1,accSelectedCar=null;
const accCups=['Pro','Pro-Am','Am','Silver','National'];
function accText(el,value){value=String(value);if(el.textContent===value)return;if(el.childNodes.length===1&&el.firstChild.nodeType===3)el.firstChild.nodeValue=value;else el.textContent=value;}
function accColor(cup){return ['#a9c6ec','#62d0a3','#f3b16d','#ccd4e3','#bf9ee6'][cup]||'#899aa8';}
function accLap(ms){if(!Number.isFinite(ms)||ms<=0)return '—';const m=Math.floor(ms/60000);return m+':'+((ms%60000)/1000).toFixed(3).padStart(6,'0');}
function accGap(d,race){if(d.position===1)return 'Лидер';if(race.sessionType!==10)return accLap(d.bestLapMs);return Number.isFinite(d.gapAheadMs)?'≈ '+(d.gapAheadMs/1000).toFixed(1)+' с':'—';}
function mountAccRace(){
 const root=node('section','accRace');root.id='accRace';
 const heading=node('div','accRaceHeading');heading.append(node('h2','','КАРТА И ПИЛОТЫ'),node('span','accRaceStatus'));root.append(heading);
 const filters=node('div','accRaceFilters');for(const [id,label] of [[-1,'Все кубки'],...accCups.map((x,i)=>[i,x])]){const b=node('button','',label);b.type='button';b.dataset.cup=id;b.onclick=()=>{accCupFilter=id;renderAccRace();};filters.append(b);}root.append(filters);
 const grid=node('div','accRaceGrid'),map=node('div','accRaceMap');const canvas=document.createElement('canvas');canvas.setAttribute('aria-label','Координаты машин на трассе ACC');map.append(canvas,node('p','accMapHint'));grid.append(map,node('div','accStandings'));root.append(grid,node('p','hint','ВЫ — белый ободок. Цвет — категория кубка. Интервалы ≈ рассчитаны по времени прохождения одной точки; на старте и в боксах могут отсутствовать.'));
 $('special').append(root);renderAccRace();
}
function renderAccRace(){
 const root=$('accRace');if(!root||profileId!=='acc')return;const race=displayData()?.acc?.race;
 const live=!!currentData()&&race?.fresh===true&&!race?.replay;
 root.querySelector('.accRaceStatus').textContent=(race?.track||'Ожидание трассы')+' · '+(live?'LIVE':race?.replay?'Повтор':'Последние / нет данных');
 root.querySelectorAll('[data-cup]').forEach(b=>b.classList.toggle('active',Number(b.dataset.cup)===accCupFilter));
 const drivers=(race?.drivers||[]).filter(d=>accCupFilter<0||d.cup===accCupFilter);
 const list=root.querySelector('.accStandings'),desired=[];
 if(!drivers.length){let hint=list.querySelector('.hint');if(!hint)hint=node('p','hint','Ожидание участников. Включите Broadcasting ACC и начните заезд с соперниками.');desired.push(hint);}
 // Preserve button nodes during updates: a finger held for >100 ms must still
 // complete its click, and keyboard focus/details must not disappear.
 for(const d of drivers){let row=list.querySelector(`[data-car="${d.index}"]`);
  if(!row){row=node('button','accDriver');row.type='button';row.dataset.car=d.index;row.onclick=()=>{accSelectedCar=accSelectedCar===d.index?null:d.index;renderAccRace();};const name=node('span','accDriverName');name.append(node('b'),node('small'));row.append(node('strong','accPosition'),name,node('span','accDriverGap'));}
  row.classList.toggle('player',d.player);row.classList.toggle('selected',d.index===accSelectedCar);accText(row.querySelector('.accPosition'),d.position>0?d.position:'—');
  const name=row.querySelector('.accDriverName');name.style.color=accColor(d.cup);accText(name.firstElementChild,d.name+(d.player?' · ВЫ':''));accText(name.lastElementChild,'#'+d.number+' · '+(accCups[d.cup]||'Кубок —')+' · '+(d.location===2?'В боксах':'Круг '+(d.lap+1)));accText(row.querySelector('.accDriverGap'),accGap(d,race));desired.push(row);
  if(d.index===accSelectedCar){let detail=list.querySelector(`[data-car-detail="${d.index}"]`);if(!detail){detail=node('p','accDriverDetail');detail.dataset.carDetail=d.index;}detail.textContent=(d.team||'Команда —')+' · Лучший '+accLap(d.bestLapMs)+' · Последний '+accLap(d.lastLapMs)+(Number.isFinite(d.gapLeaderMs)&&d.position!==1?' · До лидера ≈ '+(d.gapLeaderMs/1000).toFixed(1)+' с':'');desired.push(detail);}
 }
 desired.forEach((element,i)=>{if(list.children[i]!==element)list.insertBefore(element,list.children[i]||null);});for(const element of [...list.children])if(!desired.includes(element))element.remove();
 const canvas=root.querySelector('canvas'),box=canvas.getBoundingClientRect();if(box.width<1)return;const ratio=window.devicePixelRatio||1;canvas.width=Math.round(box.width*ratio);canvas.height=Math.round(box.height*ratio);const ctx=canvas.getContext('2d');ctx.scale(ratio,ratio);
 const points=race?.points||[],markers=drivers.filter(d=>d.location>0&&Number.isFinite(d.x)&&Number.isFinite(d.z));
 const all=[...points,...markers];if(!all.length){root.querySelector('.accMapHint').textContent='Координаты появятся после подключения к игре.';return;}
 const minX=Math.min(...all.map(p=>p.x)),maxX=Math.max(...all.map(p=>p.x)),minZ=Math.min(...all.map(p=>p.z)),maxZ=Math.max(...all.map(p=>p.z));
 const scale=Math.min((box.width-48)/Math.max(100,maxX-minX),(box.height-48)/Math.max(100,maxZ-minZ));
 const project=p=>[box.width/2+(p.x-(maxX+minX)/2)*scale,box.height/2-(p.z-(maxZ+minZ)/2)*scale];
 const path=new Path2D();for(let i=0;i<points.length;i++){const p=points[i],a=points[i-1],o=project(p);const linked=a&&p.bin-a.bin<=4&&Math.hypot(p.x-a.x,p.z-a.z)<Math.max(80,(race.trackLength||0)/40);if(linked)path.lineTo(...o);else path.moveTo(...o);}
 if(points.length>1){const a=points[0],b=points[points.length-1];if((race.bins-b.bin+a.bin)<=4&&Math.hypot(a.x-b.x,a.z-b.z)<Math.max(80,(race.trackLength||0)/40))path.lineTo(...project(a));}
 ctx.lineJoin='round';ctx.lineCap='round';ctx.strokeStyle='#34444c';ctx.lineWidth=11;ctx.stroke(path);ctx.strokeStyle='#abb6ba';ctx.lineWidth=4;ctx.stroke(path);
 const occupied=[];for(const d of markers.sort((a,b)=>Number(a.player)-Number(b.player))){const [x,y]=project(d);ctx.globalAlpha=live&&d.fresh?1:.45;ctx.beginPath();ctx.arc(x,y,d.player?7:5,0,Math.PI*2);ctx.fillStyle=accColor(d.cup);ctx.fill();ctx.lineWidth=d.player?3:1;ctx.strokeStyle=d.player?'#fff':'#101820';ctx.stroke();
  const label=(d.position||'—')+' '+(d.shortName||d.name.split(' ').at(-1).slice(0,3).toUpperCase());ctx.font='bold 11px system-ui';const w=ctx.measureText(label).width+8;
  for(let offset=0;offset<5;offset++){const lx=Math.min(Math.max(2,x+10),box.width-w-2),ly=Math.min(Math.max(16,y-8+offset*17),box.height-2);if(occupied.some(o=>Math.abs(o.y-ly)<16&&Math.abs(o.x-lx)<Math.max(w,o.w)))continue;occupied.push({x:lx,y:ly,w});ctx.fillStyle='#0e181de8';ctx.fillRect(lx-3,ly-12,w,16);ctx.fillStyle='#f0f4f4';ctx.fillText(label,lx,ly);break;}
 }ctx.globalAlpha=1;
 const coverage=race?.bins?Math.round(points.length/race.bins*100):0;root.querySelector('.accMapHint').textContent=coverage>=95?'Контур собран по координатам игры':`Контур изучается: ${coverage}%. Машинам нужно проехать круг; пропуски не соединяются.`;
}
