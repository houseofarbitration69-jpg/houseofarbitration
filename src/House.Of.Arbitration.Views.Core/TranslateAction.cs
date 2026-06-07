namespace House.Of.Arbitration.Views.Core;

public class TranslateAction : TriggerAction<VisualElement>
{
    public double X { get; set; }
    public double Y { get; set; }
    public uint Duration { get; set; } = 250;
    public Easing Easing { get; set; } = Easing.Linear;

    protected override async void Invoke(VisualElement sender)
    {
        if (sender == null) return;
        await sender.TranslateTo(X, Y, Duration, Easing);
    }
}
