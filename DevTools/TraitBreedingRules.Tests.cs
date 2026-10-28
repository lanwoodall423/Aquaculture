using AquacultureFishing;
using DeferredReality.Aquaculture;

static class TraitBreedingRulesTests
{
    private static int failures;

    private static TraitBreedingRules.Candidate Candidate(string id, string group, bool first = false, bool second = false, float weight = 1f)
    {
        return new TraitBreedingRules.Candidate
        {
            Id = id,
            CompatibilityGroup = group,
            PresentInFirstParent = first,
            PresentInSecondParent = second,
            Weight = weight
        };
    }

    private static void Check(string name, bool condition)
    {
        if (condition) return;
        Console.Error.WriteLine("FAIL: " + name);
        failures++;
    }

    private static List<string> Inherited(IEnumerable<TraitBreedingRules.Candidate> candidates, float roll, int cap = 8, Func<int, int, int> index = null, float baseChance = 0.5f)
    {
        return TraitBreedingRules.RollInheritedTraits(candidates, baseChance, cap, () => roll, index ?? ((minimum, maximumExclusive) => minimum));
    }

    private static void TestProbabilityBoundaries()
    {
        Check("zero probability", Inherited(new[] { Candidate("zero", "zero", true, true) }, 0f, baseChance: 0f).Count == 0);
        Check("full probability accepts one", Inherited(new[] { Candidate("full", "full", true, true) }, 1f, baseChance: 1f).SequenceEqual(new[] { "full" }));
        Check("full probability accepts a roll of one", TraitBreedingRules.RollNewMutationTraits(
            new[] { Candidate("full", "full") }, new HashSet<string>(), new HashSet<string>(), 1f, 1,
            () => 1f, (minimum, maximumExclusive) => minimum).SequenceEqual(new[] { "full" }));
        Check("negative cap is safe", Inherited(new[] { Candidate("negative", "negative") }, 0f, -2).Count == 0);
    }

    private static void TestParentalUnionAndCap()
    {
        var candidates = new[]
        {
            Candidate("both", "both", true, true),
            Candidate("single", "single", true, false)
        };
        List<string> result = Inherited(candidates, 0.60f);
        Check("dual-parent advantage", result.SequenceEqual(new[] { "both" }));

        int rolls = 0;
        result = TraitBreedingRules.RollInheritedTraits(new[]
        {
            Candidate("a", "a", true, true), Candidate("b", "b", true, true), Candidate("c", "c", true, true)
        }, 1f, 1, () => { rolls++; return 0f; }, (minimum, maximumExclusive) => maximumExclusive - 1);
        Check("every parental trait rolls independently", rolls == 3);
        Check("cap applies after successful rolls", result.Count == 1 && result[0] == "a");

        result = Inherited(new[] { Candidate("a", "same", true, true), Candidate("b", "same", true, true), Candidate("c", "other", true, true) }, 1f, 2, baseChance: 1f);
        Check("incompatible successes are reduced to one per group", result.Count == 2 && result.Contains("a") && result.Contains("c"));
    }

    private static List<string> DrawWeightedMutations(IEnumerable<TraitBreedingRules.Candidate> candidates,
        float weightedRoll, int maximum = 1, ISet<string> excluded = null, ISet<string> occupiedGroups = null)
    {
        int draw = 0;
        // The first random value decides whether mutation occurs; subsequent values choose weighted traits.
        return TraitBreedingRules.RollNewMutationTraits(candidates, excluded, occupiedGroups, 1f, maximum,
            () => draw++ == 0 ? 0f : weightedRoll, (minimum, maximumExclusive) => minimum);
    }

    private static void TestMutationsAndOutliers()
    {
        List<string> result = TraitBreedingRules.RollNewMutationTraits(new[]
        {
            Candidate("inherited", "one"), Candidate("occupied", "two"), Candidate("new", "three"),
            Candidate("duplicate", "three", weight: 0f), Candidate("invalid", "four", weight: float.NaN)
        }, new HashSet<string> { "inherited" }, new HashSet<string> { "two" }, 1f, 2,
            () => 0f, (minimum, maximumExclusive) => minimum);
        Check("mutations exclude inherited, occupied, duplicate groups, and invalid weights", result.SequenceEqual(new[] { "new" }));
        Check("empty mutation pool is safe", TraitBreedingRules.RollNewMutationTraits(
            Array.Empty<TraitBreedingRules.Candidate>(), new HashSet<string>(), new HashSet<string>(), 1f, 2,
            () => 0f, null).Count == 0);
        Check("traitless parent union is safe", Inherited(Array.Empty<TraitBreedingRules.Candidate>(), 1f).Count == 0);

        var hugeWeights = new[]
        {
            Candidate("huge-a", "a", weight: float.MaxValue),
            Candidate("huge-b", "b", weight: float.MaxValue)
        };
        Check("finite giant weights permit first mutation",
            DrawWeightedMutations(hugeWeights, 0f).SequenceEqual(new[] { "huge-a" }));
        Check("finite giant weights preserve later weighted selection",
            DrawWeightedMutations(hugeWeights, 0.75f).SequenceEqual(new[] { "huge-b" }));
        Check("weighted upper-bound draw selects last candidate",
            DrawWeightedMutations(hugeWeights, 1f).SequenceEqual(new[] { "huge-b" }));
        Check("finite giant weights retain multiple compatible mutation slots",
            DrawWeightedMutations(hugeWeights, 0.75f, 2).SequenceEqual(new[] { "huge-a", "huge-b" }));
        Check("three giant weights preserve middle bucket",
            DrawWeightedMutations(new[]
            {
                Candidate("one", "one", weight: float.MaxValue),
                Candidate("two", "two", weight: float.MaxValue),
                Candidate("three", "three", weight: float.MaxValue)
            }, 0.5f).SequenceEqual(new[] { "two" }));

        var ordinaryWeights = new[] { Candidate("light", "light", weight: 1f),
            Candidate("heavy", "heavy", weight: 3f) };
        Check("ordinary finite weights keep first weighted bucket",
            DrawWeightedMutations(ordinaryWeights, 0.2f).SequenceEqual(new[] { "light" }));
        Check("ordinary finite weights keep later weighted bucket",
            DrawWeightedMutations(ordinaryWeights, 0.5f).SequenceEqual(new[] { "heavy" }));
        Check("giant weights still exclude occupied compatibility groups",
            DrawWeightedMutations(new[]
            {
                Candidate("alpha", "same", weight: float.MaxValue),
                Candidate("beta", "same", weight: float.MaxValue),
                Candidate("gamma", "different", weight: float.MaxValue)
            }, 0f, 2).SequenceEqual(new[] { "alpha", "gamma" }));
        Check("giant weights still respect inherited exclusions",
            DrawWeightedMutations(hugeWeights, 0f, 2, new HashSet<string> { "huge-a" },
                new HashSet<string> { "a" }).SequenceEqual(new[] { "huge-b" }));
        Check("non-finite and nonpositive candidate weights remain excluded",
            DrawWeightedMutations(new[]
            {
                Candidate("nan", "nan", weight: float.NaN),
                Candidate("positive-infinity", "inf", weight: float.PositiveInfinity),
                Candidate("negative-infinity", "negative-inf", weight: float.NegativeInfinity),
                Candidate("zero", "zero", weight: 0f),
                Candidate("negative", "negative", weight: -1f)
            }, 0f, 5).Count == 0);
    }

    private static void TestStabilityAndMigration()
    {
        Check("low stability does not guarantee defining traits", TraitBreedingRules.RegisteredBreedDefiningTraitReliability(0.5f, 0.95f) == 0.475f);
        Check("stable breed reliability remains below one", TraitBreedingRules.RegisteredBreedDefiningTraitReliability(0.98f, 0.95f) < 1f);
        Check("matching success rate", TraitBreedingRules.SuccessRate(8, 10) == 0.8f);
        Check("non-qualifying births count in denominator", TraitBreedingRules.SuccessRate(1, 4) == 0.25f);
        Check("no births have no success rate", TraitBreedingRules.SuccessRate(0, 0) == 0f);
        Check("generation contribution is restrained", TraitBreedingRules.GenerationContribution(20) == 0.1f);
        Check("minimum integer generation cannot overflow into a bonus",
            TraitBreedingRules.GenerationContribution(int.MinValue) == 0f);
        Check("negative, zero and first generation have no bonus",
            TraitBreedingRules.GenerationContribution(-5) == 0f &&
            TraitBreedingRules.GenerationContribution(0) == 0f &&
            TraitBreedingRules.GenerationContribution(1) == 0f);
        Check("second generation retains one improvement step",
            TraitBreedingRules.GenerationContribution(2) == TraitBreedingRules.GenerationContributionPerGeneration);
        Check("maximum integer generation remains capped",
            TraitBreedingRules.GenerationContribution(int.MaxValue) == TraitBreedingRules.MaximumGenerationContribution);
        Check("invalid generation does not inflate resulting stability",
            TraitBreedingRules.ResultingStability(0, 0, int.MinValue) == TraitBreedingRules.BaseStability);
        Check("invalid generation does not inflate legacy stability",
            TraitBreedingRules.LegacyStability(0, int.MinValue) == 0.70f);
        Check("legacy second generation still contributes one step",
            Math.Abs(TraitBreedingRules.LegacyStability(0, 2) - 0.72f) < 0.0001f);
        Check("legacy maximum generation remains capped",
            TraitBreedingRules.LegacyStability(0, int.MaxValue) == 0.98f);
        Check("stability does not compound one success", TraitBreedingRules.ResultingStability(1, 1, 1) == 0.9f);
        Check("stable evidence example", Math.Abs(TraitBreedingRules.ResultingStability(8, 10, 6) - 0.92f) < 0.0001f);
        Check("legacy rate migrates to wild chance", TraitBreedingRules.MigrateLegacySettings(0, 0.37f, 4, 0.2f, 2).WildExceptionalTraitChance == 0.37f);
        Check("legacy cap migrates to inherited cap", TraitBreedingRules.MigrateLegacySettings(0, 0.37f, 4, 0.2f, 2).MaximumInheritedTraits == 4);
        Check("new settings are not overwritten", TraitBreedingRules.MigrateLegacySettings(1, 0.37f, 4, 0.61f, 3).WildExceptionalTraitChance == 0.61f);
        Check("legacy births retain established floor", TraitBreedingRules.LegacyStability(3, 2) >= 0.79f);
    }

    private static void TestFishAgeTiming()
    {
        Check("ordinary fish aging fraction is preserved",
            FishAgeTimingRules.FractionElapsed(60000, 0, 120000) == 0.5f &&
            FishAgeTimingRules.FractionElapsed(120000, 0, 120000) == 1f &&
            FishAgeTimingRules.FractionElapsed(0, 60000, 120000) == 0f);
        Check("ordinary expiry uses exact lifetime boundary",
            !FishAgeTimingRules.HasExpired(119999, 0, 120000) &&
            FishAgeTimingRules.HasExpired(120000, 0, 120000));
        Check("ordinary remaining lifespan and next check are unchanged",
            FishAgeTimingRules.RemainingTicks(90000, 0, 120000) == 30000 &&
            FishAgeTimingRules.RemainingTicks(120000, 0, 120000) == 0 &&
            FishAgeTimingRules.NextCheckTick(100, 250) == 351);
        Check("nonpositive lifespan never expires or creates an age fraction",
            FishAgeTimingRules.FractionElapsed(int.MaxValue, 0, 0) == 0f &&
            FishAgeTimingRules.FractionElapsed(int.MaxValue, 0, -1) == 0f &&
            !FishAgeTimingRules.HasExpired(int.MaxValue, 0, 0) &&
            !FishAgeTimingRules.HasExpired(int.MaxValue, 0, -1) &&
            FishAgeTimingRules.RemainingTicks(int.MaxValue, 0, -1) == 0);

        Check("very old fish reaches elder age despite elapsed tick overflow",
            FishAgeTimingRules.FractionElapsed(int.MaxValue, int.MinValue, 120000) == 1f);
        Check("birth in the future cannot appear aged after subtraction overflow",
            FishAgeTimingRules.FractionElapsed(int.MinValue, int.MaxValue, 100) == 0f);
        Check("very old fish expires despite elapsed tick overflow",
            FishAgeTimingRules.HasExpired(int.MaxValue, int.MinValue, 120000));
        Check("future birth never expires early despite subtraction overflow",
            !FishAgeTimingRules.HasExpired(int.MinValue, int.MaxValue, 1));
        Check("expired fish has no remaining lifespan after tick overflow",
            FishAgeTimingRules.RemainingTicks(int.MaxValue, int.MinValue, 120000) == 0);
        Check("future birth never displays more than configured lifespan",
            FishAgeTimingRules.RemainingTicks(10, 11, 120000) == 120000 &&
            FishAgeTimingRules.RemainingTicks(int.MinValue, int.MaxValue, 120000) == 120000);
        Check("future age check saturates rather than wrapping negative",
            FishAgeTimingRules.NextCheckTick(int.MaxValue - 2, 5) == int.MaxValue);
        Check("negative threshold offsets cannot schedule before birth",
            FishAgeTimingRules.NextCheckTick(100, -5) == 101);
        Check("lower tick boundary retains valid checks",
            FishAgeTimingRules.NextCheckTick(int.MinValue, 0) == int.MinValue + 1);

        Check("ordinary oldest-first pond harvest age keys remain exact",
            FishAgeTimingRules.AgeTicksForOrdering(60000, 10000) == 50000L &&
            FishAgeTimingRules.AgeTicksForOrdering(60000, 30000) == 30000L &&
            FishAgeTimingRules.AgeTicksForOrdering(60000, 60001) == -1L &&
            FishAgeTimingRules.AgeTicksForOrdering(100, -500) == 600L);
        Check("very old fish retain full age across positive signed overflow",
            FishAgeTimingRules.AgeTicksForOrdering(int.MaxValue, int.MinValue) == 4294967295L);
        Check("saved fish with future birth retain negative age across signed overflow",
            FishAgeTimingRules.AgeTicksForOrdering(int.MinValue, int.MaxValue) == -4294967295L);
        Check("manual harvest oldest-first sort cannot reverse when an old fish has a negative birth tick",
            new[] { 0, int.MinValue }.OrderByDescending(birth =>
                FishAgeTimingRules.AgeTicksForOrdering(int.MaxValue, birth)).First() == int.MinValue);
        Check("automatic harvest does not treat a future-born fish as oldest after tick overflow",
            new[] { int.MaxValue, int.MinValue }.OrderByDescending(birth =>
                FishAgeTimingRules.AgeTicksForOrdering(int.MinValue, birth)).First() == int.MinValue);
        Check("ordinary harvest ordering still prefers the older birth date",
            new[] { 50000, 20000, 30000 }.OrderByDescending(birth =>
                FishAgeTimingRules.AgeTicksForOrdering(60000, birth)).SequenceEqual(new[] { 20000, 30000, 50000 }));

        Check("ordinary egg hatch and breeding rescan intervals remain exact",
            FishAgeTimingRules.ScheduleAfterTicks(100, 3 * 60000) == 180100 &&
            FishAgeTimingRules.ScheduleAfterTicks(100, 250) == 350 &&
            FishAgeTimingRules.ScheduleAfterTicks(100, 900000) == 900100);
        Check("zero tick delay keeps the present due time",
            FishAgeTimingRules.ScheduleAfterTicks(100, 0) == 100);
        Check("negative corrupted delay cannot schedule an egg before its creation",
            FishAgeTimingRules.ScheduleAfterTicks(100, -500) == 100);
        Check("breeding cooldown does not wrap negative near the tick ceiling",
            FishAgeTimingRules.ScheduleAfterTicks(int.MaxValue - 100, 250) == int.MaxValue);
        Check("egg hatch time does not wrap negative near the tick ceiling",
            FishAgeTimingRules.ScheduleAfterTicks(int.MaxValue - 60, 3 * 60000) == int.MaxValue);
        Check("extreme but valid cooldown saturates without overflow",
            FishAgeTimingRules.ScheduleAfterTicks(int.MaxValue - 1, int.MaxValue) == int.MaxValue);
        Check("representable due time near the ceiling remains exact",
            FishAgeTimingRules.ScheduleAfterTicks(int.MaxValue - 300, 250) == int.MaxValue - 50);
        Check("negative base tick scheduling stays exact within range",
            FishAgeTimingRules.ScheduleAfterTicks(int.MinValue, int.MaxValue) == -1 &&
            FishAgeTimingRules.ScheduleAfterTicks(int.MinValue, 1) == int.MinValue + 1);

        Check("ordinary three-day hatch countdown keeps its exact display ticks",
            FishAgeTimingRules.RemainingUntilTick(100, 180100) == 180000 &&
            FishAgeTimingRules.RemainingUntilTick(100, 350) == 250);
        Check("hatch countdown reaches zero at the deadline and stays there",
            FishAgeTimingRules.RemainingUntilTick(349, 350) == 1 &&
            FishAgeTimingRules.RemainingUntilTick(350, 350) == 0 &&
            FishAgeTimingRules.RemainingUntilTick(351, 350) == 0);
        Check("small negative saved game ticks preserve normal countdown",
            FishAgeTimingRules.RemainingUntilTick(-500, 500) == 1000 &&
            FishAgeTimingRules.RemainingUntilTick(-1, 0) == 1);
        Check("valid future hatch deadline cannot wrap to a false zero",
            FishAgeTimingRules.RemainingUntilTick(int.MinValue, int.MaxValue) == int.MaxValue);
        Check("future deadline beyond int remaining range clamps instead of underflow",
            FishAgeTimingRules.RemainingUntilTick(int.MinValue, 0) == int.MaxValue);
        Check("expired hatch deadline cannot wrap into a positive countdown",
            FishAgeTimingRules.RemainingUntilTick(int.MaxValue, int.MinValue) == 0 &&
            FishAgeTimingRules.RemainingUntilTick(int.MaxValue - 1, int.MinValue) == 0);
        Check("exact maximum representable future countdown is preserved",
            FishAgeTimingRules.RemainingUntilTick(0, int.MaxValue) == int.MaxValue &&
            FishAgeTimingRules.RemainingUntilTick(int.MinValue + 1, 0) == int.MaxValue);
        Check("negative extreme deadlines preserve one-tick boundaries",
            FishAgeTimingRules.RemainingUntilTick(int.MinValue, int.MinValue + 1) == 1 &&
            FishAgeTimingRules.RemainingUntilTick(int.MinValue + 2, int.MinValue + 1) == 0);
    }

    private static void TestPondEcologyTiming()
    {
        Check("new ponds and unloaded ecology history retain one-hour first update",
            PondEcologyTimingRules.ElapsedHours(2500, 0) == 1f &&
            PondEcologyTimingRules.ElapsedHours(int.MaxValue, -1) == 1f);
        Check("ordinary pond ecology delta retains 1-hour progression",
            PondEcologyTimingRules.ElapsedHours(3500, 1000) == 1f);
        Check("pond ecology delta remains clamped at the usual lower and upper bounds",
            PondEcologyTimingRules.ElapsedHours(1250, 1000) == 0.1f &&
            PondEcologyTimingRules.ElapsedHours(100000, 1000) == 24f &&
            PondEcologyTimingRules.ElapsedHours(1000, 3500) == 0.1f);
        Check("overflowing backwards ecology clock cannot simulate a full day",
            PondEcologyTimingRules.ElapsedHours(int.MinValue, 12345) == 0.1f);
        Check("future positive saved ecology ticks never overflow into full-day progress",
            PondEcologyTimingRules.ElapsedHours(int.MinValue + 100, 1000) == 0.1f);
        Check("large valid forward ecology elapsed ticks remain bounded",
            PondEcologyTimingRules.ElapsedHours(int.MaxValue, 1) == 24f);

        Check("normal pond ecology initial jitter and interval retain their schedules",
            FishAgeTimingRules.ScheduleAfterTicks(1000, 250 + 375) == 1625 &&
            FishAgeTimingRules.ScheduleAfterTicks(1000, 2500) == 3500);
        Check("pond ecology initial and recurring schedules cannot wrap at the tick limit",
            FishAgeTimingRules.ScheduleAfterTicks(int.MaxValue - 100, 250 + 375) == int.MaxValue &&
            FishAgeTimingRules.ScheduleAfterTicks(int.MaxValue - 100, 625) == int.MaxValue);
    }

    private static void TestPondEffectTiming()
    {
        Check("ordinary pond swimming status effect durations retain exact expiry",
            PondEffectTimingRules.ExpirationTick(100, 30000) == 30100 &&
            PondEffectTimingRules.ExpirationTick(100, 60000) == 60100);
        Check("empty-effect fallback retains default expiry period",
            PondEffectTimingRules.ExpirationTick(100, 30000) == 30100);
        Check("zero and corrupted negative durations cannot schedule expiry in the past",
            PondEffectTimingRules.ExpirationTick(100, 0) == 100 &&
            PondEffectTimingRules.ExpirationTick(100, -30000) == 100);
        Check("per-effect expiry near game tick limit does not wrap into the past",
            PondEffectTimingRules.ExpirationTick(int.MaxValue - 100, 30000) == int.MaxValue);
        Check("empty-effect fallback expiry near game tick limit does not wrap negative",
            PondEffectTimingRules.ExpirationTick(int.MaxValue - 10, 30000) == int.MaxValue);
        Check("largest valid duration never wraps or removes a fresh pond effect",
            PondEffectTimingRules.ExpirationTick(int.MaxValue - 1, int.MaxValue) == int.MaxValue);
        Check("representable expiry near tick limit remains exact",
            PondEffectTimingRules.ExpirationTick(int.MaxValue - 30100, 30000) == int.MaxValue - 100);
        Check("negative saved game tick plus representable duration remains exact",
            PondEffectTimingRules.ExpirationTick(int.MinValue, int.MaxValue) == -1 &&
            PondEffectTimingRules.ExpirationTick(int.MinValue + 5, 30000) == int.MinValue + 30005);
        Check("multi-effect pond expiration uses the latest safe deadline",
            new[] { 120, 600, 30000 }
                .Select(duration => PondEffectTimingRules.ExpirationTick(int.MaxValue - 500, duration))
                .Max() == int.MaxValue &&
            new[] { 120, 600, 30000 }
                .Select(duration => PondEffectTimingRules.ExpirationTick(100, duration))
                .Max() == 30100);
    }

    private static void TestFishSchoolTiming()
    {
        Check("normal school update cadence remains unchanged",
            FishSchoolTimingRules.IsUpdateDue(106, 100, 6) &&
            !FishSchoolTimingRules.IsUpdateDue(105, 100, 6) &&
            FishSchoolTimingRules.IsUpdateDue(160, 100, 60) &&
            !FishSchoolTimingRules.IsUpdateDue(219, 100, 120) &&
            FishSchoolTimingRules.IsUpdateDue(220, 100, 120));
        Check("uninitialized school remains eligible for update",
            FishSchoolTimingRules.IsUpdateDue(0, -99999, 6) &&
            FishSchoolTimingRules.SimulationStepTicks(0, -99999, 120) == 120);
        Check("school update does not stall on overflowing default timestamp",
            FishSchoolTimingRules.IsUpdateDue(int.MaxValue, -99999, 6));
        Check("future school timestamp cannot trigger update after subtraction wrap",
            !FishSchoolTimingRules.IsUpdateDue(int.MinValue, 1, 6));
        Check("normal school simulation step retains capped elapsed ticks",
            FishSchoolTimingRules.SimulationStepTicks(103, 100, 6) == 3 &&
            FishSchoolTimingRules.SimulationStepTicks(131, 100, 6) == 30 &&
            FishSchoolTimingRules.SimulationStepTicks(100, 101, 6) == 1);
        Check("future school timestamp cannot induce a full 30-tick step through overflow",
            FishSchoolTimingRules.SimulationStepTicks(int.MinValue, 1, 60) == 1);
        Check("school interpolation keeps original half-step and clamping",
            FishSchoolTimingRules.InterpolationAlpha(103, 100, 6) == 0.5f &&
            FishSchoolTimingRules.InterpolationAlpha(99, 100, 6) == 0f &&
            FishSchoolTimingRules.InterpolationAlpha(107, 100, 6) == 1f &&
            FishSchoolTimingRules.InterpolationAlpha(101, 100, 0) == 1f);
        Check("school interpolation never reverses after long positive elapsed ticks",
            FishSchoolTimingRules.InterpolationAlpha(int.MaxValue, -1, 6) == 1f);
        Check("school interpolation never advances on an extreme future saved timestamp",
            FishSchoolTimingRules.InterpolationAlpha(int.MinValue, int.MaxValue, 6) == 0f);
        Check("normal school/depth/target/rest due-tick intervals remain exact",
            FishAgeTimingRules.ScheduleAfterTicks(100, 6) == 106 &&
            FishAgeTimingRules.ScheduleAfterTicks(100, 600 + 375) == 1075 &&
            FishAgeTimingRules.ScheduleAfterTicks(100, 240 + 419) == 759 &&
            FishAgeTimingRules.ScheduleAfterTicks(100, 90 + 179) == 369);
        Check("school/depth/target/rest due ticks saturate at the integer ceiling",
            FishAgeTimingRules.ScheduleAfterTicks(int.MaxValue - 1, 6) == int.MaxValue &&
            FishAgeTimingRules.ScheduleAfterTicks(int.MaxValue - 100, 600 + 375) == int.MaxValue &&
            FishAgeTimingRules.ScheduleAfterTicks(int.MaxValue - 100, 240 + 419) == int.MaxValue &&
            FishAgeTimingRules.ScheduleAfterTicks(int.MaxValue - 100, 90 + 179) == int.MaxValue);
    }

    private static void TestRecoveryRules()
    {
        NaturalWaterHabitat[] habitats = (NaturalWaterHabitat[])Enum.GetValues(typeof(NaturalWaterHabitat));
        for (int destinationIndex = 0; destinationIndex < habitats.Length; destinationIndex++)
        {
            NaturalWaterHabitat destination = habitats[destinationIndex];
            for (int sourceIndex = 0; sourceIndex < habitats.Length; sourceIndex++)
            {
                NaturalWaterHabitat source = habitats[sourceIndex];
                bool expected = NaturalFishMigrationRules.IsOpen(destination) && destination == source;
                bool actual = NaturalFishMigrationRules.HasCompatibleRecordedSource(
                    new[] { new NaturalFishMigrationSource(source, "fish", 1f) }, destination, "fish");
                Check("water compatibility " + source + " to " + destination, actual == expected);
            }
        }
        var riverSource = new NaturalFishMigrationSource(NaturalWaterHabitat.River, "fish", 1f);
        var zeroRiver = new NaturalFishMigrationSource(NaturalWaterHabitat.River, "fish", 0f);
        Check("closed water cannot recover regionally", !NaturalFishMigrationRules.HasCompatibleRecordedSource(
            new[] { riverSource }, NaturalWaterHabitat.Pond, "fish"));
        Check("river recovers only from a positive river source", NaturalFishMigrationRules.HasCompatibleRecordedSource(
            new[] { riverSource }, NaturalWaterHabitat.River, "fish"));
        Check("zero source cannot recover", !NaturalFishMigrationRules.HasCompatibleRecordedSource(
            new[] { zeroRiver }, NaturalWaterHabitat.River, "fish"));
        Check("only finite positive regional sources contribute migration pressure",
            NaturalFishMigrationRules.IsValidRecordedSourcePopulation(float.Epsilon) &&
            NaturalFishMigrationRules.IsValidRecordedSourcePopulation(float.MaxValue) &&
            !NaturalFishMigrationRules.IsValidRecordedSourcePopulation(0f) &&
            !NaturalFishMigrationRules.IsValidRecordedSourcePopulation(-1f) &&
            !NaturalFishMigrationRules.IsValidRecordedSourcePopulation(float.NaN) &&
            !NaturalFishMigrationRules.IsValidRecordedSourcePopulation(float.PositiveInfinity) &&
            !NaturalFishMigrationRules.IsValidRecordedSourcePopulation(float.NegativeInfinity));
        var invalidRegionalSources = new[]
        {
            new NaturalFishMigrationSource(NaturalWaterHabitat.River, "fish", float.NaN),
            new NaturalFishMigrationSource(NaturalWaterHabitat.River, "fish", float.PositiveInfinity),
            new NaturalFishMigrationSource(NaturalWaterHabitat.River, "fish", float.NegativeInfinity),
            new NaturalFishMigrationSource(NaturalWaterHabitat.River, "fish", -2f)
        };
        Check("corrupt regional source counts cannot authorize migration",
            !NaturalFishMigrationRules.HasCompatibleRecordedSource(
                invalidRegionalSources, NaturalWaterHabitat.River, "fish"));
        Check("valid source still authorizes migration alongside corrupt records",
            NaturalFishMigrationRules.HasCompatibleRecordedSource(
                invalidRegionalSources.Concat(new[] { riverSource }), NaturalWaterHabitat.River, "fish"));
        Check("water categories do not cross", !NaturalFishMigrationRules.HasCompatibleRecordedSource(
            new[] { new NaturalFishMigrationSource(NaturalWaterHabitat.Coastal, "fish", 1f) },
            NaturalWaterHabitat.River, "fish"));
        Check("source species must match", !NaturalFishMigrationRules.HasCompatibleRecordedSource(
            new[] { new NaturalFishMigrationSource(NaturalWaterHabitat.River, "other", 1f) },
            NaturalWaterHabitat.River, "fish"));
        Check("floor is recoverable", NaturalFishMigrationRules.NeedsRegionalRecovery(2f, 2f));
        Check("below floor is recoverable", NaturalFishMigrationRules.NeedsRegionalRecovery(1.99f, 2f));
        Check("above floor is not recoverable", !NaturalFishMigrationRules.NeedsRegionalRecovery(2.01f, 2f));
        Check("non-finite destination populations cannot authorize local recovery",
            !NaturalFishMigrationRules.NeedsRegionalRecovery(float.NaN, 2f) &&
            !NaturalFishMigrationRules.NeedsRegionalRecovery(float.PositiveInfinity, 2f) &&
            !NaturalFishMigrationRules.NeedsRegionalRecovery(float.NegativeInfinity, 2f));
        Check("non-finite breeding floors cannot authorize local recovery",
            !NaturalFishMigrationRules.NeedsRegionalRecovery(0f, float.NaN) &&
            !NaturalFishMigrationRules.NeedsRegionalRecovery(0f, float.PositiveInfinity) &&
            !NaturalFishMigrationRules.NeedsRegionalRecovery(0f, float.NegativeInfinity));
        Check("finite local population thresholds preserve existing behavior",
            NaturalFishMigrationRules.NeedsRegionalRecovery(2f, 2f) &&
            !NaturalFishMigrationRules.NeedsRegionalRecovery(2.01f, 2f) &&
            NaturalFishMigrationRules.NeedsRegionalRecovery(0f, -5f) &&
            !NaturalFishMigrationRules.NeedsRegionalRecovery(0.01f, -5f) &&
            NaturalFishMigrationRules.NeedsRegionalRecovery(-1f, 2f));
    }

    private static void TestKnowledgeContract()
    {
        bool Supports(int api, string capability) => api <= 3 && !capability.Equals("missing", StringComparison.Ordinal);
        int CapabilityVersion(string capability) => capability.Equals("old", StringComparison.Ordinal) ? 2 : 3;
        Check("knowledge V3 rejects old API", !AquacultureKnowledgeContract.SupportsV3(2, Supports, CapabilityVersion));
        Check("knowledge V3 rejects missing capability", !AquacultureKnowledgeContract.SupportsV3(3,
            (api, capability) => capability != "contextual-knowledge", CapabilityVersion));
        Check("knowledge V3 rejects old capability generation", !AquacultureKnowledgeContract.SupportsV3(3,
            Supports, capability => capability == "claims" ? 2 : 3));
        Check("knowledge V3 accepts complete capability contract", AquacultureKnowledgeContract.SupportsV3(3, Supports,
            CapabilityVersion));

        // The published array is retained for binary compatibility, but callers must
        // not be able to rewrite what the internal V3 compatibility gate requires.
        string[] publicChecklist = AquacultureKnowledgeContract.RequiredV3Capabilities;
        string[] originalChecklist = (string[])publicChecklist.Clone();
        try
        {
            publicChecklist[0] = "optional-replacement";
            Check("mutating published capability list cannot bypass mandatory domains",
                !AquacultureKnowledgeContract.SupportsV3(3,
                    (api, capability) => capability != "domains", capability => 3));
            publicChecklist[15] = "optional-replacement";
            Check("mutating published capability list cannot bypass contextual knowledge",
                !AquacultureKnowledgeContract.SupportsV3(3,
                    (api, capability) => capability != "contextual-knowledge", capability => 3));
            Check("altered public checklist cannot suppress a required capability version",
                !AquacultureKnowledgeContract.SupportsV3(3,
                    (api, capability) => true, capability => capability == "domains" ? 2 : 3));

            Array.Clear(publicChecklist, 0, publicChecklist.Length);
            Check("clearing published checklist cannot bypass another mandatory capability",
                !AquacultureKnowledgeContract.SupportsV3(3,
                    (api, capability) => capability != "claims", capability => 3));
            Check("complete V3 still works after a caller clears the published checklist",
                AquacultureKnowledgeContract.SupportsV3(3,
                    (api, capability) => capability != null, capability => 3));
        }
        finally
        {
            // Do not contaminate the rest of the independently executable suite.
            Array.Copy(originalChecklist, publicChecklist, originalChecklist.Length);
        }
        Check("published V3 checklist retains its original field entries",
            publicChecklist.SequenceEqual(originalChecklist));

        string eventId = AquacultureKnowledgeContract.StableEventId("catch", "pawn:4", 100, "map:1", "fish");
        Check("knowledge event identity is deterministic", eventId == AquacultureKnowledgeContract.StableEventId(
            "catch", "pawn:4", 100, "map:1", "fish"));
        Check("knowledge event identity separates logical events", eventId != AquacultureKnowledgeContract.StableEventId(
            "catch", "pawn:4", 101, "map:1", "fish"));
        Check("migration waits for durable commit", !AquacultureKnowledgeContract.CanFinalizeMigration(new[] { true }, false));
        Check("migration retries incomplete imports", !AquacultureKnowledgeContract.CanFinalizeMigration(new[] { true, false }, true));
        Check("migration finalizes only after every import commits", AquacultureKnowledgeContract.CanFinalizeMigration(
            new[] { true, true }, true));
        Check("invalid knowledge scalar rejected", !AquacultureKnowledgeContract.IsFiniteNonNegative(float.NaN)
            && !AquacultureKnowledgeContract.IsFiniteNonNegative(float.PositiveInfinity));
        Check("knowledge registration waits for every phase", !AquacultureKnowledgeContract.RegistrationComplete(true, false, true));
        Check("knowledge registration accepts complete phases", AquacultureKnowledgeContract.RegistrationComplete(true, true, true));
        Check("knowledge subject retention is bounded", AquacultureKnowledgeContract.DynamicSubjectCountAllowed(
            AquacultureKnowledgeContract.MaximumDynamicSubjects));
        Check("knowledge subject retention rejects overflow", !AquacultureKnowledgeContract.DynamicSubjectCountAllowed(
            AquacultureKnowledgeContract.MaximumDynamicSubjects + 1));
        string contextId = AquacultureKnowledgeContract.StableContextId("managed_pond", 4, "proxy:9", "topology:9", "freshwater");
        Check("knowledge context identity is deterministic", contextId == AquacultureKnowledgeContract.StableContextId(
            "managed_pond", 4, "proxy:9", "topology:9", "freshwater"));
        Check("knowledge context identity separates topology", contextId != AquacultureKnowledgeContract.StableContextId(
            "managed_pond", 4, "proxy:9", "topology:10", "freshwater"));
    }

    private static void TestDeferredRealityProviderRules()
    {
        string region = DeferredRealityProviderRules.Stable("region", "tile:42", "surface");
        Check("deferred region identity is deterministic", region == DeferredRealityProviderRules.Stable(
            "region", "tile:42", "surface"));
        Check("deferred map instances do not collide", DeferredRealityProviderRules.RegionId(42, "MapParent_A") !=
            DeferredRealityProviderRules.RegionId(42, "MapParent_B"));
        Check("deferred population identity separates species", DeferredRealityProviderRules.PopulationId(
            region, "water:river", "fish:a") != DeferredRealityProviderRules.PopulationId(
            region, "water:river", "fish:b"));
        Check("deferred population identity retains legacy subject shape", DeferredRealityProviderRules.PopulationId(
            region, "water:river", "fish:a").EndsWith(":water:river:fish:fish:a", StringComparison.Ordinal));
        Check("deferred process identity retains legacy key shape", DeferredRealityProviderRules.ProcessId("population:a") ==
            "aquaculture:population:population:a");
        Check("closed deferred water cannot migrate", !DeferredRealityProviderRules.AreCompatibleHabitats("Pond", "Pond"));
        Check("cross-category deferred migration is rejected", !DeferredRealityProviderRules.AreCompatibleHabitats("River", "Coastal"));
        Check("valid deferred recovery requires positive source", DeferredRealityProviderRules.CanRecover(
            "fish", "fish", "River", "River", 1f, 0f, 2f));
        Check("zero deferred source cannot recover", !DeferredRealityProviderRules.CanRecover(
            "fish", "fish", "River", "River", 0f, 0f, 2f));
        Check("non-finite breeding floors cannot authorize deferred recovery",
            !DeferredRealityProviderRules.CanRecover("fish", "fish", "River", "River", 1f, 0f, float.NaN) &&
            !DeferredRealityProviderRules.CanRecover("fish", "fish", "River", "River", 1f, 0f, float.PositiveInfinity) &&
            !DeferredRealityProviderRules.CanRecover("fish", "fish", "River", "River", 1f, 0f, float.NegativeInfinity));
        Check("infinite deferred floor cannot reclassify a full destination as recoverable",
            !DeferredRealityProviderRules.CanRecover("fish", "fish", "River", "River",
                1f, float.MaxValue, float.PositiveInfinity));
        Check("deferred finite negative floor retains clamping to zero",
            DeferredRealityProviderRules.CanRecover("fish", "fish", "River", "River", 1f, 0f, -5f) &&
            !DeferredRealityProviderRules.CanRecover("fish", "fish", "River", "River", 1f, 0.01f, -5f));
        Check("deferred recovery still requires valid species, habitat and populations",
            !DeferredRealityProviderRules.CanRecover("fish", "other", "River", "River", 1f, 0f, 2f) &&
            !DeferredRealityProviderRules.CanRecover("fish", "fish", "River", "Coastal", 1f, 0f, 2f) &&
            !DeferredRealityProviderRules.CanRecover("fish", "fish", "River", "River", float.PositiveInfinity, 0f, 2f) &&
            !DeferredRealityProviderRules.CanRecover("fish", "fish", "River", "River", 1f, float.NegativeInfinity, 2f));
        Check("invalid deferred scalar normalizes", DeferredRealityProviderRules.NormalizeNonNegative(float.NaN) == 0f &&
            DeferredRealityProviderRules.NormalizeNonNegative(4f, 0f, 2f) == 2f);
        Check("invalid deferred capacity limits fail closed",
            DeferredRealityProviderRules.NormalizeNonNegative(4f, 0f, float.NaN) == 0f &&
            DeferredRealityProviderRules.NormalizeNonNegative(4f, 0f, float.PositiveInfinity) == 0f &&
            DeferredRealityProviderRules.NormalizeNonNegative(4f, 0f, float.NegativeInfinity) == 0f &&
            DeferredRealityProviderRules.NormalizeNonNegative(4f, 0f, -1f) == 0f &&
            DeferredRealityProviderRules.NormalizeNonNegative(4f, 0f, 0f) == 0f);
        Check("invalid deferred fallback cannot produce a non-finite population",
            DeferredRealityProviderRules.NormalizeNonNegative(float.NaN, float.NaN) == 0f &&
            DeferredRealityProviderRules.NormalizeNonNegative(float.PositiveInfinity, float.PositiveInfinity) == 0f &&
            DeferredRealityProviderRules.NormalizeNonNegative(float.NegativeInfinity, float.NegativeInfinity, 2f) == 0f);
        Check("valid deferred fallback and capacity preserve finite clamping",
            DeferredRealityProviderRules.NormalizeNonNegative(float.NaN, 1.5f, 2f) == 1.5f &&
            DeferredRealityProviderRules.NormalizeNonNegative(float.NaN, 3f, 2f) == 2f &&
            DeferredRealityProviderRules.NormalizeNonNegative(-2f, 1.5f, 2f) == 0f &&
            DeferredRealityProviderRules.NormalizeNonNegative(1.25f, 0f, 2f) == 1.25f);
        Check("invalid deferred population capacity prevents growth",
            DeferredRealityProviderRules.AdvancePopulation(2f, float.NaN, 60000f, 1f) == 0f &&
            DeferredRealityProviderRules.AdvancePopulation(2f, float.PositiveInfinity, 60000f, 1f) == 0f);
        Check("deferred advancement is bounded", DeferredRealityProviderRules.AdvancePopulation(2f, 3f, 60000f, 1f) <= 3f);
        float thirtyDays = 30f * 60000f;
        float halfMaximum = float.MaxValue / 2f;
        float highProjection = DeferredRealityProviderRules.AdvancePopulation(
            halfMaximum, float.MaxValue, thirtyDays, 1f);
        Check("large finite population survives intermediate growth overflow",
            !float.IsNaN(highProjection) && !float.IsInfinity(highProjection) &&
            Math.Abs((double)highProjection / halfMaximum - 1.75d) < 0.0001d);
        Check("maximum finite population growth clamps to carrying capacity",
            DeferredRealityProviderRules.AdvancePopulation(
                float.MaxValue, float.MaxValue, thirtyDays, 1f) == float.MaxValue);
        Check("extreme finite elapsed time retains positive bounded growth",
            DeferredRealityProviderRules.AdvancePopulation(
                halfMaximum, float.MaxValue, float.MaxValue, 1f) == float.MaxValue);
        Check("large finite population with zero suitability retains mortality",
            Math.Abs((double)DeferredRealityProviderRules.AdvancePopulation(
                halfMaximum, float.MaxValue, thirtyDays, 0f) / halfMaximum - 0.70d) < 0.0001d);
        Check("ordinary growth, low-population decline and capacity remain stable",
            Math.Abs(DeferredRealityProviderRules.AdvancePopulation(2f, 3f, 60000f, 1f) - 2.05f) < 0.00001f &&
            Math.Abs(DeferredRealityProviderRules.AdvancePopulation(1f, 3f, 60000f, 1f) - 0.99f) < 0.00001f &&
            DeferredRealityProviderRules.AdvancePopulation(4f, 3f, 0f, 1f) == 3f);
        Check("invalid advancement inputs retain safe normalization",
            DeferredRealityProviderRules.AdvancePopulation(float.NaN, 3f, 60000f, 1f) == 0f &&
            DeferredRealityProviderRules.AdvancePopulation(-5f, 3f, 60000f, 1f) == 0f &&
            DeferredRealityProviderRules.AdvancePopulation(2f, 3f, float.NaN, 1f) == 2f &&
            DeferredRealityProviderRules.AdvancePopulation(2f, 3f, float.NegativeInfinity, 1f) == 2f &&
            DeferredRealityProviderRules.AdvancePopulation(2f, 0f, 60000f, 1f) == 0f);
        Check("ordinary demographic random variation remains unchanged",
            Math.Abs(DeferredRealityProviderRules.ApplyPopulationVariation(5f, 1.005f, 8f) - 5.025f) < 0.00001f &&
            Math.Abs(DeferredRealityProviderRules.ApplyPopulationVariation(5f, 0.995f, 8f) - 4.975f) < 0.00001f);
        Check("finite carrying capacity still caps demographic variation",
            DeferredRealityProviderRules.ApplyPopulationVariation(5f, 1.005f, 4f) == 4f &&
            DeferredRealityProviderRules.ApplyPopulationVariation(float.MaxValue, 1f,
                float.MaxValue) == float.MaxValue);
        Check("high finite projected population cannot collapse from 0.5 percent growth",
            DeferredRealityProviderRules.ApplyPopulationVariation(
                float.MaxValue, 1.005f, float.MaxValue) == float.MaxValue);
        Check("high finite projected population clamps to a lower valid capacity",
            DeferredRealityProviderRules.ApplyPopulationVariation(
                float.MaxValue, 1.005f, halfMaximum) == halfMaximum);
        Check("other finite variation overflow still caps without resetting the population",
            DeferredRealityProviderRules.ApplyPopulationVariation(
                halfMaximum, 3f, float.MaxValue) == float.MaxValue);
        Check("ordinary zero or negative demographic variation stays nonnegative",
            DeferredRealityProviderRules.ApplyPopulationVariation(5f, 0f, 8f) == 0f &&
            DeferredRealityProviderRules.ApplyPopulationVariation(5f, -1f, 8f) == 0f &&
            DeferredRealityProviderRules.ApplyPopulationVariation(0f, 1.005f, 8f) == 0f);
        Check("invalid random variation or projected population still fails closed",
            DeferredRealityProviderRules.ApplyPopulationVariation(5f, float.NaN, 8f) == 0f &&
            DeferredRealityProviderRules.ApplyPopulationVariation(5f, float.PositiveInfinity, 8f) == 0f &&
            DeferredRealityProviderRules.ApplyPopulationVariation(float.NaN, 1.005f, 8f) == 0f &&
            DeferredRealityProviderRules.ApplyPopulationVariation(float.NegativeInfinity, 1.005f, 8f) == 0f &&
            DeferredRealityProviderRules.ApplyPopulationVariation(5f, 1.005f, 0f) == 0f);
        Check("deferred record bound is enforced", DeferredRealityProviderRules.IsWithinRecordLimit(
            DeferredRealityProviderRules.MaximumDynamicRecords) && !DeferredRealityProviderRules.IsWithinRecordLimit(
                DeferredRealityProviderRules.MaximumDynamicRecords + 1));
        var applied = new HashSet<string>(StringComparer.Ordinal);
        int mutations = 0;
        Check("first deferred operation applies", DeferredRealityProviderRules.ApplyExactlyOnce(applied, "op:1", () =>
        {
            mutations++;
            return true;
        }));
        Check("duplicate deferred operation is a no-op", DeferredRealityProviderRules.ApplyExactlyOnce(applied, "op:1", () =>
        {
            mutations++;
            return true;
        }) && mutations == 1);
        Check("failed deferred operation can retry", !DeferredRealityProviderRules.ApplyExactlyOnce(applied, "op:2", () => false) &&
            DeferredRealityProviderRules.ApplyExactlyOnce(applied, "op:2", () => true));
    }

    private static AquacultureCommissionEligibilityInput EligibleInput()
    {
        return new AquacultureCommissionEligibilityInput
        {
            alive = true,
            spawned = true,
            correctSpecies = true,
            correctBreed = true,
            adult = true,
            sterile = false,
            foodReserve = 0.35f,
            starvationProgress = 0f,
            waterStress = 0.099f,
            temperatureStress = 0.099f,
            habitatStress = 0.749f,
            breedStability = 0.60f,
            breedGeneration = 2,
            sizeFactor = 1.006f,
            hasRequiredTrait = true
        };
    }

    private static AquacultureCommissionGoal Goal(AquacultureCommissionImprovement improvement,
        float stability = 0.60f, int generation = 2, float size = 1f)
    {
        return new AquacultureCommissionGoal
        {
            breedId = "breed",
            fishDefName = "fish",
            traitDefName = "trait",
            improvement = improvement,
            minimumStability = stability,
            minimumGeneration = generation,
            minimumSizeFactor = size
        };
    }

    private static void TestCommissionRules()
    {
        AquacultureCommissionEligibilityInput input = EligibleInput();
        Check("commission eligibility baseline", AquacultureCommissionGoalRules.IsEligible(input));
        Check("commission goal baseline", AquacultureCommissionGoalRules.MeetsGoal(input,
            Goal(AquacultureCommissionImprovement.Generation)));

        input = EligibleInput(); input.foodReserve = 0.349f;
        Check("food eligibility boundary", !AquacultureCommissionGoalRules.IsEligible(input));
        input = EligibleInput(); input.starvationProgress = 0.001f;
        Check("starvation eligibility boundary", !AquacultureCommissionGoalRules.IsEligible(input));
        input = EligibleInput(); input.waterStress = 0.10f;
        Check("water stress eligibility boundary", !AquacultureCommissionGoalRules.IsEligible(input));
        input = EligibleInput(); input.temperatureStress = 0.10f;
        Check("temperature eligibility boundary", !AquacultureCommissionGoalRules.IsEligible(input));
        input = EligibleInput(); input.habitatStress = 0.75f;
        Check("habitat eligibility boundary", !AquacultureCommissionGoalRules.IsEligible(input));
        input = EligibleInput(); input.hasRequiredTrait = false;
        Check("required trait eligibility", !AquacultureCommissionGoalRules.IsEligible(input));
        input = EligibleInput(); input.breedStability = 0.599f;
        Check("minimum stability predicate", !AquacultureCommissionGoalRules.MeetsGoal(input,
            Goal(AquacultureCommissionImprovement.Stability, 0.60f)));
        input = EligibleInput(); input.sizeFactor = 0.999f;
        Check("minimum size predicate", !AquacultureCommissionGoalRules.MeetsGoal(input,
            Goal(AquacultureCommissionImprovement.Size, size: 1f)));

        Check("size threshold floors", AquacultureCommissionGoalRules.NormalizeSizeThreshold(1.006f) == 1f);
        Check("floored threshold accepts source precision", AquacultureCommissionGoalRules.MeetsSize(1.006f, 1.006f));
        Check("floored threshold rejects below source precision", !AquacultureCommissionGoalRules.MeetsSize(0.999f, 1.006f));

        AquacultureCommissionCapability capability = new AquacultureCommissionCapability
        {
            breedId = "breed",
            fishDefName = "fish",
            definingTraitName = "trait",
            currentGeneration = 1,
            currentStability = 0.60f,
            currentSizeFactor = 1.006f,
            maximumReasonableSizeFactor = 1.106f,
            generationsReachableBeforeDeadline = 1,
            minimumBreedingWindowDays = 1,
            hasBreedingPair = true,
            canImproveStability = true,
            canImproveSize = true
        };
        List<AquacultureCommissionGoal> goals = AquacultureCommissionGoalRules.BuildGoals(capability);
        Check("generation goal is an improvement", goals.Count > 0
            && goals[0].improvement == AquacultureCommissionImprovement.Generation
            && goals[0].minimumGeneration == 2);
        AquacultureCommissionEligibilityInput generationInput = EligibleInput();
        generationInput.breedGeneration = 1;
        Check("generation goal is not already satisfied", !AquacultureCommissionGoalRules.MeetsGoal(
            generationInput, goals[0]));
        AquacultureCommissionGoal unsatisfied = AquacultureCommissionGoalRules.SelectGoal(capability,
            goal => AquacultureCommissionGoalRules.MeetsGoal(EligibleInput(), goal), null, goal => 18);
        Check("already satisfied living fish is excluded", unsatisfied != null
            && unsatisfied.improvement != AquacultureCommissionImprovement.Generation);
        AquacultureCommissionGoal repeated = AquacultureCommissionGoalRules.SelectGoal(capability, null,
            goal => goal.improvement == AquacultureCommissionImprovement.Generation, goal => 18);
        Check("duplicate goal suppression", repeated != null
            && repeated.improvement != AquacultureCommissionImprovement.Generation);

        AquacultureCommissionCapability noPair = new AquacultureCommissionCapability
        {
            breedId = "breed", fishDefName = "fish", definingTraitName = "trait"
        };
        Check("no breeding capability skips goals", AquacultureCommissionGoalRules.BuildGoals(noPair).Count == 0);
        Check("unreachable generation skips goal", !AquacultureCommissionGoalRules.IsAchievable(capability,
            goals[0], 0));
        capability.minimumBreedingWindowDays = 2;
        Check("deadline shorter than breeding window skips goal", !AquacultureCommissionGoalRules.IsAchievable(
            capability, goals[0], 1));
        capability.minimumBreedingWindowDays = 1;
        capability.maximumReasonableSizeFactor = capability.currentSizeFactor;
        capability.canImproveSize = true;
        Check("unreachable size skips goal", !AquacultureCommissionGoalRules.BuildGoals(capability)
            .Any(goal => goal.improvement == AquacultureCommissionImprovement.Size));

        int lowReward = AquacultureCommissionGoalRules.RewardSilver(0, 0f, 0.75f, 0f, 0.90f, 18);
        int highReward = AquacultureCommissionGoalRules.RewardSilver(99, 0.98f, 2f, 2f, 1.35f, 1);
        Check("reward lower bound", lowReward >= AquacultureCommissionGoalRules.MinimumReward);
        Check("reward upper bound", highReward <= AquacultureCommissionGoalRules.MaximumReward);
        Check("reward factors are bounded", highReward >= lowReward);
        Check("invalid generation cannot earn a reward multiplier",
            AquacultureCommissionGoalRules.GenerationFactor(int.MinValue) == 1f &&
            AquacultureCommissionGoalRules.GenerationFactor(-8) == 1f &&
            AquacultureCommissionGoalRules.GenerationFactor(0) == 1f &&
            AquacultureCommissionGoalRules.GenerationFactor(1) == 1f);
        Check("valid generation bonus retains normal and upper limits",
            Math.Abs(AquacultureCommissionGoalRules.GenerationFactor(2) - 1.1f) < 0.0001f &&
            Math.Abs(AquacultureCommissionGoalRules.GenerationFactor(int.MaxValue) - 1.8f) < 0.0001f);
        Check("invalid generation cannot shorten the commission deadline",
            AquacultureCommissionGoalRules.DeadlineDays(1f, int.MinValue) ==
                AquacultureCommissionGoalRules.DeadlineDays(1f, 1) &&
            AquacultureCommissionGoalRules.DeadlineDays(1f, -7) ==
                AquacultureCommissionGoalRules.DeadlineDays(1f, 0));
        Check("invalid generation does not inflate commission silver",
            AquacultureCommissionGoalRules.RewardSilver(int.MinValue, 0.7f, 1f, 1f, 1f, 14) ==
                AquacultureCommissionGoalRules.RewardSilver(1, 0.7f, 1f, 1f, 1f, 14));
        AquacultureCommissionCapability maxGenerationCapability = new AquacultureCommissionCapability
        {
            breedId = "breed",
            fishDefName = "fish",
            definingTraitName = "trait",
            currentGeneration = int.MaxValue,
            generationsReachableBeforeDeadline = 3,
            currentStability = 0.60f,
            currentSizeFactor = 1f,
            maximumReasonableSizeFactor = 2f,
            hasBreedingPair = true
        };
        Check("maximum generation cannot wrap into an impossible goal",
            !AquacultureCommissionGoalRules.BuildGoals(maxGenerationCapability)
                .Any(goal => goal.improvement == AquacultureCommissionImprovement.Generation));
        maxGenerationCapability.currentGeneration = int.MaxValue - 1;
        Check("near-maximum reachable generation avoids integer overflow",
            AquacultureCommissionGoalRules.IsAchievable(maxGenerationCapability,
                Goal(AquacultureCommissionImprovement.Generation, stability: 0.60f,
                    generation: int.MaxValue, size: 1f), 14));

        AquacultureCommissionFishIndex<string> index = new AquacultureCommissionFishIndex<string>();
        index.Register("a", "breed");
        Check("index registers fish", index.Count == 1 && index.ForBreed("breed").Contains("a"));
        index.Register("a", "other");
        Check("index updates breed assignment", !index.ForBreed("breed").Contains("a")
            && index.ForBreed("other").Contains("a"));
        index.Register("b", "other");
        index.Unregister("a");
        Check("index invalidates despawn", index.Count == 1 && !index.ForBreed("other").Contains("a"));
        index.Clear();
        Check("index invalidates load", index.Count == 0 && index.ForBreed("other").Count == 0);

        var isolatedIndex = new AquacultureCommissionFishIndex<string>();
        isolatedIndex.Register("first", "breed");
        isolatedIndex.Register("second", "breed");
        IReadOnlyCollection<string> exposed = isolatedIndex.ForBreed("breed");
        bool clearedExternally = false;
        if (exposed is ICollection<string> externallyMutable)
        {
            try
            {
                externallyMutable.Clear();
                clearedExternally = true;
            }
            catch (NotSupportedException) { }
        }
        Check("breed query cannot clear the live index through a collection cast",
            !clearedExternally && isolatedIndex.Count == 2 &&
            isolatedIndex.ForBreed("breed").Count == 2 &&
            isolatedIndex.ForBreed("breed").Contains("first"));
        bool addedExternally = false;
        if (isolatedIndex.ForBreed("breed") is ICollection<string> addable)
        {
            try
            {
                addable.Add("injected");
                addedExternally = true;
            }
            catch (NotSupportedException) { }
        }
        Check("breed query cannot inject unregistered specimens",
            !addedExternally && isolatedIndex.Count == 2 &&
            !isolatedIndex.ForBreed("breed").Contains("injected"));

        var snapshotIndex = new AquacultureCommissionFishIndex<string>();
        snapshotIndex.Register("early", "breed");
        IReadOnlyCollection<string> earlier = snapshotIndex.ForBreed("breed");
        snapshotIndex.Register("later", "breed");
        snapshotIndex.Register("early", "other");
        Check("breed query is a detached snapshot across additions and reassignment",
            earlier.Count == 1 && earlier.Contains("early") && !earlier.Contains("later") &&
            snapshotIndex.Count == 2 && snapshotIndex.ForBreed("breed").SequenceEqual(new[] { "later" }) &&
            snapshotIndex.ForBreed("other").Contains("early"));
        snapshotIndex.Unregister("later");
        snapshotIndex.Clear();
        Check("captured breed snapshots survive index removals and reset",
            earlier.Count == 1 && earlier.Contains("early") && snapshotIndex.Count == 0 &&
            snapshotIndex.ForBreed("breed").Count == 0);

        Check("delivery requires every step", !AquacultureCommissionDeliveryRules.Completes(true, true, true, false));
        Check("delivery consumes only valid specimen", AquacultureCommissionDeliveryRules.Completes(true, true, true, true));
        Check("invalid delivery cannot complete", !AquacultureCommissionDeliveryRules.Completes(true, false, true, true));

        AquacultureCommissionSavedState legacy = new AquacultureCommissionSavedState
        {
            breedId = "breed", fishDefName = "fish", traitDefName = "trait", minimumStability = 0.71f,
            minimumGeneration = 4, minimumSizeFactor = 1.006f, offeredTick = 100, deadlineTick = 1000,
            rewardSilver = 800
        };
        Check("legacy active commission loads", AquacultureCommissionGoalRules.TryNormalizeSavedState(legacy, true));
        Check("legacy active fields preserved", legacy.minimumGeneration == 4 && legacy.rewardSilver == 800
            && legacy.minimumSizeFactor == 1f);
        AquacultureCommissionSavedState invalidLegacy = new AquacultureCommissionSavedState
        {
            breedId = "breed", fishDefName = "fish", traitDefName = "trait", deadlineTick = -1, rewardSilver = 800
        };
        Check("invalid legacy commission rejected", !AquacultureCommissionGoalRules.TryNormalizeSavedState(invalidLegacy, true));
        Check("missing references rejected", !AquacultureCommissionGoalRules.TryNormalizeSavedState(legacy, false));

        Check("ordinary commission tick offsets retain their exact schedule",
            AquacultureCommissionGoalRules.ScheduleTick(100, 6000L) == 6100 &&
            AquacultureCommissionGoalRules.ScheduleTick(20, 18L * 60000L) == 1080020);
        Check("commission due tick saturates rather than wrapping near the int ceiling",
            AquacultureCommissionGoalRules.ScheduleTick(int.MaxValue - 10, 6000L) == int.MaxValue &&
            AquacultureCommissionGoalRules.ScheduleTick(int.MaxValue - 1, 30L * 60000L) == int.MaxValue);
        Check("long commission delays cannot wrap into earlier ticks",
            AquacultureCommissionGoalRules.ScheduleTick(0, long.MaxValue) == int.MaxValue);
        Check("invalid negative delay cannot schedule commission events in the past",
            AquacultureCommissionGoalRules.ScheduleTick(100, -500L) == 100);
        Check("negative-base tick addition remains exact within int range",
            AquacultureCommissionGoalRules.ScheduleTick(int.MinValue, 1L) == int.MinValue + 1);
        Check("normal commission days remaining round up at day boundaries",
            AquacultureCommissionGoalRules.RemainingDays(60000, 0) == 1 &&
            AquacultureCommissionGoalRules.RemainingDays(60001, 0) == 2 &&
            AquacultureCommissionGoalRules.RemainingDays(60000, 60000) == 0 &&
            AquacultureCommissionGoalRules.RemainingDays(59999, 60000) == 0 &&
            AquacultureCommissionGoalRules.RemainingDays(59999, 0) == 1);
        Check("negative deadline sentinel still means no active commission",
            AquacultureCommissionGoalRules.RemainingDays(-1, int.MinValue) == 0);
        Check("commission remaining days use wide arithmetic across opposite int limits",
            AquacultureCommissionGoalRules.RemainingDays(int.MaxValue, int.MinValue) == 71583 &&
            AquacultureCommissionGoalRules.RemainingDays(0, int.MinValue) == 35792);
    }

    private static void TestExactlyOnceLedgerOwnership()
    {
        var clearLedger = new FrameworkIntegrationRules.ExactlyOnceLedger();
        int effects = 0;
        clearLedger.Apply("first", () => { effects++; return true; });
        clearLedger.Apply("second", () => { effects++; return true; });
        IReadOnlyCollection<string> exposed = clearLedger.Committed;
        bool clearedExternally = false;
        if (exposed is ICollection<string> clearable)
        {
            try { clearable.Clear(); clearedExternally = true; }
            catch (NotSupportedException) { }
        }
        Check("committed operation query cannot erase the live exactly-once ledger",
            !clearedExternally && clearLedger.Count == 2 &&
            clearLedger.Apply("first", () => { effects++; return true; }) && effects == 2);

        var injectLedger = new FrameworkIntegrationRules.ExactlyOnceLedger();
        injectLedger.Apply("approved", () => true);
        bool addedExternally = false;
        bool removedExternally = false;
        if (injectLedger.Committed is ICollection<string> writable)
        {
            try { writable.Add("injected"); addedExternally = true; }
            catch (NotSupportedException) { }
            try { removedExternally = writable.Remove("approved"); }
            catch (NotSupportedException) { }
        }
        Check("committed operation query cannot inject or remove uncommitted ids",
            !addedExternally && !removedExternally && injectLedger.Count == 1 &&
            injectLedger.Committed.Contains("approved") && !injectLedger.Committed.Contains("injected"));

        var snapshotLedger = new FrameworkIntegrationRules.ExactlyOnceLedger();
        snapshotLedger.Apply("early", () => true);
        IReadOnlyCollection<string> earlier = snapshotLedger.Committed;
        snapshotLedger.Apply("later", () => true);
        Check("captured commit snapshot is detached from later successful operations",
            earlier.Count == 1 && earlier.Contains("early") && !earlier.Contains("later") &&
            snapshotLedger.Count == 2);
        snapshotLedger.Restore(null);
        Check("captured commit snapshot survives reset while ledger clears",
            earlier.Count == 1 && earlier.Contains("early") && snapshotLedger.Count == 0);

        var selfRestore = new FrameworkIntegrationRules.ExactlyOnceLedger();
        selfRestore.Apply("event-a", () => true);
        selfRestore.Apply("event-b", () => true);
        selfRestore.Restore(selfRestore.Committed);
        int replayed = 0;
        Check("restoring a ledger from its own committed ids preserves markers",
            selfRestore.Count == 2 && selfRestore.Committed.Contains("event-a") &&
            selfRestore.Committed.Contains("event-b") &&
            selfRestore.Apply("event-a", () => { replayed++; return true; }) && replayed == 0);

        var lazyRestore = new FrameworkIntegrationRules.ExactlyOnceLedger();
        lazyRestore.Apply("keep:a", () => true);
        lazyRestore.Apply("keep:b", () => true);
        lazyRestore.Apply("drop:c", () => true);
        IEnumerable<string> deferredIds = lazyRestore.Committed.Where(id => id.StartsWith("keep:", StringComparison.Ordinal));
        lazyRestore.Restore(deferredIds);
        Check("restoring from a lazy projection of own markers preserves filtered ids",
            lazyRestore.Count == 2 && lazyRestore.Committed.Contains("keep:a") &&
            lazyRestore.Committed.Contains("keep:b") && !lazyRestore.Committed.Contains("drop:c"));

        var deduplicated = new FrameworkIntegrationRules.ExactlyOnceLedger()
            .Restore(new string[] { "A", null, "", "A", "B" });
        int retryEffects = 0;
        Check("restore retains ordinal deduplication and skips invalid operation ids",
            deduplicated.Count == 2 &&
            deduplicated.Apply("A", () => { retryEffects++; return true; }) && retryEffects == 0 &&
            deduplicated.Apply("a", () => { retryEffects++; return true; }) && retryEffects == 1);
    }

    private static void TestFrameworkIntegrationRules()
    {
        string first = FrameworkIntegrationRules.CorrelationId("catch", "pawn:1", 40, "map:a", "10,10", "Fish_Cod");
        string duplicate = FrameworkIntegrationRules.CorrelationId("catch", "pawn:1", 40, "map:a", "10,10", "Fish_Cod");
        string secondPawn = FrameworkIntegrationRules.CorrelationId("catch", "pawn:2", 40, "map:a", "10,10", "Fish_Cod");
        string secondMap = FrameworkIntegrationRules.CorrelationId("catch", "pawn:1", 40, "map:b", "10,10", "Fish_Cod");
        Check("correlation retry is stable", first == duplicate);
        Check("simultaneous pawns remain distinct", first != secondPawn);
        Check("multiple maps remain distinct", first != secondMap);

        var state = new FrameworkIntegrationRules.IntegrationState { population = 10f, activeMap = true };
        Check("consume succeeds once", FrameworkIntegrationRules.ApplyPopulationMutation(state, "consume:1", -1f,
            true) == false);
        // The pure mutation helper models positive deltas; use a dedicated ledger for a consume marker.
        Check("population operation commits once", state.populationOperations.Apply("consume:1", () =>
        {
            state.population -= 1f;
            return true;
        }));
        Check("population duplicate is a no-op", state.populationOperations.Apply("consume:1", () =>
        {
            state.population -= 1f;
            return true;
        }) && state.population == 9f);
        Check("release duplicate is a no-op", state.populationOperations.Apply("release:1", () =>
        {
            state.population += 1f;
            return true;
        }) && state.populationOperations.Apply("release:1", () =>
        {
            state.population += 1f;
            return true;
        }) && state.population == 10f);

        Check("knowledge failure does not duplicate population", FrameworkIntegrationRules.ApplyKnowledgeAfterPopulation(
            state, first, false) == false && state.population == 10f);
        Check("knowledge retry commits once", FrameworkIntegrationRules.ApplyKnowledgeAfterPopulation(state, first, true)
            && FrameworkIntegrationRules.ApplyKnowledgeAfterPopulation(state, first, true)
            && state.knowledgeGroups == 1);
        Check("expertise duplicate is harmless", FrameworkIntegrationRules.ApplyExpertise(state, first, true)
            && FrameworkIntegrationRules.ApplyExpertise(state, first, true) && state.expertiseAwards == 1);
        Check("journal duplicate is harmless", FrameworkIntegrationRules.ApplyJournal(state, first, true)
            && FrameworkIntegrationRules.ApplyJournal(state, first, true) && state.journalEntries == 1);
        Check("failed population operation remains retryable", !state.populationOperations.Apply("failed:1", () => false)
            && state.populationOperations.Apply("failed:1", () => true));
        Check("failed DRF operation has no knowledge group", !FrameworkIntegrationRules.ApplyKnowledgeAfterPopulation(
            state, "failed:1", false) && state.knowledgeGroups == 1);
        Check("only one simulation advances", FrameworkIntegrationRules.AdvanceExactlyOnce(state, "process:1", false,
            true) && state.activeAdvances == 0 && state.latentAdvances == 1
            && !FrameworkIntegrationRules.AdvanceExactlyOnce(state, "process:2", true, true));
        Check("duplicate latent operation never counts a second advance",
            FrameworkIntegrationRules.AdvanceExactlyOnce(state, "process:1", false, true) &&
            state.activeAdvances == 0 && state.latentAdvances == 1);
        Check("replayed operation cannot switch from latent to active",
            FrameworkIntegrationRules.AdvanceExactlyOnce(state, "process:1", true, false) &&
            state.activeAdvances == 0 && state.latentAdvances == 1);
        Check("empty operation identity cannot advance either simulation",
            !FrameworkIntegrationRules.AdvanceExactlyOnce(state, "", true, false) &&
            state.activeAdvances == 0 && state.latentAdvances == 1);
        Check("failed operation does not advance or commit",
            !FrameworkIntegrationRules.AdvanceExactlyOnce(state, "process:retry", true, false, succeeds: false) &&
            state.activeAdvances == 0 && state.latentAdvances == 1);
        Check("invalid active/latent combination never counts",
            !FrameworkIntegrationRules.AdvanceExactlyOnce(state, "process:invalid", false, false) &&
            !FrameworkIntegrationRules.AdvanceExactlyOnce(state, "process:invalid", true, true) &&
            state.activeAdvances == 0 && state.latentAdvances == 1);
        Check("distinct active operation advances once",
            FrameworkIntegrationRules.AdvanceExactlyOnce(state, "process:retry", true, false) &&
            FrameworkIntegrationRules.AdvanceExactlyOnce(state, "process:retry", true, false) &&
            state.activeAdvances == 1 && state.latentAdvances == 1);

        FrameworkIntegrationRules.IntegrationState restored = state.Clone();
        Check("save/load keeps both simulation advances idempotent",
            FrameworkIntegrationRules.AdvanceExactlyOnce(restored, "process:1", false, true) &&
            FrameworkIntegrationRules.AdvanceExactlyOnce(restored, "process:retry", true, false) &&
            restored.activeAdvances == state.activeAdvances && restored.latentAdvances == state.latentAdvances);
        Check("save/load preserves completed operation", restored.populationOperations.Apply("consume:1", () =>
        {
            restored.population -= 1f;
            return true;
        }) && restored.population == state.population);
        FrameworkIntegrationRules.RemoveMapPreservingLatent(restored);
        Check("map removal clears active ownership only", !restored.activeMap && restored.population == state.population);
        restored.providerAvailable = false;
        Check("provider absence does not mutate", !FrameworkIntegrationRules.ApplyPopulationMutation(restored, "consume:2",
            -1f, false) && restored.population == state.population);
        restored.providerAvailable = true;
        Check("restored provider can retry", FrameworkIntegrationRules.ApplyPopulationMutation(restored, "stock:1", 1f,
            true) && restored.population == state.population + 1f);
        Check("invalid population values are rejected", !FrameworkIntegrationRules.IsFiniteNonNegative(float.NaN)
            && !FrameworkIntegrationRules.IsFiniteNonNegative(float.PositiveInfinity)
            && !FrameworkIntegrationRules.IsFiniteNonNegative(-1f));
    }

    public static int Main()
    {
        TestProbabilityBoundaries();
        TestParentalUnionAndCap();
        TestMutationsAndOutliers();
        TestStabilityAndMigration();
        TestFishAgeTiming();
        TestPondEcologyTiming();
        TestPondEffectTiming();
        TestFishSchoolTiming();
        TestRecoveryRules();
        TestCommissionRules();
        TestKnowledgeContract();
        TestDeferredRealityProviderRules();
        TestExactlyOnceLedgerOwnership();
        TestFrameworkIntegrationRules();
        Console.WriteLine(failures == 0 ? "TraitBreedingRules: all executable tests passed." : "TraitBreedingRules: " + failures + " failure(s).");
        return failures == 0 ? 0 : 1;
    }
}
