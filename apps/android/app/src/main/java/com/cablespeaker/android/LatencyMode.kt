package com.cablespeaker.android

enum class LatencyMode(val label: String, val bufferMs: Int) {
    Low("Low", 80),
    Normal("Normal", 120),
    Stable("Stable", 180);

    companion object {
        fun fromBufferMs(bufferMs: Int): LatencyMode {
            return entries.minBy { kotlin.math.abs(it.bufferMs - bufferMs) }
        }
    }
}
