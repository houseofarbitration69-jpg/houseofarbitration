#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class DrawOrderModel : ObservableObject, IDrawModel
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

    private int? _competitorId;
    /// <summary>
    /// Obtient ou définit l'identifiant du competiteur
    /// </summary>
    public int? CompetitorId
    {
        get => _competitorId;
        set => SetProperty(ref _competitorId, value);
    }

    private CompetitorModel? _competitor;
    /// <summary>
    /// Obtient ou définit le compétiteur
    /// </summary>
    public CompetitorModel? Competitor
    {
        get => _competitor;
        set => SetProperty(ref _competitor, value);
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

    /// <summary>
    /// Obtient ou définit la liste des données des arbitres
    /// </summary>
    public List<RefereeDataModel>? RefereeDatas { get; set; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public RoundType Type { get; } = RoundType.Order;

    private bool _isFinished;
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public bool IsFinished
    {
        get => _isFinished;
        set => SetProperty(ref _isFinished, value);
    }

    private int _currentRound = 1;
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
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
