'use strict';
// These layouts share action IDs, sources and illustrations with ReferenceDashboard.kt.
function mountReferenceDashboard(root){
 if(!['fs25','ets2','ats','beamng-default','snowrunner'].includes(profileId))return false;
 root.classList.add('referenceDashboard');
 if(profileId==='beamng-default')return mountBeamNgDamage(root);
 const layout=node('div','referenceLayout'),left=node('div','referenceScene'),right=dashboardCard();layout.append(left,right);root.append(layout);
 if(profileId!=='snowrunner')mountVehiclePanel(left);
 if(profileId==='fs25'){
  const heading=dashboardMetric('СОСТОЯНИЕ ТЕХНИКИ','vehicle','referenceVehicle');right.append(heading);
  right.append(dashboardRow('ПЕРЕДНЯЯ СЦЕПКА','frontAttachment'),dashboardRow('ЗАДНЯЯ СЦЕПКА','rearAttachment'));
  for(const[label,key]of [['Положение орудия','fs25Lower'],['Рабочий режим','fs25TurnOn'],['Положение складывания','fold']])right.append(referenceStatusRow(label,key,key==='fold'?'fs25Fold':key));
  right.append(dashboardButtons([['fs25Lower','Поднять / опустить'],['fs25TurnOn','Включить / выключить'],['fs25Fold','Сложить / разложить']],'referenceWorkButtons'));
 }else if(isScsTruck()){
  const truckDetails=node('details','navTruckDetails'),truckSummary=node('summary','','Состояние грузовика и прицепа');truckDetails.append(truckSummary,left);root.append(truckDetails);layout.style.display='block';right.classList.add('navigatorCard');mountEtsMap(right);
  root.append(dashboardButtons([['etsCruiseDown','Круиз −'],['etsCruise','Круиз'],['etsCruiseUp','Круиз +'],['etsLights','Свет'],['etsMap','Карта в игре']],'referenceQuickBar'));
 }else if(profileId==='beamng-default'){
  right.classList.add('damageList');for(const[name,key,id]of [['Кузов','cabin','lights'],['Радиатор','radiator','ignition'],['Передняя подвеска','suspension','fourWheelDrive'],['Двигатель','engine','ignition'],['Трансмиссия','transmission','fourWheelDrive']])right.append(referenceStatusRow(name,'damage:'+key,id));
  right.append(node('p','hint','Состояния узлов требуют расширения мода BeamNG. Серый цвет означает отсутствие данных.'));
  root.append(dashboardButtons([['ignition','Зажигание'],['lights','Фары'],['hazards','Аварийка'],['recoverRoad','Восстановить']],'referenceQuickBar'));
 }else{
  left.append(node('h2','','ПРИВОД И БЛОКИРОВКА'));const choices=node('nav','snowWheelChoices'),scene=node('div');left.append(choices,scene,node('small','hint','Ручной выбор схемы · живая телеметрия пока недоступна'));
  function draw(n){scene.replaceChildren(vehicleSvg(n===4?'suv':'truck',Array.from({length:n},(_,i)=>({x:i%2?-1:1,z:Math.floor(i/2),powered:null}))));choices.querySelectorAll('button').forEach((b,i)=>b.classList.toggle('selected',[4,6,8,10][i]===n));}
  for(const n of [4,6,8,10]){const b=node('button','',n+' колёс');b.onclick=()=>draw(n);choices.append(b);}draw(6);
  right.classList.add('transmissionStates');for(const[id,label]of [['snowAwd','ПОЛНЫЙ ПРИВОД'],['snowDifferential','БЛОКИРОВКА ДИФФЕРЕНЦИАЛА']]){const c=dashboardCard(),icon=actionIcon(id);if(icon)c.append(icon);c.append(node('h2','',label),node('strong','unknownState','СОСТОЯНИЕ НЕИЗВЕСТНО'),dashboardButtons([[id,id==='snowAwd'?'Включить / выключить AWD':'Переключить блокировку']],'referenceWorkButtons'));right.append(c);}
  right.append(node('p','hint','Игра пока не передаёт телеметрию. Нажатие не подтверждает состояние трансмиссии.'));
  root.append(dashboardButtons(profileQuick.snowrunner[1],'referenceQuickBar'));
 }
 const strip=node('div','referenceReadouts');for(const[label,key]of [['СКОРОСТЬ','speed'],['ПЕРЕДАЧА','gear'],['ОБОРОТЫ','rpm'],['УПРАВЛЕНИЕ','input']])strip.append(dashboardMetric(label,key));root.append(strip);return true;
}
function referenceStatusRow(label,key,id){const row=node('div','referenceStatusRow'),icon=actionIcon(id);if(icon)row.append(icon);const text=node('div');text.append(node('span','',label));const state=node('strong','','НЕТ ДАННЫХ');state.dataset.metric=key;text.append(state);row.append(text);return row;}
function updateReferenceDashboard(data){
 const root=$('profileDashboard');if(!root?.classList.contains('referenceDashboard'))return;
 if(profileId==='beamng-default'){updateBeamNgDamage(data);return;}
 updateEtsMap(data);
 const v=data?.vehicle||lastKnownVehicle;
 for(const el of root.querySelectorAll('[data-metric="frontAttachment"],[data-metric="rearAttachment"],[data-metric="fold"],[data-metric^="damage:"]')){
  let value='НЕТ ДАННЫХ';if(el.dataset.metric.endsWith('Attachment')){const mount=el.dataset.metric==='frontAttachment'?'front':'rear';value=v?.controlled===true?((v.attachments||[]).filter(a=>a.mount===mount).map(a=>a.name).join(', ')||'Свободна'):'—';}
  else if(el.dataset.metric==='fold'){const f=v?.attachments?.find(a=>Number.isFinite(a.fold))?.fold;value=Number.isFinite(f)?fmt(f*100,'%'):'НЕТ ДАННЫХ';}
  else{const x=v?.wear?.[el.dataset.metric.slice(7)];value=Number.isFinite(x)?'Урон '+fmt(x*100,'%'):'Нет данных узла';el.style.color=Number.isFinite(x)?x>.3?'#ff575d':x>.05?'#ffc449':'#7dda71':'var(--p-muted)';}
  if(el.textContent!==value)el.textContent=value;
 }
}
