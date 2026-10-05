package dev.simdeck
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
class SnowRunnerDataTest {
 @Test fun nativeHealthAndUnknownStatesSurviveParsing() {
  val frame=Protocol.telemetry(JSONObject().put("data",JSONObject("""{"speedMps":0,"rpm":0,"gear":0,"snowRunner":{"build":"test","fuelCapacity":360,"components":[{"id":"engine","name":"Engine","damage":8,"capacity":280}]},"actionStates":{"snowEngine":false}}""")))!!
  assertEquals(272,frame.snowRunner!!.components.single().remaining)
  assertEquals(8.0/280,frame.snowRunner!!.components.single().damageFraction,.00001)
  assertNull(frame.snowRunner!!.gearLabel)
  assertNull(frame.actionStates["snowAwd"])
  assertEquals(false,frame.actionStates["snowEngine"])
 }
 @Test fun malformedDamageAndCapacityAreRejected() {
  assertNull(SnowRunnerData.parse(JSONObject("""{"fuelCapacity":0,"components":[]}""")))
  val data=SnowRunnerData.parse(JSONObject("""{"fuelCapacity":100,"components":[{"id":"bad","damage":101,"capacity":100}],"awdAvailable":false}"""))!!
  assertTrue(data.components.isEmpty());assertEquals(false,data.awdAvailable);assertNull(data.differentialAvailable)
 }
}
