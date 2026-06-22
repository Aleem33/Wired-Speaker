package com.cablespeaker.android

import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioManager
import android.media.AudioTrack
import java.io.IOException
import java.net.InetSocketAddress
import java.net.Socket
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.concurrent.thread
import kotlin.math.max

class AudioStreamClient(
    private val latencyMode: LatencyMode,
    private val onState: (ClientState) -> Unit,
) {
    private val running = AtomicBoolean(false)
    private val jitterBuffer = PcmJitterBuffer(latencyMode.bufferMs)
    private val silenceFrame = ByteArray(Protocol.FRAME_PAYLOAD_BYTES)

    @Volatile private var socket: Socket? = null
    @Volatile private var audioTrack: AudioTrack? = null
    private var readerThread: Thread? = null
    private var writerThread: Thread? = null
    private var lastStateAt = 0L

    fun start() {
        if (!running.compareAndSet(false, true)) {
            return
        }

        publish(ClientState(false, "Connecting to Windows app...", latencyMode = latencyMode.label), force = true)
        readerThread = thread(name = "CableSpeaker-reader") {
            runClient()
        }
    }

    fun stop() {
        if (!running.compareAndSet(true, false)) {
            return
        }

        try {
            socket?.close()
        } catch (_: IOException) {
        }

        writerThread?.interrupt()
        releaseTrack()
        jitterBuffer.clear()
        publish(ClientState(false, "Disconnected.", latencyMode = latencyMode.label), force = true)
    }

    private fun runClient() {
        try {
            Socket().use { activeSocket ->
                socket = activeSocket
                activeSocket.tcpNoDelay = true
                activeSocket.connect(InetSocketAddress(Protocol.HOST, Protocol.PORT), 3000)

                val reader = FrameReader(activeSocket.getInputStream())
                val handshake = reader.readHandshake()
                handshake.validate()

                val track = createAudioTrack()
                audioTrack = track
                track.play()
                startWriter(track)

                publish(ClientState(true, "Connected. Receiving audio.", latencyMode = latencyMode.label), force = true)
                while (running.get()) {
                    val frame = reader.readFrame()
                    jitterBuffer.offer(frame.payload)
                    publishCurrent("Receiving audio.")
                }
            }
        } catch (ex: Exception) {
            if (running.get()) {
                publish(
                    ClientState(
                        connected = false,
                        message = "Connection ended: ${ex.message ?: ex.javaClass.simpleName}",
                        bufferMs = jitterBuffer.bufferMs,
                        underruns = jitterBuffer.underruns,
                        droppedFrames = jitterBuffer.droppedFrames,
                        latencyMode = latencyMode.label,
                    ),
                    force = true,
                )
            }
        } finally {
            running.set(false)
            try {
                socket?.close()
            } catch (_: IOException) {
            }
            socket = null
            releaseTrack()
        }
    }

    private fun startWriter(track: AudioTrack) {
        writerThread = thread(name = "CableSpeaker-writer") {
            var primed = false
            while (running.get()) {
                if (!primed && !jitterBuffer.isPrimed) {
                    Thread.sleep(5)
                    publishCurrent("Buffering audio.")
                    continue
                }

                primed = true
                val payload = jitterBuffer.poll() ?: silenceFrame
                val written = track.write(payload, 0, payload.size, AudioTrack.WRITE_BLOCKING)
                if (written < 0) {
                    throw IOException("AudioTrack write failed with code $written")
                }
                publishCurrent("Playing audio.")
            }
        }
    }

    private fun createAudioTrack(): AudioTrack {
        val minBuffer = AudioTrack.getMinBufferSize(
            Protocol.SAMPLE_RATE,
            AudioFormat.CHANNEL_OUT_STEREO,
            AudioFormat.ENCODING_PCM_16BIT,
        )
        val trackBufferBytes = max(minBuffer, Protocol.bytesForMs(latencyMode.bufferMs * 2))
        val attributes = AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_MEDIA)
            .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
            .build()
        val format = AudioFormat.Builder()
            .setSampleRate(Protocol.SAMPLE_RATE)
            .setChannelMask(AudioFormat.CHANNEL_OUT_STEREO)
            .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
            .build()

        return AudioTrack(
            attributes,
            format,
            trackBufferBytes,
            AudioTrack.MODE_STREAM,
            AudioManager.AUDIO_SESSION_ID_GENERATE,
        )
    }

    private fun releaseTrack() {
        val track = audioTrack ?: return
        audioTrack = null
        try {
            track.pause()
            track.flush()
            track.stop()
        } catch (_: IllegalStateException) {
        } finally {
            track.release()
        }
    }

    private fun publishCurrent(message: String) {
        publish(
            ClientState(
                connected = true,
                message = message,
                bufferMs = jitterBuffer.bufferMs,
                underruns = jitterBuffer.underruns,
                droppedFrames = jitterBuffer.droppedFrames,
                latencyMode = latencyMode.label,
            ),
            force = false,
        )
    }

    private fun publish(state: ClientState, force: Boolean) {
        val now = System.currentTimeMillis()
        if (!force && now - lastStateAt < 250) {
            return
        }

        lastStateAt = now
        onState(state)
    }
}

