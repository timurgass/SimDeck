package dev.simdeck

// Observed F1 24 PRACTICE radio menu. Other session menus require their own verified order.
internal val practiceRequests = listOf("В боксы на этом круге", "Машина впереди", "Машина позади", "Обзор сессии", "Напарник", "Состояние болида", "Моя позиция", "Прогноз погоды", "Топливо", "Лучший круг сессии")
// Observed STANDARD RACE menu. The final tyre suggestion is dynamic and is excluded.
internal val raceRequests = listOf("В боксы на этом круге", "Обзор гонки", "Машина впереди", "Машина позади", "Состояние шин", "Состояние болида", "Прогноз погоды", "Топливо", "Информация о пит-стопе")
internal fun engineerRequests(sessionType: Int?) = when(sessionType) {
    in 1..4 -> practiceRequests
    in 15..17 -> raceRequests
    else -> emptyList()
}
internal fun engineerSequence(row: Int, requests: List<String> = practiceRequests): List<String> {
    require(row in requests.indices)
    return listOf("radio") + List(row) { "mfdDown" } + "mfdRight"
}
internal suspend fun runMenuSequence(actions: List<String>, allowed: () -> Boolean,
    inject: suspend (String) -> Boolean, wait: suspend (Long) -> Unit): Boolean {
    for ((i,action) in actions.withIndex()) {
        if (!allowed() || !inject(action)) return false
        wait(if(i==0) 1400L else 300L)
    }
    return true
}

internal fun pitAdjustmentSteps(from: Int, to: Int, increase: Boolean): List<String> {
    require(from in 0..2 && to in 0..2)
    val navigation=when((to-from+3)%3) { 1 -> listOf("mfdDown"); 2 -> listOf("mfdUp"); else -> emptyList() }
    return navigation + if(increase) "mfdRight" else "mfdLeft"
}
internal fun pitSelectionSteps(from: Int, row: Int, current: Int, target: Int, count: Int): List<String> {
    require(row in 1..2 && count == if(row==1) 3 else 5)
    require(current in 0 until count && target in 0 until count)
    val navigation=pitAdjustmentSteps(from,row,true).dropLast(1)
    // Horizontal MFD lists clamp at their ends; unlike the row cursor they do not wrap.
    return navigation + List(kotlin.math.abs(target-current)) { if(target>current) "mfdRight" else "mfdLeft" }
}
internal fun pitReady(s: DeckState) = s.connected && !s.stale && !s.demo && s.profileId in setOf("f1-24","f1-25") &&
    s.inputAvailability=="ready" && s.telemetry?.f1?.mfdPanelIndex==1 &&
    s.telemetry?.f1?.race?.let { it.fresh && it.sessionType in 15..17 && it.drivers.any { d -> d.player && d.onTrack } } == true
