#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class DrawPoolsModel : ObservableObject, IDrawModel
{
    private int _id = 0;
    /// <summary>
    /// Obtient ou définit l'identifiant du tirage en mode 'Pools'
    /// </summary>
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private int _order = 0;
    /// <summary>
    /// Obtient ou définit l'ordre du match dans la catégorie
    /// </summary>
    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
    }

    private int _globalOrder = 0;
    /// <summary>
    /// Obtient ou définit l'ordre du match dans la compétition
    /// </summary>
    public int GlobalOrder
    {
        get => _globalOrder;
        set => SetProperty(ref _globalOrder, value);
    }

    private int _drawId = 0;
    /// <summary>
    /// Obtient ou définit l'identifiant du tirage
    /// </summary>
    public int DrawId
    {
        get => _drawId;
        set => SetProperty(ref _drawId, value);
    }

    private DrawModel? _draw;
    /// <summary>
    /// Obtient ou définit le tirage
    /// </summary>
    public DrawModel? Draw
    {
        get => _draw;
        set => SetProperty(ref _draw, value);
    }

    private int? _competitor1Id;
    /// <summary>
    /// Obtient ou définit l'identifiant du premier compétiteur
    /// </summary>
    public int? Competitor1Id
    {
        get => _competitor1Id;
        set => SetProperty(ref _competitor1Id, value);
    }

    private CompetitorModel? _competitor1;
    /// <summary>
    /// Obtient ou définit le premier compétiteur
    /// </summary>
    public CompetitorModel? Competitor1
    {
        get => _competitor1;
        set => SetProperty(ref _competitor1, value);
    }

    private int? _competitor2Id;
    /// <summary>
    /// Obtient ou définit l'identifiant du second compétiteur
    /// </summary>
    public int? Competitor2Id
    {
        get => _competitor2Id;
        set => SetProperty(ref _competitor2Id, value);
    }

    private CompetitorModel? _competitor2;
    /// <summary>
    /// Obtient ou définit le second compétiteur
    /// </summary>
    public CompetitorModel? Competitor2
    {
        get => _competitor2;
        set => SetProperty(ref _competitor2, value);
    }

    private int? _winnerId;
    /// <summary>
    /// Obtient ou définit l'identifiant du vainqueur
    /// </summary>
    public int? WinnerId
    {
        get => _winnerId;
        set => SetProperty(ref _winnerId, value);
    }

    private CompetitorModel? _winner;
    /// <summary>
    /// Obtient ou définit le vainqueur
    /// </summary>
    public CompetitorModel? Winner
    {
        get => _winner;
        set => SetProperty(ref _winner, value);
    }

    private int? _looserId;
    /// <summary>
    /// Obtient ou définit l'identifiant du perdant
    /// </summary>
    public int? LooserId
    {
        get => _looserId;
        set => SetProperty(ref _looserId, value);
    }

    private CompetitorModel? _looser;
    /// <summary>
    /// Obtient ou définit le perdant
    /// </summary>
    public CompetitorModel? Looser
    {
        get => _looser;
        set => SetProperty(ref _looser, value);
    }

    /// <summary>
    /// Obtient ou définit la liste des données des arbitres
    /// </summary>
    public List<RefereeDataModel>? RefereeDatas { get; set; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public RoundType Type { get; } = RoundType.Pools;

    private bool _isFinished;
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public bool IsFinished
    {
        get => _isFinished;
        set => SetProperty(ref _isFinished, value);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    private int _currentRound = 1;
    public int CurrentRound
    {
        get => _currentRound;
        set => SetProperty(ref _currentRound, value);
    }

    private string _roundResultsJson = string.Empty;
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public string RoundResultsJson
    {
        get => _roundResultsJson;
        set => SetProperty(ref _roundResultsJson, value);
    }
}
