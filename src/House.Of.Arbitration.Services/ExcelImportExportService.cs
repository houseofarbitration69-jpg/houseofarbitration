#region Imports
using ClosedXML.Excel;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
#endregion

namespace House.Of.Arbitration.Services;

/// <summary>
/// Service d'import/export Excel pour les competitions
/// </summary>
public class ExcelImportExportService : IExcelImportExportService
{
    private readonly IRepository<CompetitionModel> _competitionRepository;
    private readonly IRepository<CategoryModel> _categoryRepository;
    private readonly IRepository<CompetitorModel> _competitorRepository;
    private readonly IRepository<CompetitorCategoryModel> _competitorCategoryRepository;
    private readonly IRepository<DrawModel> _drawRepository;

    public ExcelImportExportService(
        IRepository<CompetitionModel> competitionRepository,
        IRepository<CategoryModel> categoryRepository,
        IRepository<CompetitorModel> competitorRepository,
        IRepository<CompetitorCategoryModel> competitorCategoryRepository,
        IRepository<DrawModel> drawRepository)
    {
        _competitionRepository = competitionRepository;
        _categoryRepository = categoryRepository;
        _competitorRepository = competitorRepository;
        _competitorCategoryRepository = competitorCategoryRepository;
        _drawRepository = drawRepository;
    }

    /// <inheritdoc/>
    public Task<Stream> ExportTemplateAsync()
    {
        var workbook = new XLWorkbook();

        // ── Onglet "Competition" ──────────────────────────────────────────
        var wsComp = workbook.Worksheets.Add("Compétition");
        wsComp.Cell(1, 1).Value = "Nom";
        wsComp.Cell(1, 2).Value = "Date (JJ/MM/AAAA)";
        wsComp.Cell(1, 3).Value = "Type";
        wsComp.Row(1).Style.Font.Bold = true;

        wsComp.Cell(2, 1).Value = "Ma Compétition 2026";
        wsComp.Cell(2, 2).Value = "15/03/2026";
        wsComp.Cell(2, 3).Value = "Sanda";

        // Dropdown Type competition
        var compTypeValidation = wsComp.Range("C2:C1000").CreateDataValidation();
        compTypeValidation.List("\"Sanda,Taolu\"");

        wsComp.Column(1).Width = 30;
        wsComp.Column(2).Width = 20;
        wsComp.Column(3).Width = 15;

        // ── Onglet "Categories" ───────────────────────────────────────────
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

        // Ligne exemple
        wsCat.Cell(2, 1).Value = "Sanda";
        wsCat.Cell(2, 2).Value = "Men";
        wsCat.Cell(2, 3).Value = "60";
        wsCat.Cell(2, 4).Value = "65";
        wsCat.Cell(2, 5).Value = "Seniors";
        wsCat.Cell(2, 6).Value = "Knockouts";
        wsCat.Cell(2, 8).Value = "Oui";

        // Colonne G : formule de concatenation du nom affiche de la categorie
        // Produit ex : "Sanda Seniors Hommes -65kg"
        // Formule : =SI(A2="","", A2 & SI(E2<>""," "&E2,"") & " " & SI(B2="Men","Hommes",SI(B2="Women","Femmes","Mixte"))
        //            & SI(ET(C2<=0,D2>0)," -"&D2&"kg", SI(ET(C2>0,D2<=0)," +"&C2&"kg", SI(ET(C2>0,D2>0)," -"&D2&"kg",""))))
        // En syntaxe anglaise ClosedXML :
        const string formuleCatNom =
            "=IF(A{0}=\"\",\"\"," +
            "A{0}" +
            "&IF(E{0}<>\"\",\" \"&E{0},\"\")" +
            "&\" \"&IF(B{0}=\"Men\",\"Hommes\",IF(B{0}=\"Women\",\"Femmes\",\"Mixte\"))" +
            "&IF(AND(C{0}<=0,D{0}>0),\" -\"&D{0}&\"kg\"," +
              "IF(AND(C{0}>0,D{0}<=0),\" +\"&C{0}&\"kg\"," +
              "IF(AND(C{0}>0,D{0}>0),\" -\"&D{0}&\"kg\",\"\"))))";

        for (int r = 2; r <= 1000; r++)
        {
            wsCat.Cell(r, 7).FormulaA1 = string.Format(formuleCatNom, r);
            wsCat.Cell(r, 7).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Plage nommee → source du dropdown dans l'onglet Competiteurs
        workbook.DefinedNames.Add("ListeCategories", wsCat.Range("G2:G1000"));

        // Dropdowns categories
        var catTypeValidation = wsCat.Range("A2:A1000").CreateDataValidation();
        catTypeValidation.List("\"Sanda,Sanda Light,Sanda Wushu,Sanda Tradi,Taolu,Taolu Moderne Nord,Taolu Moderne Sud\"");

        var catGenreValidation = wsCat.Range("B2:B1000").CreateDataValidation();
        catGenreValidation.List("\"Men,Women,Mixte\"");

        var catAgeValidation = wsCat.Range("E2:E1000").CreateDataValidation();
        catAgeValidation.List("\"Cadets,Juniors,Seniors,Espoirs,Veterans\"");

        var catRoundValidation = wsCat.Range("F2:F1000").CreateDataValidation();
        catRoundValidation.List("\"Knockouts,Pools,Order\"");

        var catBronzeValidation = wsCat.Range("H2:H1000").CreateDataValidation();
        catBronzeValidation.List("\"Oui,Non\"");

        wsCat.Column(1).Width = 25;
        wsCat.Column(2).Width = 10;
        wsCat.Column(3).Width = 10;
        wsCat.Column(4).Width = 10;
        wsCat.Column(5).Width = 15;
        wsCat.Column(6).Width = 15;
        wsCat.Column(7).Width = 35;
        wsCat.Column(8).Width = 22;

        // ── Onglet "Competiteurs" ─────────────────────────────────────────
        var wsComp2 = workbook.Worksheets.Add("Compétiteurs");
        wsComp2.Cell(1, 1).Value = "Prénom";
        wsComp2.Cell(1, 2).Value = "Nom";
        wsComp2.Cell(1, 3).Value = "Genre";
        wsComp2.Cell(1, 4).Value = "DateNaissance (JJ/MM/AAAA)";
        wsComp2.Cell(1, 5).Value = "Club";
        wsComp2.Cell(1, 6).Value = "PaysISO";
        wsComp2.Cell(1, 7).Value = "Poids (kg)";
        wsComp2.Cell(1, 8).Value = "Catégorie";
        wsComp2.Row(1).Style.Font.Bold = true;

        wsComp2.Cell(2, 1).Value = "Jean";
        wsComp2.Cell(2, 2).Value = "Dupont";
        wsComp2.Cell(2, 3).Value = "Men";
        wsComp2.Cell(2, 4).Value = "15/03/2000";
        wsComp2.Cell(2, 5).Value = "Club Paris";
        wsComp2.Cell(2, 6).Value = "FRA";
        wsComp2.Cell(2, 7).Value = "65";
        wsComp2.Cell(2, 8).Value = "Sanda Seniors Hommes -65kg";

        // Dropdown Genre competiteur
        var compGenreValidation = wsComp2.Range("C2:C1000").CreateDataValidation();
        compGenreValidation.List("\"Men,Women,Mixte\"");

        // Dropdown Categorie → reference la plage nommee ListeCategories (col G de l'onglet Categories)
        var compCatValidation = wsComp2.Range("H2:H1000").CreateDataValidation();
        compCatValidation.List("ListeCategories");

        wsComp2.Column(1).Width = 15;
        wsComp2.Column(2).Width = 15;
        wsComp2.Column(3).Width = 10;
        wsComp2.Column(4).Width = 25;
        wsComp2.Column(5).Width = 20;
        wsComp2.Column(6).Width = 10;
        wsComp2.Column(7).Width = 12;
        wsComp2.Column(8).Width = 35;

        // ── Onglet "Guide" ────────────────────────────────────────────────
        var wsGuide = workbook.Worksheets.Add("Guide");
        int row = 1;

        wsGuide.Cell(row, 1).Value = "GUIDE D'UTILISATION";
        wsGuide.Cell(row, 1).Style.Font.Bold = true;
        wsGuide.Cell(row, 1).Style.Font.FontSize = 14;
        row += 2;

        wsGuide.Cell(row, 1).Value = "Types de categorie";
        wsGuide.Cell(row, 1).Style.Font.Bold = true;
        row++;
        wsGuide.Cell(row, 1).Value = "Sanda"; wsGuide.Cell(row, 2).Value = "Sanda classique";
        row++;
        wsGuide.Cell(row, 1).Value = "Sanda Light"; wsGuide.Cell(row, 2).Value = "Sanda light contact";
        row++;
        wsGuide.Cell(row, 1).Value = "Sanda Wushu"; wsGuide.Cell(row, 2).Value = "Sanda Wushu";
        row++;
        wsGuide.Cell(row, 1).Value = "Sanda Tradi"; wsGuide.Cell(row, 2).Value = "Sanda traditionnel";
        row++;
        wsGuide.Cell(row, 1).Value = "Taolu"; wsGuide.Cell(row, 2).Value = "Taolu classique";
        row++;
        wsGuide.Cell(row, 1).Value = "Taolu Moderne Nord"; wsGuide.Cell(row, 2).Value = "Taolu Moderne Nord";
        row++;
        wsGuide.Cell(row, 1).Value = "Taolu Moderne Sud"; wsGuide.Cell(row, 2).Value = "Taolu Moderne Sud";
        row += 2;

        wsGuide.Cell(row, 1).Value = "Tranches d'age";
        wsGuide.Cell(row, 1).Style.Font.Bold = true;
        row++;
        wsGuide.Cell(row, 1).Value = "Cadets"; wsGuide.Cell(row, 2).Value = "12 - 14 ans";
        row++;
        wsGuide.Cell(row, 1).Value = "Juniors"; wsGuide.Cell(row, 2).Value = "15 - 17 ans";
        row++;
        wsGuide.Cell(row, 1).Value = "Seniors"; wsGuide.Cell(row, 2).Value = "18 - 34 ans";
        row++;
        wsGuide.Cell(row, 1).Value = "Espoirs"; wsGuide.Cell(row, 2).Value = "18 - 21 ans";
        row++;
        wsGuide.Cell(row, 1).Value = "Veterans"; wsGuide.Cell(row, 2).Value = "35+ ans";
        row += 2;

        wsGuide.Cell(row, 1).Value = "Genres";
        wsGuide.Cell(row, 1).Style.Font.Bold = true;
        row++;
        wsGuide.Cell(row, 1).Value = "Men";
        row++;
        wsGuide.Cell(row, 1).Value = "Women";
        row++;
        wsGuide.Cell(row, 1).Value = "Mixte";
        row += 2;

        wsGuide.Cell(row, 1).Value = "Types de round";
        wsGuide.Cell(row, 1).Style.Font.Bold = true;
        row++;
        wsGuide.Cell(row, 1).Value = "Knockouts"; wsGuide.Cell(row, 2).Value = "Elimination directe";
        row++;
        wsGuide.Cell(row, 1).Value = "Pools"; wsGuide.Cell(row, 2).Value = "Poules";
        row++;
        wsGuide.Cell(row, 1).Value = "Order"; wsGuide.Cell(row, 2).Value = "Ordre de passage";
        row += 2;

        wsGuide.Cell(row, 1).Value = "Poids des compétiteurs";
        wsGuide.Cell(row, 1).Style.Font.Bold = true;
        row++;
        wsGuide.Cell(row, 1).Value = "Pour le Sanda, le poids est obligatoire et doit respecter les limites de la catégorie.";
        row++;
        wsGuide.Cell(row, 1).Value = "Pour le Taolu, le poids n'est pas vérifié et la colonne Poids peut être laissée vide.";
        row += 2;

        wsGuide.Cell(row, 1).Value = "IMPORTANT — Colonne NomCategorie (auto) dans l'onglet Categories";
        wsGuide.Cell(row, 1).Style.Font.Bold = true;
        row++;
        wsGuide.Cell(row, 1).Value = "La colonne G (fond gris) est calculee automatiquement. Ne pas la modifier.";
        row++;
        wsGuide.Cell(row, 1).Value = "Dans l'onglet Competiteurs, selectionnez la categorie dans la liste deroulante de la colonne Categorie.";

        wsGuide.Column(1).Width = 100;
        wsGuide.Column(2).Width = 30;

        // Retourner le stream
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return Task.FromResult<Stream>(stream);
    }

    /// <inheritdoc/>
    public async Task<ImportResult> ImportAsync(Stream excelStream)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        int importedCategories = 0;
        int importedCompetitors = 0;
        int importedDraws = 0;

        try
        {
            using var workbook = new XLWorkbook(excelStream);

            // ── 1. Lire onglet "Competition" ─────────────────────────────
            var wsComp = workbook.Worksheet("Compétition");

            var competitionName = wsComp.Cell(2, 1).GetString();
            var competitionTypeStr = wsComp.Cell(2, 3).GetString();

            if (string.IsNullOrWhiteSpace(competitionName))
            {
                errors.Add("Onglet Competition: le nom de la competition est vide.");
                return BuildResult(false, importedCategories, importedCompetitors, importedDraws, errors, warnings);
            }

            if (!TryParseCellDate(wsComp.Cell(2, 2), out DateTime competitionDate))
            {
                errors.Add($"Onglet Competition: date invalide '{wsComp.Cell(2, 2).GetString()}'. Format attendu: JJ/MM/AAAA.");
                return BuildResult(false, importedCategories, importedCompetitors, importedDraws, errors, warnings);
            }

            CompetitionType competitionType;
            if (competitionTypeStr == "Taolu")
                competitionType = CompetitionType.Taolu;
            else if (competitionTypeStr == "Sanda")
                competitionType = CompetitionType.Sanda;
            else
            {
                errors.Add($"Onglet Competition: type inconnu '{competitionTypeStr}'. Valeurs acceptees: Sanda, Taolu.");
                return BuildResult(false, importedCategories, importedCompetitors, importedDraws, errors, warnings);
            }

            var competition = new CompetitionModel
            {
                Name = competitionName,
                Date = competitionDate,
                Type = competitionType
            };

            var persistedCompetition = await _competitionRepository.AddAsync(competition);
            if (persistedCompetition == null)
            {
                errors.Add("Impossible de persister la competition en base de donnees.");
                return BuildResult(false, importedCategories, importedCompetitors, importedDraws, errors, warnings);
            }
            competition = persistedCompetition;

            // ── 2. Lire onglet "Categories" ───────────────────────────────
            var wsCat = workbook.Worksheet("Catégories");
            var categories = new List<CategoryModel>();

            int catRow = 2;
            while (!string.IsNullOrWhiteSpace(wsCat.Cell(catRow, 1).GetString()))
            {
                try
                {
                    var typeStr = wsCat.Cell(catRow, 1).GetString();
                    var genreStr = wsCat.Cell(catRow, 2).GetString();
                    var weightMinStr = wsCat.Cell(catRow, 3).GetString();
                    var weightMaxStr = wsCat.Cell(catRow, 4).GetString();
                    var ageLabel = wsCat.Cell(catRow, 5).GetString();
                    var roundTypeStr = wsCat.Cell(catRow, 6).GetString();
                    var bronzeStr = wsCat.Cell(catRow, 8).GetString()?.Trim();
                    bool hasBronzeMatch = bronzeStr != null && (bronzeStr.Equals("oui", StringComparison.OrdinalIgnoreCase) ||
                                                                bronzeStr.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                                                                bronzeStr.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                                                bronzeStr.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                                                                bronzeStr.Equals("vrai", StringComparison.OrdinalIgnoreCase));

                    if (!TryParseCategoryType(typeStr, out var categoryType))
                    {
                        errors.Add($"Categories ligne {catRow}: TypeCategorie inconnu '{typeStr}'.");
                        catRow++;
                        continue;
                    }

                    if (!Enum.TryParse<Genre>(genreStr, out var genre))
                    {
                        errors.Add($"Categories ligne {catRow}: Genre inconnu '{genreStr}'.");
                        catRow++;
                        continue;
                    }

                    if (!Enum.TryParse<RoundType>(roundTypeStr, out var roundType))
                    {
                        errors.Add($"Categories ligne {catRow}: TypeRound inconnu '{roundTypeStr}'.");
                        catRow++;
                        continue;
                    }

                    double weightMin = 0;
                    if (!string.IsNullOrWhiteSpace(weightMinStr))
                        double.TryParse(weightMinStr.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out weightMin);

                    double weightMax = 0;
                    if (!string.IsNullOrWhiteSpace(weightMaxStr))
                        double.TryParse(weightMaxStr.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out weightMax);

                    var ageRange = string.IsNullOrWhiteSpace(ageLabel)
                        ? null
                        : AgeRangeModel.DefaultRanges.FirstOrDefault(r => r.Label == ageLabel);

                    var category = new CategoryModel
                    {
                        Type = categoryType,
                        Genre = genre,
                        WeightMin = weightMin,
                        WeightMax = weightMax,
                        AgeRangeId = ageRange?.Id,
                        RoundType = roundType,
                        HasBronzeMatch = hasBronzeMatch,
                        CompetitionId = competition.Id
                    };

                    var persistedCategory = await _categoryRepository.AddAsync(category);
                    if (persistedCategory != null)
                    {
                        persistedCategory.AgeRange = ageRange;
                        categories.Add(persistedCategory);
                        importedCategories++;
                    }
                    else
                    {
                        category.AgeRange = ageRange;
                        errors.Add($"Categories ligne {catRow}: impossible de persister la categorie.");
                        categories.Add(category); // conserver l'alignement d'index
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Categories ligne {catRow}: erreur inattendue - {ex.Message}");
                }

                catRow++;
            }

            // ── 3. Lire onglet "Competiteurs" ─────────────────────────────
            var wsComp2 = workbook.Worksheet("Compétiteurs");
            int compRow = 2;

            // Construire un dictionnaire nom affiche → CategoryModel pour le matching par nom
            var categoryByName = new Dictionary<string, CategoryModel>(StringComparer.OrdinalIgnoreCase);
            // Dictionnaire pour collecter les compétiteurs par catégorie pour la génération des tirages
            var competitorsByCategoryId = new Dictionary<int, List<CompetitorModel>>();

            foreach (var cat in categories)
            {
                var displayName = BuildCategoryDisplayName(cat.Type, cat.Genre, cat.WeightMin, cat.WeightMax, cat.AgeRange?.Label);
                if (!categoryByName.ContainsKey(displayName))
                    categoryByName[displayName] = cat;

                competitorsByCategoryId[cat.Id] = new List<CompetitorModel>();
            }

            while (!string.IsNullOrWhiteSpace(wsComp2.Cell(compRow, 1).GetString()))
            {
                try
                {
                    var firstName = wsComp2.Cell(compRow, 1).GetString();
                    var lastName = wsComp2.Cell(compRow, 2).GetString();
                    var genreStr = wsComp2.Cell(compRow, 3).GetString();
                    var club = wsComp2.Cell(compRow, 5).GetString();
                    var countryIso = wsComp2.Cell(compRow, 6).GetString();
                    var catNameStr = wsComp2.Cell(compRow, 8).GetString().Trim();

                    bool rowHasError = false;

                    if (!Enum.TryParse<Genre>(genreStr, out var genre))
                    {
                        errors.Add($"Competiteurs ligne {compRow}: Genre inconnu '{genreStr}'.");
                        rowHasError = true;
                    }

                    if (!TryParseCellDate(wsComp2.Cell(compRow, 4), out DateTime birthDate))
                    {
                        errors.Add($"Competiteurs ligne {compRow}: date de naissance invalide '{wsComp2.Cell(compRow, 4).GetString()}'. Format attendu: JJ/MM/AAAA.");
                        rowHasError = true;
                    }

                    // Resolution de la categorie par nom (dropdown) ou par index numerique (retro-compat)
                    if (string.IsNullOrWhiteSpace(catNameStr))
                    {
                        errors.Add($"Competiteurs ligne {compRow}: la categorie est vide. Selectionnez une categorie dans la liste deroulante.");
                        compRow++;
                        continue;
                    }

                    CategoryModel? category = null;
                    if (!categoryByName.TryGetValue(catNameStr, out category))
                    {
                        // Fallback : si l'utilisateur a saisi un numero (ancien format)
                        if (int.TryParse(catNameStr, out int catIdx) && catIdx >= 1 && catIdx <= categories.Count)
                            category = categories[catIdx - 1];
                        else
                        {
                            errors.Add($"Competiteurs ligne {compRow}: categorie introuvable '{catNameStr}'. Utilisez la liste deroulante issue de l'onglet Categories.");
                            compRow++;
                            continue;
                        }
                    }

                    bool isTaolu = competition.Type == CompetitionType.Taolu ||
                                   category.Type == CategoryType.Taolu ||
                                   category.Type == CategoryType.TaoluModerneNord ||
                                   category.Type == CategoryType.TaoluModerneSud;

                    var weightCell = wsComp2.Cell(compRow, 7);
                    double weight = 0;
                    bool weightOk = false;

                    if (isTaolu && (weightCell.IsEmpty() || string.IsNullOrWhiteSpace(weightCell.GetString())))
                    {
                        // En mode Taolu, le poids est optionnel et peut etre vide
                        weight = 0;
                        weightOk = true;
                    }
                    else if (weightCell.TryGetValue<double>(out weight))
                    {
                        weightOk = true;
                    }
                    else
                    {
                        var weightStr = weightCell.GetString()?.Trim().Replace(',', '.');
                        if (isTaolu && string.IsNullOrWhiteSpace(weightStr))
                        {
                            weight = 0;
                            weightOk = true;
                        }
                        else
                        {
                            weightOk = !string.IsNullOrWhiteSpace(weightStr) &&
                                       double.TryParse(weightStr, System.Globalization.NumberStyles.Any,
                                           System.Globalization.CultureInfo.InvariantCulture, out weight);
                        }
                    }

                    if (!weightOk)
                    {
                        errors.Add($"Competiteurs ligne {compRow}: poids invalide '{weightCell.GetString()}'.");
                        rowHasError = true;
                    }

                    if (rowHasError)
                    {
                        compRow++;
                        continue;
                    }

                    var categoryName = BuildCategoryDisplayName(category.Type, category.Genre, category.WeightMin, category.WeightMax, category.AgeRange?.Label);

                    // Validation age
                    int age = competition.Date.Year - birthDate.Year;
                    if (birthDate.AddYears(age) > competition.Date) age--;

                    int errorsBeforeValidation = errors.Count;

                    if (category.AgeRange != null && category.AgeRange.Id != -1)
                    {
                        if (age < category.AgeRange.MinAge)
                            errors.Add($"Ligne {compRow}: {firstName} {lastName} trop jeune ({age} ans) pour la categorie {categoryName} (minimum {category.AgeRange.MinAge} ans)");
                        if (category.AgeRange.MaxAge > 0 && age > category.AgeRange.MaxAge)
                            errors.Add($"Ligne {compRow}: {firstName} {lastName} trop age ({age} ans) pour la categorie {categoryName} (maximum {category.AgeRange.MaxAge} ans)");
                    }

                    // Validation poids (uniquement pour les categories de combat comme le Sanda)
                    if (!isTaolu)
                    {
                        if (category.WeightMin > 0 && weight < category.WeightMin)
                            errors.Add($"Ligne {compRow}: {firstName} {lastName} sous le poids minimum ({weight} kg) pour la categorie {categoryName} (minimum {category.WeightMin} kg)");
                        if (category.WeightMax > 0 && weight > category.WeightMax)
                            errors.Add($"Ligne {compRow}: {firstName} {lastName} au-dessus du poids maximum ({weight} kg) pour la categorie {categoryName} (maximum {category.WeightMax} kg)");
                    }

                    // Si des erreurs de validation ont ete ajoutees pour cette ligne, skip l'insertion
                    bool hasValidationErrors = errors.Count > errorsBeforeValidation;

                    if (!hasValidationErrors)
                    {
                        var competitor = new CompetitorModel
                        {
                            FirstName = firstName,
                            LastName = lastName,
                            Genre = genre,
                            BirthDate = birthDate,
                            Club = club,
                            CountryIsoCode = string.IsNullOrWhiteSpace(countryIso) ? null : countryIso,
                            Weight = weight
                        };

                        var persistedCompetitor = await _competitorRepository.AddAsync(competitor);
                        if (persistedCompetitor != null)
                        {
                            var competitorCategory = new CompetitorCategoryModel
                            {
                                CompetitorId = persistedCompetitor.Id,
                                CategoryId = category.Id
                            };
                            await _competitorCategoryRepository.AddAsync(competitorCategory);
                            importedCompetitors++;

                            // Enregistrer le compétiteur pour la génération des tirages
                            competitorsByCategoryId[category.Id].Add(persistedCompetitor);
                        }
                        else
                        {
                            errors.Add($"Competiteurs ligne {compRow}: impossible de persister le competiteur {firstName} {lastName}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Competiteurs ligne {compRow}: erreur inattendue - {ex.Message}");
                }

                compRow++;
            }

            // ── 4. Generer les tirages pour chaque categorie importee ─────────
            int globalOrder = 1;

            foreach (var category in categories)
            {
                try
                {
                    var catCompetitors = competitorsByCategoryId.TryGetValue(category.Id, out var compList)
                        ? compList
                        : new List<CompetitorModel>();

                    if (catCompetitors.Count > 0)
                    {
                        var draw = GenerateDrawForCategory(category, catCompetitors, ref globalOrder);
                        if (draw != null)
                        {
                            _drawRepository.ClearTracker();
                            var persistedDraw = await _drawRepository.AddAsync(draw);
                            if (persistedDraw != null)
                            {
                                importedDraws++;
                            }
                            else
                            {
                                warnings.Add($"Impossible de persister le tirage pour la categorie {category.Name}.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Erreur lors de la generation du tirage pour la categorie {category.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Erreur lors de l'ouverture ou du traitement du fichier Excel: {ex.Message}");
            return BuildResult(false, importedCategories, importedCompetitors, importedDraws, errors, warnings);
        }

        return BuildResult(!errors.Any(), importedCategories, importedCompetitors, importedDraws, errors, warnings);
    }

    private static ImportResult BuildResult(bool success, int importedCategories, int importedCompetitors, int importedDraws,
        List<string> errors, List<string> warnings) => new ImportResult
        {
            Success = success,
            ImportedCategories = importedCategories,
            ImportedCompetitors = importedCompetitors,
            ImportedDraws = importedDraws,
            Errors = errors,
            Warnings = warnings
        };

    /// <summary>
    /// Genere un DrawModel pour une categorie donnee en placant aleatoirement les competiteurs.
    /// </summary>
    private static DrawModel? GenerateDrawForCategory(CategoryModel category, List<CompetitorModel> competitors, ref int globalOrder)
    {
        if (competitors.Count == 0) return null;

        // Melange aleatoire des competiteurs pour le tirage
        var shuffled = competitors.OrderBy(_ => Random.Shared.Next()).ToList();

        // Les categories Taolu utilisent toujours l'ordre de passage
        var roundType = (category.Type == CategoryType.Taolu ||
                         category.Type == CategoryType.TaoluModerneNord ||
                         category.Type == CategoryType.TaoluModerneSud)
            ? RoundType.Order
            : category.RoundType;

        var draw = new DrawModel
        {
            CategoryId = category.Id,
            DrawKnockouts = new List<DrawKnockoutModel>(),
            DrawPools = new List<DrawPoolsModel>(),
            DrawOrders = new List<DrawOrderModel>()
        };

        if (roundType == RoundType.Knockouts)
        {
            int n = shuffled.Count;
            // Trouver la prochaine puissance de 2
            int m = 1;
            while (m < n) m *= 2;
            if (m < 2) m = 2;
            int matchesInRound = m / 2;

            // Premier tour : inclure les byes (passages automatiques)
            int matchOrder = 1;
            for (int i = 0; i < matchesInRound; i++)
            {
                var comp1 = (i * 2 < n) ? shuffled[i * 2] : null;
                var comp2 = (i * 2 + 1 < n) ? shuffled[i * 2 + 1] : null;
                bool hasRealMatch = comp1 != null && comp2 != null;

                var knockout = new DrawKnockoutModel
                {
                    Draw = draw,
                    Order = matchOrder++,
                    GlobalOrder = hasRealMatch ? globalOrder++ : 0,
                    Competitor1Id = comp1?.Id,
                    Competitor2Id = comp2?.Id
                };

                // Si un seul competiteur dans ce match : bye automatique
                if (comp1 != null && comp2 == null)
                {
                    knockout.WinnerId = comp1.Id;
                    knockout.IsFinished = true;
                }
                else if (comp1 == null && comp2 != null)
                {
                    knockout.WinnerId = comp2.Id;
                    knockout.IsFinished = true;
                }

                draw.DrawKnockouts!.Add(knockout);
            }

            // Tours suivants (cases vides pour les resultats a venir)
            int currentMatches = matchesInRound;
            int offset = matchesInRound;
            while (currentMatches > 1)
            {
                currentMatches /= 2;
                for (int i = 0; i < currentMatches; i++)
                {
                    draw.DrawKnockouts!.Add(new DrawKnockoutModel
                    {
                        Draw = draw,
                        Order = offset + i + 1,
                        GlobalOrder = globalOrder++
                    });
                }
                offset += currentMatches;
            }

            // Si la catégorie a une petite finale et au moins 4 compétiteurs (au moins des demi-finales)
            if (category.HasBronzeMatch && matchesInRound >= 2 && draw.DrawKnockouts!.Count > 0)
            {
                var finalMatch = draw.DrawKnockouts.Last();
                int bronzeOrder = finalMatch.Order;
                finalMatch.Order = bronzeOrder + 1;

                var bronzeMatch = new DrawKnockoutModel
                {
                    Draw = draw,
                    Order = bronzeOrder,
                    GlobalOrder = globalOrder++,
                    IsBronzeMatch = true
                };

                // Insérer la petite finale juste avant la finale
                draw.DrawKnockouts.Insert(draw.DrawKnockouts.Count - 1, bronzeMatch);
            }
        }
        else if (roundType == RoundType.Pools)
        {
            int n = shuffled.Count;
            int matchOrder = 1;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    draw.DrawPools!.Add(new DrawPoolsModel
                    {
                        Draw = draw,
                        Order = matchOrder++,
                        GlobalOrder = globalOrder++,
                        Competitor1Id = shuffled[i].Id,
                        Competitor2Id = shuffled[j].Id
                    });
                }
            }
        }
        else // Order (Taolu ou explicite)
        {
            for (int i = 0; i < shuffled.Count; i++)
            {
                draw.DrawOrders!.Add(new DrawOrderModel
                {
                    Draw = draw,
                    Order = i + 1,
                    GlobalOrder = globalOrder++,
                    CompetitorId = shuffled[i].Id
                });
            }
        }

        return draw;
    }

    /// <summary>
    /// Retourne le nom d'affichage convivial d'un CategoryType (avec espaces, ex: 'Taolu Moderne Nord', 'Sanda Light').
    /// </summary>
    public static string GetCategoryTypeDisplayName(CategoryType type) => type switch
    {
        CategoryType.Sanda => "Sanda",
        CategoryType.SandaLight => "Sanda Light",
        CategoryType.SandaWushu => "Sanda Wushu",
        CategoryType.SandaTradi => "Sanda Tradi",
        CategoryType.Taolu => "Taolu",
        CategoryType.TaoluModerneNord => "Taolu Moderne Nord",
        CategoryType.TaoluModerneSud => "Taolu Moderne Sud",
        _ => type.ToString()
    };

    /// <summary>
    /// Parse le type de categorie a partir d'une chaine (avec ou sans espaces).
    /// Supporte par exemple 'Taolu Moderne Nord' ou 'TaoluModerneNord', insensible a la casse.
    /// </summary>
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

    /// <summary>
    /// Construit le nom affiche d'une categorie, identique a la formule Excel colonne G.
    /// Exemple : "Sanda Seniors Hommes -65kg", "Taolu Cadets Femmes", "Taolu Moderne Nord Mixte"
    /// </summary>
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

        var parts = new List<string> { GetCategoryTypeDisplayName(type) };
        if (!string.IsNullOrWhiteSpace(ageLabel))
            parts.Add(ageLabel);
        parts.Add(genreStr);

        return string.Join(" ", parts) + weightStr;
    }

    /// <summary>
    /// Tente d'extraire une date d'une cellule Excel, qu'elle soit typée DateTime par Excel ou sous forme de texte.
    /// Supporte les formats JJ/MM/AAAA, AAAA-MM-JJ, avec ou sans heure.
    /// </summary>
    public static bool TryParseCellDate(IXLCell cell, out DateTime result)
    {
        // 1. Cellule typée DateTime ou serial number reconnu par ClosedXML
        if (cell.TryGetValue<DateTime>(out result))
        {
            return true;
        }

        // 2. Traitement textuel
        var str = cell.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(str))
        {
            result = default;
            return false;
        }

        // Si ClosedXML / Excel a concaténé l'heure (ex: "15/03/2000 00:00:00"), isoler la partie date
        var dateOnlyStr = str.Split(' ')[0].Trim();

        string[] formats = new[]
        {
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd/MM/yyyy HH:mm:ss",
            "d/M/yyyy HH:mm:ss",
            "dd/MM/yyyy H:mm:ss",
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "dd-MM-yyyy",
            "d-M-yyyy"
        };

        if (DateTime.TryParseExact(str, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParseExact(dateOnlyStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParse(str, new System.Globalization.CultureInfo("fr-FR"), System.Globalization.DateTimeStyles.None, out result))
            return true;

        return DateTime.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result);
    }

    // ══════════════════════════════════════════════════════════════════
    // Export Résultats
    // ══════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public Task<Stream> ExportResultsAsync(string competitionName, DateTime? competitionDate, IEnumerable<CategoryResultExportData> categories)
    {
        var workbook = new XLWorkbook();

        var categoriesList = categories.ToList();

        // ── Onglet Résumé ─────────────────────────────────────────────
        var wsSummary = workbook.Worksheets.Add("Résumé");
        StyleSummarySheet(wsSummary, competitionName, competitionDate, categoriesList);

        // ── Un onglet par catégorie ───────────────────────────────────
        foreach (var cat in categoriesList)
        {
            // Les noms d'onglets Excel sont limités à 31 caractères
            string sheetName = cat.CategoryName.Length > 31
                ? cat.CategoryName[..31]
                : cat.CategoryName;

            // Dédoublonner si nécessaire
            int suffix = 2;
            string originalName = sheetName;
            while (workbook.Worksheets.Any(ws => ws.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase)))
            {
                string suffixStr = $" ({suffix})";
                sheetName = originalName.Length + suffixStr.Length > 31
                    ? originalName[..(31 - suffixStr.Length)] + suffixStr
                    : originalName + suffixStr;
                suffix++;
            }

            var ws = workbook.Worksheets.Add(sheetName);
            StyleCategorySheet(ws, cat, competitionName, competitionDate);
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }

    private static void StyleSummarySheet(IXLWorksheet ws, string competitionName, DateTime? competitionDate, List<CategoryResultExportData> categories)
    {
        // En-tête
        ws.Cell(1, 1).Value = competitionName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 6).Merge();

        if (competitionDate.HasValue)
        {
            ws.Cell(2, 1).Value = competitionDate.Value.ToString("dd/MM/yyyy");
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 6).Merge();
        }

        // En-têtes colonnes
        int headerRow = 4;
        ws.Cell(headerRow, 1).Value = "Catégorie";
        ws.Cell(headerRow, 2).Value = "Type";
        ws.Cell(headerRow, 3).Value = "🥇 1er";
        ws.Cell(headerRow, 4).Value = "🥈 2e";
        ws.Cell(headerRow, 5).Value = "🥉 3e";
        ws.Cell(headerRow, 6).Value = "Nb. compétiteurs";

        var headerRange = ws.Range(headerRow, 1, headerRow, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Données
        int row = headerRow + 1;
        foreach (var cat in categories)
        {
            ws.Cell(row, 1).Value = cat.CategoryName;
            ws.Cell(row, 2).Value = cat.CategoryType;
            ws.Cell(row, 3).Value = cat.Rows.FirstOrDefault(r => r.Position == 1)?.CompetitorName ?? "—";
            ws.Cell(row, 4).Value = cat.Rows.FirstOrDefault(r => r.Position == 2)?.CompetitorName ?? "—";
            ws.Cell(row, 5).Value = cat.Rows.FirstOrDefault(r => r.Position == 3)?.CompetitorName ?? "—";
            ws.Cell(row, 6).Value = cat.Rows.Count;
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (row % 2 == 0)
                ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");

            row++;
        }

        // Bordures
        ws.Range(headerRow, 1, row - 1, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(headerRow, 1, row - 1, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Largeurs colonnes
        ws.Column(1).Width = 35;
        ws.Column(2).Width = 15;
        ws.Column(3).Width = 25;
        ws.Column(4).Width = 25;
        ws.Column(5).Width = 25;
        ws.Column(6).Width = 18;
    }

    private static void StyleCategorySheet(IXLWorksheet ws, CategoryResultExportData cat, string competitionName, DateTime? competitionDate)
    {
        // En-tête compétition
        ws.Cell(1, 1).Value = competitionName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 7).Merge();

        if (competitionDate.HasValue)
        {
            ws.Cell(2, 1).Value = competitionDate.Value.ToString("dd/MM/yyyy");
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 7).Merge();
        }

        // Nom catégorie
        ws.Cell(3, 1).Value = $"{cat.CategoryName} — {cat.CategoryType}";
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 1).Style.Font.FontSize = 12;
        ws.Cell(3, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.White;
        ws.Range(3, 1, 3, 7).Merge();

        // En-têtes colonnes
        int headerRow = 5;
        ws.Cell(headerRow, 1).Value = "Rang";
        ws.Cell(headerRow, 2).Value = "Médaille";
        ws.Cell(headerRow, 3).Value = "Compétiteur";
        ws.Cell(headerRow, 4).Value = "Club";
        ws.Cell(headerRow, 5).Value = "Pays";
        ws.Cell(headerRow, 6).Value = "Score / Résultat";
        ws.Cell(headerRow, 7).Value = "Statut";

        var headerRange = ws.Range(headerRow, 1, headerRow, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C5F8A");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Données
        int row = headerRow + 1;
        foreach (var r in cat.Rows.OrderBy(x => x.Position))
        {
            ws.Cell(row, 1).Value = r.Position;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 2).Value = r.Medal;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 3).Value = r.CompetitorName;
            ws.Cell(row, 4).Value = r.Club;
            ws.Cell(row, 5).Value = r.Country;
            ws.Cell(row, 6).Value = r.Score;
            ws.Cell(row, 7).Value = r.Status;

            // Coloration selon position
            var rowStyle = ws.Range(row, 1, row, 7).Style;
            if (r.Position == 1)
                rowStyle.Fill.BackgroundColor = XLColor.FromHtml("#FFF8DC"); // or pâle
            else if (r.Position == 2)
                rowStyle.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5"); // argent pâle
            else if (r.Position == 3)
                rowStyle.Fill.BackgroundColor = XLColor.FromHtml("#FFF0E6"); // bronze pâle
            else if (row % 2 == 0)
                rowStyle.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");

            row++;
        }

        // Bordures
        if (row > headerRow + 1)
            ws.Range(headerRow, 1, row - 1, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(headerRow, 1, row - 1, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Largeurs colonnes
        ws.Column(1).Width = 8;
        ws.Column(2).Width = 10;
        ws.Column(3).Width = 28;
        ws.Column(4).Width = 22;
        ws.Column(5).Width = 10;
        ws.Column(6).Width = 18;
        ws.Column(7).Width = 20;
    }

    // ══════════════════════════════════════════════════════════════════
    // Export Notes Arbitres
    // ══════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public Task<Stream> ExportRefereeDataAsync(string competitionName, DateTime? competitionDate, IEnumerable<CategoryRefereeExportData> categories)
    {
        var workbook = new XLWorkbook();
        var categoriesList = categories.ToList();

        // ── Onglet Résumé ─────────────────────────────────────────────
        var wsSummary = workbook.Worksheets.Add("Résumé");
        StyleRefereeeSummarySheet(wsSummary, competitionName, competitionDate, categoriesList);

        // ── Un onglet par catégorie ───────────────────────────────────
        foreach (var cat in categoriesList)
        {
            string sheetName = cat.CategoryName.Length > 31
                ? cat.CategoryName[..31]
                : cat.CategoryName;

            int suffix = 2;
            string originalName = sheetName;
            while (workbook.Worksheets.Any(ws => ws.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase)))
            {
                string suffixStr = $" ({suffix})";
                sheetName = originalName.Length + suffixStr.Length > 31
                    ? originalName[..(31 - suffixStr.Length)] + suffixStr
                    : originalName + suffixStr;
                suffix++;
            }

            var ws = workbook.Worksheets.Add(sheetName);
            StyleRefereeDataSheet(ws, cat, competitionName, competitionDate);
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }

    private static void StyleRefereeeSummarySheet(IXLWorksheet ws, string competitionName, DateTime? competitionDate, List<CategoryRefereeExportData> categories)
    {
        ws.Cell(1, 1).Value = $"Notes des arbitres — {competitionName}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 5).Merge();

        if (competitionDate.HasValue)
        {
            ws.Cell(2, 1).Value = competitionDate.Value.ToString("dd/MM/yyyy");
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 5).Merge();
        }

        int headerRow = 4;
        ws.Cell(headerRow, 1).Value = "Catégorie";
        ws.Cell(headerRow, 2).Value = "Nb. notes";
        ws.Cell(headerRow, 3).Value = "Nb. corrections";
        ws.Cell(headerRow, 4).Value = "Arbitres distincts";
        ws.Cell(headerRow, 5).Value = "Plage horaire";

        var headerRange = ws.Range(headerRow, 1, headerRow, 5);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int row = headerRow + 1;
        foreach (var cat in categories)
        {
            var referees = cat.Rows.Select(r => r.Referee).Distinct().Count();
            var corrections = cat.Rows.Count(r => r.IsCorrection);
            var minDate = cat.Rows.Count > 0 ? cat.Rows.Min(r => r.Date) : (DateTime?)null;
            var maxDate = cat.Rows.Count > 0 ? cat.Rows.Max(r => r.Date) : (DateTime?)null;
            string plage = (minDate.HasValue && maxDate.HasValue)
                ? $"{minDate.Value:HH:mm:ss} → {maxDate.Value:HH:mm:ss}"
                : "—";

            ws.Cell(row, 1).Value = cat.CategoryName;
            ws.Cell(row, 2).Value = cat.Rows.Count;
            ws.Cell(row, 3).Value = corrections;
            ws.Cell(row, 4).Value = referees;
            ws.Cell(row, 5).Value = plage;

            foreach (var col in new[] { 2, 3, 4 })
                ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (row % 2 == 0)
                ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");

            row++;
        }

        ws.Range(headerRow, 1, row - 1, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(headerRow, 1, row - 1, 5).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Column(1).Width = 35;
        ws.Column(2).Width = 12;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 20;
        ws.Column(5).Width = 25;
    }

    private static void StyleRefereeDataSheet(IXLWorksheet ws, CategoryRefereeExportData cat, string competitionName, DateTime? competitionDate)
    {
        // En-tête
        ws.Cell(1, 1).Value = $"Notes arbitres — {competitionName}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Range(1, 1, 1, 9).Merge();

        if (competitionDate.HasValue)
        {
            ws.Cell(2, 1).Value = competitionDate.Value.ToString("dd/MM/yyyy");
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 9).Merge();
        }

        ws.Cell(3, 1).Value = cat.CategoryName;
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 1).Style.Font.FontSize = 12;
        ws.Cell(3, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.White;
        ws.Range(3, 1, 3, 9).Merge();

        // En-têtes colonnes
        int headerRow = 5;
        ws.Cell(headerRow, 1).Value = "Date";
        ws.Cell(headerRow, 2).Value = "Heure";
        ws.Cell(headerRow, 3).Value = "Arbitre";
        ws.Cell(headerRow, 4).Value = "Nom";
        ws.Cell(headerRow, 5).Value = "Prénom";
        ws.Cell(headerRow, 6).Value = "Note";
        ws.Cell(headerRow, 7).Value = "Round";
        ws.Cell(headerRow, 8).Value = "Correction";
        ws.Cell(headerRow, 9).Value = "Match (ID / Ordre global)";

        var headerRange = ws.Range(headerRow, 1, headerRow, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C5F8A");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Données
        int row = headerRow + 1;
        string? lastMatchKey = null;
        XLColor altColor1 = XLColor.White;
        XLColor altColor2 = XLColor.FromHtml("#EEF4FB");
        bool useAlt = false;

        // Trier : par match (GlobalOrder), puis par heure
        var sorted = cat.Rows
            .OrderBy(r => r.GlobalOrder ?? int.MaxValue)
            .ThenBy(r => r.Date)
            .ToList();

        foreach (var r in sorted)
        {
            // Alterner couleur de fond par match pour faciliter la lecture
            string matchKey = $"{r.MatchType}_{r.MatchId}";
            if (matchKey != lastMatchKey)
            {
                useAlt = !useAlt;
                lastMatchKey = matchKey;
            }

            ws.Cell(row, 1).Value = r.Date.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = r.Date.ToString("HH:mm:ss");
            ws.Cell(row, 3).Value = r.Referee;
            ws.Cell(row, 4).Value = r.CompetitorLastName;
            ws.Cell(row, 5).Value = r.CompetitorFirstName;
            ws.Cell(row, 6).Value = r.Score;
            ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 7).Value = r.Round;
            ws.Cell(row, 8).Value = r.IsCorrection ? "✓" : "";
            ws.Cell(row, 9).Value = r.MatchId.HasValue
                ? $"{r.MatchType} #{r.MatchId} (ord. {r.GlobalOrder})"
                : "—";

            // Coloration : rouge clair pour corrections, alternance par match sinon
            if (r.IsCorrection)
                ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE4E4");
            else
                ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = useAlt ? altColor2 : altColor1;

            // Note négative en rouge, positive en vert
            if (r.Score < 0)
                ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;
            else if (r.Score > 0)
                ws.Cell(row, 6).Style.Font.FontColor = XLColor.FromHtml("#1B7A34");

            foreach (var col in new[] { 1, 2, 7, 8 })
                ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row++;
        }

        // Bordures
        if (row > headerRow + 1)
            ws.Range(headerRow, 1, row - 1, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(headerRow, 1, row - 1, 9).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Largeurs
        ws.Column(1).Width = 12;
        ws.Column(2).Width = 11;
        ws.Column(3).Width = 14;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 16;
        ws.Column(6).Width = 10;
        ws.Column(7).Width = 8;
        ws.Column(8).Width = 12;
        ws.Column(9).Width = 28;

        // Figer la ligne d'en-tête
        ws.SheetView.FreezeRows(headerRow);
    }
}
