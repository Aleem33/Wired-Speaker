package com.cablespeaker.android

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.IBinder

class PlaybackService : Service() {
    private var client: AudioStreamClient? = null

    override fun onCreate() {
        super.onCreate()
        ensureNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_CONNECT -> {
                val bufferMs = intent.getIntExtra(EXTRA_BUFFER_MS, LatencyMode.Normal.bufferMs)
                val mode = LatencyMode.fromBufferMs(bufferMs)
                startForeground(NOTIFICATION_ID, buildNotification("Connecting over USB..."))
                startClient(mode)
            }
            ACTION_DISCONNECT -> {
                stopClient()
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
            }
            else -> {
                startForeground(NOTIFICATION_ID, buildNotification("CableSpeaker is ready."))
            }
        }

        return START_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        stopClient()
        super.onDestroy()
    }

    private fun startClient(mode: LatencyMode) {
        stopClient()
        client = AudioStreamClient(mode) { state ->
            broadcastState(state)
            val manager = getSystemService(NotificationManager::class.java)
            manager.notify(NOTIFICATION_ID, buildNotification(state.message))
        }.also { it.start() }
    }

    private fun stopClient() {
        client?.stop()
        client = null
    }

    private fun broadcastState(state: ClientState) {
        val intent = Intent(ACTION_STATUS)
            .setPackage(packageName)
            .putExtra(EXTRA_CONNECTED, state.connected)
            .putExtra(EXTRA_MESSAGE, state.message)
            .putExtra(EXTRA_BUFFER_MS, state.bufferMs)
            .putExtra(EXTRA_UNDERRUNS, state.underruns)
            .putExtra(EXTRA_DROPPED_FRAMES, state.droppedFrames)
            .putExtra(EXTRA_LATENCY_MODE, state.latencyMode)
        sendBroadcast(intent)
    }

    private fun ensureNotificationChannel() {
        val manager = getSystemService(NotificationManager::class.java)
        val channel = NotificationChannel(
            CHANNEL_ID,
            "CableSpeaker playback",
            NotificationManager.IMPORTANCE_LOW,
        )
        manager.createNotificationChannel(channel)
    }

    private fun buildNotification(text: String): Notification {
        val activityIntent = Intent(this, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            this,
            0,
            activityIntent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )

        return Notification.Builder(this, CHANNEL_ID)
            .setContentTitle("CableSpeaker")
            .setContentText(text)
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .build()
    }

    companion object {
        const val ACTION_CONNECT = "com.cablespeaker.android.CONNECT"
        const val ACTION_DISCONNECT = "com.cablespeaker.android.DISCONNECT"
        const val ACTION_STATUS = "com.cablespeaker.android.STATUS"

        const val EXTRA_CONNECTED = "connected"
        const val EXTRA_MESSAGE = "message"
        const val EXTRA_BUFFER_MS = "bufferMs"
        const val EXTRA_UNDERRUNS = "underruns"
        const val EXTRA_DROPPED_FRAMES = "droppedFrames"
        const val EXTRA_LATENCY_MODE = "latencyMode"

        private const val CHANNEL_ID = "cablespeaker_playback"
        private const val NOTIFICATION_ID = 38271

        fun connectIntent(context: Context, mode: LatencyMode): Intent {
            return Intent(context, PlaybackService::class.java)
                .setAction(ACTION_CONNECT)
                .putExtra(EXTRA_BUFFER_MS, mode.bufferMs)
        }

        fun disconnectIntent(context: Context): Intent {
            return Intent(context, PlaybackService::class.java)
                .setAction(ACTION_DISCONNECT)
        }
    }
}

