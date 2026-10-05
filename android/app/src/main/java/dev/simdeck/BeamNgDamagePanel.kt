package dev.simdeck
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import org.json.JSONObject

@Composable internal fun BeamNgDamagePanel(state:DeckState,model:DeckModel) {
 val d=LocalProfileDesign.current;val context=LocalContext.current
 val labels=remember{JSONObject(context.assets.open("beamng-damage-ui.json").bufferedReader().use{it.readText()})}
 val identity=state.telemetry?.vehicle?:state.lastVehicle;val v=identity?.takeIf{it.controlled};val damage=if(v!=null)state.telemetry?.beamNg else null
 var tab by rememberSaveable{mutableStateOf("Кузов")}
 val scene:@Composable ()->Unit={DesignCard {
  Text("ДИАГНОСТИКА МАШИНЫ",fontWeight=FontWeight.Black,fontSize=22.sp)
  Text(v?.name?:if(identity?.controlled==false)"Вы не в технике" else "Ждём данные машины",fontWeight=FontWeight.Bold,fontSize=20.sp,color=d.accent)
  Text(v?.let{"${vehicleLabels[it.kind]} · ${it.axleCount?.let{n->"$n оси"}?:"геометрия неизвестна"}"}?:"Нужен мод BeamNG 0.4.0",color=d.muted,fontSize=12.sp)
  if(v!=null)RoadDrawing(v.kind,v.wheels,heightOverride=330.dp,beam=damage)
  Text(if(state.stale)"Последние показания · обновление задержалось" else "Данные из игры · диагностика обновляется до 5 раз/с",color=d.muted,fontSize=12.sp)
  Text("Зелёный 0–5% · жёлтый 6–30% · красный >30%. Цвет колеса: красный — поломка, жёлтый — спущена шина.",color=d.muted,fontSize=11.sp)
 }}
 val details:@Composable ()->Unit={DesignCard {
  Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(8.dp)){listOf("Кузов","Колёса","Узлы","Детали").forEach{t->FilterChip(selected=tab==t,onClick={tab=t},label={Text(t)})}}
  if(damage==null){Text("Данные диагностики не поступили. Обновите мод BeamNG до 0.4.0 и перезагрузите машину Ctrl+R.",color=d.muted)}
  else when(tab) {
   "Кузов"->{listOf("FL","FR","ML","MR","RL","RR").forEach{k->val value=damage.body[k];Row(Modifier.fillMaxWidth().padding(vertical=9.dp),horizontalArrangement=Arrangement.SpaceBetween){Text(labels.getJSONObject("zones").getString(k));Text(value?.let{"%.0f%%".format(it*100)}?:"—",fontWeight=FontWeight.Bold,color=damageColor(value?.times(100)))};HorizontalDivider(color=d.line)};Text("Степень повреждения связей кузова по шести зонам. Это не процент стоимости ремонта.",fontSize=12.sp,color=d.muted)}
   "Колёса"->{if(damage.wheels.isEmpty())Text("Состояния колёс не переданы",color=d.muted);damage.wheels.forEach{w->Text(w.name.ifBlank{"Колесо ${w.index+1}"},fontWeight=FontWeight.Bold);listOf("Колесо / крепление" to w.broken,"Шина" to w.flat,"Тормоз" to w.brakeDamaged).forEach{(name,bad)->Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween){Text(name,fontSize=13.sp);Text(when(bad){true->if(name=="Шина")"Спущена" else "Повреждено";false->"Без отметки повреждения";null->"Нет данных"},fontSize=12.sp,color=when(bad){true->damageColor(100.0);false->damageColor(0.0);null->d.muted})}};Text(w.brakeTemperature?.let{"Тормоз %.0f °C".format(it)}?:"Температура тормоза —",fontSize=12.sp,color=d.muted);HorizontalDivider(color=d.line)}}
   "Узлы"->{Text("Охлаждение ${damage.coolantTemperature?.let{"%.0f °C".format(it)}?:"—"} · Масло ${damage.oilTemperature?.let{"%.0f °C".format(it)}?:"—"}",color=d.accent);labels.getJSONObject("faults").keys().asSequence().toList().sortedByDescending{damage.faults[it]==true}.forEach{k->val bad=damage.faults[k];Row(Modifier.fillMaxWidth().padding(vertical=7.dp),horizontalArrangement=Arrangement.SpaceBetween){Text(labels.getJSONObject("faults").getString(k),modifier=Modifier.weight(1f),fontSize=13.sp);Text(when(bad){true->"ЕСТЬ";false->"НЕТ";null->"—"},color=when(bad){true->damageColor(100.0);false->damageColor(0.0);null->d.muted},fontWeight=FontWeight.Bold)};HorizontalDivider(color=d.line)};Text("Статусы отмеченных игрой неисправностей. Прочерк означает недоступное состояние или оборудование.",fontSize=12.sp,color=d.muted)}
   else->{Text(damage.totalDamagedParts?.let{"Деталей с повреждениями: $it · показано ${damage.parts.size}"}?:"Данные деталей не переданы",fontWeight=FontWeight.Bold);damage.parts.forEach{p->Text(p.name,fontSize=14.sp);LinearProgressIndicator(progress={p.damage.toFloat()},color=damageColor(p.damage*100),trackColor=d.line,modifier=Modifier.fillMaxWidth());Text("Оценка повреждения %.0f%%".format(p.damage*100),color=damageColor(p.damage*100),fontSize=12.sp)};if(damage.totalDamagedParts==0)Text("Игра не отметила повреждённых деталей",color=d.accent);Text("До восьми наиболее повреждённых деталей по оценке BeamNG. Оценка учитывает разрыв и деформацию связей.",fontSize=12.sp,color=d.muted)}
  }
 }}
 BoxWithConstraints{if(maxWidth>=650.dp)Row(horizontalArrangement=Arrangement.spacedBy(16.dp)){Column(Modifier.weight(1f)){scene()};Column(Modifier.weight(1f)){details()}}else Column(verticalArrangement=Arrangement.spacedBy(12.dp)){scene();details()}}
 DesignCard {
  val t=state.telemetry
  val readings=listOf("КМ/Ч" to (t?.let{"%.0f".format(it.speedMps*3.6)}?:"—"),"ПЕРЕДАЧА" to (t?.let{Protocol.gear(it.gear,it.gearboxMode)}?:"—"),"RPM" to (t?.let{"%.0f".format(it.rpm)}?:"—"),"ТОПЛИВО" to (t?.fuelFraction?.let{"%.0f%%".format(it*100)}?:"—"))
  Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(8.dp)){readings.forEach{(label,value)->Column(Modifier.weight(1f)){Text(label,color=d.muted,fontSize=10.sp);Text(value,fontWeight=FontWeight.Bold,fontSize=23.sp,color=d.accent)}}}
 }
 ReferenceButtons(state,model,listOf("ignition" to "Зажигание","lights" to "Фары","hazards" to "Аварийка","recoverRoad" to "На дорогу"),4)
}
