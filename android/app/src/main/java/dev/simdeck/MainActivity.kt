package dev.simdeck

import android.os.Bundle
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.horizontalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay

private val Background = Color(0xFF0B1115)
private val Panel = Color(0xFF182026)
private val Muted = Color(0xFF8D9FA8)
private val Amber = Color(0xFFF2B84B)
private val White = Color(0xFFE8ECEE)

class MainActivity : ComponentActivity() {
    private val model: DeckModel by viewModels()
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        setContent {
            val state by model.state.collectAsState()
            val design = remember(state.profileId) { profileDesign(state.profileId) }
            CompositionLocalProvider(LocalProfileDesign provides design) {
            MaterialTheme(colorScheme = darkColorScheme(primary = design.accent, onPrimary = design.background, primaryContainer = design.panelAlt, onPrimaryContainer = design.accent, secondary = design.accent, onSecondary = design.background, secondaryContainer = design.panelAlt, onSecondaryContainer = White, background = design.background, surface = design.panel, surfaceVariant = design.panelAlt, onBackground = White, onSurface = White, outline = design.line)) {
                var connectionTab by rememberSaveable { mutableStateOf(false) }
                Surface(Modifier.fillMaxSize(), color = design.background) {
                    Column(Modifier.fillMaxSize().statusBarsPadding().navigationBarsPadding().imePadding().padding(horizontal = 15.dp, vertical = 10.dp)) {
                        if(connectionTab || !(state.connected || state.controls.isNotEmpty())) Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) {
                            Column(Modifier.weight(1f)) {
                                Row { Text("SIM", fontSize = 24.sp, fontWeight = FontWeight.Black, letterSpacing = 1.sp); Text("DECK", fontSize = 24.sp, fontWeight = FontWeight.Black, letterSpacing = 1.sp,color=design.accent)
                                    Text("  ПРОФИЛЬ: ${profileShortName(state.profileId)}", fontSize = 14.sp, fontWeight = FontWeight.Black, color = design.accent)
                                }
                                Text(design.tag, color = design.muted, fontSize = 10.sp, letterSpacing = 1.sp)
                            }
                            TextButton(onClick = { model.releaseAll(); connectionTab = !connectionTab }) { Text(if (connectionTab) "ПАНЕЛЬ" else if (state.profileId in setOf("f1-24", "f1-25")) "СВЯЗЬ" else "ПОДКЛЮЧЕНИЕ", fontSize = 11.sp) }
                        }
                        if (connectionTab && state.profileId in setOf("f1-24", "f1-25", "acc")) Box(Modifier.fillMaxWidth().padding(top = 8.dp).height(3.dp).background(design.accent))
                        Spacer(Modifier.height(12.dp))
                        // Keep the dashboard composed during reconnects so pages and scroll survive.
                        if ((state.connected || state.controls.isNotEmpty()) && !connectionTab) Dashboard(state, model) { model.releaseAll();connectionTab=true }
                        else Connection(state, model) { connectionTab = false }
                    }
                }
            }
            }
        }
    }
    override fun onStart() { super.onStart(); model.start() }
    override fun onStop() { model.pause(); super.onStop() }
}

@Composable
private fun Connection(state: DeckState, model: DeckModel, showDash: () -> Unit) {
    var code by rememberSaveable { mutableStateOf("") }
    var verified by rememberSaveable(state.selected?.fingerprint) { mutableStateOf(false) }
    var advanced by rememberSaveable { mutableStateOf(false) }
    var address by rememberSaveable { mutableStateOf("") }
    var fingerprint by rememberSaveable { mutableStateOf("") }
    Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(16.dp)) {
        Text("Ваш кокпит\nначинается здесь.", fontSize = 32.sp, fontWeight = FontWeight.Bold, lineHeight = 37.sp)
        Text("Запустите SimDeck Companion на ПК. Подключите оба устройства к одной сети Wi-Fi.", color = Muted)
        Text(state.status, color = Amber)
        if(state.lastDisconnect.isNotEmpty()) Text("Восстановлений связи: ${state.reconnectCount} · последняя причина: ${state.lastDisconnect}",fontSize=13.sp,color=Muted)
        if (state.connected) Button(onClick = showDash) { Text("Открыть панель") }
        Card(colors = CardDefaults.cardColors(containerColor = Panel)) {
            Column(Modifier.fillMaxWidth().padding(18.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text("КОМПЬЮТЕРЫ В СЕТИ", color = Muted, fontSize = 11.sp, letterSpacing = 1.sp)
                if (state.computers.isEmpty()) { Text("Поиск Companion…"); Text("Если ПК не появился, проверьте частный профиль сети и разрешение SimDeck в брандмауэре Windows.", fontSize = 12.sp, color = Muted) }
                state.computers.forEach { pc -> OutlinedButton(onClick = { model.select(pc); code = "" }, modifier = Modifier.fillMaxWidth()) { Text(pc.name) } }
            }
        }
        state.selected?.let { pc ->
            Text(pc.name, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            Text("Сравните все символы с отпечатком в Companion. Затем нажмите на ПК «Подключить планшет».", color = Muted)
            Text(pc.fingerprint.chunked(16).joinToString("\n"), fontFamily = FontFamily.Monospace, color = Amber, fontSize = 13.sp)
            Row(verticalAlignment = Alignment.CenterVertically) { Checkbox(checked = verified, onCheckedChange = { verified = it }); Text("Отпечатки совпадают") }
            OutlinedTextField(value = code, onValueChange = { code = it.filter(Char::isDigit).take(6) }, label = { Text("Код из Companion") }, keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number), singleLine = true, modifier = Modifier.fillMaxWidth())
            Button(onClick = { model.pair(code); showDash() }, enabled = verified && code.length == 6 && !state.busy, modifier = Modifier.fillMaxWidth().height(52.dp)) { Text(if (state.busy) "Подключение…" else "Связать устройства") }
            TextButton(onClick = { model.connect(); showDash() }) { Text("Подключиться к сохранённому ПК") }
        }
        TextButton(onClick = { advanced = !advanced }) { Text("Резервное подключение", color = Muted) }
        if(state.selected!=null) {
            Row(horizontalArrangement=Arrangement.spacedBy(8.dp)) {
                OutlinedButton(onClick={model.savedTransport(true);showDash()}) { Text("USB · сохранённый ПК") }
                OutlinedButton(onClick={model.savedTransport(false);showDash()}) { Text("Wi-Fi · сохранённый ПК") }
            }
            Text("USB требует кабель с передачей данных и запущенный на ПК adb reverse. Используется прежнее доверенное подключение.",color=Muted,fontSize=12.sp)
        }
        if (advanced) {
            Text("Для проверки через USB: adb reverse tcp:9443 tcp:9443, адрес 127.0.0.1:9443. Вставьте отпечаток из окна Companion.", color = Muted, fontSize = 12.sp)
            OutlinedTextField(value = address, onValueChange = { address = it }, label = { Text("Адрес:порт") }, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(value = fingerprint, onValueChange = { fingerprint = it.filterNot(Char::isWhitespace) }, label = { Text("Полный SHA-256 отпечаток") }, modifier = Modifier.fillMaxWidth())
            OutlinedButton(onClick = {
                val host = address.substringBefore(':').trim()
                val port = address.substringAfter(':', "9443").toIntOrNull()
                if (host.isNotEmpty() && port != null && port in 1024..65535 && fingerprint.matches(Regex("[0-9a-fA-F]{64}"))) model.select(Computer("Companion", host, port, fingerprint.lowercase()))
            }) { Text("Выбрать") }
        }
        Text("Ранняя сборка 0.9.14 · 8 игровых профилей · ETS2 SCS Telemetry", color = Muted, fontSize = 11.sp)
    }
}

@Composable
private fun Dashboard(state: DeckState, model: DeckModel,connection:()->Unit) {
    if (state.profileId in setOf("f1-24", "f1-25")) { F1Dashboard(state, model,connection); return }
    ProfileDashboard(state, model,connection)
}

@Composable
internal fun Fs25Overview(state: DeckState) {
    val context=androidx.compose.ui.platform.LocalContext.current
    val labels=remember { Fs25FieldLabels(org.json.JSONObject(context.assets.open("fs25-field-ui.json").bufferedReader().use { it.readText() })) }
    val data = state.telemetry?.fs25
    val design = LocalProfileDesign.current
    var allFields by rememberSaveable { mutableStateOf(false) }
    Card(colors = CardDefaults.cardColors(containerColor = design.panel), shape = RoundedCornerShape(design.radius.dp), modifier = Modifier.border(1.dp, design.line, RoundedCornerShape(design.radius.dp))) {
        Column(Modifier.fillMaxWidth().padding(18.dp), verticalArrangement = Arrangement.spacedBy(9.dp)) {
            Text("FARM OPERATIONS · FS25", fontSize = 13.sp, fontWeight = FontWeight.Bold, color = design.accent)
            if (data == null) {
                Text("Сохранение FS25 пока не найдено. Сохраните игру и проверьте путь в Companion.", color = design.muted)
                return@Column
            }
            Text(data.saveName.ifBlank { "Без названия" }, fontSize = 20.sp, fontWeight = FontWeight.Bold)
            Text(listOf(data.mapName, data.period).filter(String::isNotBlank).joinToString(" · "), color = design.muted)
            Text("Сохранено: ${data.savedAt}" + if (data.stale) " · ДАННЫЕ УСТАРЕЛИ" else "", color = if (data.stale) Amber else design.accent, fontSize = 12.sp)
            data.money?.let { money ->
                Text("Деньги: ${"%,.0f".format(money)} €" + (data.loan?.let { " · кредит ${"%,.0f".format(it)} €" } ?: ""), fontSize = 15.sp)
            }
            val states = state.telemetry?.actionStates.orEmpty()
            if (!state.stale && listOf("fs25Lower", "fs25TurnOn", "fs25Motor").any { it in states }) {
                HorizontalDivider(color = design.line)
                Text("ТЕКУЩЕЕ СОСТОЯНИЕ ТЕХНИКИ", color = design.accent, fontSize = 11.sp, fontWeight = FontWeight.Bold)
                listOf(
                    listOf("fs25Lower", "Орудие", "Опущено", "Поднято"),
                    listOf("fs25TurnOn", "Рабочий режим", "Включён", "Выключен"),
                    listOf("fs25Motor", "Двигатель", "Работает", "Остановлен")
                ).forEach { (id, label, active, inactive) ->
                    Row(Modifier.fillMaxWidth().background(design.panelAlt, RoundedCornerShape(6.dp)).padding(10.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                        Text(label, fontSize = 12.sp)
                        Text(states[id]?.let { if (it) active else inactive } ?: "—", color = design.accent, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                    }
                }
            }
            HorizontalDivider(color = design.line)
            Text("ПОЛЯ · ${data.fields.size}", fontWeight = FontWeight.Bold)
            val fields=filteredFs25Fields(data.fields,"",null,labels)
            (if (allFields) fields else fields.take(6)).forEach { field ->
                Text("№${field.id} · ${labels.crop(field.crop)} · ${field.ground.ifBlank { "состояние неизвестно" }} · сорняки ${fs25FieldPercent(field.weeds,9)?.let { "$it%" } ?: "—"} · известь ${fs25FieldPercent(field.lime,3)?.let { "$it%" } ?: "—"} · удобрение ${fs25FieldPercent(field.fertilizer,3)?.let { "$it%" } ?: "—"}", fontSize = 12.sp)
            }
            if (data.fields.size > 6) TextButton(onClick = { allFields = !allFields }) {
                Text(if (allFields) "Свернуть поля" else "Показать все поля")
            }
            if (data.alerts.isNotEmpty()) {
                HorizontalDivider(color = design.line)
                Text("СОВЕТНИК · ${data.alerts.size} уведомлений", fontWeight = FontWeight.Bold)
                data.alerts.take(6).forEach { alert ->
                    Text(alert.message, color = if (alert.severity >= 2) Color(0xFFFF7770) else Amber, fontSize = 12.sp)
                }
            }
            data.planName?.takeUnless { it.isBlank() || it.equals("null", ignoreCase = true) }?.let { name ->
                HorizontalDivider(color = design.line)
                Text("ПЛАН: $name", fontWeight = FontWeight.Bold)
                data.tasks.take(6).forEach { task ->
                    val status = when (task.status) { 1 -> "✓"; 2 -> "Вне сезона"; 3 -> "Нет поля"; else -> "К исполнению" }
                    Text("$status · ${task.title}", fontSize = 12.sp)
                }
            }
        }
    }
}

@Composable internal fun Controls(state: DeckState, model: DeckModel, selectedPageOverride: String? = null, showPages: Boolean = true) {
    val design = LocalProfileDesign.current
    if (state.profileId in setOf("f1-24","f1-25") && state.controls.isNotEmpty()) { F1Controls(state, model); return }
    var selectedPage by rememberSaveable(state.profileId) { mutableStateOf("Основное") }
    val actions = state.controls.ifEmpty { listOf(
        DeckAction("lights", "Основное", "СВЕТ", "Переключить фары", "", "press"),
        DeckAction("horn", "Основное", "СИГНАЛ", "Удерживайте", "", "hold"),
        DeckAction("ignition", "Основное", "ЗАЖИГАНИЕ", "Нажать, затем держать", "", "tapThenHold"),
        DeckAction("reset", "Основное", "СБРОС", "Восстановить машину", "", "press")
    ) }
    val pages = actions.map { it.page }.distinct()
    val page = selectedPageOverride?.takeIf { it in pages } ?: selectedPage.takeIf { it in pages } ?: pages.first()
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(state.profileName.uppercase() + " · ПРОФИЛЬ УПРАВЛЕНИЯ", fontSize = 11.sp, letterSpacing = 1.sp, color = design.accent, fontWeight = FontWeight.Bold)
        if(showPages) Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            pages.forEach { name -> FilterChip(selected = page == name, onClick = { model.releaseAll(); selectedPage = name }, label = { Text(name, fontSize = 11.sp) }) }
        }
        actions.filter { it.page == page }.chunked(2).forEach { row ->
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                row.forEach { action -> key(state.profileId, action.id, action.gesture) {
                    val description = if (action.id == "ignition") {
                        if (state.ignitionReady) "Теперь удерживайте: запуск" else "Сначала коротко: зажигание"
                    } else action.description
                    Control(action.label, description, action.id, action.gesture == "hold", state, model, Modifier.weight(1f))
                } }
                if (row.size == 1) Spacer(Modifier.weight(1f))
            }
        }
        if (state.demo) Text("Демонстрационные данные. Команды отключены.", color = Amber, fontSize = 12.sp)
        else Text(controlsHint(state.profileId, page), color = Muted, fontSize = 11.sp)
        if (state.profileId == "beamng-default" && !state.stale && !state.demo && state.telemetry?.headlights == null)
            Text("Игра присылает старый поток без состояния кнопок. Перезапустите BeamNG после обновления мода.", color = Amber, fontSize = 11.sp)
        if (state.profileId == "fs25" && (state.stale || state.telemetry?.actionStates?.keys?.none { it.startsWith("fs25") } != false))
            Text("Текущее положение орудия появится после включения мода FS25_SimDeckStatus в сохранении.", color = Muted, fontSize = 11.sp)
        if (state.command.isNotBlank()) Text(state.command, color = Amber, fontSize = 12.sp)
    }
}

@Composable internal fun Control(label: String, subtitle: String, action: String, hold: Boolean, state: DeckState, model: DeckModel, modifier: Modifier, heightDp: Int = 98, tile: Boolean = false, primary: Boolean = false) {
    val design = LocalProfileDesign.current
    var down by remember { mutableStateOf(false) }
    var startingIgnition by remember { mutableStateOf(false) }
    val enabled = controlsAvailable(state)
    val feedback = Protocol.feedback(action, state.telemetry, state.demo || !state.connected)
    val active = feedback.active == true
    val ets2 = state.profileId == "ets2"
    val fs25 = state.profileId == "fs25"
    val accent = when {
        primary -> design.background
        ets2 && active && action == "etsHighBeam" || feedback.headlights == 2 -> Color(0xFF62A6FF)
        ets2 && active && action == "etsParkingBrake" -> Color(0xFFFF7770)
        ets2 && active && action == "etsCruise" -> Color(0xFF69D9CE)
        feedback.headlights == 1 -> Color(0xFF5CDD8E)
        active -> design.accent
        else -> White
    }
    val fill = when {
        primary -> design.accent
        feedback.headlights == 1 -> Color(0xFF145C35)
        feedback.headlights == 2 || ets2 && active && action == "etsHighBeam" -> Color(0xFF123F8C)
        ets2 && active && action == "etsParkingBrake" -> Color(0xFF4B2528)
        ets2 && active && action == "etsCruise" -> Color(0xFF164B4B)
        active -> when (state.profileId) { "beamng-default" -> Color(0xFF583B24); "acc" -> Color(0xFF4E3D25); "ams2" -> Color(0xFF3A5E2F); "fs25" -> Color(0xFF355B3A); "snowrunner" -> Color(0xFF5B4D34); else -> design.panelAlt }
        down -> design.panelAlt
        else -> design.panel
    }
    val displaySubtitle = (if(state.stale && feedback.description!=null) "Последнее: " else "") + (feedback.description ?: subtitle)
    Box(modifier.heightIn(min = heightDp.dp).background(fill, RoundedCornerShape(design.radius.dp))
        .border(if (active) 2.dp else 1.dp, if (active || down) accent else design.line, RoundedCornerShape(design.radius.dp))
        .semantics { contentDescription = "$label, $displaySubtitle"; stateDescription = when(feedback.active) { true -> "Включено"; false -> "Выключено"; null -> "" } }
        .pointerInput(enabled, action, hold) {
            if (enabled) detectTapGestures(onPress = {
                down = true
                var id: String? = null
                try {
                    if (action == "ignition") {
                        startingIgnition = model.state.value.ignitionReady
                        ignitionGesture(
                            ready = startingIgnition,
                            awaitRelease = { tryAwaitRelease() },
                            tap = { model.press(action, false) },
                            startHold = { model.press(action, true) },
                            endHold = { model.release(it) },
                            blocked = { model.ignitionNeedsTap() }
                        )
                    } else {
                        if (hold) id = model.press(action, true)
                        val released = tryAwaitRelease()
                        if (released && !hold) model.press(action, false)
                    }
                } finally { model.release(id); down = false; startingIgnition = false }
            })
        }, contentAlignment = Alignment.Center) {
        if(tile && heightDp in 48..80) Row(Modifier.fillMaxWidth().padding(8.dp),verticalAlignment=Alignment.CenterVertically,horizontalArrangement=Arrangement.spacedBy(10.dp)) {
            if(ets2) Ets2Icon(action,if(enabled) accent else Muted) else if(fs25) Fs25Icon(action,if(enabled) accent else Muted) else ProfileActionIcon(action,if(enabled) accent else Muted)
            Column(Modifier.weight(1f)) { Text(label,fontSize=13.sp,lineHeight=16.sp,maxLines=2,fontWeight=FontWeight.Bold,color=if(enabled) accent else Muted);Text(displaySubtitle,fontSize=10.sp,lineHeight=12.sp,maxLines=1,color=design.muted) }
        } else Column(Modifier.fillMaxWidth().padding(10.dp), horizontalAlignment = if (tile) Alignment.Start else Alignment.CenterHorizontally) {
            if (heightDp >= 85) {
                if (ets2) Ets2Icon(action, if (enabled) accent else Muted)
                if (fs25) Fs25Icon(action, if (enabled) accent else Muted)
                if (!ets2 && !fs25) ProfileActionIcon(action, if (enabled) { if (primary || feedback.headlights != null) accent else design.accent } else Muted)
            }
            if (active && !tile) Text(when(action) { "fs25Lower" -> "● ОПУЩЕНО"; "fs25TurnOn" -> "● РАБОТАЕТ"; "fs25Motor" -> "● ЗАПУЩЕН"; "fs25Pause" -> "● ВРЕМЯ ОСТАНОВЛЕНО"; else -> "● ВКЛ" }, fontSize = 9.sp, fontWeight = FontWeight.Bold, color = accent)
            Text(label, fontSize = if (state.profileId in setOf("f1-24","f1-25")) { if (label in listOf("↑", "←", "↓", "→")) 30.sp else 16.sp } else if (tile) 12.sp else 14.sp, maxLines = 2, textAlign = if (tile) TextAlign.Start else TextAlign.Center, fontWeight = FontWeight.Bold, color = if (enabled) accent else Muted)
            if (heightDp >= 70) Text(if (startingIgnition && down) "Запуск · держите кнопку" else displaySubtitle, fontSize = if (tile) 10.sp else if (state.profileId in setOf("f1-24","f1-25")) 13.sp else 11.sp, maxLines = if (tile) 2 else 3, textAlign = if (tile) TextAlign.Start else TextAlign.Center, color = if(primary) design.background else design.muted, modifier = Modifier.padding(top = 4.dp))
        }
    }
}

@Composable private fun StatusFootnote(state: DeckState) { Text(state.status, color = LocalProfileDesign.current.muted, fontSize = 10.sp, modifier = Modifier.padding(vertical = 14.dp)) }

