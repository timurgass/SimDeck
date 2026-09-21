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
    val telemetry: Telemetry? = null, val stale: Boolean = true, val demo: Boolean = false,
    val command: String = "", val busy: Boolean = false, val inputAvailability: String = "unknown",
    val ignitionReady: Boolean = false, val controls: List<DeckAction> = emptyList(),
    val profileName: String = "SimDeck", val profileId: String = "beamng-default",
    val pitCursor: Int? = null, val menuBusy: Boolean = false,
    val pitTyre: Int? = null, val pitRepair: Int? = null, val pitSent: Boolean = false,
    val reconnectCount: Int = 0, val lastDisconnect: String = ""
)

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
    private val presses = ConcurrentHashMap<String, String>()
    private val acknowledgements = ConcurrentHashMap<String, CompletableDeferred<Boolean>>()
    private var menuSequence: Job? = null
    init {
        if (prefs.contains("host")) mutable.update { it.copy(selected = Computer(prefs.getString("name", "Companion")!!, prefs.getString("host", "")!!, prefs.getInt("port", 9443), prefs.getString("fingerprint", "")!!)) }
        viewModelScope.launch {
            while (isActive) {
                delay(100)
                val now = SystemClock.elapsedRealtime()
                val expired = lastFrame == 0L || sourceAge >= 500 || now - lastFrame + sourceAge >= 500
                mutable.update { val s=it.copy(stale = expired)
                    if(s.connected && s.inputAvailability=="ready" && (s.stale || s.telemetry?.f1?.mfdPanelIndex==1)) s
                    else s.copy(pitCursor=null,pitTyre=null,pitRepair=null,pitSent=false)
                }
                if (session != null) {
                    send("input.renew") { put("pressIds", JSONArray(presses.keys.toList())) }
                    if (now - lastMessage > 10000) {
                        failed(generation,"Companion не ответил за 10 с",true)
                    }
                }
            }
        }
    }
    fun start() {
        active = true
        discover()
        if (state.value.selected != null && vault.read() != null && session == null && socket == null) connect()
    }
    fun pause() { active = false; retry?.cancel(); disconnect(); stopDiscovery() }
    fun select(computer: Computer) {
        disconnect()
        if (state.value.selected?.fingerprint != computer.fingerprint) vault.clear()
        mutable.update { it.copy(selected = computer, status = "Сравните отпечаток с Companion", command = "") }
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
                        if (text.length > 65536) error("Слишком большое сообщение")
                        val root = JSONObject(text)
                        require(root.getInt("protocolMajor") == 1) { "Несовместимая версия Companion" }
                        lastMessage = SystemClock.elapsedRealtime()
                        when (root.getString("type")) {
                            "hello" -> {
                                session = root.getString("sessionId")
                                profileRevision = root.getInt("profileRevision")
                                profileId = root.getString("profileId")
                                val controls = Protocol.controls(root)
                                mutable.update { it.copy(connected = true, status = "${computer.name} · подключено", controls = controls, profileId = profileId, profileName = root.getString("profileName"), telemetry = null, stale = true, ignitionReady = false, command = "") }
                            }
                            "telemetry.snapshot" -> {
                                require(root.getString("sessionId") == session)
                                lastFrame = SystemClock.elapsedRealtime()
                                sourceAge = if (root.isNull("ageMs")) Long.MAX_VALUE else root.getLong("ageMs")
                                mutable.update { it.copy(telemetry = Protocol.telemetry(root), demo = root.optString("source") == "demo") }
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
                    if (current == generation && revoked) vault.clear()
                    failed(current, if (revoked) "Доступ отозван. Свяжите устройства снова." else t.message ?: "Соединение потеряно", !revoked)
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
        releaseAll(); generation++; session = null; lastFrame = 0; sourceAge = Long.MAX_VALUE
        socket?.cancel(); socket = null
        client?.connectionPool?.evictAll(); client?.dispatcher?.executorService?.shutdown(); client = null
        mutable.update { it.copy(connected = false, stale = true, telemetry = null, ignitionReady = false, inputAvailability = "unknown") }
    }
    private fun send(type: String, fill: JSONObject.() -> Unit = {}) {
        val currentSession = session ?: return
        val root = JSONObject().put("protocolMajor", 1).put("sessionId", currentSession).put("type", type).apply(fill)
        socket?.send(root.toString())
    }
    fun press(action: String, hold: Boolean): String? {
        if (menuSequence?.isActive == true) return null
        if (!active || session == null || state.value.demo) return null
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
                        mutable.update { it.copy(computers = (it.computers.filter { pc -> pc.name != computer.name } + computer).sortedBy { pc -> pc.name }) }
                    }
                })
            }
        }
        discovery = listener
        runCatching { nsd.discoverServices("_simdeck._tcp.", NsdManager.PROTOCOL_DNS_SD, listener) }.onFailure { discovery = null }
    }
    private fun stopDiscovery() { discovery?.let { runCatching { nsd.stopServiceDiscovery(it) } }; discovery = null }
    override fun onCleared() { pause(); super.onCleared() }
}

