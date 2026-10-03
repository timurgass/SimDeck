package dev.simdeck

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import org.json.JSONObject

data class Fs25PriceOffer(val crop:String,val cropName:String,val station:String,val pricePer1000:Double,val formatted:String)
data class Fs25Prices(val offers:List<Fs25PriceOffer>,val ageMs:Long,val receivedAtNanos:Long=System.nanoTime()) {
    companion object {
        fun parse(root:JSONObject?):Fs25Prices? {
            root ?: return null
            val array=root.optJSONArray("offers") ?: return null
            if(array.length()>2048) return null
            val age=(root.opt("ageMs") as? Number)?.toLong()?.takeIf { it>=0 } ?: return null
            val offers=(0 until array.length()).mapNotNull { i ->
                val row=array.optJSONObject(i) ?: return@mapNotNull null
                val price=(row.opt("pricePer1000") as? Number)?.toDouble() ?: return@mapNotNull null
                val crop=row.optString("crop");val station=row.optString("station")
                if(!price.isFinite() || price<=0 || price>=100000000 || crop.isBlank() || station.isBlank() || crop.length>160 || station.length>160) return@mapNotNull null
                Fs25PriceOffer(crop,row.optString("cropName").take(160),station,price,row.optString("formatted").take(160))
            }
            return Fs25Prices(offers,age)
        }
    }
}
internal fun fs25Offers(prices:Fs25Prices?,crop:String?) = prices?.offers.orEmpty().filter { crop==null || it.crop==crop }.sortedByDescending { it.pricePer1000 }

@Composable internal fun Fs25PricesPanel(state:DeckState) {
    val d=LocalProfileDesign.current
    val prices=state.telemetry?.fs25Prices
    var crop by rememberSaveable { mutableStateOf<String?>(null) }
    val crops=prices?.offers.orEmpty().distinctBy { it.crop }.sortedBy { it.cropName }
    val selected=crop?.takeIf { id -> crops.any { it.crop==id } }
    DesignCard {
        Text("ЦЕНЫ НА КУЛЬТУРЫ",fontSize=23.sp,lineHeight=27.sp,fontWeight=FontWeight.Bold,color=d.accent)
        Text("Текущие предложения пунктов продажи · за 1 000 л",color=d.muted)
        Text(when { prices==null -> "Ждём цены из мода SimDeck FS25 версии 1.4.0.0."
            !state.connected || prices.ageMs+((System.nanoTime()-prices.receivedAtNanos)/1000000).coerceAtLeast(0)>15000 -> "Последние цены · обновление задержалось"
            else -> "Из игры · обновляются каждые 5 секунд" },color=d.muted,fontSize=12.sp,lineHeight=16.sp)
        Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(6.dp)) {
            FilterChip(selected=selected==null,onClick={crop=null},label={Text("Все")})
            crops.forEach { offer -> FilterChip(selected=selected==offer.crop,onClick={crop=offer.crop},label={Text(offer.cropName.ifBlank { offer.crop })}) }
        }
        val offers=fs25Offers(prices,selected)
        if(offers.isEmpty()) Text(if(prices==null) "Запустите сохранение с обновлённым модом. Цены не подставляются из справочника." else "В этом сохранении пока нет предложений продажи.",color=d.muted)
        offers.forEachIndexed { index,offer ->
            Row(Modifier.fillMaxWidth().padding(vertical=9.dp),horizontalArrangement=Arrangement.spacedBy(12.dp)) {
                Column(Modifier.weight(1f)) { Text(offer.cropName.ifBlank { offer.crop },fontWeight=FontWeight.Bold);Text(offer.station,color=d.muted,fontSize=13.sp,lineHeight=17.sp) }
                Column { Text(offer.formatted.ifBlank { "%.0f".format(offer.pricePer1000)+" (валюта игры)" },fontWeight=FontWeight.Bold,color=d.accent);if(index==0 && selected!=null) Text("Лучшее предложение",color=d.muted,fontSize=11.sp) }
            }
            HorizontalDivider(color=d.line)
        }
    }
}
