#region Imports
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
#endregion

namespace House.Of.Arbitration.Localization;

public class LocalizationResourceManager : INotifyPropertyChanged
{
    #region Services
    private readonly ResourceManager _resourceManager;
    #endregion

    #region Events
    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    #endregion

    #region Attributs
    private static readonly Lazy<LocalizationResourceManager> _lazy = new(() => new LocalizationResourceManager());

    /// <summary>
    /// Gets the singleton instance of the <see cref="LocalizationResourceManager"/>.
    /// </summary>
    public static LocalizationResourceManager Instance => _lazy.Value;
    #endregion

    #region Properties
    /// <summary>
    /// An indexer that retrieves a translated string resource for the given key.
    /// </summary>
    /// <param name="text">The key of the string resource to retrieve.</param>
    /// <returns>The translated string.</returns>
    public string this[string text] => GetValue(text);
    #endregion

    #region Constructors
    /// <summary>
    /// 
    /// </summary>
    private LocalizationResourceManager()
    {
        _resourceManager = new ResourceManager("House.Of.Arbitration.Localization.Resources.AppResources", typeof(LocalizationResourceManager).Assembly);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Retrieves a translated string from the resource files based on the current culture.
    /// </summary>
    /// <param name="text">The key of the string resource.</param>
    /// <returns>The translated string, or the key itself if not found.</returns>
    public string GetValue(string text)
    {
        return _resourceManager.GetString(text, CultureInfo.CurrentUICulture) ?? text;
    }

    /// <summary>
    /// Sets the application's current UI culture.
    /// </summary>
    /// <param name="culture">The culture to set.</param>
    public void SetCulture(CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = culture;
        OnPropertyChanged(null); // A null property name indicates that all bindings should be refreshed.
    }
    #endregion

    #region Implement INotifyPropertyChanged
    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion
}
