using House.Of.Arbitration.Models;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class TaoluCodesTests
{
    [Fact]
    public void DefaultCodes_ShouldContainPredefinedTaoluCodes()
    {
        // Act
        var defaults = MvtCodeModel.DefaultCodes;

        // Assert
        Assert.NotNull(defaults);
        Assert.NotEmpty(defaults);
        Assert.Contains(defaults, c => c.Code == "01");
        Assert.Contains(defaults, c => c.GroupName == "Positions");
        Assert.Contains(defaults, c => c.GroupName == "Techniques de saut");
    }

    [Fact]
    public void StandardGroups_ShouldContainExpectedGroups()
    {
        // Act
        var groups = MvtCodeModel.StandardGroups;

        // Assert
        Assert.Contains("Positions", groups);
        Assert.Contains("Techniques de main", groups);
        Assert.Contains("Techniques de jambe", groups);
        Assert.Contains("Techniques de saut", groups);
        Assert.Contains("Equilibres", groups);
        Assert.Contains("Techniques spécifiques aux armes", groups);
    }

    [Fact]
    public void GroupLabel_ShouldReturnGroupNameWhenNotEmpty()
    {
        // Arrange
        var model = new MvtCodeModel
        {
            Code = "10",
            GroupName = "Positions",
            Movement = "Gong Bu",
            Description = "Pied arrière décollé",
            Value = 0.1
        };

        // Assert
        Assert.Equal("Positions", model.GroupLabel);
    }

    [Fact]
    public void GroupLabel_ShouldReturnGeneralWhenGroupNameIsEmpty()
    {
        // Arrange
        var model = new MvtCodeModel
        {
            Code = "99",
            GroupName = "",
            Movement = "Test",
            Value = 0.2
        };

        // Assert
        Assert.Equal("Général", model.GroupLabel);
    }
}
