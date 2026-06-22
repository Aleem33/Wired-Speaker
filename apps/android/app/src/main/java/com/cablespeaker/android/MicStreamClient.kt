package com.cablespeaker.android

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.media.AudioFormat
import android.media.AudioRecord
import android.media.MediaRecorder
import java.io.IOException
import java.net.InetSocketAddress
import java.net.Socket
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.concurrent.thread
import kotlin.math.abs
import kotlin.math.max

class MicStreamClient(
    private val context: Context,
    private val onState: (MicClientState) -> Unit,
) {
    private val running = AtomicBoolean(false)

    @Volatile private var socket: Socket? = null
    @Volatile private var recorder: AudioRecord? = null
    private var workerThread: Thread? = null
    private var lastStateAt = 0L
    private var framesSent = 0L
    private var droppedFrames = 0L

    fun start() {
        if (!running.compareAndSet(false, true)) {
            return
        }

        publish(MicClientState(false, "Connecting phone mic to Windows..."), force = true)
        workerThread = thread(name = "CableSpeaker-mic") {
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

        releaseRecorder()
        publish(MicClientState(false, "Phone mic stopped.", framesSent, droppedFrames), force = true)
    }

    private fun runClient() {
        if (context.checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            publish(MicClientState(false, "Microphone permission is required."), force = true)
            running.set(false)
            return
        }

        try {
            Socket().use { activeSocket ->
                socket = activeSocket
                activeSocket.tcpNoDelay = true
                activeSocket.connect(InetSocketAddress(Protocol.HOST, Protocol.MIC_PORT), 3000)

                val writer = FrameWriter(activeSocket.getOutputStream())
                writer.writeMicHandshake()

                val activeRecorder = createRecorder()
                recorder = activeRecorder
                activeRecorder.startRecording()
                publish(MicClientState(true, "Phone mic connected.", framesSent, droppedFrames), force = true)

                val frame = ByteArray(Protocol.MIC_FRAME_PAYLOAD_BYTES)
                while (running.get()) {
                    val read = activeRecorder.read(frame, 0, frame.size, AudioRecord.READ_BLOCKING)
                    if (read < 0) {
                        throw IOException("AudioRecord read failed with code $read")
                    }

                    if (read != frame.size) {
                        droppedFrames++
                        publishCurrent("Short mic frame dropped.", 0f)
                        continue
                    }

                    val payload = frame.copyOf()
                    val peak = calculatePeak(payload)
                    writer.writeFrame(payload, System.currentTimeMillis() * 1000)
                    framesSent++
                    publishCurrent("Streaming phone mic.", peak)
                }
            }
        } catch (ex: Exception) {
            if (running.get()) {
                publish(
                    MicClientState(
                        connected = false,
                        message = "Mic connection ended: ${ex.message ?: ex.javaClass.simpleName}",
                        framesSent = framesSent,
                        droppedFrames = droppedFrames,
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
            releaseRecorder()
        }
    }

    private fun createRecorder(): AudioRecord {
        val minBuffer = AudioRecord.getMinBufferSize(
            Protocol.SAMPLE_RATE,
            AudioFormat.CHANNEL_IN_MONO,
            AudioFormat.ENCODING_PCM_16BIT,
        )
        val bufferBytes = max(minBuffer, Protocol.bytesForMs(240) / Protocol.CHANNELS)
        val format = AudioFormat.Builder()
            .setSampleRate(Protocol.SAMPLE_RATE)
            .setChannelMask(AudioFormat.CHANNEL_IN_MONO)
            .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
            .build()

        return AudioRecord.Builder()
            .setAudioSource(MediaRecorder.AudioSource.VOICE_COMMUNICATION)
            .setAudioFormat(format)
            .setBufferSizeInBytes(bufferBytes)
            .build()
    }

    private fun releaseRecorder() {
        val activeRecorder = recorder ?: return
        recorder = null
        try {
            activeRecorder.stop()
        } catch (_: IllegalStateException) {
        } finally {
            activeRecorder.release()
        }
    }

    private fun publishCurrent(message: String, peak: Float) {
        publish(MicClientState(true, message, framesSent, droppedFrames, peak), force = false)
    }

    private fun publish(state: MicClientState, force: Boolean) {
        val now = System.currentTimeMillis()
        if (!force && now - lastStateAt < 250) {
            return
        }

        lastStateAt = now
        onState(state)
    }

    private fun calculatePeak(payload: ByteArray): Float {
        var peak = 0
        var offset = 0
        while (offset + 1 < payload.size) {
            val sample = (payload[offset].toInt() and 0xFF) or (payload[offset + 1].toInt() shl 8)
            peak = max(peak, abs(sample.toShort().toInt()))
            offset += 2
        }

        return peak / Short.MAX_VALUE.toFloat()
    }
}
