plugins {
    id("com.android.application")
}

android {
    namespace = "com.cablespeaker.android"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.cablespeaker.android"
        minSdk = 26
        targetSdk = 36
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
    testImplementation("junit:junit:4.13.2")
}
