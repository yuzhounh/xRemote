using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Shawn.Utils.Wpf;

namespace _1RM.Service
{
    public class ThemeService
    {
        private readonly ResourceDictionary _appResourceDictionary;
        public ThemeConfig CurrentTheme;
        public Dictionary<string, ThemeConfig> Themes { get; } = new Dictionary<string, ThemeConfig>();
        public ThemeService(ResourceDictionary appResourceDictionary, ThemeConfig defaultTheme)
        {
            _appResourceDictionary = appResourceDictionary;
            Themes.Add("Light", new ThemeConfig()
            {
                ThemeName = "Light",
                PrimaryMidColor = "#FFF5F5F5",
                PrimaryLightColor = "#FFFFFFFF",
                PrimaryDarkColor = "#FFECECEC",
                PrimaryTextColor = "#FF303030",
                AccentMidColor = "#FF525252",
                AccentLightColor = "#FF6B6B6B",
                AccentDarkColor = "#FF3D3D3D",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFFFFFFF",
                BackgroundTextColor = "#FF303030",
            });
            Themes.Add("Dark", new ThemeConfig()
            {
                ThemeName = "Dark",
                PrimaryMidColor = "#323233",
                PrimaryLightColor = "#474748",
                PrimaryDarkColor = "#2d2d2d",
                PrimaryTextColor = "#cccccc",
                AccentMidColor = "#FF007ACC",
                AccentLightColor = "#FF32A7F4",
                AccentDarkColor = "#FF0061A3",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#1e1e1e",
                BackgroundTextColor = "#cccccc",
            });
            Themes.Add("PRemoteM", new ThemeConfig()
            {
                ThemeName = "PRemoteM",
                PrimaryMidColor = "#102b3e",
                PrimaryLightColor = "#445a68",
                PrimaryDarkColor = "#0c2230",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FFE83D61",
                AccentLightColor = "#FFED6884",
                AccentDarkColor = "#FFB5304C",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#ced8e1",
                BackgroundTextColor = "#000000",
            });
            Themes.Add("SecretKey", new ThemeConfig()
            {
                ThemeName = "SecretKey",
                PrimaryMidColor = "#FF473368",
                PrimaryLightColor = "#796090",
                PrimaryDarkColor = "#382853",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FFEF6D3B",
                AccentLightColor = "#FF9A63",
                AccentDarkColor = "#BF572F",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFF2F1EC",
                BackgroundTextColor = "#000000",
            });
            Themes.Add("Greystone", new ThemeConfig()
            {
                ThemeName = "Greystone",
                PrimaryMidColor = "#FFC7D0D5",
                PrimaryLightColor = "#F9FDFD",
                PrimaryDarkColor = "#9FA6AA",
                PrimaryTextColor = "#FF1B2C3F",
                AccentMidColor = "#FFFF7247",
                AccentLightColor = "#FFED583A",
                AccentDarkColor = "#CC5B38",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFF5F5F5",
                BackgroundTextColor = "#000000",
            });
            Themes.Add("Asphalt", new ThemeConfig()
            {
                ThemeName = "Asphalt",
                PrimaryMidColor = "#FF393939",
                PrimaryLightColor = "#6B6661",
                PrimaryDarkColor = "#2D2D2D",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FFFF7247",
                AccentLightColor = "#FFED583A",
                AccentDarkColor = "#CC5B38",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFF5F5F5",
                BackgroundTextColor = "#000000",
            });
            Themes.Add("Wine", new ThemeConfig()
            {
                ThemeName = "Wine",
                PrimaryMidColor = "#FF57112D",
                PrimaryLightColor = "#893E55",
                PrimaryDarkColor = "#450D24",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FFA82159",
                AccentLightColor = "#DA4E81",
                AccentDarkColor = "#861A47",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFFDEAD9",
                BackgroundTextColor = "#FF450D24",
            });
            Themes.Add("Forest", new ThemeConfig()
            {
                ThemeName = "Forest",
                PrimaryMidColor = "#FF253938",
                PrimaryLightColor = "#576660",
                PrimaryDarkColor = "#1D2D2C",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FF5FA291",
                AccentLightColor = "#91CFB9",
                AccentDarkColor = "#4C8174",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFF5F5F5",
                BackgroundTextColor = "#FF303030",
            });
            Themes.Add("Soil", new ThemeConfig()
            {
                ThemeName = "Soil",
                PrimaryMidColor = "#FF776245",
                PrimaryLightColor = "#A98F6D",
                PrimaryDarkColor = "#FF735E41",
                PrimaryTextColor = "#FFFFFFFF",
                AccentMidColor = "#FF0193B8",
                AccentLightColor = "#33C0E0",
                AccentDarkColor = "#007593",
                AccentTextColor = "#FFFFFFFF",
                BackgroundColor = "#FFCFC3B5",
                BackgroundTextColor = "#FF080000",
            });

            // Upgrade either shipped Light palette; preserve custom colors and fonts.
            var oldLight = new ThemeConfig { ThemeName="Light", PrimaryMidColor="#FFF2F3F5", PrimaryLightColor="#FFFFFFFF",
                PrimaryDarkColor="#FFE4E7EB", PrimaryTextColor="#FF232323", AccentMidColor="#FFE83D61",
                AccentLightColor="#FFED6884", AccentDarkColor="#FFB5304C", AccentTextColor="#FFFFFFFF",
                BackgroundColor="#FFFFFFFF", BackgroundTextColor="#000000" };
            var blueLight = new ThemeConfig { ThemeName="Light", PrimaryMidColor="#FFD3E3FD", PrimaryLightColor="#FFE8F0FE",
                PrimaryDarkColor="#FFE4E7EB", PrimaryTextColor="#FF232323", AccentMidColor="#FF1A73E8",
                AccentLightColor="#FF4A90F0", AccentDarkColor="#FF1557B0", AccentTextColor="#FFFFFFFF",
                BackgroundColor="#FFFFFFFF", BackgroundTextColor="#000000" };
            var paletteFields = typeof(ThemeConfig).GetFields().Where(f => f.Name.EndsWith("Color", StringComparison.Ordinal)).ToArray();
            if (defaultTheme.ThemeName == "Light" && new[] { oldLight, blueLight }.Any(palette => paletteFields.All(f =>
                ColorAndBrushHelper.HexColorToMediaColor((string)f.GetValue(defaultTheme)!) ==
                ColorAndBrushHelper.HexColorToMediaColor((string)f.GetValue(palette)!))))
                foreach (var field in paletteFields) field.SetValue(defaultTheme, field.GetValue(Themes["Light"]));
            CurrentTheme = defaultTheme;
            ApplyTheme(defaultTheme);
        }

        public void ApplyTheme(ThemeConfig theme)
        {
            const string resourceTypeKey = "__Resource_Type_Key";
            const string resourceTypeValue = "__Resource_Type_Value=theme";
            void SetKey(IDictionary rd, string key, object value)
            {
                if (!rd.Contains(key))
                    rd.Add(key, value);
                else
                    rd[key] = value;
            }

            // create new theme resources
            var rd = new ResourceDictionary();
            SetKey(rd, resourceTypeKey, resourceTypeValue);
            SetKey(rd, "PrimaryMidColor", ColorAndBrushHelper.HexColorToMediaColor(theme.PrimaryMidColor));
            var background = ColorAndBrushHelper.HexColorToMediaColor(theme.BackgroundColor);
            bool darkMenu = (background.R * 0.299 + background.G * 0.587 + background.B * 0.114) < 128;
            bool stockLight = theme.ThemeName == "Light";
            var bodyText = ColorAndBrushHelper.HexColorToMediaColor(theme.BackgroundTextColor);
            SolidColorBrush MutedText(double strength) => new SolidColorBrush(Color.FromRgb(
                (byte)Math.Round(bodyText.R * strength + background.R * (1 - strength)),
                (byte)Math.Round(bodyText.G * strength + background.G * (1 - strength)),
                (byte)Math.Round(bodyText.B * strength + background.B * (1 - strength))));
            SetKey(rd, "SecondaryTextBrush", MutedText(0.80));
            SetKey(rd, "PlaceholderTextBrush", MutedText(0.68));
            // Text uses a lighter semantic color on dark surfaces; control accent colors stay unchanged.
            SetKey(rd, "ValidationErrorBrush", ColorAndBrushHelper.ColorToMediaBrush(darkMenu ? "#FF8A80" : "#B22222"));
            SetKey(rd, "ReadableAccentTextBrush", ColorAndBrushHelper.ColorToMediaBrush(darkMenu ? theme.AccentLightColor : theme.AccentMidColor));
            // Match LightNote's neutral scrollbar palette independently of the accent color.
            SetKey(rd, "ScrollBarThumbBrush", ColorAndBrushHelper.ColorToMediaBrush(darkMenu ? "#2F343A" : "#EEF0F2"));
            SetKey(rd, "ScrollBarThumbHoverBrush", ColorAndBrushHelper.ColorToMediaBrush(darkMenu ? "#3F454D" : "#DDE1E5"));
            SetKey(rd, "ScrollBarThumbDragBrush", ColorAndBrushHelper.ColorToMediaBrush(darkMenu ? "#525963" : "#C4CAD1"));
            bool stockDark = theme.ThemeName == "Dark";
            var menuHover = stockLight ? "#E8E8E8" : stockDark ? "#3B3B3B" : theme.PrimaryLightColor;
            SetKey(rd, "MenuSurfaceBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? theme.PrimaryMidColor : stockDark ? "#2B2B2B" : theme.PrimaryDarkColor));
            SetKey(rd, "MenuHoverColor", ColorAndBrushHelper.HexColorToMediaColor(menuHover));
            SetKey(rd, "MenuHoverBrush", ColorAndBrushHelper.ColorToMediaBrush(menuHover));
            SetKey(rd, "MenuBorderBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#D9D9D9" : stockDark ? "#454545" : "#707A8C"));
            SetKey(rd, "LauncherSurfaceBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.PrimaryMidColor));
            SetKey(rd, "LauncherSecondaryBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#686868" : theme.PrimaryTextColor));
            SetKey(rd, "LauncherSelectedBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#FFFFFF" : theme.PrimaryLightColor));
            SetKey(rd, "LauncherHoverBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#ECECEC" : "#18FFFFFF"));
            SetKey(rd, "LauncherSelectionBorderBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#DADADA" : "#40FFFFFF"));
            SetKey(rd, "LauncherTitleFontSize", Math.Min(18.0, Math.Max(14.0, theme.FontSize + 2.0)));
            SetKey(rd, "LauncherDetailFontSize", Math.Min(14.0, Math.Max(11.0, theme.FontSize - 1.0)));
            SetKey(rd, "MainTabSelectedBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#FFFFFF" : theme.AccentMidColor));
            SetKey(rd, "MainTabSelectedTextBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? theme.PrimaryTextColor : theme.AccentTextColor));
            SetKey(rd, "MainTabHoverBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#E8E8E8" : theme.AccentLightColor));
            SetKey(rd, "MenuDividerBrush", ColorAndBrushHelper.ColorToMediaBrush(darkMenu ? "#454850" : (stockLight ? "#D9D9D9" : "#D3E3FD")));
            SetKey(rd, "DefaultBorderColor", ColorAndBrushHelper.HexColorToMediaColor(stockLight ? "#B8B8B8" : "#707A8C"));
            SetKey(rd, "DefaultBorderBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#B8B8B8" : "#707A8C"));
            SetKey(rd, "ProtocolSettingsSurfaceBrush", stockLight || stockDark
                ? ColorAndBrushHelper.ColorToMediaBrush(theme.BackgroundColor)
                : new SolidColorBrush(ColorAndBrushHelper.HexColorToMediaColor(theme.BackgroundTextColor)) { Opacity = .25 });
            SetKey(rd, "ProtocolCardSurfaceBrush", ColorAndBrushHelper.ColorToMediaBrush(stockDark ? "#252525" : theme.BackgroundColor));
            SetKey(rd, "ProtocolCardBorderThickness", new Thickness(stockDark ? 1 : 0));
            SetKey(rd, "ProtocolNavSelectedBrush", ColorAndBrushHelper.ColorToMediaBrush(stockDark ? "#2A2A2A" : (stockLight ? "#F0F0F0" : theme.PrimaryLightColor)));
            SetKey(rd, "ProtocolNavHoverBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#F7F7F7" : "#12FFFFFF"));
            SetKey(rd, "ProtocolCardHeaderBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? theme.BackgroundColor : theme.PrimaryMidColor));
            SetKey(rd, "ProtocolCardDividerBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#E5E5E5" : theme.PrimaryDarkColor));
            SetKey(rd, "ProtocolCardBorderBrush", ColorAndBrushHelper.ColorToMediaBrush(stockDark ? "#353535" : (stockLight ? "#E2E2E2" : theme.PrimaryLightColor)));
            SetKey(rd, "SettingsNavSelectedBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#F0F0F0" : theme.PrimaryLightColor));
            SetKey(rd, "SettingsNavHoverBrush", ColorAndBrushHelper.ColorToMediaBrush(stockLight ? "#F7F7F7" : "#12FFFFFF"));
            SetKey(rd, "PrimaryLightColor", ColorAndBrushHelper.HexColorToMediaColor(theme.PrimaryLightColor));
            SetKey(rd, "PrimaryDarkColor", ColorAndBrushHelper.HexColorToMediaColor(theme.PrimaryDarkColor));
            SetKey(rd, "PrimaryTextColor", ColorAndBrushHelper.HexColorToMediaColor(theme.PrimaryTextColor));
            SetKey(rd, "AccentMidColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentMidColor));
            SetKey(rd, "AccentLightColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentLightColor));
            SetKey(rd, "AccentDarkColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentDarkColor));
            SetKey(rd, "AccentTextColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentTextColor));
            SetKey(rd, "BackgroundColor", ColorAndBrushHelper.HexColorToMediaColor(theme.BackgroundColor));
            SetKey(rd, "BackgroundTextColor", ColorAndBrushHelper.HexColorToMediaColor(theme.BackgroundTextColor));


            SetKey(rd, "PrimaryMidBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.PrimaryMidColor));
            // Both windows share the same strip; light palettes need opaque selected tabs.
            var primary = ColorAndBrushHelper.HexColorToMediaColor(theme.PrimaryMidColor);
            bool lightTabs = primary.R * .299 + primary.G * .587 + primary.B * .114 >= 128;
            SetKey(rd, "SessionTabStripBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.PrimaryMidColor));
            SetKey(rd, "SessionTabSelectedBrush", ColorAndBrushHelper.ColorToMediaBrush(lightTabs ? "#FFFFFF" : "#24FFFFFF"));
            SetKey(rd, "SessionTabHoverBrush", ColorAndBrushHelper.ColorToMediaBrush(lightTabs ? (stockLight ? "#E8E8E8" : "#60FFFFFF") : "#12FFFFFF"));
            SetKey(rd, "SessionTabSeparatorBrush", ColorAndBrushHelper.ColorToMediaBrush(lightTabs ? (stockLight ? "#DADADA" : "#40000000") : (theme.ThemeName == "Dark" ? "#406F7378" : "#40FFFFFF")));
            SetKey(rd, "SessionTabActionHoverBrush", ColorAndBrushHelper.ColorToMediaBrush(lightTabs ? "#10000000" : "#20FFFFFF"));
            SetKey(rd, "SessionTabActionPressedBrush", ColorAndBrushHelper.ColorToMediaBrush(lightTabs ? "#20000000" : "#35FFFFFF"));
            SetKey(rd, "SessionTabPrefixBrush", ColorAndBrushHelper.ColorToMediaBrush(lightTabs ? (stockLight ? "#ECECEC" : "#60FFFFFF") : "#12FFFFFF"));
            SetKey(rd, "PrimaryLightBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.PrimaryLightColor));
            SetKey(rd, "PrimaryDarkBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.PrimaryDarkColor));
            SetKey(rd, "PrimaryTextBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.PrimaryTextColor));
            SetKey(rd, "AccentMidBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.AccentMidColor));
            SetKey(rd, "AccentLightBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.AccentLightColor));
            SetKey(rd, "AccentDarkBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.AccentDarkColor));
            SetKey(rd, "AccentTextBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.AccentTextColor));
            SetKey(rd, "BackgroundBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.BackgroundColor));
            SetKey(rd, "BackgroundTextBrush", ColorAndBrushHelper.ColorToMediaBrush(theme.BackgroundTextColor));

            SetKey(rd, "PrimaryColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentMidColor));
            SetKey(rd, "DarkPrimaryColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentDarkColor));
            //SetKey(rd, "PrimaryDarkColor", ColorAndBrushHelper.HexColorToMediaColor(theme.AccentTextColor));

            var font = GetFontFamily(theme.FontFamily);
            SetKey(rd, "GlobalFontFamily", font);
            theme.FontSize = Math.Max(10, theme.FontSize);
            double globalFontSizeSmall = Math.Min(20.0, theme.FontSize - 2.0);
            double globalFontSizeBody = Math.Min(20.0, theme.FontSize);
            double globalFontSizeSubtitle = Math.Min(20.0, theme.FontSize + 2.0);
            double globalFontSizeTitle = Math.Min(20.0, theme.FontSize + 6.0);
            SetKey(rd, "GlobalFontSizeTitle", globalFontSizeTitle);
            SetKey(rd, "GlobalFontSizeSubtitle", globalFontSizeSubtitle);
            SetKey(rd, "GlobalFontSizeBody", globalFontSizeBody);
            SetKey(rd, "GlobalFontSizeSmall", globalFontSizeSmall);





            // remove old theme resources
            var rs = _appResourceDictionary.MergedDictionaries.Where(o =>
                (o?.Source?.IsAbsoluteUri == true && o.Source.AbsolutePath.ToLower().IndexOf("Default.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                || o?[resourceTypeKey]?.ToString() == resourceTypeValue).ToArray();
            foreach (var r in rs)
            {
                _appResourceDictionary.MergedDictionaries.Remove(r);
            }

            // add new theme resources
            _appResourceDictionary.MergedDictionaries.Add(rd);
        }

        private static FontFamily GetFontFamily(string name)
        {
            // set default font family
            var fontFamily = Fonts.SystemFontFamilies.FirstOrDefault(x => string.Equals(x.Source, name, StringComparison.CurrentCultureIgnoreCase));
            fontFamily ??= Fonts.SystemFontFamilies.FirstOrDefault(x => string.Equals(x.Source, "Microsoft YaHei", StringComparison.CurrentCultureIgnoreCase));
            fontFamily ??= Fonts.SystemFontFamilies.FirstOrDefault(x => x.Source.EndsWith("YaHei", StringComparison.OrdinalIgnoreCase));
            fontFamily ??= Fonts.SystemFontFamilies.FirstOrDefault(x => x.Source.IndexOf("YaHei", StringComparison.OrdinalIgnoreCase) >= 0);
            fontFamily ??= Fonts.SystemFontFamilies.FirstOrDefault(x => x.Source.IndexOf("雅黑", StringComparison.OrdinalIgnoreCase) >= 0);
            fontFamily ??= Fonts.SystemFontFamilies.FirstOrDefault(x => x.Source.IndexOf("雅黑", StringComparison.OrdinalIgnoreCase) >= 0);

            return fontFamily ?? Fonts.SystemFontFamilies.First();
        }
    }
}
