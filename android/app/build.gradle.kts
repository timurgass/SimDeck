plugins { id("com.android.application"); id("org.jetbrains.kotlin.android"); id("org.jetbrains.kotlin.plugin.compose") }
android {
    namespace = "dev.simdeck"
    compileSdk = 35
    buildToolsVersion = "36.0.0"
    defaultConfig { applicationId = "dev.simdeck"; minSdk = 29; targetSdk = 35; versionCode = 44; versionName = "0.9.19"; testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner" }
    flavorDimensions += "device"
    productFlavors {
        create("tablet") {
            dimension = "device"
            buildConfigField("boolean", "PHONE_LAYOUT", "false")
            resValue("string", "app_name", "SimDeck")
        }
        create("phone") {
            dimension = "device"
            applicationIdSuffix = ".phone"
            buildConfigField("boolean", "PHONE_LAYOUT", "true")
            resValue("string", "app_name", "SimDeck Phone")
        }
    }
    buildFeatures { compose = true; buildConfig = true }
    compileOptions { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
    kotlinOptions { jvmTarget = "17" }
    buildTypes { release { isMinifyEnabled = false } }
    // Optional existing development key; keeps locally distributed APK updates compatible.
    System.getenv("SIMDECK_DEBUG_KEYSTORE")?.let { existingKey ->
        signingConfigs.getByName("debug") { storeFile = file(existingKey) }
    }
    sourceSets["test"].resources.srcDir("../../protocol/fixtures")
    sourceSets["test"].resources.srcDir("src/main/assets")
    sourceSets["test"].resources.srcDir("src/debug/assets")
    sourceSets["main"].assets.srcDir("../../assets")
    sourceSets["main"].assets.srcDir(layout.buildDirectory.dir("generated/navigatorAssets"))
}
val navigatorAssets by tasks.registering(Copy::class) {
    from("../../companion/SimDeck.App/Browser") { include("truck-navigator.js", "truck-navigator.css") }
    into(layout.buildDirectory.dir("generated/navigatorAssets/navigator"))
}
tasks.named("preBuild").configure { dependsOn(navigatorAssets) }
dependencies {
    implementation("androidx.activity:activity-compose:1.9.3")
    implementation("androidx.compose.ui:ui:1.7.6")
    implementation("androidx.compose.ui:ui-tooling-preview:1.7.6")
    implementation("androidx.compose.material3:material3:1.3.1")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.8.7")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.9.0")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.json:json:20240303")
    testImplementation("com.squareup.okhttp3:mockwebserver:4.12.0")
    testImplementation("com.squareup.okhttp3:okhttp-tls:4.12.0")
    testImplementation("org.jetbrains.kotlinx:kotlinx-coroutines-test:1.9.0")
    debugImplementation("androidx.compose.ui:ui-tooling:1.7.6")
}
