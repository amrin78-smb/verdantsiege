# Art progress log

## 2026-11-16 — Phase A: smoothness pass (owner note on 0.1.0: "make graphics smoother curves")
**Status: done, ready to import.** All 27 models were regenerated; `check_models.py` (independent FBX re-import) → **27/27 pass**.
Contract unchanged: same names, part names, pivots, facing, `Col` sRGB + alpha emission, one `VS_Vertex` material, same FBX settings.

What changed:
- Kit (`vskit.py`): smooth-shaded primitives get more segments (`RES`, default 1.6×, size-aware so tiny details stay cheap):
  lathes/spheres/tubes/leaves/shells, Bezier paths. Optional Catmull-Rom profile rounding for hero lathes (Sunspore pod,
  Glowcap cap, Heartroot trunk, mushroom caps). Default "sharp edge" angle 35° → 48°, so curved surfaces shade smoothly
  while plate rims and chamfers stay crisp.
- New soft primitives: `soft_blob` (rounded lumpy foliage/moss masses), `pebble` (smooth rocks), per-vertex colouring with
  smooth noise (`paint_vertex_normals`, `foliage_soft`, `vnoise`) instead of per-face colour, so smooth surfaces have no facet seams.
- Characters: smooth plates (Bulwark, Brute armour, Skitter shell), soft moss/fungal lumps, smooth Skitter legs.
- **Heartroot redesigned**: 1.76 × 2.91 × 3.39 m (was 2.15 × 4.73), 6 soft thick buttress roots, rounded lush canopy
  biased toward +Y so the sightline from the HeartCore to a ~54° camera on −Y stays clear (canopy underside ≥ 2.3 m).
  The hollow + HeartCore face +X, turned 30° toward −Y and tilted up. **HeartCore pivot moved** to (0.188, −0.108, 0.889)
  (was (0.25, 0, 0.95)); Canopy pivot (0, 0, 2.3) (was 2.05). Check `Art/Previews/_game/Heartroot_game.png`.
- **GroundTileA/B redesigned**: soft domed lawn with a rounded rolled edge (0.05 m at the rim, 0.062 m centre; blades/flowers
  reach 0.11 m), smooth colour variation, clover and one tiny flower. 356 tris.
- Props: bushes and tree canopies are soft blobs; TreeA is a branching oak, TreeB a smooth layered pine; rocks are smooth
  pebbles; ruin/edge stones have rounded chamfers; stone path slabs rounded. Sunmote orb smooth.
- Budgets updated in `model_specs.json` (defenders 4000, enemies 4500, Brute 6000, Heartroot 12000, trees 3500, props 1800,
  Cottage 3000, tiles 400). Per-model `vskit.RES` overrides keep Drifter (1.3), Brute (1.35), Sunspore (1.5), Bulwark (1.3) in budget.

| Model | Tris | Model | Tris |
|---|---:|---|---:|
| Sunspore | 3700 | Heartroot | 10495 |
| BulwarkBloom | 3422 | GroundTileA/B | 356 |
| Glowcap | 2852 | LaneEdge | 424 |
| Drifter | 4410 | TreeA / TreeB | 2632 / 1329 |
| Skitter | 3142 | Cottage | 2796 |
| PlatedBrute | 5964 | Rocks A/B/C | 1144 / 1716 / 572 |

Icons (`Art/Icons/*_icon.png`) and previews re-rendered. Known: hairline plate-overlap seams on Bulwark remain (sub-pixel on phones).

## 2026-11-16 — Phase B: branding art
**Status: done.** Files in `Art/Branding/` (see its README):
- `icon_foreground.png` (1024², transparent; Sunspore + Glowcap leaning around a glowing heart-seed, all inside the central 66 % — alpha is softly masked beyond r = 36 %),
- `icon_background.png` (1024², opaque radial moss → sunlit gradient with subtle leaves),
- `icon_legacy.png` (1024², composite with 8 % rounded corners, transparent outside the corners),
- `logo.png` (2048×1024, transparent; 3D Lilita One wordmark "VERDANT / SIEGE", gold→sunlit face, cream rim, dark-green outline, sprout + heart-seed motif, warm glow + drop shadow; checked on `#142A1E`).
Generators: `Tools/Blender/branding.py` (Blender renders) and `Tools/Blender/branding_compose.py` (PIL).

## 2026-11-16 — Phase C (part 1): Milestone 3 defenders
**Status: done, ready to import** (`Tools/Blender/model_roster_defenders.py`). All face +X, origin at ground centre, `Col` + alpha emission, icons in `Art/Icons/<Name>_icon.png`.

| Model | Tris | Size X×Y×Z | Parts (pivot) |
|---|---:|---|---|
| Thorncaster | 3358 | 0.59×0.71×0.81 | Stem, Head (neck −0.02,0,0.50), LeafL, LeafR, Muzzle (empty under Head, at the thorn-bolt tip) |
| SnapjawVine | 3880 | 0.58×0.58×0.72 | Stem, Jaw (hinge −0.08,0,0.47; modelled slightly open, rotate about local Y to open up/forward), JawLower (same hinge), LeafL, LeafR |
| FrostFern | 3476 | 0.65×0.69×0.55 | Stem (icy crown), Fronds (pivot 0,0,0.14; sway), Crystal (pivot 0,0,0.15), Muzzle (under Crystal) |
| EmberLily | 2220 | 0.38×0.64×0.73 | Stem, Bloom (pivot at bloom base 0.03,0,0.50; faces +X tilted up), Core (same pivot, molten stamens), Muzzle (under Core) |
| Whirlseed | 2608 | 0.57×0.65×0.70 | Stem, Rotor (spin about local Z at 0,0,0.62), Pod (0.08,0,0.36; launcher), Muzzle (under Pod) |
| MenderMoss | 3228 | 0.62×0.60×0.59 | Mound, Orbs (pivot 0,0,0.45; bob/pulse), Bloom (pivot 0,0,0.2) |
| Burrowroot | 2792 | 0.78×0.78×0.69 | Ring (dirt + roots, 0.07 m high), Sprout (0,0,0.05), Spikes (pivot at ground 0,0,0) |
| StormOrchid | 2130 | 0.33×0.59×0.85 | Stem, Petals (pivot 0.05,0,0.60), Rod (same pivot; copper rod + glowing tip), Muzzle (under Rod) |
| Mirrorleaf | 1644 | 0.30×0.54×0.80 | Stem, Mirror (pivot at base −0.02,0,0.18; silver-green face toward +X), LeafL, LeafR |

Note **Burrowroot Spikes are modelled in the ERUPTED pose** (0.45–0.7 m tall) with the pivot on the ground, so the game hides them with
localScale.z ≈ 0 (or sinks them) and scales/slides them up when the trap triggers.

## 2026-11-16 — Phase C (part 2): Milestone 3 enemies + boss, and final validation
**Status: done, ready to import.** `Tools/Blender/build_all.sh` (all models + branding) → **BUILD_ALL: PASS**, `check_models.py` → **46/46 pass**.
Scripts: `model_roster_enemies.py`, `model_rootmaw.py`. Icons for every model in `Art/Icons/` (25 files). Enemies face −X (their LEFT is −Y).
Pivots are model-space positions; `⊂Body` = child of Body (follows its animation), otherwise child of the root.

| Model | Tris | Size X×Y×Z | Parts (pivot) |
|---|---:|---|---|
| Vaultling | 3370 | 0.63×0.52×0.97 | Body (0.06,0,0.50); Head (−0.14,0,0.80) ⊂Body; ArmL/R (−0.12,∓0.13,0.68) ⊂Body; LegL/R hips (0.06,∓0.13,0.50) — long folded hind legs with glowing spring coils |
| SporeCarrier | 3428 | 0.79×0.61×1.18 | Body (0,0,0.32); Sac (0.12,0,0.88) ⊂Body (pulse about its centre); Head (−0.20,0,0.50) ⊂Body; LegL/R (0,∓0.14,0.36) |
| Burrower | 4004 | 0.89×0.58×0.59 | Body (0.22,0,0; segmented arch + dirt mound); Head (−0.06,0,0.44) ⊂Body (drill snout); ArmL/R (−0.04,∓0.10,0.34) ⊂Body |
| Howler | 3980 | 0.60×0.56×1.39 | Body (0.04,0,0.62); Head (−0.10,0,1.15) ⊂Body; Sac (−0.23,0,1.08) ⊂Body (throat sac, inflate); ArmL/R (−0.03,∓0.17,1.08) ⊂Body; LegL/R (0.04,∓0.09,0.62) |
| Sapper | 3720 | 0.80×0.55×1.13 | Body (0.05,0,0.42); Head (−0.20,0,0.78) ⊂Body; Canister (0.20,0,0.82) ⊂Body; ArmL/R (−0.12,∓0.20,0.74) ⊂Body; LegL/R (0.05,∓0.11,0.42) |
| Glider | 2828 | 0.57×0.87×0.58 (z 0.70–1.28) | Body hovers at (0.02,0,1.00); Head (−0.11,0,1.02) ⊂Body; WingL/R shoulders (0.02,∓0.07,1.04) ⊂Body (flap about local X). Root on the ground; no geometry near the ground (spec `grounded: false`) |
| Warden (elite) | 4044 | 0.96×1.02×1.58 | Body (0.03,0,0.62); Head (−0.02,0,1.25) ⊂Body; ArmL/R (0,∓0.25,1.15) ⊂Body; LegL/R (0.03,∓0.11,0.62); Shield grip (−0.22,−0.30,0.68) under root; Aura ring r 0.36–0.48 m at the feet (0,0,0) |
| BlightHerald (elite) | 3376 | 0.69×0.69×1.95 | Body (0.03,0,0.60; robe); Head (−0.02,0,1.14) ⊂Body (crown); ArmL/R (0,∓0.20,1.02) ⊂Body; LegL/R (0.03,∓0.10,0.60) (mostly hidden by the robe); Banner grip (−0.23,0.26,0.67) under root — staff to 1.95 m |
| Mimic | 2426 | 0.91×0.90×0.75 | Body (0,0,0.38; iridescent blob); Eyes (−0.30,0,0.45) ⊂Body (4 crystal eyes); Tendrils (0,0,0.05) ⊂Body |
| **RootMaw (boss)** | 13771 | 2.77×4.70×2.88 | Body (0.35,0,0); Jaw hinge (−0.05,0,1.00) ⊂Body (modelled 12° open; rotate about local Y to open); Core (−0.35,0,1.30) ⊂Body (throat weak point, lit lime — visible when the jaw opens); Eye (−0.80,0,2.12) ⊂Body (magenta weak point); RootL/R shoulders (0.10,∓0.78,1.75) ⊂Body (grasping arms reaching to x ≈ −1.3, ground contact) |

Notes for the Unity side:
- Warden `Shield` and Herald `Banner` are children of the ROOT at the hand grip; if the arm animates, re-parent them to ArmL / ArmR at runtime (or keep arm motion small).
- Spec limits I raised (documented in `model_specs.json`): Warden 1.05 m wide / 1.6 m tall, Herald 2.0 m tall (banner), Mimic and Burrower 1.0 m long.
- Phase A follow-up done in this pass: Drifter and Plated Brute skins are now painted per-vertex (no facet seams on the limbs); kit primitives `soft_blob`/`pebble` scale their resolution with size.
- All Phase A/B/C outputs were regenerated by the final `build_all.sh` run, so file contents may differ slightly from the earlier per-phase drops.

## 2026-11-16 — RootMaw weak points (task 1) — READY
`Art/Models/Bosses/RootMaw.fbx` re-exported (icon + previews re-rendered). Existing parts unchanged in name/pivot
(Body, Jaw, RootL, RootR, Eye, Core); added three emissive pods, each **a child of Body with its pivot at the pod centre**
(Blender model space, metres; the boss root is at the field's centre lane):

| Part | Pivot (X, Y, Z) | Lane |
|---|---|---|
| `WeakPoint0` | (−0.72, −2.00, 0.78) | near (−Y, camera side) |
| `WeakPoint1` | (−0.78, 0.00, 0.66) | middle (under the jaw) |
| `WeakPoint2` | (−0.72, +2.00, 0.78) | far (+Y) |

Each pod: bulbous magenta sac (≈0.40 × 0.48 × 0.52 m) with alpha-emissive magenta → pink gradient, six lime glow spots and
dark veins, cradled by root tendrils; the side pods sit on new raised root knuckles (part of Body) at the near/far lanes.
All three are readable from the ~54° camera (`Art/Previews/_game/RootMaw_game.png`). Boss now 12,647 tris (≤ 14,000; the
script uses `vskit.RES = 1.3`), 2.77 × 4.71 × 2.88 m. `model_specs.json` lists the new parts; `check_models.py` passes.

## 2026-11-16 — Projectiles (task 2) — READY
New script `Tools/Blender/model_fx.py`. Files `Art/Models/FX/<Name>.fbx` (+ `.blend`, preview). Each = root + `Mesh`,
flies along **+X**, origin at the **centre**, ≤ 200 tris (spec `grounded: false`).

| Name | Tris | Size X×Y×Z | Look / emission |
|---|---:|---|---|
| `Thorn` | 116 | 0.35×0.12×0.12 | dark-green barbed thorn bolt, 6 swept-back barbs; tip faintly glows (alpha 0.35) |
| `FrostShard` | 54 | 0.30×0.08×0.08 | faceted pale-cyan ice shard + 3 small side spikes; partial glow (alpha 0.15–0.5) |
| `EmberSeed` | 114 | 0.31×0.12×0.12 | charred seed with molten orange crack bands (alpha 1) and an ember tail |
| `WhirlSeed` | 108 | 0.10×0.28×0.06 | maple-like double-winged seed, wings across Y (≈0.3 m wide), soft glow on the wing tips |
| `SapperSpit` | 198 | 0.27×0.15×0.14 | lumpy green→yellow corrosive glob with trailing droplets, fully emissive (alpha 0.75–1) |

## 2026-11-16 — Evolution accessories (task 3) — READY
Same script; files in `Art/Models/FX/`; root + `Mesh`; **origin at the attachment point** (base, z = 0; geometry above it), ≤ 600 tris.

| Name | Tris | Size X×Y×Z | Attach / look |
|---|---:|---|---|
| `EvoSolar` | 560 | 0.35×0.34×0.10 | on top of a head: gold band (inner r 0.07) with 10 sun-ray petals and 5 glowing gems; rays glow toward the tips |
| `EvoPrism` | 152 | 0.24×0.24×0.23 | on a head/top: dark base with 5 faceted prisms (cyan, violet, rose), soft partial glow |
| `EvoRoot` | 578 | 0.75×0.75×0.07 | ground ring around a defender's base (origin = ground centre, ring r ≈ 0.34): wavy vine, 8 leaves, 8 lime glow beads |
| `EvoBloom` | 578 | 0.33×0.33×0.10 | on top: open upward flower halo, 9 outer + 6 inner pink petals with glowing tips, gold glowing ring and stamens |

Previews in `Art/Previews/` (also in `Art/Previews/_contact_sheet.png`, 55 models). `model_specs.json` has all new entries;
`check_models.py` → **55/55 pass**. `build_all.sh` now also runs `model_fx.py`.

## 2026-11-16 — Biome B "The Emberwild" environment kit — READY (not yet integrated in Unity)
New script `Tools/Blender/model_emberwild.py` (registered in `build_all.sh` and `model_specs.json`); `check_models.py` → **70/70 pass**.
All 55 earlier models were regenerated from an untouched copy of the scripts and re-checked: identical tris / size / glow-corner counts
(`vskit.py` and the other model scripts are unchanged). Same contract as the Heartlands kit: metres, Z up, root at the ground centre,
one `VS_Vertex` material, `Col` sRGB with **alpha = emission** (lava, molten seams, glowing roots, ember tips), `.fbx` + `.blend`.
Look-dev: `Art/emberwild_contact.png` (all previews) and `Art/emberwild_diorama.png` (3-lane field from ~the battle camera, with
existing defenders/enemies/Heartroot for scale; regenerate with `blender -b --factory-startup --python Tools/Blender/diorama_emberwild.py`).

Palette: basalt `#3B3436`→`#8A7A74`, ash `#A39890`/`#D6CCBE`, rust/terracotta `#7A3420` `#B0502C` `#DD8448`, charred wood `#2A2120`→`#55423A`,
obsidian `#241E30`→`#7C6A9C`, lava (emissive) `#B8301C` → `#F25A1C` → `#FF9A2E` → `#FFE07A`, ember foliage `#6E2216`→`#FFB04E`;
tiles A `#A06A4C`/`#C48A62`, B `#7A4E40`/`#9A6650`.

| Model | Folder | Tris | Size X×Y×Z | Notes |
|---|---|---:|---|---|
| EmberTileA / B | Battlefield | 320 / 320 | 1.00×1.00×0.08 | Same footprint/profile as GroundTileA/B (domed top 0.062, rolled rim 0.05). Warm ash soil with soft ash drifts and scorched patches, one short half-buried glowing root vein, two cinders |
| EmberLaneEdge | Battlefield | 1227 | 1.05×0.46×0.31 | Replaces LaneEdge (1 m along X): basalt hex columns with ash tops, glowing root along the base, ember pebbles, ash grass |
| EmberLavaChannel | Battlefield | 820 | 1.00×0.88×0.17 | 1 m segment along X, **tiles seamlessly** (all variation periodic in X); rounded basalt banks, flowing lava ribbon (lava top z ≈ 0.04), 4 floating crust plates; bank and lava ends are capped |
| EmberVentCell | Battlefield | 841 | 0.92×0.92×0.14 | Unsafe-cell hazard. Parts: `Crust` (heaved basalt plates, hot undersides) and `Fissure` (molten layer + vent bubbles), both pivot (0,0,0). Put the root on the tile top (z ≈ 0.06). Hide/pulse `Fissure` (or its emission) for the dormant/active state |
| EmberRockA | Props | 1770 | 1.25×0.94×0.69 | Split basalt boulder around a molten core + companion stone |
| EmberRockB | Props | 849 | 0.98×0.91×1.09 | Basalt column cluster, ash tops, ember glow at the foot |
| EmberRockC | Props | 445 | 0.63×0.56×0.66 | Obsidian shard cluster (faceted), molten base |
| EmberRoots | Props | 1712 | 1.23×1.21×0.34 | Six charred roots arching from a knot, molten seam along their backs, two glowing pods |
| EmberFlowerCluster | Props | 1713 | 0.54×0.63×0.39 | Five flame-cup ember-blooms (petals glow toward the tips) on olive stems, ash grass, cinders |
| EmberGrassTuft | Props | 620 | 0.36×0.39×0.36 | Ash grass with glowing ember tips |
| EmberLavaPool | Props | 1610 | 1.50×1.47×0.21 | Domed lava pool in a lumpy basalt rim, crust islands, bubble |
| EmberTreeA | Props | 2912 | 3.04×2.56×3.22 | Charred oak (`Trunk`, glowing bark cracks) + ember-red canopy (`Canopy`, pivot (0,0,1.73)), glowing ember fruit |
| EmberTreeB | Props | 1329 | 2.61×2.63×4.08 | Cinder pine: charcoal tiers, ash-dusted tops, smouldering rims (`Trunk`, `Canopy` pivot (0,0,0.79)) |
| EmberForge | Props | 2925 | 3.70×3.25×1.79 | Landmark: banded terracotta beehive kiln, glowing mouth facing **−Y**, smouldering chimney, molten-seamed flagstone apron, anvil + hot ingot, charred logs, roots |

Notes / follow-ups:
- No Emberwild Heartroot variant (the existing Heartroot reads fine on the Emberwild field; see the diorama). Optional later.
- EmberLavaChannel crust plates repeat every metre when tiled; a second segment variant would break that up if it shows in game.
- The glow is strong in Cycles previews (emission × 3); tune the Unity VertexLit emission intensity for this biome if lava over-blooms on device.
- `contact_sheet.py` now accepts optional name globs (`contact_sheet.py out.png Ember*`); with no globs it behaves as before.
