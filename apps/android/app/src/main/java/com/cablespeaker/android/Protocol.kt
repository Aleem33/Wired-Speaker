package com.cablespeaker.android

import java.io.EOFException
import java.io.IOException
import java.io.InputStream
import java.io.OutputStream
import java.nio.ByteBuffer
import java.nio.ByteOrder
import kotlin.math.max

object Protocol {
    const val HOST = "127.0.0.1"
    const val PORT = 38271
    const val MIC_PORT = 38272
    const val MAGIC = "CSB1"
    const val MIC_MAGIC = "CSM1"
    const val SAMPLE_RATE = 48_000
    const val CHANNELS = 2
    const val MIC_CHANNELS = 1
    const val BITS_PER_SAMPLE = 16
    const val BYTES_PER_SAMPLE = BITS_PER_SAMPLE / 8
    const val FRAME_DURATION_MS = 20
    const val SAMPLES_PER_CHANNEL_PER_FRAME = SAMPLE_RATE * FRAME_DURATION_MS / 1000
    const val FRAME_PAYLOAD_BYTES = SAMPLES_PER_CHANNEL_PER_FRAME * CHANNELS * BYTES_PER_SAMPLE
    const val MIC_FRAME_PAYLOAD_BYTES = SAMPLES_PER_CHANNEL_PER_FRAME * MIC_CHANNELS * BYTES_PER_SAMPLE
    const val HANDSHAKE_BYTES = 20
    const val FRAME_HEADER_BYTES = 12
    const val MAX_FRAME_PAYLOAD_BYTES = FRAME_PAYLOAD_BYTES * 8
    const val MAX_MIC_FRAME_PAYLOAD_BYTES = MIC_FRAME_PAYLOAD_BYTES * 8

    fun bytesForMs(ms: Int): Int {
        val safeMs = max(1, ms)
        return SAMPLE_RATE * CHANNELS * BYTES_PER_SAMPLE * safeMs / 1000
    }
}

data class Handshake(
    val sampleRate: Int,
    val channels: Int,
    val bitsPerSample: Int,
    val frameDurationMs: Int,
) {
    fun validate() {
        if (sampleRate != Protocol.SAMPLE_RATE ||
            channels != Protocol.CHANNELS ||
            bitsPerSample != Protocol.BITS_PER_SAMPLE ||
            frameDurationMs != Protocol.FRAME_DURATION_MS
        ) {
            throw IOException(
                "Unsupported stream format: ${sampleRate}Hz, ${channels}ch, ${bitsPerSample}-bit, ${frameDurationMs}ms frames"
            )
        }
    }
}

data class AudioFrame(
    val payload: ByteArray,
    val hostTimestampMicros: Long,
) {
    override fun equals(other: Any?): Boolean {
        if (this === other) return true
        if (other !is AudioFrame) return false
        return hostTimestampMicros == other.hostTimestampMicros && payload.contentEquals(other.payload)
    }

    override fun hashCode(): Int {
        var result = payload.contentHashCode()
        result = 31 * result + hostTimestampMicros.hashCode()
        return result
    }
}

class FrameReader(private val input: InputStream) {
    fun readHandshake(): Handshake {
        val bytes = input.readExactly(Protocol.HANDSHAKE_BYTES)
        val magic = bytes.copyOfRange(0, 4).decodeToString()
        if (magic != Protocol.MAGIC) {
            throw IOException("Unexpected protocol magic '$magic'")
        }

        val buffer = ByteBuffer.wrap(bytes).order(ByteOrder.LITTLE_ENDIAN)
        buffer.position(4)
        return Handshake(
            sampleRate = buffer.int,
            channels = buffer.int,
            bitsPerSample = buffer.int,
            frameDurationMs = buffer.int,
        )
    }

    fun readFrame(): AudioFrame {
        val headerBytes = input.readExactly(Protocol.FRAME_HEADER_BYTES)
        val header = ByteBuffer.wrap(headerBytes).order(ByteOrder.LITTLE_ENDIAN)
        val payloadLength = header.int
        if (payloadLength <= 0 || payloadLength > Protocol.MAX_FRAME_PAYLOAD_BYTES) {
            throw IOException("Invalid frame payload length $payloadLength")
        }

        val timestamp = header.long
        val payload = input.readExactly(payloadLength)
        return AudioFrame(payload, timestamp)
    }
}

class FrameWriter(private val output: OutputStream) {
    fun writeMicHandshake() {
        val bytes = ByteBuffer.allocate(Protocol.HANDSHAKE_BYTES)
            .order(ByteOrder.LITTLE_ENDIAN)
            .put(Protocol.MIC_MAGIC.toByteArray())
            .putInt(Protocol.SAMPLE_RATE)
            .putInt(Protocol.MIC_CHANNELS)
            .putInt(Protocol.BITS_PER_SAMPLE)
            .putInt(Protocol.FRAME_DURATION_MS)
            .array()
        output.write(bytes)
        output.flush()
    }

    fun writeFrame(payload: ByteArray, timestampMicros: Long) {
        if (payload.isEmpty() || payload.size > Protocol.MAX_MIC_FRAME_PAYLOAD_BYTES) {
            throw IOException("Invalid mic frame payload length ${payload.size}")
        }

        val header = ByteBuffer.allocate(Protocol.FRAME_HEADER_BYTES)
            .order(ByteOrder.LITTLE_ENDIAN)
            .putInt(payload.size)
            .putLong(timestampMicros)
            .array()
        output.write(header)
        output.write(payload)
        output.flush()
    }
}

fun InputStream.readExactly(length: Int): ByteArray {
    val buffer = ByteArray(length)
    var offset = 0
    while (offset < length) {
        val read = read(buffer, offset, length - offset)
        if (read < 0) {
            throw EOFException("Stream ended after $offset of $length bytes")
        }
        offset += read
    }

    return buffer
}
