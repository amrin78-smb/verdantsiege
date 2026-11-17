# Tester builds

| File | Version | Date | SHA-256 | Notes |
|---|---|---|---|---|
| VerdantSiege-0.5.2.apk | 0.5.2 (versionCode 8) | 2026-11-18 | `0c4e40f90524d1a61af639dab5cfe6f17e1f850323d0affd42cfc76ac469a94a` | Second readability pass: min text 34, body 38-40, costs 42+, bigger cards/buttons/HUD everywhere. Otherwise as 0.5.0. ARMv7 + ARM64, offline, debug-signed |
| ~~VerdantSiege-0.5.1.apk~~ | 0.5.1 (versionCode 7) | 2026-11-17 | `189c7d116b6cd9848d01d9db73a2248b22f77f1ddc4f2a980dddcb7db35ed996` | Readability pass: much larger text and cards on every menu and the battle HUD (min text 28 units), scrolling lists, two-column card rail for 6-8 cards. Otherwise as 0.5.0. ARMv7 + ARM64, offline, debug-signed |
| ~~VerdantSiege-0.5.0.apk~~ | 0.5.0 (versionCode 6) | 2026-11-17 | `4d6906338224bcb23b36a9fa9b65324e243d345ad67cc0d0ac05e701259f4688` | Milestone 4: Sanctuary hub, World Map (7 missions, 3 stars each), deck loadout, research (14 nodes, Seeds/Essence), defender mastery unlocking evolution branches, rewards screen, save v3 (migrates). ARMv7 + ARM64, offline, debug-signed |
| ~~VerdantSiege-0.4.1.apk~~ | 0.4.1 (versionCode 5) | 2026-11-16 | `741839268b82ce792072cdcd5d5680034272c23ff15eac8815a2cf07564a77f3` | Fix: tutorial no longer freezes the first-wave countdown (runs to 5 s, waits at most 20 s, labelled). Otherwise as 0.4.0. ARMv7 + ARM64, offline, debug-signed |
| ~~VerdantSiege-0.4.0.apk~~ | 0.4.0 (versionCode 4) | 2026-11-16 | `f0cff449c97941d0a49e80f63d41696c56602b61ba7a854675442f586925cf52` | Full roster (12 defenders, 12 enemies, 2 elites), Living Evolution (26 branches), Root Maw boss with weak points/stagger/cinematics, Essence, compact HUD, combat FX, damage numbers. ARMv7 + ARM64, offline, debug-signed |
| ~~VerdantSiege-0.2.0.apk~~ | 0.2.0 (versionCode 2), Milestone 2 in progress | 2026-11-16 | `b0f9b2c76403aedac65159910a87360b0fe3c6544465ad21b716a8d8acc6eb5f` | Easier economy + enemy drops, sunlight pools, tutorial, health bars, smoother models, app icon, branded splash. ARMv7 + ARM64, offline, debug-signed |
| ~~VerdantSiege-0.1.0.apk~~ | 0.1.0 (1) | 2026-11-16 | `92d39e2e…` | removed from the tree (in git history) |

Install: copy the APK to the phone and open it (allow "install unknown apps"), or `adb install -r VerdantSiege-0.5.2.apk`.
Debug-signed: uninstall it before installing a future release-signed build.
