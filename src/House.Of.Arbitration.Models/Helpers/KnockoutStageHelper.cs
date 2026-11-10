namespace House.Of.Arbitration.Models.Helpers;

/// <summary>
/// Fournit des méthodes pour calculer et formater les noms des phases / tours de tableau éliminatoire (Sanda).
/// </summary>
public static class KnockoutStageHelper
{
    /// <summary>
    /// Calcule le nom de l'étape pour un match donné dans un arbre éliminatoire (ex: "Finale", "Demi-finale 1", "Quart de finale 2").
    /// </summary>
    /// <param name="order">Ordre du match dans la catégorie (1-based).</param>
    /// <param name="totalMatches">Nombre total de matches dans l'arbre pour cette catégorie.</param>
    /// <param name="includeIndex">Indique s'il faut inclure le numéro du match dans la phase (ex: Demi-finale 1).</param>
    /// <returns>Le nom de la phase d'élimination.</returns>
    public static string GetStageName(int order, int totalMatches = 0, bool includeIndex = true)
    {
        if (order <= 0)
        {
            return "Combat";
        }

        // Déterminer la taille totale normalisée de l'arbre (2^k - 1)
        int effectiveTotal = totalMatches > 0 ? totalMatches : order;
        int m = 1;
        while (m - 1 < Math.Max(effectiveTotal, order))
        {
            m *= 2;
        }
        int bracketTotal = m - 1;

        int remainingFromEnd = bracketTotal - order + 1;

        if (remainingFromEnd <= 1)
        {
            return "Finale";
        }

        if (remainingFromEnd <= 3)
        {
            int index = order - (bracketTotal - 3);
            return includeIndex ? $"Demi-finale {index}" : "Demi-finale";
        }

        if (remainingFromEnd <= 7)
        {
            int index = order - (bracketTotal - 7);
            return includeIndex ? $"Quart de finale {index}" : "Quart de finale";
        }

        if (remainingFromEnd <= 15)
        {
            int index = order - (bracketTotal - 15);
            return includeIndex ? $"8ème de finale {index}" : "8ème de finale";
        }

        if (remainingFromEnd <= 31)
        {
            int index = order - (bracketTotal - 31);
            return includeIndex ? $"16ème de finale {index}" : "16ème de finale";
        }

        if (remainingFromEnd <= 63)
        {
            int index = order - (bracketTotal - 63);
            return includeIndex ? $"32ème de finale {index}" : "32ème de finale";
        }

        return $"Tour préliminaire {order}";
    }

    /// <summary>
    /// Obtient le nom du tour (colonne de tableau éliminatoire) selon le nombre de matches qu'il contient.
    /// </summary>
    /// <param name="matchesInRound">Nombre de combats dans le tour.</param>
    /// <returns>Le nom du tour au pluriel ou singulier (ex: "Demi-finales", "Finale").</returns>
    public static string GetRoundName(int matchesInRound)
    {
        return matchesInRound switch
        {
            1 => "Finale",
            2 => "Demi-finales",
            4 => "Quarts de finale",
            8 => "8èmes de finale",
            16 => "16èmes de finale",
            32 => "32èmes de finale",
            _ => $"Tour ({matchesInRound} matches)"
        };
    }
}
