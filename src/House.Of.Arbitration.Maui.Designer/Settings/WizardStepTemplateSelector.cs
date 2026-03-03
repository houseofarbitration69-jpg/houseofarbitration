using House.Of.Arbitration.Maui.Designer.Settings;

namespace House.Of.Arbitration.Maui.Designer.Settings;

public class WizardStepTemplateSelector : DataTemplateSelector
{
    public required DataTemplate CreateCompetitionStepTemplate { get; set; }
    public required DataTemplate CategoriesStepTemplate { get; set; }
    public required DataTemplate UserStepTemplate { get; set; }
    public required DataTemplate TermsStepTemplate { get; set; }
    public required DataTemplate SummaryStepTemplate { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            CompetitionStepViewModel => CreateCompetitionStepTemplate,
            CategoriesStepViewModel => CategoriesStepTemplate,
            UserStepViewModel => UserStepTemplate,
            TermsStepViewModel => TermsStepTemplate,
            SummaryStepViewModel => SummaryStepTemplate,
            _ => null
        };
    }
}