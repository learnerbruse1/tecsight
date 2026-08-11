using System.Windows;

namespace TecSight.App.Themes;

/// <summary>深色/浅色主题切换（F11）。</summary>
public static class ThemeManager
{
    public static bool IsDark { get; private set; }

    public static void SetDark(bool dark)
    {
        if (IsDark == dark) return;
        var dicts = Application.Current.Resources.MergedDictionaries;
        dicts.Clear();
        IsDark = dark;
        dicts.Add(new ResourceDictionary
        {
            Source = new Uri(IsDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative),
        });
    }

    public static void Toggle() => SetDark(!IsDark);
}