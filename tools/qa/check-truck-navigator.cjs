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
  if(process.env.SIMDECK_LANDSCAPE_QA==='1'){
   await page.waitForFunction(()=>etsMapState.navigator?.landscape,{},{timeout:120000});
   const layer=await page.evaluate(()=>{const l=etsMapState.navigator.landscape;return{forest:l.forestPolygons,water:l.waterPolygons,width:l.day.naturalWidth,span:l.span};});
   if(!layer.forest||!layer.water||layer.width!==2048)throw Error('Actual game forest/water layer missing');
   console.log('Real forest/water layer at '+width+': '+JSON.stringify(layer));
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
  const blocked=await page.evaluate(async()=>({wrong:(await fetch('/truck-nav/places?profile=ets2')).status,cross:(await fetch('/truck-nav/route?profile=ats',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'})).status,landscapeWrong:(await fetch('/truck-nav/landscape?profile=ets2&x=0&z=0&span=800')).status,landscapeBad:(await fetch('/truck-nav/landscape?profile=ats&x=NaN&z=0&span=800')).status}));
  if(blocked.wrong!==409||blocked.cross!==400||blocked.landscapeWrong!==409||blocked.landscapeBad!==400)throw Error('Profile/malformed request guard missing');
  if(errors.length)throw Error(errors.join('\n'));
  results.push({width,...initial,waypoint,overflow,errors});await page.close();
 }
 await browser.close();fs.writeFileSync(`${out}/navigator-ui-${process.env.SIMDECK_BROWSER||'chromium'}.json`,JSON.stringify(results,null,2));console.log(JSON.stringify(results));
})().catch(e=>{console.error(e);process.exit(1);});
