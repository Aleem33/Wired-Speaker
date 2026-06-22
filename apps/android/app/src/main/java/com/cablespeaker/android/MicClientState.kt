package com.cablespeaker.android

data class MicClientState(
    val connected: Boolean,
    val message: String,
    val framesSent: Long = 0,
    val droppedFrames: Long = 0,
    val peak: Float = 0f,
)
