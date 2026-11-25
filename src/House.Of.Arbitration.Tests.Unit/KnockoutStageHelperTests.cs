using House.Of.Arbitration.Models.Helpers;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class KnockoutStageHelperTests
{
    [Fact]
    public void GetStageName_SingleMatch_ReturnsFinale()
    {
        // 2 compétiteurs -> 1 match
        var stage = KnockoutStageHelper.GetStageName(order: 1, totalMatches: 1);
        Assert.Equal("Finale", stage);
    }

    [Fact]
    public void GetStageName_FourCompetitors_ReturnsDemiFinalesAndFinale()
    {
        // 4 compétiteurs -> 3 matches
        Assert.Equal("Demi-finale 1", KnockoutStageHelper.GetStageName(1, 3));
        Assert.Equal("Demi-finale 2", KnockoutStageHelper.GetStageName(2, 3));
        Assert.Equal("Finale", KnockoutStageHelper.GetStageName(3, 3));
    }

    [Fact]
    public void GetStageName_EightCompetitors_ReturnsQuartsDemisFinale()
    {
        // 8 compétiteurs -> 7 matches
        Assert.Equal("Quart de finale 1", KnockoutStageHelper.GetStageName(1, 7));
        Assert.Equal("Quart de finale 2", KnockoutStageHelper.GetStageName(2, 7));
        Assert.Equal("Quart de finale 3", KnockoutStageHelper.GetStageName(3, 7));
        Assert.Equal("Quart de finale 4", KnockoutStageHelper.GetStageName(4, 7));
        Assert.Equal("Demi-finale 1", KnockoutStageHelper.GetStageName(5, 7));
        Assert.Equal("Demi-finale 2", KnockoutStageHelper.GetStageName(6, 7));
        Assert.Equal("Finale", KnockoutStageHelper.GetStageName(7, 7));
    }

    [Fact]
    public void GetStageName_SixteenCompetitors_ReturnsHuitiemesQuartsDemisFinale()
    {
        // 16 compétiteurs -> 15 matches
        Assert.Equal("8ème de finale 1", KnockoutStageHelper.GetStageName(1, 15));
        Assert.Equal("8ème de finale 8", KnockoutStageHelper.GetStageName(8, 15));
        Assert.Equal("Quart de finale 1", KnockoutStageHelper.GetStageName(9, 15));
        Assert.Equal("Quart de finale 4", KnockoutStageHelper.GetStageName(12, 15));
        Assert.Equal("Demi-finale 1", KnockoutStageHelper.GetStageName(13, 15));
        Assert.Equal("Demi-finale 2", KnockoutStageHelper.GetStageName(14, 15));
        Assert.Equal("Finale", KnockoutStageHelper.GetStageName(15, 15));
    }

    [Theory]
    [InlineData(5, 4, 3)] // 5 compétiteurs, 4 matches en T1, 3 byes
    [InlineData(6, 4, 2)] // 6 compétiteurs, 4 matches en T1, 2 byes
    [InlineData(7, 4, 1)] // 7 compétiteurs, 4 matches en T1, 1 bye
    [InlineData(8, 4, 0)] // 8 compétiteurs, 4 matches en T1, 0 bye
    [InlineData(3, 2, 1)] // 3 compétiteurs, 2 matches en T1, 1 bye
    public void DistributeFirstRoundCompetitors_NeverHasMatchWithTwoUnknowns_AlwaysHasAtLeastOne(int competitorCount, int expectedMatches, int expectedByes)
    {
        // Arrange
        var competitors = Enumerable.Range(1, competitorCount)
            .Select(i => new Models.CompetitorModel { Id = i, LastName = $"Nom{i}", FirstName = $"Prenom{i}" })
            .ToList();

        // Act
        var pairs = KnockoutStageHelper.DistributeFirstRoundCompetitors(competitors, expectedMatches);

        // Assert
        Assert.Equal(expectedMatches, pairs.Count);

        int byeCount = 0;
        foreach (var (comp1, comp2) in pairs)
        {
            // Chaque match doit impérativement avoir au moins 1 combattant (Comp1 non null)
            Assert.NotNull(comp1);

            // Jamais de match avec deux inconnus
            Assert.False(comp1 == null && comp2 == null);

            if (comp2 == null)
            {
                byeCount++;
            }
        }

        Assert.Equal(expectedByes, byeCount);

        // Vérifier que tous les compétiteurs uniques ont été assignés exactement une fois
        var assignedIds = pairs.Select(p => p.Comp1.Id)
            .Concat(pairs.Where(p => p.Comp2 != null).Select(p => p.Comp2!.Id))
            .ToList();

        Assert.Equal(competitorCount, assignedIds.Count);
        Assert.Equal(competitorCount, assignedIds.Distinct().Count());
    }

    [Fact]
    public void GetStageName_ThirtyTwoCompetitors_ReturnsSeiziemes()
    {
        // 32 compétiteurs -> 31 matches
        Assert.Equal("16ème de finale 1", KnockoutStageHelper.GetStageName(1, 31));
        Assert.Equal("16ème de finale 16", KnockoutStageHelper.GetStageName(16, 31));
        Assert.Equal("8ème de finale 1", KnockoutStageHelper.GetStageName(17, 31));
        Assert.Equal("Quart de finale 1", KnockoutStageHelper.GetStageName(25, 31));
        Assert.Equal("Demi-finale 1", KnockoutStageHelper.GetStageName(29, 31));
        Assert.Equal("Finale", KnockoutStageHelper.GetStageName(31, 31));
    }

    [Theory]
    [InlineData(1, "Finale")]
    [InlineData(2, "Demi-finales")]
    [InlineData(4, "Quarts de finale")]
    [InlineData(8, "8èmes de finale")]
    [InlineData(16, "16èmes de finale")]
    [InlineData(32, "32èmes de finale")]
    public void GetRoundName_ReturnsCorrectPluralOrSingular(int matchesInRound, string expected)
    {
        Assert.Equal(expected, KnockoutStageHelper.GetRoundName(matchesInRound));
    }

    [Fact]
    public void DistributeFirstRoundCompetitors_OneCompetitor_ReturnsSingleByeMatch()
    {
        // Arrange : 1 seul compétiteur → doit retourner 1 paire (comp1, null)
        var competitors = new List<Models.CompetitorModel>
        {
            new Models.CompetitorModel { Id = 1, LastName = "Seul", FirstName = "Champion" }
        };

        // Act
        var pairs = KnockoutStageHelper.DistributeFirstRoundCompetitors(competitors, matchesInRound: 1);

        // Assert : 1 match, comp1 non null, comp2 null (bye)
        Assert.Single(pairs);
        Assert.NotNull(pairs[0].Comp1);
        Assert.Null(pairs[0].Comp2);
        Assert.Equal(1, pairs[0].Comp1.Id);
    }

    [Fact]
    public void GetStagePhasePriority_ReturnsStrictChronologicalOrder_From32emeToFinale()
    {
        // Règle demandée :
        // 32èmes de finale < 16èmes de finale < 8èmes de finale < Quarts < Demis < Petites finales < Finales
        int p32 = KnockoutStageHelper.GetStagePhasePriority("32ème de finale 1");
        int p16 = KnockoutStageHelper.GetStagePhasePriority("16ème de finale 3");
        int p8 = KnockoutStageHelper.GetStagePhasePriority("8ème de finale 2");
        int pQuart = KnockoutStageHelper.GetStagePhasePriority("Quart de finale 4");
        int pDemi = KnockoutStageHelper.GetStagePhasePriority("Demi-finale 1");
        int pPetite = KnockoutStageHelper.GetStagePhasePriority("Petite finale", isBronzeMatch: true);
        int pFinale = KnockoutStageHelper.GetStagePhasePriority("Finale");

        Assert.True(p32 < p16, "Les 32èmes doivent se dérouler avant les 16èmes");
        Assert.True(p16 < p8, "Les 16èmes doivent se dérouler avant les 8èmes");
        Assert.True(p8 < pQuart, "Les 8èmes doivent se dérouler avant les quarts");
        Assert.True(pQuart < pDemi, "Les quarts doivent se dérouler avant les demis");
        Assert.True(pDemi < pPetite, "Les demis doivent se dérouler avant les petites finales");
        Assert.True(pPetite < pFinale, "Les petites finales doivent se dérouler avant les finales");
    }

    [Fact]
    public void IsByeMatch_WithSingleCompetitorAndFinished_ReturnsTrue()
    {
        var comp = new Models.CompetitorModel { Id = 1, LastName = "Dupont", FirstName = "Jean" };
        var byeMatch = new Models.DrawKnockoutModel
        {
            Order = 1,
            GlobalOrder = 0,
            Competitor1 = comp,
            Competitor1Id = comp.Id,
            Competitor2 = null,
            Competitor2Id = null,
            IsFinished = true,
            WinnerId = comp.Id
        };

        Assert.True(KnockoutStageHelper.IsByeMatch(byeMatch));
        Assert.True(byeMatch.IsBye);
    }

    [Fact]
    public void IsByeMatch_WithTwoCompetitors_ReturnsFalse()
    {
        var comp1 = new Models.CompetitorModel { Id = 1, LastName = "Dupont", FirstName = "Jean" };
        var comp2 = new Models.CompetitorModel { Id = 2, LastName = "Martin", FirstName = "Paul" };
        var match = new Models.DrawKnockoutModel
        {
            Order = 1,
            GlobalOrder = 1,
            Competitor1 = comp1,
            Competitor1Id = comp1.Id,
            Competitor2 = comp2,
            Competitor2Id = comp2.Id,
            IsFinished = false
        };

        Assert.False(KnockoutStageHelper.IsByeMatch(match));
        Assert.False(match.IsBye);
    }

    [Fact]
    public void GetCategoryPriority_WomenBeforeMen_AndYoungerBeforeOlder()
    {
        var catCadetsFemmes = new Models.CategoryModel
        {
            Id = 1,
            Genre = Models.Genre.Women,
            AgeRange = new Models.AgeRangeModel { MinAge = 12, Label = "Cadets" }
        };
        var catSeniorsFemmes = new Models.CategoryModel
        {
            Id = 2,
            Genre = Models.Genre.Women,
            AgeRange = new Models.AgeRangeModel { MinAge = 18, Label = "Seniors" }
        };
        var catCadetsHommes = new Models.CategoryModel
        {
            Id = 3,
            Genre = Models.Genre.Men,
            AgeRange = new Models.AgeRangeModel { MinAge = 12, Label = "Cadets" }
        };
        var catSeniorsHommes = new Models.CategoryModel
        {
            Id = 4,
            Genre = Models.Genre.Men,
            AgeRange = new Models.AgeRangeModel { MinAge = 18, Label = "Seniors" }
        };

        var list = new List<Models.CategoryModel> { catSeniorsHommes, catSeniorsFemmes, catCadetsHommes, catCadetsFemmes };

        var sorted = list
            .OrderBy(c => KnockoutStageHelper.GetCategoryPriority(c).GenderPriority)
            .ThenBy(c => KnockoutStageHelper.GetCategoryPriority(c).MinAge)
            .ThenBy(c => c.Id)
            .ToList();

        Assert.Equal(catCadetsFemmes.Id, sorted[0].Id);
        Assert.Equal(catSeniorsFemmes.Id, sorted[1].Id);
        Assert.Equal(catCadetsHommes.Id, sorted[2].Id);
        Assert.Equal(catSeniorsHommes.Id, sorted[3].Id);
    }

    [Fact]
    public void ScheduleTournamentDraws_OrdersDrawsWithWomenFirstThenYounger()
    {
        var catFemmesCadets = new Models.CategoryModel
        {
            Id = 1,
            Genre = Models.Genre.Women,
            AgeRange = new Models.AgeRangeModel { MinAge = 12, Label = "Cadets" }
        };
        var catHommesCadets = new Models.CategoryModel
        {
            Id = 2,
            Genre = Models.Genre.Men,
            AgeRange = new Models.AgeRangeModel { MinAge = 12, Label = "Cadets" }
        };

        var drawFemmes = new Models.DrawModel { Id = 1, CategoryId = 1, Category = catFemmesCadets };
        var drawHommes = new Models.DrawModel { Id = 2, CategoryId = 2, Category = catHommesCadets };

        // Deux matches de demi-finales dans chaque catégorie (StageName null → phase inconnue → priorité 50 < 70 → section préliminaire)
        // Les femmes doivent être en premier dans la section préliminaire.
        var koH1 = new Models.DrawKnockoutModel { Id = 1, DrawId = 2, Draw = drawHommes, Order = 1 };
        var koH2 = new Models.DrawKnockoutModel { Id = 2, DrawId = 2, Draw = drawHommes, Order = 2 };
        var koF1 = new Models.DrawKnockoutModel { Id = 3, DrawId = 1, Draw = drawFemmes, Order = 1 };
        var koF2 = new Models.DrawKnockoutModel { Id = 4, DrawId = 1, Draw = drawFemmes, Order = 2 };

        var allKnockouts = new List<Models.DrawKnockoutModel> { koH1, koH2, koF1, koF2 };

        var scheduled = KnockoutStageHelper.ScheduleTournamentDraws(allKnockouts);

        // Les femmes doivent être programmées en premier au sein de la phase préliminaire
        Assert.Equal(koF1.Id, scheduled[0].Id);
        Assert.Equal(koF2.Id, scheduled[1].Id);
        Assert.Equal(koH1.Id, scheduled[2].Id);
        Assert.Equal(koH2.Id, scheduled[3].Id);
    }

    [Fact]
    public void ScheduleTournamentDraws_DirectFinalesGoToEnd_WomenBeforeMen()
    {
        // Catégorie femmes : 1 seul match = Finale directe
        // → StageName calculé depuis Draw.DrawKnockouts : 1 match non-bronze → GetStageName(1, 1) = "Finale" (priorité 80 ≥ 70 → section finale)
        // Catégorie hommes : 2 demis + 1 finale
        // → Demis : GetStageName(1,2)="Demi-finale 1", GetStageName(2,2)="Demi-finale 2" (priorité 60 → section prélim)
        // → Finale : GetStageName(3,3... ou 1 restant) = "Finale" (priorité 80 → section finale)
        var catFemmes = new Models.CategoryModel
        {
            Id = 1,
            Genre = Models.Genre.Women,
            AgeRange = new Models.AgeRangeModel { MinAge = 18, Label = "Seniors" }
        };
        var catHommes = new Models.CategoryModel
        {
            Id = 2,
            Genre = Models.Genre.Men,
            AgeRange = new Models.AgeRangeModel { MinAge = 18, Label = "Seniors" }
        };

        // Finale directe féminine — 1 seul match dans DrawKnockouts → StageName = "Finale"
        var drawFemmes = new Models.DrawModel { Id = 1, CategoryId = 1, Category = catFemmes };
        var koFemFinale = new Models.DrawKnockoutModel { Id = 1, DrawId = 1, Draw = drawFemmes, Order = 1 };
        drawFemmes.DrawKnockouts = new System.Collections.Generic.List<Models.DrawKnockoutModel> { koFemFinale };

        // Tableau masculin : 2 demis + 1 finale → 3 matchs → StageName calculé comme Demi-finale 1, Demi-finale 2, Finale
        var drawHommes = new Models.DrawModel { Id = 2, CategoryId = 2, Category = catHommes };
        var koHSemi1 = new Models.DrawKnockoutModel { Id = 2, DrawId = 2, Draw = drawHommes, Order = 1 };
        var koHSemi2 = new Models.DrawKnockoutModel { Id = 3, DrawId = 2, Draw = drawHommes, Order = 2 };
        var koHFinale = new Models.DrawKnockoutModel { Id = 4, DrawId = 2, Draw = drawHommes, Order = 3 };
        drawHommes.DrawKnockouts = new System.Collections.Generic.List<Models.DrawKnockoutModel> { koHSemi1, koHSemi2, koHFinale };

        // Vérifier les StageName calculés (GetStageName appelé avec includeIndex=false)
        Assert.Equal("Finale", koFemFinale.StageName);    // 1 match non-bronze → Finale
        Assert.Equal("Demi-finale", koHSemi1.StageName);  // 3 matchs, rank 1 → Demi-finale (sans numéro car includeIndex=false)
        Assert.Equal("Demi-finale", koHSemi2.StageName);  // rank 2 → Demi-finale
        Assert.Equal("Finale", koHFinale.StageName);       // rank 3 → Finale

        var all = new System.Collections.Generic.List<Models.DrawKnockoutModel>
            { koHSemi1, koHSemi2, koHFinale, koFemFinale };
        var scheduled = KnockoutStageHelper.ScheduleTournamentDraws(all);

        // Ordre attendu :
        // 1. Demis masculines (section préliminaire, phase priorité 60 < 70)
        // 2. Finale féminine (section finale, femmes en premier car getCategoryId femmes < hommes)
        // 3. Finale masculine (section finale, hommes en second)
        Assert.Equal(koHSemi1.Id, scheduled[0].Id); // Demi-finale 1 homme (préliminaire)
        Assert.Equal(koHSemi2.Id, scheduled[1].Id); // Demi-finale 2 homme (préliminaire)
        Assert.Equal(koFemFinale.Id, scheduled[2].Id); // Finale femme (section finale, femme d'abord)
        Assert.Equal(koHFinale.Id, scheduled[3].Id);   // Finale homme (section finale, homme ensuite)
    }

    [Fact]
    public void ScheduleTournamentDraws_QuartsAlwaysBeforeSemis_AcrossDifferentSizedCategories()
    {
        // Catégorie femmes (prioritaire) : 2 demis + 1 finale (pas de quarts)
        // Catégorie hommes : 4 quarts + 2 demis + 1 finale
        // Règle : TOUS les quarts de finale doivent passer AVANT TOUTES les demi-finales !
        var catFemmes = new Models.CategoryModel
        {
            Id = 1,
            Genre = Models.Genre.Women,
            AgeRange = new Models.AgeRangeModel { MinAge = 18, Label = "Seniors" }
        };
        var catHommes = new Models.CategoryModel
        {
            Id = 2,
            Genre = Models.Genre.Men,
            AgeRange = new Models.AgeRangeModel { MinAge = 18, Label = "Seniors" }
        };

        var drawFemmes = new Models.DrawModel { Id = 1, CategoryId = 1, Category = catFemmes };
        var koFSemi1 = new Models.DrawKnockoutModel { Id = 1, DrawId = 1, Draw = drawFemmes, Order = 1 };
        var koFSemi2 = new Models.DrawKnockoutModel { Id = 2, DrawId = 1, Draw = drawFemmes, Order = 2 };
        var koFFinale = new Models.DrawKnockoutModel { Id = 3, DrawId = 1, Draw = drawFemmes, Order = 3 };
        drawFemmes.DrawKnockouts = new List<Models.DrawKnockoutModel> { koFSemi1, koFSemi2, koFFinale };

        var drawHommes = new Models.DrawModel { Id = 2, CategoryId = 2, Category = catHommes };
        var koHQ1 = new Models.DrawKnockoutModel { Id = 4, DrawId = 2, Draw = drawHommes, Order = 1 };
        var koHQ2 = new Models.DrawKnockoutModel { Id = 5, DrawId = 2, Draw = drawHommes, Order = 2 };
        var koHQ3 = new Models.DrawKnockoutModel { Id = 6, DrawId = 2, Draw = drawHommes, Order = 3 };
        var koHQ4 = new Models.DrawKnockoutModel { Id = 7, DrawId = 2, Draw = drawHommes, Order = 4 };
        var koHSemi1 = new Models.DrawKnockoutModel { Id = 8, DrawId = 2, Draw = drawHommes, Order = 5 };
        var koHSemi2 = new Models.DrawKnockoutModel { Id = 9, DrawId = 2, Draw = drawHommes, Order = 6 };
        var koHFinale = new Models.DrawKnockoutModel { Id = 10, DrawId = 2, Draw = drawHommes, Order = 7 };
        drawHommes.DrawKnockouts = new List<Models.DrawKnockoutModel>
            { koHQ1, koHQ2, koHQ3, koHQ4, koHSemi1, koHSemi2, koHFinale };

        var all = new List<Models.DrawKnockoutModel>
            { koFSemi1, koFSemi2, koFFinale, koHQ1, koHQ2, koHQ3, koHQ4, koHSemi1, koHSemi2, koHFinale };

        var scheduled = KnockoutStageHelper.ScheduleTournamentDraws(all);

        // 1. Les 4 quarts hommes (priorité 50)
        Assert.Equal(new[] { koHQ1.Id, koHQ2.Id, koHQ3.Id, koHQ4.Id },
            scheduled.Take(4).Select(m => m.Id).ToArray());

        // 2. Les 2 demis femmes avant les 2 demis hommes (priorité 60, femmes en premier)
        Assert.Equal(new[] { koFSemi1.Id, koFSemi2.Id },
            scheduled.Skip(4).Take(2).Select(m => m.Id).ToArray());
        Assert.Equal(new[] { koHSemi1.Id, koHSemi2.Id },
            scheduled.Skip(6).Take(2).Select(m => m.Id).ToArray());

        // 3. Les finales : femmes puis hommes (priorité 80)
        Assert.Equal(new[] { koFFinale.Id, koHFinale.Id },
            scheduled.Skip(8).Take(2).Select(m => m.Id).ToArray());
    }

    [Fact]
    public void ScheduleTournamentDraws_WhenDrawKnockoutsNotLoaded_StillOrdersQuartsBeforeSemis()
    {
        // Simule un chargement EF Core où Draw.DrawKnockouts est null sur chaque DrawModel
        var catFemmes = new Models.CategoryModel
        {
            Id = 1,
            Genre = Models.Genre.Women,
            AgeRange = new Models.AgeRangeModel { MinAge = 18, Label = "Seniors" }
        };
        var catHommes = new Models.CategoryModel
        {
            Id = 2,
            Genre = Models.Genre.Men,
            AgeRange = new Models.AgeRangeModel { MinAge = 18, Label = "Seniors" }
        };

        var drawFemmes = new Models.DrawModel { Id = 1, CategoryId = 1, Category = catFemmes, DrawKnockouts = null };
        var koFSemi1 = new Models.DrawKnockoutModel { Id = 1, DrawId = 1, Draw = drawFemmes, Order = 1 };
        var koFSemi2 = new Models.DrawKnockoutModel { Id = 2, DrawId = 1, Draw = drawFemmes, Order = 2 };
        var koFFinale = new Models.DrawKnockoutModel { Id = 3, DrawId = 1, Draw = drawFemmes, Order = 3 };

        var drawHommes = new Models.DrawModel { Id = 2, CategoryId = 2, Category = catHommes, DrawKnockouts = null };
        var koHQ1 = new Models.DrawKnockoutModel { Id = 4, DrawId = 2, Draw = drawHommes, Order = 1 };
        var koHQ2 = new Models.DrawKnockoutModel { Id = 5, DrawId = 2, Draw = drawHommes, Order = 2 };
        var koHQ3 = new Models.DrawKnockoutModel { Id = 6, DrawId = 2, Draw = drawHommes, Order = 3 };
        var koHQ4 = new Models.DrawKnockoutModel { Id = 7, DrawId = 2, Draw = drawHommes, Order = 4 };
        var koHSemi1 = new Models.DrawKnockoutModel { Id = 8, DrawId = 2, Draw = drawHommes, Order = 5 };
        var koHSemi2 = new Models.DrawKnockoutModel { Id = 9, DrawId = 2, Draw = drawHommes, Order = 6 };
        var koHFinale = new Models.DrawKnockoutModel { Id = 10, DrawId = 2, Draw = drawHommes, Order = 7 };

        var all = new List<Models.DrawKnockoutModel>
            { koFSemi1, koFSemi2, koFFinale, koHQ1, koHQ2, koHQ3, koHQ4, koHSemi1, koHSemi2, koHFinale };

        var scheduled = KnockoutStageHelper.ScheduleTournamentDraws(all);

        Assert.Equal(new[] { koHQ1.Id, koHQ2.Id, koHQ3.Id, koHQ4.Id },
            scheduled.Take(4).Select(m => m.Id).ToArray());
        Assert.Equal(new[] { koFSemi1.Id, koFSemi2.Id },
            scheduled.Skip(4).Take(2).Select(m => m.Id).ToArray());
        Assert.Equal(new[] { koHSemi1.Id, koHSemi2.Id },
            scheduled.Skip(6).Take(2).Select(m => m.Id).ToArray());
        Assert.Equal(new[] { koFFinale.Id, koHFinale.Id },
            scheduled.Skip(8).Take(2).Select(m => m.Id).ToArray());
    }
}
