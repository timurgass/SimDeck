'use strict';
let snowManualWheels=6;
function mountSnowRunnerDashboard(root){
 const layout=node('div','referenceLayout snowLayout'),scene=node('section','card snowScene'),right=node('div','snowRight');layout.append(scene,right);root.append(layout);
 scene.append(node('h2','caption','ТЕХНИКА И ТРАНСМИССИЯ'),node('h3','snowName','Выберите схему машины'),node('small','snowIdentity',''));
 const choices=node('nav','snowWheelChoices');choices.setAttribute('aria-label','Ручная схема машины');for(const n of [4,6,8,10]){const b=node('button','',n+' колёс');b.onclick=()=>{snowManualWheels=n;scene.dataset.geometry='';updateSnowRunnerDashboard(displayData());};choices.append(b);}scene.append(choices,node('div','snowPicture'));
 const gauges=node('div','snowGauges');for(const[label,key]of [['СКОРОСТЬ','speed'],['ТОПЛИВО','snowFuel']])gauges.append(dashboardMetric(label,key));scene.append(gauges,node('p','hint snowFreshness','Иллюстрация класса'));
 const states=node('div','snowStateGrid');for(const[id,label]of [['snowAwd','ПОЛНЫЙ ПРИВОД'],['snowDifferential','БЛОКИРОВКА']]){
  const card=dashboardCard(),icon=actionIcon(id);if(icon)card.append(icon);card.append(node('h2','',label));const status=node('strong','snowState','НЕИЗВЕСТНО');status.dataset.snowAction=id;card.append(status,dashboardButtons([[id,'Переключить']],'referenceWorkButtons'));states.append(card);
 }
 right.append(states,node('p','hint','Переключаемая блокировка работает на L. Доступность зависит от оборудования машины.'),node('section','card snowCondition'));
 root.append(dashboardButtons(profileQuick.snowrunner[1],'referenceQuickBar'));
 const strip=node('div','referenceReadouts');for(const[label,key]of [['ПЕРЕДАЧА','gear'],['ДВИГАТЕЛЬ','snowEngine'],['УПРАВЛЕНИЕ','input']])strip.append(dashboardMetric(label,key));root.append(strip);updateSnowRunnerDashboard(displayData());return true;
}
function snowRunnerDrawing(kind,wheels,awd,diff){
 const rect=roadRect(kind)||roadRect(wheels.length===4?'suv':'truck'),svg=vehicleSvgRoot(310,'Схема трансмиссии'),h=290,bw=h*rect[2]/rect[3],left=160-bw/2;
 const defs=vElement(svg,'defs'),clip=vElement(defs,'clipPath',{id:'snowBodyClip'});
 for(const[x,y,w,hh]of [[left,0,bw,h*.18],[left+bw*.17,0,bw*.66,h],[left,h*.91,bw,h*.09]])vElement(clip,'rect',{x,y,width:w,height:hh});
 const body=vElement(svg,'g',{'clip-path':'url(#snowBodyClip)'});vehicleSprite(body,'road',rect,left,0,bw,h);
 const color=awd===true?'var(--p-accent)':'#83929b';vElement(svg,'path',{d:`M160 ${h*.2}V${h*.84}`,stroke:color,'stroke-width':1.8,'stroke-opacity':.75});
 const min=Math.min(...wheels.map(w=>w.z)),span=Math.max(1,Math.max(...wheels.map(w=>w.z))-min),axles=[...new Set(wheels.map(w=>w.z))].sort((a,b)=>a-b),gaps=axles.slice(1).map((z,i)=>(z-axles[i])/span*.65),th=h*Math.max(.075,Math.min(.15,gaps.length?Math.min(...gaps):.18)),tw=bw*.14;
 for(const w of wheels){const x=160+(w.x<0?-1:1)*bw*.43,y=h*(.2+(w.z-min)/span*.64);vElement(svg,'path',{d:`M160 ${y}H${x}`,stroke:color,'stroke-width':1.4});const g=vElement(svg,'g',{class:'snowTyre'});vElement(g,'rect',{x:x-tw/2,y:y-th/2,width:tw,height:th,rx:tw*.3,fill:'#10181b',stroke:color,'stroke-width':.9});for(let j=1;j<=5;j++)vElement(g,'path',{d:`M${x-tw*.3} ${y-th/2+th*j/6}H${x+tw*.3}`,stroke:'#34434b','stroke-width':.8});}
 if(diff===true)for(const z of axles)vElement(svg,'circle',{cx:160,cy:h*(.2+(z-min)/span*.64),r:3,fill:'var(--p-accent)'});
 return svg;
}
function updateSnowRunnerDashboard(data){
 const root=$('profileDashboard');if(profileId!=='snowrunner'||!root?.querySelector('.snowScene'))return;
 const v=data?.vehicle||lastKnownVehicle,scene=root.querySelector('.snowScene'),actual=vehicleWheels(v?.wheels),wheels=actual.length?actual:Array.from({length:snowManualWheels},(_,i)=>({x:i%2?-1:1,z:Math.floor(i/2)}));
 const awd=data?.actionStates?.snowAwd,diff=data?.actionStates?.snowDifferential,kind=v?.kind|| (wheels.length===4?'suv':'truck');
 scene.querySelector('.snowName').textContent=v?.name||'Выберите схему машины';scene.querySelector('.snowIdentity').textContent=actual.length?`АВТО · ${actual.length} колёс · ${v.axleCount??actual.length/2} оси`:'Ручная схема · данные машины пока не получены';
 scene.querySelector('.snowWheelChoices').hidden=actual.length>0;
 const signature=JSON.stringify([kind,wheels,awd,diff]);if(scene.dataset.geometry!==signature){scene.dataset.geometry=signature;scene.querySelector('.snowPicture').replaceChildren(snowRunnerDrawing(kind,wheels,awd,diff));}
 scene.querySelector('.snowFreshness').textContent=actual.length?(currentData()?'Иллюстрация класса · число осей и параметры из игры':'Последняя машина · обновление задержалось'):'Нужны данные игры · нажатия доступны отдельно';
 for(const el of root.querySelectorAll('[data-snow-action]')){const on=data?.actionStates?.[el.dataset.snowAction];el.textContent=on===true?'ВКЛЮЧЕНО':on===false?'ВЫКЛЮЧЕНО':'НЕИЗВЕСТНО';el.style.color=on===true?'var(--p-accent)':'var(--p-muted)';}
 const fuel=scene.querySelector('[data-metric="snowFuel"]');fuel.textContent=Number.isFinite(data?.fuelLiters)&&Number.isFinite(data?.snowRunner?.fuelCapacity)?`${Math.round(data.fuelLiters)} / ${Math.round(data.snowRunner.fuelCapacity)} л`:'—';
 const engine=root.querySelector('[data-metric="snowEngine"]');engine.textContent=data?.actionStates?.snowEngine===true?'РАБОТАЕТ':data?.actionStates?.snowEngine===false?'ОСТАНОВЛЕН':'—';
 const components=data?.snowRunner?.components||[],condition=root.querySelector('.snowCondition'),key=JSON.stringify(components);if(condition.dataset.snapshot===key)return;condition.dataset.snapshot=key;condition.replaceChildren(node('h2','','ЗАПАС ПРОЧНОСТИ'));
 if(!components.length)condition.append(node('p','hint','Ожидание диагностики машины'));
 for(const c of components){if(!Number.isFinite(c.damage)||!Number.isFinite(c.capacity)||c.capacity<=0||c.damage<0||c.damage>c.capacity)continue;const row=node('div','snowComponent'),head=node('div'),value=node('strong','',`${c.capacity-c.damage} / ${c.capacity}`),color=f1DamageColor(c.damage/c.capacity*100);value.style.color=color;head.append(node('span','',c.name),value);const bar=node('progress');bar.max=c.capacity;bar.value=c.capacity-c.damage;bar.style.setProperty('--bar-color',color);row.append(head,bar,node('small','hint',`Повреждение ${Math.round(c.damage/c.capacity*100)}%`));condition.append(row);}
}
