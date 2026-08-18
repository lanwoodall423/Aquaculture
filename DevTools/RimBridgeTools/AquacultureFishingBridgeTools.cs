using System;
using System.Threading;
using System.Threading.Tasks;
using AquacultureFishing;
using RimBridgeServer.Sdk;

namespace AquacultureFishing.BridgeTools
{
    public static class AquacultureFishingBridgeTools
    {
        [Tool(
            "aquaculture/test_status",
            Title = "Aquaculture test surface",
            Description = "Describe the loaded Aquaculture RimBridge companion and its developer test surface.",
            ResultDescription = "Returns the loaded companion and test-suite identity.",
            Tags = new[] { "aquaculture", "testing", "read-only" },
            RequiresAuth = true)]
        [ToolResponse("success", "boolean", "Whether the companion is loaded", Always = true)]
        [ToolResponse("suite", "string", "The available test suite name", Always = true)]
        public static object TestStatus(IRimBridgeContext context)
        {
            return new
            {
                success = true,
                suite = "aquaculture-mod-owned-live-world",
                companionAssembly = typeof(AquacultureFishingBridgeTools).Assembly.GetName().Name,
                modAssembly = typeof(AquacultureBridgeTestSuite).Assembly.GetName().Name,
                operationId = context?.OperationId,
                capabilityId = context?.CapabilityId
            };
        }

        [Tool(
            "aquaculture/run_baseline",
            Title = "Run Aquaculture baseline tests",
            Description = "Run the mod-owned live-world baseline assertions against the current playable map.",
            ResultDescription = "Returns success, check counts, and individual assertion results.",
            Tags = new[] { "aquaculture", "testing", "baseline" },
            RequiresAuth = true)]
        [ToolResponse("success", "boolean", "Whether every baseline assertion passed", Always = true)]
        [ToolResponse("checks", "array", "Baseline assertion results", Always = true)]
        public static Task<object> RunBaseline(
            IRimBridgeContext context,
            CancellationToken cancellationToken,
            [ToolParameter(Description = "Stable caller-provided run identifier. A new identifier is generated when omitted.")] string runId = null,
            [ToolParameter(Description = "Paused deterministic ticks to advance before assertions.", DefaultValue = 0)] int settleTicks = 0)
        {
            return RunSuite(context, cancellationToken, runId, settleTicks, false);
        }

        [Tool(
            "aquaculture/run_golden_path",
            Title = "Run Aquaculture golden path",
            Description = "Create, exercise, and clean up the inhabited-pond breeding and processing fixture on the current map.",
            ResultDescription = "Returns success, fixture check counts, and cleanup evidence.",
            Tags = new[] { "aquaculture", "testing", "golden-path", "mutation" },
            RequiresAuth = true)]
        [ToolResponse("success", "boolean", "Whether every golden-path assertion passed", Always = true)]
        [ToolResponse("checks", "array", "Golden-path assertion results", Always = true)]
        public static Task<object> RunGoldenPath(
            IRimBridgeContext context,
            CancellationToken cancellationToken,
            [ToolParameter(Description = "Stable caller-provided run identifier. A new identifier is generated when omitted.")] string runId = null,
            [ToolParameter(Description = "Paused deterministic ticks to advance before fixture setup.", DefaultValue = 0)] int settleTicks = 0)
        {
            return RunSuite(context, cancellationToken, runId, settleTicks, true);
        }

        private static async Task<object> RunSuite(
            IRimBridgeContext context,
            CancellationToken cancellationToken,
            string runId,
            int settleTicks,
            bool goldenPath)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (settleTicks < 0) throw new ArgumentOutOfRangeException(nameof(settleTicks));
            if (string.IsNullOrWhiteSpace(runId)) runId = Guid.NewGuid().ToString("N");

            if (settleTicks > 0)
                await context.Game.StepTicksAsync(settleTicks, null, cancellationToken);

            AquacultureInGameTestReport report = await context.MainThread.InvokeAsync(
                () => goldenPath
                    ? AquacultureBridgeTestSuite.RunGoldenPath(runId)
                    : AquacultureBridgeTestSuite.RunBaseline(runId),
                cancellationToken);

            return new
            {
                success = report.Passed,
                suite = report.suite,
                runId = report.runId,
                launchId = report.launchId,
                generation = report.generation,
                gameTick = report.gameTick,
                startedUtc = report.startedUtc,
                completedUtc = report.completedUtc,
                passed = report.results.FindAll(result => result.status == "PASS").Count,
                failed = report.results.FindAll(result => result.status == "FAIL").Count,
                checks = report.results
            };
        }
    }
}
