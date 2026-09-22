package dev.simdeck

import okhttp3.OkHttpClient
import java.security.MessageDigest
import java.security.SecureRandom
import java.security.cert.X509Certificate
import java.util.concurrent.TimeUnit
import javax.net.ssl.*

object PinnedTls {
    fun fingerprint(cert: java.security.cert.Certificate) = MessageDigest.getInstance("SHA-256").digest(cert.encoded).joinToString("") { "%02x".format(it) }
    fun client(expectedFingerprint: String): OkHttpClient {
        require(expectedFingerprint.matches(Regex("[0-9a-fA-F]{64}"))) { "Неверный отпечаток сертификата" }
        val pin = expectedFingerprint.lowercase()
        val trust = object : X509TrustManager {
            override fun getAcceptedIssuers(): Array<X509Certificate> = emptyArray()
            override fun checkClientTrusted(chain: Array<X509Certificate>, authType: String) { throw java.security.cert.CertificateException("Client certificates not supported") }
            override fun checkServerTrusted(chain: Array<X509Certificate>, authType: String) {
                val cert = chain.firstOrNull() ?: throw java.security.cert.CertificateException("Empty chain")
                cert.checkValidity()
                if (fingerprint(cert) != pin) throw java.security.cert.CertificateException("Companion certificate changed")
            }
        }
        val ssl = SSLContext.getInstance("TLS").apply { init(null, arrayOf(trust), SecureRandom()) }
        // Discovery names and LAN addresses are not identities. The entire certificate is pinned
        // after explicit comparison on the PC. Both TLS trust and hostname verification enforce it.
        val verifier = HostnameVerifier { _, tls -> runCatching { fingerprint(tls.peerCertificates[0]) == pin }.getOrDefault(false) }
        return OkHttpClient.Builder().proxy(java.net.Proxy.NO_PROXY).sslSocketFactory(ssl.socketFactory, trust).hostnameVerifier(verifier)
            // DeckModel watches incoming snapshots (10 s), while input leases expire in
            // 500 ms. A second 2 s ping deadline caused reconnects under tablet/PC load.
            .connectTimeout(5, TimeUnit.SECONDS).readTimeout(0, TimeUnit.SECONDS).pingInterval(0, TimeUnit.SECONDS).build()
    }
}

