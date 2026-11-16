# Build validation

Labels: **Verified (APK inspection)**, **Verified (automated test)**, **Verified (Linux player autoplay)**, **Pending device validation**.

## 0.1.0 (versionCode 1), Milestone 1, release APK, 2026-11-16
`Builds/Android/VerdantSiege.apk`, built with Unity 6000.3.27f2 via `Tools/build_android.sh release`.
Size 57,963,669 bytes · SHA-256 `92d39e2ee3423aa5143688e37d115ca79deab8a00df299a03b33dd352a1795b5` · **debug-signed** (no release keystore yet).

| Check | Result |
|---|---|
| `aapt dump badging … \| grep native-code` | `native-code: 'arm64-v8a' 'armeabi-v7a'` **PASS** |
| PowerVR Vulkan deny filter packaged | found in `assets/bin/Data/globalgamemanagers.assets.split5` **PASS** |
| Package / version | `com.nocvault.verdantsiege`, 0.1.0 (1), min SDK 25, target 36 |
| Orientation | `screenOrientation 0xb` (landscape) **PASS** |
| Permissions | no INTERNET **PASS** (offline) |
| EditMode tests | 61/61 passed |
| Linux autoplay | 1920x1080, 2400x1080 + 90 px inset, 2048x1536: won 5/5, 0 errors **PASS** |
| S23 Ultra / TCL Tab 10 / Redmi A3 install, relaunch, FPS | **Pending device validation** (no device reachable over adb on 2026-11-16) |
