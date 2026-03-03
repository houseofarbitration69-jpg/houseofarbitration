using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

namespace House.Of.Arbitration.Views.Wizard.Competition;

public class StepTemplateSelector : DataTemplateSelector
{
    public required DataTemplate SettingsStepTemplate { get; set; }
    //public required DataTemplate CategoriesStepTemplate { get; set; }
    //public required DataTemplate UserStepTemplate { get; set; }
    //public required DataTemplate TermsStepTemplate { get; set; }
    //public required DataTemplate SummaryStepTemplate { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            SettingsStepViewModel => SettingsStepTemplate,
            //CategoriesStepViewModel => CategoriesStepTemplate,
            //UserStepViewModel => UserStepTemplate,
            //TermsStepViewModel => TermsStepTemplate,
            //SummaryStepViewModel => SummaryStepTemplate,
            _ => null
        };
    }
}