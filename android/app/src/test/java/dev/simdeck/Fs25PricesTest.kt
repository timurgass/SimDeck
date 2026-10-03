package dev.simdeck
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class Fs25PricesTest {
    @Test fun invalidPricesRemainUnknownAndBestStationIsSorted() {
        val data=Fs25Prices.parse(JSONObject("""{"ageMs":300,"offers":[{"crop":"WHEAT","cropName":"Wheat","station":"Port","pricePer1000":900},{"crop":"WHEAT","station":"Mill","pricePer1000":1250,"formatted":"1,250 $"},{"crop":"WHEAT","station":"Invalid","pricePer1000":-1}]}"""))!!
        assertEquals(2,data.offers.size);assertEquals("Mill",fs25Offers(data,"WHEAT").first().station)
        assertTrue(fs25Offers(data,"BARLEY").isEmpty());assertNull(Fs25Prices.parse(null))
        assertNull(Fs25Prices.parse(JSONObject("""{"offers":[]} """)))
    }
    @Test fun fieldLevelsUsePercentAndUnknownNeverBecomesZero() {
        assertEquals(33,fs25FieldPercent(3,9));assertEquals(67,fs25FieldPercent(2,3))
        assertEquals(100,fs25FieldPercent(1,1));assertEquals(0,fs25FieldPercent(0,9))
        assertNull(fs25FieldPercent(null,9));assertNull(fs25FieldPercent(10,9));assertNull(fs25FieldPercent(-1,3))
    }
}
