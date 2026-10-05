// Use an isolated --ets2-map-preview host. Routes come from the actual importer,
// not mocked geometry; moving telemetry below is deliberately simulated.
const {chromium,webkit}=require('playwright');
const fs=require('fs');
const base=process.argv[2]||'http://127.0.0.1:28787',pin=fs.readFileSync(process.argv[3],'utf8').trim(),out=process.argv[4]||'artifacts';
(async()=>{
 const engine=process.env.SIMDECK_BROWSER==='webkit'?webkit:chromium;
 const browser=await engine.launch({headless:true,...(process.env.SIMDECK_BROWSER_EXECUTABLE?{executablePath:process.env.SIMDECK_BROWSER_EXECUTABLE}:{})});
 const results=[],context=await browser.newContext();let paired=false;
 for(const [width,height]of [[390,844],[800,1280],[1340,800]]){
  const page=await context.newPage(),errors=[];page.setDefaultTimeout(45000);await page.setViewportSize({width,height});console.log('Checking navigator at '+width);
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto(base);if(!paired){await page.locator('#code').fill(pin);await page.locator('#pairForm button').click();paired=true;}
  await page.locator('.sdnav').waitFor({state:'visible'});
  await page.waitForFunction(()=>etsMapState.navigator?.map,{},{timeout:60000});
  if(process.env.SIMDECK_GPS_QA_FIXTURE){
   await page.evaluate(()=>etsMapState.navigator.useGameGps());
   await page.waitForFunction(()=>etsMapState.navigator?.plan?.source==='game-gps');
   const gps=await page.evaluate(()=>{const n=etsMapState.navigator;return{points:n.plan.points.length,revision:n.gameRevision,source:n.source.textContent};});
   if(gps.points<10||!gps.revision||!gps.source.includes('Путь: GPS игры'))throw Error('Actual GPS snapshot did not become the displayed route');
   if(process.env.SIMDECK_GPS_MARKER_QA==='1'){
    const hit=await page.evaluate(()=>{const n=etsMapState.navigator,p=n.plan.stops[0];if(!p)return false;n.focusPoint(p);const s=n.toScreen(p.x,p.z);return n.hitPoi(s.x,s.y)?.id===p.id;});
    if(!hit)throw Error('Game waypoint marker missing or cannot be opened');
    await page.getByRole('button',{name:'Маршрут и поиск',exact:true}).click();
    await page.getByRole('button',{name:'На карте GPS-точка 1',exact:true}).click();
    if(await page.locator('.nav-panel').isVisible())throw Error('Focus marker did not close the route panel');
    await page.evaluate(()=>etsMapState.navigator.open('place',etsMapState.navigator.plan.stops[0]));
    if(!await page.locator('.nav-panel').textContent().then(s=>s.includes('Эта цель прочитана из игры')))throw Error('Game marker not identified as read-only');
    const deletion=await page.evaluate(async()=>{
     const n=etsMapState.navigator,request=n.request,refresh=n.refresh,old=n.plan,revision=n.gameRevision,available=n.gameAvailable;let finish,calls=0,body;
     n.refresh=()=>{};n.request=async(path,payload)=>{if(payload){calls++;body=payload;return new Promise(resolve=>finish=resolve);}return{available:true,revision,plan:old};};
     try{const pending=n.deleteGamePoint(old.stops[0]);await n.deleteGamePoint(old.stops[0]);const retained=n.plan===old&&calls===1&&n.root.querySelector('[data-gps-delete]').disabled;finish({deleted:false,message:'Карта не открыта'});await pending;const rejected=n.plan.stops.length===old.stops.length&&n.message==='Карта не открыта';
      const next={...old,stops:old.stops.slice(1)};n.request=async(path,payload)=>payload?{deleted:true,message:'Точка удалена из GPS игры'}:{available:true,revision:'verified-test-removal',plan:next};await n.deleteGamePoint(old.stops[0]);const removed=n.plan.stops.length===next.stops.length&&n.plan.destination.id===old.destination.id;
      n.open('place',old.destination);const goalProtected=!n.panel.querySelector('[data-gps-delete]');return retained&&rejected&&removed&&goalProtected&&body.pointId===old.stops[0].id&&body.revision===revision&&/^[0-9a-f-]{36}$/i.test(body.commandId);
     }finally{n.request=request;n.refresh=refresh;n.plan=old;n.gameRevision=revision;n.gameAvailable=available;}
    });
    if(!deletion)throw Error('Game delete request, pending state, rejection retention or goal guard failed');
    await page.getByRole('button',{name:'Закрыть панель',exact:true}).click();
    await page.evaluate(()=>{const n=etsMapState.navigator;n.follow=true;n.center=null;n.draw();});
   }
   await page.locator('.sdnav').screenshot({path:`${out}/navigator-game-gps-${width}.png`});
   const transitions=await page.evaluate(async()=>{
    const n=etsMapState.navigator,request=n.request,refresh=n.refresh,plan=n.plan;n.refresh=()=>{};
    const poll=async j=>{n.request=async()=>j;n.gameAttempt=-Infinity;await n.refreshGameGps();};
    try{
     await poll({available:false,pending:true,message:'Игра перестраивает маршрут'});const retained=n.plan===plan&&n.message.includes('Последняя линия GPS');
     await poll({available:true,empty:true});const cleared=!n.plan;
     await poll({available:true,empty:false,revision:'qa-route-restored',plan});
     let finish;n.request=()=>new Promise(resolve=>finish=resolve);n.gameAttempt=-Infinity;
     const pending=n.refreshGameGps();n.useOwnRoute();finish({available:true,revision:'old-response',plan});await pending;
     const own=!n.gameMode&&!n.plan&&JSON.parse(localStorage.getItem('simdeck.navigator.'+n.profile)).gameMode===false;
     return retained&&cleared&&own;
    }finally{n.request=request;n.refresh=refresh;}
   });
   if(!transitions)throw Error('GPS clearing, transient retention or delayed mode switch failed');
   const guard=await page.evaluate(async()=>(await fetch('/truck-nav/game-route?profile=ets2')).status);
   if(guard!==409)throw Error('Game GPS profile scope missing');
   console.log('Game GPS mode, clear, pending and delayed switch passed at '+width);
  }
  if(process.env.SIMDECK_LANDSCAPE_QA==='1'){
   await page.evaluate(()=>etsMapState.navigator.useGameGps());
   await page.waitForFunction(()=>etsMapState.navigator.plan?.source==='game-gps'&&!etsMapState.navigator.gameBusy);
   await page.waitForFunction(()=>etsMapState.navigator?.landscape,{},{timeout:120000});
   const layer=await page.evaluate(()=>{const l=etsMapState.navigator.landscape;return{forest:l.forestPolygons,water:l.waterPolygons,width:l.day.naturalWidth,span:l.span};});
   if(!layer.forest||!layer.water||layer.width!==2048)throw Error('Actual game forest/water layer missing');
   console.log('Real forest/water layer at '+width+': '+JSON.stringify(layer));
   const plan=await page.evaluate(()=>etsMapState.navigator.plan?.destination.id);
   for(const [style,title] of [['roads','Схема'],['terrain','Местность'],['satellite','Спутниковый вид']]){
    await page.getByRole('button',{name:'Слои карты',exact:true}).click();
    await page.getByRole('button',{name:'Вид карты: '+title,exact:true}).click();
    if(await page.getByRole('button',{name:'Вид карты: '+title,exact:true}).getAttribute('aria-pressed')!=='true')throw Error('Map style selection not shown');
    await page.getByRole('button',{name:'Закрыть панель',exact:true}).click();
    if(style!=='roads')await page.waitForFunction(style=>etsMapState.navigator.landscape?.style===style,style,{timeout:120000});
    const ok=await page.evaluate(({style,plan})=>{const n=etsMapState.navigator,saved=JSON.parse(localStorage.getItem('simdeck.navigator.'+n.profile));return n.mapStyle===style&&saved.mapStyle===style&&n.plan?.destination.id===plan;},{style,plan});
    if(!ok)throw Error('Map style reset GPS route or was not saved');
    await page.locator('.sdnav').screenshot({path:`${out}/navigator-${style}-${width}.png`});
    if(style==='satellite'){
     const wasDay=await page.evaluate(()=>etsMapState.navigator.day);
     if(!wasDay)await page.getByRole('button',{name:'День / ночь',exact:true}).click();
     const readable=await page.evaluate(()=>{const n=etsMapState.navigator;return n.day&&/247,\s*248,\s*239/.test(getComputedStyle(n.root.querySelector('.nav-maneuver')).backgroundImage)&&getComputedStyle(n.themeButton).color==='rgb(32, 52, 62)';});
     if(!readable)throw Error('Day instruments or tool contrast missing');
     await page.locator('.sdnav').screenshot({path:`${out}/navigator-satellite-day-${width}.png`});
     if(!wasDay)await page.getByRole('button',{name:'День / ночь',exact:true}).click();
    }
   }
   const restored=await page.evaluate(()=>{const n=etsMapState.navigator,parent=document.createElement('div');document.body.append(parent);const another=new SimDeckNavigator.Navigator(parent,{profile:n.profile,request:n.options.request});try{return another.mapStyle==='satellite';}finally{another.destroy();parent.remove();}});
   if(!restored)throw Error('Map style not restored when reopening navigator');
   await page.waitForFunction(()=>!etsMapState.navigator.landscapeBusy);
   const late=await page.evaluate(async()=>{const n=etsMapState.navigator,request=n.request,refresh=n.refresh,surface=n.landscape;n.refresh=()=>{};n.landscape=null;n.landscapeCache.clear();n.landscapeAttempt=-Infinity;let complete;n.request=path=>path.includes('/landscape?')?new Promise(resolve=>complete=resolve):request.call(n,path);try{n.mapStyle='satellite';const pending=refresh.call(n);while(!complete)await new Promise(r=>setTimeout(r,10));n.setMapStyle('roads');complete(surface);await pending;return n.mapStyle==='roads'&&!n.landscape;}finally{n.request=request;n.refresh=refresh;n.setMapStyle('terrain');n.closePanel();}});
   if(!late)throw Error('Delayed satellite response changed selected road view');
   console.log('All map styles, route retention, persistence and delayed response passed at '+width);
  }
  await page.evaluate(()=>etsMapState.navigator.cancel());
  const nav=()=>etsMapState.navigator;
  await page.getByRole('button',{name:'Маршрут и поиск',exact:true}).click();
  await page.getByRole('button',{name:'Город текущей доставки'}).click();
  await page.getByRole('button',{name:'Построить выбранный маршрут'}).click();
  await page.waitForFunction(()=>!!etsMapState.navigator?.plan&&!etsMapState.navigator.routeBusy,{},{timeout:45000});console.log('Route ready at '+width);
  const initial=await page.evaluate(()=>{const n=etsMapState.navigator;return{metres:n.plan.metres,points:n.plan.points.length,objects:n.map.pois.length,turns:n.plan.maneuvers.length};});
  if(initial.metres<100||initial.points<10||!initial.objects)throw Error('Real map/route/POIs missing');
  const repaint=await page.evaluate(()=>{const n=etsMapState.navigator,c=n.canvas.getContext('2d'),stroke=c.stroke;let count=0;c.stroke=function(...args){count++;return stroke.apply(this,args);};try{n.update(n.frame);n.update(n.frame);n.update(n.frame);const stationary=count;n.zoom(.95);return{stationary,zoom:count-stationary};}finally{c.stroke=stroke;}});
  if(repaint.stationary!==0||repaint.zoom===0)throw Error('Stationary map repaint or zoom missing');
  const panelRetained=await page.evaluate(async()=>{const n=etsMapState.navigator,request=n.request.bind(n),plan=n.plan;let finish;n.request=()=>new Promise(resolve=>{finish=resolve;});try{const pending=n.calculate(true);n.open('layers');finish(plan);await pending;return !n.panel.hidden&&!n.closeButton.hidden;}finally{n.request=request;}});
  if(!panelRetained)throw Error('Delayed route response closed the newly opened panel');
  for(const kind of ['layers','nearby','route','place']){console.log('Panel '+kind+' at '+width);await page.evaluate(kind=>etsMapState.navigator.open(kind,kind==='place'?etsMapState.navigator.destination:null),kind);await page.locator('.nav-panel').evaluate(el=>el.scrollTop=el.scrollHeight);const close=page.getByRole('button',{name:'Закрыть панель',exact:true});const rect=await close.boundingBox(),mapRect=await page.locator('.sdnav').boundingBox();if(!rect||rect.y<mapRect.y||rect.y+rect.height>mapRect.y+mapRect.height)throw Error('Panel close clipped '+kind);await close.click({timeout:10000}).catch(async e=>{await page.screenshot({path:`${out}/panel-failure.png`});console.log(await page.evaluate(()=>({closeHidden:etsMapState.navigator.closeButton.hidden,rootVisible:etsMapState.navigator.root.getBoundingClientRect().toJSON(),body:document.body.innerText.slice(0,1000)})));throw e;});if(await page.locator('.nav-panel').isVisible())throw Error('Panel did not close '+kind);}
  await page.locator('.sdnav').screenshot({path:`${out}/navigator-ats-${width}.png`});
  await page.getByRole('button',{name:'Маршрут и поиск',exact:true}).click();
  await page.getByRole('button',{name:'Поиск: Заправка',exact:true}).click();
  await page.locator('.nav-results .nav-place').first().waitFor().catch(async e=>{console.log(await page.locator('.nav-panel').textContent());await page.locator('.sdnav').screenshot({path:`${out}/navigator-search-failure-${width}.png`});throw e;});
  await page.locator('.nav-results .nav-place').first().locator('button').last().click();
  await page.getByRole('button',{name:'Построить выбранный маршрут'}).click();
  await page.waitForFunction(()=>!etsMapState.navigator.routeBusy,{},{timeout:45000});console.log('Waypoint ready at '+width);
  const waypoint=await page.evaluate(()=>{const n=etsMapState.navigator;return{count:n.plan.stops.length,at:n.plan.stopMetres};});
  if(waypoint.count!==1||waypoint.at.length!==1)throw Error('Waypoint was not routed');
  // A selected point must be hittable even when its POI layer is hidden.
  const selectedHit=await page.evaluate(()=>{const n=etsMapState.navigator,p=n.stops[0],layers=n.layers;n.layers=new Set();const screen=n.toScreen(p.x,p.z),hit=n.hitPoi(screen.x,screen.y);n.layers=layers;return hit?.id===p.id;});
  if(!selectedHit)throw Error('Selected waypoint cannot be opened from the map');
  // Pause the source and hold an older response while deleting a stop via UI.
  await page.evaluate(()=>{const n=etsMapState.navigator;n.qaFrame=n.frame;n.qaPlan=n.plan;n.qaRefresh=n.refresh;n.refresh=()=>{};n.update({...n.frame,stale:true});n.qaRequest=n.request;n.request=()=>new Promise(resolve=>{n.qaFinish=resolve;});n.frame={...n.frame,stale:false};n.qaPending=n.calculate();n.frame={...n.frame,stale:true};n.open('place',n.stops[0]);});
  await page.getByRole('button',{name:'Удалить выбранную точку',exact:true}).click();
  const removed=await page.evaluate(async()=>{const n=etsMapState.navigator;n.qaFinish(n.qaPlan);await n.qaPending;n.request=n.qaRequest;const saved=JSON.parse(localStorage.getItem('simdeck.navigator.'+n.profile));return !n.plan&&!n.routeBusy&&n.stops.length===0&&!!n.destination&&saved.stops.length===0;});
  if(!removed)throw Error('Deleted stop or old route returned after delayed calculation');
  // Deleting the destination retains other stops, and works without telemetry.
  await page.evaluate(()=>{const n=etsMapState.navigator;n.addStop(n.qaPlan.stops[0]);n.open('place',n.destination);});
  await page.getByRole('button',{name:'Удалить выбранную точку',exact:true}).click();
  const destRemoved=await page.evaluate(()=>{const n=etsMapState.navigator;const saved=JSON.parse(localStorage.getItem('simdeck.navigator.'+n.profile));return !n.destination&&!n.plan&&n.stops.length===1&&!saved.destination&&saved.stops.length===1;});
  if(!destRemoved)throw Error('Destination removal failed or cleared unrelated stops');
  await page.getByRole('button',{name:'Маршрут и поиск',exact:true}).click();
  await page.getByRole('button',{name:'Отменить маршрут',exact:true}).click();
  if(!await page.evaluate(()=>{const n=etsMapState.navigator;return !n.destination&&!n.stops.length&&!n.plan;}))throw Error('Cancel without a built route failed');
  // GPS data is taken directly from telemetry, independently of local points.
  const gameGps=await page.evaluate(()=>{const n=etsMapState.navigator;n.update({...n.qaFrame,data:{...n.qaFrame.data,ets2Navigation:{...n.position,remainingKm:160.9344,remainingMinutes:75}}});const present=n.gameGps.textContent.includes('100 mi')&&n.gameGps.textContent.includes('1 ч 15 мин');n.setDestination(n.qaPlan.destination);n.cancel();const retained=n.gameGps.textContent.includes('100 mi');n.update({...n.qaFrame,data:{...n.qaFrame.data,ets2Navigation:{...n.position,remainingKm:null,remainingMinutes:null}}});const cleared=n.gameGps.textContent.includes('маршрут не задан');n.update(n.qaFrame);n.refresh=n.qaRefresh;n.setDestination(n.qaPlan.destination);return present&&retained&&cleared;});
  if(!gameGps)throw Error('Game GPS metrics confused with the SimDeck route');
  await page.getByRole('button',{name:'Построить выбранный маршрут',exact:true}).click();
  await page.waitForFunction(()=>!!etsMapState.navigator.plan&&!etsMapState.navigator.routeBusy);
  // The game stream may pause, while the map and routes remain on screen.
  await page.evaluate(()=>{const n=etsMapState.navigator;n.update({data:n.frame.data,stale:true,connected:true});});
  const retained=await page.evaluate(()=>!!etsMapState.navigator.plan&&!!etsMapState.navigator.map);
  if(!retained)throw Error('Telemetry delay blanked navigator');
  await page.getByRole('button',{name:'Слои карты',exact:true}).click();
  await page.locator('.nav-panel input[type=checkbox]').first().uncheck();
  await page.getByRole('button',{name:'Закрыть панель',exact:true}).click();
  await page.getByRole('button',{name:'День / ночь',exact:true}).click();
  await page.getByRole('button',{name:'Север / направление движения',exact:true}).click();
  const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth);
  if(overflow)throw Error('Horizontal overflow at '+width);
  await page.getByRole('button',{name:'Маршрут и поиск',exact:true}).click();
  await page.getByRole('searchbox',{name:'Поиск города или объекта'}).fill('not-a-real-city');
  await page.waitForTimeout(700);
  if(!await page.locator('.nav-results').textContent().then(s=>s.includes('Ничего не найдено')))throw Error('Search result feedback missing');
  // Switch API scope without changing the active ATS profile.
  const blocked=await page.evaluate(async()=>({wrong:(await fetch('/truck-nav/places?profile=ets2')).status,cross:(await fetch('/truck-nav/route?profile=ats',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'})).status,landscapeWrong:(await fetch('/truck-nav/landscape?profile=ets2&x=0&z=0&span=800')).status,landscapeBad:(await fetch('/truck-nav/landscape?profile=ats&x=NaN&z=0&span=800')).status,styleBad:(await fetch('/truck-nav/landscape?profile=ats&x=0&z=0&span=800&style=../satellite')).status}));
  if(blocked.wrong!==409||blocked.cross!==400||blocked.landscapeWrong!==409||blocked.landscapeBad!==400||blocked.styleBad!==400)throw Error('Profile/malformed request guard missing');
  if(errors.length)throw Error(errors.join('\n'));
  results.push({width,...initial,waypoint,overflow,errors});await page.close();
 }
 await browser.close();fs.writeFileSync(`${out}/navigator-ui-${process.env.SIMDECK_BROWSER||'chromium'}.json`,JSON.stringify(results,null,2));console.log(JSON.stringify(results));
})().catch(e=>{console.error(e);process.exit(1);});
