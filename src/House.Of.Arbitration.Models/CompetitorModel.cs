#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class CompetitorModel : ObservableObject
{
    private int _id = 0;
    /// <summary>
    /// Obtient ou définit l'identifiant du compétiteur
    /// </summary>
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _firstName = string.Empty;
    /// <summary>
    /// Obtient ou définit le prénom du compétiteur
    /// </summary>
    public string FirstName
    {
        get => _firstName;
        set
        {
            if (SetProperty(ref _firstName, value))
            {
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    private string _lastName = string.Empty;
    /// <summary>
    /// Obtient ou définit le nom du compétiteur
    /// </summary>
    public string LastName
    {
        get => _lastName;
        set
        {
            if (SetProperty(ref _lastName, value))
            {
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    /// <summary>
    /// Obtient le nom complet du compétiteur
    /// </summary>
    public string Name => $"{FirstName} {LastName}".Trim();
    
    private Genre _genre = Genre.None;
    /// <summary>
    /// Obtient ou définit le genre du compétiteur
    /// </summary>
    public Genre Genre
    {
        get => _genre;
        set => SetProperty(ref _genre, value);
    }

    private DateTime _birthDate = DateTime.Now.AddYears(-20);
    /// <summary>
    /// Obtient ou définit la date de naissance du compétiteur
    /// </summary>
    public DateTime BirthDate
    {
        get => _birthDate;
        set => SetProperty(ref _birthDate, value);
    }
    
    private string _club = string.Empty;
    /// <summary>
    /// Obtient ou définit le nom du club du compétiteur
    /// </summary>
    public string Club
    {
        get => _club;
        set => SetProperty(ref _club, value);
    }

    private string? _countryIsoCode;
    /// <summary>
    /// Obtient ou définit le code ISO du pays du compétiteur
    /// </summary>
    public string? CountryIsoCode
    {
        get => _countryIsoCode;
        set => SetProperty(ref _countryIsoCode, value);
    }

    private CountryModel? _country;
    /// <summary>
    /// Obtient ou définit le pays du compétiteur
    /// </summary>
    public CountryModel? Country
    {
        get => _country;
        set => SetProperty(ref _country, value);
    }

    private double _weight;
    /// <summary>
    /// Obtient ou définit le poids du compétiteurs
    /// </summary>
    public double Weight
    {
        get => _weight;
        set => SetProperty(ref _weight, value);
    }
    
    private string? _photo;
    /// <summary>
    /// Obtient ou définit la photo du compétiteur
    /// </summary>
    public string? Photo
    {
        get => _photo;
        set => SetProperty(ref _photo, value);
    }

    /// <summary>
    /// Obtient ou définit la liste des catégoriesdu compétiteur
    /// </summary>
    public List<CompetitorCategoryModel>? Categories { get; set; }
}
