using System.Globalization;
using ClosedXML.Excel;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;

namespace House.Of.Arbitration.Server.Cli;

/// <summary>
/// Résultat du chargement d'un fichier Excel de compétition.
/// </summary>
public class ExcelLoadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public CompetitionModel? Competition { get; set; }
    public List<CategoryModel> Categories { get; set; } = new();
    public List<CompetitorModel> Competitors { get; set; } = new();
    public List<MatchItem> Matches { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Chargeur et générateur de fichiers Excel de compétition pour le serveur d'arbitrage CLI.
/// </summary>
public static class ExcelCompetitionLoader
{
    /// <summary>
    /// Charge une compétition, ses catégories, compétiteurs et génère les matchs depuis un fichier Excel (.xlsx).
    /// </summary>
    public static ExcelLoadResult LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new ExcelLoadResult
            {
                Success = false,
                ErrorMessage = "Le chemin du fichier Excel est vide."
            };
        }

        if (!File.Exists(filePath))
        {
            return new ExcelLoadResult
            {
                Success = false,
                ErrorMessage = $"Le fichier '{filePath}' est introuvable."
            };
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return LoadFromStream(stream);
        }
        catch (Exception ex)
        {
            return new ExcelLoadResult
            {
                Success = false,
                ErrorMessage = $"Erreur lors de la lecture du fichier : {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Charge une compétition depuis un flux Excel (.xlsx).
    /// </summary>
    public static ExcelLoadResult LoadFromStream(Stream stream)
    {
        var result = new ExcelLoadResult();

        try
        {
            using var workbook = new XLWorkbook(stream);

            // 1. Feuille Compétition
            var wsComp = FindWorksheet(workbook, "Compétition", "Competition");
            if (wsComp == null)
            {
                result.Success = false;
                result.ErrorMessage = "Feuille 'Compétition' manquante dans le classeur Excel.";
                return result;
            }

            string compName = wsComp.Cell(2, 1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(compName))
            {
                compName = "Compétition Wushu";
                result.Warnings.Add("Nom de compétition non spécifié, utilisation du nom par défaut.");
            }

            DateTime compDate = DateTime.Today;
            if (!TryParseCellDate(wsComp.Cell(2, 2), out compDate))
            {
                result.Warnings.Add("Date de compétition invalide ou absente, date du jour utilisée.");
                compDate = DateTime.Today;
            }

            string compTypeStr = wsComp.Cell(2, 3).GetString().Trim();
            CompetitionType compType = CompetitionType.Sanda;
            if (compTypeStr.StartsWith("t", StringComparison.OrdinalIgnoreCase))
            {
                compType = CompetitionType.Taolu;
            }
            else if (!compTypeStr.StartsWith("s", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(compTypeStr))
            {
                result.Warnings.Add($"Type de compétition '{compTypeStr}' non reconnu, Sanda appliqué par défaut.");
            }

            result.Competition = new CompetitionModel
            {
                Id = 1,
                Name = compName,
                Date = compDate,
                Type = compType
            };

            // 2. Feuille Catégories
            var wsCat = FindWorksheet(workbook, "Catégories", "Categories");
            var categoriesList = new List<CategoryModel>();
            var categoryByName = new Dictionary<string, CategoryModel>(StringComparer.OrdinalIgnoreCase);

            if (wsCat != null)
            {
                int catRow = 2;
                while (!string.IsNullOrWhiteSpace(wsCat.Cell(catRow, 1).GetString()))
                {
                    string typeStr = wsCat.Cell(catRow, 1).GetString().Trim();
                    string genreStr = wsCat.Cell(catRow, 2).GetString().Trim();
                    string weightMinStr = wsCat.Cell(catRow, 3).GetString().Trim();
                    string weightMaxStr = wsCat.Cell(catRow, 4).GetString().Trim();
                    string ageLabel = wsCat.Cell(catRow, 5).GetString().Trim();
                    string roundTypeStr = wsCat.Cell(catRow, 6).GetString().Trim();
                    string customName = wsCat.Cell(catRow, 7).GetString().Trim();
                    string bronzeStr = wsCat.Cell(catRow, 8).GetString().Trim();
                    bool hasBronzeMatch = bronzeStr.Equals("oui", StringComparison.OrdinalIgnoreCase) ||
                                          bronzeStr.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                                          bronzeStr.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                          bronzeStr.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                                          bronzeStr.Equals("vrai", StringComparison.OrdinalIgnoreCase);

                    if (!TryParseCategoryType(typeStr, out var catType))
                    {
                        catType = compType == CompetitionType.Taolu ? CategoryType.Taolu : CategoryType.Sanda;
                    }

                    if (!Enum.TryParse<Genre>(genreStr, true, out var genre))
                    {
                        genre = Genre.Men;
                    }

                    double weightMin = ParseDouble(weightMinStr);
                    double weightMax = ParseDouble(weightMaxStr);

                    if (!Enum.TryParse<RoundType>(roundTypeStr, true, out var roundType))
                    {
                        roundType = (compType == CompetitionType.Taolu || catType == CategoryType.Taolu)
                            ? RoundType.Order
                            : RoundType.Knockouts;
                    }

                    var ageRange = string.IsNullOrWhiteSpace(ageLabel)
                        ? null
                        : AgeRangeModel.DefaultRanges.FirstOrDefault(r => r.Label.Equals(ageLabel, StringComparison.OrdinalIgnoreCase));

                    string displayName = !string.IsNullOrWhiteSpace(customName)
                        ? customName
                        : BuildCategoryDisplayName(catType, genre, weightMin, weightMax, ageLabel);

                    var cat = new CategoryModel
                    {
                        Id = categoriesList.Count + 1,
                        Type = catType,
                        Genre = genre,
                        WeightMin = weightMin,
                        WeightMax = weightMax,
                        AgeRangeId = ageRange?.Id,
                        AgeRange = ageRange,
                        RoundType = roundType,
                        HasBronzeMatch = hasBronzeMatch,
                        CompetitionId = 1
                    };

                    categoriesList.Add(cat);
                    categoryByName[displayName] = cat;
                    catRow++;
                }
            }

            result.Categories = categoriesList;

            // 3. Feuille Compétiteurs
            var wsCompetitors = FindWorksheet(workbook, "Compétiteurs", "Competiteurs");
            var competitorsList = new List<CompetitorModel>();
            var competitorsByCat = new Dictionary<int, List<CompetitorModel>>();

            foreach (var cat in categoriesList)
            {
                competitorsByCat[cat.Id] = new List<CompetitorModel>();
            }

            if (wsCompetitors != null)
            {
                int compRow = 2;
                while (!string.IsNullOrWhiteSpace(wsCompetitors.Cell(compRow, 1).GetString()) ||
                       !string.IsNullOrWhiteSpace(wsCompetitors.Cell(compRow, 2).GetString()))
                {
                    string firstName = wsCompetitors.Cell(compRow, 1).GetString().Trim();
                    string lastName = wsCompetitors.Cell(compRow, 2).GetString().Trim();
                    string genreStr = wsCompetitors.Cell(compRow, 3).GetString().Trim();
                    string club = wsCompetitors.Cell(compRow, 5).GetString().Trim();
                    string countryIso = wsCompetitors.Cell(compRow, 6).GetString().Trim();
                    string weightStr = wsCompetitors.Cell(compRow, 7).GetString().Trim();
                    string catTargetStr = wsCompetitors.Cell(compRow, 8).GetString().Trim();

                    if (!Enum.TryParse<Genre>(genreStr, true, out var genre))
                    {
                        genre = Genre.Men;
                    }

                    TryParseCellDate(wsCompetitors.Cell(compRow, 4), out DateTime birthDate);
                    double weight = ParseDouble(weightStr);

                    // Recherche de la catégorie cible
                    CategoryModel? matchedCategory = null;
                    if (!string.IsNullOrWhiteSpace(catTargetStr))
                    {
                        if (categoryByName.TryGetValue(catTargetStr, out var c))
                        {
                            matchedCategory = c;
                        }
                        else if (int.TryParse(catTargetStr, out int catIdx) && catIdx >= 1 && catIdx <= categoriesList.Count)
                        {
                            matchedCategory = categoriesList[catIdx - 1];
                        }
                        else
                        {
                            // Recherche partielle
                            matchedCategory = categoriesList.FirstOrDefault(c =>
                                c.Name.Contains(catTargetStr, StringComparison.OrdinalIgnoreCase));
                        }
                    }

                    if (matchedCategory == null && categoriesList.Count > 0)
                    {
                        matchedCategory = categoriesList[0];
                        result.Warnings.Add($"Ligne {compRow} ({firstName} {lastName}) : catégorie '{catTargetStr}' non trouvée, affecté à '{matchedCategory.Name}'.");
                    }

                    var competitor = new CompetitorModel
                    {
                        Id = competitorsList.Count + 1,
                        FirstName = firstName,
                        LastName = lastName,
                        Genre = genre,
                        BirthDate = birthDate,
                        Club = club,
                        CountryIsoCode = string.IsNullOrWhiteSpace(countryIso) ? "FR" : countryIso,
                        Weight = weight
                    };

                    competitorsList.Add(competitor);

                    if (matchedCategory != null)
                    {
                        if (!competitorsByCat.ContainsKey(matchedCategory.Id))
                        {
                            competitorsByCat[matchedCategory.Id] = new List<CompetitorModel>();
                        }
                        competitorsByCat[matchedCategory.Id].Add(competitor);
                    }

                    compRow++;
                }
            }

            result.Competitors = competitorsList;

            // 4. Génération des matchs (MatchItem) et des tirages complets pour la session CLI
            var matches = new List<MatchItem>();
            int matchId = 1;

            foreach (var cat in categoriesList)
            {
                cat.CompetitionId = result.Competition.Id;
                var draw = new DrawModel
                {
                    Id = cat.Id,
                    CategoryId = cat.Id,
                    DrawKnockouts = new List<DrawKnockoutModel>(),
                    DrawPools = new List<DrawPoolsModel>(),
                    DrawOrders = new List<DrawOrderModel>()
                };
                cat.Draw = draw;

                if (!competitorsByCat.TryGetValue(cat.Id, out var catCompetitors) || catCompetitors.Count == 0)
                {
                    continue;
                }

                cat.Competitors = catCompetitors.Select(c => new CompetitorCategoryModel
                {
                    CategoryId = cat.Id,
                    CompetitorId = c.Id,
                    Competitor = c
                }).ToList();

                var roundType = (compType == CompetitionType.Taolu || cat.Type == CategoryType.Taolu || cat.RoundType == RoundType.Order)
                    ? RoundType.Order
                    : cat.RoundType;

                if (roundType == RoundType.Order)
                {
                    // Passage individuel (Taolu ou ordre de passage)
                    for (int i = 0; i < catCompetitors.Count; i++)
                    {
                        var comp = catCompetitors[i];
                        string compDisplay = FormatCompetitorName(comp);
                        int currentMatchId = matchId++;

                        matches.Add(new MatchItem
                        {
                            Id = currentMatchId,
                            Type = RoundType.Order,
                            Category = cat.Name,
                            RedCompetitor = compDisplay,
                            BlueCompetitor = string.Empty,
                            RedScore = 0,
                            BlueScore = 0
                        });

                        draw.DrawOrders!.Add(new DrawOrderModel
                        {
                            Id = currentMatchId,
                            Order = i + 1,
                            GlobalOrder = currentMatchId,
                            DrawId = draw.Id,
                            CompetitorId = comp.Id,
                            Competitor = comp
                        });
                    }
                }
                else if (roundType == RoundType.Pools)
                {
                    // Poule : tout le monde rencontre tout le monde
                    int poolOrder = 1;
                    for (int i = 0; i < catCompetitors.Count; i++)
                    {
                        for (int j = i + 1; j < catCompetitors.Count; j++)
                        {
                            var c1 = catCompetitors[i];
                            var c2 = catCompetitors[j];
                            int currentMatchId = matchId++;

                            matches.Add(new MatchItem
                            {
                                Id = currentMatchId,
                                Type = RoundType.Pools,
                                Category = cat.Name,
                                RedCompetitor = FormatCompetitorName(c1),
                                BlueCompetitor = FormatCompetitorName(c2),
                                RedScore = 0,
                                BlueScore = 0
                            });

                            draw.DrawPools!.Add(new DrawPoolsModel
                            {
                                Id = currentMatchId,
                                Order = poolOrder++,
                                GlobalOrder = currentMatchId,
                                DrawId = draw.Id,
                                Competitor1Id = c1.Id,
                                Competitor1 = c1,
                                Competitor2Id = c2.Id,
                                Competitor2 = c2,
                                RoundResultsJson = string.Empty
                            });
                        }
                    }
                }
                else
                {
                    // Élimination directe (Knockouts) : génération de l'arbre complet (quarts, demis, finale)
                    int n = catCompetitors.Count;
                    if (n <= 1)
                    {
                        if (n == 1)
                        {
                            int currentMatchId = matchId++;
                            matches.Add(new MatchItem
                            {
                                Id = currentMatchId,
                                Type = RoundType.Knockouts,
                                Category = cat.Name,
                                RedCompetitor = FormatCompetitorName(catCompetitors[0]),
                                BlueCompetitor = "[EXEMPTÉ / BYE]",
                                RedScore = 2,
                                BlueScore = 0,
                                IsFinished = true,
                                Winner = FormatCompetitorName(catCompetitors[0]),
                                WinnerSide = Constants.Side.Red,
                                DecisionReason = "Seul compétiteur inscrit",
                                StageName = "Finale"
                            });

                            draw.DrawKnockouts!.Add(new DrawKnockoutModel
                            {
                                Id = currentMatchId,
                                Order = 1,
                                GlobalOrder = currentMatchId,
                                DrawId = draw.Id,
                                Draw = draw,
                                Competitor1Id = catCompetitors[0].Id,
                                Competitor1 = catCompetitors[0],
                                Competitor2Id = null,
                                IsBronzeMatch = false,
                                IsFinished = true,
                                WinnerId = catCompetitors[0].Id,
                                RoundResultsJson = string.Empty
                            });
                        }
                    }
                    else
                    {
                        // Trouver la puissance de 2 immédiatement >= n (ex: n=4 -> m=4; n=3 -> m=4; n=8 -> m=8)
                        int m = 1;
                        while (m < n) m *= 2;
                        if (m < 2) m = 2;

                        int totalMatchesInBracket = m - 1;
                        int initialRoundMatchesCount = m / 2;

                        // Construire les listes de matchs pour chaque tour de l'arbre
                        var rounds = new List<List<MatchItem>>();
                        int currentCount = initialRoundMatchesCount;
                        int categoryMatchOrder = 1;

                        while (currentCount >= 1)
                        {
                            var roundList = new List<MatchItem>();
                            for (int i = 0; i < currentCount; i++)
                            {
                                string stage = KnockoutStageHelper.GetStageName(categoryMatchOrder, totalMatchesInBracket, includeIndex: true);
                                var match = new MatchItem
                                {
                                    Id = matchId++,
                                    Type = RoundType.Knockouts,
                                    Category = cat.Name,
                                    RedCompetitor = "À déterminer",
                                    BlueCompetitor = "À déterminer",
                                    RedScore = 0,
                                    BlueScore = 0,
                                    StageName = stage
                                };
                                categoryMatchOrder++;
                                roundList.Add(match);
                            }
                            rounds.Add(roundList);
                            currentCount /= 2;
                        }

                        // Lier les tours entre eux (NextMatchId et NextMatchSide)
                        for (int r = 0; r < rounds.Count - 1; r++)
                        {
                            var currentRoundMatches = rounds[r];
                            var nextRoundMatches = rounds[r + 1];

                            for (int i = 0; i < currentRoundMatches.Count; i++)
                            {
                                var cur = currentRoundMatches[i];
                                var nxt = nextRoundMatches[i / 2];

                                cur.NextMatchId = nxt.Id;
                                cur.NextMatchSide = (i % 2 == 0) ? Constants.Side.Red : Constants.Side.Blue;

                                if (cur.NextMatchSide == Constants.Side.Red)
                                    nxt.RedCompetitor = $"Vainqueur {cur.StageName}";
                                else
                                    nxt.BlueCompetitor = $"Vainqueur {cur.StageName}";
                            }
                        }

                        // Si la catégorie a une petite finale activée et au moins 4 compétiteurs (ou au moins 2 tours: demis + finale)
                        if (cat.HasBronzeMatch && rounds.Count >= 2)
                        {
                            var semiRound = rounds[rounds.Count - 2];
                            if (semiRound.Count == 2)
                            {
                                var semi1 = semiRound[0];
                                var semi2 = semiRound[1];

                                var bronzeMatch = new MatchItem
                                {
                                    Id = matchId++,
                                    Type = RoundType.Knockouts,
                                    Category = cat.Name,
                                    RedCompetitor = $"Perdant {semi1.StageName}",
                                    BlueCompetitor = $"Perdant {semi2.StageName}",
                                    RedScore = 0,
                                    BlueScore = 0,
                                    StageName = "Petite finale",
                                    IsBronzeMatch = true
                                };

                                semi1.NextLoserMatchId = bronzeMatch.Id;
                                semi1.NextLoserMatchSide = Constants.Side.Red;

                                semi2.NextLoserMatchId = bronzeMatch.Id;
                                semi2.NextLoserMatchSide = Constants.Side.Blue;

                                // Insérer la petite finale juste avant la finale
                                rounds[rounds.Count - 1].Insert(0, bronzeMatch);
                            }
                        }

                        // Remplir le premier tour avec les compétiteurs réels et gérer les exemptions (BYEs)
                        // Utilisation de DistributeFirstRoundCompetitors pour garantir qu'aucun match n'a deux compétiteurs null
                        var firstRoundMatches = rounds[0];
                        var firstRoundPairs = KnockoutStageHelper.DistributeFirstRoundCompetitors(catCompetitors, firstRoundMatches.Count);
                        for (int i = 0; i < firstRoundMatches.Count; i++)
                        {
                            var match = firstRoundMatches[i];
                            var (c1, c2) = firstRoundPairs[i];

                            if (c1 != null && c2 != null)
                            {
                                match.RedCompetitor = FormatCompetitorName(c1);
                                match.BlueCompetitor = FormatCompetitorName(c2);
                            }
                            else if (c1 != null && c2 == null)
                            {
                                match.RedCompetitor = FormatCompetitorName(c1);
                                match.BlueCompetitor = "[EXEMPTÉ / BYE]";
                                match.RedScore = 2;
                                match.BlueScore = 0;
                                match.IsFinished = true;
                                match.Winner = FormatCompetitorName(c1);
                                match.WinnerSide = Constants.Side.Red;
                                match.DecisionReason = "Exemption (BYE)";

                                // Propager l'exempté immédiatement au match suivant
                                if (match.NextMatchId.HasValue)
                                {
                                    var targetMatch = rounds.SelectMany(r => r).FirstOrDefault(x => x.Id == match.NextMatchId.Value);
                                    if (targetMatch != null)
                                    {
                                        if (match.NextMatchSide == Constants.Side.Red)
                                            targetMatch.RedCompetitor = match.Winner;
                                        else
                                            targetMatch.BlueCompetitor = match.Winner;
                                    }
                                }
                            }
                            else if (c1 == null && c2 != null)
                            {
                                match.RedCompetitor = "[EXEMPTÉ / BYE]";
                                match.BlueCompetitor = FormatCompetitorName(c2);
                                match.RedScore = 0;
                                match.BlueScore = 2;
                                match.IsFinished = true;
                                match.Winner = FormatCompetitorName(c2);
                                match.WinnerSide = Constants.Side.Blue;
                                match.DecisionReason = "Exemption (BYE)";

                                if (match.NextMatchId.HasValue)
                                {
                                    var targetMatch = rounds.SelectMany(r => r).FirstOrDefault(x => x.Id == match.NextMatchId.Value);
                                    if (targetMatch != null)
                                    {
                                        if (match.NextMatchSide == Constants.Side.Red)
                                            targetMatch.RedCompetitor = match.Winner;
                                        else
                                            targetMatch.BlueCompetitor = match.Winner;
                                    }
                                }
                            }
                        }

                        // Ajouter tous les matchs dans l'ordre chronologique des tours et peupler le tirage Knockouts
                        int koOrder = 1;
                        foreach (var roundList in rounds)
                        {
                            matches.AddRange(roundList);
                            foreach (var match in roundList)
                            {
                                CompetitorModel? c1 = catCompetitors.FirstOrDefault(c => FormatCompetitorName(c).Equals(match.RedCompetitor, StringComparison.OrdinalIgnoreCase));
                                CompetitorModel? c2 = catCompetitors.FirstOrDefault(c => FormatCompetitorName(c).Equals(match.BlueCompetitor, StringComparison.OrdinalIgnoreCase));

                                draw.DrawKnockouts!.Add(new DrawKnockoutModel
                                {
                                    Id = match.Id,
                                    Order = koOrder++,
                                    GlobalOrder = match.Id,
                                    DrawId = draw.Id,
                                    Draw = draw,
                                    Competitor1Id = c1?.Id,
                                    Competitor1 = c1,
                                    Competitor2Id = c2?.Id,
                                    Competitor2 = c2,
                                    IsBronzeMatch = match.IsBronzeMatch,
                                    IsFinished = match.IsFinished,
                                    WinnerId = match.WinnerSide == Constants.Side.Red ? c1?.Id : (match.WinnerSide == Constants.Side.Blue ? c2?.Id : null),
                                    RoundResultsJson = string.Empty
                                });
                            }
                        }
                    }
                }
            }

            // Ordonnancement global du tournoi :
            // 1. Tous les matchs préliminaires / demi-finales (et Taolu/Poules) de toutes les catégories
            // 2. TOUTES les Petites finales de toutes les catégories
            // 3. TOUTES les Finales de toutes les catégories
            var prelimMatches = matches.Where(m => !m.IsBronzeMatch && !string.Equals(m.StageName, "Petite finale", StringComparison.OrdinalIgnoreCase) && !string.Equals(m.StageName, "Finale", StringComparison.OrdinalIgnoreCase)).ToList();
            var bronzeMatches = matches.Where(m => m.IsBronzeMatch || string.Equals(m.StageName, "Petite finale", StringComparison.OrdinalIgnoreCase)).ToList();
            var finalMatches = matches.Where(m => !m.IsBronzeMatch && string.Equals(m.StageName, "Finale", StringComparison.OrdinalIgnoreCase)).ToList();

            var scheduledMatches = prelimMatches
                .Concat(bronzeMatches)
                .Concat(finalMatches)
                .ToList();

            var idMap = new Dictionary<int, int>();
            for (int i = 0; i < scheduledMatches.Count; i++)
            {
                int oldId = scheduledMatches[i].Id;
                int newId = i + 1;
                scheduledMatches[i].Id = newId;
                idMap[oldId] = newId;
            }

            // Remap NextMatchId et NextLoserMatchId
            foreach (var m in scheduledMatches)
            {
                if (m.NextMatchId.HasValue && idMap.TryGetValue(m.NextMatchId.Value, out int mappedNextId))
                {
                    m.NextMatchId = mappedNextId;
                }
                if (m.NextLoserMatchId.HasValue && idMap.TryGetValue(m.NextLoserMatchId.Value, out int mappedLoserId))
                {
                    m.NextLoserMatchId = mappedLoserId;
                }
            }

            // Mettre à jour les DrawKnockouts pour chaque catégorie
            foreach (var cat in categoriesList)
            {
                if (cat.Draw?.DrawKnockouts != null && cat.Draw.DrawKnockouts.Count > 0)
                {
                    foreach (var k in cat.Draw.DrawKnockouts)
                    {
                        if (idMap.TryGetValue(k.Id, out int newId))
                        {
                            k.Id = newId;
                            k.GlobalOrder = newId;
                        }
                    }

                    // Ré-ordonner l'ordre local dans la catégorie selon GlobalOrder (qui garantit Petite finale avant Finale)
                    var catKo = cat.Draw.DrawKnockouts
                        .OrderBy(k => k.GlobalOrder)
                        .ToList();

                    for (int i = 0; i < catKo.Count; i++)
                    {
                        catKo[i].Order = i + 1;
                        catKo[i].Draw = cat.Draw;
                    }

                    cat.Draw.DrawKnockouts = catKo;
                }
            }

            matches = scheduledMatches;

            result.Competition.Categories = categoriesList;
            result.Matches = matches;
            result.Success = true;

            if (categoriesList.Count == 0)
            {
                result.Warnings.Add("Aucune catégorie définie dans le fichier.");
            }
            if (competitorsList.Count == 0)
            {
                result.Warnings.Add("Aucun compétiteur défini dans le fichier.");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Erreur lors de l'analyse du classeur Excel : {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Génère un fichier Excel template prêt à être rempli.
    /// </summary>
    public static void ExportTemplate(string filePath)
    {
        using var workbook = CreateTemplateWorkbook();
        workbook.SaveAs(filePath);
    }

    /// <summary>
    /// Génère un flux contenant le classeur modèle Excel.
    /// </summary>
    public static Stream ExportTemplateStream()
    {
        var workbook = CreateTemplateWorkbook();
        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static XLWorkbook CreateTemplateWorkbook()
    {
        var workbook = new XLWorkbook();

        // 1. Feuille Compétition
        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 2).Value = "Date (JJ/MM/AAAA)";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Row(1).Style.Font.Bold = true;

        wsComp.Cell(2, 1).Value = "Championnat de France Wushu 2026";
        wsComp.Cell(2, 2).Value = DateTime.Today.ToString("dd/MM/yyyy");
        wsComp.Cell(2, 3).Value = "Sanda";

        var compTypeValidation = wsComp.Range("C2:C1000").CreateDataValidation();
        compTypeValidation.List("\"Sanda,Taolu\"");

        wsComp.Column(1).Width = 35;
        wsComp.Column(2).Width = 20;
        wsComp.Column(3).Width = 15;

        // 2. Feuille Catégories
        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "TypeCategorie";
        wsCat.Cell(1, 2).Value = "Genre";
        wsCat.Cell(1, 3).Value = "PoidsMin";
        wsCat.Cell(1, 4).Value = "PoidsMax";
        wsCat.Cell(1, 5).Value = "TrancheAge";
        wsCat.Cell(1, 6).Value = "TypeRound";
        wsCat.Cell(1, 7).Value = "NomCategorie (auto)";
        wsCat.Cell(1, 8).Value = "Petite Finale (Oui/Non)";
        wsCat.Row(1).Style.Font.Bold = true;
        wsCat.Cell(1, 7).Style.Fill.BackgroundColor = XLColor.LightGray;

        wsCat.Cell(2, 1).Value = "Sanda";
        wsCat.Cell(2, 2).Value = "Men";
        wsCat.Cell(2, 3).Value = "60";
        wsCat.Cell(2, 4).Value = "65";
        wsCat.Cell(2, 5).Value = "Seniors";
        wsCat.Cell(2, 6).Value = "Knockouts";
        wsCat.Cell(2, 8).Value = "Oui";

        wsCat.Cell(3, 1).Value = "Sanda";
        wsCat.Cell(3, 2).Value = "Men";
        wsCat.Cell(3, 3).Value = "65";
        wsCat.Cell(3, 4).Value = "70";
        wsCat.Cell(3, 5).Value = "Seniors";
        wsCat.Cell(3, 6).Value = "Knockouts";
        wsCat.Cell(3, 8).Value = "Non";

        wsCat.Cell(4, 1).Value = "Taolu";
        wsCat.Cell(4, 2).Value = "Mixte";
        wsCat.Cell(4, 3).Value = "0";
        wsCat.Cell(4, 4).Value = "0";
        wsCat.Cell(4, 5).Value = "Seniors";
        wsCat.Cell(4, 6).Value = "Order";
        wsCat.Cell(4, 8).Value = "Non";

        const string formuleCatNom =
            "=IF(A{0}=\"\",\"\"," +
            "A{0}" +
            "&IF(E{0}<>\"\",\" \"&E{0},\"\")" +
            "&\" \"&IF(B{0}=\"Men\",\"Hommes\",IF(B{0}=\"Women\",\"Femmes\",\"Mixte\"))" +
            "&IF(AND(C{0}<=0,D{0}>0),\" -\"&D{0}&\"kg\"," +
              "IF(AND(C{0}>0,D{0}<=0),\" +\"&C{0}&\"kg\"," +
              "IF(AND(C{0}>0,D{0}>0),\" -\"&D{0}&\"kg\",\"\"))))";

        for (int r = 2; r <= 100; r++)
        {
            wsCat.Cell(r, 7).FormulaA1 = string.Format(formuleCatNom, r);
            wsCat.Cell(r, 7).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var bronzeValidation = wsCat.Range("H2:H100").CreateDataValidation();
        bronzeValidation.List("\"Oui,Non\"");

        workbook.DefinedNames.Add("ListeCategories", wsCat.Range("G2:G100"));

        wsCat.Column(1).Width = 20;
        wsCat.Column(2).Width = 12;
        wsCat.Column(3).Width = 12;
        wsCat.Column(4).Width = 12;
        wsCat.Column(5).Width = 15;
        wsCat.Column(6).Width = 15;
        wsCat.Column(7).Width = 32;
        wsCat.Column(8).Width = 22;

        // 3. Feuille Compétiteurs
        var wsCompetitors = workbook.Worksheets.Add("Compétiteurs");
        wsCompetitors.Cell(1, 1).Value = "Prénom";
        wsCompetitors.Cell(1, 2).Value = "Nom";
        wsCompetitors.Cell(1, 3).Value = "Genre";
        wsCompetitors.Cell(1, 4).Value = "Date de naissance";
        wsCompetitors.Cell(1, 5).Value = "Club";
        wsCompetitors.Cell(1, 6).Value = "Code Pays";
        wsCompetitors.Cell(1, 7).Value = "Poids (kg)";
        wsCompetitors.Cell(1, 8).Value = "Catégorie";
        wsCompetitors.Row(1).Style.Font.Bold = true;

        wsCompetitors.Cell(2, 1).Value = "Jean";
        wsCompetitors.Cell(2, 2).Value = "Dupont";
        wsCompetitors.Cell(2, 3).Value = "Men";
        wsCompetitors.Cell(2, 4).Value = "12/05/1998";
        wsCompetitors.Cell(2, 5).Value = "Paris Wushu Club";
        wsCompetitors.Cell(2, 6).Value = "FR";
        wsCompetitors.Cell(2, 7).Value = "64";
        wsCompetitors.Cell(2, 8).Value = "Sanda Seniors Hommes -65kg";

        wsCompetitors.Cell(3, 1).Value = "Marc";
        wsCompetitors.Cell(3, 2).Value = "Martin";
        wsCompetitors.Cell(3, 3).Value = "Men";
        wsCompetitors.Cell(3, 4).Value = "23/08/1997";
        wsCompetitors.Cell(3, 5).Value = "Lyon Kung Fu";
        wsCompetitors.Cell(3, 6).Value = "FR";
        wsCompetitors.Cell(3, 7).Value = "63.5";
        wsCompetitors.Cell(3, 8).Value = "Sanda Seniors Hommes -65kg";

        wsCompetitors.Cell(4, 1).Value = "Antoine";
        wsCompetitors.Cell(4, 2).Value = "Leclerc";
        wsCompetitors.Cell(4, 3).Value = "Men";
        wsCompetitors.Cell(4, 4).Value = "15/11/1996";
        wsCompetitors.Cell(4, 5).Value = "Marseille Sanda";
        wsCompetitors.Cell(4, 6).Value = "FR";
        wsCompetitors.Cell(4, 7).Value = "68";
        wsCompetitors.Cell(4, 8).Value = "Sanda Seniors Hommes -70kg";

        wsCompetitors.Cell(5, 1).Value = "David";
        wsCompetitors.Cell(5, 2).Value = "Zhang";
        wsCompetitors.Cell(5, 3).Value = "Men";
        wsCompetitors.Cell(5, 4).Value = "04/02/1995";
        wsCompetitors.Cell(5, 5).Value = "Strasbourg Arts Martiaux";
        wsCompetitors.Cell(5, 6).Value = "FR";
        wsCompetitors.Cell(5, 7).Value = "69.2";
        wsCompetitors.Cell(5, 8).Value = "Sanda Seniors Hommes -70kg";

        var catDropdown = wsCompetitors.Range("H2:H100").CreateDataValidation();
        catDropdown.List("=ListeCategories");

        wsCompetitors.Column(1).Width = 16;
        wsCompetitors.Column(2).Width = 16;
        wsCompetitors.Column(3).Width = 12;
        wsCompetitors.Column(4).Width = 18;
        wsCompetitors.Column(5).Width = 24;
        wsCompetitors.Column(6).Width = 12;
        wsCompetitors.Column(7).Width = 12;
        wsCompetitors.Column(8).Width = 32;

        return workbook;
    }

    private static string FormatCompetitorName(CompetitorModel c)
    {
        string name = $"{c.LastName.ToUpperInvariant()} {c.FirstName}".Trim();
        if (!string.IsNullOrWhiteSpace(c.Club))
        {
            name += $" ({c.Club})";
        }
        return name;
    }

    private static IXLWorksheet? FindWorksheet(XLWorkbook workbook, params string[] names)
    {
        foreach (var name in names)
        {
            if (workbook.Worksheets.TryGetWorksheet(name, out var ws))
            {
                return ws;
            }
        }
        return workbook.Worksheets.FirstOrDefault();
    }

    public static bool TryParseCellDate(IXLCell cell, out DateTime result)
    {
        if (cell.TryGetValue<DateTime>(out result))
        {
            return true;
        }

        var str = cell.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(str))
        {
            result = default;
            return false;
        }

        var dateOnlyStr = str.Split(' ')[0].Trim();

        string[] formats = new[]
        {
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd/MM/yyyy HH:mm:ss",
            "d/M/yyyy HH:mm:ss",
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "dd-MM-yyyy"
        };

        if (DateTime.TryParseExact(str, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParseExact(dateOnlyStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParse(str, new CultureInfo("fr-FR"), DateTimeStyles.None, out result))
            return true;

        return DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    public static bool TryParseCategoryType(string? str, out CategoryType type)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            type = CategoryType.None;
            return false;
        }

        var normalized = str.Trim().Replace(" ", string.Empty);
        return Enum.TryParse<CategoryType>(normalized, true, out type) && type != CategoryType.None;
    }

    public static string BuildCategoryDisplayName(
        CategoryType type, Genre genre, double weightMin, double weightMax, string? ageLabel)
    {
        var genreStr = genre switch
        {
            Genre.Men   => "Hommes",
            Genre.Women => "Femmes",
            _           => "Mixte"
        };

        string weightStr;
        if (weightMin <= 0 && weightMax > 0)
            weightStr = $" -{weightMax}kg";
        else if (weightMin > 0 && weightMax <= 0)
            weightStr = $" +{weightMin}kg";
        else if (weightMin > 0 && weightMax > 0)
            weightStr = $" -{weightMax}kg";
        else
            weightStr = string.Empty;

        var parts = new List<string> { type.ToString() };
        if (!string.IsNullOrWhiteSpace(ageLabel))
            parts.Add(ageLabel);
        parts.Add(genreStr);

        return string.Join(" ", parts) + weightStr;
    }

    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Replace(' ', '_');
    }

    private static double ParseDouble(string str)
    {
        if (string.IsNullOrWhiteSpace(str)) return 0;
        double.TryParse(str.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double val);
        return val;
    }

    // ══════════════════════════════════════════════════════════════════
    // Export des Résultats (Classements par catégorie)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Exporte les résultats et classements de la compétition dans un fichier Excel (.xlsx) avec un onglet par catégorie.
    /// </summary>
    public static string ExportResults(ArbitrationServerSession session, string? filePath = null)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        string compName = session.CurrentCompetition?.Name ?? "Wushu";
        DateTime? compDate = session.CurrentCompetition?.Date ?? DateTime.Today;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            filePath = $"Resultats_{SanitizeFileName(compName)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        }

        using var workbook = new XLWorkbook();
        var summaries = session.GetCategoriesSummary();

        // 1. Onglet Résumé
        var wsSummary = workbook.Worksheets.Add("Résumé");
        StyleResultsSummarySheet(wsSummary, compName, compDate, session.CurrentCompetitionType, summaries, session);

        // 2. Un onglet par catégorie
        foreach (var summary in summaries)
        {
            string sheetName = summary.Name.Length > 31 ? summary.Name[..31] : summary.Name;
            int suffix = 2;
            string original = sheetName;
            while (workbook.Worksheets.Any(w => w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase)))
            {
                string s = $" ({suffix})";
                sheetName = original.Length + s.Length > 31
                    ? original[..(31 - s.Length)] + s
                    : original + s;
                suffix++;
            }

            var wsCat = workbook.Worksheets.Add(sheetName);
            var report = session.GetCategoryHistory(summary.Name);
            StyleCategoryResultsSheet(wsCat, summary, report, compName, compDate);
        }

        string fullPath = Path.GetFullPath(filePath);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        workbook.SaveAs(fullPath);
        return fullPath;
    }

    private static void StyleResultsSummarySheet(
        IXLWorksheet ws,
        string compName,
        DateTime? compDate,
        CompetitionType compType,
        IReadOnlyList<CategorySummary> summaries,
        ArbitrationServerSession session)
    {
        ws.Cell(1, 1).Value = compName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 7).Merge();

        string subTitle = $"Discipline : {compType}";
        if (compDate.HasValue) subTitle += $" — Date : {compDate.Value:dd/MM/yyyy}";
        ws.Cell(2, 1).Value = subTitle;
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Range(2, 1, 2, 7).Merge();

        int headerRow = 4;
        ws.Cell(headerRow, 1).Value = "Catégorie";
        ws.Cell(headerRow, 2).Value = "Type de tournoi";
        ws.Cell(headerRow, 3).Value = "🥇 1er (Champion)";
        ws.Cell(headerRow, 4).Value = "🥈 2e";
        ws.Cell(headerRow, 5).Value = "🥉 3e";
        ws.Cell(headerRow, 6).Value = "Matchs joués";
        ws.Cell(headerRow, 7).Value = "Statut";

        var headerRange = ws.Range(headerRow, 1, headerRow, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int row = headerRow + 1;
        foreach (var sum in summaries)
        {
            var rep = session.GetCategoryHistory(sum.Name);
            string first = rep?.Standings.FirstOrDefault(s => s.Rank == 1 && s.HasFinished)?.CompetitorName ?? sum.LeaderOrWinner ?? "—";
            string second = rep?.Standings.FirstOrDefault(s => s.Rank == 2 && s.HasFinished)?.CompetitorName ?? "—";
            string third = rep?.Standings.FirstOrDefault(s => s.Rank == 3 && s.HasFinished)?.CompetitorName ?? "—";

            ws.Cell(row, 1).Value = sum.Name;
            ws.Cell(row, 2).Value = sum.RoundType.ToString();
            ws.Cell(row, 3).Value = first;
            ws.Cell(row, 4).Value = second;
            ws.Cell(row, 5).Value = third;
            ws.Cell(row, 6).Value = $"{sum.CompletedMatches}/{sum.TotalMatches}";
            ws.Cell(row, 7).Value = sum.StatusText;

            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (row % 2 == 0)
                ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");

            row++;
        }

        if (row > headerRow + 1)
        {
            ws.Range(headerRow, 1, row - 1, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, row - 1, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        ws.Column(1).Width = 32;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 26;
        ws.Column(4).Width = 26;
        ws.Column(5).Width = 26;
        ws.Column(6).Width = 15;
        ws.Column(7).Width = 14;
    }

    private static void StyleCategoryResultsSheet(
        IXLWorksheet ws,
        CategorySummary summary,
        CategoryHistoryReport? report,
        string compName,
        DateTime? compDate)
    {
        ws.Cell(1, 1).Value = $"Résultats — {compName}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        if (compDate.HasValue)
        {
            ws.Cell(2, 1).Value = compDate.Value.ToString("dd/MM/yyyy");
            ws.Cell(2, 1).Style.Font.Italic = true;
        }

        bool isTaolu = summary.RoundType == RoundType.Order;
        int colCount = isTaolu ? 6 : 11;
        ws.Range(1, 1, 1, colCount).Merge();
        ws.Range(2, 1, 2, colCount).Merge();

        ws.Cell(3, 1).Value = $"Catégorie : {summary.Name} ({summary.RoundType}) — {summary.CompletedMatches}/{summary.TotalMatches} match(s) terminé(s)";
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 1).Style.Font.FontSize = 12;
        ws.Cell(3, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.White;
        ws.Range(3, 1, 3, colCount).Merge();

        int headerRow = 5;
        if (report != null && !string.IsNullOrWhiteSpace(report.CategoryWinner))
        {
            string winTxt = $"🏆 Vainqueur / Champion : {report.CategoryWinner}";
            if (!string.IsNullOrWhiteSpace(report.DecisionReason)) winTxt += $" ({report.DecisionReason})";
            ws.Cell(4, 1).Value = winTxt;
            ws.Cell(4, 1).Style.Font.Bold = true;
            ws.Cell(4, 1).Style.Font.FontColor = XLColor.FromHtml("#8B6508");
            ws.Cell(4, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF8DC");
            ws.Range(4, 1, 4, colCount).Merge();
            headerRow = 6;
        }

        if (isTaolu)
        {
            ws.Cell(headerRow, 1).Value = "Rang";
            ws.Cell(headerRow, 2).Value = "Médaille";
            ws.Cell(headerRow, 3).Value = "Compétiteur";
            ws.Cell(headerRow, 4).Value = "Score / Note finale";
            ws.Cell(headerRow, 5).Value = "Notes par juge";
            ws.Cell(headerRow, 6).Value = "Statut";
        }
        else
        {
            ws.Cell(headerRow, 1).Value = "Rang";
            ws.Cell(headerRow, 2).Value = "Médaille";
            ws.Cell(headerRow, 3).Value = "Compétiteur";
            ws.Cell(headerRow, 4).Value = "Matchs joués";
            ws.Cell(headerRow, 5).Value = "Victoires";
            ws.Cell(headerRow, 6).Value = "Défaites";
            ws.Cell(headerRow, 7).Value = "Nuls";
            ws.Cell(headerRow, 8).Value = "Pts marqués";
            ws.Cell(headerRow, 9).Value = "Pts concédés";
            ws.Cell(headerRow, 10).Value = "Différence";
            ws.Cell(headerRow, 11).Value = "Statut";
        }

        var headerRange = ws.Range(headerRow, 1, headerRow, colCount);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C5F8A");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int row = headerRow + 1;
        var standings = report?.Standings ?? Array.Empty<CategoryStandingItem>();

        foreach (var st in standings.OrderBy(s => s.Rank))
        {
            string medal = st.Rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => string.Empty
            };

            ws.Cell(row, 1).Value = st.Rank;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 2).Value = medal;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 3).Value = st.CompetitorName;

            if (isTaolu)
            {
                if (st.HasFinished)
                {
                    ws.Cell(row, 4).Value = st.Score;
                    ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
                }
                else
                {
                    ws.Cell(row, 4).Value = "—";
                }
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                string judgeNotes = st.JudgeScoresByJudge.Count > 0
                    ? string.Join(" | ", st.JudgeScoresByJudge.Select(kv => $"{kv.Key}: {kv.Value:F2}"))
                    : (st.JudgeScores.Count > 0 ? string.Join(", ", st.JudgeScores.Select(s => s.ToString("F2"))) : string.Empty);

                ws.Cell(row, 5).Value = judgeNotes;
                ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 6).Value = st.Status;
            }
            else
            {
                ws.Cell(row, 4).Value = st.MatchesPlayed;
                ws.Cell(row, 5).Value = st.Wins;
                ws.Cell(row, 6).Value = st.Losses;
                ws.Cell(row, 7).Value = st.Draws;
                ws.Cell(row, 8).Value = st.PointsFor;
                ws.Cell(row, 9).Value = st.PointsAgainst;
                ws.Cell(row, 10).Value = st.PointDifference;
                ws.Cell(row, 11).Value = st.Status;

                for (int col = 4; col <= 10; col++)
                    ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            var rowRange = ws.Range(row, 1, row, colCount);
            if (st.Rank == 1)
                rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF8DC");
            else if (st.Rank == 2)
                rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
            else if (st.Rank == 3)
                rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF0E6");
            else if (row % 2 == 0)
                rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");

            row++;
        }

        if (row > headerRow + 1)
        {
            ws.Range(headerRow, 1, row - 1, colCount).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, row - 1, colCount).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        ws.Column(1).Width = 8;
        ws.Column(2).Width = 10;
        ws.Column(3).Width = 28;
        if (isTaolu)
        {
            ws.Column(4).Width = 20;
            ws.Column(5).Width = 26;
            ws.Column(6).Width = 22;
        }
        else
        {
            ws.Column(4).Width = 13;
            ws.Column(5).Width = 10;
            ws.Column(6).Width = 10;
            ws.Column(7).Width = 8;
            ws.Column(8).Width = 13;
            ws.Column(9).Width = 13;
            ws.Column(10).Width = 12;
            ws.Column(11).Width = 22;
        }

        ws.SheetView.FreezeRows(headerRow);
    }

    // ══════════════════════════════════════════════════════════════════
    // Export des Notes des Juges (Logs / RefereeData)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Exporte l'historique complet des notes des juges (format RefereeData) dans un fichier Excel (.xlsx) avec un onglet par catégorie.
    /// </summary>
    public static string ExportJudgeScores(ArbitrationServerSession session, string? filePath = null)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        string compName = session.CurrentCompetition?.Name ?? "Wushu";
        DateTime? compDate = session.CurrentCompetition?.Date ?? DateTime.Today;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            filePath = $"NotesJuges_{SanitizeFileName(compName)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        }

        using var workbook = new XLWorkbook();
        var allScores = session.JudgeScoreHistory;

        var categoryNames = new List<string>();
        var summaries = session.GetCategoriesSummary();
        foreach (var s in summaries)
        {
            if (!categoryNames.Contains(s.Name)) categoryNames.Add(s.Name);
        }
        foreach (var sc in allScores)
        {
            string cat = string.IsNullOrWhiteSpace(sc.MatchCategory) ? "Catégorie Générale" : sc.MatchCategory.Trim();
            if (!categoryNames.Contains(cat)) categoryNames.Add(cat);
        }

        if (categoryNames.Count == 0)
        {
            categoryNames.Add("Catégorie Générale");
        }

        // 1. Onglet Résumé
        var wsSummary = workbook.Worksheets.Add("Résumé");
        StyleJudgeScoresSummarySheet(wsSummary, compName, compDate, categoryNames, allScores);

        // 2. Un onglet par catégorie
        foreach (var catName in categoryNames)
        {
            var catScores = allScores
                .Where(s => (string.IsNullOrWhiteSpace(s.MatchCategory) ? "Catégorie Générale" : s.MatchCategory.Trim()).Equals(catName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.MatchId)
                .ThenBy(s => s.Timestamp)
                .ToList();

            string sheetName = catName.Length > 31 ? catName[..31] : catName;
            int suffix = 2;
            string original = sheetName;
            while (workbook.Worksheets.Any(w => w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase)))
            {
                string s = $" ({suffix})";
                sheetName = original.Length + s.Length > 31
                    ? original[..(31 - s.Length)] + s
                    : original + s;
                suffix++;
            }

            var wsCat = workbook.Worksheets.Add(sheetName);
            StyleCategoryJudgeScoresSheet(wsCat, catName, catScores, compName, compDate);
        }

        string fullPath = Path.GetFullPath(filePath);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        workbook.SaveAs(fullPath);
        return fullPath;
    }

    private static void StyleJudgeScoresSummarySheet(
        IXLWorksheet ws,
        string compName,
        DateTime? compDate,
        List<string> categoryNames,
        IReadOnlyList<JudgeScoreEntry> allScores)
    {
        ws.Cell(1, 1).Value = $"Notes des juges (Logs) — {compName}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 7).Merge();

        if (compDate.HasValue)
        {
            ws.Cell(2, 1).Value = $"Date de compétition : {compDate.Value:dd/MM/yyyy}";
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 7).Merge();
        }

        int headerRow = 4;
        ws.Cell(headerRow, 1).Value = "Catégorie";
        ws.Cell(headerRow, 2).Value = "Nb. notes";
        ws.Cell(headerRow, 3).Value = "Points Rouge";
        ws.Cell(headerRow, 4).Value = "Points Bleu";
        ws.Cell(headerRow, 5).Value = "Déductions";
        ws.Cell(headerRow, 6).Value = "Juges distincts";
        ws.Cell(headerRow, 7).Value = "Plage horaire";

        var headerRange = ws.Range(headerRow, 1, headerRow, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int row = headerRow + 1;
        foreach (var cat in categoryNames)
        {
            var catScores = allScores
                .Where(s => (string.IsNullOrWhiteSpace(s.MatchCategory) ? "Catégorie Générale" : s.MatchCategory.Trim()).Equals(cat, StringComparison.OrdinalIgnoreCase))
                .ToList();

            int count = catScores.Count;
            int red = catScores.Count(s => s.Side == Constants.Side.Red || s.ActionType.Contains("Rouge"));
            int blue = catScores.Count(s => s.Side == Constants.Side.Blue || s.ActionType.Contains("Bleu"));
            int ded = catScores.Count(s => s.DeductionCode != null || s.ActionType.Contains("Déduction"));
            int distinctJudges = catScores.Select(s => s.JudgeName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Count();

            string plage = "—";
            if (catScores.Count > 0)
            {
                var minDate = catScores.Min(s => s.Timestamp);
                var maxDate = catScores.Max(s => s.Timestamp);
                plage = $"{minDate:HH:mm:ss} → {maxDate:HH:mm:ss}";
            }

            ws.Cell(row, 1).Value = cat;
            ws.Cell(row, 2).Value = count;
            ws.Cell(row, 3).Value = red;
            ws.Cell(row, 4).Value = blue;
            ws.Cell(row, 5).Value = ded;
            ws.Cell(row, 6).Value = distinctJudges;
            ws.Cell(row, 7).Value = plage;

            for (int col = 2; col <= 6; col++)
                ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (row % 2 == 0)
                ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");

            row++;
        }

        if (row > headerRow + 1)
        {
            ws.Range(headerRow, 1, row - 1, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, row - 1, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        ws.Column(1).Width = 32;
        ws.Column(2).Width = 12;
        ws.Column(3).Width = 14;
        ws.Column(4).Width = 14;
        ws.Column(5).Width = 14;
        ws.Column(6).Width = 16;
        ws.Column(7).Width = 24;
    }

    private static void StyleCategoryJudgeScoresSheet(
        IXLWorksheet ws,
        string catName,
        List<JudgeScoreEntry> catScores,
        string compName,
        DateTime? compDate)
    {
        ws.Cell(1, 1).Value = $"Notes des juges — {compName}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Range(1, 1, 1, 9).Merge();

        if (compDate.HasValue)
        {
            ws.Cell(2, 1).Value = compDate.Value.ToString("dd/MM/yyyy");
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 9).Merge();
        }

        ws.Cell(3, 1).Value = $"Catégorie : {catName} ({catScores.Count} note(s))";
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 1).Style.Font.FontSize = 12;
        ws.Cell(3, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.White;
        ws.Range(3, 1, 3, 11).Merge();

        int headerRow = 5;
        ws.Cell(headerRow, 1).Value = "Date";
        ws.Cell(headerRow, 2).Value = "Heure";
        ws.Cell(headerRow, 3).Value = "Chrono";
        ws.Cell(headerRow, 4).Value = "Round";
        ws.Cell(headerRow, 5).Value = "Juge / Arbitre";
        ws.Cell(headerRow, 6).Value = "Côté / Athlète";
        ws.Cell(headerRow, 7).Value = "Action";
        ws.Cell(headerRow, 8).Value = "Valeur";
        ws.Cell(headerRow, 9).Value = "Score cumulé";
        ws.Cell(headerRow, 10).Value = "Match";
        ws.Cell(headerRow, 11).Value = "Détails / Code";

        var headerRange = ws.Range(headerRow, 1, headerRow, 11);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C5F8A");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int row = headerRow + 1;
        int? lastMatchId = null;
        bool useAlt = false;

        foreach (var sc in catScores)
        {
            if (sc.MatchId != lastMatchId)
            {
                useAlt = !useAlt;
                lastMatchId = sc.MatchId;
            }

            ws.Cell(row, 1).Value = sc.Timestamp.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = sc.Timestamp.ToString("HH:mm:ss");
            ws.Cell(row, 3).Value = !string.IsNullOrEmpty(sc.MatchTimeDisplay) ? sc.MatchTimeDisplay : "00:00";
            ws.Cell(row, 4).Value = sc.Round;
            ws.Cell(row, 5).Value = sc.JudgeName;

            string sideStr = sc.Side switch
            {
                Constants.Side.Red => "Rouge",
                Constants.Side.Blue => "Bleu",
                _ => (!string.IsNullOrWhiteSpace(sc.MatchCompetitors) ? sc.MatchCompetitors : "—")
            };
            ws.Cell(row, 6).Value = sideStr;
            ws.Cell(row, 7).Value = sc.ActionType;

            ws.Cell(row, 8).Value = sc.DeltaPoints;
            ws.Cell(row, 8).Style.NumberFormat.Format = "0.00";

            ws.Cell(row, 9).Value = sc.ResultingScore;
            ws.Cell(row, 9).Style.NumberFormat.Format = "0.00";

            ws.Cell(row, 10).Value = $"Match #{sc.MatchId}";
            ws.Cell(row, 11).Value = !string.IsNullOrWhiteSpace(sc.Description) 
                ? sc.Description 
                : (sc.DeductionCode?.Code?.Description ?? sc.DeductionCode?.Code?.Code ?? "—");

            var rowRange = ws.Range(row, 1, row, 11);
            if (sc.DeductionCode != null || sc.ActionType.Contains("Déduction"))
            {
                rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE4E4");
            }
            else
            {
                rowRange.Style.Fill.BackgroundColor = useAlt ? XLColor.FromHtml("#EEF4FB") : XLColor.White;
            }

            if (sc.DeltaPoints < 0)
                ws.Cell(row, 8).Style.Font.FontColor = XLColor.Red;
            else if (sc.DeltaPoints > 0)
                ws.Cell(row, 8).Style.Font.FontColor = XLColor.FromHtml("#1B7A34");

            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row++;
        }

        if (row > headerRow + 1)
        {
            ws.Range(headerRow, 1, row - 1, 11).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, row - 1, 11).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        ws.Column(1).Width = 12;
        ws.Column(2).Width = 11;
        ws.Column(3).Width = 11;
        ws.Column(4).Width = 8;
        ws.Column(5).Width = 16;
        ws.Column(6).Width = 20;
        ws.Column(7).Width = 15;
        ws.Column(8).Width = 11;
        ws.Column(9).Width = 13;
        ws.Column(10).Width = 14;
        ws.Column(11).Width = 32;

        ws.SheetView.FreezeRows(headerRow);
    }
}
