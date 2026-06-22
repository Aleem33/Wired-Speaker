package com.cablespeaker.android

import kotlin.test.Test
import kotlin.test.assertContentEquals
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

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

        assertContentEquals(first, buffer.poll())
        assertContentEquals(second, buffer.poll())
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
        repeat(10) {
            buffer.offer(ByteArray(Protocol.FRAME_PAYLOAD_BYTES) { it.toByte() })
        }

        assertTrue(buffer.droppedFrames > 0)
        assertTrue(buffer.bufferMs <= 140)
    }
}

