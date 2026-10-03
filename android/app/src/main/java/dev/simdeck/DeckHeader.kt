package dev.simdeck

import androidx.compose.foundation.background
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

@Composable internal fun DeckHeader(state:DeckState,tabs:List<Pair<String,String>>,selected:String,navigate:(String)->Unit,connection:()->Unit) {
    val d=LocalProfileDesign.current
    val brand:@Composable ()->Unit={Column {Row {Text("SIM",fontSize=24.sp,lineHeight=28.sp,fontWeight=FontWeight.Black);Text("DECK",fontSize=24.sp,lineHeight=28.sp,fontWeight=FontWeight.Black,color=d.accent)};Text("ПРОФИЛЬ: ${profileShortName(state.profileId)}",fontSize=11.sp,lineHeight=15.sp,color=d.muted)} }
    val nav:@Composable (Modifier)->Unit={modifier->Row(modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(3.dp)) {tabs.forEach {(id,label)->Column(Modifier.width(IntrinsicSize.Max)) {TextButton(onClick={navigate(id)}) {Text(label,fontSize=13.sp,lineHeight=17.sp,fontWeight=FontWeight.Bold,color=if(selected==id)d.accent else d.muted)};Box(Modifier.fillMaxWidth().height(3.dp).background(if(selected==id)d.accent else d.line))} }} }
    BoxWithConstraints {
        if(maxWidth>=800.dp) Row(Modifier.fillMaxWidth(),verticalAlignment=Alignment.CenterVertically,horizontalArrangement=Arrangement.spacedBy(20.dp)) {brand();nav(Modifier.weight(1f));TextButton(onClick=connection) {Text("СВЯЗЬ",fontSize=11.sp,lineHeight=15.sp)}}
        else Column(verticalArrangement=Arrangement.spacedBy(10.dp)) {Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween,verticalAlignment=Alignment.CenterVertically) {brand();TextButton(onClick=connection){Text("СВЯЗЬ",fontSize=11.sp,lineHeight=15.sp)}};nav(Modifier.fillMaxWidth())}
    }
}
