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
function farmGeometry(width,height,rootAspect,toolAspect,front){
 const ground=height*.9,rh=height*.74,rw=rh*rootAspect,th=toolAspect!==null?height*.48:0,tw=th*(toolAspect||0),overlap=toolAspect!==null?Math.min(rw,tw)*.08:0,total=rw+tw-overlap,scale=Math.min(1,width*.96/total),start=(width-total*scale)/2;
 const rx=start+(front&&toolAspect!==null?(tw-overlap)*scale:0),tx=front?start:start+(rw-overlap)*scale;
 return {root:{x:rx,y:ground-rh*scale,w:rw*scale,h:rh*scale},tool:toolAspect!==null?{x:tx,y:ground-th*scale,w:tw*scale,h:th*scale}:null,pivotX:front?tx+tw*scale:tx,pivotY:ground-th*scale*.45};
}
function farmSvg(kind,attachment){
 const svg=vehicleSvgRoot(230,vehicleLabels[kind]);svg.setAttribute('viewBox','0 0 560 230');const root=farmSprite(kind),tool=attachment&&farmSprite(attachment.kind);if(!root)return node('p','hint','Схема этого класса пока не определена');
 const front=!!(attachment&&(attachment.mount==='front'||(kind==='combine'&&attachment.kind==='header'))),geometry=farmGeometry(560,230,root[1][2]/root[1][3],tool?tool[1][2]/tool[1][3]:null,front),r=geometry.root;
 vElement(svg,'path',{d:'M0 207H560',class:'vehicleGround'});vehicleSprite(svg,root[0],root[1],r.x,r.y,r.w,r.h);
 if(tool){const t=geometry.tool,g=vElement(svg,'g',{class:'vehicleImplement'});g.dataset.front=String(front);g.style.transformOrigin=geometry.pivotX+'px '+geometry.pivotY+'px';vehicleSprite(g,tool[0],tool[1],t.x,t.y,t.w,t.h);vElement(svg,'circle',{cx:geometry.pivotX,cy:geometry.pivotY,r:3,class:'vehicleHitch'});}
 return svg;
}
function vehicleSvg(kind,wheels,trailer=null){
 const height=trailer?410:290,svg=vehicleSvgRoot(height,vehicleLabels[kind]||kind),rect=roadRect(kind);if(!rect)return node('p','hint','Нет схемы этого класса · без предположений о кузове');
 const h=trailer?250:290,bw=h*rect[2]/rect[3];vehicleSprite(svg,kind==='gt'?'gt':'road',rect,160-bw/2,0,bw,h);
 const drawWheels=(list,from,to,bw)=>{list=vehicleWheels(list);if(!list.length)return;const min=Math.min(...list.map(w=>w.z)),span=Math.max(1,Math.max(...list.map(w=>w.z))-min),half=Math.max(.5,...list.map(w=>Math.abs(w.x)));for(const w of list){const x=160+w.x/half*bw*.41,y=from+(w.z-min)/span*(to-from),tw=bw*.18,th=tw*1.7;const tyre=vElement(svg,'g',{class:'vehicleWheel'});vElement(tyre,'rect',{x:x-tw/2,y:y-th/2,width:tw,height:th,rx:3,fill:'#11181c',stroke:'#72818a','stroke-width':1});for(let j=1;j<5;j++)vElement(tyre,'path',{d:`M${x-tw*.35} ${y-th/2+th*j/5}H${x+tw*.35}`,stroke:'#35424b','stroke-width':1});if(w.powered===true)vElement(svg,'rect',{x:x-tw*.33,y:y-th*.33,width:tw*.66,height:th*.66,rx:3,class:'vehiclePowered'});}};
 if(!['gt','formula'].includes(kind))drawWheels(wheels,h*.24,h*.82,h*.55);
 if(trailer!==null){const ty=h*.48,th=height-ty;drawWheels(trailer.wheels,ty+th*.72,ty+th*.84,th*.55);vehicleSprite(svg,'road',roadRect('trailer'),160-th*.18,ty,th*.36,th,'vehicleGhost');}
 return svg;
}
function mountVehiclePanel(root){
 if(['fs25','ets2','beamng-default'].includes(profileId)){const panel=node('section','card vehiclePanel');panel.id='vehiclePanel';root.append(panel);}
 else if(profileId==='snowrunner')mountSnowVehicle(root);
 else if(profileId==='ams2')mountRacingClass(root);
}
let lastKnownVehicle=null,lastVehicleProfile="";
function updateVehiclePanel(data){
 const panel=$('vehiclePanel');if(!panel)return;if(lastVehicleProfile!==profileId){lastKnownVehicle=null;lastVehicleProfile=profileId;}if(data?.vehicle)lastKnownVehicle=data.vehicle;const stale=!currentData()?.vehicle,v=data?.vehicle||lastKnownVehicle,signature=JSON.stringify([v||null,stale]);if(panel.dataset.snapshot===signature)return;panel.dataset.snapshot=signature;
 if(!v||v.controlled===false){panel.dataset.geometry='';panel.replaceChildren(node('h2','caption','ТЕХНИКА'),node('h3','',v?.controlled===false?'Вы не в технике':'Ждём сведения о технике'),node('p','hint',!data?'Нет свежих данных игры':'Нужны данные игрового мода / плагина.'));return;}
 const wheels=vehicleWheels(v.wheels),axles=vehicleAxles(wheels),kind=Object.hasOwn(vehicleLabels,v.kind)?v.kind:'unknown',attachments=Array.isArray(v.attachments)?v.attachments.slice(0,32):[],farm=profileId==='fs25',linked=attachments.find(a=>a.parentId===v.id&&farmSprite(a.kind)&&(a.mount==='front'||a.mount==='rear'||a.kind==='header')),trailer=profileId==='ets2'?attachments.find(a=>a.kind==='trailer'):null;
 const geometry=JSON.stringify([v.id,v.name,kind,farm?axles:wheels,attachments.map(a=>[a.id,a.parentId,a.name,a.kind,a.mount,farm?null:a.wheels])]);
 if(panel.dataset.geometry!==geometry){
  panel.dataset.geometry=geometry;panel.replaceChildren(node('h2','caption',farm?'ТЕХНИКА И ОРУДИЯ':'СОСТОЯНИЕ МАШИНЫ'),node('h3','',String(v.name||'Неизвестная модель').slice(0,160)),node('small','','АВТО · '+vehicleLabels[kind]+' · '+(axles?axles+' оси':'Геометрия колёс неизвестна')));
  const scene=node('div','vehicleScene');if(profileId==='ets2')scene.append(node('div','vehicleWearLeft'));scene.append(farm?farmSvg(kind,linked):vehicleSvg(kind,wheels,trailer),node('div','vehicleWear'));if(profileId==='ets2')scene.classList.add('truckWearScene');panel.append(scene,node('small','hint','Иллюстрация класса · модель и состояния из игры'),node('p','hint vehicleFreshness',''));
  const stats=node('div','vehicleStats');panel.append(stats);
 }
 textIfVehicle();function textIfVehicle(){const label=panel.querySelector('.vehicleFreshness');if(label)label.textContent=stale?'Последняя известная техника · данные устарели':'Состояние техники · живые данные';}
 const implement=panel.querySelector('.vehicleImplement');if(implement)implement.style.transform=linked?.lowered===false?`translateY(-13.8px) rotate(${implement.dataset.front==='true'?12:-12}deg)`:'translateY(0) rotate(0)';
 const stats=panel.querySelector('.vehicleStats');stats.replaceChildren();
 for(const a of attachments){const item=node('div','vehicleAttachment');item.append(node('strong','','↳ '+String(a.name||vehicleLabels[a.kind]||'Орудие').slice(0,160)));if(farm&&a!==linked)item.append(farmSvg(a.kind,null));const values=[];if(typeof a.lowered==='boolean')values.push(a.lowered?'ОПУЩЕНО':'ПОДНЯТО');if(typeof a.turnedOn==='boolean')values.push(a.turnedOn?'РАБОТАЕТ':'ВЫКЛЮЧЕНО');if(Number.isFinite(a.fold)&&a.fold>=0&&a.fold<=1)values.push('Складывание '+Math.round(a.fold*100)+'%');item.append(node('small','',(stale?'Последнее: ':'')+(values.join(' · ')||'Состояние не передано игрой')));stats.append(item);}
 const wearArea=panel.querySelector('.vehicleWear'),wearLeft=panel.querySelector('.vehicleWearLeft');wearArea.replaceChildren();wearLeft?.replaceChildren();const drawing=panel.querySelector('.vehicleDrawing');drawing?.querySelector('.wearZones')?.remove();const zones=profileId==='ets2'&&drawing?vElement(drawing,'g',{class:'wearZones'}):null;
 const names={engine:'Двигатель',transmission:'Коробка',cabin:'Кабина',chassis:'Шасси',wheels:'Колёса'};let wear=false;if(v.wear)for(const[k,label]of Object.entries(names)){const value=v.wear[k];if(Number.isFinite(value)&&value>=0&&value<=1){wear=true;const row=node('div','wearRow');row.append(node('span','',label),node('strong','',Math.round(value*100)+'%'));const bar=node('progress');bar.max=1;bar.value=value;bar.className=value>.3?'danger':value>.05?'warning':'healthy';row.append(bar);((wearLeft&&['engine','transmission','wheels'].includes(k))?wearLeft:wearArea).append(row);if(zones){const h=trailer?250:290,bw=h*roadRect(kind)[2]/roadRect(kind)[3],region={engine:[.12,.2],cabin:[.03,.15],transmission:[.4,.17],chassis:[.64,.22]}[k];if(region)vElement(zones,'rect',{x:160-bw*.33,y:h*region[0],width:bw*.66,height:h*region[1],rx:4,fill:f1DamageColor(value*100),'fill-opacity':.23,stroke:f1DamageColor(value*100),'stroke-width':1});}}}panel.querySelector('.vehicleScene').classList.toggle('hasWear',wear);if(!farm&&!wear)stats.append(node('p','hint','Повреждения узлов: нет данных'));
}
let snowWheelCount=4;
function mountSnowVehicle(root){const panel=node('section','card vehiclePanel');panel.id='snowVehiclePanel';panel.append(node('h2','caption','ПРИВОД И БЛОКИРОВКА'),node('p','hint','Ручной выбор схемы · живая телеметрия пока недоступна'));const nav=node('div','snowWheelChoices'),scene=node('div');for(const count of [4,6,8,10]){const b=node('button','',count+' колёс');b.onclick=()=>{snowWheelCount=count;render();};nav.append(b);}panel.append(nav,scene,dashboardButtons([['snowAwd','ПОЛНЫЙ ПРИВОД'],['snowDifferential','БЛОКИРОВКА']]),node('p','hint','Состояния неизвестны · нажатие не считается подтверждением от игры'));root.append(panel);function render(){scene.replaceChildren(vehicleSvg(snowWheelCount===4?'suv':'truck',Array.from({length:snowWheelCount},(_,i)=>({x:i%2?-1:1,z:Math.floor(i/2),powered:null}))),node('p','',snowWheelCount+' колёс · '+snowWheelCount/2+' оси · иллюстрация'));nav.querySelectorAll('button').forEach((b,i)=>b.classList.toggle('selected',[4,6,8,10][i]===snowWheelCount));}render();}
function f1DamageColor(value){return !Number.isFinite(value)?'var(--p-muted)':value>30?'#ff575d':value>5?'#ffc449':'#7dda71';}
let f1DamageZones=null;
fetch('/f1-damage-zones.json').then(r=>{if(!r.ok)throw Error('F1 damage shapes');return r.json();}).then(source=>{f1DamageZones=source;const overview=$('f1VehicleOverview');if(overview)overview.dataset.snapshot='';}).catch(()=>{});
function f1DamageLevel(value){return !Number.isFinite(value)||value<0||value>100?null:value>30?2:value>5?1:0;}
function f1ZoneDamage(key,values){return key==='drsFault'?(values[key]===1?100:values[key]===0?values.rearWingDamage:null):values[key];}
function paintF1Damage(picture,values){
 if(!f1DamageZones)return;
 const bw=290*235/404,zones=vElement(picture,'g',{class:'f1DamageZones',transform:`translate(${160-bw/2} 0) scale(${bw/f1DamageZones.width} ${290/f1DamageZones.height})`});
 for(const zone of f1DamageZones.zones){const value=f1ZoneDamage(zone.key,values),level=f1DamageLevel(value);if(level===null)continue;const shape=vElement(zones,'path',{d:zone.path,fill:f1DamageColor(value),'fill-opacity':level===0?.18:.52,stroke:f1DamageColor(value),'stroke-opacity':.72,'stroke-width':.65});shape.dataset.damageZone=zone.key;shape.dataset.level=String(level);}
}
function f1Schematic(root,data){
 const v=data?.values||{},layout=node('div','f1Schematic'),left=node('div','tyreColumn'),right=node('div','tyreColumn'),car=node('div','formulaPicture'),picture=vehicleSvg('formula',[]);picture.setAttribute('viewBox','60 0 200 290');car.append(picture);
 const badges=node('div','wingBadges');for(const[key,label]of [['frontLeftWingDamage','ЛЕВОЕ ПЕРЕДНЕЕ КРЫЛО'],['frontRightWingDamage','ПРАВОЕ ПЕРЕДНЕЕ КРЫЛО']]){const damage=v[key],badge=node('div','wingBadge',label+' · '+fmt(damage,'%'));badge.style.color=f1DamageColor(damage);badges.append(badge);}root.append(badges);
 paintF1Damage(picture,v);
 const compoundIndex=[16,20].includes(v.compound)?0:[17,21].includes(v.compound)?1:[18,22].includes(v.compound)?2:v.compound===7?3:[8,15].includes(v.compound)?4:-1;
 for(const[i,name]of [[2,'ПЕРЕДНЯЯ ЛЕВАЯ'],[0,'ЗАДНЯЯ ЛЕВАЯ'],[3,'ПЕРЕДНЯЯ ПРАВАЯ'],[1,'ЗАДНЯЯ ПРАВАЯ']]){const w=data?.wheels?.[i],card=node('section','tyreCard'),title=node('h3','',name),mark=node('span','compoundMark',tyreLetters[compoundIndex]||'—');mark.style.setProperty('--tyre',tyreColors[compoundIndex]||'#abbdc7');mark.setAttribute('aria-label',compound(v.compound));title.append(mark);card.append(title);for(const[label,value,unit,max]of [['Износ',w?.wear,'%',100],['Поверхность',w?.surface,' °C',140],['Внутри',w?.inner,' °C',140],['Давление',w?.pressure,' PSI',null]]){const row=node('div','wearRow');row.append(node('span','',label),node('strong','',fmt(value,unit,unit===' PSI'?1:0)));if(max&&Number.isFinite(value)){const bar=node('progress');bar.max=max;bar.value=value;const color=unit==='%'?(value>50?'#ff575d':value>25?'#ffc449':'#7dda71'):(value>105?'#ff575d':value>90?'#ff9f32':value>=80?'#ffd945':'#61b8ff');bar.style.setProperty('--bar-color',color);if(unit===' °C')row.querySelector('strong').style.color=color;bar.className='tyreMetricBar';row.append(bar);}card.append(row);}card.append(node('small','hint','Тормоз '+fmt(w?.brake,' °C')+' · урон '+fmt(w?.damage,'%')));(i===2||i===0?left:right).append(card);}
 layout.append(left,car,right);root.append(layout);
 const footer=node('div','damageFooter');for(const[key,label]of [['rearWingDamage','ЗАДНЕЕ КРЫЛО'],['floorDamage','ДНИЩЕ'],['sidepodDamage','БОКОВИНЫ · ОБЩИЙ УРОН'],['drsFault','СОСТОЯНИЕ DRS']]){const c=node('div');c.append(node('small','',label));const value=node('strong','',key==='drsFault'?v[key]===1?'НЕИСПРАВЕН':v[key]===0?'ИСПРАВЕН':'НЕТ ДАННЫХ':fmt(v[key],'%'));value.dataset.damage=key;value.style.color=f1DamageColor(key==='drsFault'?v[key]===0||v[key]===1?v[key]*100:null:v[key]);c.append(value);footer.append(c);}root.append(footer);
}

function mountRacingClass(root){
 const panel=node('section','card vehiclePanel'),nav=node('div','snowWheelChoices'),scene=node('div');panel.append(node('h2','caption','АВТОМОБИЛЬ · ВЫБОР КЛАССА'),nav,scene,node('small','hint','Ручная схема класса · модель и состояния AMS2 пока не передаются'));root.append(panel);
 for(const [id,label] of [['formula','Формула'],['gt','GT'],['car','Кузов']]){const b=node('button','',label);b.type='button';b.onclick=()=>{scene.replaceChildren(vehicleSvg(id,[]));nav.querySelectorAll('button').forEach(el=>el.classList.toggle('selected',el===b));};nav.append(b);if(id==='formula')b.click();}
}
function renderTelemetryStatus(){
 let label=$('telemetryStatus');if(!label){label=node('p','telemetryStatus');label.id='telemetryStatus';label.setAttribute('role','status');$('deck').prepend(label);}
 const fresh=!!currentData(),unsupported=['ams2','snowrunner'].includes(profileId),old=!!displayData();
 const value=!session?'Связь с ПК потеряна · последние показания сохранены':unsupported?'Профиль управления · живая телеметрия этой игры пока не подключена':frame?.source==='demo'?'Демонстрационные данные · игровой ввод выключен':fresh?'● ЖИВЫЕ ДАННЫЕ · управление подключено отдельно':old?'Обновление задержалось · показаны последние данные. Обычные кнопки доступны при активной игре.':'Ожидание данных игры · обычные кнопки доступны при активной игре';
 if(label.textContent!==value)label.textContent=value;label.classList.toggle('delayed',!fresh&&!unsupported);document.body.classList.toggle('telemetryDelayed',!fresh&&!unsupported);document.querySelector('.controls')?.classList.toggle('overviewControls',page==='Обзор'&&!isF1());
}
function renderVehicleOverview(){
 let panel=$('f1VehicleOverview');if(!panel){panel=node('section','card');panel.id='f1VehicleOverview';$('deck').append(panel);}
 panel.hidden=!isF1()||f1Destination()!=='condition';if(panel.hidden)return;
 const data=displayData()?.f1,signature=JSON.stringify([profileId,data?.values,data?.wheels]);if(panel.dataset.snapshot===signature)return;panel.dataset.snapshot=signature;panel.replaceChildren(node('h2','','БОЛИД · ШИНЫ И ПОВРЕЖДЕНИЯ'));f1Schematic(panel,data);
}
