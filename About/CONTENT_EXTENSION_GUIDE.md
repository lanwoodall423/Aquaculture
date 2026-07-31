# Aquaculture - Fishing: Content Extension

Aquaculture discovers loaded fish at startup. A `ThingDef` is treated as a fish when it belongs to `VCEF_RawFishCategory` or `Fish`, or has the Aquariums fish comp. No assembly reference is required for ordinary fish packs.

Every discovered fish automatically appears in Fishing settings with an Untrained minimum expertise. Players may raise that requirement without an XML patch; progression and settings remain keyed by the fish `ThingDef.defName`.

External fishing rods can opt in by adding `CompProperties_FishingRodTackle` and `FishingRodExtension` to the rod def. New tackle and lure options are ordinary `FishingTacklePartDef` records, including their slot, performance factors, Max Fish Mass offsets, costs, work, skill, and research. Rod and Line slots may change Max Fish Mass; Rod slots never change Reel Time. Fish packs may add `FishFishingExtension` for freshwater/saltwater lure attraction, while actual fish mass comes from the loaded Mass stat and individual size-related traits.

Tackle `costs` use RimWorld's keyed `ThingDefCountClass` XML form. The element name is the material `ThingDef` and its value is the count:

```xml
<costs>
  <Steel>20</Steel>
  <ComponentIndustrial>1</ComponentIndustrial>
</costs>
```

Do not use `<li>` entries for this field; RimWorld 1.6 interprets an `li` entry's complete text as the requested `ThingDef` name.

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

## Natural Water Population Tuning

Each `FishPopulationHabitatDef` controls one connected-water habitat. `populationPerCell` and `carryingCapacityFactor` set density, while minimum and maximum population bound capacity. `diversityCurve` is a `SimpleCurve` from connected cell count to target species count; `maximumDiversity` and `minimumViablePopulation` prevent a body from claiming more species than it can sustain. The shipped curve reaches two species near 70 cells and treats 500 cells as large, with seven lake/river species and eight coastal/ocean species when enough compatible fish exist.

`minimumBreedingPopulation`, `breedingPerDay`, and `naturalMortalityPerDay` control local population turnover. `updateIntervalTicks` controls the infrequent lifecycle cadence. `migrationPerDay` and `maximumMigrationPerDay` control bounded movement; set both migration values to zero for closed waters. Shipped ponds, lakes, and marshes are closed after initialization, so extinct species stay extinct until an explicit stocking/integration event introduces them. Rivers, coastal waters, and oceans permit pressure-aware migration. `rebalancePerDay` controls gentle removal of legacy or topology-merge excess above capacity, and `localVariation` changes catch weights between spots in one body. Pond size and the Coastal/Ocean threshold remain def fields. See `1.6/Defs/FishPopulationDefs.xml` for the shipped profiles.

Population records use connected water cells or Odyssey's existing `WaterBody.cells`, never map dimensions. Custom terrain remains compatible when `TerrainDef.IsWater` is true. Constructed `AF_Pond` terrain remains managed by the separate aquaculture pond simulation.

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

Supported fields cover Delicious food provenance, pond beauty, recreation, child recreation, Animals XP, natural healing, immunity, mental-break threshold, movement, rest fall, cleansing, toxic reduction, social impact, comfortable temperatures, heat/cold insulation, psychic sensitivity, pain, comfort, algae/detritus reduction, crop growth, capacity, mood memories, duration, and day/night behavior. Temporary pawn values are combined into one `AF_SpentTimeInPond` hediff. Recipe propagation stores the strongest inherited nutrition multiplier rather than multiplying prior food values, so chained processing remains stable.

Habitat demand values are weighted support units per fish. Leave them unset to use life stage, diet, body size, schooling, and species inference. Set any habitat field to `0` or higher to override that category for the species.

Species-name inference supplies conservative profiles for common schooling fish, large pelagic fish, shellfish, ornamental fish, and common table fish. An explicit extension always layers on top of that inferred profile.

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
