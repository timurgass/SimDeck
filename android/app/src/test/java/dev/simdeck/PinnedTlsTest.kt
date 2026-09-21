package dev.simdeck

import okhttp3.Request
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.tls.HandshakeCertificates
import okhttp3.tls.HeldCertificate
import org.junit.Assert.*
import org.junit.Test

class PinnedTlsTest {
    @Test fun acceptsOnlyPinnedCertificate() {
        val cert = HeldCertificate.Builder().commonName("SimDeck Test").build()
        val server = MockWebServer()
        server.useHttps(HandshakeCertificates.Builder().heldCertificate(cert).build().sslSocketFactory(), false)
        server.enqueue(MockResponse().setBody("connected"))
        server.start()
        val client = PinnedTls.client(PinnedTls.fingerprint(cert.certificate))
        try { client.newCall(Request.Builder().url(server.url("/")).build()).execute().use { assertEquals("connected", it.body!!.string()) } }
        finally { server.shutdown(); client.connectionPool.evictAll(); client.dispatcher.executorService.shutdown() }
    }
    @Test fun rejectsSubstitutedCertificate() {
        val cert = HeldCertificate.Builder().commonName("Wrong PC").build()
        val server = MockWebServer()
        server.useHttps(HandshakeCertificates.Builder().heldCertificate(cert).build().sslSocketFactory(), false)
        server.start()
        val client = PinnedTls.client("00".repeat(32))
        try {
            var rejected = false
            try { client.newCall(Request.Builder().url(server.url("/")).build()).execute().close() }
            catch (_: javax.net.ssl.SSLHandshakeException) { rejected = true }
            assertTrue("Wrong companion certificate must fail TLS", rejected)
        } finally { server.shutdown(); client.connectionPool.evictAll(); client.dispatcher.executorService.shutdown() }
    }
}
