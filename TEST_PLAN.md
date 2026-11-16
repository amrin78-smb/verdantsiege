# Test Plan

## How to run
| What | Command | Notes |
|---|---|---|
| Regenerate content | `Tools/setup.sh` | Run after model/script changes. Exit 0 = content valid. |
| Blender models | `Tools/Blender/build_all.sh` | Regenerates and checks all 27 models (~5 min). |
| EditMode tests | `Tools/run_tests.sh EditMode` | Results: `Logs/editmode-results.xml` |
| Linux autoplay | `Tools/autopilot.sh 1920x1080 2400x1080:90 2048x1536` | Env: `VS_ENCOUNTER`, `VS_STRATEGY=good|none`, `VS_SPEED`, `SKIP_BUILD=1`. Reports and screenshots: `Logs/AutoPilot/` |
| Android APK | `Tools/build_android.sh [release]` | Builds, then `Tools/verify_apk.sh` checks the ABIs, PowerVR filter, orientation and permissions. |
| On device | `Docs/ANDROID_TEST_CHECKLIST.md` | Mandatory for each release candidate. |

## Automated coverage (EditMode, 61 tests)
| Area (master prompt §14) | Tests |
|---|---|
| Grid coordinate conversion | GridTests, PresentationTests.Layout_WorldCoordRoundTrip |
| Valid/invalid placement, occupancy | GridTests.*, DeploymentAndEconomyTests.Deploy_InvalidCellsAndCards / RapidDuplicateTap |
| Resource spending and refund | Deploy_SpendsCost, InsufficientVitality, Undo_RefundsWithinGrace, VitalityBank_*, Pickups_*, AutoCollect |
| Defender cooldown | Cooldown_RecoversOverTime, StartingCooldown_AppliesAtBattleStart |
| Projectile hit | Shooter_FiresOnlyWithTargetAhead, Shooter_IgnoresOtherLanes, Pierce, Splash, Knockback |
| Damage and resistance | Damage_ResistanceMultiplies, Armor_AbsorbsThenOverflows_PiercingBypasses, Damage_KillsAtZero |
| Status effect duration and stacking | Status_* (5 tests) |
| Enemy movement and lane progression | Enemy_WalksAndStopsAtDefender, Enemy_KillsDefender, Enemy_Breach, ArmorBreak, Status_SlowReducesMovement |
| Wave completion | Waves_StartAfterPrep_AdvanceWhenCleared_ThenWin, Waves_Timeout, LaneSelection |
| Win/loss | Waves_…ThenWin, Loss_WhenHeartrootFalls_AndStops |
| Pause and resume | Advance_AccumulatesFixedSteps_PauseIsNoAdvance; autoplay pause round-trip |
| Pool reset | Pools_ResetStateOnReturn |
| Save/load and migration | SaveTests (7 tests) |
| Determinism | Determinism_SameSeedSameOutcome, SeededRandom_* |
| Encounter validation / balance | Validator_FlagsBrokenEncounters, ShippedContentTests (winnable on 5 seeds, lost undefended, ids unique, art present) |
| Presentation stability | Spring_IsStableAtAnyFrameTime (regression: units vanished at low FPS) |
| Not yet applicable | Boss phases (M3), mutation stacking and expedition nodes (M5), director budgets (M5) |

## Linux autoplay checks
Plays the encounter by tapping cards and cells through the camera and collecting pickups by screen tap. It pauses and resumes once, verifying time scale and that the simulation froze. It screenshots the menu, start, combat, final wave, pause and result screens. It fails on any logged error, and on any tap that did not plant at the intended cell.

## Manual / device tests
See `Docs/ANDROID_TEST_CHECKLIST.md`. Device matrix: Samsung Galaxy S23 Ultra (ARM64, high tier, any time), TCL Tab 10 (tablet aspect; **only after 21:00**), Redmi A3 (ARMv7 32-bit, PowerVR, low tier, the release blocker).

## Manual checks still to do (not automatable here)
- Drag-and-drop planting from a card; touch target comfort; long sessions for thermals.
- Audio mix on phone speakers; music loop seams.
- Android back button, app backgrounding (auto-pause), kill and relaunch with progress intact.
