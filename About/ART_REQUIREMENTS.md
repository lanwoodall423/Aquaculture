# Aquaculture Fishing Art Requirements

This is the release inventory for every Aquaculture player-facing visual Def.
It records ownership, current provenance, and the release consequence. The
full texture/rotation briefs remain in `About/VISUAL_ASSET_MATRIX.md`.

## Owner waiver — 2026-08-14

The repository owner explicitly waives the dedicated production-art
requirement for this candidate. No production art is being generated or
changed as part of this waiver. Existing vanilla, VFE, dependency-owned, and
procedural sources remain as-is. `WAIVED` is a release-scope decision; it does
not claim ownership of third-party art or relicense it under Aquaculture's
GPL-3.0-or-later license.

| Def or asset | Current source | Required production asset | Status | Provenance / license record |
| --- | --- | --- | --- | --- |
| `AF_FishingRod` / `FishingRod.png` | Dedicated mod texture | Keep the existing transparent rod and validate icon scale/tackle overlays | PASS | Mod-owned file; covered by `GPL-3.0-or-later`, Copyright (C) 2026 lanwoodall423 |
| `AF_FishFeed` | Vanilla `Things/Item/Resource/Kibble` | Dedicated fish-feed icon | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_FishMeat` | VFE Cod icon | Dedicated generic cleaned-fillet icon | WAIVED | Optional dependency-owned path; no dedicated Aquaculture art; VFE terms remain authoritative |
| `AF_FishEgg` | Vanilla small bird egg | Aquatic egg cluster icon | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_DuckweedCulture`, `AF_SeaLettuceCulture`, `AF_DaphniaCulture`, `AF_BrineShrimpCulture`, `AF_PondSnailCulture`, `AF_FreshwaterMusselCulture`, `AF_BlueMusselCulture` | Vanilla herbal-medicine icon with tint | Seven distinct culture-container icons | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_PondLifeSamplingStation` | Vanilla butcher spot | Sampling table, net, trays, and water vessel | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_FishingBucket` | Vanilla fermenting barrel | Four directional bucket textures | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_FishingBarrel` | Vanilla fermenting barrel | Four directional large water-barrel textures | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_PondFeeder` | Vanilla fermenting barrel | Four directional covered manual hopper textures | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_AutomaticPondFeeder` | Vanilla fermenting barrel | Four directional powered hopper textures | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_AquaticPlantCover` | Vanilla grass | Dedicated floating aquatic plant cover | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_PondRockShelter` | Vanilla collapsed rocks | Fitted underwater rock shelter | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_PreparedPondSubstrate` | Vanilla rubble rock | Deliberate gravel-bed texture | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_PondAerator` | Vanilla vent | Four directional underwater aerator textures | WAIVED | Vanilla borrowed path; no dedicated Aquaculture art; game-owned terms remain authoritative |
| `AF_Pond` / `AF_PondProxy` | Terrain plus procedural pond visuals | Validate water, shoreline, blueprint, snow/damage, and zoom behavior | NOT RUN | Procedural runtime visuals; no static proxy texture |

`AF_Duckweed`, `AF_SeaLettuce`, `AF_Daphnia`, `AF_BrineShrimp`,
`AF_PondSnails`, `AF_FreshwaterMussels`, and `AF_BlueMussels` are simulation
records, not spawned Things, and do not require textures. Fish species supplied
by dependencies remain dependency-owned and are outside this asset inventory.

Release rule: do not substitute placeholders or silently point new Defs at
unlicensed/borrowed art. For this candidate, the owner waiver removes the
dedicated production-art requirement as a release blocker. Existing borrowed
assets still retain their original owners' terms, and no third-party asset is
relicensed by `LICENSE`.
