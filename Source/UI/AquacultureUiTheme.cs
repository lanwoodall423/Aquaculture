using InsightCanvas;

namespace AquacultureFishing
{
    /// <summary>Creates the document-local aquatic theme without touching global GUI skin or RimWorld state.</summary>
    public static class AquacultureInsightTheme
    {
        public static InsightTheme Create(bool highContrast = false)
        {
            InsightTheme theme = InsightTheme.Default.Clone();
            theme.Id = highContrast ? "aquaculture-aquatic-v2-high-contrast" : "aquaculture-aquatic-v2";
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
            theme.TitleSize = 1.18f;
            theme.BodySize = 1f;
            theme.CaptionSize = 0.86f;

            // WithAccessibility also strengthens the base surface/text tokens. These additional
            // local tokens make selection, semantic states, and keyboard focus visibly distinct.
            if (highContrast)
            {
                theme.Background = new InsightColor(0.015f, 0.035f, 0.04f);
                theme.Surface = new InsightColor(0.12f, 0.19f, 0.2f);
                theme.ElevatedSurface = new InsightColor(0.2f, 0.29f, 0.3f);
                theme.PrimaryText = new InsightColor(1f, 1f, 0.98f);
                theme.SecondaryText = new InsightColor(0.86f, 0.94f, 0.92f);
                theme.Selected = new InsightColor(0.16f, 0.78f, 0.9f);
                theme.Hover = new InsightColor(0.24f, 0.56f, 0.62f);
                theme.Focus = new InsightColor(1f, 0.9f, 0.2f);
                theme.Positive = new InsightColor(0.45f, 0.92f, 0.58f);
                theme.Warning = new InsightColor(1f, 0.8f, 0.18f);
                theme.Negative = new InsightColor(1f, 0.48f, 0.42f);
                theme.Unknown = new InsightColor(0.7f, 0.8f, 0.8f);
                theme.Locked = new InsightColor(0.42f, 0.52f, 0.54f);
                theme.Shadow = new InsightColor(0f, 0f, 0f, 0.55f);
                theme.CornerRadius = 3f;
            }
            theme.SetRelationColor("green", theme.Positive);
            theme.SetRelationColor("amber", theme.Warning);
            theme.SetRelationColor("coral", theme.Negative);
            return theme;
        }
    }
}
