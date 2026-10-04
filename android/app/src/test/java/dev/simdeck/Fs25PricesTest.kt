package dev.simdeck
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class Fs25PricesTest {
    @Test fun visibleSearchFiltersOffersAndCombinesWithProductChoice() {
        val data=Fs25Prices(listOf(
            Fs25PriceOffer("WHEAT","Пшеница","Порт",1400.0,""),
            Fs25PriceOffer("WHEAT","Пшеница","Элеватор",1200.0,""),
            Fs25PriceOffer("HONEY","Мёд","Магазин",2000.0,"")),0)
        assertEquals(2,fs25Offers(data,null,"  ПШЕНИ  ").size)
        assertEquals("Порт",fs25Offers(data,null," ПОРТ ").single().station)
        assertEquals("HONEY",fs25Offers(data,null,"мёд").single().crop)
        assertTrue(fs25Offers(data,"HONEY","порт").isEmpty())
        assertTrue(fs25Offers(data,null,"Нет такого товара").isEmpty())
        assertEquals(3,fs25Offers(data,null,"   ").size)
    }
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
    @Test fun productsAndStationsUseRussianAlphabetAndBestPriceNeedNotBeFirst() {
        val data=Fs25Prices(listOf(
            Fs25PriceOffer("BARLEY","Ячмень","База",950.0,""),
            Fs25PriceOffer("WHEAT","Пшеница","Элеватор",1400.0,""),
            Fs25PriceOffer("HONEY","Мёд","Завод",2000.0,""),
            Fs25PriceOffer("WHEAT","Пшеница","База",1000.0,"")),0)
        assertEquals(listOf("Мёд","Пшеница","Пшеница","Ячмень"),fs25Offers(data,null).map { it.cropName })
        assertEquals(listOf("База","Элеватор"),fs25Offers(data,"WHEAT").map { it.station })
        assertEquals(1400.0,fs25Offers(data,"WHEAT").maxOf { it.pricePer1000 },0.0)
    }
}
