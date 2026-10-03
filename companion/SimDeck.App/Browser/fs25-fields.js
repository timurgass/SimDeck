'use strict';
let fs25FieldLabels={crops:{},ground:{}}, fs25FieldSelected=null, fs25FieldCrop='', fs25FieldQuery='', fs25FieldSignature='', fs25FieldMap='';
fetch('/fs25-field-ui.json').then(r=>{if(!r.ok)throw Error('Field labels');return r.json();}).then(labels=>{fs25FieldLabels=labels;fs25FieldSignature='';}).catch(()=>{});
function fs25CropName(id){return fs25FieldLabels.crops[id]?.[0] || id || 'Культура не указана';}
function fs25CropColor(id){const color=fs25FieldLabels.crops[id]?.[1];return /^#[0-9a-f]{6}$/i.test(color||'')?color:'#A7B2A0';}
function fs25FieldNumber(f,key){const v=f[key];return Number.isInteger(v)&&v>=0?v:null;}
function fs25FieldPercent(v,max){return Number.isInteger(v)&&v>=0&&v<=max&&max>0?Math.round(v*100/max):null;}
function fs25FieldRows(details){return [...new Map((details?.fields||[]).filter(f=>Number.isInteger(f.id)&&f.id>0).map(f=>[f.id,f])).values()].sort((a,b)=>a.id-b.id);}
function renderFs25Fields(){
 let root=$('fs25Fields');if(!root){root=node('section','fs25Fields');root.id='fs25Fields';$('deck').append(root);}
 root.hidden=profileId!=='fs25'||page!=='Поля';if(root.hidden)return;
 const data=displayData()?.fs25, report=displayData()?.fs25Advisor;
 const map=[data?.header?.mapId,data?.header?.savegameName].join('|');
 if(map!==fs25FieldMap){fs25FieldMap=map;fs25FieldSelected=null;fs25FieldCrop=fs25FieldQuery='';fs25FieldSignature='';root.replaceChildren();}
 const rows=fs25FieldRows(data);
 if(!root.firstChild){
  const overview=node('div','card'),detail=node('div','card');overview.id='fs25FieldList';detail.id='fs25FieldDetail';root.append(overview,detail);
  overview.append(node('h2','','ПОЛЯ'),node('p','hint'));overview.lastChild.id='fs25FieldSaved';
  overview.append(node('p','hint','Сетка по номерам, не географическая карта. Цвет обозначает культуру. Принадлежность и площадь неизвестны. Состояния обновляются после сохранения FS25.'));
  const label=node('label','','Номер поля или культура'),search=node('input');search.id='fs25FieldSearch';search.type='search';search.maxLength=80;search.autocomplete='off';search.setAttribute('aria-label','Номер поля или культура');label.append(search);overview.append(label);
  search.oninput=()=>{fs25FieldQuery=search.value;renderFs25Fields();};
  const filters=node('nav','fs25FieldFilters');filters.id='fs25FieldFilters';filters.setAttribute('aria-label','Культуры');overview.append(filters);
  const grid=node('div','fs25FieldGrid');grid.id='fs25FieldGrid';overview.append(grid);
 }
 const signature=JSON.stringify([data,report,fs25FieldLabels,fs25FieldSelected,fs25FieldQuery,fs25FieldCrop]);if(signature===fs25FieldSignature)return;fs25FieldSignature=signature;
 const saved=$('fs25FieldSaved');saved.textContent=data?`${data.header?.savegameName||'Без названия'} · ${data.header?.mapTitle||''} · из сохранения: ${data.timestamp?new Date(data.timestamp).toLocaleString('ru-RU'):'—'}${data.isStale?' · данные устарели':''}`:'Сохранение FS25 пока не найдено. Сохраните игру и проверьте путь в Companion.';saved.classList.toggle('warning',data?.isStale===true);
 const filters=$('fs25FieldFilters');clear(filters);
 for(const id of ['',...new Set(rows.map(f=>f.fruitType||''))].filter((id,i,a)=>a.indexOf(id)===i)){
  const button=node('button',fs25FieldCrop===id?'selected':'',id?fs25CropName(id):'Все');button.type='button';button.onclick=()=>{fs25FieldCrop=id;renderFs25Fields();};filters.append(button);
 }
 const query=fs25FieldQuery.trim().toLocaleLowerCase('ru-RU'),visible=rows.filter(f=>(!fs25FieldCrop||f.fruitType===fs25FieldCrop)&&(!query||`${f.id} ${f.fruitType||''} ${fs25CropName(f.fruitType)}`.toLocaleLowerCase('ru-RU').includes(query)));
 const selected=visible.find(f=>f.id===fs25FieldSelected)||visible[0],grid=$('fs25FieldGrid');clear(grid);
 for(const f of visible){const b=node('button','fs25FieldTile');b.type='button';b.dataset.field=String(f.id);b.setAttribute('aria-pressed',String(f.id===selected?.id));b.style.setProperty('--crop-color',fs25CropColor(f.fruitType));b.append(node('strong','','№'+f.id),node('small','',fs25CropName(f.fruitType)));b.onclick=()=>{fs25FieldSelected=f.id;renderFs25Fields();};grid.append(b);}
 if(!visible.length)grid.append(node('p','hint',rows.length?'По этому фильтру полей нет.':'В fields.xml сохранения нет записей для отображения. Это не означает, что на карте нет полей.'));
 const detail=$('fs25FieldDetail');clear(detail);detail.hidden=!selected;if(!selected)return;
 const f=selected;detail.append(node('h2','','ПОЛЕ №'+f.id+' · '+fs25CropName(f.fruitType)),node('p','',fs25FieldLabels.ground[f.groundType]||f.groundType||'Состояние не указано'),node('p','hint',`Рост: стадия ${fs25FieldNumber(f,'growthState')??'—'} · предыдущая стадия ${fs25FieldNumber(f,'lastGrowthState')??'—'}`),node('p','hint','Планируемая культура: '+(f.plannedFruit?fs25CropName(f.plannedFruit):'—')));
 for(const [key,label,max] of [['weedState','Сорняки',9],['limeLevel','Известь',3],['sprayLevel','Удобрение',3],['plowLevel','Вспашка',1],['rollerLevel','Прикатывание',1],['stubbleShredLevel','Мульчирование',1]]){
  const v=fs25FieldPercent(fs25FieldNumber(f,key),max),row=node('div','fs25FieldLevel');row.dataset.fieldMetric=key;row.append(node('span','',label),node('strong','',v===null?'—':`${v}%`));if(v!==null){const bar=node('progress');bar.max=100;bar.value=v;bar.setAttribute('aria-label',label);row.append(bar);}detail.append(row);
 }
 detail.append(node('p','hint',`Камни: ${fs25FieldNumber(f,'stoneLevel')??'—'} · вода: ${fs25FieldNumber(f,'waterLevel')??'—'} · тип удобрения: ${f.sprayType||'—'}`),node('h3','','СОВЕТНИК'));
 const alerts=(report?.alerts||[]).filter(a=>a.field===f.id);if(!alerts.length)detail.append(node('p','hint','Советник не передал предупреждений для этого поля.'));
 for(const a of alerts)detail.append(node('p',a.severity>=2?'warning':'hint',a.message));
 for(const t of (report?.tasks||[]).filter(t=>t.task?.field===f.id))detail.append(node('p','hint',(['К исполнению','Выполнено','Вне сезона','Нет поля'][t.status]||'Неизвестный статус')+' · '+(t.task?.title||'')));
}
