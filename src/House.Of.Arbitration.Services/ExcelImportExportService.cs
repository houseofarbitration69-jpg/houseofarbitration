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
        wsCat.Row(1).Style.Font.Bold = true;
        wsCat.Cell(1, 7).Style.Fill.BackgroundColor = XLColor.LightGray;

        // Ligne exemple
        wsCat.Cell(2, 1).Value = "Sanda";
        wsCat.Cell(2, 2).Value = "Men";
        wsCat.Cell(2, 3).Value = "60";
        wsCat.Cell(2, 4).Value = "65";
        wsCat.Cell(2, 5).Value = "Seniors";
        wsCat.Cell(2, 6).Value = "Knockouts";

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

        wsCat.Column(1).Width = 25;
        wsCat.Column(2).Width = 10;
        wsCat.Column(3).Width = 10;
        wsCat.Column(4).Width = 10;
        wsCat.Column(5).Width = 15;
        wsCat.Column(6).Width = 15;
        wsCat.Column(7).Width = 35;

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
}
