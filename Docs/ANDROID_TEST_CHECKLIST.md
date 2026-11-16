# Android device test checklist

Run for every release candidate and after any graphics/build-setting change. Record results in `BUILD_VALIDATION.md`.

| Device | ABI | When | Role |
|---|---|---|---|
| Redmi A3 | armeabi-v7a (32-bit), PowerVR GE8320 | when available | **release blocker** low tier, must run on GLES3 |
| Samsung Galaxy S23 Ultra | arm64-v8a | any time (wireless debugging) | high tier, 20:9 with camera cutout |
| TCL Tab 10 | check `ro.product.cpu.abilist` | **only after 21:00** | tablet aspect / density |

## Setup (wireless debugging, Android 11+)
```bash
# Phone: Developer options ▸ Wireless debugging ▸ Pair device with pairing code
adb pair <ip>:<pairing-port> <code>
adb connect <ip>:<debug-port>           # port shown on the Wireless debugging screen
adb devices -l
adb shell getprop ro.product.cpu.abilist   # confirm the ABI before diagnosing install problems
adb uninstall com.nocvault.verdantsiege    # clean install for each candidate
adb install -r Builds/Android/VerdantSiege.apk
adb logcat -c && adb logcat -v time Unity:V CRASH:V AndroidRuntime:E DEBUG:V libc:F '*:S' > device_log.txt
```
Start-up log line to record: `[VS Quality] <Level> (<reason>); GPU=…; API=…; ABI=32-bit|64-bit; target=30|60fps`.
On the Redmi A3 the API **must be OpenGLES3** (PowerVR Vulkan deny filter).

## Relaunch regression (mandatory)
1. Install, launch: main menu in landscape, music plays.
2. Play First Roots for at least 2 minutes.
3. Swipe the app away from Recents (full close), then relaunch, then play again for at least 2 minutes. Repeat twice.
Any crash, black screen or freeze on relaunch is release-blocking.

## Functional
- [ ] Tap card → valid cells highlight; tap cell plants; cost deducted; card shows regrowth timer
- [ ] Drag a card onto a cell plants it; dropping outside cancels
- [ ] Invalid tap (occupied/unaffordable/cooling) → red cell flash + message, nothing spent
- [ ] Tap and swipe across Sunmotes collects them; collected Vitality flies to the counter
- [ ] Tap a defender → inspect card; Undo works within 3 s and refunds fully
- [ ] Bulwark Bloom sheds plate layers at 66% / 33% health
- [ ] Plated Brute's armour shatters with debris, then it walks faster
- [ ] Wave banners; final-wave banner; Heartroot loses a leaf pip on breach
- [ ] Pause (button and Android Back) freezes everything; Resume, Restart, Main Menu work; 2x speed works
- [ ] Home button mid-battle → returns paused
- [ ] Victory and defeat screens; Seeds total persists after a full close and relaunch
- [ ] Settings (volumes, auto-collect, reduced flashes, graphics) persist after relaunch
- [ ] Reset Progress asks for confirmation and erases seeds/progress only
- [ ] No HUD clipped by the camera cutout, rounded corners or gesture bar
- [ ] Airplane mode: everything works offline

## Performance record
| Field | Value |
|---|---|
| Device / Android version / RAM | |
| APK versionName / versionCode | |
| `[VS Quality]` line | |
| FPS during final wave (`adb shell dumpsys gfxinfo com.nocvault.verdantsiege`) | |
| Lowest FPS seen | |
| Temperature/throttling after 10 min | |
| Menu → battle load time | |
