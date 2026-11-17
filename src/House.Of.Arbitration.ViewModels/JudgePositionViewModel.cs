#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels;

[QueryProperty(nameof(CompetitionId), Constants.Navigation.Parameter.COMPETITION_ID)]
[QueryProperty(nameof(ServerId), Constants.Navigation.Parameter.SERVER_ID)]
[QueryProperty(nameof(ServerName), Constants.Navigation.Parameter.SERVER_NAME)]
public partial class JudgePositionViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    /// <summary>
    /// Service d'accès aux données pour les compétitions
    /// </summary>
    private readonly IRepository<CompetitionModel> _competitionRepository;

    /// <summary>
    /// Service pour le client bluetooth
    /// </summary>
    private readonly IBluetoothClient _bluetoothClient;
    #endregion

    #region Attributs
    private int _competitionId;
    private CompetitionModel? _competition;
    private string _serverId = String.Empty;
    private string _serverName = String.Empty;
    private string _positionCode = String.Empty;
    #endregion

    #region Properties
    /// <summary>
    /// Obtient ou définit l'identifiant de la compétition
    /// </summary>
    public int CompetitionId
    {
        get => _competitionId;
        set => SetProperty(ref _competitionId, value);
    }

    /// <summary>
    /// Obtient ou définit la compétition courante
    /// </summary>
    public CompetitionModel? Competition
    {
        get => _competition;
        set
        {
            if (SetProperty(ref _competition, value))
            {
                OnPropertyChanged(nameof(IsTaolu));
                OnPropertyChanged(nameof(IsSanda));
            }
        }
    }

    /// <summary>
    /// Obtient ou définit l'identifiant du serveur
    /// </summary>
    public string ServerId
    {
        get => _serverId;
        set => SetProperty(ref _serverId, value);
    }

    /// <summary>
    /// Obtient ou définit le nom du serveur
    /// </summary>
    public string ServerName
    {
        get => _serverName;
        set
        {
            if (SetProperty(ref _serverName, value))
            {
                Title = !string.IsNullOrWhiteSpace(_serverName) ? $"JUDGE - {_serverName}" : "JUDGE";
            }
        }
    }

    /// <summary>
    /// Obtient la liste des positions pour les compétitions de type SANDA
    /// </summary>
    public ObservableCollection<JudgeModel> JudgePositions { get; } = new();

    /// <summary>
    /// Obtient la liste des positions pour le groupe A
    /// </summary>
    public ObservableCollection<JudgeModel> JudgePositionsGroupA { get; } = new();

    /// <summary>
    /// Obtient la liste des positions pour le groupe B
    /// </summary>
    public ObservableCollection<JudgeModel> JudgePositionsGroupB { get; } = new();

    /// <summary>
    /// Obtient la liste des positions pour le groupe C
    /// </summary>
    public ObservableCollection<JudgeModel> JudgePositionsGroupC { get; } = new();

    /// <summary>
    /// Obtient si la compétition courante est de type TAOLU
    /// </summary>
    public bool IsTaolu => Competition?.Type == CompetitionType.Taolu;

    /// <summary>
    /// Obtient si la compétition courante est de type SANDA
    /// </summary>
    public bool IsSanda => Competition?.Type == CompetitionType.Sanda;

    public string PositionCode
    {
        get => _positionCode;
        set => SetProperty(ref _positionCode, value);
    }
    #endregion

    #region Constructors
    /// <summary>
    /// 
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="resourceProvider"></param>
    /// <param name="popupService"></param>
    /// <param name="bluetoothClient"></param>
    /// <param name="competitionRepository"></param>
    public JudgePositionViewModel(
        ILogger<JudgePositionViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService,
        IBluetoothClient bluetoothClient,
        IRepository<CompetitionModel> competitionRepository
    ) : base(logger, resourceProvider, popupService)
    {
        _bluetoothClient = bluetoothClient;

        _competitionRepository = competitionRepository;

        _title = "JUDGE";

        for (int i = 1; i <= 5; i++)
        {
            JudgePositions.Add(new JudgeModel { Name = $"JUGE {i}", Number = i });
        }

        // Taolu positions: Groupe A (1..3), Groupe B (1..3), Groupe C (1..2)
        for (int i = 1; i <= 3; i++)
        {
            JudgePositionsGroupA.Add(new JudgeModel { Name = $"JUGE {i}", Number = i, Group = "A" });
        }

        for (int i = 1; i <= 3; i++)
        {
            JudgePositionsGroupB.Add(new JudgeModel { Name = $"JUGE {i}", Number = i, Group = "B" });
        }

        for (int i = 1; i <= 3; i++)
        {
            JudgePositionsGroupC.Add(new JudgeModel { Name = $"JUGE {i}", Number = i, Group = "C" });
        }

        ServerId = String.Empty;
    }
    #endregion

    #region Implement IQueryAttributable
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(ServerName), out var sNameObj) || query.TryGetValue(Constants.Navigation.Parameter.SERVER_NAME, out sNameObj))
        {
            if (sNameObj is string sName && !string.IsNullOrWhiteSpace(sName))
            {
                ServerName = sName;
            }
        }

        if (query.TryGetValue(nameof(CompetitionId), out var compIdObj) || query.TryGetValue(Constants.Navigation.Parameter.COMPETITION_ID, out compIdObj))
        {
            int newId = 0;

            if (compIdObj is int id)
            {
                newId = id;
            }
            else if (int.TryParse(compIdObj?.ToString(), out int parsedId))
            {
                newId = parsedId;
            }

            CompetitionId = newId;

            MainThread.BeginInvokeOnMainThread(async () => await LoadCompetitionAsync());
        }
    }
    #endregion

    #region Commandes
    /// <summary>
    /// Commande permettant de valider la position du juge
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    [RelayCommand]
    private async Task SelectPosition(JudgeModel judge)
    {
        PositionCode = !string.IsNullOrEmpty(judge.Group) ? $"{judge.Group}_{judge.Number}" : judge.Number.ToString();

        try
        {
            await _bluetoothClient.SendMessage(ServerId, $"{Constants.Message.JUDGE_POSITION}{PositionCode}");

            var navigationParameter = new Dictionary<string, object>
            {
                { Constants.Navigation.Parameter.SERVER_ID, ServerId },
                { Constants.Navigation.Parameter.SERVER_NAME, ServerName },
                { Constants.Navigation.Parameter.COMPETITION_ID, CompetitionId },
                { Constants.Navigation.Parameter.JUDGE_GROUP, judge.Group ?? String.Empty },
                { Constants.Navigation.Parameter.JUDGE_POSITION, judge.Number },
            };

            string page = IsTaolu ? Constants.Navigation.Page.JUDGE_TAOLU : Constants.Navigation.Page.JUDGE_SANDA;
            await Shell.Current.GoToAsync($"/{page}", navigationParameter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send JUDGE_POSITION to server.");
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Méthode permettant de récupérer les données de la compétition
    /// </summary>
    /// <returns></returns>
    private async Task LoadCompetitionAsync()
    {
        if (CompetitionId <= 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Competition = await _competitionRepository.GetByIdAsync(
                CompetitionId,
                "Categories.AgeRange",
                "Categories.Competitors.Competitor.Country"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading competition draws for competition {Id}", CompetitionId);
        }
        finally
        {
            IsBusy = false;
        }
    }
    #endregion
}