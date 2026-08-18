using Verse;

namespace AquacultureFishing
{
    /// <summary>
    /// Retries Knowledge Framework registration after game components are constructed.
    /// The startup callback can run before the framework's GameComponent exists.
    /// </summary>
    public sealed class AquacultureKnowledgeRegistrationComponent : GameComponent
    {
        private int nextAttemptTick;

        public AquacultureKnowledgeRegistrationComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref nextAttemptTick, "nextKnowledgeRegistrationTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) nextAttemptTick = 0;
        }

        public override void GameComponentTick()
        {
            if (Current.Game == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextAttemptTick) return;
            nextAttemptTick = now + 60;
            AquacultureKnowledgeAdapter.EnsureRegistration();
        }
    }
}
