package dev.simdeck

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import org.json.JSONObject
import java.util.Locale

private val FarmAmber=Color(0xFFFFBD64)

internal fun fs25FarmValue(n:Double?)=n?.let{String.format(Locale.forLanguageTag("ru-RU"),"%,.0f",it)}?:"—"
@Composable internal fun Fs25FarmPanel(state:DeckState) {
    val data=state.telemetry?.fs25?:return
    val d=LocalProfileDesign.current
    val context=LocalContext.current
    val labels=remember{JSONObject(context.assets.open("fs25-farm-ui.json").bufferedReader().use{it.readText()})}
    val crops=remember{Fs25FieldLabels(JSONObject(context.assets.open("fs25-field-ui.json").bufferedReader().use{it.readText()}))}
    var farmId by rememberSaveable(data.saveName,data.mapName){mutableStateOf<Int?>(null)}
    var tab by rememberSaveable{mutableStateOf("Финансы")}
    var query by rememberSaveable{mutableStateOf("")}
    var day by rememberSaveable{mutableStateOf<Int?>(null)}
    val farm=data.farms.firstOrNull{it.id==farmId}?:data.farms.firstOrNull()
    val ops=data.operations
    DesignCard {
        Text("ХОЗЯЙСТВО",fontWeight=FontWeight.Bold,fontSize=22.sp,color=d.accent)
        Text("Последнее сохранение: ${data.savedAt}. Сохраните ферму, чтобы обновить остатки и финансы. Суммы в денежных единицах игры.",color=d.muted,fontSize=12.sp)
        Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(8.dp)){data.farms.forEach{f->FilterChip(selected=f.id==farm?.id,onClick={farmId=f.id;day=null},label={Text(f.name.ifBlank{"Ферма №${f.id}"})})}}
        Text("Баланс ${fs25FarmValue(farm?.money)} · Кредит ${fs25FarmValue(farm?.loan)}",fontWeight=FontWeight.Bold)
        Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(8.dp)){listOf("Финансы","Запасы","Техника","Производства").forEach{t->FilterChip(selected=t==tab,onClick={tab=t},label={Text(t)})}}
        if(ops==null){Text("Дополнительные данные хозяйства пока не поступили. Обновите Companion.",color=d.muted);return@DesignCard}
        if(ops.truncated)Text("Большое сохранение: показана часть записей, запасов и техники. Итоги сохранённых финансовых дней полные.",color=FarmAmber,fontSize=12.sp)
        when(tab){
            "Финансы"->{
                val days=ops.ledgers.firstOrNull{it.farmId==farm?.id}?.days.orEmpty()
                val selected=days.firstOrNull{it.day==day}?:days.firstOrNull()
                Fs25Selector("Запись финансового дня",days.map{it.day.toString() to "День ${it.day+1}"},selected?.day?.toString()){day=it?.toIntOrNull()}
                if(selected==null)Text("В сохранении нет финансовых записей этой фермы.",color=d.muted)
                else {
                    Text("Доход ${fs25FarmValue(selected.income)} · Расход ${fs25FarmValue(selected.expenses)} · Итог ${fs25FarmValue(selected.net)}",fontWeight=FontWeight.Bold)
                    selected.entries.sortedWith{a,b->fs25Alphabet().compare(labels.getJSONObject("finance").optString(a.category,a.category),labels.getJSONObject("finance").optString(b.category,b.category))}.forEach{e->
                        Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween){Text(labels.getJSONObject("finance").optString(e.category,e.category),modifier=Modifier.weight(1f));Text(fs25FarmValue(e.amount),color=if(e.amount<0)FarmAmber else d.accent)}
                    }
                    if(selected.entries.isEmpty())Text("В этой записи нет доходов и расходов.",color=d.muted)
                }
            }
            "Запасы"->{
                OutlinedTextField(value=query,onValueChange={query=it.take(80)},label={Text("Культура, товар или место хранения")},singleLine=true,modifier=Modifier.fillMaxWidth())
                val price=state.telemetry?.fs25Prices
                val priceFresh=state.connected&&price!=null&&price.ageMs+(System.nanoTime()-price.receivedAtNanos)/1000000<15000
                val offers=if(priceFresh)price?.offers.orEmpty() else emptyList()
                val rows=ops.stocks.filter{it.farmId==farm?.id}.groupBy{it.crop}.entries.sortedWith{a,b->fs25Alphabet().compare(crops.crop(a.key),crops.crop(b.key))}
                    .filter{e->query.isBlank()||(crops.crop(e.key)+" "+e.key+" "+e.value.joinToString(" "){it.location}).contains(query,true)}
                if(rows.isEmpty())Text(if(!ops.storageAvailable&&!ops.fleetAvailable)"Файлы запасов в сохранении отсутствуют." else "В прочитанных складах и технике нет запасов по этому фильтру.",color=d.muted)
                rows.forEach{(crop,items)->
                    val best=offers.filter{it.crop==crop}.maxByOrNull{it.pricePer1000}
                    val litres=items.sumOf{it.litres}
                    Text("${best?.cropName?.ifBlank{crops.crop(crop)}?:crops.crop(crop)} · ${fs25FarmValue(litres)} л",fontWeight=FontWeight.Bold)
                    Text(items.joinToString(" · "){(if(it.source=="storage")"Склад" else "В технике")+": ${it.location} ${fs25FarmValue(it.litres)} л"},color=d.muted,fontSize=12.sp)
                    Text(best?.let{"Оценка продажи ≈ ${fs25FarmValue(litres/1000*it.pricePer1000)} · ${it.station}"}?:"Оценка продажи — нет свежего предложения",color=d.accent,fontSize=12.sp)
                    HorizontalDivider(color=d.line)
                }
                Text("Оценка по лучшей текущей цене, без доставки и затрат. Наземные кучи, тюки и содержимое модифицированных хранилищ могут отсутствовать.",color=d.muted,fontSize=12.sp)
            }
            "Техника"->{
                val fleet=ops.fleet.filter{it.farmId==farm?.id}.sortedByDescending{it.damage?:-1.0}
                if(fleet.isEmpty())Text(if(ops.fleetAvailable)"У этой фермы не найдена техника." else "vehicles.xml отсутствует.",color=d.muted)
                fleet.forEach{v->Text(v.model,fontWeight=FontWeight.Bold);Text("${labels.getJSONObject("property").optString(v.property,v.property)} · Наработка ${v.hours?.let{String.format(Locale.US,"%.1f ч",it)}?:"—"} · Повреждение ${fs25FarmValue(v.damage)}${if(v.damage!=null)"%" else ""}",color=if((v.damage?:0.0)>=30)FarmAmber else d.muted,fontSize=12.sp)}
            }
            "Производства"->{
                val recipes=ops.productions.filter{it.farmId==farm?.id}.sortedBy{it.building+it.recipe}
                if(recipes.isEmpty())Text(if(ops.storageAvailable)"У этой фермы нет сохранённых производств." else "placeables.xml отсутствует.",color=d.muted)
                recipes.forEach{p->Text("${p.building} · ${p.recipe}",fontWeight=FontWeight.Bold);Text(when(p.enabled){true->"Включено";false->"Выключено";null->"Состояние неизвестно"},color=d.muted)}
                Text("Переключатель рецепта из сохранения: включение не подтверждает наличие сырья или текущую работу производства.",color=d.muted,fontSize=12.sp)
            }
        }
    }
}
