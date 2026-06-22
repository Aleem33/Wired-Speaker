package com.cablespeaker.android

data class ClientState(
    val connected: Boolean,
    val message: String,
    val bufferMs: Int = 0,
    val underruns: Long = 0,
    val droppedFrames: Long = 0,
    val latencyMode: String = "Normal",
)

