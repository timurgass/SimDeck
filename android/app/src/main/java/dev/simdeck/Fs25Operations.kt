package dev.simdeck

import org.json.JSONObject

data class Fs25FarmAccount(val id:Int,val name:String,val money:Double?,val loan:Double?)
data class Fs25FinanceItem(val category:String,val amount:Double)
data class Fs25FinanceRecord(val day:Int,val income:Double,val expenses:Double,val net:Double,val entries:List<Fs25FinanceItem>)
data class Fs25Ledger(val farmId:Int,val days:List<Fs25FinanceRecord>)
data class Fs25StockItem(val farmId:Int,val crop:String,val litres:Double,val location:String,val source:String)
data class Fs25SavedVehicle(val farmId:Int,val model:String,val property:String,val hours:Double?,val damage:Double?)
data class Fs25SavedProduction(val farmId:Int,val building:String,val recipe:String,val enabled:Boolean?)
data class Fs25OperationsData(val ledgers:List<Fs25Ledger>,val stocks:List<Fs25StockItem>,val fleet:List<Fs25SavedVehicle>,val productions:List<Fs25SavedProduction>,val storageAvailable:Boolean,val fleetAvailable:Boolean,val truncated:Boolean=false) {
    companion object {
        fun parse(o:JSONObject?):Fs25OperationsData? {
            if(o==null)return null
            fun rows(p:JSONObject,key:String,max:Int):List<JSONObject>? {val a=p.optJSONArray(key)?:return null;if(a.length()>max)return null;return (0 until a.length()).map{a.optJSONObject(it)?:return null}}
            fun n(p:JSONObject,key:String,min:Double=0.0,max:Double=1e14)= (p.opt(key) as? Number)?.toDouble()?.takeIf{it.isFinite()&&it in min..max}
            fun s(p:JSONObject,key:String)=p.optString(key).take(160)
            val ledgers=(rows(o,"ledgers",64)?:return null).map{p->Fs25Ledger(p.optInt("farmId"),(rows(p,"days",31)?:return null).map{d->
                Fs25FinanceRecord(d.optInt("day"),n(d,"income")?:return null,n(d,"expenses")?:return null,n(d,"net",-1e14)?:return null,(rows(d,"entries",128)?:return null).map{e->Fs25FinanceItem(s(e,"category"),n(e,"amount",-1e14)?:return null)})})}
            val stocks=(rows(o,"stocks",2048)?:return null).map{p->Fs25StockItem(p.optInt("farmId"),s(p,"crop"),n(p,"litres",max=1e10)?:return null,s(p,"location"),s(p,"source"))}
            val fleet=(rows(o,"fleet",2048)?:return null).map{p->Fs25SavedVehicle(p.optInt("farmId"),s(p,"model"),s(p,"property"),n(p,"hours"),n(p,"damage",max=100.0))}
            val productions=(rows(o,"productions",1024)?:return null).map{p->Fs25SavedProduction(p.optInt("farmId"),s(p,"building"),s(p,"recipe"),p.opt("enabled") as? Boolean)}
            return Fs25OperationsData(ledgers,stocks,fleet,productions,o.optBoolean("storageAvailable"),o.optBoolean("fleetAvailable"),o.optBoolean("truncated"))
        }
    }
}
