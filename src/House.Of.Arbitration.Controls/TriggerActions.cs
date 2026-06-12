using Microsoft.Maui.Controls;

namespace House.Of.Arbitration.Controls;

[System.AttributeUsage(System.AttributeTargets.All)]
public sealed class PreserveAttribute : System.Attribute
{
    public bool AllMembers { get; set; }
    public bool Conditional { get; set; }
}

[Preserve(AllMembers = true)]
public class FadeAction : TriggerAction<VisualElement>
{
    public double Opacity { get; set; }
    public uint Duration { get; set; } = 250;

    protected override async void Invoke(VisualElement sender)
    {
        if (sender == null) return;
        await sender.FadeToAsync(Opacity, Duration);
    }
}

[Preserve(AllMembers = true)]
public class TranslateAction : TriggerAction<VisualElement>
{
    public double X { get; set; }
    public double Y { get; set; }
    public uint Duration { get; set; } = 250;
    public Easing Easing { get; set; } = Easing.Linear;

    protected override async void Invoke(VisualElement sender)
    {
        if (sender == null) return;
        await sender.TranslateToAsync(X, Y, Duration, Easing);
    }
}
