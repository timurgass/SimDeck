'use strict';
let farmLabels={},farmIdentity='',farmId=null,farmTab='Финансы',farmDay=null,farmQuery='',farmRenderKey='';
fetch('/fs25-farm-ui.json').then(r=>r.json()).then(v=>{farmLabels=v;farmRenderKey='';renderFs25Farm();}).catch(()=>{});
const farmNumber=n=>Number.isFinite(n)?n.toLocaleString('ru-RU',{maximumFractionDigits:0}):'—';
function farmMount(card){
 let root=$('fs25FarmPanel');if(!root){root=node('section','card farmOperations');root.id='fs25FarmPanel';root.append(node('h2','','ХОЗЯЙСТВО'),node('p','hint farmSaved'));
  const select=node('select');select.id='farmAccount';select.setAttribute('aria-label','Хозяйство');select.onchange=()=>{farmId=Number(select.value);farmDay=null;renderFs25Farm();};root.append(select,node('p','farmBalance'));
  const tabs=node('nav','farmTabs');for(const name of ['Финансы','Запасы','Техника','Производства']){const b=node('button','',name);b.type='button';b.dataset.farmTab=name;b.onclick=()=>{farmTab=name;renderFs25Farm();};tabs.append(b);}root.append(tabs);
  const search=node('input');search.id='farmSearch';search.type='search';search.placeholder='Культура, товар или место хранения';search.setAttribute('aria-label',search.placeholder);search.maxLength=80;search.oninput=()=>{farmQuery=search.value;renderFs25Farm();};root.append(search);
  const day=node('select');day.id='farmDay';day.setAttribute('aria-label','Запись финансового дня');day.onchange=()=>{farmDay=Number(day.value);renderFs25Farm();};root.append(day,node('div','farmContent'));}
 if(root.parentNode!==card.parentNode)card.before(root);return root;
}
function farmOptions(select,options,value){const key=JSON.stringify(options);if(select.dataset.options!==key){select.replaceChildren(...options.map(([id,label])=>{const o=node('option','',label);o.value=id;return o;}));select.dataset.options=key;}select.value=value??'';}
function renderFs25Farm(){
 if(typeof profileId==='undefined')return;const card=$('fs25Overview');if(!card)return;let root=$('fs25FarmPanel');if(profileId!=='fs25'){if(root)root.hidden=true;return;}
 root=farmMount(card);root.hidden=false;const data=displayData(),details=data?.fs25,ops=details?.operations;
 const identity=[details?.header?.savegameName,details?.header?.mapTitle].join('|');if(identity!==farmIdentity){farmIdentity=identity;farmId=null;farmDay=null;farmQuery='';$('farmSearch').value='';}
 const account=details?.farms?.find(f=>f.farmId===farmId)||details?.farms?.[0];farmId=account?.farmId??null;
 const price=data?.fs25Prices,priceFresh=!!currentData()&&price&&Number.isFinite(price.ageMs)&&price.ageMs+performance.now()-frameAt<15000;
 const offers=priceFresh?(price.offers||[]):[],relevant=(ops?.stocks||[]).filter(s=>s.farmId===farmId);
 const best=Object.fromEntries([...new Set(relevant.map(s=>s.crop))].map(crop=>[crop,offers.filter(p=>p.crop===crop).sort((a,b)=>b.pricePer1000-a.pricePer1000)[0]]));
 const key=[identity,details?.timestamp,!!ops,details?.isStale,account?.money,account?.loan,farmId,farmTab,farmQuery,farmDay,priceFresh,JSON.stringify(best)].join('|');
 if(farmRenderKey===key)return;farmRenderKey=key;
 root.querySelector('.farmSaved').textContent='Последнее сохранение: '+(details?.timestamp?new Date(details.timestamp).toLocaleString('ru-RU'):'—')+'. Сохраните ферму, чтобы обновить остатки и финансы. Суммы в денежных единицах игры.';
 farmOptions($('farmAccount'),(details?.farms||[]).map(f=>[f.farmId,f.name||'Ферма №'+f.farmId]),farmId);
 root.querySelector('.farmBalance').textContent='Баланс '+farmNumber(account?.money)+' · Кредит '+farmNumber(account?.loan);
 root.querySelectorAll('[data-farm-tab]').forEach(b=>b.classList.toggle('active',b.dataset.farmTab===farmTab));$('farmSearch').hidden=farmTab!=='Запасы';$('farmDay').hidden=farmTab!=='Финансы';
 const content=root.querySelector('.farmContent');content.replaceChildren();const add=(tag,text,cls='')=>{const e=node(tag,cls,text);content.append(e);return e;};
 if(!ops){add('p','Дополнительные данные хозяйства пока не поступили. Обновите Companion.','hint');return;}
 if(ops.truncated)add('p','Большое сохранение: показана часть подробностей хозяйства. Итоги сохранённых финансовых записей остаются полными.','hint');
 const alphabet=new Intl.Collator('ru',{numeric:true,sensitivity:'base'});
 if(farmTab==='Финансы'){
  const days=ops.ledgers.find(l=>l.farmId===farmId)?.days||[],selected=days.find(d=>d.day===farmDay)||days[0];farmOptions($('farmDay'),days.map(d=>[d.day,'День '+(d.day+1)]),selected?.day);
  if(!selected){add('p','В сохранении нет финансовых записей этой фермы.','hint');return;}
  add('p',`Доход ${farmNumber(selected.income)} · Расход ${farmNumber(selected.expenses)} · Итог ${farmNumber(selected.net)}`,'farmTotals');
  for(const e of [...selected.entries].sort((a,b)=>alphabet.compare(farmLabels.finance?.[a.category]||a.category,farmLabels.finance?.[b.category]||b.category))){const row=add('div','', 'farmLine');row.append(node('span','',farmLabels.finance?.[e.category]||e.category),node('strong',e.amount<0?'farmExpense':'',farmNumber(e.amount)));}
  if(!selected.entries.length)add('p','В этой записи нет доходов и расходов.','hint');
 }else if(farmTab==='Запасы'){
  const grouped=Object.create(null);for(const s of relevant)(grouped[s.crop]??=[]).push(s);const groups=Object.entries(grouped).sort((a,b)=>alphabet.compare(fs25CropName(a[0]),fs25CropName(b[0]))).filter(([crop,items])=>!farmQuery||(fs25CropName(crop)+' '+crop+' '+items.map(s=>s.location).join(' ')).toLocaleLowerCase('ru').includes(farmQuery.toLocaleLowerCase('ru')));
  if(!groups.length)add('p',!ops.storageAvailable&&!ops.fleetAvailable?'Файлы запасов в сохранении отсутствуют.':'В прочитанных складах и технике нет запасов по этому фильтру.','hint');
  for(const [crop,items] of groups){const litres=items.reduce((n,s)=>n+s.litres,0),offer=best[crop];add('h3',(offer?.cropName||fs25CropName(crop))+' · '+farmNumber(litres)+' л');add('p',items.map(s=>(s.source==='storage'?'Склад':'В технике')+': '+s.location+' '+farmNumber(s.litres)+' л').join(' · '),'hint');add('p',offer?'Оценка продажи ≈ '+farmNumber(litres/1000*offer.pricePer1000)+' · '+offer.station:'Оценка продажи — нет свежего предложения');}
  add('p','Оценка по лучшей текущей цене, без доставки и затрат. Наземные кучи, тюки и содержимое модифицированных хранилищ могут отсутствовать.','hint');
 }else if(farmTab==='Техника'){
  const fleet=ops.fleet.filter(v=>v.farmId===farmId).sort((a,b)=>(b.damage??-1)-(a.damage??-1));
  if(!fleet.length)add('p',ops.fleetAvailable?'У этой фермы не найдена техника.':'vehicles.xml отсутствует.','hint');
  for(const v of fleet){add('h3',v.model);add('p',(farmLabels.property?.[v.property]||v.property)+' · Наработка '+(Number.isFinite(v.hours)?v.hours.toFixed(1)+' ч':'—')+' · Повреждение '+farmNumber(v.damage)+(Number.isFinite(v.damage)?'%':''),(v.damage??0)>=30?'farmExpense':'hint');}
 }else{
  const recipes=ops.productions.filter(p=>p.farmId===farmId).sort((a,b)=>alphabet.compare(a.building+a.recipe,b.building+b.recipe));
  if(!recipes.length)add('p',ops.storageAvailable?'У этой фермы нет сохранённых производств.':'placeables.xml отсутствует.','hint');
  for(const p of recipes){add('h3',p.building+' · '+p.recipe);add('p',p.enabled===true?'Включено':p.enabled===false?'Выключено':'Состояние неизвестно','hint');}
  add('p','Переключатель рецепта из сохранения: включение не подтверждает наличие сырья или текущую работу производства.','hint');
 }
}
