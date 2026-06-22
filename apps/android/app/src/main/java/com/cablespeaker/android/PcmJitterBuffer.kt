package com.cablespeaker.android

class PcmJitterBuffer(targetBufferMs: Int) {
    private val frames = ArrayDeque<ByteArray>()
    private val targetBytes = Protocol.bytesForMs(targetBufferMs)
    private val maxBytes = Protocol.bytesForMs((targetBufferMs * 3).coerceAtLeast(targetBufferMs + 120))
    private var totalBytes = 0

    var droppedFrames: Long = 0
        private set

    var underruns: Long = 0
        private set

    val bufferMs: Int
        @Synchronized get() = totalBytes * 1000 / (Protocol.SAMPLE_RATE * Protocol.CHANNELS * Protocol.BYTES_PER_SAMPLE)

    val isPrimed: Boolean
        @Synchronized get() = totalBytes >= targetBytes

    @Synchronized
    fun offer(frame: ByteArray) {
        if (frame.isEmpty()) {
            return
        }

        frames.addLast(frame.copyOf())
        totalBytes += frame.size

        while (totalBytes > maxBytes && frames.isNotEmpty()) {
            val dropped = frames.removeFirst()
            totalBytes -= dropped.size
            droppedFrames++
        }
    }

    @Synchronized
    fun poll(): ByteArray? {
        val frame = frames.removeFirstOrNull()
        if (frame == null) {
            underruns++
            return null
        }

        totalBytes -= frame.size
        return frame
    }

    @Synchronized
    fun clear() {
        frames.clear()
        totalBytes = 0
    }
}

