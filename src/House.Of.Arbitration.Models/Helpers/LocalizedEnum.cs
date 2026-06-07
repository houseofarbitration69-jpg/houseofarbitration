namespace House.Of.Arbitration.Models.Helpers;

/// <summary>
/// Wrapper pour lier une valeur d'énumération à sa traduction.
/// </summary>
/// <typeparam name="T">Le type de l'énumération.</typeparam>
public class LocalizedEnum<T> where T : Enum
{
    public T Value { get; set; }
    public string DisplayName { get; set; }

    public LocalizedEnum(T value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}
