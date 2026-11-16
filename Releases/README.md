# Tester builds

| File | Version | Date | SHA-256 | Notes |
|---|---|---|---|---|
| VerdantSiege-0.4.1.apk | 0.4.1 (versionCode 5) | 2026-11-16 | `741839268b82ce792072cdcd5d5680034272c23ff15eac8815a2cf07564a77f3` | Fix: tutorial no longer freezes the first-wave countdown (runs to 5 s, waits at most 20 s, labelled). Otherwise as 0.4.0. ARMv7 + ARM64, offline, debug-signed |
| ~~VerdantSiege-0.4.0.apk~~ | 0.4.0 (versionCode 4) | 2026-11-16 | `f0cff449c97941d0a49e80f63d41696c56602b61ba7a854675442f586925cf52` | Full roster (12 defenders, 12 enemies, 2 elites), Living Evolution (26 branches), Root Maw boss with weak points/stagger/cinematics, Essence, compact HUD, combat FX, damage numbers. ARMv7 + ARM64, offline, debug-signed |
| ~~VerdantSiege-0.2.0.apk~~ | 0.2.0 (versionCode 2), Milestone 2 in progress | 2026-11-16 | `b0f9b2c76403aedac65159910a87360b0fe3c6544465ad21b716a8d8acc6eb5f` | Easier economy + enemy drops, sunlight pools, tutorial, health bars, smoother models, app icon, branded splash. ARMv7 + ARM64, offline, debug-signed |
| ~~VerdantSiege-0.1.0.apk~~ | 0.1.0 (1) | 2026-11-16 | `92d39e2e…` | removed from the tree (in git history) |

Install: copy the APK to the phone and open it (allow "install unknown apps"), or `adb install -r VerdantSiege-0.4.1.apk`.
Debug-signed: uninstall it before installing a future release-signed build.
