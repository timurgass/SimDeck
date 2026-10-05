package dev.simdeck
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
class BeamNgDamageTest {
 @Test fun unknownsAndKnownFalseRemainDifferent(){
  val d=BeamNgDamage.parse(JSONObject("""{"body":{"FL":0.2,"FR":2},"faults":{"radiatorLeak":false,"engineLockedUp":true,"unknown":null},"wheels":[{"index":0,"name":"FL","flat":true,"broken":false}],"parts":[{"name":"Suspension","damage":0.7}],"totalDamagedParts":12}"""))!!
  assertEquals(.2,d.body["FL"]!!,.001);assertFalse(d.body.containsKey("FR"));assertEquals(false,d.faults["radiatorLeak"]);assertNull(d.faults["unknown"]);assertTrue(d.wheels[0].flat!!);assertNull(d.wheels[0].brakeDamaged);assertEquals(12,d.totalDamagedParts)
 }
 @Test fun oldModRemainsSupported(){assertNull(BeamNgDamage.parse(null));val d=BeamNgDamage.parse(JSONObject())!!;assertTrue(d.body.isEmpty());assertNull(d.totalDamagedParts);assertNull(d.oilTemperature)}
}
