using System.Windows;
using System.Windows.Media;

namespace ImageBrowser
{
    public enum Theme { Mei, Lan, Zhu, Ju }

    public static class ThemeManager
    {
        public static event System.Action<Theme> ThemeChanged;

        public static void ApplyTheme(Theme theme)
        {
            ResourceDictionary resources = Application.Current.Resources;

            switch (theme)
            {
                // 梅 (Mei) - Winter Plum: Noble, Resilient
                // Background: Snowy White/Pale Pinkish Grey
                // Accent: Deep Plum Red / Crimson
                case Theme.Mei:
                    resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(250, 245, 245)); // Snow-like white
                    resources["Foreground"] = new SolidColorBrush(Color.FromRgb(60, 40, 40)); // Dark warm grey
                    resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(255, 250, 250)); // Pure snow
                    resources["GlassBackground"] = new SolidColorBrush(Color.FromArgb(128, 255, 240, 240)); // Frosty pink tint
                    resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(220, 100, 120)); // Soft Red
                    resources["SelectionBrush"] = new SolidColorBrush(Color.FromRgb(220, 20, 60)); // Crimson
                    
                    // Style: Minimalist with red accents
                    resources["ButtonBackground"] = new LinearGradientBrush(
                        Color.FromRgb(255, 240, 245), Color.FromRgb(255, 228, 225), 90);
                    resources["ButtonBorder"] = new SolidColorBrush(Color.FromRgb(205, 92, 92)); // IndianRed
                    resources["HeaderBackground"] = new LinearGradientBrush(
                        Color.FromRgb(255, 250, 250), Color.FromRgb(245, 235, 235), 90);
                    resources["AccentColor"] = new SolidColorBrush(Color.FromRgb(220, 20, 60));
                    resources["ButtonHover"] = new SolidColorBrush(Color.FromArgb(60, 220, 20, 60));
                    break;

                // 兰 (Lan) - Spring Orchid: Elegant, Secluded
                // Background: Pale Muted Cyan/Purple (Elegant)
                // Accent: Orchid Purple / Deep Slate Blue
                case Theme.Lan:
                    resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(240, 248, 255)); // AliceBlue
                    resources["Foreground"] = new SolidColorBrush(Color.FromRgb(25, 25, 112)); // MidnightBlue
                    resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(230, 230, 250)); // Lavender
                    resources["GlassBackground"] = new SolidColorBrush(Color.FromArgb(100, 240, 248, 255)); 
                    resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(100, 149, 237)); // CornflowerBlue
                    resources["SelectionBrush"] = new SolidColorBrush(Color.FromRgb(123, 104, 238)); // MediumSlateBlue
                    
                    resources["ButtonBackground"] = new LinearGradientBrush(
                        Color.FromRgb(240, 248, 255), Color.FromRgb(230, 230, 250), 90);
                    resources["ButtonBorder"] = new SolidColorBrush(Color.FromRgb(70, 130, 180)); // SteelBlue
                    resources["HeaderBackground"] = new LinearGradientBrush(
                        Color.FromRgb(245, 245, 255), Color.FromRgb(230, 230, 250), 90);
                    resources["AccentColor"] = new SolidColorBrush(Color.FromRgb(123, 104, 238));
                    resources["ButtonHover"] = new SolidColorBrush(Color.FromArgb(60, 123, 104, 238));
                    break;

                // 竹 (Zhu) - Summer Bamboo: Upright, Flexible
                // Background: Off-white / Rice Paper
                // Accent: Bamboo Green / Forest Green
                case Theme.Zhu:
                    resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(245, 255, 245)); // MintCream
                    resources["Foreground"] = new SolidColorBrush(Color.FromRgb(0, 50, 0)); // Dark Green
                    resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(240, 255, 240)); // Honeydew
                    resources["GlassBackground"] = new SolidColorBrush(Color.FromArgb(100, 230, 255, 230)); 
                    resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(60, 179, 113)); // MediumSeaGreen
                    resources["SelectionBrush"] = new SolidColorBrush(Color.FromRgb(34, 139, 34)); // ForestGreen
                    
                    resources["ButtonBackground"] = new LinearGradientBrush(
                        Color.FromRgb(245, 255, 245), Color.FromRgb(220, 245, 220), 90);
                    resources["ButtonBorder"] = new SolidColorBrush(Color.FromRgb(85, 107, 47)); // DarkOliveGreen
                    resources["HeaderBackground"] = new LinearGradientBrush(
                        Color.FromRgb(240, 255, 240), Color.FromRgb(210, 240, 210), 90);
                    resources["AccentColor"] = new SolidColorBrush(Color.FromRgb(34, 139, 34));
                    resources["ButtonHover"] = new SolidColorBrush(Color.FromArgb(60, 34, 139, 34));
                    break;

                // 菊 (Ju) - Autumn Chrysanthemum: Reclusive, Tenacious
                // Background: Warm Beige / Cream / Dark Elegant
                // Accent: Golden Yellow / Orange / Brown
                case Theme.Ju:
                    // Let's make this one a "Dark/Elegant" theme to replace the old Dark theme but with Gold accents
                    resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(40, 35, 30)); // Dark warm brown-grey
                    resources["Foreground"] = new SolidColorBrush(Color.FromRgb(255, 250, 240)); // FloralWhite
                    resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(50, 45, 40)); 
                    resources["GlassBackground"] = new SolidColorBrush(Color.FromArgb(40, 255, 215, 0)); // Gold tint
                    resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(218, 165, 32)); // GoldenRod
                    resources["SelectionBrush"] = new SolidColorBrush(Color.FromRgb(255, 140, 0)); // DarkOrange
                    
                    resources["ButtonBackground"] = new LinearGradientBrush(
                        Color.FromRgb(60, 55, 50), Color.FromRgb(45, 40, 35), 90);
                    resources["ButtonBorder"] = new SolidColorBrush(Color.FromRgb(184, 134, 11)); // DarkGoldenRod
                    resources["HeaderBackground"] = new LinearGradientBrush(
                        Color.FromRgb(55, 50, 45), Color.FromRgb(35, 30, 25), 90);
                    resources["AccentColor"] = new SolidColorBrush(Color.FromRgb(255, 215, 0)); // Gold
                    resources["ButtonHover"] = new SolidColorBrush(Color.FromArgb(60, 255, 215, 0));
                    break;
            }

            // Notify listeners
            if (ThemeChanged != null)
            {
                ThemeChanged(theme);
            }
        }
    }
}
