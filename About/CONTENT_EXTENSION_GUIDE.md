# Aquaculture - Fishing: Content Extension

Aquaculture discovers loaded fish at startup. A `ThingDef` is treated as a fish when it belongs to `VCEF_RawFishCategory` or `Fish`, or has the Aquariums fish comp. No assembly reference is required for ordinary fish packs.

Every discovered fish automatically appears in Fishing settings with a Novice minimum expertise. Players may raise that requirement without an XML patch; progression and settings remain keyed by the fish `ThingDef.defName`.

External fishing rods can opt in by adding `CompProperties_FishingRodTackle` and `FishingRodExtension` to the rod def. New tackle and lure options are ordinary `FishingTacklePartDef` records, including their slot, performance factors, Max Fish Mass offsets, costs, work, skill, and research. Rod and Line slots may change Max Fish Mass; Rod slots never change Reel Time. Fish packs may add `FishFishingExtension` for freshwater/saltwater lure attraction, while actual fish mass comes from the loaded Mass stat and individual size-related traits.

Tackle `costs` use RimWorld's keyed `ThingDefCountClass` XML form. The element name is the material `ThingDef` and its value is the count:

```xml
<costs>
  <Steel>20</Steel>
  <ComponentIndustrial>1</ComponentIndustrial>
</costs>
```

Do not use `<li>` entries for this field; RimWorld 1.6 interprets an `li` entry's complete text as the requested `ThingDef` name.

## Fishing Rule Contract

Fishing has four expertise ranks: Novice, Adept, Expert, and Master. Species access requires a present species in the shared natural-water ledger and a rank meeting that species' configured minimum. Animals skill, lure attraction, and species knowledge do not bypass either requirement.

Cast is a fixed 180-tick phase. Wait uses the rod's aggregate Wait Time factor, expertise time reduction, and the global fishing-duration setting. Reel uses the aggregate Reel Time factors from every non-Rod tackle slot, expertise time reduction, and the same duration setting. Lure and species knowledge affect attraction weighting and bite chance, not Cast duration or Reel duration. Catch resolution uses species knowledge, expertise, Animals skill, and the rod's Catch Chance factor for bite/escape calculations; Max Fish Mass is a separate eligibility limit.

## Species Metadata

Add `AquacultureFishing.AquaticSpeciesExtension` to a fish `ThingDef` when the inferred profile is not appropriate:

```xml
<modExtensions>
  <li Class="AquacultureFishing.AquaticSpeciesExtension">
    <overrideDiet>true</overrideDiet>
    <diet>Carnivore</diet>
    <overrideWater>true</overrideWater>
    <waterKind>Saltwater</waterKind>
    <minimumTemperature>8</minimumTemperature>
    <maximumTemperature>28</maximumTemperature>
    <foodDemandFactor>1.2</foodDemandFactor>
    <foodValueFactor>1.4</foodValueFactor>
    <cruiseSpeedFactor>1.15</cruiseSpeedFactor>
    <maximumSpeedFactor>1.25</maximumSpeedFactor>
    <turnRateFactor>0.85</turnRateFactor>
    <schoolingFactor>1.1</schoolingFactor>
    <cohesionFactor>1.1</cohesionFactor>
    <alignmentFactor>1.2</alignmentFactor>
    <separationFactor>1</separationFactor>
    <neighborRadiusFactor>1</neighborRadiusFactor>
    <restFrequencyFactor>0.8</restFrequencyFactor>
    <lifespanFactor>1.25</lifespanFactor>
    <breedingIntervalFactor>1.1</breedingIntervalFactor>
    <offspringFactor>0.8</offspringFactor>
    <meatYieldFactor>1.4</meatYieldFactor>
    <beautyOffset>2</beautyOffset>
    <plantCoverDemand>0</plantCoverDemand>
    <shelterDemand>1</shelterDemand>
    <substrateDemand>0.5</substrateDemand>
    <currentDemand>0</currentDemand>
    <openWaterDemand>0.25</openWaterDemand>
    <compatibleHabitats><li>Lake</li><li>River</li><li>Coastal</li></compatibleHabitats>
    <habitatPreferences>
      <li><habitat>River</habitat><suitability>1.4</suitability></li>
      <li><habitat>Lake</habitat><suitability>0.8</suitability></li>
    </habitatPreferences>
    <compatibleBiomes><li>TemperateForest</li><li>BorealForest</li></compatibleBiomes>
    <populationSeasons><li>Spring</li><li>Summer</li><li>Fall</li></populationSeasons>
    <populationMinimumTemperature>6</populationMinimumTemperature>
    <populationMaximumTemperature>24</populationMaximumTemperature>
    <populationRarity>Uncommon</populationRarity>
    <populationDensity>0.7</populationDensity>
  </li>
</modExtensions>
```

Supported diets are `Herbivore`, `Carnivore`, `Omnivore`, `Detritivore`, and `FilterFeeder`. Water kinds are `Freshwater`, `Saltwater`, and `Brackishwater`.

Unset values retain automatic dependency metadata and species-name inference. Multipliers default to `1`; beauty offset defaults to `0`.

Natural population habitats are `Pond`, `Lake`, `River`, `Marsh`, `Coastal`, and `Ocean`. `compatibleHabitats`, `compatibleBiomes`, `excludedBiomes`, and `populationSeasons` are optional restrictions. `habitatPreferences` changes relative suitability without making other compatible habitats invalid. Population temperature fields default to the species' livable temperature range. Rarity is `Common`, `Uncommon`, or `Rare`; density and suitability must be positive multipliers.

Unextended VFE fish inherit freshwater/saltwater, allowed biome groups, and commonality metadata. Other modded fish use water and temperature profile inference, so ordinary fish packs remain supported without patches.

## Fish Processing Yield

The supported dead-fish processing route is `AF_ProcessDeadFish`, used by `ButcherSpot` and `TableButcher`. Its Def supplies five baseline `AF_FishMeat`.

For a runtime fish, the output is calculated once as:

`round(base meat count * existing MeatYield * SizeYieldFactor(SizeFactor))`

`SizeYieldFactor` is `clamp(pow(clamp(SizeFactor, 0.25, 2.0), 0.75), 0.60, 1.50)`. A normal-size fish therefore keeps the recipe's base output, unusually small fish produce at least 60% of normal size yield, and very large or outlier fish produce at most 150%. RimWorld's `Mathf.RoundToInt` determines the integer stack count, with a minimum of one. `SizeFactor` includes the existing age, scale, and numeric-size effects. Processing does not use `MassFactor`, so cubic catch/mass scaling is not applied a second time. Existing species and body `MeatYield` modifiers remain separate and are applied once.

With the five-meat baseline and no species/body multiplier, representative results are:

| SizeFactor | Size yield | Fish meat |
| ---: | ---: | ---: |
| 0.25 or lower | 0.600x | 3 |
| 0.70 (Miniature) | 0.765x | 4 |
| 1.00 (normal) | 1.000x | 5 |
| 1.25 (+25%) | 1.182x | 6 |
| 1.35 (Giant) | 1.252x | 6 |
| 2.00 or higher | 1.500x | 8 |

The fish inspection string and Traits tab show the expected output for the current specimen. The Field Journal records the size-based factor for the largest specimen without adding save fields.

## Natural Water Population Tuning

Each `FishPopulationHabitatDef` controls one connected-water habitat. `populationPerCell` and `carryingCapacityFactor` set density, while minimum and maximum population bound capacity. `diversityCurve` is a `SimpleCurve` from connected cell count to target species count; `maximumDiversity` and `minimumViablePopulation` prevent a body from claiming more species than it can sustain. The shipped curve reaches two species near 70 cells and treats 500 cells as large, with seven lake/river species and eight coastal/ocean species when enough compatible fish exist.

`minimumBreedingPopulation`, `breedingPerDay`, and `naturalMortalityPerDay` control local population turnover. `updateIntervalTicks` controls the infrequent lifecycle cadence. `migrationPerDay` and `maximumMigrationPerDay` control bounded movement. Pond, lake, and marsh are closed categories; River-to-River, Coastal-to-Coastal, and Ocean-to-Ocean are the only regional migration pairings. Cross-category movement is not allowed. Shipped closed habitats keep extinct species extinct until an explicit stocking/integration event introduces them. `rebalancePerDay` controls gentle removal of legacy or topology-merge excess above capacity, and `localVariation` changes catch weights between spots in one body. Pond size and the Coastal/Ocean threshold remain def fields. See `1.6/Defs/FishPopulationDefs.xml` for the shipped profiles.

Population records use connected water cells or Odyssey's existing `WaterBody.cells`, never map dimensions. Custom terrain remains compatible when `TerrainDef.IsWater` is true. Constructed `AF_Pond` terrain remains managed by the separate aquaculture pond simulation.

## Constructed Pond Capacity

Constructed pond capacity has three distinct ceilings. **Physical space** is one fish per valid pond cell. **Industrial support** is the physical maximum capped by the base biological setting, powered aerators, and pond-trait capacity contributions. The fresh-setting base is `0.75` fish per cell; each powered aerator adds eight support units before the physical maximum is applied. **Sustainable now** is an approximate, cached ceiling no higher than industrial support, reduced by current natural food and habitat support. A positive persisted management limit is a policy ceiling and does not delete fish when it is lowered.

The default balance examples below assume no pond-trait bonus, ordinary food demand, and no prepared-feed correction. They are estimates, not guarantees:

| Pond | Aerators | Physical | Industrial | Sustainable now |
| --- | ---: | ---: | ---: | ---: |
| 5 x 5 | 0 | 25 | 18 | ~11 |
| 5 x 5 | 1 | 25 | 25 | ~11 |
| 5 x 5 | several | 25 | 25 | ~11 |
| 10 x 10 | 0 | 100 | 75 | ~45 |
| 10 x 10 | 1 | 100 | 83 | ~45 |
| 10 x 10 | several | 100 | 100 | ~45 |

The Habitat, Management, and Stocking Planner surfaces show these ceilings separately and identify the current limiting factor. A plan can be physically possible while still being ecologically unsupported by food or habitat. Existing saved settings are preserved; saves from the older capacity model receive a one-time correction notice, and existing fish are never mass-removed solely because the new default is lower.

## Fish Pond Effects

Add a `pondEffect` to any `FishTraitDef` to participate in the existing connected-pond aggregator. Contributions scale with `sqrt(count)` and stop at `maximumScale`; only living, spawned pond fish count. Omitted fields are neutral.

```xml
<pondEffect>
  <summary>Improves swimming recreation</summary>
  <joyPerTick>0.000008</joyPerTick>
  <maximumScale>3</maximumScale>
  <durationTicks>30000</durationTicks>
</pondEffect>
```

Supported fields cover Delicious food provenance, pond beauty, recreation, child recreation, Animals XP, natural healing, immunity, mental-break threshold, movement, rest fall, cleansing, toxic reduction, social impact, comfortable temperatures, heat/cold insulation, psychic sensitivity, pain, comfort, algae/detritus reduction, crop growth, capacity, mood memories, duration, and day/night behavior. Temporary pawn values are combined into one `AF_SpentTimeInPond` hediff. Recipe propagation stores the strongest inherited nutrition multiplier rather than multiplying prior food values, so chained processing remains stable. Food provenance is attached to relevant recipe output Things only; ordinary vanilla and third-party ingestible Defs are not globally modified. Existing saves receive a temporary load-time compatibility component for old serialized food provenance, which is removed from Defs after loading.

Habitat demand values are weighted support units per fish. Leave them unset to use life stage, diet, body size, schooling, and species inference. Set any habitat field to `0` or higher to override that category for the species.

Species-name inference supplies conservative profiles for common schooling fish, large pelagic fish, shellfish, ornamental fish, and common table fish. An explicit extension always layers on top of that inferred profile.

## Pond Causal Summary

The Management page exposes one cached pond-wide explanation built from the existing ecology and habitat snapshots. It reports approximate population versus physical and sustainable support, prepared-food reserve, water and temperature compatibility, habitat deficits, breeding blockers, and a next-breeding estimate only when compatible adults and available capacity make that estimate honest. Lethal conditions, hunger, severe stress, reproduction blockers, and advice are ordered by urgency. Identical causes are combined with affected-fish counts, and Details can navigate to the relevant page or fish.

## Aquaculture Collector Commissions

Registered breeds may receive one commission at a time. Requests are generated only when the current maps contain a spawned, living, adult, nonsterile, healthy fish of that breed and species. A request chooses an existing defining trait, an achievable generation and size threshold, the breed's current stability, and a bounded deadline. It never requests an extinct or unavailable combination. The reward is base-game Silver and is calculated deterministically from trait difficulty, generation, stability, size, and deadline with a hard upper bound.

The active request is serialized by the game component and shown in the Field Journal. Players select a matching specimen from the Journal; the specimen is intentionally consumed only after the Silver reward has been placed successfully. Completed counts are recorded on the breed record. One commission cooldown and request-history keys prevent repeated identical requests or frequent wealth events. No Royalty, Ideology, or other DLC quest API is required.

## Pond Organisms

Add named pooled plants or invertebrates with `PondOrganismDef`. They participate in the existing ecology pass and never create individual ticking map Things.

```xml
<AquacultureFishing.PondOrganismDef>
  <defName>ExampleScud</defName>
  <label>scuds</label>
  <description>Small detritivorous crustaceans.</description>
  <role>Detritivore</role>
  <freshwater>true</freshwater>
  <saltwater>false</saltwater>
  <brackishwater>true</brackishwater>
  <seedBiomass>0.10</seedBiomass>
  <capacityPerCell>0.03</capacityPerCell>
  <growthPerHour>0.035</growthPerHour>
  <pondBeauty>0.03</pondBeauty>
</AquacultureFishing.PondOrganismDef>
```

Roles are `Producer`, `Zooplankton`, `Detritivore`, and `FilterFeeder`. A culture item uses `CompProperties_PondCulture` and references the organism def. Producers supplement plant diets; zooplankton supports prey diets; detritivores supplement bottom-feeding diets; filter-feeders consume suspended resources and support predators.

## Performance Contract

- Fish do not run individual needs ticks.
- Ecology, prepared feeding, predation, water stress, temperature stress, and habitat welfare are resolved once per configured ecology interval.
- School movement is updated in spatial buckets. Off-screen ponds use reduced update rates.
- Pond tabs and alerts read cached snapshots rebuilt after simulation events.
- New content should use defs and the species extension instead of per-fish Harmony ticks.

## Natural-Water Conservation

The fishing tab and natural-water readout expose cached, approximate conservation status from the shared natural-population ledger. A species row shows an approximate population, its breeding floor, whether breeding is currently possible, and an expected direction: growing, stable, declining, or below breeding population. The estimate is intentionally rounded and does not claim to be an exact census.

Closed Pond, Lake, and Marsh habitats report that recovery depends on local breeding or explicit stocking. River, Coastal, and Ocean habitats report whether a compatible same-category source population is currently recorded and may recover the species through bounded migration. A catch warning is shown once when another fish is likely to cross or deepen breeding-floor risk; it is suppressed until the population recovers or the warning state changes.

Conservation summaries are rebuilt with the existing ledger summary cache after catches, stocking, births, mortality, migration, loading, and connected-water changes. They do not scan the whole water body every UI frame.
