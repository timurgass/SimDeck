// Exercise the real browser renderer with every reviewed class and source rectangle.
const {chromium,webkit}=require('playwright'),fs=require('fs');
const manifest=JSON.parse(fs.readFileSync('assets/fs25-equipment.json','utf8'));
(async()=>{
 const engine=process.env.SIMDECK_BROWSER==='webkit'?webkit:chromium;
 const browser=await engine.launch({headless:true,...(process.env.SIMDECK_BROWSER_EXECUTABLE?{executablePath:process.env.SIMDECK_BROWSER_EXECUTABLE}:{})});
 for(const width of [390,1340]){
  const page=await browser.newPage({viewport:{width,height:800}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));await page.route('**/status',r=>r.fulfill({json:{}}));
  await page.goto(process.argv[2]||'http://127.0.0.1:18978/');await page.waitForFunction(()=>typeof farmSvg==='function');
  await page.evaluate(()=>{profileId='fs25';document.querySelectorAll('body>header,body>main,body>footer').forEach(e=>e.hidden=true);const qa=document.createElement('main');qa.id='catalogQa';document.body.append(qa);mountVehiclePanel(qa);});
  // Real hitch compositions must retain BOTH source aspect ratios, on either side.
  await page.evaluate(kinds=>{
   for(const root of Object.values(kinds))for(const tool of Object.values(kinds))for(const front of [false,true]){
    const g=farmGeometry(320,230,root.rect[2]/root.rect[3],tool.rect[2]/tool.rect[3],front);
    for(const [r,source] of [[g.root,root],[g.tool,tool]]){
     if(Math.abs(r.w/r.h-source.rect[2]/source.rect[3])>1e-8)throw Error('Stretched hitch sprite');
     if(r.x<-.001||r.x+r.w>320.001||r.y<0||r.y+r.h>230.001)throw Error('Hitch composition outside viewport');
    }
    if(g.tool.h>=g.root.h)throw Error('Attachment taller than its machine');
   }
   const qa=document.querySelector('#catalogQa');
   for(const [kind,tool,mount] of [['tractor','trailer','rear'],['combine','header','front'],['tractor','seeder','rear']]){
    const svg=farmSvg(kind,{kind:tool,mount});qa.append(svg);
    const sprites=svg.querySelectorAll('.vehicleSprite');
    for(const sprite of sprites){const source=sprite.getAttribute('viewBox').split(' ').map(Number);if(Math.abs(Number(sprite.getAttribute('width'))/Number(sprite.getAttribute('height'))-source[2]/source[3])>1e-8)throw Error('Rendered SVG stretched');}
    svg.remove();
   }
  },manifest.kinds);
  for(const[kind,info] of Object.entries(manifest.kinds)){
   const result=await page.evaluate(async({kind,info})=>{
    updateVehiclePanel({vehicle:{id:'fixture',name:info.label,kind,controlled:true,wheels:[],attachments:[]}});
    const panel=document.querySelector('#vehiclePanel'),image=panel.querySelector('image'),im=new Image();im.src=image.getAttribute('href');await im.decode();
    return {src:image.getAttribute('href'),rect:panel.querySelector('.vehicleSprite').getAttribute('viewBox'),width:im.naturalWidth,height:im.naturalHeight,overflow:document.documentElement.scrollWidth>innerWidth};
   },{kind,info});
   if(result.src!==`/vehicles/${info.atlas}.png`||result.rect!==info.rect.join(' ')||result.overflow)throw Error(`Incorrect class rendering ${kind}: ${JSON.stringify(result)}`);
   if(info.rect[0]+info.rect[2]>result.width||info.rect[1]+info.rect[3]>result.height)throw Error(`Rectangle outside source: ${kind}`);
  }
  // A gallery uses the actual app's drawing function, labelled as class illustrations.
  await page.evaluate(()=>{
   const qa=document.querySelector('#catalogQa');qa.replaceChildren();qa.style.display='grid';qa.style.gridTemplateColumns='repeat(3,minmax(0,1fr))';
   for(const kind of ['tractor','combine','header','plow','cultivator','seeder','baler','trailer','mower']){
    const panel=document.createElement('section');panel.className='card';const label=document.createElement('h3');label.textContent=vehicleLabels[kind];panel.append(label,farmSvg(kind,null));qa.append(panel);
   }
  });
  if(width===1340){await page.locator('#catalogQa').screenshot({path:'artifacts/ui-096/fs25-flat-gallery.png'});}
  if(errors.length)throw Error(errors.join('\n'));
  console.log(`PASS FS25 catalog ${width}: ${Object.keys(manifest.kinds).length} classes, exact shared atlas crops, no overflow`);await page.close();
 }
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
