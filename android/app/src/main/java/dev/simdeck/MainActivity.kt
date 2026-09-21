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
            MaterialTheme(colorScheme = darkColorScheme(primary = Amber, onPrimary = Background, background = Background, surface = Panel, onBackground = White, onSurface = White)) {
                val state by model.state.collectAsState()
                var connectionTab by rememberSaveable { mutableStateOf(false) }
                Surface(Modifier.fillMaxSize(), color = Background) {
                    Column(Modifier.fillMaxSize().statusBarsPadding().navigationBarsPadding().imePadding().padding(horizontal = 22.dp, vertical = 12.dp)) {
                        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) {
                            Column { Text("SIMDECK", fontSize = 24.sp, fontWeight = FontWeight.Black, letterSpacing = 2.sp); Text("YOUR RIG. ONE TOUCH.", color = Muted, fontSize = 10.sp, letterSpacing = 1.sp) }
                            TextButton(onClick = { model.releaseAll(); connectionTab = !connectionTab }) { Text(if (connectionTab) "ПАНЕЛЬ" else "ПОДКЛЮЧЕНИЕ", fontSize = 11.sp) }
                        }
                        Spacer(Modifier.height(18.dp))
                        // Keep the dashboard composed during reconnects so pages and scroll survive.
                        if ((state.connected || state.controls.isNotEmpty()) && !connectionTab) Dashboard(state, model)
                        else Connection(state, model) { connectionTab = false }
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
    var code by remember { mutableStateOf("") }
    var verified by remember(state.selected?.fingerprint) { mutableStateOf(false) }
    var advanced by remember { mutableStateOf(false) }
    var address by remember { mutableStateOf("") }
    var fingerprint by remember { mutableStateOf("") }
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
        Text("Ранняя сборка 0.5.1 · BeamNG / F1 24 · редактор кнопок на ПК", color = Muted, fontSize = 11.sp)
    }
}

@Composable
private fun Dashboard(state: DeckState, model: DeckModel) {
    val data = state.telemetry.takeUnless { state.stale }
    BoxWithConstraints(Modifier.fillMaxSize()) {
        val wide = maxWidth > 700.dp
        if (wide) Row(horizontalArrangement = Arrangement.spacedBy(18.dp)) {
            Column(Modifier.weight(if (state.profileId in setOf("f1-24","f1-25")) 1f else 1.5f).verticalScroll(rememberScrollState())) { Instruments(state, data) }
            Column(Modifier.weight(if (state.profileId in setOf("f1-24","f1-25")) 1.8f else 1f).verticalScroll(rememberScrollState())) { Controls(state, model); StatusFootnote(state) }
        } else Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(18.dp)) {
            Instruments(state, data); Controls(state, model); StatusFootnote(state)
        }
    }
}

@Composable
private fun Instruments(state: DeckState, data: Telemetry?) {
    if (state.profileId in setOf("f1-24","f1-25")) { F1Instruments(state, data); return }
    Card(colors = CardDefaults.cardColors(containerColor = Panel), shape = RoundedCornerShape(20.dp)) {
        Column(Modifier.fillMaxWidth().padding(22.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                Text(if (state.demo) "ДЕМОНСТРАЦИЯ" else state.profileName.uppercase(), fontSize = 12.sp, letterSpacing = 1.sp, color = Muted)
                Text(if (state.stale) "● НЕТ ДАННЫХ" else "● LIVE", fontSize = 11.sp, color = if (state.stale) Muted else Color(0xFF9ED8B0))
            }
            Spacer(Modifier.height(26.dp))
            if (state.profileId in setOf("f1-24","f1-25")) {
                Metric("ОБОРОТЫ ДВИГАТЕЛЯ", data?.let { "%.0f".format(it.rpm) } ?: "—", "RPM")
                Spacer(Modifier.height(12.dp))
            }
            val limit = data?.maxRpm ?: 8000.0
            val filled = ((data?.rpm ?: 0.0) / limit * 12).toInt().coerceIn(0, 12)
            Canvas(Modifier.fillMaxWidth().height(16.dp)) {
                val step = size.width / 12
                for (i in 0..11) drawCircle(if(i >= filled) Color(0xFF303C43) else if (i >= 10) Color(0xFFFF7770) else Amber, radius = 5.dp.toPx(), center = Offset(step * (i + .5f), size.height / 2))
            }
            Text(if (data == null) "—" else Protocol.gear(data.gear, data.gearboxMode), fontSize = 112.sp, lineHeight = 120.sp, fontWeight = FontWeight.Black, color = White)
            Text(when(data?.gearboxMode) { "arcade" -> "АРКАДА · D / N / R"; "realistic" -> "РЕАЛИЗМ" + (data.maxGear?.let { " · $it ПЕРЕДАЧ" } ?: ""); else -> "ПЕРЕДАЧА" }, color = Muted, letterSpacing = 2.sp, fontSize = 10.sp)
            Spacer(Modifier.height(18.dp))
            Row(verticalAlignment = Alignment.Bottom) { Text(data?.let { "%.0f".format(it.speedMps * 3.6) } ?: "—", fontSize = 52.sp, fontWeight = FontWeight.Bold); Text(" км/ч", color = Muted, modifier = Modifier.padding(bottom = 10.dp)) }
            Spacer(Modifier.height(18.dp))
            HorizontalDivider(color = Color(0xFF304049))
            Row(Modifier.fillMaxWidth().padding(top = 18.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                if (state.profileId !in setOf("f1-24","f1-25")) Metric("ОБОРОТЫ", data?.let { "%.0f".format(it.rpm) } ?: "—", "RPM")
                Metric("ТОПЛИВО", data?.fuelFraction?.let { "%.0f".format(it * 100) } ?: "—", "% бака")
            }
            if (data?.maxRpm == null) Text("Шкала RPM: 8000 · настроечный предел", fontSize = 10.sp, color = Muted, modifier = Modifier.padding(top = 16.dp))
            if (state.stale && !state.demo) Text(if (state.profileId in setOf("f1-24","f1-25")) "Включите UDP → 127.0.0.1:20777 и выйдите на трассу." else "Нет телеметрии от игры. Проверьте мод SimDeck в Companion.", fontSize = 11.sp, color = Amber, modifier = Modifier.padding(top = 10.dp))
        }
    }
}

@Composable private fun F1Instruments(state: DeckState, data: Telemetry?) {
    Card(colors = CardDefaults.cardColors(containerColor = Panel), shape = RoundedCornerShape(20.dp)) {
        Column(Modifier.fillMaxWidth().padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                Text(if (state.demo) "ДЕМОНСТРАЦИЯ" else "F1 24", fontSize = 14.sp, color = Muted)
                Text(if (state.stale) "● НЕТ ДАННЫХ" else "● LIVE", fontSize = 12.sp, color = if (state.stale) Muted else Color(0xFF9ED8B0))
            }
            Metric("ОБОРОТЫ ДВИГАТЕЛЯ", data?.let { "%.0f".format(it.rpm) } ?: "—", "RPM")
            val filled = ((data?.rpm ?: 0.0) / (data?.maxRpm ?: 15000.0) * 12).toInt().coerceIn(0, 12)
            Canvas(Modifier.fillMaxWidth().height(14.dp)) {
                val step = size.width / 12
                for (i in 0..11) drawCircle(if (i >= filled) Color(0xFF303C43) else if (i >= 10) Color(0xFFFF7770) else Amber, radius = 5.dp.toPx(), center = Offset(step * (i + .5f), size.height / 2))
            }
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceAround, verticalAlignment = Alignment.CenterVertically) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(data?.let { Protocol.gear(it.gear, it.gearboxMode) } ?: "—", fontSize = 48.sp, lineHeight = 54.sp, fontWeight = FontWeight.Black)
                    Text("ПЕРЕДАЧА", color = Muted, fontSize = 11.sp)
                }
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(data?.let { "%.0f".format(it.speedMps * 3.6) } ?: "—", fontSize = 36.sp, fontWeight = FontWeight.Bold)
                    Text("км/ч", color = Muted, fontSize = 14.sp)
                }
            }
            Text("Топливо: ${data?.fuelFraction?.let { "%.0f".format(it * 100) } ?: "—"}% бака", color = Muted, fontSize = 15.sp)
            HorizontalDivider(color = Color(0xFF304049))
            Text("МИНИ-КАРТА", fontSize = 15.sp)
            F1CircuitMap(state.telemetry?.f1?.race, state.stale, compact = true)
        }
    }
}

@Composable private fun Metric(label: String, value: String, unit: String) {
    Column { Text(label, color = Muted, fontSize = 10.sp, letterSpacing = 1.sp); Row(verticalAlignment = Alignment.Bottom) { Text(value, color = Amber, fontSize = 24.sp, fontWeight = FontWeight.Bold); Text(" $unit", color = Muted, fontSize = 10.sp, modifier = Modifier.padding(bottom = 5.dp)) } }
}

@Composable private fun Controls(state: DeckState, model: DeckModel) {
    if (state.profileId in setOf("f1-24","f1-25") && state.controls.isNotEmpty()) { F1Controls(state, model); return }
    var selectedPage by rememberSaveable(state.profileId) { mutableStateOf("Основное") }
    val actions = state.controls.ifEmpty { listOf(
        DeckAction("lights", "Основное", "СВЕТ", "Переключить фары", "", "press"),
        DeckAction("horn", "Основное", "СИГНАЛ", "Удерживайте", "", "hold"),
        DeckAction("ignition", "Основное", "ЗАЖИГАНИЕ", "Нажать, затем держать", "", "tapThenHold"),
        DeckAction("reset", "Основное", "СБРОС", "Восстановить машину", "", "press")
    ) }
    val pages = actions.map { it.page }.distinct()
    val page = selectedPage.takeIf { it in pages } ?: pages.first()
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(state.profileName.uppercase() + " · ПРОФИЛЬ УПРАВЛЕНИЯ", fontSize = 10.sp, letterSpacing = 1.sp, color = Muted)
        Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
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
        else Text(if (state.profileId in setOf("f1-24","f1-25")) "DRS, ERS и лимитер: ВКЛ по телеметрии. Свои кнопки добавляются в Companion." else if (page == "Возврат") "Вернуть — удержание. Сохранить — записать текущую позицию машины." else "ВКЛ и тёмная кнопка — действие включено. Ближний — зелёный, дальний — синий.", color = Muted, fontSize = 11.sp)
        if (state.profileId == "beamng-default" && !state.stale && !state.demo && state.telemetry?.headlights == null)
            Text("Игра присылает старый поток без состояния кнопок. Перезапустите BeamNG после обновления мода.", color = Amber, fontSize = 11.sp)
        if (state.command.isNotBlank()) Text(state.command, color = Amber, fontSize = 12.sp)
    }
}

@Composable internal fun Control(label: String, subtitle: String, action: String, hold: Boolean, state: DeckState, model: DeckModel, modifier: Modifier, heightDp: Int = 98) {
    var down by remember { mutableStateOf(false) }
    var startingIgnition by remember { mutableStateOf(false) }
    val enabled = state.connected && !state.demo
    val feedback = Protocol.feedback(action, state.telemetry, state.stale || state.demo || !state.connected)
    val active = feedback.active == true
    val accent = when(feedback.headlights) { 1 -> Color(0xFF5CDD8E); 2 -> Color(0xFF62A6FF); else -> if (active) Amber else White }
    val fill = when { feedback.headlights == 1 -> Color(0xFF145C35); feedback.headlights == 2 -> Color(0xFF123F8C); down -> Color(0xFF373020); active -> Color(0xFF070D11); else -> Panel }
    val displaySubtitle = feedback.description ?: subtitle
    Box(modifier.height(heightDp.dp).background(fill, RoundedCornerShape(14.dp))
        .border(if (active) 2.dp else 1.dp, if (active) accent else if (down) Amber else Color(0xFF304049), RoundedCornerShape(14.dp))
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
        Column(Modifier.padding(horizontal = 8.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            if (active) Text("● ВКЛ", fontSize = 9.sp, fontWeight = FontWeight.Bold, color = accent)
            Text(label, fontSize = if (state.profileId in setOf("f1-24","f1-25")) { if (label in listOf("↑", "←", "↓", "→")) 30.sp else 16.sp } else 14.sp, maxLines = 2, textAlign = TextAlign.Center, fontWeight = FontWeight.Bold, color = if (enabled) accent else Muted)
            Text(if (startingIgnition && down) "Запуск · держите кнопку" else displaySubtitle, fontSize = if (state.profileId in setOf("f1-24","f1-25")) 13.sp else 11.sp, textAlign = TextAlign.Center, color = if (active) Color(0xFFBDD0D8) else Muted, modifier = Modifier.padding(top = 6.dp))
        }
    }
}

@Composable private fun StatusFootnote(state: DeckState) { Text(state.status, color = Muted, fontSize = 10.sp, modifier = Modifier.padding(vertical = 14.dp)) }

