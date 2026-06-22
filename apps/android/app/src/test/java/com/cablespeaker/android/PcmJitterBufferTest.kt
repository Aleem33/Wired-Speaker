package com.cablespeaker.android

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PcmJitterBufferTest {
    @Test
    fun primesAtTargetBufferLevel() {
        val buffer = PcmJitterBuffer(40)
        assertFalse(buffer.isPrimed)

        buffer.offer(ByteArray(Protocol.FRAME_PAYLOAD_BYTES))
        assertFalse(buffer.isPrimed)

        buffer.offer(ByteArray(Protocol.FRAME_PAYLOAD_BYTES))
        assertTrue(buffer.isPrimed)
        assertEquals(40, buffer.bufferMs)
    }

    @Test
    fun pollsInOrder() {
        val buffer = PcmJitterBuffer(20)
        val first = ByteArray(Protocol.FRAME_PAYLOAD_BYTES) { 1 }
        val second = ByteArray(Protocol.FRAME_PAYLOAD_BYTES) { 2 }

        buffer.offer(first)
        buffer.offer(second)

        assertArrayEquals(first, buffer.poll())
        assertArrayEquals(second, buffer.poll())
    }

    @Test
    fun countsUnderruns() {
        val buffer = PcmJitterBuffer(20)

        assertEquals(null, buffer.poll())
        assertEquals(1, buffer.underruns)
    }

    @Test
    fun dropsOldFramesWhenTooFull() {
        val buffer = PcmJitterBuffer(20)
        repeat(12) {
            buffer.offer(ByteArray(Protocol.FRAME_PAYLOAD_BYTES) { it.toByte() })
        }

        assertTrue(buffer.droppedFrames > 0)
        assertTrue(buffer.bufferMs <= 200)
    }

    @Test
    fun canRaiseTargetBufferAfterUnderrun() {
        val buffer = PcmJitterBuffer(40)
        buffer.offer(ByteArray(Protocol.FRAME_PAYLOAD_BYTES))
        buffer.offer(ByteArray(Protocol.FRAME_PAYLOAD_BYTES))
        assertTrue(buffer.isPrimed)

        buffer.setTargetBufferMs(180)
        assertFalse(buffer.isPrimed)
    }
}
