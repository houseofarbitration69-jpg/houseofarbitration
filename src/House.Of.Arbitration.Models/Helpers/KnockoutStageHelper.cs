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
    /// Détermine l'ordre de priorité chronologique de déroulement d'une phase de combat éliminatoire.
    /// Règle officielle : toutes les 32èmes de finale de l'ensemble des combats, puis toutes les 16èmes,
    /// toutes les 8èmes, les quarts, les demi-finales, les petites finales et enfin les finales.
    /// </summary>
    /// <param name="stageName">Nom de la phase (ex: "32ème de finale 1", "Quart de finale 2", "Finale").</param>
    /// <param name="isBronzeMatch">Indique s'il s'agit du combat pour la 3ème place (petite finale).</param>
    /// <returns>Un entier représentant la priorité (plus petit = plus tôt dans le tournoi).</returns>
    public static int GetStagePhasePriority(string? stageName, bool isBronzeMatch = false)
    {
        if (isBronzeMatch)
        {
            return 70;
        }

        if (string.IsNullOrWhiteSpace(stageName))
        {
            return 50;
        }

        string s = stageName.Trim();

        if (s.StartsWith("Finale", StringComparison.OrdinalIgnoreCase))
        {
            return 80;
        }

        if (s.Contains("petite", StringComparison.OrdinalIgnoreCase))
        {
            return 70;
        }

        if (s.Contains("Demi", StringComparison.OrdinalIgnoreCase))
        {
            return 60;
        }

        if (s.Contains("Quart", StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        if (s.Contains("8ème", StringComparison.OrdinalIgnoreCase) || s.Contains("8eme", StringComparison.OrdinalIgnoreCase))
        {
            return 40;
        }

        if (s.Contains("16ème", StringComparison.OrdinalIgnoreCase) || s.Contains("16eme", StringComparison.OrdinalIgnoreCase))
        {
            return 30;
        }

        if (s.Contains("32ème", StringComparison.OrdinalIgnoreCase) || s.Contains("32eme", StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        if (s.Contains("64ème", StringComparison.OrdinalIgnoreCase) || s.Contains("64eme", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("préliminaire", StringComparison.OrdinalIgnoreCase) || s.Contains("preliminaire", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        return 50;
    }

    /// <summary>
    /// Détermine l'ordre de priorité chronologique de déroulement pour un combat DrawKnockoutModel.
    /// </summary>
    public static int GetStagePhasePriority(DrawKnockoutModel? match, int totalMatches = 0)
    {
        if (match == null) return 50;
        if (match.IsBronzeMatch) return 70;

        string? stage = match.StageName;
        if (string.IsNullOrWhiteSpace(stage) || stage.StartsWith("Combat", StringComparison.OrdinalIgnoreCase))
        {
            var nonBronze = match.Draw?.DrawKnockouts?
                .Where(k => !k.IsBronzeMatch)
                .OrderBy(k => k.Order)
                .ToList();

            if (nonBronze != null && nonBronze.Count > 0)
            {
                int rank = nonBronze.IndexOf(match) + 1;
                if (rank > 0)
                {
                    stage = GetStageName(rank, nonBronze.Count, false);
                }
            }
            else if (match.Order > 0 && totalMatches > 0)
            {
                stage = GetStageName(match.Order, totalMatches, false);
            }
        }

        return GetStagePhasePriority(stage, match.IsBronzeMatch);
    }

    /// <summary>
    /// Indique si un combat est une exemption / bye du premier tour (un seul compétiteur).
    /// Ces combats ne se déroulent pas réellement sur le tapis et ne doivent pas recevoir de numéro de combat.
    /// </summary>
    public static bool IsByeMatch(DrawKnockoutModel? match)
    {
        if (match == null) return false;
        if (match.IsBye) return true;

        bool hasComp1 = match.Competitor1Id.HasValue || match.Competitor1 != null;
        bool hasComp2 = match.Competitor2Id.HasValue || match.Competitor2 != null;

        return (hasComp1 ^ hasComp2) && (match.IsFinished || match.GlobalOrder == 0);
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

    /// <summary>
    /// Calcule la priorité de tri d'une catégorie :
    /// 1. Femmes en premier (Genre.Women = 0, Mixte = 1, Men = 2, autre = 3).
    /// 2. Par ordre croissant d'âge (AgeRange.MinAge).
    /// </summary>
    public static (int GenderPriority, int MinAge, int CategoryId) GetCategoryPriority(CategoryModel? category, int fallbackId = 0)
    {
        if (category == null) return (3, int.MaxValue, fallbackId);

        int genderPriority = category.Genre switch
        {
            Genre.Women => 0,
            Genre.Mixte => 1,
            Genre.Men   => 2,
            _           => 3
        };

        int minAge = category.AgeRange?.MinAge ?? (category.AgeRangeId switch
        {
            1 => 12, // Cadets
            2 => 15, // Juniors
            4 => 18, // Espoirs
            3 => 18, // Seniors
            5 => 35, // Veterans
            _ => int.MaxValue / 2
        });

        return (genderPriority, minAge, category.Id != 0 ? category.Id : fallbackId);
    }

    /// <summary>
    /// Ordonne l'ensemble des combats d'un tournoi selon les règles officielles :
    /// 1. Les matches du premier tour sans adversaire (Byes / Exemptions) ne sont pas combattus.
    /// 2. Structure en deux grandes sections :
    ///    - Section préliminaire (phases &lt; 70 : 32èmes, 16èmes, 8èmes, quarts, demis + poules + ordre de passage) :
    ///      Tri par (roundIndex, catégorie, ordre local).
    ///    - Section finale (phases ≥ 70 : petites finales et finales, y compris les finales directes des catégories à 2 compétiteurs) :
    ///      Toujours en toute fin de tournoi. Femmes en premier au sein de chaque type de phase.
    /// 3. Au sein de la section préliminaire, les matchs KO sont ordonnés par leur priorité de phase absolue
    ///    (64èmes=10, 32èmes=20, 16èmes=30, 8èmes=40, Quarts=50, Demis=60) afin qu'aucune demi-finale ne passe
    ///    jamais avant un quart de finale d'une autre catégorie.
    ///    Pour les poules : rang = tour du match dans la catégorie (match 1 → 10, match 2 → 20, ...).
    /// 4. Au sein de chaque (phase, catégorie), TOUS les matchs sont consécutifs (localOrder croissant).
    ///    Ex : tous les quarts de finale de C1 se déroulent à la suite, puis tous les quarts de C2.
    /// 5. getCategoryId doit retourner un rang pré-calculé avec Femmes en premier (valeur plus petite = plus prioritaire).
    /// </summary>
    public static List<T> ScheduleTournamentMatches<T>(
        IEnumerable<T> allMatches,
        Func<T, RoundType> getRoundType,
        Func<T, int> getCategoryId,
        Func<T, int> getLocalOrder,
        Func<T, int> getStagePhasePriority,
        Func<T, bool> isBye)
    {
        var rawList = allMatches?.ToList() ?? new List<T>();
        if (rawList.Count == 0) return rawList;

        var byes = new List<T>();
        var activeMatches = new List<T>();
        foreach (var m in rawList)
        {
            if (isBye(m)) byes.Add(m); else activeMatches.Add(m);
        }

        int n = activeMatches.Count;
        if (n == 0) return byes;

        // --- Séparation : finales (≥ 70) vs préliminaires (< 70 + poules + ordre) ---
        // Les Finales directes (catégorie à 2 compétiteurs, 1 seul match = Finale) ont priorité 80 ≥ 70
        // → elles vont dans la section finale, à la fin du tournoi.
        const int finalsThreshold = 70;

        var finalsMatches = new List<T>();
        var prelimMatches = new List<T>();

        foreach (var m in activeMatches)
        {
            if (getRoundType(m) == RoundType.Knockouts && getStagePhasePriority(m) >= finalsThreshold)
                finalsMatches.Add(m);
            else
                prelimMatches.Add(m);
        }

        // ---- Section préliminaire : tri par (phasePriority, catégorie, ordre local) ----
        int np = prelimMatches.Count;
        var roundIndices = new int[np];

        // KO prelims (priority < 70) : priorité de phase absolue (10, 20, 30, 40, 50=Quarts, 60=Demis)
        // Garantit que tous les quarts de toutes les catégories passent avant toutes les demi-finales.
        for (int i = 0; i < np; i++)
        {
            if (getRoundType(prelimMatches[i]) == RoundType.Knockouts)
            {
                roundIndices[i] = getStagePhasePriority(prelimMatches[i]);
            }
        }

        // Poules : rang dans le calendrier de la catégorie (match 1 → 10, match 2 → 20, ...)
        var poolIndicesByCat = new Dictionary<int, List<int>>();
        for (int i = 0; i < np; i++)
        {
            if (getRoundType(prelimMatches[i]) == RoundType.Pools)
            {
                int catId = getCategoryId(prelimMatches[i]);
                if (!poolIndicesByCat.TryGetValue(catId, out var lst))
                    poolIndicesByCat[catId] = lst = new List<int>();
                lst.Add(i);
            }
        }

        foreach (var (_, indices) in poolIndicesByCat)
        {
            int rank = 1;
            foreach (int i in indices.OrderBy(i => getLocalOrder(prelimMatches[i])))
                roundIndices[i] = (rank++) * 10;
        }

        // Ordre de passage (Taolu) : ordre local = numéro de tour
        for (int i = 0; i < np; i++)
        {
            if (getRoundType(prelimMatches[i]) == RoundType.Order)
                roundIndices[i] = getLocalOrder(prelimMatches[i]);
        }

        var prelimScheduled = prelimMatches
            .Select((m, i) => (Match: m, RoundIndex: roundIndices[i]))
            .OrderBy(x => x.RoundIndex)
            .ThenBy(x => getCategoryId(x.Match))
            .ThenBy(x => getLocalOrder(x.Match))
            .Select(x => x.Match)
            .ToList();

        // ---- Section finale : petites finales (70) puis finales (80) ----
        // Au sein de chaque sous-section, Femmes en premier (getCategoryId croissant).
        var finalsScheduled = finalsMatches
            .OrderBy(m => getStagePhasePriority(m))  // 70 (petites finales) avant 80 (finales)
            .ThenBy(m => getCategoryId(m))            // Femmes en premier
            .ThenBy(m => getLocalOrder(m))
            .ToList();

        var result = new List<T>(n + byes.Count);
        result.AddRange(prelimScheduled);
        result.AddRange(finalsScheduled);
        result.AddRange(byes);
        return result;
    }

    /// <summary>
    /// Ordonne l'ensemble des tirages (IDrawModel) d'une compétition selon les règles de tournoi.
    /// Les catégories sont ordonnées avec priorité aux femmes, puis par âge croissant.
    /// </summary>
    public static List<T> ScheduleTournamentDraws<T>(IEnumerable<T> draws) where T : IDrawModel
    {
        var drawsList = draws?.ToList() ?? new List<T>();
        if (drawsList.Count == 0) return drawsList;

        // Pré-calculer l'ordre de priorité des catégories (Femmes en premier, puis âge croissant)
        var categories = drawsList
            .Select(d => d.Draw?.Category)
            .Where(c => c != null)
            .DistinctBy(c => c!.Id)
            .OrderBy(c => GetCategoryPriority(c).GenderPriority)
            .ThenBy(c => GetCategoryPriority(c).MinAge)
            .ThenBy(c => c!.Id)
            .ToList();

        var catRank = new Dictionary<int, int>();
        for (int i = 0; i < categories.Count; i++)
        {
            catRank[categories[i]!.Id] = i;
        }

        // Pré-calculer la priorité de phase pour chaque DrawKnockoutModel en se basant sur
        // l'ensemble des matchs non-bronze du même tableau (même si Draw.DrawKnockouts n'est pas chargé par EF Core).
        var koPhasePriority = new Dictionary<DrawKnockoutModel, int>();
        var koGroups = drawsList
            .OfType<DrawKnockoutModel>()
            .GroupBy(k => k.Draw?.CategoryId ?? k.DrawId);

        foreach (var group in koGroups)
        {
            var nonBronze = group
                .Where(k => !k.IsBronzeMatch)
                .OrderBy(k => k.Order)
                .ToList();

            foreach (var ko in group)
            {
                if (ko.IsBronzeMatch)
                {
                    koPhasePriority[ko] = 70;
                    continue;
                }

                // Si Draw.DrawKnockouts est déjà complet et donne une phase explicite, l'utiliser ;
                // sinon calculer à partir du groupe non-bronze de drawsList.
                if (ko.Draw?.DrawKnockouts != null && ko.Draw.DrawKnockouts.Count >= group.Count())
                {
                    koPhasePriority[ko] = GetStagePhasePriority(ko);
                }
                else if (nonBronze.Count > 0)
                {
                    int rank = nonBronze.IndexOf(ko) + 1;
                    string stage = rank > 0
                        ? GetStageName(rank, nonBronze.Count, false)
                        : GetStageName(ko.Order, nonBronze.Count, false);
                    koPhasePriority[ko] = GetStagePhasePriority(stage, false);
                }
                else
                {
                    koPhasePriority[ko] = GetStagePhasePriority(ko);
                }
            }
        }

        return ScheduleTournamentMatches(
            drawsList,
            d => d.Type,
            d => d.Draw?.CategoryId != null && catRank.TryGetValue(d.Draw.CategoryId, out int rank) ? rank : (d.Draw?.CategoryId ?? 0),
            d => d switch
            {
                DrawKnockoutModel ko => ko.Order,
                DrawPoolsModel pool => pool.Order,
                DrawOrderModel order => order.Order,
                _ => 0
            },
            d => d switch
            {
                DrawKnockoutModel ko => koPhasePriority.TryGetValue(ko, out int prio) ? prio : GetStagePhasePriority(ko),
                _ => 0
            },
            d => d is DrawKnockoutModel ko && IsByeMatch(ko)
        );
    }

}
