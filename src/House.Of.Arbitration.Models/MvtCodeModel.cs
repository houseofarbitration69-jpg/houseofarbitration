#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class MvtCodeModel : ObservableObject
{
    public int Id { get; set; }

    public string Code { get; set; } = String.Empty;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int? MvtTypeId { get; set; }

    public MvtTypeModel? Type { get; set; }

    public int? MvtGroupId { get; set; }

    public MvtGroupModel? Group { get; set; }

    public double Value { get; set; } = 0;

    /// <summary>
    /// Liste statique des codes de mouvements par défaut
    /// </summary>
    public static List<MvtCodeModel> DefaultCodes => new()
    {
        new MvtCodeModel { Id = 1, Code = "01", Category = "Taolu", Description = "Saut frontal frappe de pied", MvtTypeId = 1, MvtGroupId = 1, Value = 0.2 },
        new MvtCodeModel { Id = 2, Code = "02", Category = "Taolu", Description = "Grand saut tournant", MvtTypeId = 2, MvtGroupId = 1, Value = 0.3 },
        new MvtCodeModel { Id = 3, Code = "03", Category = "Taolu", Description = "Equilibre sur une jambe", MvtTypeId = 1, MvtGroupId = 2, Value = 0.2 },
        new MvtCodeModel { Id = 4, Code = "04", Category = "Taolu", Description = "Balayage arrière 360", MvtTypeId = 2, MvtGroupId = 3, Value = 0.3 }
    };
}
