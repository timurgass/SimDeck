'use strict';
const vehicleLabels={unknown:'Общая схема',car:'Легковая',suv:'Внедорожник',pickup:'Пикап',van:'Фургон',bus:'Автобус',truck:'Грузовик / тягач',trailer:'Прицеп',tractor:'Трактор',combine:'Комбайн',loader:'Погрузчик',telehandler:'Телескопический погрузчик',forestry:'Лесная техника',sprayer:'Опрыскиватель',tracked:'Гусеничная техника',implement:'Орудие'};
function vehicleWheels(w){return Array.isArray(w)&&w.length<=32&&w.every(p=>Number.isFinite(p.x)&&Number.isFinite(p.z)&&Math.abs(p.x)<=100&&Math.abs(p.z)<=100)?w:[];}
function vehicleAxles(w){const axles=[];for(const z of w.map(w=>w.z).sort((a,b)=>a-b))if(!axles.length||z-axles.at(-1)>.35)axles.push(z);return axles.length||null;}
function vehicleSvg(kind,wheels,trailer=null,lowered=null){
 const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.setAttribute('viewBox','0 0 320 235');svg.setAttribute('role','img');svg.setAttribute('aria-label',vehicleLabels[kind]||vehicleLabels.unknown);svg.classList.add('vehicleDrawing');
 const element=(tag,attrs)=>{const e=document.createElementNS(svg.namespaceURI,tag);for(const [k,v]of Object.entries(attrs))e.setAttribute(k,String(v));svg.append(e);return e;};
 const bw=['bus','van','truck','trailer'].includes(kind)?72:92,left=160-bw/2;
 const rect=(x,y,width,height,cls)=>element('rect',{x,y,width,height,class:cls});
 rect(left,20,bw,195,'vehicleBody');
 if(kind==='unknown')element('path',{d:`M${left} 20L${left+bw} 215`,class:'vehicleLine'});
 else if(['truck','tractor','loader','telehandler','forestry'].includes(kind)){
  rect(left-9,42,bw+18,55,'vehicleCab');if(['loader','telehandler','forestry'].includes(kind))element('path',{d:'M160 55L130 8',class:'vehicleArm'});
 }else if(kind==='combine')rect(80,20,160,20,'vehicleImplement');
 else if(kind==='sprayer')element('path',{d:'M50 110H270',class:'vehicleArm'});
 else if(kind==='tracked'){rect(left-20,45,16,130,'vehicleTrack');rect(left+bw+4,45,16,130,'vehicleTrack');}
 else if(kind==='implement')rect(80,100,160,35,'vehicleImplement '+(lowered===true?'lowered':''));
 else rect(left+8,55,bw-16,40,'vehicleCab');
 const drawWheels=(list,from,to)=>{list=vehicleWheels(list);if(!list.length)return;const min=Math.min(...list.map(w=>w.z)),span=Math.max(1,Math.max(...list.map(w=>w.z))-min),half=Math.max(.5,...list.map(w=>Math.abs(w.x)));for(const w of list)rect(160+w.x/half*(bw/2+9)-7,from+(w.z-min)/span*(to-from)-12,14,24,w.powered===true?'vehicleWheel powered':'vehicleWheel');};
 drawWheels(wheels,50,185);
 if(trailer!==null){rect(108,102,104,123,'vehicleGhost');drawWheels(trailer,175,215);}
 return svg;
}
function mountVehiclePanel(root){if(!['fs25','ets2','beamng-default'].includes(profileId))return;const panel=node('section','dashboardCard vehiclePanel');panel.id='vehiclePanel';root.append(panel);}
function updateVehiclePanel(data){
 const panel=$('vehiclePanel');if(!panel)return;
 const v=data?.vehicle,signature=JSON.stringify(v||null);if(panel.dataset.snapshot===signature)return;panel.dataset.snapshot=signature;panel.replaceChildren(node('h2','caption','ТЕХНИКА · АВТОМАТИЧЕСКИЙ ВЫБОР'));
 if(!v||v.controlled===false){panel.append(node('h3','',v?.controlled===false?'Вы не в технике':'Ждём сведения о технике'),node('p','hint',!data?'Нет свежих данных игры':'FS25 / BeamNG: нужен обновлённый мод. ETS2: данные из телеметрического плагина.'));return;}
 const wheels=vehicleWheels(v.wheels),axles=vehicleAxles(wheels),kind=Object.hasOwn(vehicleLabels,v.kind)?v.kind:'unknown';
 panel.append(node('h3','',String(v.name||'Неизвестная модель').slice(0,160)),node('small','',vehicleLabels[kind]+' · '+(axles?axles+' оси':'Колёса не переданы')));
 const attachments=Array.isArray(v.attachments)?v.attachments.slice(0,32):[],trailer=profileId==='ets2'?attachments.find(a=>a.kind==='trailer'):null;
 panel.append(vehicleSvg(kind,wheels,trailer?vehicleWheels(trailer.wheels):null),node('small','hint','Схема класса техники · светлые колёса — ведущие'));
 for(const a of attachments){const item=node('div','vehicleAttachment');item.append(node('strong','','↳ '+String(a.name||vehicleLabels[a.kind]||'Орудие').slice(0,160)));if(a!==trailer)item.append(vehicleSvg(a.kind,vehicleWheels(a.wheels),null,a.lowered));const values=[];if(typeof a.lowered==='boolean')values.push(a.lowered?'Опущено':'Поднято');if(typeof a.turnedOn==='boolean')values.push(a.turnedOn?'Работает':'Выключено');if(Number.isFinite(a.fold)&&a.fold>=0&&a.fold<=1)values.push('Положение складывания: '+Math.round(a.fold*100)+'%');item.append(node('small','',values.join(' · ')||'Состояние не передано игрой'));panel.append(item);}
 const names={engine:'Двигатель',transmission:'Коробка',cabin:'Кабина',chassis:'Шасси',wheels:'Колёса'};if(v.wear)for(const [k,label]of Object.entries(names)){const value=v.wear[k];if(Number.isFinite(value)&&value>=0&&value<=1)panel.append(node('p','',label+': '+Math.round(value*100)+'% износа / повреждений'));}
}
