package dev.simdeck

import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class PhoneNavigationTest {
    private fun profiles()=JSONArray(javaClass.classLoader!!.getResource("phone-preview-profiles.json")!!.readText())
    @Test fun shippedShortcutsUseReceivedActionsAndKeepGestures() {
        val profiles=profiles()
        assertEquals(8,profiles.length())
        for(i in 0 until profiles.length()) {
            val p=profiles.getJSONObject(i);val id=p.getString("id")
            val actions=Protocol.controls(JSONObject().put("controls",p.getJSONArray("actions")))
            val ids=actions.map {it.id}.toSet()
            for((shortcut,_) in phoneShortcuts(id)) assertTrue("$id shortcut $shortcut missing",shortcut in ids)
            assertEquals(4,phoneTabs(id).size)
            assertEquals(4,phoneTabs(id).map {it.id}.distinct().size)
            assertEquals("more",phoneTabs(id).last().id)
            // The runtime must keep received bindings/hold gestures; navigation does not manufacture them.
            val all=phonePages(actions).flatMap {phonePageActions(actions,it)}
            assertEquals(actions.toSet(),all.toSet())
        }
    }
    @Test fun customPagesActionsAndHoldsRemainReachable() {
        val custom=listOf(DeckAction("custom","Моя страница","Мой сигнал","","Ctrl+H","hold","Мои действия"),DeckAction("custom2","Моя страница","Свет","","L","press"),DeckAction("custom3","Другое","Тест","","G","press"))
        assertEquals(custom.take(2),phonePageActions(custom,"Моя страница"))
        assertEquals("hold",phonePageActions(custom,"Моя страница").first().gesture)
        assertEquals(custom.toSet(),phonePages(custom).flatMap {phonePageActions(custom,it)}.toSet())
    }
    @Test fun formulaYearsShareDestinationsAndActions() {
        assertEquals(phoneTabs("f1-24"),phoneTabs("f1-25"))
        assertEquals(phoneShortcuts("f1-24"),phoneShortcuts("f1-25"))
    }
}
