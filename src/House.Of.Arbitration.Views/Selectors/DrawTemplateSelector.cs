using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Views.Selectors;

public class DrawTemplateSelector : DataTemplateSelector
{
    public DataTemplate? KnockoutTemplate { get; set; }
    public DataTemplate? PoolTemplate { get; set; }
    public DataTemplate? OrderTemplate { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            DrawKnockoutModel => KnockoutTemplate,
            DrawPoolsModel => PoolTemplate,
            DrawOrderModel => OrderTemplate,
            _ => null
        };
    }
}
