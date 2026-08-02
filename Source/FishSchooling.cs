using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class FishSpeciesProfile
    {
        private static readonly Dictionary<ThingDef, FishSpeciesProfile> Cache = new Dictionary<ThingDef, FishSpeciesProfile>();

        public float cruiseSpeed;
        public float maximumSpeed;
        public float turnRate;
        public float schooling;
        public float cohesion;
        public float alignment;
        public float separationDistance;
        public float neighborRadius;
        public float restFrequency;

        public static FishSpeciesProfile For(ThingDef def)
        {
            if (Cache.TryGetValue(def, out FishSpeciesProfile profile)) return profile;
            float mass = 0.5f;
            try { mass = Mathf.Max(0.05f, def.GetStatValueAbstract(StatDefOf.Mass)); } catch { }
            uint seed = def.shortHash;
            float n1 = Noise(seed * 1664525u + 1013904223u);
            float n2 = Noise(seed * 22695477u + 1u);
            float sizePenalty = Mathf.Clamp(1f / Mathf.Pow(mass + 0.35f, 0.16f), 0.72f, 1.22f);
            profile = new FishSpeciesProfile
            {
                cruiseSpeed = Mathf.Lerp(0.010f, 0.016f, n1) * sizePenalty,
                maximumSpeed = Mathf.Lerp(0.018f, 0.028f, n2) * sizePenalty,
                turnRate = Mathf.Lerp(0.12f, 0.24f, 1f - n1 * 0.55f),
                schooling = Mathf.Lerp(0.58f, 1f, n2),
                cohesion = Mathf.Lerp(0.65f, 1.15f, n1),
                alignment = Mathf.Lerp(0.55f, 1.1f, n2),
                separationDistance = Mathf.Lerp(0.38f, 0.72f, Mathf.Clamp01(mass / 2f)),
                neighborRadius = Mathf.Lerp(2.7f, 4.2f, n1),
                restFrequency = Mathf.Lerp(0.65f, 1.25f, n2)
            };
            ApplySpeciesBehavior(def, profile);
            AquaticSpeciesExtension extension = def.GetModExtension<AquaticSpeciesExtension>();
            if (extension != null)
            {
                profile.cruiseSpeed *= Mathf.Max(0.05f, extension.cruiseSpeedFactor);
                profile.maximumSpeed *= Mathf.Max(0.05f, extension.maximumSpeedFactor);
                profile.turnRate *= Mathf.Max(0.05f, extension.turnRateFactor);
                profile.schooling *= Mathf.Max(0f, extension.schoolingFactor);
                profile.cohesion *= Mathf.Max(0f, extension.cohesionFactor);
                profile.alignment *= Mathf.Max(0f, extension.alignmentFactor);
                profile.separationDistance *= Mathf.Max(0.05f, extension.separationFactor);
                profile.neighborRadius *= Mathf.Max(0.05f, extension.neighborRadiusFactor);
                profile.restFrequency *= Mathf.Max(0.05f, extension.restFrequencyFactor);
            }
            Cache[def] = profile;
            return profile;
        }

        private static void ApplySpeciesBehavior(ThingDef def, FishSpeciesProfile profile)
        {
            string name = ((def.defName ?? string.Empty) + " " + (def.label ?? string.Empty)).ToLowerInvariant();
            if (ContainsAny(name, "anchovy", "herring", "minnow", "sprat", "sardine", "mackerel"))
            {
                profile.cruiseSpeed *= 1.22f; profile.maximumSpeed *= 1.22f;
                profile.schooling *= 1.35f; profile.cohesion *= 1.25f; profile.alignment *= 1.25f;
                profile.separationDistance *= 0.72f;
            }
            else if (ContainsAny(name, "tuna", "salmon", "trout", "swordfish", "marlin"))
            {
                profile.cruiseSpeed *= 1.18f; profile.maximumSpeed *= 1.2f;
                profile.schooling *= 1.08f; profile.alignment *= 1.18f; profile.turnRate *= 0.86f;
            }
            else if (ContainsAny(name, "eel", "angler", "catfish", "halibut", "lobster", "crab"))
            {
                profile.cruiseSpeed *= 0.72f; profile.maximumSpeed *= 0.78f;
                profile.schooling *= 0.58f; profile.cohesion *= 0.65f; profile.alignment *= 0.62f;
                profile.restFrequency *= 1.45f;
            }
            else if (ContainsAny(name, "koi", "goldfish", "guppy", "angelfish", "clownfish"))
            {
                profile.cruiseSpeed *= 0.88f; profile.maximumSpeed *= 0.92f;
                profile.schooling *= 1.08f; profile.cohesion *= 1.12f; profile.turnRate *= 1.15f;
            }
            else if (ContainsAny(name, "piranha", "bass", "perch"))
            {
                profile.maximumSpeed *= 1.12f; profile.turnRate *= 1.12f;
                profile.cohesion *= 0.88f; profile.separationDistance *= 1.08f;
            }
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            for (int i = 0; i < terms.Length; i++) if (value.Contains(terms[i])) return true;
            return false;
        }

        private static float Noise(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            return (value & 0xffffu) / 65535f;
        }
    }

    public sealed class FishMenuEntry
    {
        public CompFishTraits fish;
        public string label;
        public string traits;
        public string stats;
    }

    public sealed class FishSchoolSnapshot
    {
        public string title;
        public string leftStats;
        public string rightStats;
    }

    public sealed class PondOrganismSnapshot
    {
        public string label;
        public string role;
        public float biomass;
        public float capacity;
        public float percent;
    }

    public sealed class PondMenuSnapshot
    {
        public string inspectString;
        public string overview;
        public int population;
        public int capacity;
        public int eggs;
        public int eligibleHarvest;
        public int pendingHarvest;
        public int pendingEggRemoval;
        public int pendingSterilization;
        public int hungry;
        public int starving;
        public int wrongWater;
        public int temperatureStressed;
        public float algaePercent;
        public float detritusPercent;
        public float feedHours;
        public float zooplanktonPercent;
        public float benthosPercent;
        public float temperature;
        public string feederStatus;
        public int blueprintTarget;
        public int blueprintDeficit;
        public int blueprintSurplus;
        public float blueprintFit;
        public string blueprintStatus;
        public PondHabitatSnapshot habitat;
        public readonly List<string> warnings = new List<string>();
        public readonly List<FishMenuEntry> fish = new List<FishMenuEntry>();
        public readonly List<FishSchoolSnapshot> schools = new List<FishSchoolSnapshot>();
        public readonly List<PondOrganismSnapshot> organisms = new List<PondOrganismSnapshot>();
    }

    public sealed class PondProxyThing : Building
    {
        public override string LabelNoCount => "pond";

        public override string GetInspectString()
        {
            if (!AquacultureProgression.IsAvailable("AF_IndustrialAquaculture"))
                return AquacultureProgression.IsAvailable("AF_ManagedAquaculture") ? "Managed pond" : "Constructed pond";
            return Map?.GetComponent<FishPondMapComponent>()?.MenuSnapshotAt(Position)?.inspectString ?? "Constructed pond";
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            FishPondMapComponent component = Map?.GetComponent<FishPondMapComponent>();
            if (AquacultureProgression.IsAvailable("AF_IndustrialAquaculture"))
            {
                yield return new Command_Action
                {
                    defaultLabel = "Remove Fish",
                    defaultDesc = "Remove one living fish from the pond and place it on the shore.",
                    icon = TexCommand.SelectCarriedThing,
                    action = () => OpenFishActionMenu(component, false)
                };
            }
            if (AquacultureProgression.IsAvailable("AF_ManagedAquaculture")) yield return new Command_Action
            {
                defaultLabel = "Harvest Fish",
                defaultDesc = "Harvest one eligible fish without reducing the pond below its protected population.",
                icon = TexCommand.Attack,
                action = () => OpenFishActionMenu(component, true)
            };
        }

        private void OpenFishActionMenu(FishPondMapComponent component, bool harvest)
        {
            List<ThingDef> species = harvest ? component?.HarvestableFishSpeciesAt(Position) : component?.FishSpeciesAt(Position);
            if (species == null || species.Count == 0)
            {
                Messages.Message(harvest ? "No fish currently meet this pond's harvest limits." : "This pond contains no living fish.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.WindowStack.Add(new FloatMenu(species.Select(def => new FloatMenuOption(
                (harvest ? "Harvest " : "Remove ") + def.LabelCap,
                () =>
                {
                    if (harvest) OpenHarvestIndividualMenu(component, def);
                    else component.TryRemoveFish(Position, def, false);
                })).ToList()));
        }

        private void OpenHarvestIndividualMenu(FishPondMapComponent component, ThingDef species)
        {
            List<CompFishTraits> fish = component.HarvestableFishAt(Position, species);
            if (fish.Count == 0)
            {
                Messages.Message("No " + species.LabelCap + " currently meet this pond's harvest limits.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.WindowStack.Add(new FloatMenu(fish.Select(candidate =>
            {
                string details = candidate.TraitSummary.NullOrEmpty() ? "No traits" : candidate.TraitSummary;
                return new FloatMenuOption(candidate.parent.LabelCap + " - " + details,
                    () => FishHarvestUtility.Designate(candidate));
            }).ToList()));
        }
    }

    public sealed class CompPondMeditationFocus : CompMeditationFocus
    {
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            MeditationFocusDef natural = DefDatabase<MeditationFocusDef>.GetNamedSilentFail("Natural");
            Props.focusTypes.Clear();
            if (natural != null) Props.focusTypes.Add(natural);
        }

        public override float GetStatOffset(Pawn pawn = null)
        {
            if (!ModsConfig.RoyaltyActive || parent?.Spawned != true) return 0f;
            return parent.Map.GetComponent<FishPondMapComponent>()?.AnimaMeditationStrengthAt(parent.Position) ?? 0f;
        }

        public override IEnumerable<string> GetExplanation()
        {
            int count = parent?.Map?.GetComponent<FishPondMapComponent>()?.AnimaFishCountAt(parent.Position) ?? 0;
            int perTree = Mathf.Max(1, AquacultureMod.Settings?.animaFishPerTree ?? 20);
            yield return "Anima fish: " + count + " (" + perTree + " equal one anima tree)";
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            if (parent?.Spawned != true || parent.Map.GetComponent<FishPondMapComponent>()?.AnimaFishCountAt(parent.Position) <= 0) yield break;
            foreach (StatDrawEntry entry in base.SpecialDisplayStats()) yield return entry;
        }
    }

    [HarmonyPatch(typeof(CompMeditationFocus), nameof(CompMeditationFocus.CanPawnUse))]
    public static class PondMeditationFocusTypePatch
    {
        public static bool Prefix(CompMeditationFocus __instance, Pawn pawn, ref bool __result)
        {
            if (!(__instance is CompPondMeditationFocus) || __instance.parent?.Spawned != true) return true;
            MeditationFocusDef natural = DefDatabase<MeditationFocusDef>.GetNamedSilentFail("Natural");
            int anima = __instance.parent.Map.GetComponent<FishPondMapComponent>()?.AnimaFishCountAt(__instance.parent.Position) ?? 0;
            __result = anima > 0 && natural?.CanPawnUse(pawn) == true;
            return false;
        }
    }

    public sealed partial class FishPondMapComponent
    {
        private sealed class SchoolRuntime
        {
            public Vector2 target;
            public int targetUntilTick;
            public int restUntilTick;
            public int nextRestCheckTick;
        }

        private sealed class SchoolState
        {
            public ThingDef species;
            public FishSpeciesProfile profile;
            public int seed;
            public readonly List<CompFishTraits> members = new List<CompFishTraits>();
            public readonly SchoolRuntime runtime = new SchoolRuntime();
            public readonly Dictionary<IntVec2, int> bucketIndex = new Dictionary<IntVec2, int>();
            public readonly List<List<CompFishTraits>> buckets = new List<List<CompFishTraits>>();
            public int lastUpdateTick = -99999;
            public int nextBreedingCheckTick;
        }

        private sealed class PondState
        {
            public PondMovementUtility.PondInfo info;
            public PondProxyThing proxy;
            public readonly List<CompFishTraits> fish = new List<CompFishTraits>();
            public readonly List<SchoolState> schools = new List<SchoolState>();
            public readonly Dictionary<ThingDef, SchoolState> schoolBySpecies = new Dictionary<ThingDef, SchoolState>();
            public PondMenuSnapshot menuSnapshot;
            public float beauty;
            public bool beautyDirty = true;
            public int animaFishCount;
            public PondEcologyRecord ecology;
            public PondVisualData visuals;
            public PondHabitatSnapshot habitat;
            public bool habitatDirty = true;
        }

        private readonly List<PondState> pondStates = new List<PondState>();
        private readonly Dictionary<IntVec3, PondState> pondByCell = new Dictionary<IntVec3, PondState>();
        private readonly Dictionary<CompFishTraits, PondState> pondByFish = new Dictionary<CompFishTraits, PondState>();
        private bool pondTopologyDirty = true;
        private bool pondMembershipDirty = true;
        private int nextSchoolTick;

        public void MarkPondTopologyDirty()
        {
            pondTopologyDirty = true;
            pondMembershipDirty = true;
            AquacultureSnapshotCache.Invalidate();
        }

        public void NotifyFishChanged(CompFishTraits comp)
        {
            if (comp == null) return;
            bool isMember = pondByFish.TryGetValue(comp, out PondState state);
            bool shouldBeMember = comp.parent?.Spawned == true && comp.IsSwimmingInPond;
            if (isMember != shouldBeMember)
            {
                pondMembershipDirty = true;
                AquacultureSnapshotCache.Invalidate();
                return;
            }
            if (isMember)
            {
                InvalidatePondSnapshot(state);
                state.beautyDirty = true;
                state.habitatDirty = true;
                RecountAnimaFish(state);
                if (state.schoolBySpecies.TryGetValue(comp.parent.def, out SchoolState school)) school.nextBreedingCheckTick = 0;
            }
        }

        public PondMenuSnapshot MenuSnapshotAt(IntVec3 pondCell)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(pondCell, out PondState state)) return null;
            return state.menuSnapshot ?? (state.menuSnapshot = BuildMenuSnapshot(state));
        }

        public PondProxyThing ProxyFor(IntVec3 pondCell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(pondCell, out PondState state) ? state.proxy : null;
        }

        public int AnimaFishCountAt(IntVec3 pondCell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(pondCell, out PondState state) ? state.animaFishCount : 0;
        }

        public float AnimaMeditationStrengthAt(IntVec3 pondCell)
        {
            int count = AnimaFishCountAt(pondCell);
            int perTree = Mathf.Max(1, AquacultureMod.Settings?.animaFishPerTree ?? 20);
            return 0.28f * count / perTree;
        }

        public void InvalidatePondMenus()
        {
            for (int i = 0; i < pondStates.Count; i++) pondStates[i].menuSnapshot = null;
            AquacultureSnapshotCache.Invalidate();
        }

        private static void InvalidatePondSnapshot(PondState pond)
        {
            if (pond == null) return;
            pond.menuSnapshot = null;
            AquacultureSnapshotCache.Invalidate();
        }

        public Vector3 SchoolDrawPosition(CompFishTraits comp, Vector3 fallback)
        {
            if (!comp.schoolInitialized) InitializeSchoolState(comp);
            float alpha = Mathf.Clamp01((Find.TickManager.TicksGame - comp.schoolLastUpdateTick) / (float)Mathf.Max(1, comp.schoolUpdateInterval));
            Vector2 position = Vector2.Lerp(comp.schoolPreviousPosition, comp.schoolPosition, Mathf.SmoothStep(0f, 1f, alpha));
            return new Vector3(position.x, fallback.y, position.y);
        }

        private void SchoolTick(int now)
        {
            EnsurePondState();
            if (now < nextSchoolTick) return;
            nextSchoolTick = now + 6;
            PrunePondWatchers(now);
            CellRect visibleRect = map == Find.CurrentMap ? Find.CameraDriver.CurrentViewRect.ExpandedBy(3) : CellRect.Empty;

            for (int pondIndex = 0; pondIndex < pondStates.Count; pondIndex++)
            {
                PondState pond = pondStates[pondIndex];
                bool visible = map == Find.CurrentMap && pond.info.Intersects(visibleRect);
                int interval = visible ? 6 : map == Find.CurrentMap ? 60 : 120;
                for (int schoolIndex = 0; schoolIndex < pond.schools.Count; schoolIndex++)
                {
                    SchoolState school = pond.schools[schoolIndex];
                    if (school.members.Count == 0 || now - school.lastUpdateTick < interval) continue;
                    int delta = school.lastUpdateTick < 0 ? interval : Mathf.Clamp(now - school.lastUpdateTick, 1, 30);
                    school.lastUpdateTick = now;
                    SimulateSchool(pond, school, now, delta, interval);
                }
            }
        }

        private void SimulateSchool(PondState pond, SchoolState school, int now, int delta, int interval)
        {
            FishSpeciesProfile profile = school.profile;
            List<CompFishTraits> members = school.members;
            for (int i = 0; i < members.Count; i++) if (!members[i].schoolInitialized) InitializeSchoolState(members[i]);
            UpdateTarget(school.runtime, pond.info, now, school.seed);
            bool resting = UpdateSchoolRest(school.runtime, school, now);
            BuildSpatialGrid(school, Mathf.Max(1.2f, profile.neighborRadius));

            for (int i = 0; i < members.Count; i++)
            {
                CompFishTraits comp = members[i];
                Vector2 position = comp.schoolPosition;
                Vector2 velocity = comp.schoolVelocity;
                if (comp.harvestReserved)
                {
                    comp.schoolNextPosition = position;
                    comp.schoolNextVelocity = Vector2.zero;
                    continue;
                }
                float maxSpeed = profile.maximumSpeed * comp.MovementSpeed;
                float cruiseSpeed = profile.cruiseSpeed * comp.MovementSpeed;
                float radius = profile.neighborRadius;
                float radiusSquared = radius * radius;
                Vector2 centerSum = Vector2.zero;
                Vector2 velocitySum = Vector2.zero;
                Vector2 separationForce = Vector2.zero;
                int neighborCount = 0;
                IntVec2 centerBucket = HashCell(position, radius);

                if (!comp.Solitary) for (int x = centerBucket.x - 1; x <= centerBucket.x + 1; x++)
                {
                    for (int z = centerBucket.z - 1; z <= centerBucket.z + 1; z++)
                    {
                        if (!school.bucketIndex.TryGetValue(new IntVec2(x, z), out int bucketIndex)) continue;
                        List<CompFishTraits> bucket = school.buckets[bucketIndex];
                        for (int n = 0; n < bucket.Count; n++)
                        {
                            CompFishTraits other = bucket[n];
                            if (other == comp) continue;
                            Vector2 away = position - other.schoolPosition;
                            float distanceSquared = away.sqrMagnitude;
                            if (distanceSquared > radiusSquared) continue;
                            neighborCount++;
                            centerSum += other.schoolPosition;
                            velocitySum += other.schoolVelocity;
                            float distance = Mathf.Max(0.05f, Mathf.Sqrt(distanceSquared));
                            float preferred = profile.separationDistance * comp.SeparationFactor;
                            if (distance < preferred) separationForce += away / distance * ((preferred - distance) / preferred);
                        }
                    }
                }

                Vector2 force = Vector2.zero;
                if (neighborCount > 0)
                {
                    Vector2 center = centerSum / neighborCount;
                    Vector2 averageVelocity = velocitySum / neighborCount;
                    force += SafeNormal(center - position) * 0.0018f * profile.cohesion * comp.CohesionFactor * profile.schooling * comp.SchoolingFactor;
                    force += (averageVelocity - velocity) * 0.075f * profile.alignment * comp.AlignmentFactor;
                    force += separationForce * 0.0045f;
                }
                Vector2 movementTarget = school.runtime.target;
                if (comp.Solitary)
                {
                    int personalIndex = PositiveMod(comp.parent.thingIDNumber * 397 ^ now / 360, pond.info.cells.Count);
                    IntVec3 personalCell = pond.info.cells[personalIndex];
                    movementTarget = new Vector2(personalCell.x + 0.5f, personalCell.z + 0.5f);
                }
                movementTarget = HabitatTargetFor(comp, pond, movementTarget);
                Vector2 curiousTarget = Vector2.zero;
                bool approachingWatcher = comp.Curious && TryGetCuriousTarget(pond, out curiousTarget);
                if (approachingWatcher) movementTarget = curiousTarget;
                force += SafeNormal(movementTarget - position) * (approachingWatcher ? 0.0032f : 0.0012f);

                if (AquacultureMod.Settings?.pseudoDepth != false)
                {
                    if (approachingWatcher) comp.schoolTargetDepth = 0.04f;
                    else if (now >= comp.nextDepthChangeTick)
                    {
                        uint depthHash = (uint)(comp.parent.thingIDNumber * 747796405 ^ now / 600);
                        comp.schoolTargetDepth = 0.08f + (depthHash & 1023u) / 1023f * 0.68f;
                        comp.nextDepthChangeTick = now + 600 + PositiveMod(comp.parent.thingIDNumber * 31, 900);
                    }
                    comp.schoolDepth = Mathf.MoveTowards(comp.schoolDepth, comp.schoolTargetDepth, delta / 1800f);
                }
                else comp.schoolDepth = 0f;

                comp.schoolRestUntilTick = school.runtime.restUntilTick;
                Vector2 desired = resting ? velocity * 0.72f : velocity + force * delta;
                if (!resting && desired.magnitude < cruiseSpeed)
                    desired += SafeNormal(desired == Vector2.zero ? school.runtime.target - position : desired) * (cruiseSpeed - desired.magnitude) * 0.18f;
                desired = ClampMagnitude(desired, maxSpeed);
                float turn = Mathf.Clamp01(profile.turnRate * comp.TurnRateFactor * delta);
                Vector2 nextVelocity = Vector2.Lerp(velocity, desired, turn);
                Vector2 nextPosition = position + nextVelocity * delta;
                if (!PondContains(pond, nextPosition))
                {
                    Vector2 inward = SafeNormal(pond.info.center - position);
                    nextVelocity = Vector2.Lerp(nextVelocity, inward * Mathf.Max(cruiseSpeed, nextVelocity.magnitude), 0.72f);
                    nextPosition = position + nextVelocity * delta;
                    if (!PondContains(pond, nextPosition)) nextPosition = position;
                }
                comp.schoolNextPosition = nextPosition;
                comp.schoolNextVelocity = nextVelocity;
            }

            for (int i = 0; i < members.Count; i++)
            {
                CompFishTraits comp = members[i];
                comp.schoolPreviousPosition = comp.schoolPosition;
                comp.schoolPosition = comp.schoolNextPosition;
                comp.schoolVelocity = comp.schoolNextVelocity;
                comp.schoolLastUpdateTick = now;
                comp.schoolUpdateInterval = interval;
                if (comp.schoolVelocity.sqrMagnitude > 0.000001f)
                    comp.schoolDrawRotation = -157.5f - Mathf.Atan2(comp.schoolVelocity.y, comp.schoolVelocity.x) * Mathf.Rad2Deg;
                IntVec3 cell = PositionCell(comp.schoolPosition);
                if (cell != comp.parent.Position && pond.info.cellSet.Contains(cell))
                {
                    map.thingGrid.Deregister(comp.parent);
                    comp.parent.Position = cell;
                    map.thingGrid.Register(comp.parent);
                }
            }
        }

        private void InitializeSchoolState(CompFishTraits comp)
        {
            Vector3 draw = comp.parent.DrawPos;
            float offsetX = (((comp.parent.thingIDNumber * 37) % 101) / 100f - 0.5f) * 0.36f;
            float offsetY = (((comp.parent.thingIDNumber * 61) % 97) / 96f - 0.5f) * 0.36f;
            comp.schoolPosition = new Vector2(draw.x + offsetX, draw.z + offsetY);
            comp.schoolPreviousPosition = comp.schoolPosition;
            float angle = (comp.parent.thingIDNumber * 137 % 360) * Mathf.Deg2Rad;
            float speed = FishSpeciesProfile.For(comp.parent.def).cruiseSpeed * comp.MovementSpeed;
            comp.schoolVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            comp.schoolLastUpdateTick = Find.TickManager?.TicksGame ?? 0;
            uint depthHash = (uint)(comp.parent.thingIDNumber * 2654435761u);
            comp.schoolDepth = 0.08f + (depthHash & 1023u) / 1023f * 0.68f;
            comp.schoolTargetDepth = comp.schoolDepth;
            comp.nextDepthChangeTick = comp.schoolLastUpdateTick + 600 + PositiveMod(comp.parent.thingIDNumber * 31, 900);
            comp.schoolInitialized = true;
        }

        private static void BuildSpatialGrid(SchoolState school, float cellSize)
        {
            school.bucketIndex.Clear();
            for (int i = 0; i < school.buckets.Count; i++) school.buckets[i].Clear();
            int usedBuckets = 0;
            for (int i = 0; i < school.members.Count; i++)
            {
                CompFishTraits comp = school.members[i];
                if (comp.Solitary) continue;
                IntVec2 key = HashCell(comp.schoolPosition, cellSize);
                if (!school.bucketIndex.TryGetValue(key, out int bucketIndex))
                {
                    bucketIndex = usedBuckets++;
                    school.bucketIndex.Add(key, bucketIndex);
                    if (bucketIndex == school.buckets.Count) school.buckets.Add(new List<CompFishTraits>());
                }
                school.buckets[bucketIndex].Add(comp);
            }
        }

        private static void UpdateTarget(SchoolRuntime runtime, PondMovementUtility.PondInfo info, int now, int seed)
        {
            if (now < runtime.targetUntilTick && info.cellSet.Contains(PositionCell(runtime.target))) return;
            int index = PositiveMod(seed * 397 ^ now / 300, info.cells.Count);
            IntVec3 cell = info.cells[index];
            runtime.target = new Vector2(cell.x + 0.5f, cell.z + 0.5f);
            runtime.targetUntilTick = now + 240 + PositiveMod(seed, 420);
        }

        private static bool UpdateSchoolRest(SchoolRuntime runtime, SchoolState school, int now)
        {
            if (now < runtime.restUntilTick) return true;
            int hash = school.seed;
            if (runtime.nextRestCheckTick == 0)
            {
                runtime.nextRestCheckTick = now + 600 + PositiveMod(hash, 600);
                return false;
            }
            if (now < runtime.nextRestCheckTick) return false;
            float restFactor = 0f;
            for (int i = 0; i < school.members.Count; i++) restFactor += school.profile.restFrequency * school.members[i].RestFrequencyFactor;
            restFactor /= Mathf.Max(1, school.members.Count);
            runtime.nextRestCheckTick = now + Mathf.RoundToInt(Mathf.Lerp(900f, 420f, Mathf.Clamp01((restFactor - 0.5f) / 1.5f))) + PositiveMod(hash + now, 300);
            if (!DeterministicChance(hash, now, Mathf.Clamp01(0.18f * restFactor))) return false;
            runtime.restUntilTick = now + 90 + PositiveMod(hash * 31 + now, 180);
            return true;
        }

        private void EnsurePondState()
        {
            if (pondTopologyDirty) ReconcilePonds();
            if (pondMembershipDirty) RebuildMembership();
        }

        private void ReconcilePonds()
        {
            pondTopologyDirty = false;
            ReleasePondVisuals();
            ThingDef proxyDef = DefDatabase<ThingDef>.GetNamedSilentFail("AF_PondProxy");
            if (proxyDef != null)
            {
                List<Thing> existing = map.listerThings.ThingsOfDef(proxyDef);
                for (int i = existing.Count - 1; i >= 0; i--)
                    if (existing[i] is PondProxyThing proxy && !proxy.Destroyed) proxy.Destroy();
            }
            pondStates.Clear();
            pondByCell.Clear();
            pondByFish.Clear();
            IReadOnlyList<PondMovementUtility.PondInfo> infos = PondMovementUtility.AllPonds(map);
            for (int i = 0; i < infos.Count; i++)
            {
                PondMovementUtility.PondInfo info = infos[i];
                var state = new PondState { info = info, ecology = RecordFor(info), visuals = BuildPondVisuals(info) };
                if (proxyDef != null)
                {
                    state.proxy = (PondProxyThing)ThingMaker.MakeThing(proxyDef);
                    GenSpawn.Spawn(state.proxy, info.anchor, map);
                }
                pondStates.Add(state);
                for (int cellIndex = 0; cellIndex < info.cells.Count; cellIndex++) pondByCell[info.cells[cellIndex]] = state;
            }
            var activeRecords = new HashSet<PondEcologyRecord>(pondStates.Select(state => state.ecology));
            ecologyRecords.RemoveAll(record => !activeRecords.Contains(record));
            pondMembershipDirty = true;
        }

        private void RebuildMembership()
        {
            pondMembershipDirty = false;
            pondByFish.Clear();
            for (int i = 0; i < pondStates.Count; i++)
            {
                PondState pond = pondStates[i];
                pond.fish.Clear();
                pond.schools.Clear();
                pond.schoolBySpecies.Clear();
                InvalidatePondSnapshot(pond);
                pond.beautyDirty = true;
                pond.animaFishCount = 0;
            }

            foreach (CompFishTraits comp in fish)
            {
                if (comp?.parent?.Spawned != true || !comp.IsSwimmingInPond || !pondByCell.TryGetValue(comp.parent.Position, out PondState pond)) continue;
                pond.fish.Add(comp);
                pondByFish[comp] = pond;
                if (IsAnimaFish(comp)) pond.animaFishCount++;
                if (!pond.schoolBySpecies.TryGetValue(comp.parent.def, out SchoolState school))
                {
                    school = new SchoolState
                    {
                        species = comp.parent.def,
                        profile = FishSpeciesProfile.For(comp.parent.def),
                        seed = comp.parent.def.shortHash ^ pond.info.anchor.GetHashCode()
                    };
                    pond.schoolBySpecies.Add(comp.parent.def, school);
                    pond.schools.Add(school);
                }
                school.members.Add(comp);
            }
        }

        private static bool IsAnimaFish(CompFishTraits fish)
        {
            return fish?.traitDefNames?.Contains("AF_Color_Anima") == true;
        }

        private static void RecountAnimaFish(PondState pond)
        {
            int count = 0;
            for (int i = 0; i < pond.fish.Count; i++) if (IsAnimaFish(pond.fish[i])) count++;
            pond.animaFishCount = count;
            InvalidatePondSnapshot(pond);
        }

        private PondMenuSnapshot BuildMenuSnapshot(PondState pond)
        {
            var snapshot = new PondMenuSnapshot();
            float algaeCapacity = Mathf.Max(0.1f, pond.info.cells.Count * 0.25f);
            var inspect = new StringBuilder();
            inspect.Append("Water: ").Append(pond.ecology.waterKind == PondWaterKind.Brackishwater ? "Brackishwater" : pond.ecology.waterKind.ToString())
                .Append("\nLiving fish: ").Append(pond.fish.Count).Append("\nSchools: ").Append(pond.schools.Count)
                .Append("\nPond beauty: ").Append(pond.beauty.ToString("0.#"));
            if (pond.animaFishCount > 0)
            {
                float meditation = 0.28f * pond.animaFishCount / Mathf.Max(1, AquacultureMod.Settings?.animaFishPerTree ?? 20);
                inspect.Append("\nAnima fish: ").Append(pond.animaFishCount).Append("\nNatural meditation: ").Append(meditation.ToStringPercent()).Append(" / day");
            }
            inspect.Append("\nAlgae: ").Append((pond.ecology.algae / algaeCapacity).ToStringPercent());
            PopulateOverview(pond, snapshot);
            if (snapshot.warnings.Count > 0) inspect.Append("\nWarnings: ").Append(snapshot.warnings.Count);
            snapshot.inspectString = inspect.ToString();
            var sortedFish = new List<CompFishTraits>(pond.fish);
            sortedFish.Sort((a, b) =>
            {
                int label = string.Compare(a.parent.def.label, b.parent.def.label, StringComparison.OrdinalIgnoreCase);
                return label != 0 ? label : a.parent.thingIDNumber.CompareTo(b.parent.thingIDNumber);
            });
            for (int i = 0; i < sortedFish.Count; i++)
            {
                CompFishTraits comp = sortedFish[i];
                FishSpeciesProfile movement = FishSpeciesProfile.For(comp.parent.def);
                AquaticSpeciesProfile ecology = AquaticSpeciesProfile.For(comp.parent.def);
                snapshot.fish.Add(new FishMenuEntry
                {
                    fish = comp,
                    label = comp.parent.LabelCap,
                    traits = comp.TraitSummary,
                    stats = ecology.diet + " | " + ecology.waterKind + "\nSpeed: " +
                        (movement.maximumSpeed * comp.MovementSpeed * 60f).ToString("0.00") + " cells/s   Yield: " +
                        comp.MeatYield.ToStringPercent() + "\nBeauty: " + (1f + comp.BeautyOffset).ToString("0.#") +
                        "   Fed: " + comp.foodReserve.ToStringPercent() +
                        "   Habitat: " + comp.habitatFit.ToStringPercent()
                });
            }

            var sortedSchools = new List<SchoolState>(pond.schools);
            sortedSchools.Sort((a, b) => string.Compare(a.species.label, b.species.label, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < sortedSchools.Count; i++) snapshot.schools.Add(BuildSchoolSnapshot(sortedSchools[i]));
            return snapshot;
        }

        private static FishSchoolSnapshot BuildSchoolSnapshot(SchoolState school)
        {
            float maxSpeed = 0f;
            float turn = 0f;
            float cohesion = 0f;
            float alignment = 0f;
            float schooling = 0f;
            float separation = 0f;
            for (int i = 0; i < school.members.Count; i++)
            {
                CompFishTraits comp = school.members[i];
                maxSpeed += school.profile.maximumSpeed * comp.MovementSpeed;
                turn += school.profile.turnRate * comp.TurnRateFactor;
                cohesion += school.profile.cohesion * comp.CohesionFactor;
                alignment += school.profile.alignment * comp.AlignmentFactor;
                schooling += school.profile.schooling * comp.SchoolingFactor;
                separation += school.profile.separationDistance * comp.SeparationFactor;
            }
            float divisor = Mathf.Max(1, school.members.Count);
            return new FishSchoolSnapshot
            {
                title = school.species.LabelCap + " - " + school.members.Count + " fish",
                leftStats = "Species-specific schooling\nCruise speed: " + (school.profile.cruiseSpeed * 60f).ToString("0.00") + " cells/s\nPreferred spacing: " + (separation / divisor).ToString("0.00") + " cells",
                rightStats = "Max speed: " + (maxSpeed / divisor * 60f).ToString("0.00") + " cells/s\nTurn response: " + (turn / divisor).ToString("0.00") + "\nCohesion: " + (cohesion / divisor).ToString("0.00") + "   Alignment: " + (alignment / divisor).ToString("0.00") + "\nSchool tendency: " + (schooling / divisor).ToString("0.00")
            };
        }

        private static bool PondContains(PondState pond, Vector2 position) => pond.info.cellSet.Contains(PositionCell(position));
        private static IntVec3 PositionCell(Vector2 position) => new IntVec3(Mathf.FloorToInt(position.x), 0, Mathf.FloorToInt(position.y));
        private static IntVec2 HashCell(Vector2 position, float size) => new IntVec2(Mathf.FloorToInt(position.x / size), Mathf.FloorToInt(position.y / size));
        private static Vector2 SafeNormal(Vector2 value) => value.sqrMagnitude < 0.000001f ? Vector2.zero : value.normalized;
        private static Vector2 ClampMagnitude(Vector2 value, float maximum) => value.sqrMagnitude > maximum * maximum ? value.normalized * maximum : value;
        private static int PositiveMod(int value, int divisor) => divisor <= 0 ? 0 : (value & int.MaxValue) % divisor;

        private static bool DeterministicChance(int id, int tick, float chance)
        {
            uint value = (uint)(id * 73856093) ^ (uint)(tick * 19349663);
            value ^= value >> 13;
            return (value & 0xffffu) / 65535f < chance;
        }
    }

    [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.SetTerrain))]
    public static class PondTerrainChangedPatch
    {
        public static void Prefix(TerrainGrid __instance, IntVec3 __0, TerrainDef __1, Map ___map, ref bool __state)
        {
            __state = __1?.defName == "AF_Pond" || (___map != null && __0.InBounds(___map) && __instance.TerrainAt(__0).defName == "AF_Pond");
        }

        public static void Postfix(bool __state, Map ___map)
        {
            if (!__state) return;
            PondMovementUtility.Invalidate(___map);
            ___map?.GetComponent<FishPondMapComponent>()?.MarkPondTopologyDirty();
        }
    }

    [HarmonyPatch(typeof(Selector), "SelectUnderMouse")]
    public static class PondTerrainSelectionPatch
    {
        public static bool Prefix(Selector __instance)
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (map == null || !cell.InBounds(map) || map.terrainGrid.TerrainAt(cell).defName != "AF_Pond") return true;
            if (cell.GetThingList(map).Any(ThingSelectionUtility.SelectableByMapClick)) return true;
            PondProxyThing proxy = map.GetComponent<FishPondMapComponent>()?.ProxyFor(cell);
            if (proxy == null) return true;
            if (!Selector.ShiftIsHeld) __instance.ClearSelection();
            __instance.Select(proxy);
            return false;
        }
    }

    public sealed class ITab_PondFish : ITab
    {
        private Vector2 scroll;
        public ITab_PondFish() { size = new Vector2(620f, 470f); labelKey = "AF_PondFishTab"; }
        public override bool IsVisible => SelThing is PondProxyThing && AquacultureProgression.IsAvailable("AF_IndustrialAquaculture");

        protected override void FillTab()
        {
            PondProxyThing pond = SelThing as PondProxyThing;
            PondMenuSnapshot snapshot = pond?.Map?.GetComponent<FishPondMapComponent>()?.MenuSnapshotAt(pond.Position);
            List<FishMenuEntry> fish = snapshot?.fish;
            int count = fish?.Count ?? 0;
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(12f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "Pond Fish (" + count + ")");
            Text.Font = GameFont.Small;
            Rect outRect = new Rect(rect.x, rect.y + 40f, rect.width, rect.height - 40f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, count * 90f));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            for (int i = 0; i < count; i++)
            {
                FishMenuEntry entry = fish[i];
                float y = i * 90f;
                Rect row = new Rect(0f, y, view.width, 84f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(8f, y + 4f, view.width * 0.42f, 24f), entry.label);
                Widgets.Label(new Rect(8f, y + 29f, view.width * 0.56f, 50f), entry.traits);
                Widgets.Label(new Rect(view.width * 0.59f, y + 4f, view.width * 0.39f, 76f), entry.stats);
                if (Widgets.ButtonInvisible(row) && entry.fish?.parent?.Spawned == true)
                {
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(entry.fish.parent);
                }
            }
            Widgets.EndScrollView();
        }
    }

    public sealed class ITab_PondSchools : ITab
    {
        private Vector2 scroll;
        public ITab_PondSchools() { size = new Vector2(620f, 470f); labelKey = "AF_PondSchoolsTab"; }
        public override bool IsVisible => SelThing is PondProxyThing && AquacultureProgression.IsAvailable("AF_IndustrialAquaculture");

        protected override void FillTab()
        {
            PondProxyThing pond = SelThing as PondProxyThing;
            PondMenuSnapshot snapshot = pond?.Map?.GetComponent<FishPondMapComponent>()?.MenuSnapshotAt(pond.Position);
            List<FishSchoolSnapshot> schools = snapshot?.schools;
            int count = schools?.Count ?? 0;
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(12f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "Schools (" + count + ")");
            Text.Font = GameFont.Small;
            Rect outRect = new Rect(rect.x, rect.y + 40f, rect.width, rect.height - 40f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, count * 112f));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            for (int i = 0; i < count; i++)
            {
                FishSchoolSnapshot school = schools[i];
                float y = i * 112f;
                Rect row = new Rect(0f, y, view.width, 104f);
                Widgets.DrawMenuSection(row);
                Widgets.Label(new Rect(10f, y + 7f, view.width * 0.48f, 25f), school.title);
                Widgets.Label(new Rect(10f, y + 34f, view.width * 0.48f, 62f), school.leftStats);
                Widgets.Label(new Rect(view.width * 0.52f, y + 7f, view.width * 0.45f, 88f), school.rightStats);
            }
            Widgets.EndScrollView();
        }
    }
}
