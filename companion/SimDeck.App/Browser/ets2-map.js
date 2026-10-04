'use strict';
const etsMapState={slice:null,position:null,attempt:0,status:'Подготовка карты игры…',navigator:null};
async function navigatorRequest(path,body){
 const abort=new AbortController(),timer=setTimeout(()=>abort.abort(),15000);
 try{const r=await fetch(path,{method:body?'POST':'GET',headers:body?{'Content-Type':'application/json'}:{},body:body?JSON.stringify(body):undefined,signal:abort.signal,cache:'no-store'});const text=await r.text();if(text.length>12000000)throw Error('Ответ карты слишком большой');let j;try{j=JSON.parse(text);}catch{throw Error('Не удалось получить карту');}if(!r.ok)throw Error(j.error||'Карта пока недоступна');return j;}finally{clearTimeout(timer);}
}
function mountEtsMap(parent){
 etsMapState.navigator?.destroy();
 etsMapState.navigator=new SimDeckNavigator.Navigator(parent,{profile:profileId,request:navigatorRequest});
}
function updateEtsMap(data){
 const nav=etsMapState.navigator;if(!nav||!isScsTruck())return;
 if(nav.profile!==profileId){nav.destroy();etsMapState.navigator=null;return;}
 if(!nav.root.isConnected){nav.destroy();etsMapState.navigator=null;return;}
 nav.update({data,stale:!currentData(),connected:!!session});
}
function drawEtsMap(){etsMapState.navigator?.draw();}
function validEtsSlice(j){return SimDeckNavigator.validSlice(j);}
