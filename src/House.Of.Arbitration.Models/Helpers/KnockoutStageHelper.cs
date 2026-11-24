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
        if (string.IsNullOrWhiteSpace(stage) || stage == "Combat")
        {
            int total = totalMatches > 0 ? totalMatches : (match.Draw?.DrawKnockouts?.Count ?? 0);
            if (match.Order > 0 && total > 0)
            {
                stage = GetStageName(match.Order, total, false);
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
    /// Ordonne l'ensemble des combats d'un tournoi selon les règles officielles :
    /// 1. Les matches du premier tour sans adversaire (Byes / Exemptions) ne sont pas combattus (GlobalOrder = 0).
    /// 2. Les catégories en éliminatoire sont ordonnées par phase chronologique (32èmes, 16èmes, 8èmes, quarts, demis),
    ///    en alternant/mélangeant les catégories au sein de chaque phase.
    /// 3. Les matches de poules sont mélangés avec les combats éliminatoires préliminaires,
    ///    avec un espacement d'au moins 3 ou 4 combats entre deux matches d'une même poule.
    /// 4. Les petites finales et finales de toutes les catégories se déroulent à la toute fin du tournoi.
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
        if (rawList.Count == 0) return new List<T>();

        var byes = new List<T>();
        var activeMatches = new List<T>();

        foreach (var m in rawList)
        {
            if (isBye(m))
            {
                byes.Add(m);
            }
            else
            {
                activeMatches.Add(m);
            }
        }

        // 1. Finales et Petites finales éliminatoires (Priorité >= 70 : 70 = Petites finales, 80 = Finales)
        var finals = activeMatches
            .Where(m => getRoundType(m) == RoundType.Knockouts && getStagePhasePriority(m) >= 70)
            .OrderBy(m => getStagePhasePriority(m)) // 70 d'abord (petites finales), puis 80 (finales)
            .ThenBy(m => getCategoryId(m))
            .ThenBy(m => getLocalOrder(m))
            .ToList();

        // 2. Combats éliminatoires préliminaires (Priorité < 70 : 32èmes, 16èmes, 8èmes, quarts, demis)
        var koPrelims = activeMatches
            .Where(m => getRoundType(m) == RoundType.Knockouts && getStagePhasePriority(m) < 70)
            .ToList();

        // Mélange des catégories au sein de chaque phase éliminatoire (round-robin entre catégories)
        var orderedKoPrelims = InterleaveKnockoutPhases(koPrelims, getStagePhasePriority, getCategoryId, getLocalOrder);

        // 3. Matches de poules (groupés par catégorie, ordonnés par ordre local)
        var poolMatches = activeMatches
            .Where(m => getRoundType(m) == RoundType.Pools)
            .ToList();

        var poolQueues = poolMatches
            .GroupBy(m => getCategoryId(m))
            .ToDictionary(g => g.Key, g => new Queue<T>(g.OrderBy(m => getLocalOrder(m))));

        // 4. Matches d'ordre de passage (Taolu / passages individuels)
        var orderMatches = new Queue<T>(activeMatches
            .Where(m => getRoundType(m) == RoundType.Order)
            .OrderBy(m => getCategoryId(m))
            .ThenBy(m => getLocalOrder(m)));

        // 5. Entrelacement des poules et des éliminatoires préliminaires
        // Règle : espacer les combats d'une même poule d'au moins 3 ou 4 combats
        var prelimScheduled = InterleavePoolsAndPrelims(orderedKoPrelims, poolQueues, orderMatches);

        // 6. Assemblage final : Préliminaires entrelacés + Petites finales & Finales + Byes (non combattus)
        var finalSchedule = new List<T>(rawList.Count);
        finalSchedule.AddRange(prelimScheduled);
        finalSchedule.AddRange(finals);
        finalSchedule.AddRange(byes);

        return finalSchedule;
    }

    /// <summary>
    /// Ordonne l'ensemble des tirages (IDrawModel) d'une compétition selon les règles de tournoi.
    /// </summary>
    public static List<T> ScheduleTournamentDraws<T>(IEnumerable<T> draws) where T : IDrawModel
    {
        return ScheduleTournamentMatches(
            draws,
            d => d.Type,
            d => d.Draw?.CategoryId ?? 0,
            d => d switch
            {
                DrawKnockoutModel ko => ko.Order,
                DrawPoolsModel pool => pool.Order,
                DrawOrderModel order => order.Order,
                _ => 0
            },
            d => d switch
            {
                DrawKnockoutModel ko => GetStagePhasePriority(ko),
                _ => 0
            },
            d => d is DrawKnockoutModel ko && IsByeMatch(ko)
        );
    }

    private static List<T> InterleaveKnockoutPhases<T>(
        List<T> koMatches,
        Func<T, int> getStagePhasePriority,
        Func<T, int> getCategoryId,
        Func<T, int> getLocalOrder)
    {
        var result = new List<T>(koMatches.Count);

        // Regrouper par phase éliminatoire (ordre chronologique strict : 10, 20, 30, 40, 50, 60)
        var phases = koMatches
            .GroupBy(m => getStagePhasePriority(m))
            .OrderBy(g => g.Key);

        foreach (var phaseGroup in phases)
        {
            // Au sein d'une même phase : tous les matches de C1 ensemble, puis tous ceux de C2, etc.
            // (Les poules seront intercalées entre ces blocs par InterleavePoolsAndPrelims)
            var catGroups = phaseGroup
                .GroupBy(m => getCategoryId(m))
                .OrderBy(g => g.Key);

            foreach (var catGroup in catGroups)
            {
                foreach (var m in catGroup.OrderBy(m => getLocalOrder(m)))
                {
                    result.Add(m);
                }
            }
        }

        return result;
    }

    private static List<T> InterleavePoolsAndPrelims<T>(
        List<T> orderedKoPrelims,
        Dictionary<int, Queue<T>> poolQueues,
        Queue<T> orderMatches)
    {
        var koQueue = new Queue<T>(orderedKoPrelims);
        int totalPoolMatches = poolQueues.Values.Sum(q => q.Count);
        int totalOtherMatches = koQueue.Count + orderMatches.Count;
        int totalToSchedule = totalPoolMatches + totalOtherMatches;

        var result = new List<T>(totalToSchedule);
        if (totalToSchedule == 0) return result;

        var lastPlacedIndex = new Dictionary<int, int>();

        const int minSpacing = 3;    // Minimum 3 combats entre deux matches de la même poule
        const int targetSpacing = 4; // Cible recommandée : 4 combats d'espacement

        for (int i = 0; i < totalToSchedule; i++)
        {
            var activePools = poolQueues.Where(kvp => kvp.Value.Count > 0).ToList();
            int remainingOther = koQueue.Count + orderMatches.Count;

            int? selectedPoolCatId = null;

            if (activePools.Count > 0)
            {
                var candidates = new List<(int CatId, int Distance, bool IsCritical, int RemInPool)>();

                foreach (var kvp in activePools)
                {
                    int catId = kvp.Key;
                    int remInPool = kvp.Value.Count;
                    int otherAvailable = remainingOther + activePools.Where(p => p.Key != catId).Sum(p => p.Value.Count);

                    int dist = lastPlacedIndex.TryGetValue(catId, out int lastIdx)
                        ? (i - lastIdx - 1)
                        : (int.MaxValue / 2);

                    bool isEligible = dist >= minSpacing;
                    bool isCritical = remInPool > 1 && otherAvailable <= (remInPool - 1) * minSpacing;

                    candidates.Add((catId, dist, isCritical, remInPool));
                }

                // 1. Si une poule est critique et éligible, la planifier en priorité pour préserver l'espacement futur
                var criticalCandidates = candidates.Where(c => c.Distance >= minSpacing && c.IsCritical).ToList();
                if (criticalCandidates.Count > 0)
                {
                    selectedPoolCatId = criticalCandidates
                        .OrderByDescending(c => c.RemInPool)
                        .ThenByDescending(c => c.Distance)
                        .First().CatId;
                }
                else
                {
                    // 2. Poules prêtes pour leur tour normal (distance >= targetSpacing ou espacement idéal)
                    var readyCandidates = candidates.Where(c =>
                    {
                        if (c.Distance < minSpacing) return false;
                        if (c.Distance >= targetSpacing) return true;

                        int otherAvailable = remainingOther + activePools.Where(p => p.Key != c.CatId).Sum(p => p.Value.Count);
                        int idealSpacing = c.RemInPool > 0 ? (otherAvailable + c.RemInPool) / c.RemInPool : targetSpacing;
                        return c.Distance >= Math.Max(minSpacing, idealSpacing);
                    }).ToList();

                    if (readyCandidates.Count > 0)
                    {
                        // Priorité à la poule qui a attendu le plus longtemps
                        selectedPoolCatId = readyCandidates
                            .OrderByDescending(c => c.Distance)
                            .ThenByDescending(c => c.RemInPool)
                            .First().CatId;
                    }
                    else if (remainingOther == 0)
                    {
                        // Plus d'autres matches disponibles : prendre la poule la plus reposée
                        selectedPoolCatId = candidates
                            .OrderByDescending(c => c.Distance)
                            .ThenByDescending(c => c.RemInPool)
                            .First().CatId;
                    }
                }
            }

            if (selectedPoolCatId.HasValue)
            {
                var match = poolQueues[selectedPoolCatId.Value].Dequeue();
                result.Add(match);
                lastPlacedIndex[selectedPoolCatId.Value] = i;
            }
            else if (koQueue.Count > 0)
            {
                result.Add(koQueue.Dequeue());
            }
            else if (orderMatches.Count > 0)
            {
                result.Add(orderMatches.Dequeue());
            }
            else if (activePools.Count > 0)
            {
                var fallbackCatId = activePools.OrderByDescending(p =>
                    lastPlacedIndex.TryGetValue(p.Key, out int lastIdx) ? (i - lastIdx) : int.MaxValue
                ).First().Key;
                var match = poolQueues[fallbackCatId].Dequeue();
                result.Add(match);
                lastPlacedIndex[fallbackCatId] = i;
            }
        }

        return result;
    }
}
