# Release Feature Set

## Progression

1. **Pondkeeping** is Neolithic, follows Fishing when Odyssey is active, and unlocks constructed ponds, fishing buckets, habitat structures, and natural-water sampling.
2. **Managed Aquaculture** is Medieval and unlocks fish processing, prepared feed, fishing barrels, manual pond feeders, and harvest-limit management.
3. **Industrial Aquaculture** unlocks automatic pond feeders, powered aerators, ecosystem and habitat diagnostics, school information, stocking goals, and labor-backed surplus harvesting.
4. **Selective Fish Breeding** follows Industrial Aquaculture and unlocks medicine-backed permanent sterilization plus paused, encouraged, and intensive pond breeding programs.

Research gates can be disabled in General settings for sandbox play.

## Field Journal And Breeds

The Fish Journal gives the colony a persistent collection and breeding progression. Every loaded fish species can advance through Discovered, Established, Bred, and Stable milestones. Discovery records the fisher when available; establishment requires pond placement; breeding requires colony-born offspring; stability requires a healthy adult male/female population of at least three fish to persist for one quadrum. The journal also records the colony's largest, most beautiful, most nutritious, rarest, and longest-lived specimen for each discovered species.

When the Wildlife mod is loaded, Fish Journal registers in its responsive Wildlife menu after Wildlife Overview and Wildlife Expeditions. Its standalone bottom-bar tab hides to avoid duplication. Wildlife remains optional; without it, the original Fish Journal tab remains visible.

Selective Fish Breeding allows an adult male/female population with matching inheritable traits and at least one colony-born member to be registered as a named **Breed**. Breeds begin with 70% per-trait inheritance stability and improve through matching offspring and later generations, reaching mastery at 90%. Breed identity is shown on fish and in the Traits tab, and each newly established generation is retained in permanent lineage history. Registered fish gain stability-scaled market value and pond beauty, while unsuccessful offspring remain ordinary fish instead of silently inheriting the breed name.

All journal updates are event-driven. The one-quadrum stability check reuses the existing batched ecology pass and adds no new ticker.

## Pond Management

Managed ponds provide a Management tab with separate Basic, Advanced, and Breeding pages. Each page starts with cached visual status cards, while policy controls remain compact enough to avoid clipped text. Basic policies cover empty-pond water selection, minimum breeding stock, harvestable life stages, and protection of breeding females. Harvest commands select a species and individual fish, then create a handling job that requires a pawn to enter the pond, catch the fish, and carry it to shore.

Industrial management adds a soft population goal, optional surplus harvest designations that still require handler labor, and powered automatic feeding backed by a stocked automatic feeder and fish feed. Predation is an ecological fact rather than an administrative toggle. Selective breeding interventions consume prepared feed; pausing designates eggs for handler removal. Sterilization is permanent, requires a handler, and consumes one medicine.

Industrial Aquaculture also unlocks a visual Stocking Planner. It starts from the current pond population and lets players model any loaded fish species, hypothetical water type, population, food pressure, predation, temperature compatibility, breeding potential, beauty, and yield. Forecasts run only while the planner is open. The planned total can be applied as the pond's management population goal without moving fish.

Stocking plans can now be saved as **Living Pond Blueprints**. A blueprint persists its intended water and exact species counts, displays live ecosystem fit plus missing and surplus fish, and makes automatic surplus harvesting species-aware. Existing handler jobs remove older eligible fish above each species target without reducing protected breeding stock. No additional ticking system is introduced; reconciliation occurs during existing ecology events and menu snapshot rebuilds.

Pond tabs are ordered Management, Fish, Schools, and Overview from right to left.

## Living Habitats

Pond species now create weighted needs for plant cover, rock shelter, prepared substrate, powered current, and open water. Needs are inferred from age, diet, schooling, body size, reproductive traits, and species ecology, with optional XML overrides for content packs.

Pondkeeping provides a complete natural route. Colonists build a sampling station beside natural water and spend Animals work gathering duckweed, sea lettuce, daphnia, brine shrimp, pond snails, freshwater mussels, or blue mussels. Culture baskets establish named, water-compatible biomass populations. Producers grow directly; zooplankton graze algae; snails consume detritus; and mussels filter suspended food. Fish consume the relevant populations, so poor stocking can collapse a culture while a balanced pond can become self-renewing.

Aquatic plant cover shelters young fish, supports herbivore grazing, improves algae growth, and reduces predation on fry and juveniles. Rock shelters support solitary fish and ambush predators. Gravel beds support detritivores and bottom-dwellers while restoring detritus. Powered aerators support filter-feeders and raise carrying capacity by eight fish each. Every structure and established organism contributes restrained pond beauty.

The Habitat tab compares weighted supply and demand, lists every established organism and biomass level, classifies the pond as a Natural Ecosystem, Managed Hybrid, or Intensive Aquaculture system, and reports whether its food web appears self-renewing. Individual fish show habitat fit and accumulated stress in their Traits tab. Good habitat improves breeding cadence; poor habitat slows breeding and only severe, sustained stress prevents it. Fish prefer relevant structures through the existing school-target system, with no per-fish pathfinding or additional ticker.

Natural ponds trade density for low inputs, biodiversity, beauty, and self-renewing food. Industrial ponds use prepared feed, automatic feeders, and powered aeration to support substantially higher production. This is an emergent construction and stocking choice, not a permanent mode toggle.

## Simulation

Fish carry required Age, Sex, Diet, and Water traits plus configurable random traits. Compatible adults breed naturally without research. Selective breeding exposes Natural, Paused, Encouraged, and Intensive programs as visible controls. Ponds simulate algae, detritus, prepared feed, habitat, predation, hunger, water compatibility, temperature, lifespan, breeding, eggs/livebearing, beauty, recreation, meditation, and species-specific schools. Pawns and animals use RimWorld's swimming state while traversing constructed pond water.

Species receive inferred ecology and economy profiles covering food demand, speed, schooling, lifespan, breeding cadence, offspring, meat yield, and beauty. XML extensions can override or multiply every profile value for additional fish packs.

## Development Diagnostics

When RimWorld Dev Bridge is loaded, Aquaculture exposes read-only commands for compact summaries, pond and fish inspection, species aggregates, active settings, scheduler scale, and state validation. The integration is reflection-discovered and does not make the bridge a dependency.
