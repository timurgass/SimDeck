'use strict';
const vehicleLabels={unknown:'Неизвестный класс',car:'Легковая',suv:'Внедорожник',pickup:'Пикап',van:'Фургон',bus:'Автобус',truck:'Грузовик / тягач',trailer:'Прицеп',tractor:'Трактор',combine:'Комбайн',loader:'Погрузчик',telehandler:'Телескопический погрузчик',forestry:'Лесная техника',sprayer:'Опрыскиватель',tracked:'Гусеничная техника',implement:'Орудие',header:'Жатка',cultivator:'Культиватор',plow:'Плуг',seeder:'Сеялка',...fs25EquipmentLabels};
const farmSprites={tractor:[0,130,354,320],truck:[772,210,368,220],loader:[1145,150,391,290],telehandler:[20,565,350,320],forestry:[390,555,378,340],sprayer:[775,615,370,265]};
function vehicleWheels(w){return Array.isArray(w)&&w.length<=32&&w.every(p=>Number.isFinite(p.x)&&Number.isFinite(p.z)&&Math.abs(p.x)<=100&&Math.abs(p.z)<=100)?w:[];}
function vehicleAxles(w){const axles=[];for(const z of w.map(w=>w.z).sort((a,b)=>a-b))if(!axles.length||z-axles.at(-1)>.35)axles.push(z);return axles.length||null;}
function vehicleSvgRoot(height=290,label='Схема техники'){
 const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.setAttribute('viewBox',`0 0 320 ${height}`);svg.setAttribute('role','img');svg.setAttribute('aria-label',label);svg.classList.add('vehicleDrawing');return svg;
}
function vElement(parent,tag,attrs={}){const e=document.createElementNS('http://www.w3.org/2000/svg',tag);for(const[k,v]of Object.entries(attrs))e.setAttribute(k,String(v));parent.append(e);return e;}
function vehicleSprite(parent,atlas,rect,x,y,w,h,cls=''){
 const sprite=vElement(parent,'svg',{x,y,width:w,height:h,viewBox:rect.join(' '),overflow:'hidden',class:'vehicleSprite '+cls});const dims={...fs25AtlasSizes,farm:[1536,1024],road:[1254,1254],equipment:[2172,724],gt:[1024,1536],harvest:[1254,1254]};vElement(sprite,'image',{href:'/vehicles/'+atlas+'.png',width:dims[atlas][0],height:dims[atlas][1]});return sprite;
}
function farmSprite(kind){return fs25EquipmentSprites[kind]||fs25EquipmentSprites[kind==="forestry"?"woodharvester":kind]||null;}
function roadRect(kind){return ({car:[120,9,233,391],suv:[523,6,209,412],pickup:[905,9,223,394],van:[124,418,225,409],bus:[534,418,186,410],truck:[917,418,199,410],formula:[510,840,235,404],trailer:[945,840,144,401],gt:[0,0,1024,1536]})[kind]||null;}
function farmSvg(kind,attachment){
 const svg=vehicleSvgRoot(230,vehicleLabels[kind]);const root=farmSprite(kind),tool=attachment&&farmSprite(attachment.kind);if(!root)return node('p','hint','Схема этого класса пока не определена');
 vElement(svg,'path',{d:'M0 205H320',class:'vehicleGround'});const front=attachment&&(attachment.mount==='front'||(kind==='combine'&&attachment.kind==='header')),w=tool?185:250,h=Math.min(190,w*root[1][3]/root[1][2]),rw=h*root[1][2]/root[1][3],x=tool?(front?120:0):(320-rw)/2;vehicleSprite(svg,root[0],root[1],x,205-h,rw,h);
 if(tool){const tx=front?0:x+rw*.93,tw=front?x+rw*.12:320-tx,th=Math.min(160,tw*tool[1][3]/tool[1][2]);const g=vElement(svg,'g',{class:'vehicleImplement'});g.dataset.pivot=tx+','+(205-th*.6);g.style.transformOrigin=tx+'px '+(205-th*.6)+'px';vehicleSprite(g,tool[0],tool[1],tx,205-th,tw,th);vElement(svg,'circle',{cx:tx,cy:205-th*.6,r:3,class:'vehicleHitch'});}
 return svg;
}
function vehicleSvg(kind,wheels,trailer=null){
 const height=trailer?410:290,svg=vehicleSvgRoot(height,vehicleLabels[kind]||kind),rect=roadRect(kind);if(!rect)return node('p','hint','Нет схемы этого класса · без предположений о кузове');
 const h=trailer?250:290,bw=h*rect[2]/rect[3];vehicleSprite(svg,kind==='gt'?'gt':'road',rect,160-bw/2,0,bw,h);
 const drawWheels=(list,from,to,bw)=>{list=vehicleWheels(list);if(!list.length)return;const min=Math.min(...list.map(w=>w.z)),span=Math.max(1,Math.max(...list.map(w=>w.z))-min),half=Math.max(.5,...list.map(w=>Math.abs(w.x)));for(const w of list){const x=160+w.x/half*bw*.41,y=from+(w.z-min)/span*(to-from),tw=bw*.18,th=tw*1.7;vehicleSprite(svg,'equipment',[1670,80,330,560],x-tw/2,y-th/2,tw,th,'vehicleWheel');if(w.powered===true)vElement(svg,'rect',{x:x-tw*.33,y:y-th*.33,width:tw*.66,height:th*.66,rx:3,class:'vehiclePowered'});}};
 if(!['gt','formula'].includes(kind))drawWheels(wheels,h*.24,h*.82,h*.55);
 if(trailer!==null){const ty=h*.48,th=height-ty;drawWheels(trailer.wheels,ty+th*.72,ty+th*.84,th*.55);vehicleSprite(svg,'road',roadRect('trailer'),160-th*.18,ty,th*.36,th,'vehicleGhost');}
 return svg;
}
function mountVehiclePanel(root){
 if(['fs25','ets2','beamng-default'].includes(profileId)){const panel=node('section','card vehiclePanel');panel.id='vehiclePanel';root.append(panel);}
 else if(profileId==='snowrunner')mountSnowVehicle(root);
 else if(['acc','ams2'].includes(profileId)){const panel=node('section','card vehiclePanel');panel.append(node('h2','caption','СОСТОЯНИЕ МАШИНЫ'),vehicleSvg(profileId==='acc'?'gt':'formula',[]),node('small','hint','Иллюстрация класса · параметры из доступной телеметрии'));root.append(panel);}
}
let lastKnownVehicle=null,lastVehicleProfile="";
function updateVehiclePanel(data){
 const panel=$('vehiclePanel');if(!panel)return;if(lastVehicleProfile!==profileId){lastKnownVehicle=null;lastVehicleProfile=profileId;}if(data?.vehicle)lastKnownVehicle=data.vehicle;const stale=!data?.vehicle,v=data?.vehicle||lastKnownVehicle,signature=JSON.stringify([v||null,stale]);if(panel.dataset.snapshot===signature)return;panel.dataset.snapshot=signature;
 if(!v||v.controlled===false){panel.dataset.geometry='';panel.replaceChildren(node('h2','caption','ТЕХНИКА'),node('h3','',v?.controlled===false?'Вы не в технике':'Ждём сведения о технике'),node('p','hint',!data?'Нет свежих данных игры':'Нужны данные игрового мода / плагина.'));return;}
 const wheels=vehicleWheels(v.wheels),axles=vehicleAxles(wheels),kind=Object.hasOwn(vehicleLabels,v.kind)?v.kind:'unknown',attachments=Array.isArray(v.attachments)?v.attachments.slice(0,32):[],farm=profileId==='fs25',linked=attachments.find(a=>a.parentId===v.id&&farmSprite(a.kind)&&(a.mount==='front'||a.mount==='rear'||a.kind==='header')),trailer=profileId==='ets2'?attachments.find(a=>a.kind==='trailer'):null;
 const geometry=JSON.stringify([v.id,v.name,kind,wheels,attachments.map(a=>[a.id,a.parentId,a.name,a.kind,a.mount,a.wheels])]);
 if(panel.dataset.geometry!==geometry){
  panel.dataset.geometry=geometry;panel.replaceChildren(node('h2','caption',farm?'ТЕХНИКА И ОРУДИЯ':'СОСТОЯНИЕ МАШИНЫ'),node('h3','',String(v.name||'Неизвестная модель').slice(0,160)),node('small','','АВТО · '+vehicleLabels[kind]+' · '+(axles?axles+' оси':'Геометрия колёс неизвестна')));
  const scene=node('div','vehicleScene');scene.append(farm?farmSvg(kind,linked):vehicleSvg(kind,wheels,trailer),node('div','vehicleWear'));panel.append(scene,node('small','hint','Иллюстрация класса · модель и состояния из игры'),node('p','hint vehicleFreshness',''));
  const stats=node('div','vehicleStats');panel.append(stats);
 }
 textIfVehicle();function textIfVehicle(){const label=panel.querySelector('.vehicleFreshness');if(label)label.textContent=stale?'Последняя известная техника · данные устарели':'';}
 const implement=panel.querySelector('.vehicleImplement');if(implement)implement.style.transform=linked?.lowered===false?'translateY(-20px) rotate(-12deg)':'translateY(0) rotate(0)';
 const stats=panel.querySelector('.vehicleStats');stats.replaceChildren();
 for(const a of attachments){const item=node('div','vehicleAttachment');item.append(node('strong','','↳ '+String(a.name||vehicleLabels[a.kind]||'Орудие').slice(0,160)));if(farm&&a!==linked)item.append(farmSvg(a.kind,null));const values=[];if(typeof a.lowered==='boolean')values.push(a.lowered?'ОПУЩЕНО':'ПОДНЯТО');if(typeof a.turnedOn==='boolean')values.push(a.turnedOn?'РАБОТАЕТ':'ВЫКЛЮЧЕНО');if(Number.isFinite(a.fold)&&a.fold>=0&&a.fold<=1)values.push('Складывание '+Math.round(a.fold*100)+'%');item.append(node('small','',stale?'Состояние орудия: данные устарели':values.join(' · ')||'Состояние не передано игрой'));stats.append(item);}
 const wearArea=panel.querySelector('.vehicleWear');wearArea.replaceChildren();
 const names={engine:'Двигатель',transmission:'Коробка',cabin:'Кабина',chassis:'Шасси',wheels:'Колёса'};let wear=false;if(!stale&&v.wear)for(const[k,label]of Object.entries(names)){const value=v.wear[k];if(Number.isFinite(value)&&value>=0&&value<=1){wear=true;const row=node('div','wearRow');row.append(node('span','',label),node('strong','',Math.round(value*100)+'%'));const bar=node('progress');bar.max=1;bar.value=value;bar.className=value>.3?'danger':value>.05?'warning':'healthy';row.append(bar);wearArea.append(row);}}panel.querySelector('.vehicleScene').classList.toggle('hasWear',wear);if(!farm&&!wear)stats.append(node('p','hint','Повреждения узлов: нет данных'));
}
let snowWheelCount=4;
function mountSnowVehicle(root){const panel=node('section','card vehiclePanel');panel.id='snowVehiclePanel';panel.append(node('h2','caption','ПРИВОД И БЛОКИРОВКА'),node('p','hint','Ручной выбор схемы · живая телеметрия пока недоступна'));const nav=node('div','snowWheelChoices'),scene=node('div');for(const count of [4,6,8,10]){const b=node('button','',count+' колёс');b.onclick=()=>{snowWheelCount=count;render();};nav.append(b);}panel.append(nav,scene,node('p','hint','Полный привод: неизвестно · блокировка: неизвестно'));root.append(panel);function render(){scene.replaceChildren(vehicleSvg(snowWheelCount===4?'suv':'truck',Array.from({length:snowWheelCount},(_,i)=>({x:i%2?-1:1,z:Math.floor(i/2),powered:null}))),node('p','',snowWheelCount+' колёс · '+snowWheelCount/2+' оси · иллюстрация'));nav.querySelectorAll('button').forEach((b,i)=>b.classList.toggle('selected',[4,6,8,10][i]===snowWheelCount));}render();}
function f1Schematic(root,data){
 const v=data?.values||{},layout=node('div','f1Schematic'),left=node('div','tyreColumn'),right=node('div','tyreColumn'),car=node('div','formulaPicture');const picture=vehicleSvg('formula',[]);car.append(picture);for(const [key,side]of [['frontLeftWingDamage',-1],['frontRightWingDamage',1]]){const damage=v[key];if(Number.isFinite(damage))vElement(picture,'path',{d:`M${160+side*43-20} 16L${160+side*43+20} 29`,stroke:damage>=30?'#ff575d':damage>5?'#ffc449':'#7dda71','stroke-width':9,'stroke-linecap':'round'});}
 for(const[key,label]of [['frontLeftWingDamage','Левое крыло'],['frontRightWingDamage','Правое крыло']]){const badge=node('div','wingBadge',label+' · '+fmt(v[key],'%'));badge.classList.add(!Number.isFinite(v[key])?'unknown':v[key]>30?'danger':v[key]>5?'warning':'healthy');car.append(badge);}
 for(const[i,name]of [[2,'ПЕРЕДНЯЯ ЛЕВАЯ'],[0,'ЗАДНЯЯ ЛЕВАЯ'],[3,'ПЕРЕДНЯЯ ПРАВАЯ'],[1,'ЗАДНЯЯ ПРАВАЯ']]){const w=data?.wheels?.[i],card=node('section','tyreCard');card.append(node('h3','',name));for(const[label,value,unit,max]of [['Износ',w?.wear,'%',100],['Поверхность',w?.surface,' °C',140],['Внутри',w?.inner,' °C',140],['Давление',w?.pressure,' PSI',null]]){const row=node('div','wearRow');row.append(node('span','',label),node('strong','',fmt(value,unit,1)));if(max){const bar=node('progress');bar.max=max;bar.value=Number.isFinite(value)?value:0;bar.className=unit==='%'?'healthy':'warning';row.append(bar);}card.append(row);}card.append(node('small','hint','Тормоз '+fmt(w?.brake,' °C')+' · урон '+fmt(w?.damage,'%')));(i===2||i===0?left:right).append(card);}
 layout.append(left,car,right);root.append(layout);
 for(const[key,label]of [['rearWingDamage','Заднее крыло'],['floorDamage','Днище']])root.append(node('p','hint',label+' · '+fmt(v[key],'%')));
}
