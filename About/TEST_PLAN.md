# Aquaculture - Fishing: Release Test Plan

Use a new colony with Harmony, Vanilla Fishing Expanded, its Fishing Treasures add-on, Aquaculture - Fishing, and optionally Aquariums enabled in that order.

## Load And Progression

1. Confirm the main menu log has no red XML, def, or Harmony errors.
2. Confirm the Aquaculture research tab contains Pondkeeping, Managed Aquaculture, Industrial Aquaculture, and Selective Fish Breeding in order.
3. Confirm Pondkeeping is Neolithic, requires Fishing when Odyssey is active, and unlocks ponds, buckets, habitat structures, and the pond-life sampling station.
4. Confirm Managed Aquaculture requires Pondkeeping and unlocks barrels, fish processing, fish feed, manual feeders, and the Management tab.
5. Confirm Industrial Aquaculture requires Managed Aquaculture and unlocks automatic feeders, aerators, Overview/Habitat/Fish/Schools tabs, alerts, stocking goals, surplus harvest policy, and removal controls.
6. Confirm Selective Fish Breeding requires Industrial Aquaculture and unlocks sterilization and pond breeding programs.
7. Disable Research Progression in settings and confirm all four stages behave as complete.

## Field Journal And Breeds

1. Open Fish Journal and confirm it starts with no discovered species while showing the loaded-species total.
2. Catch a living fish. Confirm Discovered unlocks once, the fisher is recorded when available, and repeated catches do not spam messages.
3. Place that species in a compatible pond and confirm Established unlocks.
4. Hatch or live-bear colony offspring and confirm Bred unlocks.
5. Maintain at least three healthy fish of the species, including an adult male and female, for one quadrum. Confirm progress resets if the population becomes unhealthy and Stable unlocks after an uninterrupted quadrum.
6. Confirm size, beauty, nutrition, and longevity records update as better specimens appear or age.
7. Before Selective Fish Breeding, confirm Register Breed is unavailable.
8. After research, create an adult male/female cohort with identical inheritable traits and at least one colony-born member. Register it with a blank-by-default naming dialog and confirm Enter saves.
9. Confirm duplicate or blank breed names cannot be registered, all matching founders receive the breed, and the Traits tab shows breed, generation, and stability.
10. Breed two members of the same breed. Confirm matching offspring retain the breed and increase its generation/stability, while offspring missing defining traits do not receive the breed name.
11. Reach 90% inheritance stability and confirm the breed becomes Mastered.
12. Confirm registered fish receive the journal's displayed market-value and pond-beauty bonuses and that all journal/breed data survives save/load.

## Catching And Containers

1. Complete ten fishing jobs and confirm each successful job produces exactly one fish.
2. Confirm each fish has a Traits tab and required Age, Sex, Diet, and Water traits.
3. Confirm an enabled nearby bucket or barrel receives caught fish.
4. Confirm fish in containers remain alive and pond fish are never pulled into containers.
5. Empty a container and confirm exposed living fish die after roughly four in-game hours, become dead fish, and begin normal rot.

## Pond Management

1. Build one connected pond and select any tile.
2. Walk colonists and animals through the pond. Confirm they use swimming state/graphics, water movement cost, ripples, and soaking-wet behavior.
3. Before Managed Aquaculture, confirm no management/information tabs appear.
4. After Managed Aquaculture, confirm Basic, Advanced, and Breeding pages fit without clipped labels. Confirm water type can change only while the pond contains no fish or eggs. Test minimum breeding stock, mature-only harvesting, breeding-female protection, and manual feeding.
5. Open Harvest Fish, choose a species and then an individual. Confirm a handler enters the pond, catches that fish, and carries the dead fish to shore.
6. After Industrial Aquaculture, set a population goal and enable surplus harvest. Confirm ecology checks only designate eligible surplus fish and handlers must perform every harvest. Test automatic feeding with powered, unpowered, empty, and stocked feeders.
7. Confirm the pond tabs read Management, Habitat, Fish, Schools, Overview from right to left.
8. After Selective Fish Breeding, confirm Natural, Paused (Remove Eggs), Encouraged, and Intensive are all visible. Confirm Paused designates eggs and handlers enter the pond to remove them.
9. Confirm Encouraged and Intensive consume prepared feed when their bonuses apply and fall back to natural breeding when feed is insufficient.
10. Designate a fish for sterilization. Confirm a handler carries one medicine to the fish, performs the procedure, and the fish remains permanently sterile.
11. Confirm removed fish appear alive on shore and harvested fish appear dead.
12. Confirm pond fish and eggs cannot be hauled or eaten while in pond water except through their designated management jobs.
13. Confirm warnings and the pond alert appear for wrong water, unsafe temperature, starvation, and over-capacity populations.
14. Confirm each management page's status cards update after ecology checks, designations, completed jobs, feeder power changes, and policy changes without continuously refreshing.
15. Open the Industrial Stocking Planner. Confirm it starts from current fish, searches all loaded species, changes planned counts and water type, flags incompatible water and temperature, estimates algae/feed pressure and predation, and applies only the planned total when Set Goal is pressed.
16. Reopen the planner after changing actual pond stock. Confirm Current reloads the live population and Clear removes the hypothetical plan without changing the pond.
17. Save a Living Blueprint with at least three species. Confirm it survives save/load, shows live fit/deficit/surplus metrics, reloads through Blueprint, and sets the management population goal.
18. Enable surplus harvesting, then exceed one species target while another remains below target. Confirm handlers designate only eligible fish above the overstocked species target and preserve minimum breeding stock.

## Living Habitats

1. Build each habitat structure on constructed pond terrain. Confirm placement is rejected outside the pond and when another habitat occupies the cell.
2. Confirm aquatic plant cover, rock shelters, gravel beds, and the sampling station unlock with Pondkeeping, and the powered aerator with Industrial Aquaculture.
3. Add fry, juveniles, herbivores, livebearers, solitary fish, predators, bottom-dwellers, filter-feeders, schooling fish, and large fish. Confirm the Habitat tab reports sensible demand categories without clipped text.
4. Add habitat structures and wait one ecology interval. Confirm weighted fit improves, stressed-fish count falls over time, and individual Traits tabs show updated Habitat Fit.
5. Confirm plant cover improves algae regeneration and substantially reduces predation on fry and juveniles.
6. Confirm substrate restores detritus and powered aerators add current support and four pond-capacity units each. Turn off aerator power and confirm both effects disappear after the next ecology interval.
7. Compare well-fitted and poorly fitted breeding pairs. Confirm habitat modifies breeding cadence, with severe sustained stress preventing breeding.
8. Watch fish movement. Confirm young/herbivorous fish prefer plants, solitary/predatory fish prefer shelters, bottom-dwellers prefer substrate, and filter-feeders prefer active aerators without erratic per-fish movement.
9. Save and reload a populated decorated pond. Confirm structures, habitat fit, stress, and movement preferences survive correctly.
10. Place the sampling station within three cells of natural water. Confirm placement is rejected elsewhere and all seven gathering bills require meaningful Animals work.
11. Gather and seed duckweed/daphnia/snails/freshwater mussels into freshwater, and sea lettuce/brine shrimp/blue mussels into saltwater. Confirm incompatible cultures are rejected and brackish ponds accept both sets.
12. Watch multiple ecology intervals. Confirm named biomass populations grow from their required resource, are consumed by relevant fish diets, can collapse under excessive predation or insufficient food, and survive in a balanced pond.
13. Confirm the Habitat tab lists each species and biomass/capacity, reports natural balance, and identifies Natural Ecosystem, Managed Hybrid, and Intensive Aquaculture states correctly.
14. Compare a diverse conservative natural pond against a fed, aerated high-density pond. Confirm both are viable while the industrial pond supports eight additional fish per powered aerator and consumes ongoing resources and power.
15. Run `AQUA_HABITAT <pond>` and confirm bridge values and organism populations match the Habitat tab.

## Ecology And Breeding

1. Put freshwater and saltwater species into each water type and confirm incompatible fish accumulate stress and die at the configured rate.
2. Test cold- and warm-water species outside their livable range.
3. Confirm herbivores consume algae, detritivores consume detritus, and carnivores consume smaller fish when predation is enabled.
4. Manually dispense fish feed, then research Industrial Aquaculture and confirm automatic consumption from a nearby feeder.
5. Before researching Selective Fish Breeding, put adult male and female fish of one species together. Confirm natural eggs or live fry, configured offspring counts, hatch timing, trait inheritance, and pond capacity limits.
6. Confirm Livebearer, Prolific, Seasonal Breeding, Solitary, Curious, Hardy, Heavy-Bodied, Slender, Nutritious, scale, color, pattern, and finish traits produce their stated effects.
7. Compare a tiny schooling fish, ornamental fish, common table fish, shellfish, and large pelagic fish. Confirm their lifespan, breeding, offspring, food demand, meat yield, beauty, and school behavior differ in sensible directions.

## Visuals And Performance

1. Paint Spotted and Striped masks for every texture variation of one fish and confirm single-item and stack graphics use the correct masks.
2. Confirm every swimming fish receives the same underwater grade; pseudo-depth may affect scale and shadows but must not put a fish visually above the surface.
3. Toggle every Visuals setting and confirm it changes only rendering.
4. Test ponds containing 25, 100, and 200 fish at speed 3. Pan on and off screen and watch ponds with several pawns.
5. Confirm opening pond tabs causes no sustained frame-time increase and displayed information updates only after simulation events.

## Pond Swimming And Effect Traits

1. Select an undrafted healthy colonist and right-click a reachable, safe-temperature constructed pond containing living fish. Confirm **Enter Pond** starts an interruptible `swimming in pond` recreation job.
2. Repeat with an empty pond, drafted/downed/mental-state pawn, unsafe temperature, known danger, and unreachable or reserved cells. Confirm a disabled option explains rejection or no inappropriate job starts.
3. Confirm Playful, Curious, and Friendly increase recreation; Curious grants modest Animals XP; Friendly is stronger for children.
4. Confirm Healing, Calming, Energizing, Aromatic, Cooling, Warming, Therapeutic, Anima, and nighttime Lunar update one **Spent Time in Pond** hediff rather than creating duplicate Health-tab entries.
5. Confirm Cleansing removes carried filth/blood and only very slightly reduces Toxic Buildup. Confirm Luxurious grants **Enjoyed Luxury Pond**, and Sparkling grants its mood memory only at night.
6. Compare one, four, nine, and sixteen matching fish. Confirm effects follow diminishing returns and stop at each trait's configured cap; dead, despawned, and removed fish do not contribute.
7. Confirm Beautiful and Sparkling add capped pond beauty, Sparkling is stronger after nightfall, Grazer reduces algae, Fertilizing modestly improves nearby crop growth, and Oxygenator reduces detritus and raises sustainable capacity.
8. Process Delicious and Nutritious fish into fish meat and then another prepared food. Confirm Delicious mood provenance remains and nutrition uses the strongest inherited multiplier without multiplying again at each recipe.
9. Breed effect-trait fish through eggs and live birth. Confirm inherited trait names and numeric Nutritious values survive save/load and produce the same pond/food effects in offspring.

## Compatibility

1. Repeat catching, traits, pond placement, and processing with Vanilla Fishing Expanded fish, Odyssey fish where available, Fishing Treasures fish, and Aquariums.
2. Put a trait-bearing fish in an aquarium and confirm its visual material remains visible.
3. Save and reload with populated ponds, eggs, containers, feeder fuel, water types, pond toggles, masks, and modified settings.
4. Interrupt harvest, egg-removal, and sterilization jobs by drafting the handler, removing the target, and saving mid-job. Confirm reservations and designations recover without stuck work.

## Fishing Expertise

1. Start fishing in water with multiple species and confirm the attempt remains paired with one species until completion.
2. Raise every present species above the fisher's expertise and confirm the fishing reservation is rejected rather than selecting a locked species.
3. Remove the paired species, its fishing zone, or the water body's available population during the attempt and confirm no fish is produced.
4. Compare low and high Animals skill, species knowledge, and expertise. Confirm each independently reduces catch time and escape chance.
5. Confirm an escaped fish grants no progression and a successful catch increases both species knowledge and expertise.
6. Save during an active attempt and after several catches, reload, and confirm the pair and progression persist.
7. Add a fish content pack and confirm every recognized fish appears in settings and the Expertise tab with an Untrained default requirement.
8. Before Selective Fish Breeding, confirm no Breeds tab or breed summary appears; complete the research and confirm both appear.

## Natural Fish Populations

1. Generate connected bodies below 25 cells, at 25-69, 70-149, 150-299, 300-499, and 500+ cells. Confirm diversity transitions smoothly through 0-1, 1-2, 2-3, 3-5, 4-7, and 6+ compatible species rather than changing from map dimensions or hard-coded buckets.
2. Confirm a roughly 70-cell source supports two or three compatible species and a 500-cell source is treated as large. Confirm every selected species starts with at least the configured viable population and total abundance follows connected cell count.
3. Generate a roughly 266-cell still-water body in a biome with at least five compatible freshwater fish. Confirm it is classified as a lake, reaches a target of three to five present species, and gives every selected species at least the configured breeding population.
4. Inspect multiple fishing spots on one connected body. Confirm they expose broadly the same species, with modest deterministic local weighting differences. Mouse over each spot and confirm the bottom-left readout lists every present species with its estimated population. Save/reload and regenerate from the same world seed; confirm the selected mix is stable.
5. Compare common, uncommon, and rare species over several bodies. Confirm common species dominate population and selection, uncommon species are sparser, and rare species appear least often.
6. Add fish defs restricted to Pond, Lake, River, Marsh, Coastal, and Ocean; allowed/excluded biomes; temperature bands; and seasons. Confirm each appears only where compatible. Repeat with an unextended VFE or Odyssey fish pack and confirm dependency metadata is respected.
7. Catch repeatedly from two zones or fishing spots on one connected body. Confirm both deplete the same species ledger and depleted species disappear below one available fish. Confirm failed bites and escaped fish do not deplete population.
8. Load an established save with VFE zones or Odyssey fish population. Confirm listed species and population seed the new record, no established species is abruptly deleted, and any over-cap total decreases gradually over subsequent days.
9. Change a water connection through terrain editing. Confirm split/merged bodies rebuild their connected-cell records safely and merged species populations are retained.
10. Deplete a pond/lake species below its breeding floor and then to zero. Confirm it neither breeds nor naturally returns over repeated lifecycle updates; restock it explicitly and confirm breeding resumes only after the viable breeding threshold is reached.
11. Observe river, coastal, and ocean records below and near capacity across many lifecycle updates. Confirm compatible species migrate gradually, regional open-water abundance biases the mix, population pressure limits inward movement and permits outward movement, and no update exceeds the configured maximum migration.
12. Change season or temperature so a candidate becomes unsuitable. Confirm it is neither introduced by migration nor breeds until conditions become suitable again.
13. Modify `FishPopulationHabitatDef` density, capacity, diversity curve, viable/breeding floors, lifecycle interval, mortality, breeding, migration, local variation, and rebalance fields. Confirm bodies follow the def values without code changes.
14. Select an Odyssey Fishing zone on a body with at least four present species. Confirm the Fishing tab shows the shared total/capacity and one estimated-count row for every species shown by the water readout. Catch or explicitly stock a fish and confirm both surfaces update without reopening the map.
15. Profile with a large lake and the Fishing tab or water tooltip open. Confirm UI frames perform prepared dictionary reads only, connected-body discovery occurs only at initialization or a debounced water-terrain change, and lifecycle work runs at the habitat interval rather than every tick or every 2,500 ticks.

## Fishing Rods And Phases

1. Attempt VFE and Odyssey fishing without a rod, with a rod on the ground, and with a rod equipped. Confirm only the equipped case starts.
2. Observe Cast, Wait, and Reel in the pawn job label and progress bar. Confirm Cast is shortest, Wait longest, and Reel second-longest.
3. Equip default tackle and compare timing/catch behavior with the prior release. Confirm neutral defaults apart from the new phase split and rod requirement.
4. Compare every Handle, Rod, Reel, Line, and Lure option. Confirm the Tackle window aligns Name, Effects, and Cost; effects use labels such as `-6% Reel Time`; Rod never changes Reel Time; and both Rod and Line change Max Fish Mass.
5. Inspect fish with Giant, Miniature, numeric Size, Heavy-Bodied, and Slender traits. Confirm their Mass stat changes. Hook fish below and above Max Fish Mass and confirm an over-mass fish escapes without catch or progression.
6. Confirm the default lure is None. Compare Saltwater and Smart lures in mixed water species sets and confirm attraction weighting and Wait time follow the lure defs.
7. Queue a tackle change from an equipped rod. Confirm the rod is dropped for work, one Crafting job is created, listed materials are reserved and consumed, skill/research requirements are honored, and the part does not install instantly.
8. Try to queue another change on the same rod. Confirm duplicate work is rejected; cancel the bill and confirm another can then be queued. With missing materials, confirm normal tick rate remains stable and the pending request waits without repeated jobs or log spam.
9. Save with installed parts and with a pending upgrade, reload, and confirm both remain on that exact rod. Confirm inspection text and stats update after installation.
10. Add a custom `FishingTacklePartDef`, lure, rod extension, and fish extension. Confirm they are discovered without code changes or shared-def mutation.
