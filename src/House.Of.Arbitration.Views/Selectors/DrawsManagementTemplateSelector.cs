using House.Of.Arbitration.ViewModels.Competition;

namespace House.Of.Arbitration.Views.Selectors;

public class DrawsManagementTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? MatchTemplate { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            DrawCategoryHeaderItem => HeaderTemplate,
            DrawMatchItemViewModel => MatchTemplate,
            _ => null
        };
    }
}
