using InsightCanvas;

namespace AquacultureFishing
{
    /// <summary>
    /// Applies the Aquaculture-owned presentation contract to one document. Every player-facing
    /// Insight Canvas document calls this once after construction; settings changes call it again
    /// for the live settings document.
    /// </summary>
    public static class AquacultureInsightPresentation
    {
        public static AquaculturePresentationPreferences Current(AquacultureSettings authority = null)
        {
            AquacultureSettings settings = authority ?? AquacultureMod.Settings;
            return settings == null ? AquaculturePresentationPreferences.Default : settings.PresentationPreferences;
        }

        public static void Apply(InsightUiDocument document, AquacultureSettings authority = null)
        {
            if (document == null) return;
            AquaculturePresentationPreferences preferences = Current(authority);
            document.Theme = AquacultureInsightTheme.Create(preferences.HighContrast);
            document.Density = ToInsightDensity(preferences.DensityIndex);
            document.HighContrast = preferences.HighContrast;
            document.ReducedMotion = preferences.ReducedMotion;
            document.Invalidate();
        }

        public static InsightUiDensity ToInsightDensity(int densityIndex)
        {
            switch (AquaculturePresentationPreferences.ClampDensityIndex(densityIndex))
            {
                case (int)AquaculturePresentationDensity.Comfortable: return InsightUiDensity.Comfortable;
                case (int)AquaculturePresentationDensity.Compact: return InsightUiDensity.Compact;
                default: return InsightUiDensity.Normal;
            }
        }
    }
}
