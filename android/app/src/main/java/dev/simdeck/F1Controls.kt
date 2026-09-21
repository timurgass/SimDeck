package dev.simdeck

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

@Composable internal fun F1Controls(state: DeckState, model: DeckModel) {
    val actions = state.controls
    val sections = (actions.map { it.page } + "Трасса").distinct()
    var selectedSection by rememberSaveable { mutableStateOf("Control Scheme") }
    val section = selectedSection.takeIf { it in sections } ?: sections.first()
    val groups = (actions.filter { it.page == section }.map { it.group.ifEmpty { "Общие" } } + if(section=="Control Scheme") listOf("Инженер") else emptyList()).distinct().ifEmpty { listOf("Карта и пилоты") }
    var selectedGroup by rememberSaveable(section) { mutableStateOf(groups.first()) }
    val group = selectedGroup.takeIf { it in groups } ?: groups.first()
    val visible = actions.filter { it.page == section && it.group.ifEmpty { "Общие" } == group }
    var panel by rememberSaveable { mutableStateOf("mfdSetup") }
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(if(!state.connected) state.status else when(state.inputAvailability) {
            "ready" -> "Ввод готов · F1 24 активно"
            "disabled" -> "Кнопки отключены: включите «Разрешить клавиатурный ввод» в Companion"
            "unfocused" -> "Кнопки ждут активного окна F1 24"
            "demo" -> "Демонстрация: игровой ввод отключён"
            else -> "Проверка готовности ввода…"
        }, fontSize = 15.sp)
        if (state.command.isNotBlank()) Text(state.command, fontSize = 15.sp)
        Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            sections.forEach { name -> FilterChip(selected = name == section,
                onClick = { model.releaseAll(); selectedSection = name }, label = { Text(name, fontSize = 15.sp) }) }
        }
        if (section != "Трасса") Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            groups.forEach { name -> FilterChip(selected = name == group,
                onClick = { model.releaseAll(); selectedGroup = name }, label = { Text(name, fontSize = 14.sp) }) }
        }
        key(section, group) {
            when {
                section == "Control Scheme" && group == "Инженер" -> {
                    val sessionType=state.telemetry?.f1?.race?.sessionType
                    val requests=engineerRequests(sessionType)
                    Text("Запросы инженеру · " + if(sessionType in 15..17) "гонка" else "тренировка",fontSize=20.sp)
                    Text("Запускайте при закрытом радиоменю. Кнопка открывает список, выбирает запрос и подтверждает его. Ответ звучит в игре.",fontSize=15.sp)
                    val ready=state.connected && !state.stale && !state.demo && state.inputAvailability=="ready" && requests.isNotEmpty() && state.telemetry?.f1?.race?.let { it.fresh && it.drivers.any { d -> d.player && d.onTrack } } == true
                    if(!ready) Text("Выйдите на трассу в тренировке или гонке. В боксах запросы отключены.",fontSize=14.sp)
                    requests.withIndex().toList().chunked(2).forEach { row ->
                        Row(horizontalArrangement=Arrangement.spacedBy(8.dp)) {
                            row.forEach { (i,label) -> OutlinedButton(onClick={ model.engineerRequest(i) },enabled=ready && !state.menuBusy,modifier=Modifier.weight(1f).height(58.dp)) { Text(label,fontSize=16.sp) } }
                        }
                    }
                }
                section == "Трасса" -> F1RaceScreen(state)
                section == "Control Scheme" && group == "Обзор" -> {
                    F1Arrows("look", visible, state, model)
                    F1Grid(visible.filterNot { it.id in setOf("lookUp", "lookDown", "lookLeft", "lookRight") }, state, model)
                }
                section == "MFD" && group == "Навигация" -> {
                    val pageNames = mapOf("mfd" to "Листать", "mfdSetup" to "Машина", "mfdPit" to "Пит-стоп", "mfdDamage" to "Повреждения", "mfdEngine" to "Двигатель", "mfdTemps" to "Температуры")
                    (pageNames.toList() + ("map" to "Карта")).chunked(3).forEach { row ->
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            row.forEach { (id, label) ->
                                FilterChip(selected = panel == id, onClick = {
                                    model.releaseAll()
                                    if (id == "mfd") model.press(id, false)
                                    if (id != "mfd") panel = id
                                }, modifier = Modifier.weight(1f), label = { Text(label, fontSize = 15.sp) })
                            }
                        }
                    }
                    F1Panel(panel, state, model)
                    actions.find { it.id == panel && panel!="mfdPit" }?.let { pageAction ->
                        OutlinedButton(onClick = { model.press(pageAction.id, false) }, modifier = Modifier.fillMaxWidth()) {
                            Text("Открыть эту страницу в игре · ${pageAction.key}", fontSize = 15.sp)
                        }
                    }
                    BoxWithConstraints {
                        if (maxWidth > 440.dp) Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(8.dp)) { F1Arrows("mfd", visible, state, model) }
                            Column(Modifier.weight(1.2f), verticalArrangement = Arrangement.spacedBy(4.dp)) { F1Adjustments(actions, state, model, expandable = true) }
                        } else Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            F1Arrows("mfd", visible, state, model)
                            F1Adjustments(actions, state, model, expandable = true)
                        }
                    }
                    F1Grid(visible.filterNot { it.id.startsWith("mfd") }, state, model)
                }
                section == "MFD" && group == "Быстрые настройки" -> {
                    F1Adjustments(actions, state, model, expandable = false)
                    F1Grid(visible.filterNot { it.id in f1AdjustmentIds }, state, model)
                }
                section == "Menu Controls" && group == "Навигация" -> {
                    F1Arrows("menu", visible, state, model)
                    F1Grid(visible.filterNot { it.id in setOf("menuUp", "menuDown", "menuLeft", "menuRight") }, state, model)
                }
                section == "Menu Controls" && group == "Стики" -> {
                    Text("Левый стик", fontSize = 16.sp); F1Arrows("ls", visible, state, model)
                    Text("Правый стик", fontSize = 16.sp); F1Arrows("rs", visible, state, model)
                    F1Grid(visible.filterNot { it.id in setOf("lsUp", "lsDown", "lsLeft", "lsRight", "rsUp", "rsDown", "rsLeft", "rsRight") }, state, model)
                }
                else -> {
                    if (section == "Control Scheme" && group == "Гонка") {
                        Button(onClick = { selectedSection = "MFD"; selectedGroup = "Навигация"; panel = "mfdPit" }, modifier = Modifier.fillMaxWidth()) { Text("Пит-стоп · резина и крыло", fontSize = 18.sp) }
                        F1Grid(visible.filterNot { it.id == "pitStop" }, state, model)
                        Text("Радио инженера — T. Голосовая связь — удержание Y для микрофона ПК; звук с планшета не передаётся.", fontSize = 14.sp)
                    } else F1Grid(visible, state, model)
                }
            }
        }
        Text("Keyboard Preset 2 · MFD B · лимитер P", fontSize = 13.sp)
        if (state.demo) Text("Демонстрация: команды отключены.", fontSize = 13.sp)
    }
}

private val f1Adjustments = listOf(
    Triple("Баланс тормозов", "brakeBiasDown", "brakeBiasUp"),
    Triple("Дифференциал", "diffDown", "diffUp"),
    Triple("Топливная смесь", "fuelDown", "fuelUp"),
    Triple("Режим ERS", "ersDown", "ersUp")
)
private val f1AdjustmentIds = f1Adjustments.flatMap { listOf(it.second, it.third) }.toSet()

@Composable private fun F1Adjustments(actions: List<DeckAction>, state: DeckState, model: DeckModel, expandable: Boolean) {
    var expanded by rememberSaveable { mutableStateOf("") }
    f1Adjustments.forEach { (label, down, up) ->
        val pair = listOfNotNull(actions.find { it.id == down }, actions.find { it.id == up })
        if (pair.isNotEmpty()) {
            if (expandable) OutlinedButton(onClick = { model.releaseAll(); expanded = if (expanded == down) "" else down }, modifier = Modifier.fillMaxWidth()) {
                Text(label + if (expanded == down) "  ▴" else "  ▾", fontSize = 16.sp)
            } else Text(label, fontSize = 16.sp)
            if (!expandable || expanded == down) F1Grid(pair.map { it.copy(label = if (it.id == down) "−" else "+") }, state, model, heightDp = 68)
        }
    }
}

@Composable private fun F1Arrows(prefix: String, actions: List<DeckAction>, state: DeckState, model: DeckModel) {
    val directions = listOf(listOf(null, "Up", null), listOf("Left", "Down", "Right"))
    val symbols = mapOf("Up" to "↑", "Left" to "←", "Down" to "↓", "Right" to "→")
    directions.forEach { row ->
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            row.forEach { direction ->
                val action = actions.find { direction != null && it.id == prefix + direction }
                if (action == null) Spacer(Modifier.weight(1f))
                else F1Button(action.copy(label = symbols.getValue(direction!!), description = action.label), state, model, Modifier.weight(1f), heightDp = 72)
            }
        }
    }
}

@Composable private fun F1Grid(actions: List<DeckAction>, state: DeckState, model: DeckModel, columns: Int = 2, heightDp: Int = 98) {
    actions.chunked(columns).forEach { row ->
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            row.forEach { action -> F1Button(action, state, model, Modifier.weight(1f), heightDp) }
            repeat(columns - row.size) { Spacer(Modifier.weight(1f)) }
        }
    }
}

@Composable private fun F1Button(action: DeckAction, state: DeckState, model: DeckModel, modifier: Modifier, heightDp: Int = 98) {
    key(action.id, action.gesture) {
        Control(action.label, if (action.gesture == "hold") "Удерживать · ${action.key}" else action.key,
            action.id, action.gesture == "hold", state, model, modifier, heightDp)
    }
}
