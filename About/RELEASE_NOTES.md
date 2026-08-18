# Release Feature Set

## Licensing and release scope

Aquaculture's original source and packaged artifacts are licensed under
`GPL-3.0-or-later`, Copyright (C) 2026 lanwoodall423. The Insight Canvas
dependency separately declares GPLv3.0 in its own checkout license. The owner
has waived the dedicated production-art requirement for this candidate; no
production art was generated or changed, and third-party art remains under its
original owner's terms.

## Prompt 1 UI foundation

The settings surface now uses an embedded Insight Canvas 2.x document with responsive navigation, stable-ID scopes, document-local aquatic theming, accessibility density options, searchable virtualized fish/trait lists, and direct bindings to the existing authoritative `AquacultureSettings`. Serialization keys, defaults, migrations, clamps, and the native mask painter remain unchanged. Journal, pond, fish-traits, stocking planner, commission, and rod dialogs are intentionally not part of this migration.

## Progression

1. **Pondkeeping** is Neolithic, follows Fishing when Odyssey is active, and unlocks constructed ponds, fishing buckets, habitat structures, and natural-water sampling.
2. **Managed Aquaculture** is Medieval and unlocks fish processing, prepared feed, fishing barrels, manual pond feeders, and harvest-limit management.
3. **Industrial Aquaculture** unlocks automatic pond feeders, powered aerators, ecosystem and habitat diagnostics, school information, stocking goals, and labor-backed surplus harvesting.
4. **Selective Fish Breeding** follows Industrial Aquaculture and unlocks medicine-backed permanent sterilization plus paused, encouraged, and intensive pond breeding programs.

Research gates can be disabled in General settings for sandbox play.

Vanilla Fishing Expanded is required for the fishing framework. Its Fishing Treasures add-on is optional and contributes only its own VFE content; Aquaculture has no direct dependency on the add-on.

## Field Journal And Breeds

Fishing jobs now use visible Cast, Wait, and Reel phases. Fishing requires a suitable rod from equipment, inventory, or valid storage; Cast is fixed at 180 ticks, Wait uses aggregate Wait Time, expertise, and the global duration setting, and Reel uses non-Rod Reel Time, expertise, and the same duration setting. Lure and fish water attraction plus species knowledge weight eligible species and bite selection. Animals skill, species knowledge, expertise, and rod Catch Chance affect escape resolution, while Max Fish Mass is a separate catch limit. Fish mass is derived from the loaded Mass stat and the hooked individual's size-related traits.

Fishing rods now carry per-item Handle, Rod, Reel, Line, and Lure modifications. The equipped rod's Tackle gizmo opens an aligned Name, Effects, and Cost comparison window. Rod and Line parts affect Max Fish Mass, Rod parts do not affect Reel Time, and the default lure is None. Changes create Crafting work with def-defined materials, skill, work, and research requirements instead of installing instantly; installed and pending parts persist on that rod.

Tackle material costs use RimWorld 1.6's keyed `ThingDefCountClass` format, and the default fishing rod includes a valid low-power melee tool so it passes equipment configuration validation.

Dead-fish processing now uses the existing species/body `MeatYield` once and adds a bounded size curve. The five-meat baseline remains five for a normal-size fish; a size factor below 0.25 cannot reduce output below the 0.60x size floor, and a size factor above 2.0 cannot increase output above the 1.50x size cap. The formula uses `Mathf.RoundToInt` and does not reuse cubic `MassFactor`, so size cannot be counted twice.

Rodless fishing is rejected by the VFE and Odyssey job producers before `StartJob()` rather than by changing a successful reservation result afterward. Empty saved tackle keys are also handled without passing null names to `DefDatabase`, preventing upgrade WorkGiver float-menu exceptions on rods without a pending upgrade.

Pending rod upgrades are event-indexed. Work scans inspect only rods with pending state and keep ingredient pathfinding out of `HasJobOnThing`; failed allocations retry after 600 ticks instead of repeating every scan.

Added Delicious, Beautiful, Healing, Playful, Calming, Energizing, Cleansing, Aromatic, Cooling, Warming, Therapeutic, Luxurious, Sparkling, Friendly, Grazer, Fertilizing, Oxygenator, Anima, and Lunar traits, while extending the existing Nutritious and Curious traits. Connected ponds aggregate these effects from living fish with square-root diminishing returns and def-configured caps.

Selected colonists can now right-click a safe constructed pond containing living fish and choose **Enter Pond**. Swimming is an interruptible recreation job that validates health, schedule, danger, reachability, reservation, and temperature. Temporary bonuses are collected in one refreshable **Spent Time in Pond** hediff. Fish nutrition and Delicious provenance propagate through prepared food without repeated recipe multiplication.

Fishing attempts now bind to one fish species currently present in the selected natural-water ledger. The four expertise ranks are Novice, Adept, Expert, and Master. Species access requires ledger presence and the configured minimum expertise; species knowledge affects attraction, bite, and escape, while Animals skill affects escape. Successful catches advance both knowledge and expertise. Per-species minimum expertise is configurable for every dynamically discovered fish definition.

Natural ponds, lakes, rivers, marshes, coasts, and oceans now receive deterministic habitat populations shared across each connected water body. Smooth connected-cell curves make roughly 70 cells support two or three compatible species and treat 500 cells as a large source with six or more when habitat and biome compatibility permit. Every generated species receives a viable founding population. Common fish dominate, uncommon fish are sparser, and rare fish become more likely only as compatible diversity grows. Species defs may restrict habitat, biome, temperature, and season or tune rarity, density, and suitability.

Initial generation now fills the diversity target using only compatible, currently present species; incompatible, extinct, or sub-present framework entries no longer consume species slots. Capacity is divided across the selected species with a breeding-viable floor, and records produced by the earlier underfilled generator receive one additive repair. The bottom-left water readout lists every species in the shared population ledger with an estimated count.

The Odyssey Fishing tab now shows the authoritative connected-water total, capacity, and every present species with its estimated population. Connected-water membership and prepared UI summaries are cached, terrain invalidation is debounced, and lifecycle simulation wakes only at configured habitat intervals; UI and routine fishing queries no longer repeat region discovery, stable-anchor sorting, or per-frame population formatting.

Existing Odyssey populations and VFE zone species seed additive natural-water records. After initialization, ponds, lakes, and marshes are closed populations: viable survivors breed under capacity, natural mortality and explicit gameplay events change abundance, and local extinction persists until explicit compatible stocking. Rivers, coastal waters, and oceans instead receive small pressure-weighted migration updates only within River-to-River, Coastal-to-Coastal, or Ocean-to-Ocean regions. All lifecycle work runs at a def-configured infrequent cadence, and over-cap legacy populations rebalance gradually. Successful catches consume one unit from the shared connected-body population, while different fishing spots retain small deterministic differences in species weight.

The Field Journal includes an Expertise tab for selecting a colonist, reviewing level progress and per-species knowledge, and identifying expertise-locked fish. The Breeds tab and breed summary remain hidden until Selective Fish Breeding is available.

The Fish Journal gives the colony a persistent collection and breeding progression. Every loaded fish species can advance through Discovered, Established, Bred, and Stable milestones. Discovery records the fisher when available; establishment requires pond placement; breeding requires colony-born offspring; stability requires a healthy adult male/female population of at least three fish to persist for one quadrum. The journal also records the colony's largest, most beautiful, most nutritious, rarest, and longest-lived specimen for each discovered species.

When the Wildlife mod is loaded, Fish Journal registers in its responsive Wildlife menu after Wildlife Overview and Wildlife Expeditions. Its standalone bottom-bar tab hides to avoid duplication. Wildlife remains optional; without it, the original Fish Journal tab remains visible.

Selective Fish Breeding allows an adult male/female population with matching inheritable traits and at least one colony-born member to be registered as a named **Breed**. Wild exceptional traits, per-parental-trait inheritance, inherited-trait caps, separate offspring mutations, and registered-breed defining-trait reliability are independent settings and calculations. A trait present in both parents receives a 1.5x inheritance chance; caps apply after successful rolls, and incompatible or unknown traits are filtered before mutation. Registered-breed reliability increases with demonstrated stability but never guarantees defining traits at low stability. Breed identity is shown on fish and in the Traits tab, and each newly established generation is retained in permanent lineage history. Registered fish gain stability-scaled market value and pond beauty, while unsuccessful offspring remain ordinary fish instead of silently inheriting the breed name.

Breed stability is evidence-led: the journal records both matching births and total qualifying births, and uses `clamp(0.50 + (matching / qualifying) * 0.40 + generation contribution, 0.50, 0.98)`. Generation contribution is capped at 0.10 and cannot double-reward one birth through compounding terms. The breed UI shows matching births, total qualifying births, success rate, generation contribution, and resulting stability. Non-qualifying crosses do not affect the record; legacy records preserve their established stability floor when failed-birth totals are unavailable.

Registered breeds can receive one achievable **Aquaculture Collector Commission** at a time. Registration does not immediately sell a founder. When a healthy male/female pair can actually breed, the request combines one defining trait with one reachable generation, size, or stability improvement and rejects goals already satisfied by living fish. The Journal lists matching living fish for deliberate delivery; delivery revalidates, places the reward first, and then consumes exactly one specimen. Bounded generation, stability, trait difficulty, size, rarity, and deadline factors produce 250-2,400 Silver. A breed-keyed fish index, cooldown, request history, save-safe active record, and expiry/failure counter prevent repeated, impossible, or trivial requests.

All journal updates are event-driven. The one-quadrum stability check reuses the existing batched ecology pass and adds no new ticker.

Natural-water fishing also shows approximate per-species conservation status: current population, breeding floor, breeding availability, expected direction, catch-floor risk, and compatible migration recovery context. Closed water explains local breeding or explicit stocking recovery; open water distinguishes a recorded same-category source from an enabled category with no current source. Risk warnings appear once per unchanged water-body/species state before a likely floor-crossing or further-depleting catch rather than interrupting every sustainable fishing job.

## Pond Management

Managed ponds provide a Management tab with separate Basic, Advanced, and Breeding pages. Each page starts with cached visual status cards, while policy controls remain compact enough to avoid clipped text. Basic policies cover empty-pond water selection, minimum breeding stock, harvestable life stages, and protection of breeding females. Harvest commands select a species and individual fish, then create a handling job that requires a pawn to enter the pond, catch the fish, and carry it to shore.

Industrial management adds a soft population goal, optional surplus harvest designations that still require handler labor, and powered automatic feeding backed by a stocked automatic feeder and fish feed. Global predation enables the system, and each pond can disable predation locally. Selective breeding interventions consume prepared feed; pausing designates eggs for handler removal. Sterilization is permanent, requires a handler, and consumes one medicine.

Industrial Aquaculture also unlocks a visual Stocking Planner. It starts from the current pond population and lets players model any loaded fish species, hypothetical water type, population, food pressure, predation, temperature compatibility, breeding potential, beauty, and yield. Forecasts run only while the planner is open. The planned total can be applied as the pond's management population goal without moving fish.

Stocking plans can now be saved as **Living Pond Blueprints**. A blueprint persists its intended water and exact species counts, displays live ecosystem fit plus missing and surplus fish, and makes automatic surplus harvesting species-aware. Existing handler jobs remove older eligible fish above each species target without reducing protected breeding stock. No additional ticking system is introduced; reconciliation occurs during existing ecology events and menu snapshot rebuilds.

Pond Management now presents a cached causal summary: population versus physical and sustainable support, prepared-food reserve, water and temperature compatibility, habitat deficits, breeding blockers, prioritized risks, and conditional next-breeding estimates. Identical pond-wide problems are combined with affected-fish counts, and Details actions navigate to the relevant management page or fish.

Pond tabs are ordered Management, Fish, Schools, and Overview from right to left.

## Living Habitats

Pond species now create weighted needs for plant cover, rock shelter, prepared substrate, powered current, and open water. Needs are inferred from age, diet, schooling, body size, reproductive traits, and species ecology, with optional XML overrides for content packs.

Pondkeeping provides a complete natural route. Colonists build a sampling station beside natural water and spend Animals work gathering duckweed, sea lettuce, daphnia, brine shrimp, pond snails, freshwater mussels, or blue mussels. Culture baskets establish named, water-compatible biomass populations. Producers grow directly; zooplankton graze algae; snails consume detritus; and mussels filter suspended food. Fish consume the relevant populations, so poor stocking can collapse a culture while a balanced pond can become self-renewing.

Aquatic plant cover shelters young fish, supports herbivore grazing, improves algae growth, and reduces predation on fry and juveniles. Rock shelters support solitary fish and ambush predators. Gravel beds support detritivores and bottom-dwellers while restoring detritus. Powered aerators add eight industrial-support fish each, subject to physical pond space; the new base ecological default is 0.75 fish per valid cell. Habitat, Management, and the Stocking Planner distinguish physical space, approximate sustainable support, industrial support, and management limits. Every structure and established organism contributes restrained pond beauty.

The Habitat tab compares weighted supply and demand, lists every established organism and biomass level, classifies the pond as a Natural Ecosystem, Managed Hybrid, or Intensive Aquaculture system, and reports whether its food web appears self-renewing. Individual fish show habitat fit and accumulated stress in their Traits tab. Good habitat improves breeding cadence; poor habitat slows breeding and only severe, sustained stress prevents it. Fish prefer relevant structures through the existing school-target system, with no per-fish pathfinding or additional ticker.

Natural ponds trade density for low inputs, biodiversity, beauty, and self-renewing food. Industrial ponds use prepared feed, automatic feeders, and powered aeration to support substantially higher production. This is an emergent construction and stocking choice, not a permanent mode toggle.

## Simulation

Fish carry required Age, Sex, Diet, and Water traits plus configurable random traits. Compatible adults breed naturally without research. Selective breeding exposes Natural, Paused, Encouraged, and Intensive programs as visible controls. Ponds simulate algae, detritus, prepared feed, habitat, predation, hunger, water compatibility, temperature, lifespan, breeding, eggs/livebearing, beauty, recreation, meditation, and species-specific schools. Pawns and animals use RimWorld's swimming state while traversing constructed pond water.

Species receive inferred ecology and economy profiles covering food demand, speed, schooling, lifespan, breeding cadence, offspring, meat yield, and beauty. XML extensions can override or multiply every profile value for additional fish packs.

## Development Diagnostics

During development, the RimTest-selected DevBridge2/RimBridge companion suite
exposes bounded live-world validation. RimTest is the test entrypoint and
DevBridge2 is not a player dependency.

Runtime Def changes are now scoped. Recognized fish retain only the Def-backed state required for individual lifecycle, rendering, stats, and save behavior. Food provenance is attached to relevant recipe output Things instead of every ingestible Def, while a temporary load-only compatibility pass preserves old serialized food components. Vanilla foods, unrelated modded ingestibles, storage, trade, and unrelated recipes are not modified.

Knowledge Framework compatibility is explicit: Aquaculture targets `lan.knowledgeframework` API generation 3 with capability generation 3, registers Balanced stage aggregation, uses stable domain/context/subject/event IDs, commits migrations only after durable success, and falls back to the existing legacy expertise path when the V3 capability contract is unavailable. Personal and colony evidence from one logical event is submitted as one transaction, and dynamic individual subjects are bounded and resolved through the fish lifecycle registry.
### Deferred Reality

The natural-water Deferred Reality provider is now source-controlled and built
as a deterministic compatibility artifact. It keeps active Things and ponds in
Aquaculture, latent aggregate populations and exactly-once regional operations
in Deferred Reality, and observations in Knowledge Framework. Closed water and
cross-category migration are rejected, provider migration is retry-safe, and
missing or incompatible Deferred Reality content fails closed to the legacy
simulation. See `About/DEFERRED_REALITY_INTEGRATION.md` for the contract.
