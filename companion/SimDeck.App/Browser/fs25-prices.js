'use strict';
let fs25PriceCrop='', fs25PriceQuery='', fs25PriceSignature='', fs25PriceOptionsSignature='';
const fs25Alphabet = new Intl.Collator('ru', {numeric:true,sensitivity:'base'});
function fs25PriceOffers(prices,crop='') {
 return (prices?.offers||[]).filter(o=>typeof o.crop==='string' && o.crop && typeof o.station==='string' && o.station && Number.isFinite(o.pricePer1000) && o.pricePer1000>0 && o.pricePer1000<100000000 && (!crop||o.crop===crop)).sort((a,b)=>fs25Alphabet.compare(a.cropName||a.crop,b.cropName||b.crop)||fs25Alphabet.compare(a.crop,b.crop)||fs25Alphabet.compare(a.station,b.station)||b.pricePer1000-a.pricePer1000);
}
function renderFs25Prices(){
 let root=$('fs25Prices');if(!root){root=node('section','card fs25Prices');root.id='fs25Prices';$('deck').append(root);}
 root.hidden=profileId!=='fs25'||page!=='Цены';if(root.hidden)return;
 const prices=displayData()?.fs25Prices,offers=fs25PriceOffers(prices),crops=[...new Map(offers.map(o=>[o.crop,o.cropName||o.crop])).entries()].sort((a,b)=>fs25Alphabet.compare(a[1],b[1])||fs25Alphabet.compare(a[0],b[0]));
 if(!crops.some(([id])=>id===fs25PriceCrop))fs25PriceCrop='';
 if(!root.firstChild){
  root.append(node('h2','','ЦЕНЫ НА КУЛЬТУРЫ И ТОВАРЫ'),node('p','hint','Текущие предложения пунктов продажи · за 1 000 л'));
  const status=node('p','hint');status.id='fs25PriceStatus';root.append(status);
  const searchLabel=node('label','','Поиск культуры или товара'),search=node('input');search.id='fs25PriceSearch';search.type='search';search.maxLength=80;search.autocomplete='off';search.setAttribute('aria-label','Поиск культуры или товара');searchLabel.append(search);root.append(searchLabel);
  const selectLabel=node('label','','Культура или товар'),select=node('select');select.id='fs25PriceSelect';select.setAttribute('aria-label','Культура или товар');selectLabel.append(select);root.append(selectLabel);
  search.oninput=()=>{fs25PriceQuery=search.value;renderFs25Prices();};
  select.onchange=()=>{fs25PriceCrop=select.value;renderFs25Prices();};
  const rows=node('div');rows.id='fs25PriceRows';root.append(rows);
 }
 const query=fs25PriceQuery.trim().toLocaleLowerCase('ru-RU');
 const optionsSignature=JSON.stringify([crops,query,fs25PriceCrop]);
 if(optionsSignature!==fs25PriceOptionsSignature){
  fs25PriceOptionsSignature=optionsSignature;const select=$('fs25PriceSelect');clear(select);
  const all=node('option','','Все культуры и товары');all.value='';select.append(all);
  for(const [id,name]of crops)if(id===fs25PriceCrop||!query||name.toLocaleLowerCase('ru-RU').includes(query)){const option=node('option','',name);option.value=id;select.append(option);}
  select.value=fs25PriceCrop;
 }
 const delayed=!session || !prices || prices.ageMs+Math.max(0,performance.now()-frameAt)>15000;
 const signature=JSON.stringify([prices?.offers,fs25PriceCrop,delayed]);if(signature===fs25PriceSignature)return;fs25PriceSignature=signature;
 $('fs25PriceStatus').textContent=!prices?'Ждём цены из мода SimDeck FS25 версии 1.4.0.0 или новее.':delayed?'Последние цены · обновление задержалось':'Из игры · обновляются каждые 5 секунд';
 const rows=fs25PriceOffers(prices,fs25PriceCrop),list=$('fs25PriceRows');clear(list);
 if(!rows.length)list.append(node('p','hint',prices?'В этом сохранении пока нет предложений продажи.':'Запустите сохранение с модом. Цены не подставляются из справочника.'));
 const best=new Map();for(const o of offers)best.set(o.crop,Math.max(best.get(o.crop)||0,o.pricePer1000));
 for(const o of rows){const row=node('div','fs25PriceRow');row.dataset.crop=o.crop;const label=node('div');label.append(node('strong','',o.cropName||o.crop),node('small','hint',o.station));const price=node('div');price.append(node('strong','',o.formatted||Math.round(o.pricePer1000)+' (валюта игры)'));if(o.pricePer1000===best.get(o.crop))price.append(node('small','hint','Лучшее предложение'));row.append(label,price);list.append(row);}
}
