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

        if (totalMatches <= 0)
        {
            return $"Combat {order}";
        }

        // Déterminer la taille totale normalisée de l'arbre (2^k - 1)
        int effectiveTotal = totalMatches;
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

    /// <summary>
    /// Distribue les compétiteurs dans les matches du premier tour d'un tableau éliminatoire (knockout).
    /// Garantit qu'aucun combat du premier tour n'a 2 combattants inconnus (chaque combat a au moins 1 combattant).
    /// </summary>
    /// <param name="competitors">Liste des compétiteurs engagés dans la catégorie.</param>
    /// <param name="matchesInRound">Nombre de combats dans le premier tour (m / 2).</param>
    /// <returns>Une liste de paires (Compétiteur 1, Compétiteur 2 ou null si bye/exempt).</returns>
    public static List<(CompetitorModel Comp1, CompetitorModel? Comp2)> DistributeFirstRoundCompetitors(
        IList<CompetitorModel> competitors,
        int matchesInRound)
    {
        var result = new List<(CompetitorModel Comp1, CompetitorModel? Comp2)>();
        if (competitors == null || competitors.Count == 0 || matchesInRound <= 0)
        {
            return result;
        }

        int n = competitors.Count;
        int m = matchesInRound * 2;
        int numByes = Math.Max(0, m - n);

        // Déterminer quels matches sont des exemptions (Byes)
        // Les exemptions sont réparties équitablement entre les moitiés haute et basse du tableau
        var isBye = new bool[matchesInRound];
        if (numByes > 0)
        {
            var byeOrder = GetByePriorityOrder(matchesInRound);
            for (int b = 0; b < Math.Min(numByes, matchesInRound); b++)
            {
                isBye[byeOrder[b]] = true;
            }
        }

        // Assigner les compétiteurs :
        // Chaque match i a toujours Comp1 (au moins 1 combattant).
        // Si le match i n'est pas une exemption, il a également Comp2.
        int compIdx = 0;
        for (int i = 0; i < matchesInRound; i++)
        {
            if (compIdx >= n)
            {
                break;
            }

            if (isBye[i])
            {
                // Match avec 1 seul compétiteur (Exempt / Bye)
                var comp1 = competitors[compIdx++];
                result.Add((comp1, null));
            }
            else
            {
                // Match complet avec 2 compétiteurs
                var comp1 = competitors[compIdx++];
                CompetitorModel? comp2 = (compIdx < n) ? competitors[compIdx++] : null;
                result.Add((comp1, comp2));
            }
        }

        return result;
    }

    /// <summary>
    /// Ordre de priorité des positions d'exemptions (Byes) pour équilibrer l'arbre (alternance haut / bas).
    /// </summary>
    public static int[] GetByePriorityOrder(int matchesInRound)
    {
        if (matchesInRound <= 1) return new[] { 0 };
        if (matchesInRound == 2) return new[] { 0, 1 };
        if (matchesInRound == 4) return new[] { 0, 3, 1, 2 };

        var list = new List<int>();
        void AddRecursive(int start, int end)
        {
            if (start == end)
            {
                list.Add(start);
                return;
            }
            list.Add(start);
            list.Add(end);
            int mid = (start + end) / 2;
            if (mid > start) AddRecursive(start + 1, mid);
            if (end > mid + 1) AddRecursive(mid + 1, end - 1);
        }

        AddRecursive(0, matchesInRound - 1);
        return list.Distinct().Take(matchesInRound).ToArray();
    }
}
