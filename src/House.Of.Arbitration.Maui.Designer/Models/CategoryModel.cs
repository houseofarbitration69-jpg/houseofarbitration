using CommunityToolkit.Mvvm.ComponentModel;

namespace House.Of.Arbitration.Maui.Designer.Models;

public class CategoryModel : ObservableObject
{
    #region Attributs
    private string _name = String.Empty;
    private int _minOld = 0;
    private int _maxOld = 0;
    private Genre _genre = Genre.None;
    private CategoryType _type = CategoryType.None;
    #endregion

    #region Properties
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int MinOld
    {
        get => _minOld;
        set => SetProperty(ref _minOld, value);
    }

    public int MaxOld
    {
        get => _maxOld;
        set => SetProperty(ref _maxOld, value);
    }

    public Genre Genre
    {
        get => _genre;
        set => SetProperty(ref _genre, value);
    }

    public CategoryType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }
    #endregion
}

public enum Genre
{
    None,
    Men,
    Women
}

public enum CategoryType
{
    None,
    Taolu,
    Sanda
}