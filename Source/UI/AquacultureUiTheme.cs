using InsightCanvas;

namespace AquacultureFishing
{
    /// <summary>Creates the settings document's local aquatic theme without touching the global GUI skin or RimWorld state.</summary>
    public static class AquacultureInsightTheme
    {
        public static InsightTheme Create()
        {
            InsightTheme theme = InsightTheme.Default.Clone();
            theme.Id = "aquaculture-aquatic-v2";
            theme.Background = new InsightColor(0.035f, 0.065f, 0.075f);
            theme.Surface = new InsightColor(0.075f, 0.125f, 0.14f);
            theme.ElevatedSurface = new InsightColor(0.11f, 0.18f, 0.19f);
            theme.PrimaryText = new InsightColor(0.91f, 0.95f, 0.93f);
            theme.SecondaryText = new InsightColor(0.63f, 0.75f, 0.74f);
            theme.Selected = new InsightColor(0.22f, 0.58f, 0.65f);
            theme.Hover = new InsightColor(0.16f, 0.35f, 0.39f);
            theme.Focus = new InsightColor(0.9f, 0.68f, 0.27f);
            theme.Positive = new InsightColor(0.34f, 0.74f, 0.47f);
            theme.Warning = new InsightColor(0.91f, 0.67f, 0.25f);
            theme.Negative = new InsightColor(0.86f, 0.39f, 0.34f);
            theme.Unknown = new InsightColor(0.48f, 0.58f, 0.59f);
            theme.Locked = new InsightColor(0.31f, 0.4f, 0.41f);
            theme.Shadow = new InsightColor(0f, 0f, 0f, 0.3f);
            theme.CornerRadius = 4f;
            theme.Spacing = 8f;
            theme.SetRelationColor("green", theme.Positive);
            theme.SetRelationColor("amber", theme.Warning);
            theme.SetRelationColor("coral", theme.Negative);
            return theme;
        }
    }
}
