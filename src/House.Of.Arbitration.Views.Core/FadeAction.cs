namespace House.Of.Arbitration.Views.Core;

public class FadeAction : TriggerAction<VisualElement>
{
    public double Opacity { get; set; }
    public uint Duration { get; set; } = 250;

    protected override async void Invoke(VisualElement sender)
    {
        if (sender == null) return;
        await sender.FadeTo(Opacity, Duration);
    }
}
