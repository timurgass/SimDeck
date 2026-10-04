package dev.simdeck

import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class Fs25FieldsTest {
    @Test fun fieldsKeepRealValuesAndMissingValuesUnknown() {
        val data=Fs25Data.parse(JSONObject("""{"header":{"savegameName":"Farm","mapTitle":"Map"},"environment":{},"fields":[{"id":6,"fruitType":"WHEAT","growthState":5,"lastGrowthState":4,"sprayLevel":2,"weedState":8,"plowLevel":1},{"id":9,"fruitType":"BARLEY","weedState":null,"sprayLevel":-1}]}"""),JSONObject("""{"alerts":[{"field":6,"message":"Weeds","severity":2}],"tasks":[{"task":{"field":6,"title":"Weed"},"status":0}]}"""))!!
        assertEquals(5,data.fields[0].growth);assertEquals(4,data.fields[0].lastGrowth);assertEquals(2,data.fields[0].fertilizer)
        assertNull(data.fields[1].weeds);assertNull(data.fields[1].fertilizer);assertEquals(6,data.alerts[0].field);assertEquals(6,data.tasks[0].field)
    }
    @Test fun numberAndCropSearchUsesSavedFieldsOnly() {
        val labels=Fs25FieldLabels(JSONObject("""{"crops":{"WHEAT":["Пшеница","#ffffff"],"BARLEY":["Ячмень","#ffffff"]}}"""))
        val fields=listOf(Fs25Field(9,"BARLEY","",null,null,null),Fs25Field(6,"WHEAT","",8,3,2),Fs25Field(6,"WHEAT","",8,3,2),Fs25Field(0,"WHEAT","",0,0,0))
        assertEquals(listOf(6,9),filteredFs25Fields(fields,"",null,labels).map{it.id})
        assertEquals(listOf(6),filteredFs25Fields(fields,"пшени",null,labels).map{it.id})
        assertEquals(listOf(9),filteredFs25Fields(fields,"9",null,labels).map{it.id})
        assertTrue(filteredFs25Fields(fields,"6","BARLEY",labels).isEmpty())
    }
    @Test fun malformedFieldLevelsDoNotShowAsZero() {
        assertNull(fs25Int(JSONObject("""{"value":-1}"""),"value"));assertNull(fs25Int(JSONObject("""{"value":1.5}"""),"value"));assertNull(fs25Int(JSONObject("""{"value":"3"}"""),"value"));assertEquals(0,fs25Int(JSONObject("""{"value":0}"""),"value"))
    }
    @Test fun alphabeticalFieldsUseTranslatedNamesAndNumericIdsWithinCulture() {
        val labels=Fs25FieldLabels(JSONObject("""{"crops":{"BARLEY":["Ячмень"],"HONEY":["Мёд"],"WHEAT":["Пшеница"]}}"""))
        val fields=listOf(Fs25Field(1,"BARLEY","",null,null,null),Fs25Field(12,"WHEAT","",null,null,null),Fs25Field(2,"WHEAT","",null,null,null),Fs25Field(9,"HONEY","",null,null,null))
        assertEquals(listOf(9,2,12,1),filteredFs25Fields(fields,"",null,labels).map { it.id })
    }
}
