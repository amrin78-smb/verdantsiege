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
