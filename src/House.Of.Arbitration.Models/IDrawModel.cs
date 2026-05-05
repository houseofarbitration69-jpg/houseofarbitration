namespace House.Of.Arbitration.Models;

public interface IDrawModel
{
    /// <summary>
    /// Obtient ou définit l'identifiant du tirage en mode 'Pools'
    /// </summary>
    int Id { get; set; }

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
    DrawModel? Draw { get; }

    /// <summary>
    /// Obtient ou définit si le tirage est terminé
    /// </summary>
    bool IsFinished { get; set; }
}
