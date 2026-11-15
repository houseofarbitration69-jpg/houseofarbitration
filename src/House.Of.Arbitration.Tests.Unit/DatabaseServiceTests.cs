using House.Of.Arbitration.Data;
using House.Of.Arbitration.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace House.Of.Arbitration.Tests.Unit;

public class DatabaseServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly DatabaseService _databaseService;

    public DatabaseServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _databaseService = new DatabaseService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ImportCompetitionDataAsync_ImportsAllEntitiesCorrectly()
    {
        // Arrange
        var competition = new CompetitionModel
        {
            Id = 1,
            Name = "Championnat de France 2026",
            Date = new DateTime(2026, 6, 15),
            Type = CompetitionType.Sanda,
            Categories = new List<CategoryModel>
            {
                new CategoryModel
                {
                    Id = 10,
                    CompetitionId = 1,
                    Type = CategoryType.Sanda,
                    Genre = Genre.Men,
                    WeightMin = 60,
                    WeightMax = 65,
                    AgeRangeId = 1,
                    RoundType = RoundType.Knockouts,
                    Competitors = new List<CompetitorCategoryModel>
                    {
                        new CompetitorCategoryModel
                        {
                            Id = 100,
                            CategoryId = 10,
                            CompetitorId = 501,
                            Competitor = new CompetitorModel
                            {
                                Id = 501,
                                FirstName = "Lucas",
                                LastName = "Martin",
                                Club = "Club A",
                                BirthDate = new DateTime(2005, 5, 20),
                                Genre = Genre.Men,
                                Weight = 63.5,
                                CountryIsoCode = "FR"
                            }
                        },
                        new CompetitorCategoryModel
                        {
                            Id = 101,
                            CategoryId = 10,
                            CompetitorId = 502,
                            Competitor = new CompetitorModel
                            {
                                Id = 502,
                                FirstName = "Thomas",
                                LastName = "Bernard",
                                Club = "Club B",
                                BirthDate = new DateTime(2004, 8, 12),
                                Genre = Genre.Men,
                                Weight = 64.0,
                                CountryIsoCode = "FR"
                            }
                        }
                    },
                    Draw = new DrawModel
                    {
                        Id = 200,
                        CategoryId = 10,
                        DrawKnockouts = new List<DrawKnockoutModel>
                        {
                            new DrawKnockoutModel
                            {
                                Id = 300,
                                DrawId = 200,
                                Order = 1,
                                GlobalOrder = 1,
                                Competitor1Id = 501,
                                Competitor2Id = 502,
                                CurrentRound = 1
                            }
                        }
                    }
                }
            }
        };

        // Act
        await _databaseService.ImportCompetitionDataAsync(competition);

        // Assert
        var savedComp = await _context.Competitions.FirstOrDefaultAsync(c => c.Id == 1);
        Assert.NotNull(savedComp);
        Assert.Equal("Championnat de France 2026", savedComp.Name);

        var savedCat = await _context.Categories.FirstOrDefaultAsync(c => c.Id == 10);
        Assert.NotNull(savedCat);
        Assert.Equal(1, savedCat.CompetitionId);

        var competitor1 = await _context.Competitors.FirstOrDefaultAsync(c => c.Id == 501);
        Assert.NotNull(competitor1);
        Assert.Equal("Lucas", competitor1.FirstName);
        Assert.Equal("Martin", competitor1.LastName);
        Assert.Equal("FR", competitor1.CountryIsoCode);

        var competitor2 = await _context.Competitors.FirstOrDefaultAsync(c => c.Id == 502);
        Assert.NotNull(competitor2);
        Assert.Equal("Thomas", competitor2.FirstName);

        var compCat = await _context.CompetitorCategories.FirstOrDefaultAsync(cc => cc.CategoryId == 10 && cc.CompetitorId == 501);
        Assert.NotNull(compCat);

        var draw = await _context.Draws.FirstOrDefaultAsync(d => d.Id == 200);
        Assert.NotNull(draw);
        Assert.Equal(10, draw.CategoryId);

        var knockout = await _context.DrawsKnockouts
            .Include(k => k.Draw)
                .ThenInclude(d => d.Category)
            .Include(k => k.Competitor1)
            .Include(k => k.Competitor2)
            .FirstOrDefaultAsync(k => k.Id == 300);

        Assert.NotNull(knockout);
        Assert.Equal(501, knockout.Competitor1Id);
        Assert.Equal(502, knockout.Competitor2Id);
        Assert.Equal("Lucas", knockout.Competitor1?.FirstName);
        Assert.Equal("Thomas", knockout.Competitor2?.FirstName);
        Assert.Equal(10, knockout.Draw?.Category?.Id);
    }

    [Fact]
    public async Task ImportCompetitionDataAsync_ReimportUpdatesWithoutDuplicating()
    {
        // Arrange
        var competition = new CompetitionModel
        {
            Id = 2,
            Name = "Open Régional",
            Date = new DateTime(2026, 4, 1),
            Type = CompetitionType.Taolu,
            Categories = new List<CategoryModel>
            {
                new CategoryModel
                {
                    Id = 20,
                    CompetitionId = 2,
                    Type = CategoryType.Taolu,
                    Genre = Genre.Women,
                    RoundType = RoundType.Order,
                    Competitors = new List<CompetitorCategoryModel>
                    {
                        new CompetitorCategoryModel
                        {
                            Id = 201,
                            CategoryId = 20,
                            CompetitorId = 601,
                            Competitor = new CompetitorModel
                            {
                                Id = 601,
                                FirstName = "Sophie",
                                LastName = "Durand",
                                CountryIsoCode = "FR"
                            }
                        }
                    },
                    Draw = new DrawModel
                    {
                        Id = 220,
                        CategoryId = 20,
                        DrawOrders = new List<DrawOrderModel>
                        {
                            new DrawOrderModel
                            {
                                Id = 230,
                                DrawId = 220,
                                Order = 1,
                                CompetitorId = 601
                            }
                        }
                    }
                }
            }
        };

        // Act 1: Initial import
        await _databaseService.ImportCompetitionDataAsync(competition);

        // Modify in-memory
        competition.Name = "Open Régional - Modifié";
        competition.Categories[0].Competitors[0].Competitor!.FirstName = "Sophie-Anne";

        // Act 2: Re-import
        await _databaseService.ImportCompetitionDataAsync(competition);

        // Assert
        var competitions = await _context.Competitions.Where(c => c.Id == 2).ToListAsync();
        Assert.Single(competitions);
        Assert.Equal("Open Régional - Modifié", competitions[0].Name);

        var competitor = await _context.Competitors.FirstOrDefaultAsync(c => c.Id == 601);
        Assert.NotNull(competitor);
        Assert.Equal("Sophie-Anne", competitor.FirstName);
    }

    [Fact]
    public async Task ImportMatchDataAsync_ImportsMatchOnDemand_WithoutPriorMatchesInDb()
    {
        // 1. Importer d'abord la compétition allégée (compétition + catégorie sans matchs)
        var competition = new CompetitionModel
        {
            Id = 10,
            Name = "Open Test On Demand",
            Date = DateTime.Today,
            Type = CompetitionType.Sanda,
            Categories = new List<CategoryModel>
            {
                new()
                {
                    Id = 101,
                    Genre = Genre.Men,
                    WeightMax = 65,
                    Type = CategoryType.Sanda,
                    RoundType = RoundType.Knockouts,
                    Draw = new DrawModel { Id = 101, CategoryId = 101 }
                }
            }
        };

        await _databaseService.ImportCompetitionDataAsync(competition);

        // Vérifier qu'aucun match n'est dans la DB
        var matchesBefore = await _context.DrawsKnockouts.ToListAsync();
        Assert.Empty(matchesBefore);

        // 2. Simuler l'arrivée d'un match au fur et à mesure via MATCH_INFO
        var comp1 = new CompetitorModel { Id = 501, FirstName = "Jean", LastName = "Rouge" };
        var comp2 = new CompetitorModel { Id = 502, FirstName = "Marc", LastName = "Bleu" };

        var matchData = new MatchInfoData
        {
            Id = 1,
            Type = RoundType.Knockouts,
            Round = 1,
            CategoryId = 101,
            CategoryName = "Senior -65kg",
            Competitor1 = comp1,
            Competitor2 = comp2,
            DrawKnockout = new DrawKnockoutModel
            {
                Id = 1,
                Order = 1,
                GlobalOrder = 1,
                DrawId = 101,
                Competitor1Id = 501,
                Competitor1 = comp1,
                Competitor2Id = 502,
                Competitor2 = comp2,
                CurrentRound = 1
            }
        };

        await _databaseService.ImportMatchDataAsync(matchData);

        // Vérifier que le match a été correctement inséré
        var savedMatch = await _context.DrawsKnockouts.FirstOrDefaultAsync(k => k.Id == 1);
        Assert.NotNull(savedMatch);
        Assert.Equal(501, savedMatch.Competitor1Id);
        Assert.Equal(502, savedMatch.Competitor2Id);
        Assert.Equal(1, savedMatch.CurrentRound);
    }

    [Fact]
    public async Task ImportMatchDataAsync_KnockoutFinalMatch_PopulatesCompetitorsWhenInitiallyEmpty()
    {
        // Arrange : simulation d'un match de finale (id 3) qui n'avait aucun compétiteur au début
        var competition = new CompetitionModel
        {
            Id = 2,
            Name = "Tournoi Test Finales",
            Date = DateTime.Today,
            Type = CompetitionType.Sanda,
            Categories = new List<CategoryModel>
            {
                new()
                {
                    Id = 201,
                    Genre = Genre.Men,
                    WeightMax = 70,
                    Type = CategoryType.Sanda,
                    RoundType = RoundType.Knockouts,
                    Draw = new DrawModel
                    {
                        Id = 201,
                        CategoryId = 201,
                        DrawKnockouts = new List<DrawKnockoutModel>
                        {
                            new()
                            {
                                Id = 3,
                                Order = 3,
                                GlobalOrder = 3,
                                DrawId = 201,
                                Competitor1Id = null,
                                Competitor2Id = null,
                                CurrentRound = 1
                            }
                        }
                    }
                }
            }
        };

        // 1. Initialement, téléchargement léger : la compétition et ses catégories sont créées
        await _databaseService.ImportCompetitionDataAsync(competition);

        // 2. Plus tard, les demi-finales se jouent et les vainqueurs se qualifient
        var comp1 = new CompetitorModel { Id = 601, FirstName = "Lucas", LastName = "Dupont", Club = "Club A" };
        var comp2 = new CompetitorModel { Id = 602, FirstName = "Wei", LastName = "Chen", Club = "Club B" };

        var matchData = new MatchInfoData
        {
            Id = 3,
            Type = RoundType.Knockouts,
            Round = 1,
            CategoryId = 201,
            CategoryName = "Sanda -70kg",
            Competitor1 = comp1,
            Competitor2 = comp2,
            DrawKnockout = new DrawKnockoutModel
            {
                Id = 3,
                Order = 3,
                GlobalOrder = 3,
                DrawId = 201,
                Competitor1Id = 601,
                Competitor1 = comp1,
                Competitor2Id = 602,
                Competitor2 = comp2,
                CurrentRound = 1
            }
        };

        // Act : le juge reçoit MATCH_INFO de la finale avec les compétiteurs résolus
        await _databaseService.ImportMatchDataAsync(matchData);

        // Assert : Les compétiteurs existent en base et le match de finale a ses CompetitorIds mis à jour
        var savedComp1 = await _context.Competitors.FirstOrDefaultAsync(c => c.Id == 601);
        var savedComp2 = await _context.Competitors.FirstOrDefaultAsync(c => c.Id == 602);
        Assert.NotNull(savedComp1);
        Assert.Equal("Lucas", savedComp1.FirstName);
        Assert.Equal("Dupont", savedComp1.LastName);
        Assert.NotNull(savedComp2);
        Assert.Equal("Wei", savedComp2.FirstName);
        Assert.Equal("Chen", savedComp2.LastName);

        var finalMatch = await _context.DrawsKnockouts.FirstOrDefaultAsync(k => k.Id == 3);
        Assert.NotNull(finalMatch);
        Assert.Equal(601, finalMatch.Competitor1Id);
        Assert.Equal(602, finalMatch.Competitor2Id);
    }
}
