#region Imports
using House.Of.Arbitration.Models.Helpers;
#endregion

namespace House.Of.Arbitration.Controls;

public class IconInfo
{
    public string FontFamily { get; set; } = FontHelper.CUSTOM_NAME;
    public string Icon { get; set; } = string.Empty;
    public double FontSize { get; set; } = 18;
    public Color Color { get; set; } = Colors.White;
}
