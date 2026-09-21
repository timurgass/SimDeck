package dev.simdeck
import kotlinx.coroutines.test.runTest
import org.junit.Assert.*
import org.junit.Test
class EngineerRequestsTest {
    @Test fun absolutePitSelectionsNavigateToCorrectFieldFromEveryCursor() {
        for(start in 0..2) for(row in 1..2) {
            val count=if(row==1) 3 else 5
            for(current in 0 until count) for(target in 0 until count) {
                var cursor=start
                val values=intArrayOf(25,if(row==1) current else 0,if(row==2) current else 0)
                for(action in pitSelectionSteps(start,row,current,target,count)) when(action) {
                    "mfdUp" -> cursor=(cursor+2)%3
                    "mfdDown" -> cursor=(cursor+1)%3
                    "mfdLeft" -> values[cursor]=(values[cursor]-1).coerceAtLeast(0)
                    "mfdRight" -> values[cursor]=(values[cursor]+1).coerceAtMost(count-1)
                }
                assertEquals("selected field",row,cursor)
                assertEquals(target,values[row])
                assertEquals("tyre/repair must never alter downforce",25,values[0])
                assertEquals("other selector unchanged",0,values[if(row==1) 2 else 1])
            }
        }
    }
    @Test fun raceAndPracticeUseTheirOwnObservedOrder() {
        val race=engineerRequests(15); val practice=engineerRequests(1)
        assertEquals("Состояние шин",race[4])
        assertEquals("Напарник",practice[4])
        assertEquals(6,engineerSequence(race.indexOf("Прогноз погоды"),race).count { it=="mfdDown" })
        assertEquals(7,engineerSequence(practice.indexOf("Прогноз погоды"),practice).count { it=="mfdDown" })
        assertTrue(engineerRequests(5).isEmpty())
        assertTrue(engineerRequests(null).isEmpty())
    }
    @Test fun pitMovesFromEverySyncedRowWithoutAssumingTopOfMenu() {
        for(from in 0..2) for(to in 0..2) {
            var cursor=from
            val steps=pitAdjustmentSteps(from,to,true)
            steps.dropLast(1).forEach { cursor=(cursor+if(it=="mfdDown") 1 else 2)%3 }
            assertEquals(to,cursor); assertEquals("mfdRight",steps.last())
            assertEquals("mfdLeft",pitAdjustmentSteps(from,to,false).last())
        }
    }
    @Test fun weatherOrderMatchesObservedPracticeMenu() {
        val a=engineerSequence(7);assertEquals("radio",a.first());assertEquals(7,a.count { it=="mfdDown" });assertEquals("mfdRight",a.last())
    }
    @Test fun rejectedStepNeverConfirmsAnotherMenuItem()=runTest {
        val sent=mutableListOf<String>()
        assertFalse(runMenuSequence(engineerSequence(2),{true},{sent.add(it);sent.size<2},{}))
        assertEquals(listOf("radio","mfdDown"),sent)
    }
    @Test fun lossOfFocusStopsBeforeNextKey()=runTest {
        var allowed=true;val sent=mutableListOf<String>()
        assertFalse(runMenuSequence(engineerSequence(1),{allowed},{sent.add(it);true},{allowed=false}))
        assertEquals(listOf("radio"),sent)
    }
}
