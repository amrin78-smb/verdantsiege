# VERDANT SIEGE
## Game Design Document (GDD)
**Version:** 1.0  
**Engine:** Unity 6  
**Initial platform:** Android  
**Visual format:** 2.5D  
**Genre:** Lane-based tactical defence / strategy / roguelite progression  
**Working title:** Verdant Siege

---

# 1. High Concept

Verdant Siege is a premium-looking 2.5D lane-defence strategy game in which players cultivate, position and evolve living defenders to protect settlements, ancient ecosystems and mysterious world-seeds from an adaptive invading force known as the Blight.

The game takes the instantly understandable pleasure of lane-based defence and expands it with:
- Deep unit roles, synergies and tactical formation decisions.
- Multi-biome campaigns with unique battlefield rules.
- Branching expeditions and roguelite choices.
- Persistent progression, unlockable species and meaningful upgrades.
- Enemy factions with readable behaviours and counterplay.
- Boss encounters that transform the battlefield.
- Daily/weekly challenges, endless survival and custom scenarios.
- A world that visibly changes as the player restores it.

The game must be easy to understand in its first minute but continue revealing new systems for dozens of hours.

**Design principle:** Familiar controls, unfamiliar situations. The player should always recognise the core game, but should rarely feel that they are solving exactly the same encounter twice.

This is an original intellectual property. Do not copy Plants vs. Zombies characters, art, names, animations, level layouts, music, UI, or proprietary content. Use the broad genre inspiration only.

# 2. Product Vision and Pillars

## 2.1 Vision
Create a highly replayable strategy game with the visual quality and production values of a polished premium mobile title, while retaining fast sessions and intuitive touch controls.

## 2.2 Pillars
1. **Instant readability:** Players can understand lanes, threats, ranges, resources and unit roles at a glance.
2. **Strategic expression:** Multiple viable compositions and tactical answers; no single dominant formation.
3. **Constant novelty:** Biomes, map rules, enemy combinations, events, mutations, bosses and branching choices.
4. **Satisfying feedback:** Beautiful attacks, readable impacts, expressive defenders, strong audio and clear victories.
5. **Fair challenge:** Losses should teach. Threats must be telegraphed and counterplay should be available.
6. **Respect player time:** Short encounters, quick restart, no forced waiting, no energy system and no pay-to-win.
7. **Expandable architecture:** Content-driven systems should make adding units, enemies, maps and modifiers straightforward.

# 3. Audience and Platform

- Primary: strategy, tower-defence and roguelite players.
- Secondary: casual players attracted by colourful characters and approachable controls.
- Initial target: Android phones and tablets.
- Performance target: scalable from entry-level 32-bit Android devices to modern flagship devices.
- Unity: Unity 6, URP.
- Build both ARMv7 (32-bit) and ARM64 (64-bit) where the Unity/package configuration supports the target device. Do not assume every device supports every graphics feature.
- Landscape orientation.
- Offline-first single-player. Online services are optional future additions, not required for the core game.

# 4. Core Game Structure

## 4.1 Battlefield
The battlefield is a 2.5D scene viewed from a fixed elevated camera. It uses a grid of lanes and columns, with perspective, depth, lighting and 3D scenery.

Recommended starting configuration:
- 5 combat lanes.
- 9 placement columns.
- One player-side deployment boundary.
- One enemy entry boundary.
- Each cell supports one primary defender unless a unit explicitly uses another placement rule.
- Some maps add elevated platforms, water channels, bridges, tunnels, hazards or destructible terrain.

The camera remains fixed during standard combat. Small scripted camera movements may be used for boss introductions, map reveals and victory moments, but must never obscure active gameplay.

## 4.2 Core Loop
1. Select an expedition, mission or challenge.
2. Review the battlefield, enemy intelligence and special conditions.
3. Select a limited roster of defenders.
4. Enter combat and collect the primary resource.
5. Deploy defenders, manage cooldowns and react to threats.
6. Survive waves and adapt to changing enemy behaviours.
7. Defeat the encounter's final threat.
8. Earn seeds, research materials, relics and restoration resources.
9. Choose upgrades, unlock new species or improve the home sanctuary.
10. Begin a new encounter with different conditions and strategic opportunities.

## 4.3 Session Length
- Tutorial encounter: 2–4 minutes.
- Standard encounter: 4–8 minutes.
- Elite encounter: 7–12 minutes.
- Boss encounter: 10–15 minutes.
- Endless mode: player-defined duration.

Avoid artificially extending sessions with slow animations or mandatory waits.

# 5. Controls and User Experience

## 5.1 Touch Controls
- Tap a defender card to select it.
- Tap a valid battlefield cell to deploy.
- Tap an occupied defender to inspect its stats and abilities.
- Drag a selected card to a cell as an optional alternative.
- Tap ability buttons to activate skills.
- Tap a resource pickup to collect it, with optional accessibility setting for automatic collection.
- Long-press or use an inspect button for detailed information.

## 5.2 Interaction Requirements
- Large touch targets.
- Clear selected-card state.
- Valid placement cells highlighted.
- Invalid placement gives immediate feedback.
- Undo deployment during a short configurable grace period, unless combat rules prohibit it.
- Pause, speed controls and settings accessible without covering the battlefield.
- Never rely on hover.
- Support common aspect ratios and safe areas.

## 5.3 Accessibility
- Adjustable game speed.
- Reduced screen shake and flash effects.
- Distinct silhouettes and colour-independent status indicators.
- Text scaling.
- Optional auto-collect resources.
- Optional simplified damage numbers.
- Remappable or configurable ability placement where practical.
- Avoid requiring precise rapid tapping for the main campaign.

# 6. Resources and Economy

## 6.1 Combat Resource: Vitality
Vitality is generated by selected defenders, battlefield objects and certain events. It is spent to deploy units and activate some abilities.

Rules:
- Starts at a modest amount.
- Has a configurable cap.
- Passive generation is controlled by map rules and selected units.
- Pickups remain visible for a limited but generous duration.
- Resource production is strategically vulnerable where appropriate.
- Never make the player wait passively for long periods.

## 6.2 Persistent Resources
- **Seeds:** common progression currency, earned from missions.
- **Essence:** rarer material used for advanced research and evolution.
- **Relic fragments:** used to unlock and improve relics.
- **Sanctuary materials:** used to restore the home sanctuary and cosmetic environment.
- **Research notes:** earned through discoveries, optional objectives and bestiary progress.

All currencies must have clear purposes. Avoid excessive currency fragmentation.

# 7. Defender System

Defenders are living species with roles, costs, attack patterns, strengths, weaknesses, upgrade paths and optional mutations.

## 7.1 Core Roles
- **Artillery:** ranged damage, projectiles, splash attacks.
- **Guardian:** durable front-line units that block or disrupt enemies.
- **Generator:** creates Vitality or other tactical resources.
- **Control:** slows, roots, pushes, stuns or redirects enemies.
- **Assassin:** high single-target damage, ambushes or back-line attacks.
- **Support:** buffs, heals, shields, cleanses or improves nearby units.
- **Trap:** hidden, triggered or area-denial effects.
- **Specialist:** terrain interaction, anti-air, anti-armour, anti-swarm or other niche counters.

A unit can occupy more than one role, but its main purpose must be clear.

## 7.2 Launch Roster: Initial 12 Defenders
These are working concepts. Names, visuals and values may be refined during production.

1. **Sunspore** — Basic ranged attacker. Fires reliable seed bolts down its lane.
2. **Bulwark Bloom** — High-health guardian that blocks enemies and protects units behind it.
3. **Glowcap** — Generates Vitality over time and can store a bonus burst.
4. **Thorncaster** — Heavy piercing projectile; effective against armoured targets.
5. **Snapjaw Vine** — Close-range ambusher that devours or heavily damages a single enemy, then enters recovery.
6. **Frost Fern** — Applies chill and slows groups; can freeze enemies after repeated hits.
7. **Ember Lily** — Area damage and burn effects; can ignite oil, spores or environmental hazards.
8. **Whirlseed** — Launches bouncing or ricocheting projectiles, effective against clustered enemies.
9. **Mender Moss** — Repairs nearby defenders and removes selected status effects.
10. **Burrowroot** — Underground trap that erupts when enemies pass; strong against fast units.
11. **Storm Orchid** — Chains electrical damage between enemies and can disable mechanical units.
12. **Mirrorleaf** — Creates temporary decoys or reflects selected projectiles.

Each defender requires:
- Unique silhouette and readable idle/attack/hit/death states.
- Deployment cost and cooldown.
- Health, damage, attack interval, range and target rules where applicable.
- Clear strengths and counters.
- Sound identity.
- Upgrade and evolution definitions.
- Tooltips written in plain language.

## 7.3 Defender Growth
Defenders improve through:
- Account-level research.
- Individual mastery earned by using them.
- Branching evolution choices.
- Temporary expedition mutations.
- Relic synergies.

Do not make progression purely numerical. New mechanics and trade-offs are more interesting than endlessly increasing damage.

## 7.4 Evolution Branches
At defined mastery milestones, a defender may choose one of two specialisations. Example:
- Sunspore: **Rapid Bloom** (higher firing rate) or **Piercing Bloom** (shots penetrate additional targets).
- Frost Fern: **Deep Freeze** (stronger control) or **Glacial Shards** (damage when chilled enemies are hit).
- Glowcap: **Abundant** (more frequent generation) or **Capacitor** (stores energy for a deployable burst).

Evolution choices should be reversible in the sanctuary for a modest, non-premium cost or through a free loadout system.

# 8. Enemy System

Enemies must have distinct silhouettes, movement patterns, attack tells and counterplay. Difficulty should come from combinations, timing and battlefield conditions rather than inflated health alone.

## 8.1 Launch Enemy Archetypes
1. **Drifter:** basic walking enemy.
2. **Skitter:** fast, fragile runner.
3. **Plated Brute:** slow, armoured front-liner.
4. **Vaultling:** leaps over the first defender or obstacle, with a clear wind-up.
5. **Spore Carrier:** releases a damaging or disabling cloud on defeat.
6. **Burrower:** travels underground and emerges further along a lane.
7. **Howler:** buffs nearby enemies or calls reinforcements.
8. **Sapper:** targets and damages placed defenders or environmental objects.
9. **Glider:** uses elevated paths and requires anti-air or special targeting.
10. **Mimic:** temporarily copies a limited property of a nearby enemy or battlefield object.
11. **Warden:** elite unit with a protective aura.
12. **Blight Herald:** wave commander that changes the encounter rules while alive.

## 8.2 Enemy Design Rules
- Every special ability has a readable telegraph.
- Every elite enemy has at least one intended counter.
- Enemies should not ignore core placement rules without clear warning.
- Avoid unavoidable damage.
- Enemy combinations should create tactical puzzles.
- Use a data-driven resistance and status-effect system.

## 8.3 Factions
The Blight is not one uniform enemy type. Each region reveals a different manifestation:
- **Rotbound:** organic, fungal, swarm-based.
- **Hollowed:** spectral and evasive.
- **Ironroot:** corrupted machines and armour.
- **Ashen:** fire, smoke and aggressive rushes.
- **Deepwild:** aquatic, burrowing and environmental control.

Factions introduce new mechanics and visual identities while reusing robust underlying systems.

# 9. Worlds, Biomes and Campaign

The campaign is a sequence of regions connected through a world map. Each region contains standard missions, optional challenges, elite encounters and a boss.

## 9.1 Launch Biomes
### A. Verdant Heartlands
- Lush meadows, old stone paths, streams and ruined cottages.
- Teaches core lanes, resource generation and basic enemy types.
- Environmental feature: sunlight pools that periodically boost selected cells.

### B. The Emberwild
- Volcanic soil, glowing roots, ashfall and lava channels.
- Heat cycles change unit behaviour.
- Some cells become temporarily unsafe.
- Fire units may gain bonuses, while frost units have altered effectiveness.

### C. Frostveil Tundra
- Snow-covered ruins, frozen lakes and aurora-lit forests.
- Snowdrifts can slow enemies and defenders.
- Ice may crack under heavy units.
- Wind changes projectile travel or accuracy on selected maps.

### D. Sunken Kingdom
- Overgrown temples, flooded courtyards and submerged structures.
- Water lanes, bridges and floating platforms.
- Some defenders require aquatic adaptation or special placement.
- Tides alter the battlefield during combat.

### E. The Hollow Canopy
- Giant trees, hanging roots, luminous spores and vertical depth.
- Elevated enemy routes and shifting platforms.
- Light and shadow affect selected units and hazards.

### F. The Blighted Crown
- A corrupted endgame region that combines previously learned mechanics.
- Dynamic battlefield transformations.
- Elite enemy combinations and multi-phase bosses.

Each biome needs a distinct colour palette, lighting setup, ambient soundscape, music identity, environmental hazards, enemy variants and visual landmarks.

## 9.2 Mission Types
- Standard defence.
- Survival: endure escalating waves.
- Elimination: defeat a target before a deadline.
- Escort: protect a moving world-seed or convoy.
- Harvest: collect and protect resources.
- Siege: defend multiple objectives.
- Boss hunt.
- Puzzle defence: restricted roster or unusual battlefield rule.
- Rescue: free trapped creatures or restore structures during combat.
- Rift event: survive a sequence of changing rules.
- Gauntlet: consecutive encounters with limited recovery.

# 10. Replayability and "Never Get Bored" Systems

No game can literally guarantee endless novelty. The design should instead combine authored content with controlled procedural variation and a healthy content update structure.

## 10.1 Expedition Mode
After the campaign introduces the systems, players can enter branching expeditions.

Each expedition contains:
- A map path with multiple route choices.
- Combat, elite, event, rest, shop and boss nodes.
- A limited number of encounters.
- Temporary upgrades and mutations.
- A final biome boss or expedition guardian.

At each branch, show enough information for a meaningful decision without revealing every detail.

## 10.2 Mutation Draft
After selected encounters, offer a choice of one of three temporary mutations. Examples:
- **Overgrowth:** defenders gain range but cost more Vitality.
- **Symbiosis:** adjacent defenders share a portion of healing.
- **Volatile Sap:** defeated enemies leave an explosive residue.
- **Moonlit Bloom:** support units become stronger at night.
- **Root Network:** connected defenders share a small defensive bonus.
- **Wild Harvest:** resource pickups are larger but less frequent.
- **Last Stand:** defenders gain power as their health falls.
- **Prismatic Spores:** projectiles gain a random elemental effect.

Mutations must create trade-offs and combinations, not only unconditional power increases.

## 10.3 Relic System
Relics are passive run modifiers earned through bosses, events and exploration.

Examples:
- **Heartseed:** increases starting Vitality.
- **Glass Thorn:** boosts damage but reduces defender health.
- **Old Gardener's Bell:** improves cooldown recovery after a perfect wave.
- **Tidal Lens:** enhances water-based units and changes one terrain rule.
- **Ashen Crown:** fire damage spreads, but fire hazards last longer.

Limit equipped relics to create meaningful builds. Relics should have clear descriptions and avoid opaque probability manipulation.

## 10.4 Dynamic Encounter Director
Create a controlled system that selects encounter compositions using:
- Player progression and unlocked knowledge.
- Biome and mission type.
- Recent enemy combinations.
- Player-selected difficulty.
- A bounded threat budget.
- Special event rules.
- Anti-repetition memory.

The director must never create impossible encounters. It should use validated encounter templates and budgets, not unconstrained random spawning.

Avoid repeating the same encounter pattern too frequently. Track recent waves, enemy archetypes, modifiers and map features.

## 10.5 World Events
Examples:
- Meteor shower temporarily changes terrain.
- A travelling merchant appears between waves.
- A wandering giant crosses the battlefield.
- A rare seed creature must be protected.
- An eclipse changes the strength of selected units.
- A blight storm introduces a timed hazard.
- A lost guardian offers a powerful but risky blessing.

Events must be optional or clearly telegraphed where they impose risk.

## 10.6 Endgame Modes
- Endless defence with escalating but bounded difficulty.
- Daily seeded challenge: identical conditions for all players.
- Weekly themed expedition.
- Boss rush.
- Draft mode: random roster selection.
- One-life challenge.
- Community scoreboards as a future online feature.
- Custom challenge builder as a later feature.

# 11. Boss Encounters

Bosses should change the tactical problem, not simply have enormous health.

Launch boss concepts:
1. **The Root Maw:** grows underground roots that alter cells and attack selected columns.
2. **Cinder Colossus:** ignites lanes and periodically changes safe zones.
3. **The Drowned Regent:** raises and lowers water levels, changing available placement.
4. **The Pale Matriarch:** summons illusions and divides into lesser threats.
5. **The Crown of Rot:** multi-phase final encounter that corrupts terrain, summons faction champions and forces the player to adapt.

Boss design requirements:
- Multiple phases with visible transitions.
- Telegraphs before major attacks.
- Distinct weak points or tactical windows.
- More than one viable counter strategy.
- Clear phase progress and readable health.
- Checkpoint or retry options appropriate to difficulty.
- Boss-specific music and environmental effects.

# 12. Progression and Meta Game

## 12.1 The Sanctuary
The Sanctuary is the player's persistent home base and visual progression hub.

Restoration areas:
- Seed Conservatory: unlock and research defenders.
- Relic Archive: inspect and improve relics.
- Bestiary: view discovered enemies and counters.
- World Map: select regions and expeditions.
- Training Grounds: test compositions.
- Habitat: discover cosmetic creatures and ambient life.
- Ancient Grove: story, lore and long-term objectives.

As the player restores regions, the sanctuary visibly becomes greener, brighter and more populated.

## 12.2 Progression Tracks
- Campaign completion.
- Defender unlocks and mastery.
- Research tree.
- Relic collection.
- Bestiary discoveries.
- Sanctuary restoration.
- Achievements and challenge milestones.

Progress should offer horizontal options, not just power inflation. Earlier content should remain relevant through challenge modifiers and rewards.

## 12.3 Difficulty
- Story: forgiving, teaches mechanics.
- Standard: intended baseline.
- Veteran: more complex compositions and modifiers.
- Nightmare: demanding but fair, with stronger encounter rules.
- Custom: configurable rules for experimentation.

Do not gate basic content behind extreme difficulty.

# 13. Narrative and Worldbuilding

The player is a newly awakened **Keeper of the Verdant Seed**, guided by an ancient companion called **Morrow**, a small sentient seed-spirit.

The Blight is spreading because the world's natural cycle has been broken. Each restored region reveals fragments of the old world and the origin of the corruption.

Narrative delivery:
- Short dialogue scenes between regions.
- Environmental storytelling.
- Boss introductions.
- Bestiary entries.
- Sanctuary discoveries.
- Optional lore objects.

Keep dialogue concise. Gameplay remains the primary focus. Avoid long unskippable exposition.

# 14. Art Direction

## 14.1 Visual Target
Premium stylised 3D with:
- Rich, tactile materials.
- Expressive character silhouettes.
- Beautiful foliage and layered vegetation.
- Atmospheric lighting and volumetric effects used selectively.
- Carefully composed 2.5D battlefields.
- High-quality animation and VFX.
- Strong environmental storytelling.
- Distinct biome identities.

The look should feel polished, warm and imaginative, not flat or cheaply cartoonish. It should be original and not imitate the exact visual design of any existing franchise.

## 14.2 Camera and Composition
- Fixed elevated camera with a slight perspective angle.
- Orthographic or carefully tuned perspective camera; choose through visual and readability tests.
- Battlefield grid remains visually clear.
- Units are 3D models placed on a logical 2D grid.
- Use depth layers for scenery, not to obscure combat.
- Use camera framing that keeps all active lanes visible on phone screens.

## 14.3 Animation
Defenders need:
- Idle.
- Anticipation.
- Attack.
- Recoil/hit.
- Ability.
- Defeat.
- Victory or celebration.

Enemies need:
- Idle/move.
- Attack wind-up.
- Attack.
- Hit reaction.
- Special ability.
- Defeat.

Use animation events or explicit ability timing data carefully. Gameplay logic must not depend solely on an animation event firing.

## 14.4 VFX
- Clear projectile trails.
- Readable hit impacts.
- Elemental status effects.
- Area-of-effect indicators.
- Placement preview.
- Enemy telegraphs.
- Boss phase transitions.
- Resource collection feedback.

Effects must communicate gameplay state. Avoid excessive screen clutter.

# 15. Audio Direction

- Original orchestral/organic fantasy soundtrack.
- Biome-specific ambience and music layers.
- Distinct unit attack and ability sounds.
- Clear audio cues for resource generation, enemy arrivals, elite spawns, boss attacks and wave completion.
- Strong but restrained impact sounds.
- Audio sliders for music, SFX and ambience.
- Mute and reduced-effects options.

Use original or properly licensed assets only.

# 16. Technical Architecture

## 16.1 Recommended Stack
- Unity 6.
- Universal Render Pipeline (URP).
- C#.
- ScriptableObjects for content definitions.
- Addressables only if their complexity and memory cost are justified; initial build may use simpler local asset references.
- Unity Input System or a consistent touch abstraction.
- Unity Audio Mixer.
- Object pooling for frequent projectiles, damage numbers, pickups and VFX.
- JSON or versioned local save data.
- Git with clear asset and code conventions.

## 16.2 Core Systems
1. Game state and scene flow.
2. Grid and lane manager.
3. Placement validation.
4. Defender runtime and targeting.
5. Enemy runtime and path progression.
6. Wave and encounter director.
7. Resource economy.
8. Projectile and damage system.
9. Status effects.
10. Abilities and cooldowns.
11. Boss phase controller.
12. Mutation and relic systems.
13. Campaign and expedition progression.
14. Save/load and migration.
15. UI and accessibility.
16. Audio and VFX orchestration.
17. Performance and device quality settings.

## 16.3 Data-Driven Content
Use ScriptableObjects for:
- Defender definitions.
- Enemy definitions.
- Projectile definitions.
- Status effects.
- Wave definitions.
- Encounter templates.
- Biome definitions.
- Mission rules.
- Relic definitions.
- Mutation definitions.
- Upgrade/evolution definitions.

Separate immutable definitions from runtime state. Do not store mutable health, cooldowns or run-specific modifiers in shared ScriptableObject assets.

## 16.4 Deterministic and Testable Systems
- Use seeded random generation for expeditions and daily challenges.
- Keep combat calculations independent from visual effects.
- Provide debug controls for spawning enemies, granting resources and testing waves.
- Add automated tests for placement, damage, status effects, wave progression, save migration and encounter validation.
- Log errors with enough context to diagnose a failed encounter.

# 17. Performance and Device Compatibility

The game must be designed for a broad Android device range, including entry-level hardware.

Critical requirements:
- Support ARMv7 32-bit and ARM64 64-bit builds where possible.
- Use IL2CPP for production Android builds.
- Verify required Android API levels and package dependencies for both architectures.
- Do not accidentally ship an ARM64-only APK when the intended device requires ARMv7.
- Test on a real 32-bit device, specifically the Redmi A3 target profile from the project's prior mobile game work, rather than relying only on the Unity Editor.
- Confirm the actual ABI of the device before diagnosing installation failures.
- Use URP mobile-friendly settings.
- Limit real-time lights and shadow-casting objects.
- Use baked lighting where appropriate.
- Use LODs and sensible texture sizes.
- Pool projectiles, enemies and effects.
- Avoid per-frame allocations and expensive LINQ in hot paths.
- Avoid excessive transparent overdraw from foliage and particles.
- Use scalable quality profiles.

Suggested initial performance goals (to be validated on target hardware):
- 30 FPS minimum target on low-tier devices.
- 60 FPS target on mid/high-tier devices.
- Stable frame pacing.
- Fast scene transitions.
- No thermal runaway during extended play.
- Memory budget to be profiled early, not guessed.

Quality profiles:
- Low: reduced shadows, particles, foliage density and post-processing.
- Medium: selected shadows and moderate effects.
- High: full intended lighting and effects where supported.

Do not sacrifice battlefield readability to visual effects.

# 18. Monetisation and Ethical Design

Initial release should be a complete, enjoyable game without monetisation dependencies.

If monetisation is considered later:
- Cosmetic-only purchases.
- Optional expansion content.
- No pay-to-win units or stat advantages.
- No energy timers.
- No loot boxes or opaque paid random rewards.
- No forced advertisements.
- No manipulative streaks or child-targeted pressure mechanics.

# 19. Content Roadmap

## Prototype
- One battlefield.
- 3 defenders.
- 3 enemies.
- One resource.
- One complete encounter.
- Touch deployment.
- Win/loss states.

## Vertical Slice
- One polished biome.
- 5 defenders.
- 6 enemies.
- One elite and one boss.
- 10–15 authored encounters.
- Core progression and save/load.
- Production-quality UI, VFX, audio and lighting.
- Performance-tested Android build.

## Early Access / First Full Release Target
- 6 biomes.
- 12+ defenders.
- 12+ enemy archetypes.
- 5 bosses.
- Campaign, expeditions, endless mode and challenge mode.
- Mutations, relics, mastery and sanctuary.
- Tutorial and accessibility options.

## Post-launch Expansion
- Additional biomes and factions.
- More defenders and evolution branches.
- New bosses and expedition events.
- Custom challenge tools.
- Optional online leaderboards.
- Seasonal content that does not invalidate existing progression.

# 20. Definition of Done

A feature is not complete merely because it compiles.

Every gameplay feature must be:
- Functional in an actual playable build.
- Integrated with save/load where relevant.
- Tested on supported screen sizes.
- Tested on target Android hardware.
- Readable and usable with touch.
- Tuned for performance.
- Equipped with appropriate feedback and audio.
- Documented sufficiently for future extension.

# 21. First Playable Acceptance Criteria

The first playable build is successful when:
1. The player can start a mission and understand the battlefield.
2. The player can select and deploy defenders by touch.
3. Vitality generation and spending work correctly.
4. Enemies follow lanes and interact with defenders.
5. Projectiles, damage, status effects and defeat work reliably.
6. Waves progress and the player can win or lose.
7. The UI communicates costs, cooldowns and threats.
8. The game can pause, resume and restart.
9. The game saves and loads basic progression.
10. The Android build installs and runs on the intended 32-bit target device.
11. The experience is stable and readable at the target frame rate.

---

# Appendix A — Initial Balance Guidelines

These are prototype starting points, not final values.

| Unit | Role | Cost | Cooldown | Primary function |
|---|---|---:|---:|---|
| Sunspore | Artillery | 100 | 5 s | Reliable lane damage |
| Bulwark Bloom | Guardian | 125 | 12 s | Blocks and absorbs |
| Glowcap | Generator | 75 | 8 s | Generates Vitality |
| Thorncaster | Artillery | 175 | 10 s | Pierces armour |
| Snapjaw Vine | Assassin | 150 | 18 s | High single-target burst |
| Frost Fern | Control | 150 | 12 s | Slows and freezes |
| Ember Lily | Artillery | 175 | 14 s | Area burn |
| Whirlseed | Artillery | 125 | 9 s | Ricochet damage |
| Mender Moss | Support | 125 | 12 s | Repairs defenders |
| Burrowroot | Trap | 100 | 10 s | Triggered burst |
| Storm Orchid | Specialist | 200 | 16 s | Chain lightning |
| Mirrorleaf | Control | 150 | 15 s | Decoy and reflection |

Prototype resource and damage values must be playtested. Avoid treating this table as final balance.

# Appendix B — Production Priorities

1. Fun and clarity before content volume.
2. A robust simulation before elaborate graphics.
3. One exceptional vertical slice before building six unfinished biomes.
4. Reusable systems before hard-coded one-off behaviours.
5. Real device profiling before assuming performance.
6. Controlled procedural variety before unconstrained randomness.
7. Original art, audio and names throughout.
