# Verdant Siege: project rules

- Source of truth: `Docs/Design/` GDD + master prompt (owner supplied). Status: `PROJECT_STATUS.md` (keep honest), decisions: `TECHNICAL_DECISIONS.md`, tests: `TEST_PLAN.md`.
- Unity 6000.3.27f2, URP 17.3, Input System, uGUI+TMP. Android landscape, offline. Only one Unity process may open the project at a time.
- Code: `Assets/_Game/Scripts/Core` (VerdantSiege.Core: pure simulation, data, save; no scene code), `Scripts/Game` (presentation, UI, app), `Scripts/Editor` (generators, build), tests in `Assets/_Game/Tests`.
- Definitions are immutable ScriptableObjects; runtime state lives on pooled runtime objects. Gameplay never depends on VFX/animation.
- Generated (edit the generator, not the output): 3D models (`Tools/Blender`, outputs in `Art/`), materials, fonts, UI sprites, prefabs, content data, audio, scenes (`Tools/setup.sh` → `ProjectSetup`).
- Android: IL2CPP **ARMv7 + ARM64**, PowerVR Vulkan deny filter (Redmi A3). Verify every APK with `Tools/verify_apk.sh`.
- Commands: `Tools/setup.sh`, `Tools/run_tests.sh [EditMode]`, `Tools/autopilot.sh [WxH[:inset]...]`, `Tools/build_android.sh [release]`, `Tools/build_linux.sh`, `Tools/Blender/build_all.sh`.
- Test devices: S23 Ultra (wireless adb, any time), TCL Tab 10 (only after 21:00), Redmi A3 (32-bit target).
- Never claim a build, test or device result without evidence.
