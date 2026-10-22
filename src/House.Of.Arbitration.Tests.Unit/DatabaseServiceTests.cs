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
}
