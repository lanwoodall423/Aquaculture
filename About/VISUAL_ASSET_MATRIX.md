# Aquaculture Visual Asset Matrix

This is the visual-identity inventory for the RimWorld 1.6 content in this mod. The owner has waived the dedicated production-art requirement for the current candidate, so missing art remains specified below rather than replaced with placeholder PNGs. No production art is generated or changed by that waiver. The only mod-owned production texture currently present is `FishingRod.png`.

## Conventions

- Buildings use transparent 128x128 PNG tiles at one tile per image. Directional `Graphic_Multi` assets use `_north`, `_east`, `_south`, and `_west` variants and keep the existing north/east/south/west camera convention.
- Item and product icons use transparent 128x128 PNGs. The existing fishing rod remains the established 500x500 item source and is scaled by its Def draw size.
- Use hard-edged pixel clusters with restrained vanilla RimWorld shading: one dark outline, a readable midtone silhouette, one light-facing highlight, and no text or lettering in the texture.
- Keep transparent padding around the silhouette so minification does not create a dark box. Do not bake snow, damage, blueprint, ghost, fuel, or power overlays into the base art.
- Building assets should remain legible at zoomed-out game scale. Color is a secondary identifier; shape and function must remain readable in grayscale.

## Current Matrix

| Def | Current graphic | Footprint / rotation | Current visual source | Identity priority | Production asset brief |
|---|---|---|---|---|---|
| `AF_FishingRod` | `Graphic_Single` | Item; no directional set | **Owned:** `Textures/Things/Item/Equipment/FishingRod.png` | Existing dedicated identity | Keep the existing 500x500 transparent rod. Validate alpha, stack/trade icon scale, and tackle overlays. |
| `AF_FishFeed` | `Graphic_Single` | Item; stack 75 | Vanilla Kibble | High | `Textures/Things/Item/Resource/Aquaculture/FishFeed.png`, 128x128. A tied paper pouch or shallow pellet scoop with a fish-shaped feed mark; muted tan, green, and blue. |
| `AF_FishMeat` | `Graphic_StackCount` | Item; stack product | VCEF Cod | High | `Textures/Things/Item/Resource/Aquaculture/FishMeat.png`, 128x128. A small cleaned fillet on a pale board, red-pink center, dark edge, no species-specific face. |
| `AF_FishEgg` | `Graphic_StackCount` | 1x1 map item; `MapMeshOnly` | Vanilla small bird egg | High | `Textures/Things/Item/Resource/Aquaculture/FishEgg.png`, 128x128. Cluster of blue-green translucent eggs with a subtle inner fry silhouette; readable as aquatic without text. |
| `AF_DuckweedCulture` | `Graphic_StackCount` | Item; stack 10 | Herbal medicine, green tint | High | `Textures/Things/Item/Resource/Aquaculture/DuckweedCulture.png`, 128x128. Clear vial or shallow culture jar with floating green discs. |
| `AF_SeaLettuceCulture` | `Graphic_StackCount` | Item; stack 10 | Herbal medicine, sea-green tint | High | `.../SeaLettuceCulture.png`, 128x128. Wide jar with folded teal fronds and visible water line. |
| `AF_DaphniaCulture` | `Graphic_StackCount` | Item; stack 10 | Herbal medicine, rose tint | High | `.../DaphniaCulture.png`, 128x128. Small glass culture tube with pale water and clustered pink-brown specks. |
| `AF_BrineShrimpCulture` | `Graphic_StackCount` | Item; stack 10 | Herbal medicine, orange tint | High | `.../BrineShrimpCulture.png`, 128x128. Saltwater jar with orange suspended specks and a short cork. |
| `AF_PondSnailCulture` | `Graphic_StackCount` | Item; stack 10 | Herbal medicine, brown tint | High | `.../PondSnailCulture.png`, 128x128. Low jar with two distinct spiral shells and dark substrate. |
| `AF_FreshwaterMusselCulture` | `Graphic_StackCount` | Item; stack 10 | Herbal medicine, slate tint | Medium | `.../FreshwaterMusselCulture.png`, 128x128. Blue-gray mussel shells in a clear freshwater jar. |
| `AF_BlueMusselCulture` | `Graphic_StackCount` | Item; stack 10 | Herbal medicine, deep-blue tint | Medium | `.../BlueMusselCulture.png`, 128x128. Dark blue mussel shells with a saltwater highlight and visible water line. |
| `AF_PondLifeSamplingStation` | `Graphic_Single` | 1x1; station facing is interaction-side only | Vanilla ButcherSpot | High | `Textures/Things/Building/Aquaculture/PondLifeSamplingStation.png`, 128x128. Low wooden sampling table, dip net, two trays, and a blue water vessel; asymmetrical silhouette communicates sampling. |
| `AF_FishingBucket` | `Graphic_Multi` | 1x1; four directions | Vanilla FermentingBarrel | High | Four files `.../FishingBucket_north.png`, `_east.png`, `_south.png`, `_west.png`, 128x128 each. Open pail with water surface, rope handle, and one visible fin; smaller and taller than a feeder. |
| `AF_FishingBarrel` | `Graphic_Multi` | 1x1; four directions | Vanilla FermentingBarrel | High | Four files `.../FishingBarrel_north.png`, `_east.png`, `_south.png`, `_west.png`, 128x128 each. Large reinforced water barrel with broad rim, sloshing water, and drain tap; visibly wider than the bucket. |
| `AF_PondFeeder` | `Graphic_Multi` | 1x1; four directions | Vanilla FermentingBarrel | Critical | Four files `.../PondFeeder_north.png`, `_east.png`, `_south.png`, `_west.png`, 128x128 each. Covered manual hopper with hinged lid, short feed chute, and hand lever; warm wood/iron palette. |
| `AF_AutomaticPondFeeder` | `Graphic_Multi` | 1x1; four directions | Vanilla FermentingBarrel | Critical | Four files `.../AutomaticPondFeeder_north.png`, `_east.png`, `_south.png`, `_west.png`, 128x128 each. Squat steel hopper with motor housing, power cable, feed auger, and one amber status lamp; unmistakably distinct from manual feeder. |
| `AF_AquaticPlantCover` | `Graphic_Random` | 1x1; current random plant silhouette | Vanilla Grass, green tint | High | `Textures/Things/Building/Aquaculture/AquaticPlantCover.png`, 128x128. Dense floating leaves and stems in shallow water; no grass blades or ground soil. |
| `AF_PondRockShelter` | `Graphic_Single` | 1x1; non-directional shelter | Vanilla CollapsedRocks | High | `Textures/Things/Building/Aquaculture/PondRockShelter.png`, 128x128. Three fitted rocks forming a visible dark underwater cavity; readable as refuge, not rubble. |
| `AF_PreparedPondSubstrate` | `Graphic_Random` | 1x1; current random floor scatter | Vanilla RubbleRock, brown tint | High | `Textures/Things/Building/Aquaculture/PreparedPondSubstrate.png`, 128x128. Deliberate gravel bed with woody pockets and a clean oval footprint; not loose rubble. |
| `AF_PondAerator` | `Graphic_Multi` | 1x1; four directions | Vanilla Vent, blue tint | Critical | Four files `Textures/Things/Building/Aquaculture/PondAerator_north.png`, `_east.png`, `_south.png`, `_west.png`, 128x128 each. Waterproof pump head, visible intake grate, short pipe into water, and blue current fins; no HVAC vent silhouette. |
| `AF_PondProxy` | `drawerType=None` | 1x1 proxy; pond visuals are procedural | Runtime `PondVisuals` | Existing intentional runtime art | No static texture. Preserve procedural water, shoreline, ripples, reeds, caustics, and per-pond meshes. Validate proxy selection/blueprint behavior separately. |

`AF_PondCultureBase` is abstract and has no spawned ThingDef of its own. Pond organism Defs such as `AF_Duckweed` and `AF_Daphnia` are simulation records, not Things, and therefore require no texture.

## Def Wiring Plan

Do not point a Def at any brief path until the corresponding reviewed PNG exists. Once art is supplied, replace only the relevant `<texPath>` while preserving current component, footprint, stack, and research behavior. `Graphic_Multi` directional files must use the exact lower/upper-case path shown above. The existing fishing rod is already correctly wired and is the only dedicated asset that can be integrated now.

## Validation Matrix

- Missing texture: clean startup must report no missing-texture errors; deliberately remove a test asset only in a disposable copy and confirm the Def loader reports the exact path.
- Rotation: inspect all four directions for bucket, barrel, manual feeder, automatic feeder, and aerator; confirm interaction cells and shadows remain aligned.
- Blueprint/ghost: place and preview every building, including transparent ghost, costs, rotation, and minified Thing icon.
- Damage/snow/power overlays: damage each building, expose it to snow where applicable, drain feeders, and toggle power without baked-in art hiding overlays.
- Minification and stacks: inspect bucket/barrel/feeder minified icons, feed/culture/meat/egg stack counts, trade dialogs, storage slots, and bill products.
- Color application: verify culture and habitat tinting affects only intended base art and does not recolor water highlights or silhouettes into ambiguity.
- Case-sensitive paths: compare every Def `texPath` and directional suffix against the production filenames on a case-sensitive filesystem.
