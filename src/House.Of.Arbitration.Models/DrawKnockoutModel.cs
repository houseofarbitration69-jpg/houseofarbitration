#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Layouts;
#endregion

namespace House.Of.Arbitration.Models;

public partial class DrawKnockoutModel : ObservableObject, IDrawModel
{
    private int _id = 0;
    /// <summary>
    /// Obtient ou définit l'identifiant du tirage en mode knockout
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
        set
        {
            if (SetProperty(ref _order, value))
            {
                OnPropertyChanged(nameof(StageName));
            }
        }
    }

    private bool _isBronzeMatch = false;
    /// <summary>
    /// Indique si ce match est la petite finale (combat pour la 3e place).
    /// </summary>
    public bool IsBronzeMatch
    {
        get => _isBronzeMatch;
        set
        {
            if (SetProperty(ref _isBronzeMatch, value))
            {
                OnPropertyChanged(nameof(StageName));
            }
        }
    }

    private bool _isBye = false;
    /// <summary>
    /// Indique si ce match est une exemption (Bye) du premier tour sans adversaire réel.
    /// Ces matches ne se déroulent pas sur le tatami et ne reçoivent pas de numéro de combat.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsBye
    {
        get => _isBye || (IsFinished && ((Competitor1Id.HasValue || Competitor1 != null) ^ (Competitor2Id.HasValue || Competitor2 != null)));
        set => SetProperty(ref _isBye, value);
    }

    /// <summary>
    /// Obtient le nom d'étape du combat (ex: "Finale", "Petite finale", "Demi-finale 1", "Quart de finale 2")
    /// </summary>
    public string StageName
    {
        get
        {
            if (IsBronzeMatch) return "Petite finale";
            var nonBronzeMatches = Draw?.DrawKnockouts?.Where(k => !k.IsBronzeMatch).OrderBy(k => k.Order).ToList();
            if (nonBronzeMatches != null && nonBronzeMatches.Count > 0)
            {
                int rankInNonBronze = nonBronzeMatches.IndexOf(this) + 1;
                if (rankInNonBronze > 0)
                {
                    return Helpers.KnockoutStageHelper.GetStageName(rankInNonBronze, nonBronzeMatches.Count, false);
                }
            }
            return Helpers.KnockoutStageHelper.GetStageName(Order, Draw?.DrawKnockouts?.Count ?? 0, false);
        }
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
        set
        {
            if (SetProperty(ref _draw, value))
            {
                OnPropertyChanged(nameof(StageName));
            }
        }
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
    public RoundType Type { get; } = RoundType.Knockouts;

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
