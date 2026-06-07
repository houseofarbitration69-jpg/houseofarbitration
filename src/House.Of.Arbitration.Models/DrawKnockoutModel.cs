#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class DrawKnockoutModel : ObservableObject, IDrawModel
{
    /// <summary>
    /// Obtient ou définit l'identifiant du tirage en mode knockout
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit l'ordre du match dans la catégorie
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit l'ordre du match dans la compétition
    /// </summary>
    public int GlobalOrder { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit l'identifiant du tirage
    /// </summary>
    public int DrawId { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit le tirage
    /// </summary>
    public DrawModel? Draw { get; set; }

    /// <summary>
    /// Obtient ou définit l'identifiant du premier compétiteur
    /// </summary>
    public int? Competitor1Id { get; set; }

    /// <summary>
    /// Obtient ou définit le premier compétiteur
    /// </summary>
    public CompetitorModel? Competitor1 { get; set; }

    /// <summary>
    /// Obtient ou définit l'identifiant du second compétiteur
    /// </summary>
    public int? Competitor2Id { get; set; }

    /// <summary>
    /// Obtient ou définit le second compétiteur
    /// </summary>
    public CompetitorModel? Competitor2 { get; set; }

    /// <summary>
    /// Obtient ou définit l'identifiant du vainqueur
    /// </summary>
    public int? WinnerId { get; set; }
    
    /// <summary>
    /// Obtient ou définit le vainqueur
    /// </summary>
    public CompetitorModel? Winner { get; set; }  

    /// <summary>
    /// Obtient ou définit l'identifiant du perdant
    /// </summary>
    public int? LooserId { get; set; }

    /// <summary>
    /// Obtient ou définit le perdant
    /// </summary>
    public CompetitorModel? Looser { get; set; }

    /// <summary>
    /// Obtient ou définit la liste des données des arbitres
    /// </summary>
    public List<RefereeDataModel>? RefereeDatas { get; set; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public RoundType Type { get; } = RoundType.Knockouts;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public bool IsFinished { get; set; }
}
