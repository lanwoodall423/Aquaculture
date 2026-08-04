using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class AquacultureCommissionRecord : IExposable
    {
        public string id;
        public string breedId;
        public string fishDefName;
        public string traitDefName;
        public float minimumStability;
        public int minimumGeneration = 1;
        public float minimumSizeFactor = 1f;
        public int offeredTick = -1;
        public int deadlineTick = -1;
        public int rewardSilver;

        public ThingDef FishDef => DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);
        public FishTraitDef Trait => DefDatabase<FishTraitDef>.GetNamedSilentFail(traitDefName);
        public FishBreedRecord Breed => AquacultureJournalComponent.Current?.BreedById(breedId);

        public int DaysRemaining(int now)
        {
            if (deadlineTick < 0) return 0;
            return Mathf.Max(0, Mathf.CeilToInt((deadlineTick - now) / 60000f));
        }

        public bool IsExpired(int now) => deadlineTick >= 0 && now > deadlineTick;

        public string RequirementsText
        {
            get
            {
                return "AquacultureFishing.CommissionRequirements".Translate(
                    FishDef?.LabelCap ?? "AquacultureFishing.CommissionUnknown".Translate(),
                    Breed?.name ?? "AquacultureFishing.CommissionUnknown".Translate(),
                    minimumStability.ToStringPercent(), minimumGeneration,
                    Trait?.LabelCap ?? "AquacultureFishing.CommissionUnknown".Translate(),
                    minimumSizeFactor.ToStringPercent()).ToString();
            }
        }

        public bool Matches(CompFishTraits fish, bool requireSpawned)
        {
            if (fish?.parent == null || !fish.IsAlive || fish.parent.def != FishDef) return false;
            if (requireSpawned && !fish.parent.Spawned) return false;
            if (fish.breedId != breedId || fish.sterilized || !fish.IsAdult) return false;
            if (fish.foodReserve < 0.35f || fish.starvationProgress > 0f || fish.waterStress >= 0.10f
                || fish.temperatureStress >= 0.10f || fish.habitatStress >= 0.75f) return false;
            if (fish.breedGeneration < minimumGeneration || fish.SizeFactor < minimumSizeFactor) return false;
            return traitDefName.NullOrEmpty() || fish.traitDefNames?.Contains(traitDefName) == true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref breedId, "breedId");
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Values.Look(ref traitDefName, "traitDefName");
            Scribe_Values.Look(ref minimumStability, "minimumStability", 0.70f);
            Scribe_Values.Look(ref minimumGeneration, "minimumGeneration", 1);
            Scribe_Values.Look(ref minimumSizeFactor, "minimumSizeFactor", 1f);
            Scribe_Values.Look(ref offeredTick, "offeredTick", -1);
            Scribe_Values.Look(ref deadlineTick, "deadlineTick", -1);
            Scribe_Values.Look(ref rewardSilver, "rewardSilver");
        }
    }

    public static class AquacultureCommissionRules
    {
        public const int OfferCooldownTicks = 30 * 60000;
        public const int EligibilityCheckIntervalTicks = 6000;

        public static float TraitDifficulty(FishTraitDef trait)
        {
            if (trait == null) return 1f;
            float rarity = Mathf.Clamp(1.50f - Mathf.Max(0f, trait.commonality) * 0.25f, 0.90f, 1.50f);
            float kindFactor = trait.kind == FishTraitKind.Body || trait.kind == FishTraitKind.Breeding
                || trait.kind == FishTraitKind.Hardiness || trait.kind == FishTraitKind.Effect ? 1.25f
                : trait.IsNumeric ? 1.15f : 1f;
            return Mathf.Clamp(rarity * kindFactor, 0.75f, 2f);
        }

        public static int DeadlineDays(float traitDifficulty, int generation)
        {
            return Mathf.Clamp(18 - Mathf.RoundToInt(traitDifficulty * 2f) - Mathf.Max(0, generation - 1) / 2, 7, 18);
        }

        public static int RewardSilver(int generation, float stability, float traitDifficulty, float sizeFactor, int deadlineDays)
        {
            float rarityFactor = 1f + Mathf.Clamp((traitDifficulty - 0.75f) / 1.25f, 0f, 1f) * 0.35f;
            float generationFactor = 1f + Mathf.Min(8, Mathf.Max(0, generation - 1)) * 0.10f;
            float stabilityFactor = 1f + Mathf.Clamp01((stability - 0.70f) / 0.28f) * 0.45f;
            float sizeFactorReward = 1f + Mathf.Clamp(sizeFactor - 0.75f, 0f, 1.25f) * 0.30f;
            float deadlineFactor = Mathf.Clamp(14f / Mathf.Max(1, deadlineDays), 0.75f, 1.75f);
            float raw = 300f * rarityFactor * generationFactor * stabilityFactor * sizeFactorReward * deadlineFactor;
            return Mathf.Clamp(Mathf.RoundToInt(raw / 25f) * 25, 250, 2400);
        }

        public static string RequestKey(string breedId, string traitDefName, int generation, float sizeFactor)
        {
            return (breedId ?? string.Empty) + "|" + (traitDefName ?? string.Empty) + "|" + generation + "|" +
                sizeFactor.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    public sealed class AquacultureCommissionComponent : GameComponent
    {
        private AquacultureCommissionRecord activeCommission;
        private List<string> requestHistory = new List<string>();
        private readonly List<Thing> eligibleSpecimens = new List<Thing>();
        private string eligibleSpecimensCommissionId;
        private int nextOfferTick;
        private int nextEligibilityCheckTick;

        public AquacultureCommissionComponent(Game game)
        {
        }

        public static AquacultureCommissionComponent Current => Verse.Current.Game?.GetComponent<AquacultureCommissionComponent>();
        public AquacultureCommissionRecord ActiveCommission => activeCommission;

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref activeCommission, "activeCommission");
            Scribe_Collections.Look(ref requestHistory, "commissionRequestHistory", LookMode.Value);
            Scribe_Values.Look(ref nextOfferTick, "commissionNextOfferTick");
            Scribe_Values.Look(ref nextEligibilityCheckTick, "commissionNextEligibilityCheckTick");
            if (requestHistory == null) requestHistory = new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                requestHistory.RemoveAll(key => key.NullOrEmpty());
                if (activeCommission != null && (activeCommission.Breed == null || activeCommission.FishDef == null))
                    activeCommission = null;
                eligibleSpecimens.Clear();
                eligibleSpecimensCommissionId = null;
            }
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextEligibilityCheckTick) return;
            nextEligibilityCheckTick = now + AquacultureCommissionRules.EligibilityCheckIntervalTicks;

            if (activeCommission != null)
            {
                if (!activeCommission.IsExpired(now))
                {
                    RefreshEligibleSpecimens();
                    return;
                }
                AquacultureCommissionRecord expired = activeCommission;
                activeCommission = null;
                ClearEligibleSpecimens();
                nextOfferTick = now + AquacultureCommissionRules.OfferCooldownTicks;
                Messages.Message("AquacultureFishing.CommissionExpired".Translate(
                    expired.Breed?.name ?? "AquacultureFishing.CommissionUnknown".Translate()),
                    MessageTypeDefOf.CautionInput, false);
                return;
            }

            if (now < nextOfferTick) return;
            foreach (FishBreedRecord breed in AquacultureJournalComponent.Current?.Breeds
                ?.Where(item => item != null).OrderBy(item => item.id, StringComparer.Ordinal)
                ?? Enumerable.Empty<FishBreedRecord>())
            {
                if (TryOfferForBreed(breed, now)) break;
            }
        }

        public bool TryOfferForBreed(FishBreedRecord breed, int now = -1)
        {
            now = now >= 0 ? now : Find.TickManager?.TicksGame ?? 0;
            if (activeCommission != null || breed?.id.NullOrEmpty() != false || now < nextOfferTick || breed.FishDef == null)
                return false;

            List<CompFishTraits> candidates = HealthyBreedFish(breed).ToList();
            if (candidates.Count == 0) return false;

            List<FishTraitDef> traits = breed.traitDefNames?.Select(DefDatabase<FishTraitDef>.GetNamedSilentFail)
                .Where(trait => trait != null && candidates.Any(fish => fish.traitDefNames.Contains(trait.defName)))
                .OrderByDescending(AquacultureCommissionRules.TraitDifficulty)
                .ThenBy(trait => trait.defName, StringComparer.Ordinal).ToList() ?? new List<FishTraitDef>();
            if (traits.Count == 0) return false;

            float stability = Mathf.Clamp(Mathf.Round(breed.Stability * 100f) / 100f, 0.70f, 0.98f);
            FishTraitDef selectedTrait = null;
            string requestKey = null;
            int selectedGeneration = 1;
            float selectedSize = 1f;
            for (int i = 0; i < traits.Count; i++)
            {
                CompFishTraits bestCandidate = candidates
                    .Where(fish => fish.traitDefNames.Contains(traits[i].defName))
                    .OrderByDescending(fish => fish.breedGeneration)
                    .ThenByDescending(fish => fish.SizeFactor)
                    .ThenBy(fish => fish.parent.ThingID)
                    .FirstOrDefault();
                if (bestCandidate == null) continue;
                int candidateGeneration = Mathf.Max(1, bestCandidate.breedGeneration);
                float candidateSize = Mathf.Clamp(Mathf.Round(bestCandidate.SizeFactor * 100f) / 100f, 0.25f, 2f);
                string key = AquacultureCommissionRules.RequestKey(breed.id, traits[i].defName, candidateGeneration, candidateSize);
                if (!requestHistory.Contains(key))
                {
                    selectedTrait = traits[i];
                    requestKey = key;
                    selectedGeneration = candidateGeneration;
                    selectedSize = candidateSize;
                    break;
                }
            }
            if (selectedTrait == null) return false;

            float difficulty = AquacultureCommissionRules.TraitDifficulty(selectedTrait);
            int deadlineDays = AquacultureCommissionRules.DeadlineDays(difficulty, selectedGeneration);
            activeCommission = new AquacultureCommissionRecord
            {
                id = Guid.NewGuid().ToString("N"),
                breedId = breed.id,
                fishDefName = breed.fishDefName,
                traitDefName = selectedTrait.defName,
                minimumStability = stability,
                minimumGeneration = selectedGeneration,
                minimumSizeFactor = selectedSize,
                offeredTick = now,
                deadlineTick = now + deadlineDays * 60000,
                rewardSilver = AquacultureCommissionRules.RewardSilver(
                    selectedGeneration, stability, difficulty, selectedSize, deadlineDays)
            };
            requestHistory.Add(requestKey);
            if (requestHistory.Count > 64) requestHistory.RemoveAt(0);
            RefreshEligibleSpecimens();
            Find.LetterStack.ReceiveLetter(
                "AquacultureFishing.CommissionLetterLabel".Translate(),
                "AquacultureFishing.CommissionLetterText".Translate(
                    activeCommission.Breed?.name ?? breed.name, activeCommission.RequirementsText,
                    activeCommission.DaysRemaining(now), activeCommission.rewardSilver).ToString(),
                LetterDefOf.PositiveEvent, null, 0, true);
            return true;
        }

        public IEnumerable<Thing> EligibleSpecimens(AquacultureCommissionRecord commission = null)
        {
            commission = commission ?? activeCommission;
            if (commission == null) return Enumerable.Empty<Thing>();
            if (commission == activeCommission && eligibleSpecimensCommissionId != commission.id)
                RefreshEligibleSpecimens();
            return eligibleSpecimens.Where(thing => thing != null && !thing.Destroyed && thing.Spawned);
        }

        public int EligibleSpecimenCount
        {
            get
            {
                if (activeCommission != null && eligibleSpecimensCommissionId != activeCommission.id)
                    RefreshEligibleSpecimens();
                int count = 0;
                for (int i = 0; i < eligibleSpecimens.Count; i++)
                    if (eligibleSpecimens[i] != null && !eligibleSpecimens[i].Destroyed && eligibleSpecimens[i].Spawned) count++;
                return count;
            }
        }

        public void RefreshEligibleSpecimens()
        {
            eligibleSpecimens.Clear();
            eligibleSpecimensCommissionId = activeCommission?.id;
            if (activeCommission == null) return;
            foreach (Map map in Find.Maps ?? Enumerable.Empty<Map>())
            {
                foreach (Thing thing in map.listerThings.AllThings ?? Enumerable.Empty<Thing>())
                {
                    if (activeCommission.Matches(thing?.TryGetComp<CompFishTraits>(), true))
                        eligibleSpecimens.Add(thing);
                }
            }
        }

        private void ClearEligibleSpecimens()
        {
            eligibleSpecimens.Clear();
            eligibleSpecimensCommissionId = null;
        }

        public bool TryDeliver(Thing specimen, out string reason)
        {
            reason = null;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (activeCommission == null)
            {
                reason = "AquacultureFishing.CommissionNoActive".Translate().ToString();
                return false;
            }
            if (activeCommission.IsExpired(now))
            {
                reason = "AquacultureFishing.CommissionExpiredShort".Translate().ToString();
                return false;
            }
            CompFishTraits fish = specimen?.TryGetComp<CompFishTraits>();
            if (!activeCommission.Matches(fish, true))
            {
                reason = "AquacultureFishing.CommissionSpecimenInvalid".Translate().ToString();
                return false;
            }

            Thing reward = ThingMaker.MakeThing(ThingDefOf.Silver);
            reward.stackCount = activeCommission.rewardSilver;
            if (!GenPlace.TryPlaceThing(reward, specimen.Position, specimen.Map, ThingPlaceMode.Near))
            {
                reward.Destroy(DestroyMode.Vanish);
                reason = "AquacultureFishing.CommissionRewardPlacementFailed".Translate().ToString();
                return false;
            }
            specimen.Destroy(DestroyMode.Vanish);
            if (!specimen.Destroyed)
            {
                reward.Destroy(DestroyMode.Vanish);
                reason = "AquacultureFishing.CommissionSpecimenTransferFailed".Translate().ToString();
                return false;
            }

            FishBreedRecord breed = activeCommission.Breed;
            if (breed != null)
            {
                breed.commissionsCompleted++;
                breed.lastCommissionTick = now;
            }
            int rewardAmount = activeCommission.rewardSilver;
            string breedName = breed?.name ?? "AquacultureFishing.CommissionUnknown".Translate().ToString();
            activeCommission = null;
            ClearEligibleSpecimens();
            nextOfferTick = now + AquacultureCommissionRules.OfferCooldownTicks;
            Messages.Message("AquacultureFishing.CommissionCompleted".Translate(breedName, rewardAmount),
                MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public bool HasHistoryFor(FishBreedRecord breed)
        {
            if (breed == null) return false;
            return requestHistory.Any(key => key.StartsWith(breed.id + "|", StringComparison.Ordinal));
        }

        private static IEnumerable<CompFishTraits> HealthyBreedFish(FishBreedRecord breed)
        {
            if (breed?.FishDef == null) yield break;
            foreach (Map map in Find.Maps ?? Enumerable.Empty<Map>())
            {
                foreach (Thing thing in map.listerThings.AllThings ?? Enumerable.Empty<Thing>())
                {
                    CompFishTraits fish = thing.TryGetComp<CompFishTraits>();
                    if (fish == null || !FishUtility.IsRuntimeFish(thing.def) ||
                        fish.parent.def != breed.FishDef || fish.breedId != breed.id || !fish.IsAlive ||
                        !fish.parent.Spawned || fish.sterilized || !fish.IsAdult || fish.foodReserve < 0.35f ||
                        fish.starvationProgress > 0f || fish.waterStress >= 0.10f || fish.temperatureStress >= 0.10f ||
                        fish.habitatStress >= 0.75f) continue;
                    yield return fish;
                }
            }
        }
    }

    public static class AquacultureCommissionManager
    {
        public static AquacultureCommissionComponent Current => AquacultureCommissionComponent.Current;

        public static void NotifyBreedRegistered(FishBreedRecord breed)
        {
            Current?.TryOfferForBreed(breed);
        }
    }

    public sealed class Dialog_DeliverAquacultureCommission : Window
    {
        private readonly AquacultureCommissionComponent component;
        private readonly AquacultureCommissionRecord commission;
        private readonly List<Thing> specimens;
        private Vector2 scrollPosition;

        public Dialog_DeliverAquacultureCommission(AquacultureCommissionComponent component,
            AquacultureCommissionRecord commission)
        {
            this.component = component;
            this.commission = commission;
            component?.RefreshEligibleSpecimens();
            specimens = component?.EligibleSpecimens(commission)?.ToList() ?? new List<Thing>();
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnAccept = false;
            resizeable = false;
            draggable = true;
            layer = WindowLayer.Dialog;
        }

        public override Vector2 InitialSize => new Vector2(620f, Mathf.Clamp(180f + specimens.Count * 58f, 240f, 680f));

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f),
                "AquacultureFishing.CommissionDeliverTitle".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 42f),
                "AquacultureFishing.CommissionDeliverInstruction".Translate(
                    commission?.FishDef?.LabelCap ?? "AquacultureFishing.CommissionUnknown".Translate()).ToString());
            Rect outRect = new Rect(inRect.x, inRect.y + 84f, inRect.width, inRect.height - 84f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, specimens.Count * 58f));
            Widgets.BeginScrollView(outRect, ref scrollPosition, view);
            for (int i = 0; i < specimens.Count; i++)
            {
                Thing specimen = specimens[i];
                Rect row = new Rect(0f, i * 58f, view.width, 52f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.ThingIcon(new Rect(row.x + 4f, row.y + 4f, 44f, 44f), specimen);
                CompFishTraits fish = specimen.TryGetComp<CompFishTraits>();
                Widgets.Label(new Rect(row.x + 56f, row.y + 4f, view.width * 0.45f, 24f), specimen.LabelCap);
                Widgets.Label(new Rect(row.x + 56f, row.y + 28f, view.width * 0.45f, 22f),
                    "AquacultureFishing.CommissionSpecimenDetails".Translate(
                        fish?.breedGeneration ?? 0, fish?.SizeFactor.ToStringPercent() ?? "-").ToString());
                if (Widgets.ButtonText(new Rect(view.width - 150f, row.y + 9f, 140f, 34f),
                    "AquacultureFishing.CommissionDeliverButton".Translate().ToString()))
                {
                    if (component.TryDeliver(specimen, out string reason))
                    {
                        Close();
                        return;
                    }
                    Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                }
            }
            if (specimens.Count == 0)
                Widgets.Label(new Rect(8f, 8f, view.width - 16f, 32f), "AquacultureFishing.CommissionNoEligible".Translate());
            Widgets.EndScrollView();
        }
    }

    public static class AquacultureCommissionUi
    {
        public static void DrawBreedSection(Rect view, ref float y, FishBreedRecord breed)
        {
            y += 12f;
            Widgets.DrawLineHorizontal(0f, y, view.width);
            y += 16f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, view.width, 30f), "AquacultureFishing.CommissionTitle".Translate());
            Text.Font = GameFont.Small;
            y += 36f;

            AquacultureCommissionComponent component = AquacultureCommissionComponent.Current;
            AquacultureCommissionRecord active = component?.ActiveCommission;
            if (active?.breedId == breed?.id)
            {
                Widgets.Label(new Rect(0f, y, view.width, 24f), active.RequirementsText);
                y += 28f;
                int days = active.DaysRemaining(Find.TickManager?.TicksGame ?? 0);
                Widgets.Label(new Rect(0f, y, view.width, 24f),
                    "AquacultureFishing.CommissionPreview".Translate(days, active.rewardSilver).ToString());
                y += 30f;
                int candidates = component.EligibleSpecimenCount;
                if (candidates > 0 && Widgets.ButtonText(new Rect(0f, y, 220f, 34f),
                    "AquacultureFishing.CommissionDeliverButton".Translate().ToString()))
                    Find.WindowStack.Add(new Dialog_DeliverAquacultureCommission(component, active));
                else
                    Widgets.Label(new Rect(0f, y, view.width, 26f), "AquacultureFishing.CommissionNoEligible".Translate());
                y += 42f;
            }
            else if (component?.ActiveCommission != null)
            {
                Widgets.Label(new Rect(0f, y, view.width, 42f),
                    "AquacultureFishing.CommissionOtherActive".Translate().ToString());
                y += 48f;
            }
            else
            {
                Widgets.Label(new Rect(0f, y, view.width, 42f),
                    "AquacultureFishing.CommissionWaiting".Translate().ToString());
                y += 48f;
            }

            if (breed != null)
                Widgets.Label(new Rect(0f, y, view.width, 24f),
                    "AquacultureFishing.CommissionCompletedCount".Translate(breed.commissionsCompleted).ToString());
            y += 32f;
        }
    }
}
