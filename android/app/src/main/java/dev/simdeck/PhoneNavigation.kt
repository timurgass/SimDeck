package dev.simdeck

internal data class PhoneTab(val id:String,val label:String,val icon:String)
internal fun phoneTabs(profile:String):List<PhoneTab> = when(profile) {
    "f1-24","f1-25" -> listOf(PhoneTab("drive","Гонка","overtake"),PhoneTab("condition","Болид","mfdDamage"),PhoneTab("map","Карта","map"))
    "acc" -> listOf(PhoneTab("drive","Гонка","accStarter"),PhoneTab("condition","Шины","accTcUp"),PhoneTab("pit","Пит","accRequestPit"))
    "ams2" -> listOf(PhoneTab("drive","Езда","amsStarter"),PhoneTab("pit","Пит","amsRequestPit"),PhoneTab("camera","Камера","amsCamera"))
    "ets2" -> listOf(PhoneTab("drive","Кабина","etsEngine"),PhoneTab("map","Навигатор","etsMap"),PhoneTab("condition","Состояние","etsDifferential"))
    "fs25" -> listOf(PhoneTab("drive","Техника","fs25Motor"),PhoneTab("fields","Поля","fs25Seeds"),PhoneTab("prices","Цены","fs25Store"))
    "snowrunner" -> listOf(PhoneTab("drive","Езда","snowEngine"),PhoneTab("winch","Лебёдка","snowQuickWinch"),PhoneTab("cargo","Груз","snowPackCargo"))
    else -> listOf(PhoneTab("drive","Езда","ignition"),PhoneTab("condition","Машина","reset"),PhoneTab("camera","Камера","camera"))
} + PhoneTab("more","Ещё","menu")

// Only shortcuts are curated. Every received action remains accessible through More.
internal fun phoneShortcuts(profile:String):List<Pair<String,String>> = when(profile) {
    "f1-24","f1-25" -> listOf("mfd" to "MFD","camera" to "Камера","radio" to "Радио","pitStop" to "Пит-стоп · действие","pitLimiter" to "Лимитер","menuBack" to "Назад в игре")
    "acc" -> listOf("accIgnition" to "Зажигание","accStarter" to "Стартер","accHeadlights" to "Фары","accWipers" to "Дворники","accRainLight" to "Дождевой фонарь","accRequestPit" to "Пит-стоп")
    "ams2" -> listOf("amsHeadlights" to "Фары","amsWipers" to "Дворники","amsRequestPit" to "Пит-стоп","amsPitLimiter" to "Лимитер","amsCamera" to "Камера","amsPause" to "Пауза")
    "ets2" -> listOf("etsLights" to "Фары","etsHighBeam" to "Дальний свет","etsHazards" to "Аварийка","etsParkingBrake" to "Ручник","etsCruise" to "Круиз","etsCamera" to "Камера")
    "fs25" -> listOf("fs25Lower" to "Поднять / опустить","fs25TurnOn" to "Включить агрегат","fs25Fold" to "Сложить / разложить","fs25Attach" to "Сцепка","fs25Unload" to "Разгрузить в точке","fs25Pause" to "Пауза времени")
    "snowrunner" -> listOf("snowAwd" to "Полный привод","snowDifferential" to "Блокировка","snowQuickWinch" to "Лебёдка","snowReleaseWinch" to "Отпустить трос","snowEngine" to "Двигатель","snowCamera" to "Камера")
    else -> listOf("ignition" to "Зажигание","lights" to "Фары","hazards" to "Аварийка","fourWheelDrive" to "Полный привод","camera" to "Камера","reset" to "Сброс авто")
}
internal fun phonePages(actions:List<DeckAction>) = actions.map { it.page }.distinct()
internal fun phonePageActions(actions:List<DeckAction>,page:String) = actions.filter { it.page==page }
