const {chromium,webkit}=require('playwright');
const fs=require('fs');const profiles=JSON.parse(fs.readFileSync(process.argv[2] || 'profiles.json','utf8'));
(async()=>{
 const engine=process.env.SIMDECK_BROWSER==='webkit'?webkit:chromium;
 const browser=await engine.launch({headless:true,...(process.env.SIMDECK_BROWSER_EXECUTABLE ? {executablePath:process.env.SIMDECK_BROWSER_EXECUTABLE} : {})});
 for(const [width,height] of [[320,640],[390,844],[844,390],[800,1340],[1340,800]]){
  const page=await browser.newPage({viewport:{width,height}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/status',r=>r.fulfill({json:{}}));
  await page.addInitScript(({profiles})=>{
   window.fixtureProfiles=profiles;window.commands=[];window.fixtureAgeMs=0;window.inputAvailability='ready';window.telemetryMissing=false;window.fixtureProfile=profiles[0];window.fixtureIgnition=false;window.fixtureVehicle=null;
   window.WebSocket=class{readyState=1;constructor(){window.fixtureSocket=this;setTimeout(()=>{this.hello();setInterval(()=>{const id=window.fixtureProfile.id;this.emit({type:'input.state',availability:window.inputAvailability,ignitionReady:window.fixtureIgnition});this.emit({type:'telemetry.snapshot',source:'fixture',ageMs:window.telemetryMissing?10000:window.fixtureAgeMs,data:{vehicle:window.fixtureVehicle,speedMps:45,rpm:6200,maxRpm:8000,gear:4,gearDisplay:'4',fuelFraction:.64,gearboxMode:'realistic',headlights:1,actionStates:{ignition:true,hazards:true,fourWheelDrive:true,etsCruise:true,fs25Lower:true,fs25TurnOn:false,fs25Motor:true},acc:{wheels:Array.from({length:4},(_,i)=>({coreTemperature:83+i,pressure:27.5,brakeTemperature:405,wear:4})),waterTemperature:90,roadTemperature:31},ets2Navigation:{remainingKm:184,remainingMinutes:134,speedLimitKmh:80},fs25:{header:{savegameName:'Тестовая ферма',mapTitle:'Riverbend Springs'},environment:{period:{russianMonth:'Сентябрь'}},farms:[],fields:window.fixtureFields||[],timestamp:'2026-10-02T10:00:00Z'},fs25Advisor:{tasks:[],alerts:[]},fs25Prices:window.fixturePrices||null,f1:{engineTemperature:109,values:{compound:16,frontLeftWingDamage:20,frontRightWingDamage:38,rearWingDamage:4,floorDamage:8,sidepodDamage:18,drsFault:1},wheels:Array.from({length:4},()=>({surface:89,pressure:23,wear:12})),race:{trackId:3,trackLength:5300,sessionType:15,fresh:true,drivers:['Norris','Player','Piastri'].map((name,i)=>({index:i,name,team:1,number:i+1,position:i+1,lap:8,distance:2200+i*100,player:i===1,pit:0,result:2,driverStatus:1}))}}}});},100);},10);}hello(){const p=window.fixtureProfile;this.emit({type:'hello',profileId:p.id,profileName:p.name,profileRevision:p.revision,controls:[...p.actions,{id:'customQa',page:'Мои кнопки',group:'Тест',label:'Моя кнопка',description:'Пользовательское действие',key:'Ctrl+F12',gesture:'press'}]});}emit(m){this.onmessage?.({data:JSON.stringify({protocolMajor:1,sessionId:'qa',...m})});}send(s){const m=JSON.parse(s);window.commands.push(m);if(m.actionId==='ignition'&&m.phase==='press')window.fixtureIgnition=true;if(m.type==='control.invoke')this.emit({type:'control.ack',commandId:m.commandId,success:true,code:'injected'});}close(){this.readyState=3;}};
  },{profiles});
  await page.goto(process.argv[3] || 'http://127.0.0.1:8765/');await page.locator('#deck').waitFor({state:'visible'});
  for(const name of ['farm.png','road.png','equipment.png','gt.png','harvest.png','fs25-flat-1.png','fs25-flat-2.png','fs25-flat-3.png','fs25-flat-4.png','fs25-flat-5.png','fs25-flat-6.png']){
   const dimensions=await page.evaluate(async name=>{const im=new Image();im.src='/vehicles/'+name;await im.decode();return [im.naturalWidth,im.naturalHeight];},name);
   if(dimensions[0]<1000||dimensions[1]<700)throw Error('Missing production image '+name);
  }
  for(const p of profiles){
   await page.evaluate(id=>{window.fixtureProfile=window.fixtureProfiles.find(p=>p.id===id);window.fixtureSocket.hello();window.commands=[];window.fixtureVehicle=null;window.telemetryMissing=['ams2','snowrunner'].includes(id);window.inputAvailability='ready';},p.id);await page.waitForTimeout(600);
   if(!await page.locator('#brandGame').textContent())throw Error('Profile title missing '+p.id);
   if(!await page.locator('#telemetryStatus').isVisible())throw Error('Telemetry status missing '+p.id);const collision=await page.evaluate(()=>{const logo=document.querySelector('header>div').getBoundingClientRect(),nav=document.querySelector(isF1()?'#f1Nav':'#pages').getBoundingClientRect();return logo.right>nav.left+1&&nav.right>logo.left+1&&logo.bottom>nav.top+1&&nav.bottom>logo.top+1;});if(collision)throw Error('Header overlaps navigation '+p.id+' '+width);
   if(p.id.startsWith('f1-')){
    if(await page.locator('#f1VehicleOverview [data-damage-zone="frontLeftWingDamage"][data-level="1"]').count()!==1 || await page.locator('#f1VehicleOverview [data-damage-zone="drsFault"][data-level="2"]').count()!==1)throw Error('F1 surface damage masks missing');
    if(!await page.locator('#f1VehicleOverview').isVisible()||await page.locator('#f1VehicleOverview .tyreCard').count()!==4)throw Error('F1 schematic missing on condition screen '+p.id);await page.evaluate(()=>goF1('race'));
   }else{
    if(await page.locator('#f1VehicleOverview').isVisible())throw Error('F1 schematic leaked into '+p.id);
    if(['acc','ams2','snowrunner'].includes(p.id)&&!await page.locator('#profileDashboard .vehicleDrawing').count())throw Error('Vehicle class absent '+p.id);
   }
   // Stop the game stream while retaining the controller: readings and ordinary input
   // must survive, including profiles with no game data source at all.
   const reading=await page.locator(p.id.startsWith('f1-')?'#speed':p.id==='snowrunner'?'#profileDashboard [data-metric="gear"]':p.id==='fs25'?'#profileDashboard [data-metric="vehicle"]':'#profileDashboard [data-metric="speed"]').first().textContent();
   await page.evaluate(()=>window.telemetryMissing=true);await page.waitForTimeout(250);
   const lastReading=await page.locator(p.id.startsWith('f1-')?'#speed':p.id==='snowrunner'?'#profileDashboard [data-metric="gear"]':p.id==='fs25'?'#profileDashboard [data-metric="vehicle"]':'#profileDashboard [data-metric="speed"]').first().textContent();
   if(lastReading!==reading)throw Error('Readout disappeared on delayed telemetry '+p.id);
   const inputButton=page.locator(p.id.startsWith('f1-')?'#f1Quick button:first-child':'#profileDashboard .action:enabled').first();
   if(!await inputButton.isEnabled())throw Error('Ordinary controls gated by telemetry '+p.id);
   await inputButton.click();if(!await page.evaluate(()=>commands.some(m=>m.type==='control.invoke')))throw Error('Delayed telemetry blocked command '+p.id);
   await page.evaluate(()=>{window.telemetryMissing=['ams2','snowrunner'].includes(window.fixtureProfile.id);window.commands=[];});await page.waitForTimeout(250);
   if(p.id.startsWith('f1-')){for(const dest of ['race','condition','pit','mfd','track','menu']){await page.evaluate(dest=>goF1(dest),dest);const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth);if(overflow)throw Error(`${p.id} ${width} ${dest} overflow`);}await page.evaluate(()=>{goF1('mfd');panel='mfdDamage';renderF1Panel();});await page.waitForTimeout(150);if(await page.locator('#f1Panel .tyreCard').count()!==4)throw Error('F1 tyre diagram missing');if(width===390||width===1340)await page.locator('#f1Panel').screenshot({path:`${process.argv[4] || '.'}/f1-schematic-${p.id}-${width}.png`});await page.evaluate(()=>goF1('race'));}
   const overflow=await page.evaluate(()=>({scroll:document.documentElement.scrollWidth,width:document.documentElement.clientWidth}));if(overflow.scroll>overflow.width)throw Error(JSON.stringify({id:p.id,width,overflow}));
   if(!p.id.startsWith('f1-')){
    const count=await page.locator('#profileDashboard .action').count();if(count<(p.id==='fs25'?3:4))throw Error('Quick actions missing '+p.id);
    if(['ams2','snowrunner'].includes(p.id)){if(await page.locator('[data-metric="gear"]').textContent()!=='—')throw Error('Fabricated telemetry '+p.id);}
    const quick=page.locator('#profileDashboard .action').filter({has:page.locator('span')}).first();await quick.click();
    if(!await page.evaluate(()=>commands.some(m=>m.type==='control.invoke')))throw Error('Quick action did not send '+p.id);
    const tabs=await page.locator('#pages button').evaluateAll(nodes=>nodes.map(n=>n.dataset.page));
    for(const name of ['Обзор',...new Set(p.actions.map(a=>a.page)),'Мои кнопки'])if(!tabs.includes(name))throw Error('Persistent page missing '+p.id+' '+name);
    if(!await page.locator('#pages').isVisible())throw Error('Navigation hidden '+p.id);
   }else{await page.locator('#f1Nav button').filter({hasText:'Мои кнопки'}).click();await page.locator('#buttons [data-action="customQa"]').click();await page.evaluate(()=>goF1('race'));await page.locator('#f1Quick button').first().click();if(!await page.evaluate(()=>commands.some(m=>m.actionId==='radio')))throw Error('Radio did not send');}
   // Verify every configured action remains reachable, including custom actions.
   const missing=await page.evaluate(()=>{const missing=[];for(const a of actions){setPage(a.page,a.group||'Общие');if(!document.querySelector(`.action[data-action="${a.id}"]`)&&!isF1())missing.push(a.id);}return missing;});if(missing.length)throw Error(p.id+' missing '+missing);
   if(!p.id.startsWith('f1-')){await page.evaluate(()=>setPage('Мои кнопки','Тест'));await page.locator('#buttons [data-action="customQa"]').click();if(!await page.evaluate(()=>commands.some(m=>m.actionId==='customQa')))throw Error('Custom action lost');}
   const held=p.actions.find(a=>a.gesture==='hold');if(held){await page.evaluate(a=>setPage(a.page,a.group||'Общие'),held);const btn=page.locator(`.action[data-action="${held.id}"]:visible`).last();await btn.scrollIntoViewIfNeeded();const box=await btn.boundingBox();await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down();await page.waitForTimeout(250);await page.mouse.up();const phases=await page.evaluate(id=>commands.filter(m=>m.actionId===id).map(m=>m.phase),held.id);if(!phases.includes('down')||!phases.includes('up'))throw Error('Hold lost '+p.id);}
   await page.evaluate(()=>window.inputAvailability='disabled');await page.waitForTimeout(220);if(await page.locator('.action:enabled').count()>0)throw Error('Input disabled but action enabled '+p.id);await page.evaluate(()=>{window.inputAvailability='ready';if(isF1())goF1('race');else setPage('Обзор');});await page.waitForTimeout(220);
   if(['fs25','ets2','beamng-default'].includes(p.id)){
    await page.evaluate(()=>{window.fixtureVehicle={id:'truck',name:'Test truck',kind:'truck',controlled:true,wheels:Array.from({length:6},(_,i)=>({x:i%2===0?-1:1,z:Math.floor(i/2)*2-2,powered:i>1})),attachments:[{id:'trailer',parentId:'truck',name:'Trailer',kind:'trailer',wheels:[{x:-1,z:1},{x:1,z:1}],lowered:true,turnedOn:false}]};});await page.waitForTimeout(220);
    if(!await page.locator('#vehiclePanel').textContent().then(t=>t.includes('Test truck')&&t.includes('3 оси')))throw Error('Automatic truck selection failed');
    if(width===390||width===1340)await page.locator('#vehiclePanel').screenshot({path:`${process.argv[4] || '.'}/vehicle-${p.id}-${width}.png`});
    if(p.id==='ets2'&&await page.locator('#vehiclePanel .vehicleGhost').count()!==1)throw Error('Transparent trailer missing');
    if(p.id==='fs25'){await page.evaluate(()=>{window.fixtureVehicle={id:'tractor',name:'MT635',kind:'tractor',controlled:true,wheels:[{x:-1,z:-1},{x:1,z:-1},{x:-1,z:1},{x:1,z:1}],attachments:[{id:'implement',parentId:'tractor',name:'980',kind:'cultivator',mount:'rear',lowered:true,fold:1}]};});await page.waitForTimeout(250);await page.evaluate(()=>window.savedImplement=document.querySelector('.vehicleImplement'));await page.evaluate(()=>window.fixtureVehicle.attachments[0].lowered=false);await page.waitForTimeout(500);if(!await page.evaluate(()=>savedImplement===document.querySelector('.vehicleImplement')&&savedImplement.style.transform.includes('-13.8px')))throw Error('Farm lift animation rebuilt or absent');if(width===390||width===1340)await page.locator('#vehiclePanel').screenshot({path:`${process.argv[4] || '.'}/farm-implement-${width}.png`});}

    if(p.id==='fs25'){
     await page.evaluate(()=>window.fixtureVehicle={id:'combine',name:'MF 8570',kind:'combine',controlled:true,wheels:[],attachments:[{id:'header',parentId:'combine',name:'MF 8570 Жатка',kind:'header',mount:'front',lowered:false}]});await page.waitForTimeout(250);
     const before=await page.locator('.vehicleImplement').count();if(before!==1)throw Error('Attached header missing');
     const headerX=await page.locator('.vehicleImplement .vehicleSprite').getAttribute('x');const rootX=await page.locator('#vehiclePanel .vehicleDrawing>.vehicleSprite').getAttribute('x');if(Number(headerX)>=Number(rootX))throw Error('Header mounted behind combine');
     await page.evaluate(()=>window.fixtureVehicle.attachments=[]);await page.waitForTimeout(250);
     if(await page.locator('.vehicleImplement').count())throw Error('Detached header still drawn');
     const atlas=await page.locator('#vehiclePanel .vehicleSprite image').first().getAttribute('href');if(!atlas.endsWith('fs25-flat-1.png'))throw Error('Bare combine asset not used');
     await page.evaluate(()=>window.telemetryMissing=true);await page.waitForTimeout(350);
     if(!await page.locator('#vehiclePanel').textContent().then(t=>t.includes('MF 8570')&&t.includes('данные устарели')))throw Error('Known identity lost on stale frame');
     if(await page.locator('#vehiclePanel .vehicleDrawing').count()!==1)throw Error('Cached geometry disappeared');
     await page.evaluate(()=>{window.telemetryMissing=false;window.fixtureVehicle={id:'unknownTool',name:'Tractor',kind:'tractor',controlled:true,wheels:[],attachments:[{id:'tool',parentId:'unknownTool',name:'Unknown tool',kind:'implement'}]};});await page.waitForTimeout(250);
     if(await page.locator('.vehicleImplement').count())throw Error('Generic tool fabricated as plough');
    }

    await page.evaluate(()=>{window.fixtureVehicle={id:'custom',name:'New vehicle',kind:'unknown',controlled:true,wheels:[],attachments:[]};});await page.waitForTimeout(220);
    if(await page.locator('#vehiclePanel .vehicleWheel').count()!==0||await page.locator('#vehiclePanel .vehicleGhost').count()!==0)throw Error('Previous vehicle geometry survived switch');
    await page.evaluate(()=>{window.fixtureVehicle={id:'',name:'',kind:'unknown',controlled:false,wheels:[],attachments:[]};});await page.waitForTimeout(220);
    if(!await page.locator('#vehiclePanel').textContent().then(t=>t.includes('Вы не в технике')))throw Error('Dismount did not clear diagram');
    await page.evaluate(()=>window.fixtureVehicle=null);
   }
   if(p.id==='fs25'){
    await page.evaluate(()=>{window.fixtureFields=[{id:6,fruitType:'WHEAT',groundType:'GROWING',growthState:5,lastGrowthState:4,weedState:8,limeLevel:1,sprayLevel:2},{id:9,fruitType:'BARLEY',groundType:'HARVEST_READY',growthState:7},{id:12,fruitType:'WHEAT',groundType:'CUT',growthState:0,weedState:0}];window.commands=[];setPage('Поля');});await page.waitForTimeout(300);
    if(!await page.locator('#fs25FieldDetail [data-field-metric="weedState"] strong').textContent().then(t=>t==='89%'))throw Error('Field percentage incorrect');
    if(await page.locator('#fs25FieldGrid button').count()!==3)throw Error('Saved fields are absent');
    await page.locator('#fs25FieldGrid button[data-field="9"]').click();if(!await page.locator('#fs25FieldDetail').textContent().then(t=>t.includes('ПОЛЕ №9')))throw Error('Field selection failed');
    if(await page.locator('#fs25FieldDetail [data-field-metric="weedState"]').textContent().then(t=>t.includes('0%')))throw Error('Unknown weeds fabricated as zero');
    await page.locator('#fs25FieldSearch').fill('пшени');if(await page.locator('#fs25FieldGrid button').count()!==2)throw Error('Translated crop search failed');await page.locator('#fs25FieldSearch').fill('');
    await page.locator('#fs25FieldFilters button').filter({hasText:'Ячмень'}).click();if(await page.locator('#fs25FieldGrid button').count()!==1)throw Error('Crop filter failed');
    await page.locator('#fs25FieldFilters button').filter({hasText:/^Все$/}).click();
    if(await page.evaluate(()=>commands.some(m=>m.type==='control.invoke')))throw Error('Field inspection sent game input');
    if(width===390||width===1340)await page.locator('#fs25Fields').screenshot({path:`${process.argv[4] || '.'}/fields-${width}.png`});
    await page.evaluate(()=>{window.fixturePrices={ageMs:0,offers:[{crop:'WHEAT',cropName:'Пшеница',station:'Элеватор',pricePer1000:1200,formatted:'1 200 €'},{crop:'WHEAT',cropName:'Пшеница',station:'Порт',pricePer1000:1450,formatted:'1 450 €'},{crop:'BARLEY',cropName:'Ячмень',station:'Элеватор',pricePer1000:950,formatted:'950 €'}]};setPage('Цены');});await page.waitForTimeout(300);
    await page.locator('#fs25Prices nav button').filter({hasText:'Пшеница'}).click();
    if(await page.locator('#fs25Prices .fs25PriceRow').count()!==2||!await page.locator('#fs25Prices .fs25PriceRow').first().textContent().then(t=>t.includes('Порт')))throw Error('Current crop prices not sorted by best station');
    if(await page.evaluate(()=>commands.some(m=>m.type==='control.invoke')))throw Error('Price inspection sent game input');
    if(width===390||width===1340)await page.locator('#fs25Prices').screenshot({path:`${process.argv[4] || '.'}/prices-${width}.png`});
    await page.evaluate(()=>{window.fixturePrices=null;setPage('Обзор');});
   }
   if(p.id==='fs25'){await page.evaluate(()=>window.fixtureAgeMs=750);await page.waitForTimeout(300);if(!await page.locator('#profileDashboard [data-action="fs25Lower"]').evaluate(e=>e.classList.contains('on')))throw Error('FS25 indicator flickers after missed file poll');await page.evaluate(()=>window.fixtureAgeMs=1500);await page.waitForTimeout(300);if(!await page.locator('#profileDashboard [data-action="fs25Lower"]').evaluate(e=>e.classList.contains('on'))||!await page.locator('#telemetryStatus').evaluate(e=>e.classList.contains('delayed')))throw Error('FS25 last state lost or not marked delayed');await page.evaluate(()=>window.fixtureAgeMs=0);await page.waitForTimeout(300);}
   if(['fs25','ets2','beamng-default'].includes(p.id)){await page.evaluate(id=>{const farm=id==='fs25',truck=id==='ets2',count=truck?6:4;window.fixtureVehicle={id:'gallery',name:farm?'MT635':truck?'FH16':'Hatchback',kind:farm?'tractor':truck?'truck':'car',controlled:true,wheels:Array.from({length:count},(_,i)=>({x:i%2===0?-1:1,z:Math.floor(i/2)*2-2,powered:i>1})),attachments:farm?[{id:'980',parentId:'gallery',name:'980',kind:'cultivator',mount:'rear',lowered:true,fold:0}]:truck?[{id:'trailer',parentId:'gallery',name:'Полуприцеп',kind:'trailer',wheels:Array.from({length:6},(_,i)=>({x:i%2===0?-1:1,z:Math.floor(i/2)*2-2}))}]:[],wear:truck?{engine:.03,transmission:.01,cabin:.08,chassis:.02,wheels:.04}:{}};},p.id);await page.waitForTimeout(250);}
   if(p.id.startsWith('f1-'))await page.evaluate(()=>goF1('condition'));if(width===390||width===1340)await page.screenshot({path:`${process.argv[4] || '.'}/dashboard-${p.id}-${width}.png`,fullPage:true});
  }
  if(errors.length)throw Error(errors.join('\n'));console.log(`PASS ${width}x${height}: eight profiles, five F1 screens, all configured/custom actions, hold/release, disabled input, stale data, no overflow`);await page.close();
 }
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
