using CommunityToolkit.Mvvm.ComponentModel;

namespace House.Of.Arbitration.Maui.Designer.Models;

public class CompetitorModel : ObservableObject
{
    #region Attributs
    private string _name = String.Empty;
    private Genre _genre = Genre.None;
    #endregion

    #region Properties
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public Genre Genre
    {
        get => _genre;
        set => SetProperty(ref _genre, value);
    }
    #endregion
}