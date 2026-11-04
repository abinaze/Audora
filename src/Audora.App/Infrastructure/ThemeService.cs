using System.Windows;

namespace Audora.App.Infrastructure;

public static class ThemeService
{
    public static void Apply(bool dark)
    {
        var source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = source };

        foreach (Window window in Application.Current.Windows)
        {
            TitleBar.Apply(window, dark);
        }
    }
}
