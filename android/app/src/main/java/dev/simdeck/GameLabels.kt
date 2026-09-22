package dev.simdeck

internal fun gameDisplayName(profileId: String, profileName: String): String = when (profileId) {
    "f1-24" -> "F1 24"
    "f1-25" -> "F1 25"
    else -> profileName
}

internal fun f1UdpFormat(profileId: String): String = if (profileId == "f1-25") "2025" else "2024"

internal fun telemetryHint(profileId: String): String = when (profileId) {
    "f1-24", "f1-25" -> "Включите UDP → 127.0.0.1:20777 и выйдите на трассу."
    "beamng-default" -> "Нет телеметрии от игры. Проверьте мод SimDeck в Companion."
    else -> "Профиль управления готов. Телеметрия для этой игры пока не подключена."
}

internal fun controlsHint(profileId: String, page: String): String = when {
    profileId in setOf("f1-24", "f1-25") -> "DRS, ERS и лимитер: ВКЛ по телеметрии. Свои кнопки добавляются в Companion."
    profileId == "beamng-default" && page == "Возврат" -> "Вернуть — удержание. Сохранить — записать текущую позицию машины."
    profileId == "beamng-default" -> "ВКЛ и тёмная кнопка — действие включено. Ближний — зелёный, дальний — синий."
    else -> "Клавиши должны совпадать с назначениями в игре. Изменить их можно в Companion."
}
