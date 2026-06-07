#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class CompetitionModel : ObservableObject
{
    /// <summary>
    /// Obtient ou définit l'identifiant de la compétition
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit le nom de la compétition
    /// </summary>
    public string Name { get; set; } = String.Empty;

    /// <summary>
    /// Obtient ou définit la date de début de la compétition
    /// </summary>
    public DateTime Date { get; set; } = DateTime.Now;

    /// <summary>
    /// Obtient ou définit la liste des catégories de la compétition
    /// </summary>
    public List<CategoryModel> Categories { get; set; } = new();
}
