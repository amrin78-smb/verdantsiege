# Test Plan

## How to run
| What | Command | Notes |
|---|---|---|
| Regenerate content | `Tools/setup.sh` | Run after model/script changes. Exit 0 = content valid. |
| Blender models | `Tools/Blender/build_all.sh` | Regenerates and checks all 27 models (~5 min). |
| EditMode tests | `Tools/run_tests.sh EditMode` | Results: `Logs/editmode-results.xml` |
| Linux autoplay | `Tools/autopilot.sh 1920x1080 2400x1080:90 2048x1536` | Plays with the same ScriptedPlayer as the balance tests. Env: `VS_ENCOUNTER` (e.g. `vh_07` boss), `VS_STRATEGY=good|none`, `VS_SPEED`, `SKIP_BUILD=1`. Reports and screenshots: `Logs/AutoPilot/` |
| Android APK | `Tools/build_android.sh [release]` | Builds, then `Tools/verify_apk.sh` checks the ABIs, PowerVR filter, orientation and permissions. |
| On device | `Docs/ANDROID_TEST_CHECKLIST.md` | Mandatory for each release candidate. |

## Automated coverage (EditMode, 115 tests + 1 explicit tuning report)
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
| M3 defenders | DefenderAbilityTests: Thorncaster pierce through armour (2 targets), Whirlseed ricochet count + falloff, Storm Orchid chain targets across adjacent lanes + mechanical stun, Frost Fern chill stacks then freeze on the 4th hit, Burrowroot hidden/trigger/edible-while-rearming/rearm, Mender Moss 3x3 heal radius + one cleanse per pulse, Mirrorleaf lure (adjacent lane only) + Sapper target preference, Mirrorleaf spit reflection, Snapjaw devour/recovery/non-devourable bite |
| M3 enemies | EnemyAbilityTests: Vaultling vaults exactly one defender, Spore cloud damage/slow and burning by fire, Burrower untargetable underground (trap still hits) then surfaces, Howler haste duration, Sapper stops at range and spits (no bites), Glider ignores blockers and only anti-air hits it (breach 1), Mimic copies once, Warden aura reduction (not itself, ends on death), Herald speed rule while alive |
| Boss | BossTests: arrival/untargetable while rising, phase transition at threshold never skipped + invulnerable roar, vulnerability window multiplier, telegraph-then-strike timing on the most crowded column (refund, blocked cell), blocked cells released with original terrain, summons + defeat withers enemies and wins, projectiles in any lane hit it |
| Shipped content | ShippedContentTests: all 7 encounters (incl. the boss) validate, are won by the shared ScriptedPlayer on 5 seeds within 2.5..9 min, and are lost undefended; 12 defenders / 12 enemies / 2 elites present; roster <= 8. `BalanceReport` (explicit, `-testFilter VerdantSiege.Tests.ShippedContentTests.BalanceReport`) logs win rate, length, breaches by enemy and cards used over 8 seeds |
| Living Evolution | EvolutionTests (23): XP per role (damage dealt without overkill, damage absorbed, Vitality produced, healing done, trap triggers; other roles earn nothing from the wrong source), readiness event once + XP cap + passive growth only after the first wave, atomic purchase (not ready / not enough Vitality / invalid option / double tap leave everything unchanged, exact spend), no evolution after death or without branches, chooser state machine (open on ready tap, switch, close elsewhere, deny keeps open, success closes, closes on death), one test per branch type: fan multishot, charge-shot override + slower rhythm + piercing, Glacial bonus vs chilled, lob cluster + Wildfire side-lane splash, aura adjacency (diagonal linked, 2 lanes away not) + Vitality per link + links vanish with the source, Abundant interval, Capacitor store/cap/release x1.5, thorns/regen/snare, trap rearm + Bramble Web across lanes with root, Wide Bloom reach, Tempest/Thunderhead chain overrides, Grasping Vines reach + anti-air + Quick Digest, Hall of Mirrors lure reach; determinism with the bot evolving. ShippedContentTests: every defender has 2-3 branches with unique ids, cost 0-75, one-line descriptions; the balance test bot evolves (`ScriptedPlayer.DecideEvolution`). `BalanceReport` also logs evolutions chosen and time-to-ready percentiles |
| Not yet applicable | Mutation stacking and expedition nodes (M5), director budgets (M5) |

## Linux autoplay checks
Plays the encounter by tapping cards and cells through the camera and collecting pickups by screen tap. It pauses and resumes once, verifying time scale and that the simulation froze. It screenshots the menu, start, combat, final wave, pause and result screens, plus M3 moments when they occur (boss arrival/telegraph/Core exposed/roots/phases, elite, Vaultling crouch, burrow trail, glider, spore cloud). It fails on any logged error, and on any tap that did not plant at the intended cell. Living Evolution: the harness evolves through the real tap path (tap the ready plant, the chooser opens, then the option the shared ScriptedPlayer picked) and screenshots the ready badge, the chooser, the evolution moment, the evolved look and aura links; the report counts evolutions.

## Manual / device tests
See `Docs/ANDROID_TEST_CHECKLIST.md`. Device matrix: Samsung Galaxy S23 Ultra (ARM64, high tier, any time), TCL Tab 10 (tablet aspect; **only after 21:00**), Redmi A3 (ARMv7 32-bit, PowerVR, low tier, the release blocker).

## Manual checks still to do (not automatable here)
- Evolution on a phone: badge readability and touch size, chooser card size vs lanes, evolved silhouettes (1.12x + ring + accessory) at a glance, evolution SFX in the mix, Capacitor tap discoverability.
- M3 readability on a phone screen: telegraphs (Vaultling crouch, Howler sac, Sapper canister glow, Mimic shimmer, boss cell warnings), status tints, 8-card rail touch targets.
- Human difficulty of vh_03..vh_07 (the bot wins them; whether they feel fair and varied needs play-testing).
- Drag-and-drop planting from a card; touch target comfort; long sessions for thermals.
- Audio mix on phone speakers; music loop seams.
- Android back button, app backgrounding (auto-pause), kill and relaunch with progress intact.
