#region Imports
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;
using House.Of.Arbitration.Services.Abstractions;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Globalization;

// Désambiguïsation avec les usings globaux MAUI (Microsoft.Maui.Graphics / Microsoft.Maui.Controls)
using Font = iTextSharp.text.Font;
using Element = iTextSharp.text.Element;
using Rectangle = iTextSharp.text.Rectangle;
using Image = iTextSharp.text.Image;
#endregion

namespace House.Of.Arbitration.Services;

/// <summary>
/// Service d'export PDF des tirages de compétition (knockout, poules, taolu).
/// Utilise iTextSharp.LGPLv2.Core (100% C# géré, compatible Windows et Android).
/// </summary>
public class DrawPdfExportService : IDrawPdfExportService
{
    private static readonly BaseColor ColorPrimary = new(21, 101, 192);     // #1565C0
    private static readonly BaseColor ColorHeader = new(25, 118, 210);      // #1976D2
    private static readonly BaseColor ColorRowAlt = new(238, 244, 251);     // #EEF4FB
    private static readonly BaseColor ColorSectionBg = new(227, 240, 251);  // #E3F0FB
    private static readonly BaseColor ColorBorder = new(176, 196, 222);     // #B0C4DE
    private static readonly BaseColor ColorBronze = new(205, 127, 50);      // #CD7F32
    private static readonly BaseColor ColorGreen = new(56, 142, 60);        // #388E3C
    private static readonly BaseColor ColorRed = new(211, 47, 47);          // #D32F2F
    private static readonly BaseColor ColorMuted = new(100, 100, 100);

    private readonly BaseFont _bfHelvetica;
    private readonly BaseFont _bfHelveticaBold;
    private readonly BaseFont _bfHelveticaOblique;

    private static readonly Dictionary<string, byte[]> _flagBytesCache = new(StringComparer.OrdinalIgnoreCase);

    public DrawPdfExportService()
    {
        _bfHelvetica = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
        _bfHelveticaBold = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
        _bfHelveticaOblique = BaseFont.CreateFont(BaseFont.HELVETICA_OBLIQUE, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
    }

    /// <summary>
    /// Résout le code ISO (2 lettres, minuscules) du pays d'un compétiteur.
    /// </summary>
    private static string? ResolveIsoCode(CompetitorModel? competitor)
    {
        if (competitor == null) return null;

        if (!string.IsNullOrWhiteSpace(competitor.CountryIsoCode))
        {
            return competitor.CountryIsoCode.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(competitor.Country?.IsoCode))
        {
            return competitor.Country.IsoCode.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(competitor.Country?.Name))
        {
            var found = CountryModel.DefaultCountries
                .FirstOrDefault(c => string.Equals(c.Name, competitor.Country.Name, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                return found.IsoCode.ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>
    /// Charge l'image du drapeau correspondante depuis les ressources embarquées.
    /// </summary>
    private static Image? GetFlagImage(string? isoCode)
    {
        if (string.IsNullOrWhiteSpace(isoCode)) return null;
        string code = isoCode.Trim().ToLowerInvariant();

        byte[]? bytes;
        lock (_flagBytesCache)
        {
            if (!_flagBytesCache.TryGetValue(code, out bytes))
            {
                string resName = $"House.Of.Arbitration.Services.Flags.{code}.png";
                using var stream = typeof(DrawPdfExportService).Assembly.GetManifestResourceStream(resName);
                if (stream == null)
                {
                    _flagBytesCache[code] = Array.Empty<byte>();
                    return null;
                }

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                bytes = ms.ToArray();
                _flagBytesCache[code] = bytes;
            }
        }

        if (bytes == null || bytes.Length == 0) return null;

        try
        {
            return Image.GetInstance(bytes);
        }
        catch
        {
            return null;
        }
    }

    public Task<Stream> ExportKnockoutBracketAsync(DrawModel draw, List<DrawKnockoutModel> knockouts, string competitionName)
    {
        var stream = new MemoryStream();
        var doc = new Document(PageSize.A4.Rotate(), 20, 20, 20, 25);
        var writer = PdfWriter.GetInstance(doc, stream);
        writer.PageEvent = new PageFooterEvent(_bfHelvetica);

        doc.Open();
        AddTitleBanner(doc, competitionName, draw.Category?.Name ?? "Catégorie", "Tableau éliminatoire (Sanda)");
        doc.Add(new Paragraph(" ") { SpacingAfter = 5 });
        BuildKnockoutBracket(doc, knockouts);
        doc.Close();

        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }

    public Task<Stream> ExportPoolsAsync(DrawModel draw, List<DrawPoolsModel> pools, string competitionName)
    {
        var stream = new MemoryStream();
        var doc = new Document(PageSize.A4, 25, 25, 25, 25);
        var writer = PdfWriter.GetInstance(doc, stream);
        writer.PageEvent = new PageFooterEvent(_bfHelvetica);

        doc.Open();
        AddTitleBanner(doc, competitionName, draw.Category?.Name ?? "Catégorie", "Tableau de poules (Sanda)");
        doc.Add(new Paragraph(" ") { SpacingAfter = 5 });
        BuildPoolsTable(doc, pools);
        doc.Close();

        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }

    public Task<Stream> ExportTaoluOrderAsync(DrawModel draw, List<DrawOrderModel> orders, string competitionName)
    {
        var stream = new MemoryStream();
        var doc = new Document(PageSize.A4, 25, 25, 25, 25);
        var writer = PdfWriter.GetInstance(doc, stream);
        writer.PageEvent = new PageFooterEvent(_bfHelvetica);

        doc.Open();
        AddTitleBanner(doc, competitionName, draw.Category?.Name ?? "Catégorie", "Ordre de passage & Notes (Taolu)");
        doc.Add(new Paragraph(" ") { SpacingAfter = 5 });
        BuildTaoluTable(doc, orders);
        doc.Close();

        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }

    /// <summary>
    /// Calcule le numéro d'ordre chronologique du premier combat d'un tirage (catégorie).
    /// Permet de classer les tirages dans l'ordre chronologique de leur entrée dans la compétition.
    /// </summary>
    public static int GetFirstMatchOrder(DrawModel draw)
    {
        int minOrder = int.MaxValue;

        if (draw.DrawKnockouts != null && draw.DrawKnockouts.Count > 0)
        {
            var realMatches = draw.DrawKnockouts.Where(k => k.GlobalOrder > 0).ToList();
            if (realMatches.Count > 0)
            {
                int minGlobal = realMatches.Min(k => k.GlobalOrder);
                if (minGlobal < minOrder) minOrder = minGlobal;
            }
            else
            {
                var nonByes = draw.DrawKnockouts.Where(k => !KnockoutStageHelper.IsByeMatch(k)).ToList();
                int minLocal = nonByes.Count > 0 ? nonByes.Min(k => k.Order) : draw.DrawKnockouts.Min(k => k.Order);
                if (minLocal < minOrder) minOrder = minLocal;
            }
        }

        if (draw.DrawPools != null && draw.DrawPools.Count > 0)
        {
            var realPools = draw.DrawPools.Where(p => p.GlobalOrder > 0).ToList();
            if (realPools.Count > 0)
            {
                int minGlobal = realPools.Min(p => p.GlobalOrder);
                if (minGlobal < minOrder) minOrder = minGlobal;
            }
            else
            {
                int minLocal = draw.DrawPools.Min(p => p.Order);
                if (minLocal < minOrder) minOrder = minLocal;
            }
        }

        if (draw.DrawOrders != null && draw.DrawOrders.Count > 0)
        {
            var realOrders = draw.DrawOrders.Where(o => o.GlobalOrder > 0).ToList();
            if (realOrders.Count > 0)
            {
                int minGlobal = realOrders.Min(o => o.GlobalOrder);
                if (minGlobal < minOrder) minOrder = minGlobal;
            }
            else
            {
                int minLocal = draw.DrawOrders.Min(o => o.Order);
                if (minLocal < minOrder) minOrder = minLocal;
            }
        }

        return minOrder;
    }

    public Task<Stream> ExportAllDrawsAsync(List<DrawModel> draws, string competitionName)
    {
        var stream = new MemoryStream();
        var doc = new Document(PageSize.A4.Rotate(), 20, 20, 20, 25);
        var writer = PdfWriter.GetInstance(doc, stream);
        writer.PageEvent = new PageFooterEvent(_bfHelvetica);

        doc.Open();
        bool isFirstPage = true;

        // Trier les tirages selon l'ordre officiel :
        // 1. Ordre chronologique du premier combat (GetFirstMatchOrder)
        // 2. Femmes en premier, puis âge croissant (KnockoutStageHelper.GetCategoryPriority)
        // 3. Nom de catégorie
        var orderedDraws = draws
            .OrderBy(d => GetFirstMatchOrder(d))
            .ThenBy(d => KnockoutStageHelper.GetCategoryPriority(d.Category).GenderPriority)
            .ThenBy(d => KnockoutStageHelper.GetCategoryPriority(d.Category).MinAge)
            .ThenBy(d => d.Category?.Name ?? string.Empty)
            .ToList();

        foreach (var draw in orderedDraws)
        {
            if (draw.DrawKnockouts != null && draw.DrawKnockouts.Count > 0)
            {
                if (!isFirstPage)
                {
                    doc.SetPageSize(PageSize.A4.Rotate());
                    doc.NewPage();
                }
                AddTitleBanner(doc, competitionName, draw.Category?.Name ?? "Catégorie", "Tableau éliminatoire (Sanda)");
                doc.Add(new Paragraph(" ") { SpacingAfter = 5 });
                BuildKnockoutBracket(doc, draw.DrawKnockouts);
                isFirstPage = false;
            }

            if (draw.DrawPools != null && draw.DrawPools.Count > 0)
            {
                if (!isFirstPage)
                {
                    doc.SetPageSize(PageSize.A4);
                    doc.NewPage();
                }
                AddTitleBanner(doc, competitionName, draw.Category?.Name ?? "Catégorie", "Tableau de poules (Sanda)");
                doc.Add(new Paragraph(" ") { SpacingAfter = 5 });
                BuildPoolsTable(doc, draw.DrawPools);
                isFirstPage = false;
            }

            if (draw.DrawOrders != null && draw.DrawOrders.Count > 0)
            {
                if (!isFirstPage)
                {
                    doc.SetPageSize(PageSize.A4);
                    doc.NewPage();
                }
                AddTitleBanner(doc, competitionName, draw.Category?.Name ?? "Catégorie", "Ordre de passage & Notes (Taolu)");
                doc.Add(new Paragraph(" ") { SpacingAfter = 5 });
                BuildTaoluTable(doc, draw.DrawOrders);
                isFirstPage = false;
            }
        }

        if (isFirstPage)
        {
            doc.Add(new Paragraph("Aucun tirage disponible pour cette compétition.", new Font(_bfHelvetica, 14, Font.NORMAL, ColorMuted)));
        }

        doc.Close();
        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }

    // ───────────────────────── En-tête / Bannière ─────────────────────────

    private void AddTitleBanner(Document doc, string competition, string category, string subtitle)
    {
        var table = new PdfPTable(2) { WidthPercentage = 100 };
        table.SetWidths(new float[] { 70f, 30f });

        var leftCell = new PdfPCell
        {
            BackgroundColor = ColorHeader,
            Padding = 8,
            Border = Rectangle.NO_BORDER
        };
        leftCell.AddElement(new Paragraph(competition.ToUpperInvariant(), new Font(_bfHelveticaBold, 10, Font.NORMAL, new BaseColor(187, 222, 251))));
        leftCell.AddElement(new Paragraph(category, new Font(_bfHelveticaBold, 15, Font.NORMAL, BaseColor.White)));

        var rightCell = new PdfPCell
        {
            BackgroundColor = ColorHeader,
            Padding = 8,
            Border = Rectangle.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            VerticalAlignment = Element.ALIGN_MIDDLE
        };
        var subP = new Paragraph(subtitle, new Font(_bfHelveticaOblique, 11, Font.NORMAL, BaseColor.White))
        {
            Alignment = Element.ALIGN_RIGHT
        };
        rightCell.AddElement(subP);

        table.AddCell(leftCell);
        table.AddCell(rightCell);
        doc.Add(table);
    }

    // ───────────────────────── Knockout Bracket ─────────────────────────

    private void BuildKnockoutBracket(Document doc, List<DrawKnockoutModel> knockouts)
    {
        var bronzeMatch = knockouts.FirstOrDefault(k => k.IsBronzeMatch);
        var mainMatches = knockouts.Where(k => !k.IsBronzeMatch).OrderBy(k => k.Order).ToList();

        int total = mainMatches.Count;
        int bracketSize = 1;
        while (bracketSize - 1 < total)
            bracketSize *= 2;

        var rounds = new List<(string Name, List<DrawKnockoutModel> Matches)>();
        int pos = 0;
        int roundSize = bracketSize / 2;

        while (roundSize >= 1 && pos < total)
        {
            var roundMatches = mainMatches.Skip(pos).Take(roundSize).ToList();
            if (roundMatches.Count == 0) break;
            rounds.Add((KnockoutStageHelper.GetRoundName(roundMatches.Count), roundMatches));
            pos += roundMatches.Count;
            roundSize /= 2;
        }

        if (rounds.Count == 0)
        {
            rounds.Add(("Combats", mainMatches));
        }

        int roundCount = rounds.Count;
        int maxMatchesInRound0 = rounds[0].Matches.Count;

        // Calcul dynamique des dimensions selon la taille de l'arbre
        float cardHeight;
        float gap0;
        float fontSize;

        if (maxMatchesInRound0 <= 2)
        {
            cardHeight = 56f;
            gap0 = 40f;
            fontSize = 8.5f;
        }
        else if (maxMatchesInRound0 <= 4)
        {
            cardHeight = 52f;
            gap0 = 26f;
            fontSize = 8f;
        }
        else if (maxMatchesInRound0 <= 8)
        {
            cardHeight = 38f;
            gap0 = 12f;
            fontSize = 7f;
        }
        else
        {
            cardHeight = 24f;
            gap0 = 4f;
            fontSize = 5.5f;
        }

        float d0 = cardHeight + gap0;
        float headerHeight = cardHeight * 0.28f;
        float rowHeight = (cardHeight - headerHeight) / 2f;
        // Ligne de démarcation entre les deux combattants (Rouge et Bleu) au sein d'une fiche de combat
        float competitorDividerFromCardTop = headerHeight + rowHeight;

        // Configuration de la table principale : colonnes de tours alternées avec colonnes de connecteurs (arborescence)
        // Pour R tours, il y a (2 * R - 1) colonnes
        int totalCols = roundCount > 1 ? (roundCount * 2 - 1) : 1;
        var bracketTable = new PdfPTable(totalCols) { WidthPercentage = 100 };

        var colWidths = new float[totalCols];
        for (int c = 0; c < totalCols; c++)
        {
            // Colonnes paires (0, 2, 4...) = tours de combats ; Colonnes impaires (1, 3...) = connecteurs d'arbre
            if (c % 2 == 0)
            {
                colWidths[c] = 100f;
            }
            else
            {
                colWidths[c] = roundCount <= 3 ? 18f : 14f;
            }
        }
        bracketTable.SetWidths(colWidths);

        const float roundHeaderHeight = 20f;

        for (int r = 0; r < roundCount; r++)
        {
            var (roundName, matches) = rounds[r];

            // 1. Colonne du tour r
            var colTable = new PdfPTable(1) { WidthPercentage = 100 };

            // En-tête du tour
            var hCell = new PdfPCell(new Phrase(roundName, new Font(_bfHelveticaBold, Math.Max(7f, fontSize), Font.NORMAL, ColorPrimary)))
            {
                BackgroundColor = ColorSectionBg,
                BorderColor = ColorBorder,
                BorderWidth = 1,
                Padding = 3,
                FixedHeight = roundHeaderHeight,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE
            };
            colTable.AddCell(hCell);

            // Espacement initial pour centrer les matches de ce tour entre les deux matches du tour précédent
            // Formule géométrique exacte : TopOffset_r = (2^r - 1) * (d0 / 2)
            float topOffset = (float)(Math.Pow(2, r) - 1.0) * (d0 / 2f);
            if (topOffset > 0)
            {
                colTable.AddCell(CreateSpacerCell(topOffset));
            }

            // Espacement vertical entre deux matches consécutifs dans ce tour
            // Formule exacte : Gap_r = (2^r * d0) - cardHeight
            float gapR = (float)(Math.Pow(2, r) * d0) - cardHeight;

            for (int i = 0; i < matches.Count; i++)
            {
                if (i > 0 && gapR > 0)
                {
                    colTable.AddCell(CreateSpacerCell(gapR));
                }

                var cardTable = CreateMatchCardTable(matches[i], cardHeight, fontSize);
                var cardCell = new PdfPCell(cardTable)
                {
                    Border = Rectangle.NO_BORDER,
                    Padding = 0,
                    FixedHeight = cardHeight
                };
                colTable.AddCell(cardCell);
            }

            // Si c'est le dernier tour (Finale) et qu'il y a une petite finale, l'insérer sous la finale
            if (r == roundCount - 1 && bronzeMatch != null)
            {
                float bronzeSpacer = Math.Max(14f, gap0);
                colTable.AddCell(CreateSpacerCell(bronzeSpacer));

                var bHeader = new PdfPCell(new Phrase("Combat 3e place (Petite finale)", new Font(_bfHelveticaBold, Math.Max(6.5f, fontSize - 1f), Font.NORMAL, BaseColor.White)))
                {
                    BackgroundColor = ColorBronze,
                    Padding = 2,
                    Border = Rectangle.NO_BORDER,
                    FixedHeight = 16f,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    VerticalAlignment = Element.ALIGN_MIDDLE
                };
                colTable.AddCell(bHeader);

                var bCardTable = CreateMatchCardTable(bronzeMatch, cardHeight, fontSize);
                var bCardCell = new PdfPCell(bCardTable)
                {
                    Border = Rectangle.NO_BORDER,
                    Padding = 0,
                    FixedHeight = cardHeight
                };
                colTable.AddCell(bCardCell);
            }

            // Cellule conteneur en mode composite (AddElement) pour éviter tout étirement vertical forcé
            var roundContainerCell = new PdfPCell
            {
                Border = Rectangle.NO_BORDER,
                PaddingLeft = 3f,
                PaddingRight = 3f,
                PaddingTop = 0,
                PaddingBottom = 0,
                VerticalAlignment = Element.ALIGN_TOP
            };
            roundContainerCell.AddElement(colTable);
            bracketTable.AddCell(roundContainerCell);

            // 2. Colonne de connecteurs d'arbre entre tour r et r+1
            if (r < roundCount - 1)
            {
                var connTable = new PdfPTable(1) { WidthPercentage = 100 };

                // Aligner avec l'en-tête du tour
                connTable.AddCell(CreateSpacerCell(roundHeaderHeight));

                // Espacement jusqu'au milieu des deux combattants du 1er match du tour r
                float connTopSpacer = topOffset + competitorDividerFromCardTop;
                if (connTopSpacer > 0)
                {
                    connTable.AddCell(CreateSpacerCell(connTopSpacer));
                }

                // Pour chaque paire de matches du tour r alimentant un match du tour r+1
                float currentD = (float)(Math.Pow(2, r) * d0);
                int pairCount = (matches.Count + 1) / 2;

                for (int p = 0; p < pairCount; p++)
                {
                    if (p > 0)
                    {
                        // Espacement entre deux fourches = distance entre les paires
                        connTable.AddCell(CreateSpacerCell(currentD));
                    }

                    int match1Idx = p * 2;
                    int match2Idx = p * 2 + 1;

                    if (match2Idx < matches.Count)
                    {
                        // Deux matches forment une fourche complète
                        var forkCell = new PdfPCell(new Phrase("", new Font(_bfHelvetica, 1)))
                        {
                            Border = Rectangle.NO_BORDER,
                            Padding = 0,
                            FixedHeight = currentD,
                            CellEvent = new BracketForkEvent(ColorBorder, 1.2f, pad: 3f)
                        };
                        connTable.AddCell(forkCell);
                    }
                    else
                    {
                        // Match orphelin (bye / nombre impair de matches)
                        var singleCell = new PdfPCell(new Phrase("", new Font(_bfHelvetica, 1)))
                        {
                            Border = Rectangle.NO_BORDER,
                            Padding = 0,
                            FixedHeight = currentD,
                            CellEvent = new BracketSingleToNextEvent(ColorBorder, 1.2f, pad: 3f)
                        };
                        connTable.AddCell(singleCell);
                    }
                }

                // Cellule finale vide pour absorber l'espace restant sans étirer la dernière fourche
                connTable.AddCell(new PdfPCell(new Phrase("", new Font(_bfHelvetica, 1)))
                {
                    Border = Rectangle.NO_BORDER,
                    Padding = 0
                });

                // Conteneur en mode composite (AddElement) pour que la table conserve sa hauteur exacte sans étirement
                var connContainerCell = new PdfPCell
                {
                    Border = Rectangle.NO_BORDER,
                    Padding = 0,
                    VerticalAlignment = Element.ALIGN_TOP
                };
                connContainerCell.AddElement(connTable);
                bracketTable.AddCell(connContainerCell);
            }
        }

        doc.Add(bracketTable);
    }

    private PdfPCell CreateSpacerCell(float height)
    {
        return new PdfPCell(new Phrase("", new Font(_bfHelvetica, 1)))
        {
            FixedHeight = Math.Max(0.1f, height),
            Border = Rectangle.NO_BORDER,
            Padding = 0
        };
    }

    private PdfPTable CreateMatchCardTable(DrawKnockoutModel match, float cardHeight, float fontSize)
    {
        var cardTable = new PdfPTable(1) { WidthPercentage = 100 };

        bool c1Winner = match.WinnerId.HasValue && match.WinnerId == match.Competitor1Id;
        bool c2Winner = match.WinnerId.HasValue && match.WinnerId == match.Competitor2Id;

        float headerHeight = cardHeight * 0.28f;
        float rowHeight = (cardHeight - headerHeight) / 2f;

        // Top bar : StageName / N° d'ordre du combat à gauche, Résultat à droite
        bool isBye = KnockoutStageHelper.IsByeMatch(match);
        string stageLabel;
        if (isBye)
        {
            stageLabel = !string.IsNullOrEmpty(match.StageName)
                ? $"{match.StageName} (Exempt)"
                : "Exempt";
        }
        else
        {
            string matchOrderLabel = match.GlobalOrder > 0 ? $"Combat #{match.GlobalOrder}" : $"Combat #{match.Order}";
            stageLabel = !string.IsNullOrEmpty(match.StageName)
                ? $"{matchOrderLabel} • {match.StageName}"
                : matchOrderLabel;
        }

        string scoreSummary = "";
        if (match.IsFinished && !string.IsNullOrEmpty(match.RoundResultsJson))
        {
            try
            {
                var results = System.Text.Json.JsonSerializer.Deserialize<List<RoundResultModel>>(match.RoundResultsJson);
                if (results != null && results.Count > 0)
                {
                    scoreSummary = string.Join(" | ", results.Select(r => $"R{r.RoundNumber}: {r.RedVotes}R-{r.BlueVotes}B"));
                }
            }
            catch { }
        }
        else if (match.IsFinished)
        {
            scoreSummary = isBye ? "Exempté" : "Terminé";
        }

        var headerTable = new PdfPTable(2) { WidthPercentage = 100 };
        if (string.IsNullOrEmpty(scoreSummary))
        {
            headerTable.SetWidths(new float[] { 99.9f, 0.1f });
        }
        else
        {
            headerTable.SetWidths(new float[] { 68f, 32f });
        }

        var hLeft = new PdfPCell(new Phrase(stageLabel, new Font(_bfHelveticaBold, Math.Max(5.2f, fontSize - 1.5f), Font.NORMAL, ColorMuted)))
        {
            BackgroundColor = ColorRowAlt,
            Border = Rectangle.NO_BORDER,
            Padding = 1.5f,
            HorizontalAlignment = Element.ALIGN_LEFT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            FixedHeight = headerHeight
        };

        var hRight = new PdfPCell(new Phrase(scoreSummary, new Font(_bfHelveticaOblique, Math.Max(5f, fontSize - 2f), Font.NORMAL, ColorPrimary)))
        {
            BackgroundColor = ColorRowAlt,
            Border = Rectangle.NO_BORDER,
            Padding = 1.5f,
            HorizontalAlignment = Element.ALIGN_RIGHT,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            FixedHeight = headerHeight
        };

        headerTable.AddCell(hLeft);
        headerTable.AddCell(hRight);

        var topCell = new PdfPCell(headerTable)
        {
            BackgroundColor = ColorRowAlt,
            BorderColor = ColorBorder,
            BorderWidth = 0.5f,
            Padding = 0,
            FixedHeight = headerHeight
        };
        cardTable.AddCell(topCell);

        // Competitor 1 (Rouge)
        cardTable.AddCell(CreateCompetitorRowCell(match.Competitor1, c1Winner, isRed: true, rowHeight, fontSize, match.WinnerId.HasValue));

        // Competitor 2 (Bleu)
        cardTable.AddCell(CreateCompetitorRowCell(match.Competitor2, c2Winner, isRed: false, rowHeight, fontSize, match.WinnerId.HasValue));

        return cardTable;
    }

    private PdfPCell CreateCompetitorRowCell(CompetitorModel? competitor, bool isWinner, bool isRed, float height, float fontSize, bool matchHasWinner)
    {
        var cell = new PdfPCell
        {
            BorderColor = ColorBorder,
            BorderWidth = 0.5f,
            Padding = 2f,
            FixedHeight = height,
            VerticalAlignment = Element.ALIGN_MIDDLE,
            BackgroundColor = isWinner ? ColorRowAlt : BaseColor.White
        };

        if (competitor == null)
        {
            string placeholder = (!isRed && matchHasWinner && !isWinner) ? "— (Exempt)" : "En attente...";
            cell.AddElement(new Phrase(placeholder, new Font(_bfHelveticaOblique, fontSize, Font.NORMAL, ColorMuted)));
            return cell;
        }

        var p = new Paragraph { Leading = fontSize * 1.15f };
        if (isWinner)
        {
            p.Add(new Chunk("✓ ", new Font(_bfHelveticaBold, fontSize, Font.NORMAL, ColorGreen)));
        }
        else
        {
            p.Add(new Chunk(isRed ? "● " : "○ ", new Font(_bfHelvetica, fontSize - 1, Font.NORMAL, isRed ? ColorRed : ColorPrimary)));
        }

        var flagImg = GetFlagImage(ResolveIsoCode(competitor));
        if (flagImg != null)
        {
            flagImg.ScaleToFit(fontSize * 1.25f, fontSize * 0.9f);
            p.Add(new Chunk(flagImg, 0, -1f));
            p.Add(new Chunk("   ", new Font(_bfHelvetica, fontSize * 0.8f)));
        }

        string fullName = $"{competitor.LastName?.ToUpperInvariant()} {competitor.FirstName}";
        var nameFont = isWinner ? new Font(_bfHelveticaBold, fontSize, Font.NORMAL, ColorPrimary) : new Font(_bfHelvetica, fontSize, Font.NORMAL, BaseColor.Black);
        p.Add(new Chunk(fullName, nameFont));

        if (!string.IsNullOrEmpty(competitor.Club))
        {
            p.Add(new Chunk($" ({competitor.Club})", new Font(_bfHelvetica, Math.Max(5f, fontSize - 1.5f), Font.NORMAL, ColorMuted)));
        }

        cell.AddElement(p);
        return cell;
    }

    // ───────────────────────── Connecteurs d'arbre (Fork / Straight) ─────────────────────────

    private class BracketForkEvent : IPdfPCellEvent
    {
        private readonly BaseColor _lineColor;
        private readonly float _lineWidth;
        private readonly float _pad;

        public BracketForkEvent(BaseColor lineColor, float lineWidth = 1.2f, float pad = 3f)
        {
            _lineColor = lineColor;
            _lineWidth = lineWidth;
            _pad = pad;
        }

        public void CellLayout(PdfPCell cell, Rectangle position, PdfContentByte[] canvases)
        {
            var cb = canvases[PdfPTable.LINECANVAS];
            cb.SaveState();
            cb.SetColorStroke(_lineColor);
            cb.SetLineWidth(_lineWidth);

            float left = position.Left - _pad;
            float right = position.Right + _pad;
            float top = position.Top;
            float bottom = position.Bottom;
            float midX = (position.Left + position.Right) / 2f;
            float midY = (top + bottom) / 2f;

            // Branche supérieure (départ du match 2p)
            cb.MoveTo(left, top);
            cb.LineTo(midX, top);

            // Branche inférieure (départ du match 2p+1)
            cb.MoveTo(left, bottom);
            cb.LineTo(midX, bottom);

            // Pont vertical
            cb.MoveTo(midX, bottom);
            cb.LineTo(midX, top);

            // Sortie vers le match suivant au milieu
            cb.MoveTo(midX, midY);
            cb.LineTo(right, midY);

            cb.Stroke();
            cb.RestoreState();
        }
    }

    private class BracketSingleToNextEvent : IPdfPCellEvent
    {
        private readonly BaseColor _lineColor;
        private readonly float _lineWidth;
        private readonly float _pad;

        public BracketSingleToNextEvent(BaseColor lineColor, float lineWidth = 1.2f, float pad = 3f)
        {
            _lineColor = lineColor;
            _lineWidth = lineWidth;
            _pad = pad;
        }

        public void CellLayout(PdfPCell cell, Rectangle position, PdfContentByte[] canvases)
        {
            var cb = canvases[PdfPTable.LINECANVAS];
            cb.SaveState();
            cb.SetColorStroke(_lineColor);
            cb.SetLineWidth(_lineWidth);

            float left = position.Left - _pad;
            float right = position.Right + _pad;
            float top = position.Top;
            float midX = (position.Left + position.Right) / 2f;
            float midY = (position.Top + position.Bottom) / 2f;

            // Départ au milieu des deux combattants du combat 2p
            cb.MoveTo(left, top);
            cb.LineTo(midX, top);

            // Descente vers le niveau médian du match suivant
            cb.MoveTo(midX, top);
            cb.LineTo(midX, midY);

            // Sortie vers le match suivant
            cb.MoveTo(midX, midY);
            cb.LineTo(right, midY);

            cb.Stroke();
            cb.RestoreState();
        }
    }

    // ───────────────────────── Pools Table ─────────────────────────

    private void BuildPoolsTable(Document doc, List<DrawPoolsModel> pools)
    {
        // 1. Liste des combats de la poule
        var table = new PdfPTable(5) { WidthPercentage = 100 };
        table.SetWidths(new float[] { 13f, 32.5f, 32.5f, 14f, 8f });

        void AddHeader(string text)
        {
            table.AddCell(new PdfPCell(new Phrase(text, new Font(_bfHelveticaBold, 9, Font.NORMAL, BaseColor.White)))
            {
                BackgroundColor = ColorHeader,
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_CENTER
            });
        }

        AddHeader("Combat");
        AddHeader("Compétiteur 1 (Rouge)");
        AddHeader("Compétiteur 2 (Bleu)");
        AddHeader("Vainqueur");
        AddHeader("État");

        int rowIdx = 0;
        foreach (var pool in pools.OrderBy(p => p.Order))
        {
            rowIdx++;
            var bg = rowIdx % 2 == 0 ? ColorRowAlt : BaseColor.White;

            string orderDisplay = pool.GlobalOrder > 0 ? $"Combat #{pool.GlobalOrder}" : $"Combat #{pool.Order}";
            table.AddCell(new PdfPCell(new Phrase(orderDisplay, new Font(_bfHelveticaBold, 8.5f, Font.NORMAL, BaseColor.Black)))
            {
                BackgroundColor = bg,
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_CENTER
            });

            table.AddCell(CreateCompetitorInfoCell(pool.Competitor1, bg));
            table.AddCell(CreateCompetitorInfoCell(pool.Competitor2, bg));

            // Vainqueur
            CompetitorModel? winner = pool.WinnerId == pool.Competitor1Id ? pool.Competitor1
                                    : pool.WinnerId == pool.Competitor2Id ? pool.Competitor2
                                    : null;
            string winnerText = winner != null ? $"{winner.LastName?.ToUpperInvariant()} {winner.FirstName}" : (pool.IsFinished ? "—" : "À venir");
            var winnerFont = winner != null ? new Font(_bfHelveticaBold, 8, Font.NORMAL, ColorPrimary) : new Font(_bfHelveticaOblique, 8, Font.NORMAL, ColorMuted);
            table.AddCell(new PdfPCell(new Phrase(winnerText, winnerFont))
            {
                BackgroundColor = bg,
                Padding = 5
            });

            // État
            string status = pool.IsFinished ? "Terminé" : "En cours";
            var statusFont = pool.IsFinished ? new Font(_bfHelveticaBold, 8, Font.NORMAL, ColorGreen) : new Font(_bfHelvetica, 8, Font.NORMAL, ColorMuted);
            table.AddCell(new PdfPCell(new Phrase(status, statusFont))
            {
                BackgroundColor = bg,
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_CENTER
            });
        }

        doc.Add(table);

        // 2. Classement provisoire de la poule
        doc.Add(new Paragraph(" ") { SpacingAfter = 10 });
        BuildPoolStandings(doc, pools);
    }

    private void BuildPoolStandings(Document doc, List<DrawPoolsModel> pools)
    {
        var competitors = new Dictionary<int, (CompetitorModel Model, int Wins, int Losses)>();

        foreach (var pool in pools.Where(p => p.IsFinished))
        {
            void Record(int? id, CompetitorModel? model, bool won)
            {
                if (id == null || model == null) return;
                if (competitors.TryGetValue(id.Value, out var entry))
                    competitors[id.Value] = won ? (entry.Model, entry.Wins + 1, entry.Losses) : (entry.Model, entry.Wins, entry.Losses + 1);
                else
                    competitors[id.Value] = won ? (model, 1, 0) : (model, 0, 1);
            }

            bool c1Won = pool.WinnerId == pool.Competitor1Id;
            Record(pool.Competitor1Id, pool.Competitor1, c1Won);
            Record(pool.Competitor2Id, pool.Competitor2, !c1Won);
        }

        if (competitors.Count == 0) return;

        var standingsTable = new PdfPTable(5) { WidthPercentage = 80, HorizontalAlignment = Element.ALIGN_LEFT };
        standingsTable.SetWidths(new float[] { 10f, 45f, 25f, 10f, 10f });

        void AddStandingHeader(string text)
        {
            standingsTable.AddCell(new PdfPCell(new Phrase(text, new Font(_bfHelveticaBold, 9, Font.NORMAL, BaseColor.White)))
            {
                BackgroundColor = ColorPrimary,
                Padding = 4,
                HorizontalAlignment = Element.ALIGN_CENTER
            });
        }

        AddStandingHeader("Rang");
        AddStandingHeader("Athlète");
        AddStandingHeader("Club");
        AddStandingHeader("V");
        AddStandingHeader("D");

        var ranked = competitors.Values.OrderByDescending(x => x.Wins).ThenBy(x => x.Losses).ToList();
        for (int i = 0; i < ranked.Count; i++)
        {
            var (model, wins, losses) = ranked[i];
            var bg = i % 2 == 0 ? BaseColor.White : ColorRowAlt;
            string rankLabel = i switch { 0 => "1er", 1 => "2e", 2 => "3e", _ => $"{i + 1}e" };

            standingsTable.AddCell(new PdfPCell(new Phrase(rankLabel, new Font(_bfHelveticaBold, 9, Font.NORMAL, BaseColor.Black)))
            {
                BackgroundColor = bg,
                Padding = 4,
                HorizontalAlignment = Element.ALIGN_CENTER
            });

            var athletePhrase = new Phrase();
            var standingFlag = GetFlagImage(ResolveIsoCode(model));
            if (standingFlag != null)
            {
                standingFlag.ScaleToFit(13f, 9f);
                athletePhrase.Add(new Chunk(standingFlag, 0, -1.5f));
                athletePhrase.Add(new Chunk("   "));
            }
            athletePhrase.Add(new Chunk($"{model.LastName?.ToUpperInvariant()} {model.FirstName}", new Font(_bfHelveticaBold, 9, Font.NORMAL, BaseColor.Black)));

            standingsTable.AddCell(new PdfPCell(athletePhrase)
            {
                BackgroundColor = bg,
                Padding = 4
            });
            standingsTable.AddCell(new PdfPCell(new Phrase(model.Club ?? "—", new Font(_bfHelvetica, 8, Font.NORMAL, ColorMuted)))
            {
                BackgroundColor = bg,
                Padding = 4
            });
            standingsTable.AddCell(new PdfPCell(new Phrase($"{wins}", new Font(_bfHelveticaBold, 9, Font.NORMAL, ColorGreen)))
            {
                BackgroundColor = bg,
                Padding = 4,
                HorizontalAlignment = Element.ALIGN_CENTER
            });
            standingsTable.AddCell(new PdfPCell(new Phrase($"{losses}", new Font(_bfHelvetica, 9, Font.NORMAL, ColorRed)))
            {
                BackgroundColor = bg,
                Padding = 4,
                HorizontalAlignment = Element.ALIGN_CENTER
            });
        }

        doc.Add(standingsTable);
    }

    // ───────────────────────── Taolu Table ─────────────────────────

    private void BuildTaoluTable(Document doc, List<DrawOrderModel> orders)
    {
        var finished = orders
            .Where(o => o.IsFinished && !string.IsNullOrEmpty(o.RoundResultsJson)
                        && double.TryParse(o.RoundResultsJson, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            .OrderByDescending(o => double.Parse(o.RoundResultsJson!, NumberStyles.Any, CultureInfo.InvariantCulture))
            .ToList();

        var pending = orders.Where(o => !finished.Contains(o)).OrderBy(o => o.Order).ToList();
        var sorted = finished.Concat(pending).ToList();

        var table = new PdfPTable(5) { WidthPercentage = 100 };
        table.SetWidths(new float[] { 10f, 45f, 25f, 10f, 10f });

        void AddHeader(string text)
        {
            table.AddCell(new PdfPCell(new Phrase(text, new Font(_bfHelveticaBold, 9, Font.NORMAL, BaseColor.White)))
            {
                BackgroundColor = ColorHeader,
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_CENTER
            });
        }

        AddHeader("Ordre");
        AddHeader("Athlète");
        AddHeader("Club");
        AddHeader("Note");
        AddHeader("Rang");

        int rank = 1;
        for (int i = 0; i < sorted.Count; i++)
        {
            var order = sorted[i];
            bool isFinished = order.IsFinished
                && !string.IsNullOrEmpty(order.RoundResultsJson)
                && double.TryParse(order.RoundResultsJson, NumberStyles.Any, CultureInfo.InvariantCulture, out _);

            double? score = isFinished
                ? double.Parse(order.RoundResultsJson!, NumberStyles.Any, CultureInfo.InvariantCulture)
                : null;

            var bg = i % 2 == 0 ? BaseColor.White : ColorRowAlt;
            string rankDisplay = isFinished ? (rank == 1 ? "1er" : rank == 2 ? "2e" : rank == 3 ? "3e" : $"{rank}e") : "—";
            string scoreDisplay = score.HasValue ? $"{score.Value:F2} pts" : "En attente";

            // Ordre
            string orderDisplay = order.GlobalOrder > 0 ? $"#{order.GlobalOrder}" : $"#{order.Order}";
            table.AddCell(new PdfPCell(new Phrase(orderDisplay, new Font(_bfHelveticaBold, 9, Font.NORMAL, BaseColor.Black)))
            {
                BackgroundColor = bg,
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_CENTER
            });

            // Athlète
            table.AddCell(CreateCompetitorInfoCell(order.Competitor, bg));

            // Club
            table.AddCell(new PdfPCell(new Phrase(order.Competitor?.Club ?? "—", new Font(_bfHelvetica, 8, Font.NORMAL, ColorMuted)))
            {
                BackgroundColor = bg,
                Padding = 5
            });

            // Note
            var scoreFont = isFinished ? new Font(_bfHelveticaBold, 10, Font.NORMAL, ColorPrimary) : new Font(_bfHelvetica, 9, Font.NORMAL, ColorMuted);
            table.AddCell(new PdfPCell(new Phrase(scoreDisplay, scoreFont))
            {
                BackgroundColor = bg,
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_CENTER
            });

            // Rang
            var rankFont = isFinished && rank <= 3 ? new Font(_bfHelveticaBold, 9, Font.NORMAL, ColorPrimary) : new Font(_bfHelvetica, 9, Font.NORMAL, BaseColor.Black);
            table.AddCell(new PdfPCell(new Phrase(rankDisplay, rankFont))
            {
                BackgroundColor = bg,
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_CENTER
            });

            if (isFinished) rank++;
        }

        doc.Add(table);
    }

    private PdfPCell CreateCompetitorInfoCell(CompetitorModel? competitor, BaseColor bg)
    {
        var cell = new PdfPCell
        {
            BackgroundColor = bg,
            Padding = 5
        };

        if (competitor == null)
        {
            cell.AddElement(new Phrase("En attente...", new Font(_bfHelveticaOblique, 8, Font.NORMAL, ColorMuted)));
            return cell;
        }

        var p = new Paragraph();

        var flagImg = GetFlagImage(ResolveIsoCode(competitor));
        if (flagImg != null)
        {
            flagImg.ScaleToFit(14f, 10f);
            p.Add(new Chunk(flagImg, 0, -1.5f));
            p.Add(new Chunk("   "));
        }

        p.Add(new Chunk($"{competitor.LastName?.ToUpperInvariant()} {competitor.FirstName}", new Font(_bfHelveticaBold, 9, Font.NORMAL, BaseColor.Black)));
        if (!string.IsNullOrEmpty(competitor.CountryIsoCode))
        {
            p.Add(new Chunk($"  [{competitor.CountryIsoCode}]", new Font(_bfHelvetica, 7, Font.NORMAL, ColorMuted)));
        }
        cell.AddElement(p);
        return cell;
    }

    // ───────────────────────── Page Event (Pied de page) ─────────────────────────

    private sealed class PageFooterEvent : PdfPageEventHelper
    {
        private readonly BaseFont _bf;

        public PageFooterEvent(BaseFont bf)
        {
            _bf = bf;
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            var cb = writer.DirectContent;
            cb.BeginText();
            cb.SetFontAndSize(_bf, 8);
            cb.SetColorFill(new BaseColor(120, 120, 120));

            string text = $"House of Arbitration  •  Généré le {DateTime.Now:dd/MM/yyyy HH:mm}  •  Page {writer.PageNumber}";
            cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER, text, (document.Right + document.Left) / 2, document.Bottom - 12, 0);
            cb.EndText();
        }
    }
}
