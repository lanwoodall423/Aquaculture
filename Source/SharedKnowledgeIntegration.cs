namespace AquacultureFishing
{
    /// <summary>Compatibility entry point retained for older startup code; V3 owns the integration.</summary>
    public static class AquacultureSharedKnowledgeIntegration
    {
        public static void Register() => AquacultureKnowledgeAdapter.EnsureRegistration();
    }
}
