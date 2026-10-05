package dev.simdeck

import android.content.Context
import org.json.JSONArray
import org.json.JSONObject

/** Explicit debug-only QA launch. Never connects, changes pairing, or enables game input. */
internal fun phonePreview(context:Context,id:String?):DeckState? {
    if(!BuildConfig.DEBUG || !BuildConfig.PHONE_LAYOUT || id==null) return null
    val profiles=JSONArray(context.assets.open("phone-preview-profiles.json").bufferedReader().use {it.readText()})
    val p=(0 until profiles.length()).map {profiles.getJSONObject(it)}.firstOrNull {it.getString("id")==id}?:return null
    val actions=Protocol.controls(JSONObject().put("controls",p.getJSONArray("actions")))
    val states=mapOf("fs25Motor" to true,"fs25Lower" to false,"fs25TurnOn" to true,"fs25Pause" to false,"accHeadlights" to true,"ignition" to true,"hazards" to true,"etsCruise" to true,"etsParkingBrake" to false)
    val wheels=List(4){i->VehicleWheel(if(i%2==0)-1.0 else 1.0,if(i<2)-1.0 else 1.0,null)}
    val vehicle=when(id) {
        "fs25" -> VehicleInfo("preview","MF 8570","combine",wheels,listOf(VehicleAttachment("header","preview","Жатка MF 8570","header",emptyList(),false,true,1.0,"front")),true,emptyMap())
        "ets2", "ats" -> VehicleInfo("preview",if(id=="ats")"Kenworth W900" else "Volvo FH","truck",wheels+listOf(VehicleWheel(-1.0,2.0,true),VehicleWheel(1.0,2.0,true)),listOf(VehicleAttachment("trailer","preview","Полуприцеп","trailer",wheels,null,null,null)),true,mapOf("engine" to .03,"transmission" to .12,"chassis" to .08,"cabin" to .02,"wheels" to .07))
        "snowrunner" -> VehicleInfo("preview","Western Star 6900 TwinSteer","truck",List(8){i->VehicleWheel(if(i%2==0)-1.0 else 1.0,(i/2).toDouble(),null)},emptyList(),true,emptyMap())
        "beamng-default" -> VehicleInfo("preview","Gavril D-Series","pickup",wheels,emptyList(),true,mapOf("engine" to .07,"cabin" to .18,"chassis" to .05))
        else -> null
    }
    val f1=if(id.startsWith("f1-")) F1Data(List(4){WheelData(89.0,93.0,420.0,23.0,12.0,0.0)},105.0,mapOf("compound" to 16.0,"frontLeftWingDamage" to 20.0,"frontRightWingDamage" to 38.0,"rearWingDamage" to 4.0,"floorDamage" to 8.0,"sidepodDamage" to 18.0,"drsFault" to 1.0),emptyList(),null,RaceData(3,5300,15,true,listOf(RaceDriver(0,"Norris",1,4,1,8,2200.0,0,2,1,0,0,false),RaceDriver(1,"Player",1,1,2,8,2100.0,0,2,1,1000,1000,true),RaceDriver(2,"Piastri",1,81,3,8,2000.0,0,2,1,1000,2000,false)))) else null
    val acc=if(id=="acc") AccData(List(4){AccWheelData(27.5,83.0,410.0,4.0,90.0,95.0,0.0)},23.0,31.0,90.0,54.0,false,true,false,true) else null
    val fs=if(id=="fs25") Fs25Data("Тестовая ферма","Riverbend Springs","Сентябрь","04.10.2026 12:00",false,165000.0,0.0,listOf(Fs25Field(6,"WHEAT","GROWING",3,1,2),Fs25Field(9,"BARLEY","HARVEST_READY",0,3,3),Fs25Field(12,"CANOLA","GROWING",1,2,1)),emptyList(),emptyList(),null) else null
    val prices=if(id=="fs25") Fs25Prices(listOf(Fs25PriceOffer("WHEAT","Пшеница","Зерновой элеватор",1124.0,"1 124 €"),Fs25PriceOffer("BARLEY","Ячмень","Фермерский рынок",980.0,"980 €"),Fs25PriceOffer("CANOLA","Рапс","Маслозавод",2340.0,"2 340 €")),0) else null
    val snow=if(id=="snowrunner") SnowRunnerData("QA fixture",360.0,listOf(SnowRunnerComponent("engine","Двигатель",8,280),SnowRunnerComponent("transmission","Коробка",0,220),SnowRunnerComponent("fuelTank","Бак",0,100),SnowRunnerComponent("suspension","Подвеска",15,260)),null,null,null) else null
    val t=if(id=="ams2") null else Telemetry(if(id=="fs25")3.3 else 59.4,if(id=="fs25")2100.0 else 11200.0,5,.64,15000.0,headlights=1,fuelLiters=if(snow!=null)294.0 else null,snowRunner=snow,actionStates=if(snow!=null)mapOf("snowEngine" to true,"snowAwd" to false,"snowDifferential" to true) else states,f1=f1,acc=acc,vehicle=vehicle,fs25=fs,fs25Prices=prices,ets2Navigation=if(isScsTruck(id)) Ets2Navigation(184.0,134.0,80.0,0.0,0.0,.15) else null)
    val map=if(isScsTruck(id)) EtsMap(0.0,0.0,1600.0,listOf(EtsRoad(floatArrayOf(-700f,-500f,-100f,-50f,200f,300f,700f,400f),12f),EtsRoad(floatArrayOf(-600f,400f,0f,0f,700f,-300f),8f)),emptyList()) else null
    return DeckState(status="Тестовый просмотр",connected=true,telemetry=t,stale=false,demo=true,controls=actions,profileName=p.getString("name"),profileId=id,inputAvailability="demo",etsMap=map)
}
