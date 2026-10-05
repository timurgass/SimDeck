const {chromium,webkit}=require('playwright'),fs=require('fs'),path=require('path');
const profiles=JSON.parse(fs.readFileSync(process.argv[2],'utf8')),out=process.argv[4]||'artifacts/acc-qa';fs.mkdirSync(out,{recursive:true});
(async()=>{
 const browser=await(process.env.SIMDECK_BROWSER==='webkit'?webkit:chromium).launch({headless:true,...(process.env.SIMDECK_BROWSER_EXECUTABLE?{executablePath:process.env.SIMDECK_BROWSER_EXECUTABLE}:{})});
 for(const [width,height] of [[390,844],[800,1340],[1340,800]]){
  const page=await browser.newPage({viewport:{width,height}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/status',r=>r.fulfill({json:{}}));
  await page.addInitScript(({profile})=>{
   window.age=0;window.commands=[];window.race={track:'Monza',trackId:1,trackLength:5793,sessionType:10,phase:5,remainingSeconds:600,fresh:true,replay:false,bins:256,
    points:Array.from({length:256},(_,bin)=>({bin,x:Math.cos(bin/256*2*Math.PI)*850,z:Math.sin(bin/256*2*Math.PI)*500})),
    drivers:['Alex Morgan','Player Test','Chris Smith'].map((name,i)=>({index:i,name,shortName:name.split(' ').at(-1).slice(0,3),number:20+i,team:'Test team',model:7,cup:i===2?2:3,position:i+1,cupPosition:i+1,lap:3,spline:.1+i*.1,x:Math.cos((.1+i*.1)*2*Math.PI)*850,z:Math.sin((.1+i*.1)*2*Math.PI)*500,location:1,bestLapMs:95000,lastLapMs:96000,gapAheadMs:i?1200:null,gapLeaderMs:i*1200,player:i===1,fresh:true}))};
   window.WebSocket=class{readyState=1;constructor(){setTimeout(()=>{this.onmessage?.({data:JSON.stringify({protocolMajor:1,type:'hello',sessionId:'qa',profileId:'acc',profileName:'ACC',profileRevision:1,controls:profile.actions})});this.tick();setInterval(()=>this.tick(),100)},10)}tick(){this.onmessage?.({data:JSON.stringify({protocolMajor:1,type:'input.state',sessionId:'qa',availability:'ready'})});this.onmessage?.({data:JSON.stringify({protocolMajor:1,type:'telemetry.snapshot',sessionId:'qa',source:'acc',ageMs:window.age,data:{speedMps:50,rpm:6200,maxRpm:8000,gear:4,gearDisplay:'4',fuelFraction:.5,actionStates:{accPitLimiter:false},acc:{race:window.race,wheels:Array.from({length:4},()=>({coreTemperature:83,pressure:27.5,brakeTemperature:400,wear:4})),waterTemperature:90,roadTemperature:31}}})})}send(s){window.commands.push(JSON.parse(s))}close(){this.readyState=3}};
  },{profile:profiles.find(p=>p.id==='acc')});
  await page.goto(process.argv[3]);await page.locator('#deck').waitFor({state:'visible'});await page.locator('#pages [data-page="Трасса"]').click();
  await page.locator('.accDriver').first().waitFor();if(await page.locator('.accDriver').count()!==3)throw Error('Missing opponents');
  if(!await page.locator('.accDriver.player').textContent().then(s=>s.includes('Player Test')))throw Error('Player identity lost');
  await page.locator('[data-cup="2"]').click();if(await page.locator('.accDriver').count()!==1)throw Error('Cup filtering failed');
  await page.locator('[data-cup="-1"]').click();await page.locator('.accDriver').nth(1).click({delay:350});if(!await page.locator('.accDriverDetail').textContent().then(s=>s.includes('1:35.000')))throw Error('Lap details missing');
  await page.locator('.accDriver').nth(1).click({delay:350});if(await page.locator('.accDriverDetail').count()!==0)throw Error('Pilot details cannot be closed');await page.locator('.accDriver').nth(1).click();
  await page.evaluate(()=>{window.age=3000;window.race.fresh=false});await page.waitForTimeout(250);
  if(await page.locator('.accDriver').count()!==3||!await page.locator('.accRaceStatus').textContent().then(s=>s.includes('Последние')))throw Error('Stale state erased map');
  await page.locator('#pages [data-page="Гонка"]').click();await page.locator('[data-action="accPitLimiter"]').first().click();if(!await page.evaluate(()=>commands.some(x=>x.actionId==='accPitLimiter')))throw Error('Stale race disabled ordinary controls');
  await page.locator('#pages [data-page="Трасса"]').click();await page.evaluate(()=>{window.age=0;window.race.fresh=true});await page.waitForTimeout(250);
  if(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth))throw Error('Horizontal overflow '+width);
  await page.locator('#accRace').screenshot({path:path.join(out,`acc-race-${process.env.SIMDECK_BROWSER||'chromium'}-${width}.png`)});
  if(errors.length)throw Error(errors.join('\n'));console.log('PASS ACC race, filters, stale controls and layout '+width);await page.close();
 }
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
