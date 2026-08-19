using System;
using InsightCanvas;

namespace AquacultureFishing
{
    /// <summary>Stable-ID-scoped compositions shared by all Aquaculture Insight Canvas screens.</summary>
    public static class AquacultureUiComponents
    {
        public static InsightUiElement Callout(string id, InsightUiCalloutSeverity severity, string title, string body)
        {
            return InsightUi.Callout(AquacultureUiStableIds.For(id, "callout"), severity, title, body);
        }

        public static InsightUiElement Stat(string id, string label, string value)
        {
            return InsightUi.StatRow(AquacultureUiStableIds.For(id, "stat"), label, value);
        }

        /// <summary>Compact status pill whose text remains explicit in every contrast mode.</summary>
        public static InsightUiElement Badge(string id, string label)
        {
            return InsightUi.Badge(AquacultureUiStableIds.For(id, "badge"), label ?? string.Empty);
        }

        public static InsightUiElement Meter(string id, string label, float current, float maximum, string valueText)
        {
            InsightUiMeter meter = InsightUi.Meter(AquacultureUiStableIds.For(id, "meter"), current, maximum)
                .SetLabel(label)
                .SetValueText(valueText);
            return meter;
        }

        public static InsightUiElement ResearchGate(string id, string title, string body)
        {
            return Callout(id, InsightUiCalloutSeverity.Info, title, body);
        }

        /// <summary>Semantic status presentation that includes a readable status label as well as tone.</summary>
        public static InsightUiElement Status(string id, AquacultureUiStatus status, string label, string body)
        {
            InsightUiCalloutSeverity severity;
            switch (status)
            {
                case AquacultureUiStatus.Healthy: severity = InsightUiCalloutSeverity.Success; break;
                case AquacultureUiStatus.Attention: severity = InsightUiCalloutSeverity.Warning; break;
                case AquacultureUiStatus.Critical: severity = InsightUiCalloutSeverity.Error; break;
                default: severity = InsightUiCalloutSeverity.Info; break;
            }
            return Callout(id, severity, label ?? string.Empty, body ?? string.Empty);
        }

        public static InsightUiElement Empty(string id, string message)
        {
            return InsightUi.Empty(AquacultureUiStableIds.For(id, "empty"), message);
        }

        /// <summary>One restrained, document-local surface for a coherent content section.</summary>
        public static InsightUiElement Panel(string id, InsightUiElement content)
        {
            return InsightUi.Surface(AquacultureUiStableIds.For(id, "panel"), content).SetPadding(10f);
        }

        public static InsightUiElement Description(string id, string text)
        {
            return InsightUi.Label(AquacultureUiStableIds.For(id, "description"), text ?? string.Empty,
                InsightUiTextStyle.Caption);
        }

        public static InsightUiElement ToggleSetting(string id, string label, string description,
            Func<bool> getter, Action<bool> setter)
        {
            InsightUiToggle toggle = InsightUi.Toggle(AquacultureUiStableIds.For(id, "toggle"), label)
                .Bind(getter, setter);
            toggle.SetTooltip(description);
            return InsightUi.Column(AquacultureUiStableIds.For(id, "setting"))
                .SetGap(2f)
                .Add(toggle, Description(id, description));
        }

        public static InsightUiElement SliderSetting(string id, string label, string description,
            float minimum, float maximum, Func<float> getter, Action<float> setter, Func<float, string> formatter)
        {
            InsightUiSlider slider = InsightUi.Slider(AquacultureUiStableIds.For(id, "slider"), getter(), minimum, maximum)
                .Bind(getter, setter);
            return SliderSetting(id, label, description, slider, () => formatter(getter()));
        }

        /// <summary>Wraps an existing slider with a live human-readable value and tooltip.</summary>
        public static InsightUiElement SliderSetting(string id, string label, string description,
            InsightUiSlider slider, Func<string> valueProvider)
        {
            InsightUiLabel value = InsightUi.Label(AquacultureUiStableIds.For(id, "value"), string.Empty,
                InsightUiTextStyle.Caption).SetTextProvider(valueProvider);
            if (slider != null) slider.SetTooltip(description);
            InsightUiStack header = InsightUi.Row(AquacultureUiStableIds.For(id, "header"))
                .SetGap(8f)
                .SetAlignment(InsightAlignment.Start, InsightAlignment.Center)
                .Add(InsightUi.Label(AquacultureUiStableIds.For(id, "label"), label),
                    InsightUi.Spacer(AquacultureUiStableIds.For(id, "spacer")).SetFlex(1f), value);
            return InsightUi.Column(AquacultureUiStableIds.For(id, "setting"))
                .SetGap(2f)
                .Add(header, slider, Description(id, description));
        }

        public static InsightUiElement IntSliderSetting(string id, string label, string description,
            int minimum, int maximum, Func<int> getter, Action<int> setter, Func<int, string> formatter)
        {
            return SliderSetting(id, label, description, minimum, maximum,
                () => getter(), value => setter((int)Math.Round(value)), value => formatter((int)Math.Round(value)));
        }

        public static InsightUiElement SelectSetting(string id, string label, string description,
            string[] options, Func<int> getter, Action<int> setter)
        {
            InsightUiSelect select = InsightUi.Select(AquacultureUiStableIds.For(id, "select"), label, options, getter())
                .Bind(getter, setter);
            select.SetTooltip(description);
            return select;
        }

        public static InsightUiElement ResetButton(string id, string label, Action reset)
        {
            InsightUiElement button = InsightUi.Button(AquacultureUiStableIds.For(id, "reset"), label, reset)
                .SetMinSize(150f, 30f)
                .SetTooltip(label);
            return InsightUi.Column(AquacultureUiStableIds.For(id, "reset-group")).SetGap(4f).Add(
                InsightUi.Divider(AquacultureUiStableIds.For(id, "reset-divider")), button);
        }

        public static InsightUiElement Page(string id, string title, string subtitle, params InsightUiElement[] content)
        {
            InsightUiStack column = InsightUi.Column(AquacultureUiStableIds.For(id, "content"))
                .SetGap(10f)
                .Add(InsightUi.SectionHeader(AquacultureUiStableIds.For(id, "header"), title, subtitle, null, null, true));
            column.Add(content);
            return InsightUi.Scroll(AquacultureUiStableIds.For(id, "scroll"),
                Panel(id, column));
        }
    }
}
