package dev.simdeck

import androidx.activity.compose.BackHandler
import android.annotation.SuppressLint
import android.graphics.Color as AndroidColor
import android.view.MotionEvent
import android.webkit.JavascriptInterface
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebView
import android.webkit.WebViewClient
import android.speech.tts.TextToSpeech
import java.util.Locale
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.viewinterop.AndroidView
import org.json.JSONObject
import java.io.ByteArrayInputStream

/** Shared offline renderer. No token, direct network access, file access or TLS bypass in WebView. */
@SuppressLint("SetJavaScriptEnabled")
@Composable internal fun TruckNavigator(state: DeckState, model: DeckModel, availableHeight:Dp?=null) {
    val context=LocalContext.current
    val configuration=LocalConfiguration.current
    val height=availableHeight ?: if(configuration.screenWidthDp>=650 && configuration.screenHeightDp<750)
        (configuration.screenHeightDp-210).coerceIn(300,540).dp else 680.dp
    val profile=state.profileId
    val panelOpen=remember(profile){mutableStateOf(false)}
    val disposed=remember(profile){java.util.concurrent.atomic.AtomicBoolean(false)}
    val speech=remember(profile){TextToSpeech(context) {}}
    DisposableEffect(speech){onDispose{speech.stop();speech.shutdown()}}
    val web=remember(profile) {
        WebView(context).apply {
            WebView.setWebContentsDebuggingEnabled(BuildConfig.DEBUG)
            setBackgroundColor(AndroidColor.rgb(17,29,36))
            settings.javaScriptEnabled=true
            settings.domStorageEnabled=true
            settings.allowFileAccess=false;settings.allowContentAccess=false
            settings.blockNetworkLoads=true
            settings.setSupportZoom(false)
            webViewClient=object:WebViewClient() {
                override fun onPageFinished(view:WebView,url:String) { (view.tag as? String)?.let { view.evaluateJavascript("window.updateNavigator && window.updateNavigator($it)",null) } }
                override fun shouldOverrideUrlLoading(view:WebView,request:WebResourceRequest)=true
                override fun shouldInterceptRequest(view:WebView,request:WebResourceRequest):WebResourceResponse =
                    WebResourceResponse("text/plain","UTF-8",ByteArrayInputStream(ByteArray(0)))
            }
            addJavascriptInterface(object {
                @JavascriptInterface fun panel(open:Boolean) { post { panelOpen.value=open } }
                @JavascriptInterface fun voice(text:String) {
                    post { if(text.length<=250){speech.language=Locale("ru","RU");speech.speak(text,TextToSpeech.QUEUE_FLUSH,null,"simdeck-route") } }
                }
                @JavascriptInterface fun request(id:String,path:String,body:String) {
                    if(!id.matches(Regex("[0-9]{1,10}")))return
                    model.navigatorRequest(profile,path,body) { result,error ->
                        post { if(!disposed.get())evaluateJavascript("window.navReply && window.navReply(${JSONObject.quote(id)},${JSONObject.quote(result ?: "")},${JSONObject.quote(error ?: "")})",null) }
                    }
                }
            },"AndroidNavigator")
            setOnTouchListener { view,event ->
                if(event.actionMasked==MotionEvent.ACTION_DOWN)view.parent?.requestDisallowInterceptTouchEvent(true)
                if(event.actionMasked==MotionEvent.ACTION_UP||event.actionMasked==MotionEvent.ACTION_CANCEL)view.parent?.requestDisallowInterceptTouchEvent(false)
                false
            }
            val css=context.assets.open("navigator/truck-navigator.css").bufferedReader().use{it.readText()}
            val js=context.assets.open("navigator/truck-navigator.js").bufferedReader().use{it.readText()}
            val bridge="""
                const calls=new Map();let serial=0;
                window.navReply=(id,result,error)=>{const p=calls.get(id);if(!p)return;calls.delete(id);clearTimeout(p.timer);if(error)p.reject(Error(error));else{try{p.resolve(JSON.parse(result));}catch(e){p.reject(e);}}};
                const request=(path,body)=>new Promise((resolve,reject)=>{if(calls.size>=8){reject(Error('Карта занята'));return;}const id=String(++serial),timer=setTimeout(()=>{calls.delete(id);reject(Error('ПК не ответил: последний маршрут сохранён'));},18000);calls.set(id,{resolve,reject,timer});AndroidNavigator.request(id,path,body?JSON.stringify(body):'');});
                window.navigatorView=new SimDeckNavigator.Navigator(document.body,{profile:'$profile',request,panelChanged:open=>AndroidNavigator.panel(open),speak:text=>AndroidNavigator.voice(text)});
                window.updateNavigator=data=>window.navigatorView.update(data);
            """.trimIndent()
            val html="""<!doctype html><html lang="ru"><head><meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1"><style>$css html,body{margin:0;height:${height.value}px;background:#111d24}body>.sdnav{height:${height.value}px!important;min-height:0!important;border:0;border-radius:14px}</style></head><body><script>$js</script><script>$bridge</script></body></html>"""
            loadDataWithBaseURL("https://simdeck.invalid/",html,"text/html","UTF-8",null)
        }
    }
    BackHandler(panelOpen.value) { web.evaluateJavascript("window.navigatorView && window.navigatorView.closePanel()",null) }
    DisposableEffect(web) { onDispose { disposed.set(true);web.evaluateJavascript("window.navigatorView && window.navigatorView.destroy()",null);web.removeJavascriptInterface("AndroidNavigator");web.stopLoading();web.destroy() } }
    AndroidView(factory={web},modifier=Modifier.fillMaxWidth().height(height),update={view->
        val data=JSONObject();val t=state.telemetry
        if(t!=null) {
            data.put("fuelLiters",t.fuelLiters ?: JSONObject.NULL)
            data.put("gear",t.gear)
            data.put("speedMps",t.speedMps).put("fuelFraction",t.fuelFraction ?: JSONObject.NULL)
            val n=t.ets2Navigation
            if(n!=null)data.put("ets2Navigation",JSONObject().apply {
                put("worldX",n.worldX ?: JSONObject.NULL);put("worldZ",n.worldZ ?: JSONObject.NULL);put("heading",n.heading ?: JSONObject.NULL)
                put("remainingKm",n.remainingKm ?: JSONObject.NULL);put("remainingMinutes",n.remainingMinutes ?: JSONObject.NULL)
                put("speedLimitKmh",n.speedLimitKmh ?: JSONObject.NULL);put("scale",n.scale ?: JSONObject.NULL)
                put("restMinutes",n.restMinutes ?: JSONObject.NULL);put("gameMinutes",n.gameMinutes ?: JSONObject.NULL)
                put("destinationCity",n.destinationCity ?: JSONObject.NULL)
            })
        }
        val snapshot=JSONObject().put("data",data).put("connected",state.connected).put("stale",state.stale)
        view.tag=snapshot.toString()
        view.evaluateJavascript("if(window.navigatorView){const h=${height.value};if(window.navigatorView.root.style.height!==h+'px'){document.documentElement.style.height=h+'px';document.body.style.height=h+'px';window.navigatorView.root.style.setProperty('height',h+'px','important');}window.updateNavigator($snapshot);}",null)
    })
}
