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
}
