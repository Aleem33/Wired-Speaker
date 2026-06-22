package com.cablespeaker.android

import android.Manifest
import android.app.Activity
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.View
import android.widget.Button
import android.widget.LinearLayout
import android.widget.RadioButton
import android.widget.RadioGroup
import android.widget.TextView

class MainActivity : Activity() {
    private lateinit var statusText: TextView
    private lateinit var detailText: TextView
    private lateinit var connectButton: Button
    private lateinit var disconnectButton: Button
    private lateinit var latencyGroup: RadioGroup

    private var selectedMode = LatencyMode.Normal

    private val statusReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            if (intent?.action != PlaybackService.ACTION_STATUS) {
                return
            }

            val connected = intent.getBooleanExtra(PlaybackService.EXTRA_CONNECTED, false)
            val message = intent.getStringExtra(PlaybackService.EXTRA_MESSAGE) ?: "No status yet."
            val bufferMs = intent.getIntExtra(PlaybackService.EXTRA_BUFFER_MS, 0)
            val underruns = intent.getLongExtra(PlaybackService.EXTRA_UNDERRUNS, 0)
            val dropped = intent.getLongExtra(PlaybackService.EXTRA_DROPPED_FRAMES, 0)
            val latency = intent.getStringExtra(PlaybackService.EXTRA_LATENCY_MODE) ?: selectedMode.label

            statusText.text = if (connected) "Connected" else "Disconnected"
            detailText.text = "Status: $message\nLatency: $latency\nBuffer: ${bufferMs}ms\nUnderruns: $underruns\nDropped frames: $dropped"
            connectButton.isEnabled = !connected
            disconnectButton.isEnabled = connected
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        requestNotificationPermissionIfNeeded()
        setContentView(buildContentView())
        updateIdleUi()
    }

    override fun onStart() {
        super.onStart()
        val filter = IntentFilter(PlaybackService.ACTION_STATUS)
        if (Build.VERSION.SDK_INT >= 33) {
            registerReceiver(statusReceiver, filter, RECEIVER_NOT_EXPORTED)
        } else {
            @Suppress("DEPRECATION")
            registerReceiver(statusReceiver, filter)
        }
    }

    override fun onStop() {
        unregisterReceiver(statusReceiver)
        super.onStop()
    }

    private fun buildContentView(): View {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(42, 42, 42, 42)
            setBackgroundColor(0xFFF6F7FB.toInt())
        }

        root.addView(TextView(this).apply {
            text = "CableSpeaker"
            textSize = 28f
            setTextColor(0xFF151821.toInt())
        })

        root.addView(TextView(this).apply {
            text = "Use this phone as the laptop speaker over USB."
            textSize = 15f
            setTextColor(0xFF4A5363.toInt())
            setPadding(0, 8, 0, 28)
        })

        statusText = TextView(this).apply {
            textSize = 22f
            setTextColor(0xFF151821.toInt())
        }
        root.addView(statusText)

        detailText = TextView(this).apply {
            textSize = 15f
            setTextColor(0xFF4A5363.toInt())
            setPadding(0, 12, 0, 24)
        }
        root.addView(detailText)

        latencyGroup = RadioGroup(this).apply {
            orientation = RadioGroup.HORIZONTAL
            setPadding(0, 0, 0, 24)
        }

        LatencyMode.entries.forEach { mode ->
            latencyGroup.addView(RadioButton(this).apply {
                text = "${mode.label} ${mode.bufferMs}ms"
                id = mode.bufferMs
                isChecked = mode == selectedMode
            })
        }
        latencyGroup.setOnCheckedChangeListener { _, checkedId ->
            selectedMode = LatencyMode.fromBufferMs(checkedId)
        }
        root.addView(latencyGroup)

        val buttons = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.START
        }

        connectButton = Button(this).apply {
            text = "Connect"
            setOnClickListener {
                startForegroundService(PlaybackService.connectIntent(this@MainActivity, selectedMode))
            }
        }
        buttons.addView(connectButton)

        disconnectButton = Button(this).apply {
            text = "Disconnect"
            setOnClickListener {
                startService(PlaybackService.disconnectIntent(this@MainActivity))
            }
        }
        buttons.addView(disconnectButton)

        root.addView(buttons)

        root.addView(TextView(this).apply {
            text = "Windows app must be started and USB tunnel must be ready before connecting."
            textSize = 13f
            setTextColor(0xFF667085.toInt())
            setPadding(0, 28, 0, 0)
        })

        return root
    }

    private fun updateIdleUi() {
        statusText.text = "Disconnected"
        detailText.text = "Status: waiting for USB tunnel\nLatency: ${selectedMode.label}\nBuffer: 0ms\nUnderruns: 0\nDropped frames: 0"
        connectButton.isEnabled = true
        disconnectButton.isEnabled = false
    }

    private fun requestNotificationPermissionIfNeeded() {
        if (Build.VERSION.SDK_INT >= 33 &&
            checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) {
            requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), 100)
        }
    }
}

