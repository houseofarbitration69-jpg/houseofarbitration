#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class AgeRangeModel : ObservableObject
{
    /// <summary>
    /// Obtient ou définit l'identifiant de la plage d'âge
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Obtient ou définit le libellé de la plage d'âge
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Obtient ou définit l'âge minimum
    /// </summary>
    public int MinAge { get; set; }

    /// <summary>
    /// Obtient ou définit l'âge maximum
    /// </summary>
    public int MaxAge { get; set; }

    /// <summary>
    /// Représente une plage d'âge vide (placeholder)
    /// </summary>
    public static AgeRangeModel Empty => new AgeRangeModel { Id = -1, Label = "Choisissez une tranche d'âge", MinAge = 0, MaxAge = 0 };

    private static List<AgeRangeModel>? _defaultRanges;

    /// <summary>
    /// Liste statique des plages d'âge par défaut
    /// </summary>
    public static List<AgeRangeModel> DefaultRanges
    {
        get
        {
            if (_defaultRanges == null)
            {
                _defaultRanges = new List<AgeRangeModel>
                {
                    Empty,
                    new AgeRangeModel { Id = 1, Label = "Cadets", MinAge = 12, MaxAge = 14 },
                    new AgeRangeModel { Id = 2, Label = "Juniors", MinAge = 15, MaxAge = 17 },
                    new AgeRangeModel { Id = 3, Label = "Seniors", MinAge = 18, MaxAge = 34 },
                    new AgeRangeModel { Id = 4, Label = "Espoirs", MinAge = 18, MaxAge = 21 },
                    new AgeRangeModel { Id = 5, Label = "Veterans", MinAge = 35, MaxAge = 99 }
                };
            }
            return _defaultRanges;
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is AgeRangeModel other)
            return Id == other.Id;
        return false;
    }

    public override int GetHashCode() => Id.GetHashCode();
    
    public override string ToString() => Label;
}
