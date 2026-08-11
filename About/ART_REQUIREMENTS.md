# Aquaculture Fishing Art Requirements

This is the release inventory for every Aquaculture player-facing visual Def.
It records ownership, current provenance, and the release consequence. The
full texture/rotation briefs remain in `About/VISUAL_ASSET_MATRIX.md`.

| Def or asset | Current source | Required production asset | Status | Provenance / license record |
| --- | --- | --- | --- | --- |
| `AF_FishingRod` / `FishingRod.png` | Dedicated mod texture | Keep the existing transparent rod and validate icon scale/tackle overlays | PASS | Mod-owned file; author/license confirmation still needs to be recorded before publication |
| `AF_FishFeed` | Vanilla `Things/Item/Resource/Kibble` | Dedicated fish-feed icon | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_FishMeat` | VFE Cod icon | Dedicated generic cleaned-fillet icon | BLOCKED | Optional dependency-owned path; no dedicated Aquaculture art |
| `AF_FishEgg` | Vanilla small bird egg | Aquatic egg cluster icon | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_DuckweedCulture`, `AF_SeaLettuceCulture`, `AF_DaphniaCulture`, `AF_BrineShrimpCulture`, `AF_PondSnailCulture`, `AF_FreshwaterMusselCulture`, `AF_BlueMusselCulture` | Vanilla herbal-medicine icon with tint | Seven distinct culture-container icons | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_PondLifeSamplingStation` | Vanilla butcher spot | Sampling table, net, trays, and water vessel | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_FishingBucket` | Vanilla fermenting barrel | Four directional bucket textures | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_FishingBarrel` | Vanilla fermenting barrel | Four directional large water-barrel textures | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_PondFeeder` | Vanilla fermenting barrel | Four directional covered manual hopper textures | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_AutomaticPondFeeder` | Vanilla fermenting barrel | Four directional powered hopper textures | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_AquaticPlantCover` | Vanilla grass | Dedicated floating aquatic plant cover | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_PondRockShelter` | Vanilla collapsed rocks | Fitted underwater rock shelter | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_PreparedPondSubstrate` | Vanilla rubble rock | Deliberate gravel-bed texture | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_PondAerator` | Vanilla vent | Four directional underwater aerator textures | BLOCKED | Vanilla borrowed path; no dedicated Aquaculture art |
| `AF_Pond` / `AF_PondProxy` | Terrain plus procedural pond visuals | Validate water, shoreline, blueprint, snow/damage, and zoom behavior | NOT RUN | Procedural runtime visuals; no static proxy texture |

`AF_Duckweed`, `AF_SeaLettuce`, `AF_Daphnia`, `AF_BrineShrimp`,
`AF_PondSnails`, `AF_FreshwaterMussels`, and `AF_BlueMussels` are simulation
records, not spawned Things, and do not require textures. Fish species supplied
by dependencies remain dependency-owned and are outside this asset inventory.

Release rule: do not substitute placeholders or silently point new Defs at
unlicensed/borrowed art. The current asset gate is BLOCKED until the dedicated
assets above are supplied, reviewed in all rotations/overlays, and their
provenance/licenses are recorded.
