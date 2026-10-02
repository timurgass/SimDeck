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
   window.WebSocket=class{readyState=1;constructor(){window.fixtureSocket=this;setTimeout(()=>{this.hello();setInterval(()=>{const id=window.fixtureProfile.id;this.emit({type:'input.state',availability:window.inputAvailability,ignitionReady:window.fixtureIgnition});this.emit({type:'telemetry.snapshot',source:'fixture',ageMs:window.telemetryMissing?10000:window.fixtureAgeMs,data:{vehicle:window.fixtureVehicle,speedMps:45,rpm:6200,maxRpm:8000,gear:4,gearDisplay:'4',fuelFraction:.64,gearboxMode:'realistic',headlights:1,actionStates:{ignition:true,hazards:true,fourWheelDrive:true,etsCruise:true,fs25Lower:true,fs25TurnOn:false,fs25Motor:true},acc:{wheels:Array.from({length:4},(_,i)=>({coreTemperature:83+i,pressure:27.5,brakeTemperature:405,wear:4})),waterTemperature:90,roadTemperature:31},ets2Navigation:{remainingKm:184,remainingMinutes:134,speedLimitKmh:80},fs25:{header:{savegameName:'Тестовая ферма',mapTitle:'Riverbend Springs'},environment:{period:{russianMonth:'Сентябрь'}},farms:[],fields:[],timestamp:'2026-10-02T10:00:00Z'},fs25Advisor:{tasks:[],alerts:[]},f1:{engineTemperature:109,values:{compound:16},wheels:Array.from({length:4},()=>({surface:89,pressure:23,wear:12})),race:{trackId:3,trackLength:5300,sessionType:15,fresh:true,drivers:['Norris','Player','Piastri'].map((name,i)=>({index:i,name,team:1,number:i+1,position:i+1,lap:8,distance:2200+i*100,player:i===1,pit:0,result:2,driverStatus:1}))}}}});},100);},10);}hello(){const p=window.fixtureProfile;this.emit({type:'hello',profileId:p.id,profileName:p.name,profileRevision:p.revision,controls:[...p.actions,{id:'customQa',page:'Мои кнопки',group:'Тест',label:'Моя кнопка',description:'Пользовательское действие',key:'Ctrl+F12',gesture:'press'}]});}emit(m){this.onmessage?.({data:JSON.stringify({protocolMajor:1,sessionId:'qa',...m})});}send(s){const m=JSON.parse(s);window.commands.push(m);if(m.actionId==='ignition'&&m.phase==='press')window.fixtureIgnition=true;if(m.type==='control.invoke')this.emit({type:'control.ack',commandId:m.commandId,success:true,code:'injected'});}close(){this.readyState=3;}};
  },{profiles});
  await page.goto(process.argv[3] || 'http://127.0.0.1:8765/');await page.locator('#deck').waitFor({state:'visible'});
  for(const p of profiles){
   await page.evaluate(id=>{window.fixtureProfile=window.fixtureProfiles.find(p=>p.id===id);window.fixtureSocket.hello();window.commands=[];window.telemetryMissing=['ams2','snowrunner'].includes(id);window.inputAvailability='ready';},p.id);await page.waitForTimeout(600);
   if(!await page.locator('#brandGame').textContent())throw Error('Profile title missing '+p.id);
   if(p.id.startsWith('f1-')){for(const dest of ['race','pit','mfd','track','menu']){await page.evaluate(dest=>goF1(dest),dest);const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth);if(overflow)throw Error(`${p.id} ${width} ${dest} overflow`);}await page.evaluate(()=>goF1('race'));}
   const overflow=await page.evaluate(()=>({scroll:document.documentElement.scrollWidth,width:document.documentElement.clientWidth}));if(overflow.scroll>overflow.width)throw Error(JSON.stringify({id:p.id,width,overflow}));
   if(!p.id.startsWith('f1-')){
    const count=await page.locator('#profileDashboard .action').count();if(count<4)throw Error('Quick actions missing '+p.id);
    if(['ams2','snowrunner'].includes(p.id)){if(await page.locator('[data-metric="gear"]').textContent()!=='—')throw Error('Fabricated telemetry '+p.id);}
    const quick=page.locator('#profileDashboard .action').filter({has:page.locator('span')}).first();await quick.click();
    if(!await page.evaluate(()=>commands.some(m=>m.type==='control.invoke')))throw Error('Quick action did not send '+p.id);
    await page.locator('#allControls summary').click();
   }else{await page.locator('#f1Nav button').filter({hasText:'Мои кнопки'}).click();await page.locator('#buttons [data-action="customQa"]').click();await page.evaluate(()=>goF1('race'));await page.locator('#f1Quick button').first().click();if(!await page.evaluate(()=>commands.some(m=>m.actionId==='radio')))throw Error('Radio did not send');}
   // Verify every configured action remains reachable, including custom actions.
   const missing=await page.evaluate(()=>{const missing=[];for(const a of actions){setPage(a.page,a.group||'Общие');if(!document.querySelector(`.action[data-action="${a.id}"]`)&&!isF1())missing.push(a.id);}return missing;});if(missing.length)throw Error(p.id+' missing '+missing);
   if(!p.id.startsWith('f1-')){await page.evaluate(()=>setPage('Мои кнопки','Тест'));await page.locator('#buttons [data-action="customQa"]').click();if(!await page.evaluate(()=>commands.some(m=>m.actionId==='customQa')))throw Error('Custom action lost');}
   const held=p.actions.find(a=>a.gesture==='hold');if(held){await page.evaluate(a=>setPage(a.page,a.group||'Общие'),held);const btn=page.locator(`.action[data-action="${held.id}"]`).last();await btn.scrollIntoViewIfNeeded();const box=await btn.boundingBox();await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down();await page.waitForTimeout(250);await page.mouse.up();const phases=await page.evaluate(id=>commands.filter(m=>m.actionId===id).map(m=>m.phase),held.id);if(!phases.includes('down')||!phases.includes('up'))throw Error('Hold lost '+p.id);}
   await page.evaluate(()=>window.inputAvailability='disabled');await page.waitForTimeout(220);if(await page.locator('.action:enabled').count()>0)throw Error('Input disabled but action enabled '+p.id);await page.evaluate(()=>{window.inputAvailability='ready';if(isF1())goF1('race');else document.getElementById('allControls').open=false;});await page.waitForTimeout(220);
   if(['fs25','ets2','beamng-default'].includes(p.id)){
    await page.evaluate(()=>{window.fixtureVehicle={id:'truck',name:'Test truck',kind:'truck',controlled:true,wheels:Array.from({length:6},(_,i)=>({x:i%2===0?-1:1,z:Math.floor(i/2)*2-2,powered:i>1})),attachments:[{id:'trailer',parentId:'truck',name:'Trailer',kind:'trailer',wheels:[{x:-1,z:1},{x:1,z:1}],lowered:true,turnedOn:false}]};});await page.waitForTimeout(220);
    if(!await page.locator('#vehiclePanel').textContent().then(t=>t.includes('Test truck')&&t.includes('3 оси')))throw Error('Automatic truck selection failed');
    if(width===390||width===1340)await page.locator('#vehiclePanel').screenshot({path:`${process.argv[4] || '.'}/vehicle-${p.id}-${width}.png`});
    if(p.id==='ets2'&&await page.locator('#vehiclePanel .vehicleGhost').count()!==1)throw Error('Transparent trailer missing');
    await page.evaluate(()=>{window.fixtureVehicle={id:'custom',name:'New vehicle',kind:'unknown',controlled:true,wheels:[],attachments:[]};});await page.waitForTimeout(220);
    if(await page.locator('#vehiclePanel .vehicleWheel').count()!==0||await page.locator('#vehiclePanel .vehicleGhost').count()!==0)throw Error('Previous vehicle geometry survived switch');
    await page.evaluate(()=>{window.fixtureVehicle={id:'',name:'',kind:'unknown',controlled:false,wheels:[],attachments:[]};});await page.waitForTimeout(220);
    if(!await page.locator('#vehiclePanel').textContent().then(t=>t.includes('Вы не в технике')))throw Error('Dismount did not clear diagram');
    await page.evaluate(()=>window.fixtureVehicle=null);
   }
   if(p.id==='fs25'){await page.evaluate(()=>window.fixtureAgeMs=750);await page.waitForTimeout(300);if(!await page.locator('#profileDashboard [data-action="fs25Lower"]').evaluate(e=>e.classList.contains('on')))throw Error('FS25 indicator flickers after missed file poll');await page.evaluate(()=>window.fixtureAgeMs=1500);await page.waitForTimeout(300);if(await page.locator('#profileDashboard [data-action="fs25Lower"]').evaluate(e=>e.classList.contains('on')))throw Error('FS25 old indicator did not expire');await page.evaluate(()=>window.fixtureAgeMs=0);await page.waitForTimeout(300);}
   if(width===390||width===1340)await page.screenshot({path:`${process.argv[4] || '.'}/dashboard-${p.id}-${width}.png`,fullPage:true});
  }
  if(errors.length)throw Error(errors.join('\n'));console.log(`PASS ${width}x${height}: eight profiles, five F1 screens, all configured/custom actions, hold/release, disabled input, stale data, no overflow`);await page.close();
 }
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
