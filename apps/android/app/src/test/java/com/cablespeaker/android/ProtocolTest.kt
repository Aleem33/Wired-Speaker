package com.cablespeaker.android

import java.io.ByteArrayInputStream
import java.io.IOException
import java.nio.ByteBuffer
import java.nio.ByteOrder
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class ProtocolTest {
    @Test
    fun handshakeRoundTrips() {
        val bytes = ByteBuffer.allocate(Protocol.HANDSHAKE_BYTES)
            .order(ByteOrder.LITTLE_ENDIAN)
            .put(Protocol.MAGIC.toByteArray())
            .putInt(Protocol.SAMPLE_RATE)
            .putInt(Protocol.CHANNELS)
            .putInt(Protocol.BITS_PER_SAMPLE)
            .putInt(Protocol.FRAME_DURATION_MS)
            .array()

        val handshake = FrameReader(ByteArrayInputStream(bytes)).readHandshake()

        assertEquals(Protocol.SAMPLE_RATE, handshake.sampleRate)
        assertEquals(Protocol.CHANNELS, handshake.channels)
        assertEquals(Protocol.BITS_PER_SAMPLE, handshake.bitsPerSample)
        assertEquals(Protocol.FRAME_DURATION_MS, handshake.frameDurationMs)
        handshake.validate()
    }

    @Test
    fun frameRoundTrips() {
        val payload = ByteArray(Protocol.FRAME_PAYLOAD_BYTES) { (it % 251).toByte() }
        val bytes = ByteBuffer.allocate(Protocol.FRAME_HEADER_BYTES + payload.size)
            .order(ByteOrder.LITTLE_ENDIAN)
            .putInt(payload.size)
            .putLong(123456789L)
            .put(payload)
            .array()

        val frame = FrameReader(ByteArrayInputStream(bytes)).readFrame()

        assertEquals(123456789L, frame.hostTimestampMicros)
        assertArrayEquals(payload, frame.payload)
    }

    @Test
    fun micFrameWriterWritesHandshakeAndFrame() {
        val output = java.io.ByteArrayOutputStream()
        val writer = FrameWriter(output)
        val payload = ByteArray(Protocol.MIC_FRAME_PAYLOAD_BYTES) { (it % 127).toByte() }

        writer.writeMicHandshake()
        writer.writeFrame(payload, 987654321L)

        val bytes = output.toByteArray()
        assertEquals(Protocol.MIC_MAGIC, bytes.copyOfRange(0, 4).decodeToString())
        val frame = FrameReader(java.io.ByteArrayInputStream(bytes.copyOfRange(Protocol.HANDSHAKE_BYTES, bytes.size))).readFrame()
        assertEquals(987654321L, frame.hostTimestampMicros)
        assertArrayEquals(payload, frame.payload)
    }

    @Test
    fun rejectsOversizedFrame() {
        val bytes = ByteBuffer.allocate(Protocol.FRAME_HEADER_BYTES)
            .order(ByteOrder.LITTLE_ENDIAN)
            .putInt(Protocol.MAX_FRAME_PAYLOAD_BYTES + 1)
            .putLong(0)
            .array()

        assertThrows(IOException::class.java) {
            FrameReader(ByteArrayInputStream(bytes)).readFrame()
        }
    }
}
