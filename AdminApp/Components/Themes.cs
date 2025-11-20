namespace ByProxy.AdminApp.Components {
    public static class Themes {
        public static readonly AppTheme DarkOcean = new DarkOceanTheme();
        public static readonly AppTheme LightOcean = new LightOceanTheme();

        public static readonly ImmutableList<AppTheme> AvailableThemes = [
            DarkOcean, LightOcean
        ];

        public static readonly AppTheme DefaultTheme = DarkOcean;
    }

    public abstract class AppTheme {
        public abstract string Name { get; }
        public abstract string DisplayName { get; }
        public abstract bool IsDarkTheme { get; }
        public abstract string TextColor { get; }
        public abstract string CaptionTextColor { get; }
        public abstract string HoverTextColor { get; }
        public abstract string DisabledColor { get; }
        public abstract string LightBgColor { get; }
        public abstract string BgColor { get; }
        public abstract string DarkBgColor { get; }
        public abstract string DarkerBgColor { get; }
        public abstract string ShadowColor { get; }

        public abstract string ButtonBgColor { get; }
        public abstract string ButtonBorderColor { get; }
        public abstract string ButtonHoverBgColor { get; }
        public abstract string ButtonHoverBorderColor { get; }

        public abstract string AlertTextColor { get; }
        public abstract string AlertBgColor { get; }
        public abstract string AlertBorderColor  { get; }
        public abstract string AlertHoverBgColor  { get; }
        public abstract string AlertHoverBorderColor  { get; }

        public abstract string InputBgColor { get; }
        public abstract string InputTextColor { get; }
        public abstract string InputPlaceholderTextColor { get; }
        public abstract string InputBorderColor { get; }

        public abstract string ReconnectBgColor { get; }
        public abstract string ReconnectTextColor { get; }

        public MarkupString ConvertToCss() {
            StringBuilder cssOutput = new StringBuilder();

            cssOutput.Append("<style>");

            /* -- Color Variables -- */
            cssOutput.Append(":root{");

            cssOutput.Append($"--text-color:{TextColor};");
            cssOutput.Append($"--caption-text-color:{CaptionTextColor};");
            cssOutput.Append($"--hover-text-color:{HoverTextColor};");
            cssOutput.Append($"--disabled-text-color:{DisabledColor};");
            cssOutput.Append($"--light-bg-color:{LightBgColor};");
            cssOutput.Append($"--bg-color:{BgColor};");
            cssOutput.Append($"--dark-bg-color:{DarkBgColor};");
            cssOutput.Append($"--darker-bg-color:{DarkerBgColor};");
            cssOutput.Append($"--shadow-color:{ShadowColor};");

            cssOutput.Append($"--button-bg-color:{ButtonBgColor};");
            cssOutput.Append($"--button-border-color:{ButtonBorderColor};");
            cssOutput.Append($"--button-hover-bg-color:{ButtonHoverBgColor};");
            cssOutput.Append($"--button-hover-border-color:{ButtonHoverBorderColor};");

            cssOutput.Append($"--alert-text-color:{AlertTextColor};");
            cssOutput.Append($"--alert-bg-color:{AlertBgColor};");
            cssOutput.Append($"--alert-border-color:{AlertBorderColor};");
            cssOutput.Append($"--alert-hover-bg-color:{AlertHoverBgColor};");
            cssOutput.Append($"--alert-hover-border-color:{AlertHoverBorderColor};");

            cssOutput.Append($"--input-bg-color:{InputBgColor};");
            cssOutput.Append($"--input-text-color:{InputTextColor};");
            cssOutput.Append($"--input-placeholder-text-color:{InputPlaceholderTextColor};");
            cssOutput.Append($"--input-border-color:{InputBorderColor};");

            cssOutput.Append($"--reconnect-bg-color:{ReconnectBgColor};");
            cssOutput.Append($"--reconnect-text-color:{ReconnectTextColor};");

            cssOutput.Append("}");

            cssOutput.Append("</style>");
            return new MarkupString(cssOutput.ToString());
        }
    }

    public class DarkOceanTheme : AppTheme {
        public override string Name => "ocean-dark";
        public override string DisplayName => "Dark Ocean";
        public override bool IsDarkTheme => true;
        public override string TextColor => "#dce7f3";
        public override string CaptionTextColor => "#9fc5c3";
        public override string DisabledColor => "#5f7380";
        public override string HoverTextColor => "#ffffff";
        public override string LightBgColor => "#203344";
        public override string BgColor => "#16232e";
        public override string DarkBgColor => "#101b25";
        public override string DarkerBgColor => "#0b141c";
        public override string ShadowColor => "#00000080";

        public override string ButtonBgColor => "#a85c0d";
        public override string ButtonBorderColor => "#8c4b0b";
        public override string ButtonHoverBgColor => "#c2711b";
        public override string ButtonHoverBorderColor => "#a85c0d";

        public override string AlertTextColor => "#eef3f9";
        public override string AlertBgColor => "#bf6c15";
        public override string AlertBorderColor => "#9d4b23";
        public override string AlertHoverBgColor => "#9d4b23";
        public override string AlertHoverBorderColor => "#9d4b23";

        public override string InputBgColor => "#2b3d4a";
        public override string InputTextColor => "#ffffff";
        public override string InputPlaceholderTextColor => "#8aa0ae";
        public override string InputBorderColor => "#1c2a35";

        public override string ReconnectBgColor => "#101010bd";
        public override string ReconnectTextColor => "#f4f9ff";
    }

    public class LightOceanTheme : AppTheme {
        public override string Name => "ocean-light";
        public override string DisplayName => "Ocean Light";
        public override bool IsDarkTheme => false;
        public override string TextColor => "#1a2a33";
        public override string CaptionTextColor => "#357d7a";
        public override string DisabledColor => "#8f9ca3";
        public override string HoverTextColor => "#000000";

        public override string LightBgColor => "#f9fcfd";
        public override string BgColor => "#f2f7f9";
        public override string DarkBgColor => "#e6eef2";
        public override string DarkerBgColor => "#dbe6eb";
        public override string ShadowColor => "#00000026";

        public override string ButtonBgColor => "#f5d48c";
        public override string ButtonBorderColor => "#e0be7a";
        public override string ButtonHoverBgColor => "#f2c978";
        public override string ButtonHoverBorderColor => "#d8b56b";

        public override string AlertTextColor => "#ffffff";
        public override string AlertBgColor => "#d57b1c";
        public override string AlertBorderColor => "#c16c3a";
        public override string AlertHoverBgColor => "#c16c3a";
        public override string AlertHoverBorderColor => "#c16c3a";

        public override string InputBgColor => "#ffffff";
        public override string InputTextColor => "#1a2a33";
        public override string InputPlaceholderTextColor => "#8b9aa3";
        public override string InputBorderColor => "#ccd9df";

        public override string ReconnectBgColor => "#d9d9d994";
        public override string ReconnectTextColor => "#0d1519";
    }
}
