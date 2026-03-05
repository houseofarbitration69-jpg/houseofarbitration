#region Imports
using House.Of.Arbitration.Models;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CategoriesStepViewModel : WizardStepViewModel<CompetitionModel>
{
    #region Attributs
    private List<CategoryModel> _categories = new();
    #endregion

    #region Properties
    public override string Title => String.Empty;

    public List<CategoryModel> Categories
    {
        get => _categories;
        set
        {
            SetProperty(ref _categories, value);

            if (Model != null)
            {
                Model.Categories = value;
            }

            Validate();
        }
    }
    #endregion

    #region Override Methods
    protected override void OnModelUpdated(Models.CompetitionModel value)
    {
        if (value != null)
        {
            Categories = value.Categories;
            Validate();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 
    /// </summary>
    private void Validate()
    {
        IsValid = Categories != null && Categories.Count > 0;
    }
    #endregion
}
