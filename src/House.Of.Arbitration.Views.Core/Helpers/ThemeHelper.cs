using House.Of.Arbitration.Views.Core.Styles;

namespace House.Of.Arbitration.Views.Core.Helpers
{
    public enum AppThemeType
    {
        Martial,
        Pastel
    }

    public static class ThemeHelper
    {
        public static void SetTheme(AppThemeType themeType)
        {
            var mergedDictionaries = Application.Current?.Resources.MergedDictionaries;
            if (mergedDictionaries != null)
            {
                // On cherche le dictionnaire de thème actuel
                var themeDictionary = mergedDictionaries.FirstOrDefault(d => 
                    d is MartialTheme || d is PastelTheme);

                if (themeDictionary != null)
                {
                    mergedDictionaries.Remove(themeDictionary);
                }

                // On ajoute le nouveau thème sous forme d'instance de classe
                switch (themeType)
                {
                    case AppThemeType.Martial:
                        mergedDictionaries.Add(new MartialTheme());
                        break;
                    case AppThemeType.Pastel:
                        mergedDictionaries.Add(new PastelTheme());
                        break;
                }
            }
        }
    }
}
