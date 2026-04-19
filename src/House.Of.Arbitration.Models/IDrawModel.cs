namespace House.Of.Arbitration.Models;

public interface IDrawModel
{
    /// <summary>
    /// Obtient l'ordre du tirage
    /// </summary>
    int GlobalOrder { get; set; }

    /// <summary>
    /// Obtient le type de round
    /// </summary>
    RoundType Type { get; }

    /// <summary>
    /// Obtient le tirage
    /// </summary>
    DrawModel Draw { get; }
}
