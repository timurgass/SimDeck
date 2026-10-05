package dev.simdeck

import android.app.Application
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.os.SystemClock
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.security.MessageDigest
import java.security.SecureRandom
import java.security.cert.X509Certificate
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.TimeUnit
import javax.net.ssl.*

data class Computer(val name: String, val host: String, val port: Int, val fingerprint: String)
data class DeckState(
    val status: String = "Найдите Companion в вашей сети", val connected: Boolean = false,
    val computers: List<Computer> = emptyList(), val selected: Computer? = null,
    val telemetry: Telemetry? = null, val lastVehicle: VehicleInfo? = null, val stale: Boolean = true, val demo: Boolean = false,
    val command: String = "", val busy: Boolean = false, val inputAvailability: String = "unknown",
    val ignitionReady: Boolean = false, val controls: List<DeckAction> = emptyList(),
    val profileName: String = "SimDeck", val profileId: String = "beamng-default",
    val pitCursor: Int? = null, val menuBusy: Boolean = false,
    val pitTyre: Int? = null, val pitRepair: Int? = null, val pitSent: Boolean = false,
    val reconnectCount: Int = 0, val lastDisconnect: String = "",
    val etsMap:EtsMap?=null,val etsMapStatus:String="Подготовка карты ETS2…"
)

// Receiving game telemetry and sending keyboard commands are independent capabilities.
internal fun controlsAvailable(state: DeckState) =
    state.connected && !state.demo && state.inputAvailability == "ready" && !state.menuBusy

class DeckModel(app: Application) : AndroidViewModel(app) {
    private val mutable = MutableStateFlow(DeckState())
    val state = mutable.asStateFlow()
    private val prefs = app.getSharedPreferences("connection", 0)
    private val vault = TokenVault(app)
    private val nsd = app.getSystemService(NsdManager::class.java)
    private var discovery: NsdManager.DiscoveryListener? = null
    private var client: OkHttpClient? = null
    private var socket: WebSocket? = null
    @Volatile private var session: String? = null
    @Volatile private var profileRevision = 1
    @Volatile private var profileId = "beamng-default"
    @Volatile private var generation = 0
    @Volatile private var active = false
    @Volatile private var lastFrame = 0L
    @Volatile private var sourceAge = Long.MAX_VALUE
    @Volatile private var lastMessage = 0L
    private var retry: Job? = null
    private var backgroundClose: Job? = null
    private val presses = ConcurrentHashMap<String, String>()
    private val acknowledgements = ConcurrentHashMap<String, CompletableDeferred<Boolean>>()
    private var menuSequence: Job? = null
    private var mapRequest:Job?=null
    private var mapAttempt=0L
    private var mapLoadedAt=0L
    private val navigatorCalls=java.util.concurrent.atomic.AtomicInteger()
    internal fun navigatorRequest(requestedProfile:String,path:String,body:String,complete:(String?,String?)->Unit) {
        if(path.length>2000 || body.length>12000 || !path.matches(Regex("/(ets2-map|truck-nav/(places|route|landscape|game-route))\\?.*")) ||
            !isScsTruck(requestedProfile) || requestedProfile!=profileId ||
            (body.isNotEmpty() != path.startsWith("/truck-nav/route?"))) { complete(null,"Недопустимый запрос навигатора");return }
        val pc=state.value.selected;val http=client;val token=vault.read();val current=generation;val requestedSession=session
        if(pc==null || http==null || token==null || requestedSession==null) {complete(null,"Нет соединения с ПК");return}
        if(navigatorCalls.incrementAndGet()>6){navigatorCalls.decrementAndGet();complete(null,"Карта занята");return}
        viewModelScope.launch {
            try {
                val result=withContext(Dispatchers.IO) {
                    val builder=Request.Builder().url("https://${pc.host}:${pc.port}$path").header("Authorization","Bearer $token")
                    if(body.isNotEmpty())builder.post(body.toRequestBody("application/json".toMediaType()))
                    val call=http.newCall(builder.build());call.timeout().timeout(15,TimeUnit.SECONDS)
                    call.execute().use { response->
                        require((response.body?.contentLength() ?: 0)<=12000000)
                        val text=response.body?.string() ?: error("Пустой ответ карты");require(text.length<=12000000)
                        if(!response.isSuccessful)error(runCatching{JSONObject(text).optString("error")}.getOrNull()?.takeIf{it.isNotBlank()} ?: "Карта недоступна (${response.code})")
                        text
                    }
                }
                if(current==generation && requestedProfile==profileId && requestedSession==session)complete(result,null)
                else complete(null,"Подключение изменилось")
            } catch(e:CancellationException){throw e}
            catch(e:Exception){complete(null,e.message?.take(180) ?: "Карта пока недоступна")}
            finally {navigatorCalls.decrementAndGet()}
        }
    }
    private fun updateEtsMap(n:Ets2Navigation?,current:Int) {
        if(!isScsTruck(profileId) || n?.worldX==null || n.worldZ==null || mapRequest?.isActive==true) return
        val now=SystemClock.elapsedRealtime();if(now-mapAttempt<2500) return
        val old=state.value.etsMap
        if(old!=null && now-mapLoadedAt<60000 && kotlin.math.abs(old.x-n.worldX)<old.span/4 && kotlin.math.abs(old.z-n.worldZ)<old.span/4) return
        val pc=state.value.selected ?: return;val http=client ?: return;val token=vault.read() ?: return
        val requestedProfile=profileId
        val requestedSession=session
        mapAttempt=now
        mapRequest=viewModelScope.launch {
            try {
                val map=withContext(Dispatchers.IO) {
                    val url="https://${pc.host}:${pc.port}/ets2-map?x=${n.worldX}&z=${n.worldZ}&span=6400"
                    val call=http.newCall(Request.Builder().url(url).header("Authorization","Bearer $token").build())
                    call.timeout().timeout(10,TimeUnit.SECONDS)
                    call.execute().use { response->
                        if(!response.isSuccessful) error(if(response.code==503) "Карта готовится на ПК…" else "Карта пока недоступна")
                        require((response.body?.contentLength() ?: 0)<=5000000)
                        val text=response.body!!.string();require(text.length<=5000000)
                        EtsMap.parse(JSONObject(text))
                    }
                }
                if(current==generation && profileId==requestedProfile && session==requestedSession) { mapLoadedAt=SystemClock.elapsedRealtime();mutable.update { it.copy(etsMap=map,etsMapStatus="Карта готова") } }
            } catch(e:CancellationException) { throw e }
            catch(e:Exception) { if(current==generation && profileId==requestedProfile && session==requestedSession)mutable.update { it.copy(etsMapStatus=e.message?.take(100) ?: "Карта пока недоступна") } }
        }
    }
    init {
        if (prefs.contains("host")) mutable.update { it.copy(selected = Computer(prefs.getString("name", "Companion")!!, prefs.getString("host", "")!!, prefs.getInt("port", 9443), prefs.getString("fingerprint", "")!!)) }
        viewModelScope.launch {
            while (isActive) {
                delay(100)
                val now = SystemClock.elapsedRealtime()
                val expired = lastFrame == 0L || !Protocol.telemetryFresh(profileId, sourceAge, now - lastFrame)
                mutable.update { val s=it.copy(stale = expired)
                    if(s.connected && s.inputAvailability=="ready" && (s.stale || s.telemetry?.f1?.mfdPanelIndex==1)) s
                    else s.copy(pitCursor=null,pitTyre=null,pitRepair=null,pitSent=false)
                }
                if (session != null) {
                    if (presses.isNotEmpty()) send("input.renew") { put("pressIds", JSONArray(presses.keys.toList())) }
                    if (now - lastMessage > 20000) {
                        failed(generation,"Companion не ответил за 20 с",true)
                    }
                }
            }
        }
    }
    fun start() {
        backgroundClose?.cancel()
        backgroundClose = null
        active = true
        discover()
        if (state.value.selected != null && vault.read() != null && session == null && socket == null) connect()
    }
    fun pause() {
        // Android briefly stops the Activity for system overlays and task switching. Give it
        // time to resume without tearing down TLS, losing the controller slot and flashing
        // the disconnected state on the dashboard.
        backgroundClose?.cancel()
        backgroundClose = viewModelScope.launch {
            delay(30_000)
            active = false
            retry?.cancel()
            disconnect()
            stopDiscovery()
        }
    }
    fun select(computer: Computer) {
        disconnect()
        mutable.update { it.copy(selected = computer, status = "Сравните отпечаток с Companion", command = "") }
    }
    fun savedTransport(usb: Boolean) {
        val pin=prefs.getString("fingerprint",null)
        if(pin.isNullOrBlank() || vault.read()==null) {
            mutable.update { it.copy(status="Сначала свяжите планшет с этим ПК по коду") }
            return
        }
        val old=prefs.getString("host","").orEmpty()
        val host=if(usb) "127.0.0.1" else prefs.getString("wifiHost",null)?.takeIf { it.isNotBlank() }
            ?: old.takeIf { it!="127.0.0.1" && it.isNotBlank() }
        if(host==null) { mutable.update { it.copy(status="Найдите Companion в сети для подключения по Wi-Fi") };return }
        val editor=prefs.edit().putString("host",host)
        if(usb && old.isNotBlank() && old!="127.0.0.1")editor.putString("wifiHost",old)
        editor.apply()
        val pc=Computer(if(usb) "Companion · USB" else "Companion · Wi-Fi",host,prefs.getInt("port",9443),pin)
        disconnect()
        mutable.update { it.copy(selected=pc,command="") }
        connect()
    }
    fun pair(code: String) {
        val computer = state.value.selected ?: return
        mutable.update { it.copy(busy = true, status = "Подключение…") }
        viewModelScope.launch {
            try {
                val token = withContext(Dispatchers.IO) {
                    val http = PinnedTls.client(computer.fingerprint)
                    try {
                        val body = JSONObject().put("code", code).put("name", android.os.Build.MODEL).toString().toRequestBody("application/json".toMediaType())
                        http.newCall(Request.Builder().url("https://${computer.host}:${computer.port}/pair").post(body).build()).execute().use {
                            if (!it.isSuccessful) error("Код неверен или истёк. Откройте новое подключение на ПК.")
                            JSONObject(it.body!!.string()).getString("token")
                        }
                    } finally { http.connectionPool.evictAll(); http.dispatcher.executorService.shutdown() }
                }
                if (!active || state.value.selected != computer) return@launch
                vault.save(token)
                prefs.edit().putString("name", computer.name).putString("host", computer.host).putInt("port", computer.port).putString("fingerprint", computer.fingerprint).apply()
                connect()
            } catch (e: Exception) { mutable.update { it.copy(status = e.message ?: "Ошибка подключения") } }
            finally { mutable.update { it.copy(busy = false) } }
        }
    }
    fun connect() {
        val computer = state.value.selected ?: return
        if (prefs.getString("fingerprint", null) != computer.fingerprint) {
            mutable.update { it.copy(status = "Для этого ПК нужен код из Companion") }
            return
        }
        val token = vault.read() ?: return
        disconnect()
        val current = generation
        mutable.update { it.copy(status = "Соединение с ${computer.name}…") }
        try {
            val http = PinnedTls.client(computer.fingerprint)
            client = http
            socket = http.newWebSocket(Request.Builder().url("wss://${computer.host}:${computer.port}/ws").header("Authorization", "Bearer $token").build(), object : WebSocketListener() {
                override fun onMessage(webSocket: WebSocket, text: String) {
                    if (current != generation) return
                    try {
                        // Large FS25 farms can exceed the old 64 KiB telemetry limit.
                        if (text.length > 1_048_576) error("Слишком большое сообщение")
                        val root = JSONObject(text)
                        require(root.getInt("protocolMajor") == 1) { "Несовместимая версия Companion" }
                        lastMessage = SystemClock.elapsedRealtime()
                        when (root.getString("type")) {
                            "hello" -> {
                                if(profileId != root.getString("profileId")) {
                                    mapRequest?.cancel();mapRequest=null;mapAttempt=0;mapLoadedAt=0
                                    mutable.update { it.copy(etsMap=null,etsMapStatus="Подготовка карты игры…") }
                                }
                                session = root.getString("sessionId")
                                profileRevision = root.getInt("profileRevision")
                                profileId = root.getString("profileId")
                                lastMessage = SystemClock.elapsedRealtime()
                                val controls = Protocol.controls(root)
                                mutable.update { it.copy(connected = true, status = "${computer.name} · подключено", controls = controls, profileId = profileId, profileName = root.getString("profileName"), telemetry = it.telemetry.takeIf { _ -> it.profileId == profileId }, lastVehicle = it.lastVehicle.takeIf { _ -> it.profileId == profileId }, stale = true, ignitionReady = false, command = "") }
                            }
                            "telemetry.snapshot" -> {
                                require(root.getString("sessionId") == session)
                                lastFrame = SystemClock.elapsedRealtime()
                                sourceAge = if (root.isNull("ageMs")) Long.MAX_VALUE else root.getLong("ageMs")
                                val telemetry = Protocol.telemetry(root)
                                mutable.update { it.copy(telemetry = telemetry ?: it.telemetry, lastVehicle = telemetry?.vehicle ?: it.lastVehicle,
                                    stale = !Protocol.telemetryFresh(profileId,sourceAge,0), demo = root.optString("source") == "demo") }
                                updateEtsMap(telemetry?.ets2Navigation,current)
                            }
                            "input.state" -> {
                                require(root.getString("sessionId") == session)
                                mutable.update { it.copy(ignitionReady = root.getBoolean("ignitionReady"), inputAvailability = root.optString("availability", "unknown")) }
                            }
                            "control.ack" -> {
                                acknowledgements.remove(root.optString("commandId"))?.complete(root.optBoolean("success") && root.optString("code") == "injected")
                                mutable.update { it.copy(command = when(root.optString("code")) {
                                "injected" -> "Команда отправлена в Windows"
                                "released" -> "Кнопка отпущена"
                                "game_not_focused_or_input_disabled" -> "Включите ввод в Companion и откройте окно игры"
                                "injection_failed" -> "Windows заблокировала ввод: запустите Companion с теми же правами, что игру"
                                "input_fault_restart_required" -> "Перезапустите Companion после ошибки ввода"
                                "ignition_tap_required" -> "Сначала коротко нажмите IGNITION, затем удерживайте"
                                "ignition_busy" -> "Дождитесь завершения предыдущего нажатия"
                                "input_busy" -> "Отпустите другую кнопку и повторите"
                                "profile_mismatch" -> "Переподключитесь к обновлённому Companion"
                                else -> root.optString("code")
                            }) }
                            }
                        }
                    } catch (e: Exception) { webSocket.cancel(); failed(current, e.message ?: "Ошибка протокола", false) }
                }
                override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                    if (current == generation) android.util.Log.w("SimDeckConnection", "${t.javaClass.simpleName}: ${t.message}; incomingAgeMs=${SystemClock.elapsedRealtime()-lastMessage}; pendingBytes=${webSocket.queueSize()}")
                    val revoked = response?.code == 401
                    val occupied = response?.code == 409
                    if (current == generation && revoked) vault.clear()
                    failed(current, when {
                        revoked -> "Доступ отозван. Свяжите устройства снова."
                        occupied -> "Другой телефон или Safari уже управляет SimDeck. Закройте его и нажмите «Подключиться»."
                        else -> t.message ?: "Соединение потеряно"
                    }, !revoked && !occupied)
                }
                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) { webSocket.close(code, null); failed(current, "Соединение закрыто", true) }
            })
        } catch (e: Exception) { failed(current, e.message ?: "Ошибка TLS", false) }
    }
    private fun failed(current: Int, message: String, reconnect: Boolean) {
        if (current != generation) return
        viewModelScope.launch {
            if (current != generation) return@launch
            disconnect()
            android.util.Log.w("SimDeckConnection", message)
            mutable.update { it.copy(status = message + if(reconnect) " · переподключение…" else "", reconnectCount=it.reconnectCount+1,lastDisconnect=message) }
            if (reconnect && active) retry = viewModelScope.launch { delay(2500); if (active) connect() }
        }
    }
    fun disconnect() {
        mapRequest?.cancel();mapRequest=null;mapAttempt=0;mapLoadedAt=0
        releaseAll(); generation++; session = null; lastFrame = 0; sourceAge = Long.MAX_VALUE
        socket?.cancel(); socket = null
        client?.connectionPool?.evictAll(); client?.dispatcher?.executorService?.shutdown(); client = null
        mutable.update { it.copy(connected = false, stale = true, ignitionReady = false, inputAvailability = "unknown") }
    }
    private fun send(type: String, fill: JSONObject.() -> Unit = {}) {
        val currentSession = session ?: return
        val root = JSONObject().put("protocolMajor", 1).put("sessionId", currentSession).put("type", type).apply(fill)
        socket?.send(root.toString())
    }
    fun press(action: String, hold: Boolean): String? {
        if (menuSequence?.isActive == true) return null
        if (!active || session == null || !controlsAvailable(state.value)) return null
        mutable.update { it.copy(pitCursor=null,pitTyre=null,pitRepair=null,pitSent=false) }
        val id = UUID.randomUUID().toString()
        if (hold) presses[id] = action
        invoke(action, if (hold) "down" else "press", id)
        return id
    }
    private fun invoke(action: String, phase: String, id: String) = send("control.invoke") {
        put("commandId", UUID.randomUUID().toString()); put("profileId", profileId); put("profileRevision", profileRevision)
        put("actionId", action); put("phase", phase); put("pressId", id)
    }
    fun release(id: String?) { if (id != null) presses.remove(id)?.let { invoke(it, "up", id) } }
    fun releaseAll() { menuSequence?.cancel(); acknowledgements.values.forEach { it.cancel() }; acknowledgements.clear(); presses.clear(); mutable.update { it.copy(ignitionReady = false, pitCursor=null,pitTyre=null,pitRepair=null,pitSent=false, menuBusy=false) }; send("input.releaseAll") }
    fun syncPit(row: Int, tyre: Int, repair: Int) {
        if(row in 0..2 && tyre in 0..4 && repair in 0..2 && pitReady(state.value) && menuSequence?.isActive!=true)
            mutable.update { it.copy(pitCursor=row,pitTyre=tyre,pitRepair=repair,pitSent=false, command="Исходные значения записаны с вашего выбора. При изменении MFD геймпадом сверяйте их заново.") }
    }
    private suspend fun acknowledgedPress(action: String): Boolean {
        val commandId=UUID.randomUUID().toString(); val ack=CompletableDeferred<Boolean>(); acknowledgements[commandId]=ack
        return try {
            send("control.invoke") {
                put("commandId",commandId); put("profileId",profileId); put("profileRevision",profileRevision)
                put("actionId",action); put("phase","press"); put("pressId",UUID.randomUUID().toString())
            }
            withTimeoutOrNull(1500) { ack.await() } == true
        } finally { acknowledgements.remove(commandId) }
    }
    fun pitAdjust(row: Int, increase: Boolean) {
        val from=state.value.pitCursor ?: return
        if(row!=0) return
        runPitSteps(row,pitAdjustmentSteps(from,row,increase))
    }
    fun pitSelect(row: Int, value: Int) {
        val s=state.value; val from=s.pitCursor ?: return
        val current=when(row) { 1 -> s.pitRepair; 2 -> s.pitTyre; else -> null } ?: return
        val count=if(row==1) 3 else 5
        if(value !in 0 until count) return
        runPitSteps(row,pitSelectionSteps(from,row,current,value,count),value)
    }
    private fun runPitSteps(row: Int, steps: List<String>, selected: Int? = null) {
        if(!pitReady(state.value) || menuSequence?.isActive==true) return
        val expected=session; val expectedGeneration=generation
        mutable.update { it.copy(menuBusy=true) }
        menuSequence=viewModelScope.launch {
            var ok=false
            try {
                ok=true
                for(action in steps) {
                    if(!active || session!=expected || generation!=expectedGeneration || !pitReady(state.value) || !acknowledgedPress(action)) { ok=false; break }
                    delay(320)
                }
                if(ok && !pitReady(state.value)) ok=false
            } catch(e: CancellationException) {
                ok=false; throw e
            } finally {
                mutable.update { it.copy(menuBusy=false,pitCursor=if(ok) row else null,
                    pitTyre=if(!ok) null else if(row==2) selected else it.pitTyre,
                    pitRepair=if(!ok) null else if(row==1) selected else it.pitRepair,pitSent=ok,
                    command=if(ok) "Выбор отправлен. Шины и ремонт проверьте в MFD; крыло подтверждается телеметрией." else "Остановлено: заново откройте пит-меню и сверяйте исходные значения") }
            }
        }
    }
    fun engineerRequest(row: Int) {
        val expectedType=state.value.telemetry?.f1?.race?.sessionType
        val requests=engineerRequests(expectedType)
        if (menuSequence?.isActive == true || row !in requests.indices) return
        val expectedSession=session; val expectedGeneration=generation
        fun allowed(): Boolean = active && expectedSession!=null && session==expectedSession && generation==expectedGeneration &&
            state.value.connected && !state.value.demo && !state.value.stale && state.value.inputAvailability=="ready" &&
            state.value.profileId in setOf("f1-24","f1-25") && state.value.telemetry?.f1?.race?.sessionType==expectedType &&
            state.value.telemetry?.f1?.race?.let { it.fresh && it.drivers.any { d -> d.player && d.onTrack } } == true
        if(!allowed()) { mutable.update { current -> current.copy(command="Запрос доступен на трассе в тренировке или гонке ${gameDisplayName(current.profileId, current.profileName)}") }; return }
        mutable.update { it.copy(menuBusy=true) }
        menuSequence=viewModelScope.launch {
            try {
            val ok=runMenuSequence(engineerSequence(row,requests), ::allowed, { action ->
                val commandId=UUID.randomUUID().toString(); val ack=CompletableDeferred<Boolean>(); acknowledgements[commandId]=ack
                try {
                    send("control.invoke") {
                        put("commandId",commandId); put("profileId",profileId); put("profileRevision",profileRevision)
                        put("actionId",action); put("phase","press"); put("pressId",UUID.randomUUID().toString())
                    }
                    withTimeoutOrNull(1500) { ack.await() } == true
                } finally { acknowledgements.remove(commandId) }
            }, { delay(it) })
            mutable.update { it.copy(command=if(ok) "Отправлены клавиши: ${requests[row]}. Проверьте ответ в игре." else "Запрос остановлен: проверьте ввод и радиоменю игры") }
            } finally { mutable.update { it.copy(menuBusy=false) } }
        }
    }
    fun ignitionNeedsTap() { mutable.update { it.copy(command = "Сначала коротко нажмите IGNITION, затем удерживайте") } }
    @Suppress("DEPRECATION")
    fun discover() {
        if (discovery != null) return
        val listener = object : NsdManager.DiscoveryListener {
            override fun onDiscoveryStarted(type: String) {}
            override fun onDiscoveryStopped(type: String) {}
            override fun onStartDiscoveryFailed(type: String, error: Int) { mutable.update { it.copy(status = "Автопоиск недоступен ($error). Проверьте Wi-Fi.") }; stopDiscovery() }
            override fun onStopDiscoveryFailed(type: String, error: Int) {}
            override fun onServiceLost(info: NsdServiceInfo) { mutable.update { it.copy(computers = it.computers.filter { pc -> pc.name != info.serviceName }) } }
            override fun onServiceFound(info: NsdServiceInfo) {
                nsd.resolveService(info, object : NsdManager.ResolveListener {
                    override fun onResolveFailed(service: NsdServiceInfo, error: Int) {}
                    override fun onServiceResolved(service: NsdServiceInfo) {
                        val host = service.host?.hostAddress ?: return
                        val fingerprint = service.attributes["fingerprint"]?.toString(Charsets.UTF_8) ?: return
                        val computer = Computer(service.serviceName, host, service.port, fingerprint)
                        mutable.update { state ->
                            val previous = state.computers.firstOrNull { it.name == computer.name }
                            val preferred = DiscoveryAddress.prefer(previous, computer)
                            val selected = state.selected
                            val samePc = selected?.fingerprint == computer.fingerprint && selected.host != "127.0.0.1"
                            if (samePc && selected.host != preferred.host) {
                                prefs.edit().putString("host", preferred.host).putInt("port", preferred.port).apply()
                            }
                            state.copy(
                                computers = (state.computers.filter { it.name != computer.name } + preferred).sortedBy { it.name },
                                selected = if (samePc) preferred else selected
                            )
                        }
                    }
                })
            }
        }
        discovery = listener
        runCatching { nsd.discoverServices("_simdeck._tcp.", NsdManager.PROTOCOL_DNS_SD, listener) }.onFailure { discovery = null }
    }
    private fun stopDiscovery() { discovery?.let { runCatching { nsd.stopServiceDiscovery(it) } }; discovery = null }
    override fun onCleared() { backgroundClose?.cancel(); active = false; retry?.cancel(); disconnect(); stopDiscovery(); super.onCleared() }
}
