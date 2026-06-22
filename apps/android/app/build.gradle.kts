plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.cablespeaker.android"
    compileSdk = 37

    defaultConfig {
        applicationId = "com.cablespeaker.android"
        minSdk = 26
        targetSdk = 37
        versionCode = 1
        versionName = "0.1.0"
    }

    testOptions {
        unitTests.isReturnDefaultValues = true
    }
}

kotlin {
    jvmToolchain(17)
}

dependencies {
    testImplementation(kotlin("test"))
    testImplementation("junit:junit:4.13.2")
}
