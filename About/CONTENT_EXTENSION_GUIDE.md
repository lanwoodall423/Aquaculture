# Aquaculture - Fishing: Content Extension

Aquaculture discovers loaded fish at startup. A `ThingDef` is treated as a fish when it belongs to `VCEF_RawFishCategory` or `Fish`, or has the Aquariums fish comp. No assembly reference is required for ordinary fish packs.

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
  </li>
</modExtensions>
```

Supported diets are `Herbivore`, `Carnivore`, `Omnivore`, `Detritivore`, and `FilterFeeder`. Water kinds are `Freshwater`, `Saltwater`, and `Brackishwater`.

Unset values retain automatic dependency metadata and species-name inference. Multipliers default to `1`; beauty offset defaults to `0`.

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
