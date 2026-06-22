package com.cablespeaker.android

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.IBinder

class MicService : Service() {
    private var client: MicStreamClient? = null

    override fun onCreate() {
        super.onCreate()
        ensureNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_START_MIC -> {
                startForeground(NOTIFICATION_ID, buildNotification("Connecting phone mic..."))
                startClient()
            }
            ACTION_STOP_MIC -> {
                stopClient()
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
            }
            else -> startForeground(NOTIFICATION_ID, buildNotification("Phone mic ready."))
        }

        return START_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        stopClient()
        super.onDestroy()
    }

    private fun startClient() {
        stopClient()
        client = MicStreamClient(this) { state ->
            broadcastState(state)
            val manager = getSystemService(NotificationManager::class.java)
            manager.notify(NOTIFICATION_ID, buildNotification(state.message))
        }.also { it.start() }
    }

    private fun stopClient() {
        client?.stop()
        client = null
    }

    private fun broadcastState(state: MicClientState) {
        val intent = Intent(ACTION_MIC_STATUS)
            .setPackage(packageName)
            .putExtra(EXTRA_MIC_CONNECTED, state.connected)
            .putExtra(EXTRA_MIC_MESSAGE, state.message)
            .putExtra(EXTRA_MIC_FRAMES_SENT, state.framesSent)
            .putExtra(EXTRA_MIC_DROPPED_FRAMES, state.droppedFrames)
            .putExtra(EXTRA_MIC_PEAK, state.peak)
        sendBroadcast(intent)
    }

    private fun ensureNotificationChannel() {
        val manager = getSystemService(NotificationManager::class.java)
        val channel = NotificationChannel(
            CHANNEL_ID,
            "CableSpeaker phone mic",
            NotificationManager.IMPORTANCE_LOW,
        )
        manager.createNotificationChannel(channel)
    }

    private fun buildNotification(text: String): Notification {
        val activityIntent = Intent(this, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            this,
            1,
            activityIntent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )

        return Notification.Builder(this, CHANNEL_ID)
            .setContentTitle("CableSpeaker Mic")
            .setContentText(text)
            .setSmallIcon(android.R.drawable.ic_btn_speak_now)
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .build()
    }

    companion object {
        const val ACTION_START_MIC = "com.cablespeaker.android.START_MIC"
        const val ACTION_STOP_MIC = "com.cablespeaker.android.STOP_MIC"
        const val ACTION_MIC_STATUS = "com.cablespeaker.android.MIC_STATUS"

        const val EXTRA_MIC_CONNECTED = "micConnected"
        const val EXTRA_MIC_MESSAGE = "micMessage"
        const val EXTRA_MIC_FRAMES_SENT = "micFramesSent"
        const val EXTRA_MIC_DROPPED_FRAMES = "micDroppedFrames"
        const val EXTRA_MIC_PEAK = "micPeak"

        private const val CHANNEL_ID = "cablespeaker_mic"
        private const val NOTIFICATION_ID = 38272

        fun startIntent(context: Context): Intent {
            return Intent(context, MicService::class.java).setAction(ACTION_START_MIC)
        }

        fun stopIntent(context: Context): Intent {
            return Intent(context, MicService::class.java).setAction(ACTION_STOP_MIC)
        }
    }
}
