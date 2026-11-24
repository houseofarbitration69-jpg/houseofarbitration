#region Imports
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services;
using Xunit;
#endregion

namespace House.Of.Arbitration.Tests.Unit;

public class DrawPdfExportServiceTests
{
    private readonly DrawPdfExportService _service = new();

    [Fact]
    public async Task ExportKnockoutBracketAsync_GeneratesNonEmptyPdfStream()
    {
        // Arrange
        var category = new CategoryModel { Id = 1, Type = CategoryType.Sanda, Genre = Genre.Men, WeightMax = 70 };
        var draw = new DrawModel { Id = 1, CategoryId = 1, Category = category };

        var c1 = new CompetitorModel { Id = 10, FirstName = "Jean", LastName = "Dupont", Club = "Club Paris", CountryIsoCode = "FR" };
        var c2 = new CompetitorModel { Id = 20, FirstName = "Li", LastName = "Wei", Club = "Club Beijing", CountryIsoCode = "CN" };
        var c3 = new CompetitorModel { Id = 30, FirstName = "Carlos", LastName = "Silva", Club = "Club Madrid", CountryIsoCode = "ES" };
        var c4 = new CompetitorModel { Id = 40, FirstName = "Marc", LastName = "Dubois", Club = "Club Lyon", CountryIsoCode = "FR" };

        var demi1 = new DrawKnockoutModel
        {
            Id = 1,
            Order = 1,
            Competitor1Id = c1.Id,
            Competitor1 = c1,
            Competitor2Id = c2.Id,
            Competitor2 = c2,
            WinnerId = c1.Id,
            LooserId = c2.Id,
            IsFinished = true,
            RoundResultsJson = "[{\"RoundNumber\":1,\"RedVotes\":3,\"BlueVotes\":0,\"WinnerColor\":\"Red\"},{\"RoundNumber\":2,\"RedVotes\":3,\"BlueVotes\":0,\"WinnerColor\":\"Red\"}]"
        };

        var demi2 = new DrawKnockoutModel
        {
            Id = 2,
            Order = 2,
            Competitor1Id = c3.Id,
            Competitor1 = c3,
            Competitor2Id = c4.Id,
            Competitor2 = c4,
            WinnerId = c4.Id,
            LooserId = c3.Id,
            IsFinished = true
        };

        var finale = new DrawKnockoutModel
        {
            Id = 3,
            Order = 3,
            Competitor1Id = c1.Id,
            Competitor1 = c1,
            Competitor2Id = c4.Id,
            Competitor2 = c4,
            IsFinished = false
        };

        var bronze = new DrawKnockoutModel
        {
            Id = 4,
            Order = 4,
            IsBronzeMatch = true,
            Competitor1Id = c2.Id,
            Competitor1 = c2,
            Competitor2Id = c3.Id,
            Competitor2 = c3,
            IsFinished = false
        };

        var knockouts = new List<DrawKnockoutModel> { demi1, demi2, finale, bronze };

        // Act
        using var stream = await _service.ExportKnockoutBracketAsync(draw, knockouts, "Championnat National 2026");

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // Verify PDF magic header "%PDF"
        stream.Position = 0;
        var header = new byte[4];
        int bytesRead = stream.Read(header, 0, 4);
        Assert.Equal(4, bytesRead);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(header));
    }

    [Fact]
    public async Task ExportKnockoutBracketAsync_With8Competitors_GeneratesValidCenteredBracketPdf()
    {
        // Arrange : Arbre à 8 compétiteurs (4 quarts, 2 demis, 1 finale, 1 petite finale)
        var category = new CategoryModel { Id = 1, Type = CategoryType.Sanda, Genre = Genre.Men, WeightMax = 75 };
        var draw = new DrawModel { Id = 1, CategoryId = 1, Category = category };

        var competitors = Enumerable.Range(1, 8).Select(i => new CompetitorModel
        {
            Id = i,
            FirstName = $"Prenom{i}",
            LastName = $"Nom{i}",
            Club = $"Club {i}",
            CountryIsoCode = "FR"
        }).ToList();

        var knockouts = new List<DrawKnockoutModel>();

        // 4 Quarts
        for (int i = 0; i < 4; i++)
        {
            var comp1 = competitors[i * 2];
            var comp2 = competitors[i * 2 + 1];
            knockouts.Add(new DrawKnockoutModel
            {
                Id = i + 1,
                Order = i + 1,
                Competitor1Id = comp1.Id,
                Competitor1 = comp1,
                Competitor2Id = comp2.Id,
                Competitor2 = comp2,
                WinnerId = comp1.Id,
                LooserId = comp2.Id,
                IsFinished = true,
                RoundResultsJson = "[{\"RoundNumber\":1,\"RedVotes\":3,\"BlueVotes\":0,\"WinnerColor\":\"Red\"},{\"RoundNumber\":2,\"RedVotes\":3,\"BlueVotes\":0,\"WinnerColor\":\"Red\"}]"
            });
        }

        // 2 Demis
        for (int i = 0; i < 2; i++)
        {
            var comp1 = knockouts[i * 2].Competitor1!;
            var comp2 = knockouts[i * 2 + 1].Competitor1!;
            knockouts.Add(new DrawKnockoutModel
            {
                Id = 5 + i,
                Order = 5 + i,
                Competitor1Id = comp1.Id,
                Competitor1 = comp1,
                Competitor2Id = comp2.Id,
                Competitor2 = comp2,
                WinnerId = comp1.Id,
                LooserId = comp2.Id,
                IsFinished = true
            });
        }

        // 1 Finale
        knockouts.Add(new DrawKnockoutModel
        {
            Id = 7,
            Order = 7,
            Competitor1Id = knockouts[4].Competitor1!.Id,
            Competitor1 = knockouts[4].Competitor1,
            Competitor2Id = knockouts[5].Competitor1!.Id,
            Competitor2 = knockouts[5].Competitor1,
            IsFinished = false
        });

        // 1 Petite finale
        knockouts.Add(new DrawKnockoutModel
        {
            Id = 8,
            Order = 8,
            IsBronzeMatch = true,
            Competitor1Id = knockouts[4].Competitor2!.Id,
            Competitor1 = knockouts[4].Competitor2,
            Competitor2Id = knockouts[5].Competitor2!.Id,
            Competitor2 = knockouts[5].Competitor2,
            IsFinished = false
        });

        // Act
        using var stream = await _service.ExportKnockoutBracketAsync(draw, knockouts, "Championnat de France Sanda 2026");

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        stream.Position = 0;
        var header = new byte[4];
        int bytesRead = stream.Read(header, 0, 4);
        Assert.Equal(4, bytesRead);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(header));
    }

    [Fact]
    public async Task ExportPoolsAsync_GeneratesNonEmptyPdfStream()
    {
        // Arrange
        var category = new CategoryModel { Id = 2, Type = CategoryType.Sanda, Genre = Genre.Men, WeightMax = 60, RoundType = RoundType.Pools };
        var draw = new DrawModel { Id = 2, CategoryId = 2, Category = category };

        var c1 = new CompetitorModel { Id = 1, FirstName = "Lucas", LastName = "Martin", Club = "Dragon Blanc", CountryIsoCode = "FR" };
        var c2 = new CompetitorModel { Id = 2, FirstName = "Ali", LastName = "Benali", Club = "Tigre Noir", CountryIsoCode = "FR" };
        var c3 = new CompetitorModel { Id = 3, FirstName = "Ken", LastName = "Takahashi", Club = "Tokyo Dojo", CountryIsoCode = "JP" };

        var pool1 = new DrawPoolsModel
        {
            Id = 1,
            Order = 1,
            Competitor1Id = c1.Id,
            Competitor1 = c1,
            Competitor2Id = c2.Id,
            Competitor2 = c2,
            WinnerId = c1.Id,
            LooserId = c2.Id,
            IsFinished = true
        };

        var pool2 = new DrawPoolsModel
        {
            Id = 2,
            Order = 2,
            Competitor1Id = c2.Id,
            Competitor1 = c2,
            Competitor2Id = c3.Id,
            Competitor2 = c3,
            WinnerId = c3.Id,
            LooserId = c2.Id,
            IsFinished = true
        };

        var pool3 = new DrawPoolsModel
        {
            Id = 3,
            Order = 3,
            Competitor1Id = c1.Id,
            Competitor1 = c1,
            Competitor2Id = c3.Id,
            Competitor2 = c3,
            IsFinished = false
        };

        var pools = new List<DrawPoolsModel> { pool1, pool2, pool3 };

        // Act
        using var stream = await _service.ExportPoolsAsync(draw, pools, "Coupe de France 2026");

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        stream.Position = 0;
        var header = new byte[4];
        int bytesRead = stream.Read(header, 0, 4);
        Assert.Equal(4, bytesRead);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(header));
    }

    [Fact]
    public async Task ExportTaoluOrderAsync_GeneratesNonEmptyPdfStream()
    {
        // Arrange
        var category = new CategoryModel { Id = 3, Type = CategoryType.TaoluModerneNord, Genre = Genre.Men };
        var draw = new DrawModel { Id = 3, CategoryId = 3, Category = category };

        var c1 = new CompetitorModel { Id = 1, FirstName = "David", LastName = "Roux", Club = "Wushu Paris", CountryIsoCode = "FR" };
        var c2 = new CompetitorModel { Id = 2, FirstName = "Alexandre", LastName = "Bernard", Club = "KungFu Lyon", CountryIsoCode = "FR" };
        var c3 = new CompetitorModel { Id = 3, FirstName = "Thomas", LastName = "Moreau", Club = "Shaolin Marseille", CountryIsoCode = "FR" };

        var order1 = new DrawOrderModel
        {
            Id = 1,
            Order = 1,
            CompetitorId = c1.Id,
            Competitor = c1,
            RoundResultsJson = "9.45",
            IsFinished = true
        };

        var order2 = new DrawOrderModel
        {
            Id = 2,
            Order = 2,
            CompetitorId = c2.Id,
            Competitor = c2,
            RoundResultsJson = "9.60",
            IsFinished = true
        };

        var order3 = new DrawOrderModel
        {
            Id = 3,
            Order = 3,
            CompetitorId = c3.Id,
            Competitor = c3,
            RoundResultsJson = "",
            IsFinished = false
        };

        var orders = new List<DrawOrderModel> { order1, order2, order3 };

        // Act
        using var stream = await _service.ExportTaoluOrderAsync(draw, orders, "Open International Taolu 2026");

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        stream.Position = 0;
        var header = new byte[4];
        int bytesRead = stream.Read(header, 0, 4);
        Assert.Equal(4, bytesRead);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(header));
    }

    [Fact]
    public async Task ExportAllDrawsAsync_GeneratesCombinedPdfDocument()
    {
        // Arrange
        var catKo = new CategoryModel { Id = 10, Type = CategoryType.Sanda, Genre = Genre.Men };
        var catPool = new CategoryModel { Id = 20, Type = CategoryType.Sanda, Genre = Genre.Women, RoundType = RoundType.Pools };
        var catTaolu = new CategoryModel { Id = 30, Type = CategoryType.TaoluModerneNord, Genre = Genre.Men };

        var drawKo = new DrawModel
        {
            Id = 10,
            CategoryId = 10,
            Category = catKo,
            DrawKnockouts = new List<DrawKnockoutModel>
            {
                new() { Id = 1, Order = 1, IsFinished = false }
            }
        };

        var drawPool = new DrawModel
        {
            Id = 20,
            CategoryId = 20,
            Category = catPool,
            DrawPools = new List<DrawPoolsModel>
            {
                new() { Id = 2, Order = 1, IsFinished = false }
            }
        };

        var drawTaolu = new DrawModel
        {
            Id = 30,
            CategoryId = 30,
            Category = catTaolu,
            DrawOrders = new List<DrawOrderModel>
            {
                new() { Id = 3, Order = 1, IsFinished = false }
            }
        };

        var draws = new List<DrawModel> { drawKo, drawPool, drawTaolu };

        // Act
        using var stream = await _service.ExportAllDrawsAsync(draws, "Tournoi Global");

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        stream.Position = 0;
        var header = new byte[4];
        int bytesRead = stream.Read(header, 0, 4);
        Assert.Equal(4, bytesRead);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(header));
    }

    [Fact]
    public async Task ExportKnockoutBracketAsync_WithDifferentCountryRepresentations_EmbedsFlagsProperly()
    {
        // Arrange
        var category = new CategoryModel { Id = 1, Type = CategoryType.Sanda, Genre = Genre.Men, WeightMax = 70 };
        var draw = new DrawModel { Id = 1, CategoryId = 1, Category = category };

        // Compétiteur 1 : Code ISO direct
        var c1 = new CompetitorModel { Id = 1, FirstName = "Antoine", LastName = "Griezmann", CountryIsoCode = "FR" };
        // Compétiteur 2 : Country.IsoCode
        var c2 = new CompetitorModel { Id = 2, FirstName = "Lionel", LastName = "Messi", Country = new CountryModel { Name = "Argentine", IsoCode = "AR" } };
        // Compétiteur 3 : Country.Name seul (résolu via DefaultCountries)
        var c3 = new CompetitorModel { Id = 3, FirstName = "Cristiano", LastName = "Ronaldo", Country = new CountryModel { Name = "Portugal" } };
        // Compétiteur 4 : Inconnu ou sans pays
        var c4 = new CompetitorModel { Id = 4, FirstName = "John", LastName = "Doe" };

        var match1 = new DrawKnockoutModel
        {
            Id = 1,
            Order = 1,
            Competitor1Id = c1.Id,
            Competitor1 = c1,
            Competitor2Id = c2.Id,
            Competitor2 = c2,
            WinnerId = c1.Id,
            IsFinished = true
        };

        var match2 = new DrawKnockoutModel
        {
            Id = 2,
            Order = 2,
            Competitor1Id = c3.Id,
            Competitor1 = c3,
            Competitor2Id = c4.Id,
            Competitor2 = c4,
            WinnerId = c3.Id,
            IsFinished = true
        };

        var matchFinal = new DrawKnockoutModel
        {
            Id = 3,
            Order = 3,
            Competitor1Id = c1.Id,
            Competitor1 = c1,
            Competitor2Id = c3.Id,
            Competitor2 = c3,
            IsFinished = false
        };

        var knockouts = new List<DrawKnockoutModel> { match1, match2, matchFinal };

        // Act
        using var stream = await _service.ExportKnockoutBracketAsync(draw, knockouts, "Coupe du Monde");

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 1000);

        stream.Position = 0;
        var header = new byte[4];
        int bytesRead = stream.Read(header, 0, 4);
        Assert.Equal(4, bytesRead);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(header));
    }

    [Fact]
    public void GetFirstMatchOrder_ReturnsLowestGlobalOrder_OrOrder()
    {
        // Tirage 1 : match commence au GlobalOrder 15
        var draw1 = new DrawModel
        {
            Id = 1,
            CategoryId = 1,
            DrawKnockouts = new List<DrawKnockoutModel>
            {
                new() { Order = 1, GlobalOrder = 15 },
                new() { Order = 2, GlobalOrder = 20 }
            }
        };

        // Tirage 2 : match commence au GlobalOrder 3
        var draw2 = new DrawModel
        {
            Id = 2,
            CategoryId = 2,
            DrawKnockouts = new List<DrawKnockoutModel>
            {
                new() { Order = 1, GlobalOrder = 3 },
                new() { Order = 2, GlobalOrder = 12 }
            }
        };

        // Tirage 3 : Poule avec GlobalOrder 1
        var draw3 = new DrawModel
        {
            Id = 3,
            CategoryId = 3,
            DrawPools = new List<DrawPoolsModel>
            {
                new() { Order = 1, GlobalOrder = 1 }
            }
        };

        int order1 = DrawPdfExportService.GetFirstMatchOrder(draw1);
        int order2 = DrawPdfExportService.GetFirstMatchOrder(draw2);
        int order3 = DrawPdfExportService.GetFirstMatchOrder(draw3);

        Assert.Equal(15, order1);
        Assert.Equal(3, order2);
        Assert.Equal(1, order3);

        // Tri des tirages selon l'ordre du premier combat
        var list = new List<DrawModel> { draw1, draw2, draw3 };
        var sorted = list.OrderBy(d => DrawPdfExportService.GetFirstMatchOrder(d)).ToList();

        Assert.Equal(3, sorted[0].Id); // Combat 1
        Assert.Equal(2, sorted[1].Id); // Combat 3
        Assert.Equal(1, sorted[2].Id); // Combat 15
    }

    [Fact]
    public async Task ExportKnockoutBracketAsync_WithByeMatch_GeneratesPdfSuccessfully()
    {
        // 3 compétiteurs dans un tableau à 4 : 1 quart avec match réel, 1 quart avec bye (1 seul compétiteur)
        var category = new CategoryModel { Id = 1, Type = CategoryType.Sanda, Genre = Genre.Men, WeightMax = 65 };
        var draw = new DrawModel { Id = 1, CategoryId = 1, Category = category };

        var c1 = new CompetitorModel { Id = 1, FirstName = "Jean", LastName = "Dupont", CountryIsoCode = "FR" };
        var c2 = new CompetitorModel { Id = 2, FirstName = "Pierre", LastName = "Durand", CountryIsoCode = "FR" };
        var c3 = new CompetitorModel { Id = 3, FirstName = "Paul", LastName = "Moreau", CountryIsoCode = "FR" };

        // Match 1 : Bye (1 seul compétiteur) -> GlobalOrder = 0, IsBye = true
        var byeMatch = new DrawKnockoutModel
        {
            Id = 1,
            Order = 1,
            GlobalOrder = 0,
            Competitor1 = c1,
            Competitor1Id = c1.Id,
            Competitor2 = null,
            Competitor2Id = null,
            IsFinished = true,
            IsBye = true,
            WinnerId = c1.Id
        };

        // Match 2 : Duel réel -> GlobalOrder = 1
        var realMatch = new DrawKnockoutModel
        {
            Id = 2,
            Order = 2,
            GlobalOrder = 1,
            Competitor1 = c2,
            Competitor1Id = c2.Id,
            Competitor2 = c3,
            Competitor2Id = c3.Id,
            IsFinished = false
        };

        // Match 3 : Finale
        var finale = new DrawKnockoutModel
        {
            Id = 3,
            Order = 3,
            GlobalOrder = 2,
            Competitor1 = c1,
            Competitor1Id = c1.Id,
            Competitor2 = null,
            Competitor2Id = null,
            IsFinished = false
        };

        var knockouts = new List<DrawKnockoutModel> { byeMatch, realMatch, finale };

        using var stream = await _service.ExportKnockoutBracketAsync(draw, knockouts, "Tournoi Test Byes");

        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public void GetFirstMatchOrder_WithByesHavingZeroGlobalOrder_PicksFirstRealMatch()
    {
        // Catégorie avec 1 bye (GlobalOrder = 0) et 1 match réel (GlobalOrder = 5)
        var draw = new DrawModel
        {
            Id = 1,
            CategoryId = 1,
            DrawKnockouts = new List<DrawKnockoutModel>
            {
                new() { Order = 1, GlobalOrder = 0, IsFinished = true, Competitor1Id = 1, Competitor2Id = null, IsBye = true },
                new() { Order = 2, GlobalOrder = 5, Competitor1Id = 2, Competitor2Id = 3 }
            }
        };

        int firstMatchOrder = DrawPdfExportService.GetFirstMatchOrder(draw);

        // L'ordre du premier combat doit être 5 (et non 0 car le bye ne combat pas!)
        Assert.Equal(5, firstMatchOrder);
    }
}
