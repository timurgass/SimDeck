'use strict';
let fs25PriceCrop='', fs25PriceSignature='';
function fs25PriceOffers(prices,crop='') {
 return (prices?.offers||[]).filter(o=>typeof o.crop==='string' && o.crop && typeof o.station==='string' && o.station && Number.isFinite(o.pricePer1000) && o.pricePer1000>0 && o.pricePer1000<100000000 && (!crop||o.crop===crop)).sort((a,b)=>b.pricePer1000-a.pricePer1000);
}
function renderFs25Prices(){
 let root=$('fs25Prices');if(!root){root=node('section','card fs25Prices');root.id='fs25Prices';$('deck').append(root);}
 root.hidden=profileId!=='fs25'||page!=='Цены';if(root.hidden)return;
 const prices=displayData()?.fs25Prices,offers=fs25PriceOffers(prices),crops=[...new Map(offers.map(o=>[o.crop,o.cropName||o.crop])).entries()].sort((a,b)=>a[1].localeCompare(b[1],'ru'));
 if(!crops.some(([id])=>id===fs25PriceCrop))fs25PriceCrop='';
 const delayed=!session || !prices || prices.ageMs+Math.max(0,performance.now()-frameAt)>15000;
 const signature=JSON.stringify([prices?.offers,fs25PriceCrop,delayed]);if(signature===fs25PriceSignature)return;fs25PriceSignature=signature;clear(root);
 root.append(node('h2','','ЦЕНЫ НА КУЛЬТУРЫ'),node('p','hint','Текущие предложения пунктов продажи · за 1 000 л'),node('p','hint',!prices?'Ждём цены из мода SimDeck FS25 версии 1.4.0.0.':delayed?'Последние цены · обновление задержалось':'Из игры · обновляются каждые 5 секунд'));
 const filters=node('nav','fs25FieldFilters');root.append(filters);
 for(const [id,name] of [['','Все'],...crops])tab(filters,name,id===fs25PriceCrop,()=>{fs25PriceCrop=id;renderFs25Prices();});
 const rows=fs25PriceOffers(prices,fs25PriceCrop);
 if(!rows.length)root.append(node('p','hint',prices?'В этом сохранении пока нет предложений продажи.':'Запустите сохранение с обновлённым модом. Цены не подставляются из справочника.'));
 rows.forEach((o,i)=>{const row=node('div','fs25PriceRow');row.dataset.crop=o.crop;const label=node('div');label.append(node('strong','',o.cropName||o.crop),node('small','hint',o.station));const price=node('div');price.append(node('strong','',o.formatted||Math.round(o.pricePer1000)+' (валюта игры)'));if(i===0 && fs25PriceCrop)price.append(node('small','hint','Лучшее предложение'));row.append(label,price);root.append(row);});
}
