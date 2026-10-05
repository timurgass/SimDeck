package dev.simdeck

import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class Fs25OperationsTest {
    private fun data()=JSONObject("""{"ledgers":[{"farmId":2,"days":[{"day":0,"income":1200,"expenses":200,"net":1000,"entries":[{"category":"purchaseFuel","amount":-200}]}]}],"stocks":[{"farmId":2,"crop":"WHEAT","litres":10000,"location":"silo","source":"storage"}],"fleet":[{"farmId":2,"model":"tractor","property":"LEASED","hours":2,"damage":35}],"productions":[{"farmId":2,"building":"Mill","recipe":"flour","enabled":true}],"storageAvailable":true,"fleetAvailable":true}""")
    @Test fun parsesSavedFarmWithoutCrossingOwnership(){val d=Fs25OperationsData.parse(data())!!;assertEquals(2,d.stocks.single().farmId);assertEquals(-200.0,d.ledgers.single().days.single().entries.single().amount,0.0);assertEquals(35.0,d.fleet.single().damage!!,0.0);assertEquals(true,d.productions.single().enabled)}
    @Test fun rejectsNegativeStockAndOversizedDamageIsUnknown(){val a=data();a.getJSONArray("stocks").getJSONObject(0).put("litres",-1);assertNull(Fs25OperationsData.parse(a));val b=data();b.getJSONArray("fleet").getJSONObject(0).put("damage",101);assertNull(Fs25OperationsData.parse(b)!!.fleet.single().damage)}
    @Test fun oldPayloadHasNoInventedOperations(){assertNull(Fs25OperationsData.parse(null));val a=data();a.getJSONArray("productions").getJSONObject(0).put("enabled",JSONObject.NULL);assertNull(Fs25OperationsData.parse(a)!!.productions.single().enabled)}
}
