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
}
