#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CompetitorPopupViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    private readonly IPopupService _popupService;
    #endregion

    #region Attributs
    private CompetitorModel _competitor = new();
    private LocalizedEnum<Genre>? _selectedGenre;
    #endregion

    #region Properties
    public CompetitorModel Competitor
    {
        get => _competitor;
        set
        {
            SetProperty(ref _competitor, value);
            if (value != null)
            {
                SelectedGenre = Genres.FirstOrDefault(x => x.Value == value.Genre);
            }
        }
    }

    public LocalizedEnum<Genre>? SelectedGenre
    {
        get => _selectedGenre;
        set => SetProperty(ref _selectedGenre, value);
    }

    public List<LocalizedEnum<Genre>> Genres { get; }
    #endregion

    #region Constructor
    public CompetitorPopupViewModel(IPopupService popupService, ILogger<CompetitorPopupViewModel> logger, ResourceProvider resourceProvider)
        : base(logger, resourceProvider)
    {
        _popupService = popupService;
        Genres = LocalizeEnum<Genre>("ENUM_GENRE_");
    }
    #endregion

    #region Implement IQueryAttributable
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey(nameof(Competitor)))
        {
            Competitor = (CompetitorModel)query[nameof(Competitor)];
        }
    }
    #endregion

    #region Private Methods
    private List<LocalizedEnum<T>> LocalizeEnum<T>(string prefix) where T : Enum
    {
        return Enum.GetValues(typeof(T))
            .Cast<T>()
            .Select(e => new LocalizedEnum<T>(e, LocalizationResourceManager.Instance.GetValue($"{prefix}{e.ToString().ToUpper()}")))
            .ToList();
    }

    public CompetitorModel GetResult()
    {
        Competitor.Genre = SelectedGenre?.Value ?? Genre.None;
        return Competitor;
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Close()
    {
        await _popupService.ClosePopupAsync(Shell.Current);
    }

    [RelayCommand]
    private async Task Validate()
    {
        await _popupService.ClosePopupAsync<CompetitorModel>(Shell.Current, GetResult());
    }
    #endregion
}
