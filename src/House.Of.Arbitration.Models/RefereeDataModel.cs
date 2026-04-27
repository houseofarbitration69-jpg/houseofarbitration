#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class RefereeDataModel : ObservableObject
{
    /// <summary>
    /// Obtient ou définit l'identifiant 
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit l'arbitre
    /// </summary>
    public string Referee { get; set; } = String.Empty;

    /// <summary>
    /// Obtient ou définit la date de la donnée
    /// </summary>
    public required DateTime Date { get; set; }

    /// <summary>
    /// Obtient ou définit la donnée
    /// </summary>
    public string Data { get; set; } = String.Empty;

    /// <summary>
    /// 
    /// </summary>
    public bool IsCorrection { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public int? DrawKnockoutId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public DrawKnockoutModel? DrawKnockoutModel { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public int? DrawOrderId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public DrawOrderModel? DrawOrder { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public int? DrawPoolsId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public DrawPoolsModel? DrawPools { get; set; }
}
