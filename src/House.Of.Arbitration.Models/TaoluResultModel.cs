namespace House.Of.Arbitration.Models;

/// <summary>
/// Modèle représentant le résultat détaillé d'un passage en Taolu (note finale et notes individuelles des juges).
/// </summary>
public class TaoluResultModel
{
    /// <summary>
    /// Note finale attribuée à l'athlète.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Liste des notes attribuées par chaque juge pour ce passage.
    /// </summary>
    public List<double> JudgeScores { get; set; } = new();

    /// <summary>
    /// Dictionnaire associant le nom/poste du juge à sa note.
    /// </summary>
    public Dictionary<string, double> JudgeScoresByJudge { get; set; } = new();

    /// <summary>
    /// Motif ou détail du calcul de la note.
    /// </summary>
    public string? DecisionReason { get; set; }
}
