'use strict';
// Same compositions and action IDs as Android/ProfileDashboard.kt. Values never come from mockups.
const profileNames={'f1-24':'F1 24','f1-25':'F1 25','beamng-default':'BEAMNG',acc:'ACC',ams2:'AMS2',ets2:'ETS2',ats:'ATS',snowrunner:'SNOWRUNNER',fs25:'FS25'};
const profileQuick={
 'beamng-default':['БЫСТРОЕ УПРАВЛЕНИЕ',[['ignition','ЗАЖИГАНИЕ'],['lights','ФАРЫ'],['esc','ESC / TCS'],['fourWheelDrive','ПРИВОД'],['hazards','АВАРИЙКА'],['recoverRoad','НА ДОРОГУ']]],
 ams2:['БЫСТРЫЕ ДЕЙСТВИЯ',[['amsIcmCycle','ICM'],['amsRequestPit','ПИТ-СТОП'],['amsPitLimiter','ЛИМИТЕР'],['amsCamera','КАМЕРА']]],
 ets2:['КАБИНА И ТРАНСМИССИЯ',[['etsLights','ФАРЫ'],['etsHighBeam','ДАЛЬНИЙ'],['etsDifferential','БЛОКИРОВКА'],['etsParkingBrake','РУЧНИК'],['etsHorn','СИГНАЛ'],['etsCamera','КАМЕРА']]],
 snowrunner:['ПОЛЕВЫЕ ДЕЙСТВИЯ',[['snowQuickWinch','ЛЕБЁДКА'],['snowPackCargo','ГРУЗ'],['snowMap','КАРТА'],['snowRecover','ЭВАКУАЦИЯ']]],
 fs25:['РАБОТА С ОРУДИЕМ',[['fs25Lower','ОПУСТИТЬ / ПОДНЯТЬ'],['fs25TurnOn','ВКЛ / ВЫКЛ'],['fs25Attach','ПРИЦЕПИТЬ / ОТЦЕПИТЬ'],['fs25Fold','СЛОЖИТЬ / РАЗЛОЖИТЬ']]]
};
let dashboardProfile='',dashboardRevision=-1;
function profileIconPath(id){
 if(/^(ignition)$|Ignition|Starter|Engine$/.test(id))return ets2IconPaths.etsEngine;
 if(/^(lights)$|Headlights|Flash|RainLight/.test(id))return ets2IconPaths.etsLights;
 if(/^(fourWheelDrive|differentials|esc)$|Differential|Awd$/.test(id))return ets2IconPaths.etsDifferential;
 if(id==='hazards')return ets2IconPaths.etsHazards;
 if(/Wipers/.test(id))return ets2IconPaths.etsWipers;
 if(/Camera/.test(id))return ets2IconPaths.etsCamera;
 if(/Horn|^horn$/.test(id))return ets2IconPaths.etsHorn;
 if(/ParkingBrake/.test(id))return ets2IconPaths.etsParkingBrake;
 if(/Winch/.test(id))return 'M8 8H40V38H8Z M12 16H36 M12 22H36 M12 28H36 M24 28V42 C24 47 33 47 33 40';
 if(/Map/.test(id))return ets2IconPaths.etsMap;
 if(/recoverRoad|snowRecover/.test(id))return 'M11 18 C14 7 35 7 39 23 M39 23V11 M39 23H27 M37 31 C33 42 12 42 8 26 M8 26V38 M8 26H20';
 if(/Pit|Icm|Cargo/.test(id))return ets2IconPaths.etsRouteAdvisor;
 return null;
}
function dashboardMetric(label,key,cls=''){const cell=node('div',cls);cell.append(node('small','caption',label));const v=node('strong','reading','—');v.dataset.metric=key;cell.append(v);return cell;}
function dashboardRow(label,key){const row=node('div','stateRow');row.append(node('span','',label));const value=node('b','','—');value.dataset.metric=key;row.append(value);return row;}
function dashboardCard(title){const card=node('section','card profileCard');if(title)card.append(node('h2','caption',title));return card;}
function dashboardButtons(entries,cls='quickTiles'){const grid=node('div',cls);for(const [id,label] of entries){const a=action(id);if(a)grid.append(actionButton(a,label,a.gesture==='hold'?'Удерживать · '+a.key:a.key));}return grid;}
function mountProfileDashboard(){
 text('brandGame',' / '+(profileNames[profileId]||''));
 document.body.classList.toggle('profileDeck',!isF1());
 let root=$('profileDashboard');if(!root){root=node('section');root.id='profileDashboard';$('deck').prepend(root);}
 root.hidden=isF1()||page!=='Обзор';document.querySelector('header').append(isF1()?$('f1Nav'):$('pages'));$('pages').hidden=isF1();$('f1Nav').hidden=!isF1();
 let details=$('allControls');if(!details){details=node('details','allControls');details.id='allControls';details.append(node('summary','','Все действия и пользовательские кнопки'));$('deck').append(details);}
 let farmDetails=$('farmDetails');if(!farmDetails){farmDetails=node('details','farmDetails');farmDetails.id='farmDetails';farmDetails.open=true;farmDetails.append(node('summary','','Хозяйство · поля · советник · план'));$('deck').append(farmDetails);farmDetails.append($('fs25Overview'));}
 details.hidden=true;farmDetails.hidden=profileId!=='fs25'||page!=='Хозяйство';
 let gameNav=$('fs25GameNav');if(!gameNav){gameNav=node('div','twoGrid');gameNav.id='fs25GameNav';$('deck').prepend(gameNav);}gameNav.hidden=profileId!=='fs25';clear(gameNav);
 if(profileId==='fs25')for(const [id,label]of [['fs25Back','← Назад в игре'],['fs25Pause','Пауза времени']]){const a=action(id);if(a)gameNav.append(actionButton(a,label));}

 if(isF1()){$('deck').append(document.querySelector('.controls'));return;}
 $('deck').insertBefore(document.querySelector('.controls'),root);
 if(dashboardProfile===profileId&&dashboardRevision===revision)return;
 dashboardProfile=profileId;dashboardRevision=revision;clear(root);root.classList.remove('referenceDashboard');
 if(mountReferenceDashboard(root))return;
 const banner=node('div','profileBanner');root.append(banner);
 document.querySelector('.controls').classList.toggle('overviewControls',page==='Обзор');
 banner.hidden=['beamng-default','acc','fs25','snowrunner'].includes(profileId);
 if(['ams2','snowrunner','fs25'].includes(profileId)){
  const info=node('div');info.append(node('small','caption',profileId==='fs25'?'ХОЗЯЙСТВО':profileNames[profileId]),node('h1','',({ams2:'Гонка под контролем.',snowrunner:'Любая дорога начинается здесь.',fs25:'Хороший день для работы.'})[profileId]));
  const sub=node('small','',({ams2:'Важные действия под рукой',snowrunner:'Трансмиссия · лебёдка · груз',fs25:'Сохраните ферму в игре'})[profileId]);if(profileId==='fs25')sub.dataset.metric='farm';info.append(sub);banner.append(info);
  if(profileId==='snowrunner'){const compass=node('div','compass');compass.innerHTML='<svg viewBox="0 0 80 80" aria-hidden="true"><path d="M40 12 58 62 40 52 22 62Z"/></svg><small>СЕВЕР</small>';banner.append(compass);}
 }else if(isScsTruck()){banner.append(dashboardMetric('МАРШРУТ ИЗ ИГРЫ','route','routeReading'),dashboardMetric('ЛИМИТ','limit'));}
 else{banner.append(node('strong','caption',profileId==='acc'?'GT COCKPIT · ACC':'VEHICLE CONTROL UNIT'));const status=node('span','caption');status.dataset.metric='live';banner.append(status);}
 const layout=node('div','profileLayout'),stack=node('div','vehicleStack'),left=dashboardCard(),right=dashboardCard();mountVehiclePanel(stack);stack.append(left);layout.append(stack,right);root.append(layout);
 if(['beamng-default','acc'].includes(profileId)){const live=node('span','instrumentLive');live.dataset.metric='live';left.append(live);}
 switch(profileId){
  case 'beamng-default': {
   const mode=node('small','caption');mode.dataset.metric='mode';left.append(mode);
   const meters=node('div','beamMeters'),dial=node('div','beamDial'),ring=dashboardMetric('КМ/Ч','speed','beamRing'),gear=dashboardMetric('ПЕРЕДАЧА','gear','beamGear');dial.append(ring);const rpm=node('small');rpm.dataset.metric='rpm';gear.append(rpm);meters.append(dial,gear);left.append(meters,dashboardRow('Двигатель','ignition'),dashboardRow('Топливо','fuel'));break;
  }
  case 'acc': {
   left.append(dashboardMetric('ОБОРОТЫ ДВИГАТЕЛЯ','rpm','accRpm'));const strip=node('div','profileRpm');strip.innerHTML='<i></i>'.repeat(10);left.append(strip);const speed=node('div','accSpeed');speed.append(dashboardMetric('ПЕРЕДАЧА','gear'),dashboardMetric('КМ/Ч','speed'));left.append(speed,dashboardButtons([['accPitLimiter','ЛИМИТЕР'],['accHeadlights','ФАРЫ'],['accWipers','ДВОРНИКИ'],['accRainLight','ДОЖД. СВЕТ']],'quickTiles accSwitches'));
   right.append(node('h2','caption','ШИНЫ И ТОРМОЗА'),vehicleSvg('gt',[]));const tyres=node('div','accTyres');for(let i=0;i<4;i++){const card=node('div');card.append(node('small','caption',['ПЕРЕДНЯЯ ЛЕВАЯ','ПЕРЕДНЯЯ ПРАВАЯ','ЗАДНЯЯ ЛЕВАЯ','ЗАДНЯЯ ПРАВАЯ'][i]));const temp=node('strong','reading');temp.dataset.metric='tyre'+i;const extra=node('small');extra.dataset.metric='tyreExtra'+i;card.append(temp,extra);tyres.append(card);}const engine=node('small');engine.dataset.metric='engine';right.append(tyres,engine,dashboardButtons([['accRequestPit','ПИТ-СТРАТЕГИЯ']],'pitStrategy'));break;
  }
  case 'ams2':left.append(node('h2','caption','ПРИБОРЫ'),dashboardRow('Скорость','speed'),dashboardRow('Передача','gear'),dashboardRow('Обороты','rpm'));{const strip=node('div','profileRpm');strip.innerHTML='<i></i>'.repeat(10);left.append(strip);}break;
  case 'ats': case 'ets2': {
   left.classList.add('etsGauges');const dial=dashboardMetric('СКОРОСТЬ','speed','etsDial');dial.append(node('small','caption','КМ/Ч'));const trip=node('div','etsTrip');trip.append(dashboardMetric('КРУИЗ-КОНТРОЛЬ','etsCruise'),dashboardButtons([['etsCruiseDown','−'],['etsCruise','КРУИЗ'],['etsCruiseUp','+']],'cruiseButtons'),dashboardRow('Передача','gear'),dashboardRow('Топливо','fuel'));left.append(dial,trip);break;
  }
  case 'snowrunner':left.append(node('h2','caption','ТРАНСМИССИЯ'),dashboardMetric('ТЕКУЩАЯ ПЕРЕДАЧА','gear','snowGear'),dashboardRow('Топливо','fuel'));{const fuel=node('progress');fuel.max=1;fuel.dataset.metric='fuelBar';left.append(fuel,node('p','hint','Профиль кнопок. Живая телеметрия SnowRunner пока недоступна.'));}break;
  case 'fs25': {
   const vehicle=node('div','farmVehicle'),icon=actionIcon('fs25Motor');if(icon)vehicle.append(icon);vehicle.append(dashboardMetric('ВЫБРАННАЯ ТЕХНИКА','vehicle'));left.append(vehicle,dashboardRow('Положение орудия','fs25Lower'),dashboardRow('Рабочий режим','fs25TurnOn'),dashboardRow('Двигатель','fs25Motor'),dashboardRow('Задачи плана','tasks'));const bar=node('progress');bar.max=1;bar.dataset.metric='tasksBar';left.append(bar);break;
  }
 }
 if(profileQuick[profileId]){right.append(node('h2','caption',profileQuick[profileId][0]),dashboardButtons(profileQuick[profileId][1]));}
 root.append(dashboardRow('Управление','input'));const result=node('small');result.dataset.metric='command';root.append(result);
}
function updateProfileDashboard(data){
 updateVehiclePanel(data);
 if(isF1())return;const root=$('profileDashboard');if(!root)return;
 const toggle=(id,on='ВКЛ',off='ВЫКЛ')=>typeof data?.actionStates?.[id]==='boolean'?(data.actionStates[id]?on:off):'—';
 const farm=frame?.data?.fs25,advisor=frame?.data?.fs25Advisor,tasks=advisor?.tasks||[],done=tasks.filter(t=>t.status===1).length;
 const values={speed:data?Math.round(data.speedMps*3.6):'—',gear:data?.gearDisplay??'—',rpm:data?Math.round(data.rpm)+' RPM':'— RPM',fuel:data?.fuelFraction!=null?Math.round(data.fuelFraction*100)+'%':'—',fuelBar:data?.fuelFraction??0,
  live:frame?.source==='demo'?'● ДЕМО':currentData()?'● LIVE':data?'● ПОСЛЕДНИЕ ДАННЫЕ':'● ОЖИДАНИЕ',mode:'МАШИНА · '+({arcade:'АРКАДА',realistic:'РЕАЛИЗМ'}[data?.gearboxMode]||'—'),ignition:toggle('ignition','Работает','Выключен'),etsCruise:toggle('etsCruise'),route:fmt(data?.ets2Navigation?.remainingKm,' км')+' · '+fmt(data?.ets2Navigation?.remainingMinutes,' мин'),limit:fmt(data?.ets2Navigation?.speedLimitKmh,' км/ч'),
  farm:farm?[farm.header?.savegameName,farm.header?.mapTitle,farm.environment?.period?.russianMonth].filter(Boolean).join(' · '):'Сохраните ферму в игре',vehicle:(data?.vehicle||lastKnownVehicle)?.controlled?(data?.vehicle||lastKnownVehicle).name:'Ждём технику',fs25Lower:toggle('fs25Lower','Опущено','Поднято'),fs25TurnOn:toggle('fs25TurnOn','Работает','Выключен'),fs25Motor:toggle('fs25Motor','Запущен','Остановлен'),tasks:tasks.length?done+' / '+tasks.length:'—',tasksBar:tasks.length?done/tasks.length:0,engine:data?.acc?'Двигатель '+fmt(data.acc.waterTemperature,' °C')+' · трасса '+fmt(data.acc.roadTemperature,' °C'):'Двигатель — · трасса —',input:({ready:'● ВВОД ГОТОВ',unfocused:'Открой окно игры на ПК',disabled:'Разреши ввод в Companion',demo:'Демо · ввод отключён'})[availability]||'Ожидание подключения',command:$('command').textContent};
 for(let i=0;i<4;i++){const w=data?.acc?.wheels?.[i];values['tyre'+i]=fmt(w?.coreTemperature,' °C');values['tyreExtra'+i]=fmt(w?.pressure,' PSI',1)+' · тормоз '+fmt(w?.brakeTemperature,' °C');}
 for(const el of root.querySelectorAll('[data-metric]')){const value=values[el.dataset.metric];if(el.tagName==='PROGRESS')el.value=value||0;else el.textContent=value??'—';}
 const fraction=Math.max(0,Math.min(1,(data?.rpm||0)/(data?.maxRpm||8000)));root.querySelectorAll('.profileRpm i').forEach((el,i)=>el.classList.toggle('filled',i<fraction*10));root.style.setProperty('--speed-sweep',Math.max(0,Math.min(270,(data?.speedMps||0)*3.6/240*270))+'deg');
}

profileQuick.ats=profileQuick.ets2;
