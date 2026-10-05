package dev.simdeck

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.util.Locale

internal fun phoneDesign(id:String):ProfileDesign {
    val base=profileDesign(id)
    val colors=when(id) {
        "f1-24","f1-25" -> listOf(0xFF17191D,0xFF202226,0xFF2B2D33,0xFF484044)
        "beamng-default" -> listOf(0xFF181C1E,0xFF242A2E,0xFF30373B,0xFF434C50)
        "acc" -> listOf(0xFF111314,0xFF1D2020,0xFF2B2E2D,0xFF45453D)
        "ams2" -> listOf(0xFF0C1C24,0xFF132E37,0xFF20414A,0xFF356061)
        "ets2", "ats" -> listOf(0xFF1B2325,0xFF273133,0xFF364042,0xFF535950)
        "snowrunner" -> listOf(0xFF1A221E,0xFF27322B,0xFF384439,0xFF58604C)
        "fs25" -> listOf(0xFF17241D,0xFF223428,0xFF304635,0xFF49624C)
        else -> listOf(0xFF17191D,0xFF202226,0xFF2B2D33,0xFF484044)
    }
    return base.copy(background=Color(colors[0]),panel=Color(colors[1]),panelAlt=Color(colors[2]),line=Color(colors[3]),muted=Color(0xFFB5BCB9),radius=10,
        accent=when(id){"fs25"->Color(0xFF76BB59);"snowrunner"->Color(0xFFD8A961);else->base.accent})
}

/** Phone composition shares the tablet's commands, telemetry and vehicle artwork. */
@Composable internal fun PhoneDashboard(state:DeckState,model:DeckModel,connection:()->Unit,initialTab:String="drive") {
    val d=LocalProfileDesign.current
    val tabs=phoneTabs(state.profileId)
    var selected by rememberSaveable(state.profileId,initialTab) { mutableStateOf(initialTab) }
    var page by rememberSaveable(state.profileId) { mutableStateOf<String?>(null) }
    fun navigate(id:String) { model.releaseAll();selected=id;page=null }
    BackHandler(selected!="drive" || page!=null) { if(page!=null) {model.releaseAll();page=null} else navigate("drive") }
    Column(Modifier.fillMaxSize(),verticalArrangement=Arrangement.spacedBy(8.dp)) {
        Row(Modifier.fillMaxWidth(),verticalAlignment=Alignment.CenterVertically,horizontalArrangement=Arrangement.SpaceBetween) {
            Column(Modifier.weight(1f)) {
                Row { Text("SIM",fontSize=24.sp,fontWeight=FontWeight.Black);Text("DECK",fontSize=24.sp,fontWeight=FontWeight.Black,color=d.accent) }
                Text(profileShortName(state.profileId)+" · "+d.tag,fontSize=10.sp,color=d.muted,maxLines=1)
            }
            TextButton(onClick=connection) {Text(if(state.connected) "● Связь" else "○ Подключить",fontSize=12.sp)}
        }
        HorizontalDivider(color=d.line)
        // Only the content scrolls. All four navigation destinations remain reachable.
        key(state.profileId,selected,page) {
            if(selected=="map" && isScsTruck(state.profileId)) {
                Column(Modifier.weight(1f).fillMaxWidth(),verticalArrangement=Arrangement.spacedBy(8.dp)) {
                    PhoneStatus(state)
                    BoxWithConstraints(Modifier.weight(1f).fillMaxWidth()) { TruckNavigator(state,model,availableHeight=maxHeight) }
                    PhoneGrid(state,model,listOf("etsMap" to "Карта в игре"),height=48)
                }
            } else {
            Column(Modifier.weight(1f).fillMaxWidth().verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(12.dp)) {
                PhoneStatus(state)
                if(state.profileId=="fs25" && selected!="drive") Row(horizontalArrangement=Arrangement.spacedBy(9.dp)) {
                    for((id,label) in listOf("fs25Back" to "Назад в игре","fs25Pause" to "Пауза времени")) state.controls.firstOrNull {it.id==id}?.let {a->Control(label,a.key,a.id,false,state,model,Modifier.weight(1f),heightDp=64,tile=true)}
                }
                when(selected) {
                    "drive" -> {PhoneReadout(state);PhoneVehicle(state);PhoneGrid(state,model,phoneShortcuts(state.profileId),height=86)
                        if(state.profileId.startsWith("f1-")) OutlinedButton(onClick={navigate("pit")},modifier=Modifier.fillMaxWidth().heightIn(min=52.dp)) {Text("Выбор шин и настройка пит-стопа")}
                        if(isScsTruck(state.profileId)) PhoneGrid(state,model,listOf("etsCruiseDown" to "Круиз −","etsCruiseUp" to "Круиз +")) }
                    "condition" -> when(state.profileId) {
                        "f1-24","f1-25" -> PhoneF1Condition(state)
                        "acc" -> {PhoneAccCondition(state);PhoneGrid(state,model,listOf("accTcDown" to "TC −","accTcUp" to "TC +","accAbsDown" to "ABS −","accAbsUp" to "ABS +"))}
                        else -> {PhoneVehicle(state,detail=true);PhoneWear(state)}
                    }
                    "map" -> if(isScsTruck(state.profileId)) {TruckNavigator(state,model);PhoneGrid(state,model,listOf("etsMap" to "Карта в игре"))} else if(state.profileId=="acc") AccRaceScreen(state) else PhoneF1Map(state)
                    "fields" -> Fs25Fields(state)
                    "prices" -> Fs25PricesPanel(state)
                    "pit" -> when(state.profileId) {
                        "f1-24","f1-25" -> F1Panel("mfdPit",state,model)
                        "acc" -> {PhoneGrid(state,model,listOf("accRequestPit" to "Открыть пит-стоп","accPitLimiter" to "Лимитер"));PhonePage(state,model,"MFD")}
                        else -> {PhoneGrid(state,model,listOf("amsRequestPit" to "Запросить пит-стоп","amsPitLimiter" to "Лимитер"));PhonePage(state,model,"Пит и HUD")}
                    }
                    "camera" -> PhoneGrid(state,model,state.controls.filter { it.group=="Обзор" || it.id.contains("camera",true) || it.id.contains("look",true) }.map {it.id to it.label})
                    "winch" -> {DesignCard {Text("ЛЕБЁДКА",fontWeight=FontWeight.Bold);Text("Подключение и отпускание — через штатные действия игры. Выбор точки крепления остаётся в игре.",color=d.muted)};PhoneGrid(state,model,state.controls.filter {it.group=="Лебёдка"}.map {it.id to it.label})}
                    "cargo" -> PhoneGrid(state,model,state.controls.filter {it.group in setOf("Груз","Прицеп","Оборудование")}.map {it.id to it.label})
                    "more" -> if(page==null) {
                        Text("ВСЕ РАЗДЕЛЫ",fontWeight=FontWeight.Bold)
                        if(state.profileId=="acc") OutlinedButton(onClick={navigate("pit")},modifier=Modifier.fillMaxWidth()) {Text("Пит-стоп и MFD")}
                        if(state.profileId.startsWith("f1-")) {
                            OutlinedButton(onClick={model.releaseAll();selected="pit"},modifier=Modifier.fillMaxWidth()) {Text("Пит-стоп · шины и крыло")}
                            OutlinedButton(onClick={model.releaseAll();page="@engineer"},modifier=Modifier.fillMaxWidth()) {Text("Запросы инженеру")}
                        }
                        if(state.profileId=="fs25") OutlinedButton(onClick={page="@farm"},modifier=Modifier.fillMaxWidth()) {Text("Хозяйство · сохранение и план")}
                        phonePages(state.controls).forEach {name -> OutlinedButton(onClick={model.releaseAll();page=name},modifier=Modifier.fillMaxWidth().heightIn(min=52.dp)) {Text(name,modifier=Modifier.weight(1f));Text("›",color=d.accent)} }
                    } else {
                        TextButton(onClick={model.releaseAll();page=null}) {Text("‹ Все разделы")}
                        when(page) {
                            "@farm" -> Fs25Overview(state)
                            "@engineer" -> PhoneEngineer(state,model)
                            else -> if(state.profileId.startsWith("f1-")) F1Controls(state,model,initialSection=page!!,showSections=false,showRaceShortcuts=false) else PhonePage(state,model,page!!)
                        }
                    }
                }
                if(state.command.isNotBlank()) Text(state.command,color=d.muted,fontSize=12.sp)
                Spacer(Modifier.height(6.dp))
            }
            }
        }
        HorizontalDivider(color=d.line)
        Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(2.dp)) {
            tabs.forEach {tab -> TextButton(onClick={navigate(tab.id)},modifier=Modifier.weight(1f).heightIn(min=62.dp),contentPadding=PaddingValues(2.dp)) {
                Column(horizontalAlignment=Alignment.CenterHorizontally,verticalArrangement=Arrangement.spacedBy(3.dp)) {
                    Box(Modifier.size(26.dp),contentAlignment=Alignment.Center) {PhoneIcon(state.profileId,tab.icon,if(selected==tab.id) d.accent else d.muted)}
                    Text(tab.label,fontSize=11.sp,maxLines=1,color=if(selected==tab.id)d.accent else d.muted)
                }
            } }
        }
    }
}

@Composable private fun PhoneStatus(state:DeckState) {
    val d=LocalProfileDesign.current
    val text=when {state.demo->"ДЕМО · тестовые данные · ввод отключён";!state.connected->"Связь потеряна · подключаемся…";state.profileId in setOf("ams2","snowrunner")->"Управление · телеметрия пока недоступна";state.stale->"Ожидаем данные · кнопки доступны отдельно";else->"● Живые данные"}
    Text(text,color=d.muted,fontSize=11.sp,lineHeight=15.sp)
    if(!state.demo && state.connected && !controlsAvailable(state)) Text(when(state.inputAvailability){"unfocused"->"Откройте игру на ПК";"disabled"->"Разрешите ввод в Companion";else->"Проверка готовности ввода…"},color=d.accent,fontSize=12.sp)
}
internal fun phoneNumber(value:Double?,unit:String="",dec:Int=0)=value?.let {String.format(Locale.US,"%.${dec}f",it)+unit} ?: "—"
@Composable private fun PhoneReadout(state:DeckState) {
    val d=LocalProfileDesign.current;val t=state.telemetry
    DesignCard(4,10) {
        Row(Modifier.fillMaxWidth(),verticalAlignment=Alignment.CenterVertically) {
            Column(Modifier.weight(1.4f)) {Text(phoneNumber(t?.speedMps?.times(3.6)),fontSize=36.sp,lineHeight=40.sp,fontWeight=FontWeight.Black);Text("КМ/Ч",color=d.muted,fontSize=10.sp)}
            Column(Modifier.weight(.8f)) {Text(t?.let {Protocol.gear(it.gear,it.gearboxMode)} ?: "—",fontSize=36.sp,lineHeight=40.sp,fontWeight=FontWeight.Black,color=d.accent);Text("ПЕРЕДАЧА",color=d.muted,fontSize=10.sp)}
            Column(Modifier.weight(1f),horizontalAlignment=Alignment.End) {Text(phoneNumber(t?.rpm),fontSize=20.sp,fontWeight=FontWeight.Bold);Text("RPM",color=d.muted,fontSize=10.sp);Text("Топливо ${phoneNumber(t?.fuelFraction?.times(100),"%")}",fontSize=11.sp,color=d.muted)}
        }
        if(state.profileId in setOf("f1-24","f1-25","acc","ams2")) Row(horizontalArrangement=Arrangement.spacedBy(4.dp)) {
            repeat(12) {i->Box(Modifier.weight(1f).height(5.dp).background(if(t!=null && i<t.rpm/(t.maxRpm?:12000.0)*12) d.accent else d.line,RoundedCornerShape(3.dp)))}
        }
    }
}
@Composable private fun PhoneIcon(profile:String,id:String,color:Color) {if(id=="menu") ProfileActionIcon(id,color) else if(profile=="fs25") Fs25Icon(id,color) else if(isScsTruck(profile)) Ets2Icon(id,color) else ProfileActionIcon(id,color)}
@Composable internal fun PhoneGrid(state:DeckState,model:DeckModel,entries:List<Pair<String,String>>,height:Int=104) {
    val actions=entries.mapNotNull { (id,label)->state.controls.firstOrNull {it.id==id}?.let {it to label} }
    Column(verticalArrangement=Arrangement.spacedBy(9.dp)) {actions.chunked(2).forEach {row->Row(horizontalArrangement=Arrangement.spacedBy(9.dp)) {
        row.forEach { (a,label)->Control(label,when(a.gesture){"hold"->"Удерживать";"tapThenHold"->"Нажать, затем держать";else->a.key},a.id,a.gesture=="hold",state,model,Modifier.weight(1f),heightDp=height,tile=true) }
        if(row.size==1) Spacer(Modifier.weight(1f))
    } } }
}
@Composable private fun PhonePage(state:DeckState,model:DeckModel,page:String) {
    val actions=phonePageActions(state.controls,page)
    actions.groupBy {it.group}.forEach { (group,items)->if(group.isNotBlank()) Text(group,fontWeight=FontWeight.Bold);PhoneGrid(state,model,items.map {it.id to it.label}) }
}
@Composable private fun PhoneVehicle(state:DeckState,detail:Boolean=false) {
    val d=LocalProfileDesign.current;val v=(state.telemetry?.vehicle?:state.lastVehicle)?.takeIf {it.controlled}
    when(state.profileId) {
        "f1-24","f1-25" -> DesignCard(4,8) {
            val player=state.telemetry?.f1?.race?.drivers?.firstOrNull {it.player}
            Text("ПОЗИЦИЯ ${player?.position ?: "—"} · КРУГ ${player?.lap ?: "—"}",fontSize=11.sp,color=d.muted)
            F1CircuitMap(state.telemetry?.f1?.race,state.stale,compact=true,heightDp=75,showCaptions=false)
        }
        "acc","ams2" -> DesignCard(5,10) {Text(if(state.profileId=="acc") "GT · СОСТОЯНИЕ МАШИНЫ" else "GT · ПУЛЬТ УПРАВЛЕНИЯ",fontSize=11.sp,color=d.muted);RoadDrawing("gt",heightOverride=160.dp)}
        "fs25" -> DesignCard(6,12) {
            Text(v?.name?:"Ждём технику",fontSize=20.sp,fontWeight=FontWeight.Bold,color=d.accent)
            if(v!=null) {
                val linked=v.attachments.firstOrNull {it.parentId==v.id && (it.mount!="unknown" || it.kind=="header")}
                key(v.id,linked?.id) {FarmDrawing(v.kind,linked,heightOverride=145.dp)}
                Text(linked?.let {it.name+" · "+when(it.lowered){true->"Опущено";false->"Поднято";null->"Нет состояния"}} ?: "Подключённых орудий нет",fontSize=12.sp,color=d.muted)
                v.attachments.filter {it.id!=linked?.id}.forEach {Text(it.name,fontSize=12.sp,color=d.muted);FarmDrawing(it.kind,null,small=true)}
            } else Text("Состояние поступит из мода FS25",color=d.muted,fontSize=12.sp)
        }
        "snowrunner" -> DesignCard(4,10) {
            var wheels by rememberSaveable {mutableIntStateOf(4)}
            Text("СХЕМА ТЕХНИКИ · РУЧНОЙ ВЫБОР",fontSize=10.sp,color=d.muted)
            Row(horizontalArrangement=Arrangement.spacedBy(5.dp)) {listOf(4,6,8,10).forEach {n->FilterChip(selected=wheels==n,onClick={wheels=n},label={Text("$n",fontSize=12.sp)})}}
            RoadDrawing(if(wheels==4) "suv" else "truck",List(wheels){i->VehicleWheel(if(i%2==0)-1.0 else 1.0,(i/2).toDouble(),null)},heightOverride=155.dp)
        }
        else -> DesignCard(5,10) {
            Text(v?.name?:"Ждём сведения о машине",fontSize=17.sp,fontWeight=FontWeight.Bold,color=d.accent)
            if(v!=null) RoadDrawing(v.kind,v.wheels,v.attachments.firstOrNull {it.kind=="trailer"}.takeIf {isScsTruck(state.profileId)},heightOverride=if(detail) 300.dp else 185.dp,wear=if(detail)v.wear else emptyMap())
            else Text("Для определения класса нужны данные игрового мода / плагина",fontSize=12.sp,color=d.muted)
        }
    }
}
@Composable private fun PhoneWear(state:DeckState) {
    val v=state.telemetry?.vehicle?:state.lastVehicle;val d=LocalProfileDesign.current
    DesignCard {Text("СОСТОЯНИЕ УЗЛОВ",fontWeight=FontWeight.Bold)
        listOf("engine" to "Двигатель","transmission" to "Трансмиссия","cabin" to "Кузов / кабина","chassis" to "Шасси","wheels" to "Колёса").forEach { (key,label)->Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) {Text(label,fontSize=14.sp);Text(phoneNumber(v?.wear?.get(key)?.times(100),"%"),color=damageColor(v?.wear?.get(key)?.times(100)),fontWeight=FontWeight.Bold)} }
        Text("Серый цвет и «—» означают, что источник не передал состояние узла.",fontSize=11.sp,color=d.muted)
    }
}
@Composable private fun PhoneF1Map(state:DeckState) {
    val d=LocalProfileDesign.current;val race=state.telemetry?.f1?.race
    DesignCard {F1CircuitMap(race,state.stale,heightDp=250)}
    Text("ПОРЯДОК ПИЛОТОВ",fontWeight=FontWeight.Bold)
    race?.drivers.orEmpty().sortedBy {it.position}.forEach {driver->Row(Modifier.fillMaxWidth().background(d.panel,RoundedCornerShape(8.dp)).padding(12.dp),horizontalArrangement=Arrangement.spacedBy(12.dp)) {Text(driver.position.toString(),fontWeight=FontWeight.Bold);Text((if(driver.player) "ВЫ · " else "")+driver.name,color=teamColor(driver.team),modifier=Modifier.weight(1f));Text("Кр. ${driver.lap}",fontSize=12.sp,color=d.muted)} }
    if(race==null) Text("Ждём пакет участников игры",color=d.muted)
}
@Composable private fun PhoneEngineer(state:DeckState,model:DeckModel) {
    val type=state.telemetry?.f1?.race?.sessionType;val requests=engineerRequests(type)
    val ready=controlsAvailable(state)&&!state.stale && state.telemetry?.f1?.race?.let {it.fresh&&it.drivers.any {d->d.player&&d.onTrack}}==true
    Text("ЗАПРОСЫ ИНЖЕНЕРУ",fontWeight=FontWeight.Bold)
    Text("Запускайте с закрытым радиоменю. Ответ звучит в игре.",fontSize=13.sp,color=LocalProfileDesign.current.muted)
    requests.forEachIndexed {index,label->OutlinedButton(onClick={model.engineerRequest(index)},enabled=ready,modifier=Modifier.fillMaxWidth().heightIn(min=52.dp)) {Text(label)} }
}
@Composable private fun PhoneAccCondition(state:DeckState) {
    val data=state.telemetry?.acc;val d=LocalProfileDesign.current
    Text("ШИНЫ И ТОРМОЗА",fontWeight=FontWeight.Bold)
    listOf(0,1,2,3).chunked(2).forEach {row->Row(horizontalArrangement=Arrangement.spacedBy(10.dp)) {row.forEach {i->Column(Modifier.weight(1f)) {DesignCard(8,12) {val w=data?.wheels?.getOrNull(i);Text(listOf("ПЕРЕДНЯЯ ЛЕВАЯ","ПЕРЕДНЯЯ ПРАВАЯ","ЗАДНЯЯ ЛЕВАЯ","ЗАДНЯЯ ПРАВАЯ")[i],fontSize=11.sp,color=d.muted);Text(phoneNumber(w?.coreTemperature," °C"),fontSize=28.sp,color=d.accent,fontWeight=FontWeight.Bold);Text(phoneNumber(w?.pressure," PSI",1),fontSize=19.sp);Text("Тормоз ${phoneNumber(w?.brakeTemperature," °C")}",fontSize=12.sp,color=d.muted)}}} } }
    DesignCard {Text("Баланс тормозов ${phoneNumber(data?.brakeBias,"%")}");Text("Двигатель ${phoneNumber(data?.waterTemperature," °C")}",color=d.muted)}
}
@Composable private fun PhoneF1Condition(state:DeckState) {
    val f=state.telemetry?.f1;val d=LocalProfileDesign.current;val values=f?.values.orEmpty()
    Text("СОСТОЯНИЕ БОЛИДА",fontWeight=FontWeight.Bold)
    val compound=when(values["compound"]?.toInt()){16,20->"SOFT";17,21->"MEDIUM";18,22->"HARD";7->"INTERMEDIATE";8,15->"WET";else->"Состав неизвестен"}
    Text("$compound · возраст ${phoneNumber(values["tyreAge"]," круг.")}",fontSize=11.sp,color=d.muted)
    Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(6.dp),verticalAlignment=Alignment.CenterVertically) {
        Column(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(10.dp)) {PhoneTyre(f?.wheels?.getOrNull(2),"ПЕР. ЛЕВАЯ");PhoneTyre(f?.wheels?.getOrNull(0),"ЗАД. ЛЕВАЯ")}
        Column(Modifier.weight(1.2f)) {RoadDrawing("formula",values=values,heightOverride=295.dp)}
        Column(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(10.dp)) {PhoneTyre(f?.wheels?.getOrNull(3),"ПЕР. ПРАВАЯ");PhoneTyre(f?.wheels?.getOrNull(1),"ЗАД. ПРАВАЯ")}
    }
    val metrics=listOf("frontLeftWingDamage" to "Левое крыло","frontRightWingDamage" to "Правое крыло","rearWingDamage" to "Заднее крыло","drsFault" to "DRS","floorDamage" to "Днище","sidepodDamage" to "Боковины")
    metrics.chunked(2).forEach {row->Row(horizontalArrangement=Arrangement.spacedBy(9.dp)) {row.forEach { (key,label)->Column(Modifier.weight(1f)) {DesignCard(5,12) {Text(label,fontSize=12.sp,color=d.muted);Text(if(key=="drsFault") f1FaultLabel(values[key]) else phoneNumber(values[key],"%"),color=damageColor(f1ZoneDamage(key,values)),fontSize=if(key=="drsFault") 13.sp else 22.sp,fontWeight=FontWeight.Bold)}}} } }
    Text("Зелёный ≤5% · жёлтый 6–30% · красный >30%",color=d.muted,fontSize=11.sp)
}
@Composable private fun PhoneTyre(w:WheelData?,label:String) {
    val d=LocalProfileDesign.current
    val wearColor=when {w?.wear==null->d.muted;w.wear>50->Color(0xFFFF575D);w.wear>25->Color(0xFFFFC449);else->Color(0xFF7DDA71)}
    val temperatureColor=when {w?.surface==null->d.muted;w.surface>105->Color(0xFFFF575D);w.surface>90->Color(0xFFFF9F32);w.surface>=80->Color(0xFFFFD945);else->Color(0xFF61B8FF)}
    Column(Modifier.fillMaxWidth().background(d.panel,RoundedCornerShape(8.dp)).border(1.dp,d.line,RoundedCornerShape(8.dp)).padding(6.dp),horizontalAlignment=Alignment.CenterHorizontally,verticalArrangement=Arrangement.spacedBy(5.dp)) {
        Text(label,fontSize=9.sp,color=d.muted,maxLines=1)
        Box(Modifier.size(54.dp).border(3.dp,wearColor,RoundedCornerShape(27.dp)),contentAlignment=Alignment.Center) {Text(phoneNumber(w?.wear,"%"),fontSize=18.sp,fontWeight=FontWeight.Bold)}
        Text(phoneNumber(w?.surface," °C"),fontSize=15.sp,color=temperatureColor,fontWeight=FontWeight.Bold)
        Text(phoneNumber(w?.pressure," PSI",1),fontSize=11.sp)
        Text("Внутри ${phoneNumber(w?.inner,"°")}",fontSize=9.sp,color=d.muted)
        Text("Тормоз ${phoneNumber(w?.brake,"°")}",fontSize=9.sp,color=d.muted)
    }
}
