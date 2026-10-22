using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Views.Selectors;

public class DrawTemplateSelector : DataTemplateSelector
{
    public DataTemplate? KnockoutTemplate { get; set; }
    public DataTemplate? PoolTemplate { get; set; }
    public DataTemplate? OrderTemplate { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }

    /// <summary>
    /// Selectionne le template approprie selon le type d'element du tirage.
    /// Retourne HeaderTemplate plutot que null pour les types inconnus afin d'eviter
    /// les crashes de recyclage de cellule sur Android (TargetException / ValidateInvokeTarget).
    /// </summary>
    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
    {
        if (item is string)
            return HeaderTemplate;

        return item switch
        {
            DrawKnockoutModel => KnockoutTemplate,
            DrawPoolsModel => PoolTemplate,
            DrawOrderModel => OrderTemplate,
            // Retourne HeaderTemplate (template vide) plutot que null pour eviter le recyclage
            // sur un type inattendu (protege contre le crash TargetException Android)
            _ => HeaderTemplate
        };
    }
}
