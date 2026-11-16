# CLAUDE CODE MASTER DEVELOPMENT PROMPT
## VERDANT SIEGE — Unity 6 / Android / 2.5D

You are the lead game architect, senior Unity engineer, gameplay programmer, technical artist, UI engineer, QA lead and production partner for **Verdant Siege**.

Your assignment is to build a polished, production-minded 2.5D lane-defence strategy game for Android, using Unity 6 and C#.

The accompanying Game Design Document (GDD) is the product source of truth. Read it fully before making changes. The instructions below define how to execute it.

---

# 1. Product Objective

Build an original, visually rich and highly replayable strategy game inspired by the broad lane-defence genre.

Do not make a direct clone of Plants vs. Zombies or any other existing game. Do not reproduce copyrighted characters, art, names, music, level designs, UI or assets. All characters, environments, names, mechanics presentation and audio must be original or properly licensed.

The game must combine:
- Instantly understandable lane-based defence.
- Tactical defender placement and resource management.
- Distinct enemy behaviours and counters.
- Multiple biomes and battlefield rules.
- Boss encounters with changing mechanics.
- Roguelite expeditions, mutations and relics.
- Persistent mastery, research and sanctuary restoration.
- Controlled encounter variation and long-term replayability.

The initial platform is Android. Landscape orientation. Offline-first.

# 2. Mandatory Project Rules

1. Inspect the existing Unity project before changing anything.
2. Preserve working project settings, scenes, assets and code unless there is a clear technical reason to change them.
3. Do not delete or replace existing assets blindly.
4. Do not generate a huge amount of placeholder content and claim the game is complete.
5. Build in small, testable milestones.
6. After each milestone, compile, test and report what works and what remains.
7. Prefer maintainable, modular C# architecture over monolithic scripts.
8. Do not hard-code content that should be data-driven.
9. Never store runtime state in shared ScriptableObject definitions.
10. Never claim a feature is complete unless it is implemented and tested in the actual project.
11. Use original placeholder assets where final art is unavailable. Clearly label placeholders.
12. Keep the game playable at the end of every meaningful milestone.
13. If a design decision is unclear, choose the simplest robust implementation consistent with the GDD and document the decision.
14. Do not stop at producing plans or code snippets. Implement the work in the Unity project.
15. Maintain a `PROJECT_STATUS.md` file with completed work, current state, known issues and next actions.
16. Maintain a `TECHNICAL_DECISIONS.md` file for significant architecture and design decisions.
17. Maintain a `TEST_PLAN.md` file and update it as features are added.

# 3. Mandatory Android Compatibility

This is a critical requirement.

The game must support low-end Android hardware, including 32-bit devices. The project has prior experience with the Redmi A3, which requires a compatible 32-bit build configuration for the target device.

Required actions:
- Inspect the Unity Android build settings.
- Configure IL2CPP for production Android builds.
- Include ARMv7 (32-bit) and ARM64 (64-bit) architectures where supported by the selected Unity version and dependencies.
- Do not ship an ARM64-only build.
- Verify the actual target device ABI using Android tooling or device information where available.
- Build and test an installable APK on the Redmi A3 or equivalent 32-bit Android target.
- Test a separate ARM64 device as well.
- Check every third-party plugin for ABI compatibility.
- Avoid dependencies that silently remove 32-bit support.
- Verify Android API level, minimum OS, target SDK, package identifier and signing configuration.
- Keep a documented build profile for low-end devices.

Use URP and mobile-friendly rendering. Do not assume desktop rendering settings will perform adequately on mobile.

Performance priorities:
- Stable 30 FPS on low-tier hardware as an initial target.
- 60 FPS on capable devices.
- Object pooling for projectiles, enemies, pickups and repeated VFX.
- Sensible texture sizes and compression.
- Limited real-time lights and shadow casters.
- Baked lighting where appropriate.
- LODs for detailed scenery.
- Controlled particle counts and transparent overdraw.
- Avoid unnecessary Update loops, allocations and expensive scene searches.
- Profile on real hardware.

Create three quality profiles: Low, Medium and High. Low must remain visually attractive and preserve gameplay readability.

# 4. First Action: Audit the Project

Before implementation:
1. List the Unity version and installed packages.
2. Inspect the Assets, Scenes, Scripts, Prefabs, Materials, Textures and ProjectSettings structure.
3. Identify current render pipeline and Android build configuration.
4. Identify existing code that can be reused.
5. Identify missing systems and technical risks.
6. Check whether a playable scene already exists.
7. Propose a milestone implementation plan.
8. Record the audit in `PROJECT_STATUS.md`.

Do not overwrite project configuration before understanding it.

# 5. Recommended Architecture

Adapt this architecture to the actual project, but keep responsibilities separated.

Suggested folders:

Assets/
  _Game/
    Art/
      Characters/
        Defenders/
        Enemies/
        Bosses/
      Environments/
      Materials/
      Textures/
      VFX/
    Audio/
      Music/
      SFX/
      Ambience/
    Data/
      Defenders/
      Enemies/
      Projectiles/
      StatusEffects/
      Waves/
      Biomes/
      Missions/
      Relics/
      Mutations/
      Evolutions/
    Prefabs/
      Defenders/
      Enemies/
      Projectiles/
      UI/
      Environment/
      VFX/
    Scenes/
      Boot/
      MainMenu/
      Sanctuary/
      Battle/
    Scripts/
      Core/
      Combat/
      Grid/
      Defenders/
      Enemies/
      Waves/
      Economy/
      Abilities/
      Bosses/
      Progression/
      Expeditions/
      UI/
      Audio/
      Save/
      Performance/
      Utilities/
    UI/
      Fonts/
      Sprites/
      Themes/
    Tests/
      EditMode/
      PlayMode/

Do not create empty folders solely for appearance. Add folders when they have a purpose.

Core architectural requirements:
- Immutable content definitions in ScriptableObjects.
- Runtime instances own health, cooldowns, status effects and temporary modifiers.
- Interfaces or clear base abstractions for damageable, targetable and ability-driven entities.
- A central combat simulation/state owner.
- Grid coordinates independent of world-space transforms.
- Object pools with explicit lifecycle reset.
- Events/signals for UI updates rather than constant polling.
- Save data versioning and migration support.
- Dependency injection or explicit composition roots where helpful, without overengineering.

Avoid a single GameManager that owns every system.

# 6. Gameplay Implementation Requirements

## 6.1 Battlefield and Grid

Implement:
- 5 lanes and 9 columns as the initial standard battlefield.
- Configurable dimensions per map.
- Clear placement cells and visual placement feedback.
- Occupancy tracking.
- Placement validation.
- Terrain types and special cell rules.
- Logical lane coordinates separated from world positions.
- Support for water, bridges, elevated platforms, hazards and blocked cells in later milestones.

The battlefield should use a fixed elevated 2.5D camera. All lanes must remain visible on common phone aspect ratios.

Create a grid visualisation/debug mode.

## 6.2 Defender Deployment

Implement card selection and tap-to-place controls.

Required:
- Defender cards show icon, cost and cooldown.
- Selecting a card highlights valid cells.
- Tapping a valid cell deploys the defender.
- Invalid placement gives clear feedback.
- Optional drag-and-drop placement.
- Inspecting an active defender shows health, role and available ability.
- Deployment cost is checked and deducted atomically.
- Cooldown begins at the correct time.
- Prevent duplicate or invalid deployment caused by rapid taps.
- Provide optional short undo grace period if practical.

Make all touch targets appropriate for mobile.

## 6.3 Combat and Targeting

Implement a robust combat system:
- Health and damage.
- Damage types and resistances.
- Projectile travel and collision.
- Splash and piercing damage.
- Target selection rules.
- Attack intervals and cooldowns.
- Status effects.
- Knockback and control effects.
- Defender defeat and cleanup.
- Enemy arrival and objective damage.

Combat logic must not depend on VFX completion or animation events. Visuals should reflect authoritative gameplay state.

## 6.4 Resource Economy

Implement Vitality:
- Starting amount.
- Maximum capacity.
- Generation over time.
- Pickup spawning.
- Pickup collection.
- Unit deployment spending.
- UI updates.
- Configurable map modifiers.

Provide a debug command to grant or remove resources during development.

## 6.5 Initial Defenders

Implement the first three units first:
1. Sunspore — basic lane projectile attacker.
2. Bulwark Bloom — durable blocker.
3. Glowcap — Vitality generator.

Then implement the remaining launch roster:
4. Thorncaster.
5. Snapjaw Vine.
6. Frost Fern.
7. Ember Lily.
8. Whirlseed.
9. Mender Moss.
10. Burrowroot.
11. Storm Orchid.
12. Mirrorleaf.

Each unit must have:
- Data definition.
- Prefab and runtime component.
- Placement cost and cooldown.
- Health and combat properties.
- Attack/ability behaviour.
- Targeting logic.
- Visual states and feedback.
- Audio hooks.
- UI description.
- Test coverage.

Do not make all defenders variations of the same projectile shooter. Their tactical roles must be meaningfully different.

Use temporary high-quality placeholder models if final assets are not ready, but keep prefab and data structures ready for replacement.

## 6.6 Initial Enemies

Start with:
1. Drifter.
2. Skitter.
3. Plated Brute.

Then add:
4. Vaultling.
5. Spore Carrier.
6. Burrower.
7. Howler.
8. Sapper.
9. Glider.
10. Mimic.
11. Warden.
12. Blight Herald.

Each special ability must have a readable telegraph and a counterplay path.

## 6.7 Wave Director

Implement authored wave definitions first.

Then implement controlled procedural encounter generation using:
- Threat budget.
- Enemy unlock/progression gates.
- Biome.
- Mission type.
- Difficulty.
- Recent encounter history.
- Special modifiers.

Do not use unconstrained random spawning. Validate every generated wave against known budgets and encounter rules.

Track recent patterns to reduce repetition.

## 6.8 Win, Loss and Pause

Implement:
- Wave progress.
- Mission objective.
- Win conditions.
- Loss conditions.
- Pause and resume.
- Restart.
- Return to menu.
- Victory and defeat screens.
- Reward summary.

Ensure time scale, audio and pooled objects recover correctly after pause/restart.

# 7. Replayability Systems

Build these in layers, not all at once.

## 7.1 Expedition Map
Create a branching route map with node types:
- Combat.
- Elite.
- Event.
- Rest.
- Merchant.
- Boss.

A run should contain a configurable number of nodes. Choices should be visible and meaningful.

## 7.2 Mutation Draft
After selected encounters, offer three choices. Implement a data-driven mutation system with duration, stacking rules, effects and trade-offs.

Start with:
- Overgrowth.
- Symbiosis.
- Volatile Sap.
- Moonlit Bloom.
- Root Network.
- Wild Harvest.
- Last Stand.
- Prismatic Spores.

## 7.3 Relics
Implement passive run modifiers, equip limits, rarity only if useful, acquisition and clear descriptions.

Initial relics:
- Heartseed.
- Glass Thorn.
- Old Gardener's Bell.
- Tidal Lens.
- Ashen Crown.

## 7.4 Anti-Repetition
Track recent encounter seeds, enemy compositions, modifiers and map features. Use this history to reduce repetitive patterns while preserving deterministic replay when a seed is supplied.

## 7.5 Modes
Implement in this order:
1. Campaign.
2. Expedition.
3. Endless.
4. Daily seeded challenge.
5. Boss rush.
6. Draft mode.
7. Custom challenge builder (later).

Do not implement online leaderboards until the offline game is stable.

# 8. Biomes and Environment

Build one excellent biome before expanding to six.

Initial vertical slice biome: **Verdant Heartlands**.

It should contain:
- Rich grass and layered vegetation.
- Stone paths and ruined structures.
- Small streams and environmental details.
- Attractive sky and distant scenery.
- Ambient life.
- Clear combat lanes.
- Distinct battlefield boundary.
- Subtle environmental animation.
- Optimised lighting and shadows.

Then build biome configuration support for:
- Emberwild.
- Frostveil Tundra.
- Sunken Kingdom.
- Hollow Canopy.
- Blighted Crown.

Each biome must be configurable through data for:
- Visual theme.
- Lighting.
- Ambient sound.
- Music.
- Terrain rules.
- Hazards.
- Enemy faction.
- Background and scenery.
- VFX palette.

Do not create six visually weak environments just to tick a box. One excellent environment is more valuable than six unfinished ones.

# 9. Bosses

Build boss framework after core combat is reliable.

Bosses:
- Root Maw.
- Cinder Colossus.
- Drowned Regent.
- Pale Matriarch.
- Crown of Rot.

Boss framework must support:
- Multiple phases.
- Phase transitions.
- Telegraphs.
- Special attacks.
- Battlefield changes.
- Weak points or vulnerability windows.
- Boss health UI.
- Retry and checkpoint rules.

Do not implement bosses as normal enemies with huge health values.

# 10. UI and Visual Quality

The UI must look like a polished premium mobile strategy game.

Design:
- Main menu.
- World map.
- Sanctuary.
- Defender collection.
- Defender details and mastery.
- Battle HUD.
- Defender selection cards.
- Resource display.
- Wave indicator.
- Objective display.
- Ability controls.
- Pause menu.
- Victory/defeat.
- Mutation draft.
- Relic inventory.
- Expedition map.
- Settings and accessibility.

UI principles:
- Strong hierarchy.
- Clean typography.
- Large readable touch controls.
- Consistent spacing and iconography.
- Subtle transitions.
- Avoid clutter.
- Do not cover active lanes unnecessarily.
- Use original icons and visual language.

Do not use generic default Unity UI as the final presentation. Temporary UI is acceptable for early mechanics, but visual polish is a milestone requirement.

# 11. Art and Asset Pipeline

Use original assets or assets with clear commercial licensing.

For each asset:
- Record source and licence.
- Keep naming consistent.
- Use sensible pivots and scales.
- Set mobile-appropriate texture compression.
- Create LODs where useful.
- Use shared materials where practical.
- Avoid excessive unique materials.
- Ensure silhouettes read at phone resolution.

If using generated art:
- Keep a consistent art bible.
- Maintain consistent proportions, palette and material language.
- Generate clean source assets without text baked into the image unless intentionally required.
- Do not use AI-generated output as a substitute for rigging, animation, collision setup or gameplay implementation.

Do not use copyrighted game assets or rip content from existing games.

# 12. Audio and Feedback

Implement an audio service with:
- Music.
- SFX.
- Ambience.
- Volume controls.
- Pause/resume behaviour.
- Audio pooling or voice management where appropriate.

Gameplay feedback:
- Defender placement.
- Resource collection.
- Attack and hit.
- Status application.
- Enemy special telegraph.
- Elite arrival.
- Boss phase.
- Wave complete.
- Victory and defeat.

Use temporary sounds only during prototype. Record all final audio licensing.

# 13. Save and Progression

Implement versioned local saves for:
- Campaign progress.
- Unlocked defenders.
- Defender mastery.
- Research.
- Sanctuary restoration.
- Relic collection.
- Settings.
- Achievements.
- Current expedition run where appropriate.

Requirements:
- Safe save writes.
- Corrupt save handling.
- Version migration.
- New game/reset option with confirmation.
- No reliance on network connectivity.
- Test save/load after app termination and restart.

Never store secrets or credentials in local save data.

# 14. Testing Requirements

Create EditMode and PlayMode tests.

At minimum test:
- Grid coordinate conversion.
- Valid/invalid placement.
- Occupancy.
- Resource spending and refund.
- Defender cooldown.
- Projectile hit.
- Damage and resistance.
- Status effect duration and stacking.
- Enemy movement and lane progression.
- Wave completion.
- Win/loss state.
- Pause and resume.
- Pool reset.
- Boss phase transitions.
- Mutation stacking rules.
- Expedition node progression.
- Save/load and migration.
- Encounter director budget validation.

Add a repeatable manual test checklist for Android.

Test on:
- Unity Editor.
- Low-tier Android.
- 32-bit ARMv7 device.
- ARM64 device.
- Different aspect ratios and screen densities.

# 15. Milestone Execution Plan

Do not skip ahead. Complete and validate each milestone before beginning the next major one.

## Milestone 0 — Project Audit and Technical Foundation
Deliver:
- Project audit.
- Architecture plan.
- Android compatibility report.
- Folder structure.
- PROJECT_STATUS.md.
- TECHNICAL_DECISIONS.md.
- TEST_PLAN.md.

Acceptance: existing project understood, no destructive changes, build configuration risks identified.

## Milestone 1 — Core Playable Prototype
Implement:
- Battle scene.
- 5 x 9 grid.
- 3 defenders.
- 3 enemies.
- Vitality economy.
- Placement.
- Combat.
- Waves.
- Win/loss.
- Pause/restart.
- Basic mobile UI.

Acceptance: one complete playable encounter using touch controls.

## Milestone 2 — Vertical Slice Polish
Implement:
- Verdant Heartlands environment.
- Better 3D models and materials.
- Camera and lighting.
- Animation.
- VFX and audio.
- Production-style battle UI.
- Tutorial.
- Quality settings.

Acceptance: a polished 5–10 minute experience that looks and feels like a real game, not a prototype.

## Milestone 3 — Content Expansion
Implement:
- Remaining defenders.
- Remaining initial enemies.
- More wave templates.
- Elite enemies.
- First boss.

Acceptance: meaningful roster variety and multiple viable strategies.

## Milestone 4 — Campaign and Progression
Implement:
- World map.
- Campaign missions.
- Persistent rewards.
- Research.
- Defender mastery.
- Evolution choices.
- Sanctuary.

Acceptance: progress survives application restart and unlocks meaningful options.

## Milestone 5 — Roguelite Expedition
Implement:
- Branching route map.
- Mutation draft.
- Relics.
- Events.
- Merchant/rest nodes.
- Encounter director and anti-repetition.

Acceptance: multiple expeditions can produce materially different strategies and outcomes.

## Milestone 6 — Biome Expansion and Bosses
Implement:
- Additional biomes.
- Terrain modifiers.
- Faction mechanics.
- Remaining bosses.
- Environmental storytelling.

Acceptance: each biome changes tactical play, not just background scenery.

## Milestone 7 — Endgame and Content Systems
Implement:
- Endless.
- Daily seeded challenge (offline seed first).
- Boss rush.
- Draft mode.
- Achievements.
- Bestiary.
- Custom challenge foundations.

Acceptance: repeatable endgame loops with controlled difficulty and rewards.

## Milestone 8 — Android Optimisation and Release Candidate
Implement:
- Low/Medium/High profiles.
- Memory and CPU profiling.
- Pooling review.
- Device-specific fixes.
- Build signing configuration.
- ABI validation.
- Install and play tests.
- Final bug fixing.

Acceptance: stable Android release candidate, including ARMv7 compatibility for the intended 32-bit target.

# 16. Required Working Method

For each milestone:

1. State the scope and acceptance criteria.
2. Inspect relevant existing files.
3. Implement the smallest coherent vertical slice.
4. Compile and fix all errors.
5. Run relevant automated tests.
6. Run or describe exact manual play tests.
7. Verify no previous functionality regressed.
8. Update PROJECT_STATUS.md, TECHNICAL_DECISIONS.md and TEST_PLAN.md.
9. Report changed files and any required Unity Editor actions.
10. State remaining limitations honestly.

Do not make unrelated changes while fixing a bug.

If Unity Editor automation is unavailable:
- Create the required scripts and assets safely.
- Give exact Editor setup instructions.
- Do not claim the scene or prefab has been created unless it actually exists.
- Prefer editor scripts that create repeatable assets and scenes when safe.

# 17. Definition of Quality

The finished game should:
- Feel responsive and satisfying.
- Have readable combat at phone size.
- Offer meaningful tactical choices.
- Have distinct defenders and enemy behaviours.
- Introduce new mechanics at a deliberate pace.
- Make each biome memorable.
- Provide strong boss encounters.
- Have long-term progression without excessive grind.
- Run on low-end Android hardware.
- Support 32-bit and 64-bit Android architectures where technically supported.
- Be maintainable and expandable.

A feature is not finished because a class exists. It is finished when it works in the game, has feedback, is tested and is integrated with progression and UI where relevant.

# 18. First Response Required From You

Begin with **Milestone 0 — Project Audit and Technical Foundation**.

Do not start by generating hundreds of scripts.

First inspect the project and report:
1. Current project structure and Unity version.
2. Existing systems and reusable assets.
3. Rendering pipeline and Android configuration.
4. ARMv7/ARM64 status and risks.
5. Proposed architecture.
6. Exact Milestone 1 implementation plan.
7. Any blockers that require my decision.

After the audit, proceed with Milestone 1 unless a genuine blocker requires clarification.

The goal is not to produce the largest amount of code. The goal is to build a genuinely excellent game, one validated milestone at a time.
