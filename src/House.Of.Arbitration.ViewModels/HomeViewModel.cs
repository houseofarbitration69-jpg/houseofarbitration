#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    #region Services
    private readonly IRepository<CompetitionModel> _competitions;
    private readonly IRepository<CompetitorModel> _competitors;
    private readonly IRepository<CategoryModel> _categories;
    private readonly IRepository<CompetitorCategoryModel> _competitorsCategories;
    private readonly IRepository<DrawModel> _draws;
    private readonly IRepository<DrawPoolsModel> _drawPools;
    private readonly IRepository<DrawKnockoutModel> _drawKnockouts;
    #endregion

    #region Attributs
    private string _title = String.Empty;
    #endregion

    #region Properties
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    #endregion

    #region Constructors
    public HomeViewModel(
        ILogger<HomeViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService,
        IRepository<CompetitionModel> competitions,
        IRepository<CategoryModel> categories,
        IRepository<CompetitorModel> competitors,
        IRepository<CompetitorCategoryModel> competitorsCategories,
        IRepository<DrawModel> draws,
        IRepository<DrawPoolsModel> drawPools,
        IRepository<DrawKnockoutModel> drawKnockout
    ) : base(logger, resourceProvider, popupService)
    {
        Title = resourceProvider.APPLICATION_NAME;

        _competitions = competitions;
        _categories = categories;
        _competitors = competitors;
        _competitorsCategories = competitorsCategories;
        _draws = draws;
        _drawPools = drawPools;
        _drawKnockouts = drawKnockout;
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task SeedData()
    {
        CategoryModel category;
        CompetitorModel competitor;
        DrawModel draw;
        DrawPoolsModel drawPool;
        DrawKnockoutModel drawKnockout;

        var competition = new CompetitionModel()
        {
            Id = 1,
            Date = new DateTime(2026, 2, 14),
            Name = "Championnat de France 2025-2026",
        };
        await _competitions.AddAsync(competition);

        #region Taolu
        category = new CategoryModel()
        {
            Id = 100,
            AgeRangeId = 1,
            Genre = Genre.Men,
            RoundType = RoundType.Order,
            Type = CategoryType.Taolu,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);

        competitor = new CompetitorModel()
        {
            Id = 100,
            FirstName = "Firsname",
            LastName = "Lastname",
            Club = "Punch Team Sanda",
            BirthDate = new DateTime(2013, 1, 20),
            Genre = Genre.Men,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 100, CompetitorId = 100 });

        draw = new DrawModel() { Id = 100, CategoryId = 100 };
        await _draws.AddAsync(draw);

        drawPool = new DrawPoolsModel() { Id = 100, Order = 1, Competitor1Id = 100, DrawId = 100, GlobalOrder = 0 };
        await _drawPools.AddAsync(drawPool);
        #endregion

        #region Sanda Light / Masculin / Cadets / -65kg
        category = new CategoryModel()
        {
            Id = 1,
            AgeRangeId = 1,
            Genre = Genre.Men,
            RoundType = RoundType.Pools,
            Type = CategoryType.SandaLight,
            WeightMax = 65,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 1,
            FirstName = "Ismaël",
            LastName = "Benharrats",
            Club = "Punch Team Sanda",
            BirthDate = new DateTime(2013, 1, 20),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 2,
            FirstName = "Morgan",
            LastName = "Laur",
            Club = "S'Fight Academy",
            BirthDate = new DateTime(2013, 1, 4),
            Genre = Genre.Men,
            Weight = 63,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 3,
            FirstName = "Amil",
            LastName = "Ceman",
            Club = "Association Tigre du Sud",
            BirthDate = new DateTime(2013, 1, 15),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 1, CompetitorId = 1 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 1, CompetitorId = 2 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 1, CompetitorId = 3 });

        draw = new DrawModel() { Id = 1, CategoryId = 1 };
        await _draws.AddAsync(draw);

        drawPool = new DrawPoolsModel() { Id = 1, Order = 1, Competitor1Id = 1, Competitor2Id = 2, DrawId = 1, GlobalOrder = 1 };
        await _drawPools.AddAsync(drawPool);

        drawPool = new DrawPoolsModel() { Id = 2, Order = 2, Competitor1Id = 1, Competitor2Id = 3, DrawId = 1, GlobalOrder = 18 };
        await _drawPools.AddAsync(drawPool);

        drawPool = new DrawPoolsModel() { Id = 3, Order = 3, Competitor1Id = 2, Competitor2Id = 3, DrawId = 1, GlobalOrder = 30 };
        await _drawPools.AddAsync(drawPool);
        #endregion

        #region Sanda Light / Masculin / Juniors / -60kg
        category = new CategoryModel()
        {
            Id = 2,
            AgeRangeId = 2,
            Genre = Genre.Men,
            RoundType = RoundType.Pools,
            Type = CategoryType.SandaLight,
            WeightMax = 60,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 4,
            FirstName = "Lyam",
            LastName = "Benmerzouq",
            Club = "French Federation of Chinese Energetic And Martial Arts",
            BirthDate = new DateTime(2009, 1, 10),
            Genre = Genre.Men,
            Weight = 60,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 5,
            FirstName = "Pablo",
            LastName = "Dury",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(2010, 2, 8),
            Genre = Genre.Men,
            Weight = 59,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 6,
            FirstName = "Adam",
            LastName = "Razine",
            Club = "Association Tigre du Sud",
            BirthDate = new DateTime(2009, 5, 15),
            Genre = Genre.Men,
            Weight = 58,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 2, CompetitorId = 4 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 2, CompetitorId = 5 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 2, CompetitorId = 6 });

        draw = new DrawModel() { Id = 2, CategoryId = 2 };
        await _draws.AddAsync(draw);

        drawPool = new DrawPoolsModel() { Id = 4, Order = 1, Competitor1Id = 4, Competitor2Id = 5, DrawId = 2, GlobalOrder = 2 };
        await _drawPools.AddAsync(drawPool);

        drawPool = new DrawPoolsModel() { Id = 5, Order = 2, Competitor1Id = 4, Competitor2Id = 6, DrawId = 2, GlobalOrder = 19 };
        await _drawPools.AddAsync(drawPool);

        drawPool = new DrawPoolsModel() { Id = 6, Order = 3, Competitor1Id = 5, Competitor2Id = 6, DrawId = 2, GlobalOrder = 31 };
        await _drawPools.AddAsync(drawPool);
        #endregion

        #region Sanda Light / Masculin / Seniors / -80kg
        category = new CategoryModel()
        {
            Id = 3,
            AgeRangeId = 3,
            Genre = Genre.Men,
            RoundType = RoundType.Pools,
            Type = CategoryType.SandaLight,
            WeightMax = 80,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 7,
            FirstName = "Kevin",
            LastName = "Gros",
            Club = "La voie du Tigre Blanc",
            BirthDate = new DateTime(2007, 2, 15),
            Genre = Genre.Men,
            Weight = 80,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 8,
            FirstName = "Paul",
            LastName = "Kleindienst",
            Club = "Association Tigre Du Sud",
            BirthDate = new DateTime(2006, 5, 8),
            Genre = Genre.Men,
            Weight = 78,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 9,
            FirstName = "Ludovic",
            LastName = "Courla",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(2007, 6, 12),
            Genre = Genre.Men,
            Weight = 79,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 3, CompetitorId = 7 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 3, CompetitorId = 8 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 3, CompetitorId = 9 });

        draw = new DrawModel() { Id = 3, CategoryId = 3 };
        await _draws.AddAsync(draw);

        drawPool = new DrawPoolsModel() { Id = 7, Order = 1, Competitor1Id = 7, Competitor2Id = 8, DrawId = 3, GlobalOrder = 3 };
        await _drawPools.AddAsync(drawPool);

        drawPool = new DrawPoolsModel() { Id = 8, Order = 2, Competitor1Id = 7, Competitor2Id = 9, DrawId = 3, GlobalOrder = 20 };
        await _drawPools.AddAsync(drawPool);

        drawPool = new DrawPoolsModel() { Id = 9, Order = 3, Competitor1Id = 8, Competitor2Id = 9, DrawId = 3, GlobalOrder = 32 };
        await _drawPools.AddAsync(drawPool);
        #endregion

        #region Sanda Light / Masculin / Veterans / -75kg
        category = new CategoryModel()
        {
            Id = 4,
            AgeRangeId = 5,
            Genre = Genre.Men,
            RoundType = RoundType.Pools,
            Type = CategoryType.SandaLight,
            WeightMax = 75,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 10,
            FirstName = "Fabien",
            LastName = "Dursapt",
            Club = "La voie du Tigre Blanc",
            BirthDate = new DateTime(1990, 3, 4),
            Genre = Genre.Men,
            Weight = 75,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 11,
            FirstName = "Benoist",
            LastName = "Delextrat",
            Club = "Kung Fu Niort 1171",
            BirthDate = new DateTime(1989, 6, 12),
            Genre = Genre.Men,
            Weight = 74,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 12,
            FirstName = "Sophien",
            LastName = "Naguib",
            Club = "Association Tigre Du Sud",
            BirthDate = new DateTime(1988, 7, 23),
            Genre = Genre.Men,
            Weight = 75,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 4, CompetitorId = 10 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 4, CompetitorId = 11 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 4, CompetitorId = 12 });

        draw = new DrawModel() { Id = 4, CategoryId = 4 };
        await _draws.AddAsync(draw);

        drawPool = new DrawPoolsModel() { Id = 10, Order = 1, Competitor1Id = 10, Competitor2Id = 11, DrawId = 4, GlobalOrder = 4 };
        await _drawPools.AddAsync(drawPool);

        drawPool = new DrawPoolsModel() { Id = 11, Order = 2, Competitor1Id = 10, Competitor2Id = 12, DrawId = 4, GlobalOrder = 21 };
        await _drawPools.AddAsync(drawPool);

        drawPool = new DrawPoolsModel() { Id = 12, Order = 3, Competitor1Id = 11, Competitor2Id = 12, DrawId = 4, GlobalOrder = 33 };
        await _drawPools.AddAsync(drawPool);
        #endregion

        #region Sanda / Masculin / Seniors / -65kg
        category = new CategoryModel()
        {
            Id = 5,
            AgeRangeId = 3,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 65,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 13,
            FirstName = "Pablo",
            LastName = "Espero",
            Club = "KungFuFighter",
            BirthDate = new DateTime(2007, 8, 3),
            Genre = Genre.Men,
            Weight = 65,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 14,
            FirstName = "Andreas",
            LastName = "Monteiro",
            Club = "Wulin Association",
            BirthDate = new DateTime(2007, 1, 12),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 15,
            FirstName = "Dario",
            LastName = "Dewet",
            Club = "Ecole Hoang Nam",
            BirthDate = new DateTime(2007, 2, 23),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 16,
            FirstName = "Matisse",
            LastName = "Thomas",
            Club = "KungFuFighter",
            BirthDate = new DateTime(2007, 3, 3),
            Genre = Genre.Men,
            Weight = 63,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 17,
            FirstName = "Deniz",
            LastName = "Akmaz",
            Club = "Takedown - Cluses MMA",
            BirthDate = new DateTime(2007, 4, 12),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 18,
            FirstName = "Jonathan",
            LastName = "Nguyen",
            Club = "Ecole Kim Dieu",
            BirthDate = new DateTime(2007, 5, 8),
            Genre = Genre.Men,
            Weight = 65,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 5, CompetitorId = 13 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 5, CompetitorId = 14 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 5, CompetitorId = 15 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 5, CompetitorId = 16 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 5, CompetitorId = 17 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 5, CompetitorId = 18 });

        draw = new DrawModel() { Id = 5, CategoryId = 5 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 1, Order = 1, Competitor1Id = 13, Competitor2Id = 14, DrawId = 5, GlobalOrder = 5 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 2, Order = 2, Competitor1Id = 15, WinnerId = 15, DrawId = 5, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 3, Order = 3, Competitor1Id = 16, Competitor2Id = 17, DrawId = 5, GlobalOrder = 6 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 4, Order = 4, Competitor1Id = 18, WinnerId = 18, DrawId = 5, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 5, Order = 5, Competitor2Id = 15, DrawId = 5, GlobalOrder = 22 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 6, Order = 6, Competitor2Id = 18, DrawId = 5, GlobalOrder = 23 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 7, Order = 7, DrawId = 5, GlobalOrder = 67 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Masculin / Seniors / -70kg
        category = new CategoryModel()
        {
            Id = 6,
            AgeRangeId = 3,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 70,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 19,
            FirstName = "Pierrick",
            LastName = "Moisan",
            Club = "Ecole Kim Dieu",
            BirthDate = new DateTime(2007, 9, 6),
            Genre = Genre.Men,
            Weight = 70,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 20,
            FirstName = "Kaan",
            LastName = "Yener",
            Club = "Takedown - Cluses MMA",
            BirthDate = new DateTime(2007, 9, 21),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 21,
            FirstName = "Emile",
            LastName = "Favarel-Gardennes",
            Club = "Wulin Association",
            BirthDate = new DateTime(2007, 2, 13),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 22,
            FirstName = "Albert",
            LastName = "Thomas",
            Club = "Sporting Club 390",
            BirthDate = new DateTime(2007, 5, 11),
            Genre = Genre.Men,
            Weight = 68,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 23,
            FirstName = "Alexandre",
            LastName = "Baylet",
            Club = "S'Fight Academy",
            BirthDate = new DateTime(2007, 4, 12),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 24,
            FirstName = "Eladjy",
            LastName = "Bissol",
            Club = "KungFuFighter",
            BirthDate = new DateTime(2007, 5, 8),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 6, CompetitorId = 19 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 6, CompetitorId = 20 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 6, CompetitorId = 21 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 6, CompetitorId = 22 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 6, CompetitorId = 23 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 6, CompetitorId = 24 });

        draw = new DrawModel() { Id = 6, CategoryId = 6 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 8, Order = 1, Competitor1Id = 19, Competitor2Id = 20, DrawId = 6, GlobalOrder = 7 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 9, Order = 2, Competitor1Id = 21, WinnerId = 21, DrawId = 6, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 10, Order = 3, Competitor1Id = 22, Competitor2Id = 23, DrawId = 6, GlobalOrder = 8 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 11, Order = 4, Competitor1Id = 24, WinnerId = 24, DrawId = 6, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 12, Order = 5, Competitor2Id = 21, DrawId = 6, GlobalOrder = 24 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 13, Order = 6, Competitor2Id = 24, DrawId = 6, GlobalOrder = 25 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 14, Order = 7, DrawId = 6, GlobalOrder = 68 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Seniors / -70kg
        category = new CategoryModel()
        {
            Id = 7,
            AgeRangeId = 3,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 70,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 25,
            FirstName = "Kindy",
            LastName = "Hebert-Robin",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(2007, 1, 1),
            Genre = Genre.Men,
            Weight = 70,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 26,
            FirstName = "Paul",
            LastName = "Luong",
            Club = "Tiger Boxing Club",
            BirthDate = new DateTime(2007, 8, 20),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 27,
            FirstName = "Anthony",
            LastName = "Coto",
            Club = "Team KCM 83",
            BirthDate = new DateTime(2007, 5, 5),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 28,
            FirstName = "Joseph",
            LastName = "D'ardaillon",
            Club = "(No team)",
            BirthDate = new DateTime(2007, 6, 23),
            Genre = Genre.Men,
            Weight = 68,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 29,
            FirstName = "Dorian",
            LastName = "Sedrati",
            Club = "Kung-Arts",
            BirthDate = new DateTime(2007, 7, 10),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 30,
            FirstName = "Maxime",
            LastName = "Pambrun",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(2007, 8, 11),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 7, CompetitorId = 25 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 7, CompetitorId = 26 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 7, CompetitorId = 27 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 7, CompetitorId = 28 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 7, CompetitorId = 29 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 7, CompetitorId = 30 });

        draw = new DrawModel() { Id = 7, CategoryId = 7 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 15, Order = 1, Competitor1Id = 25, Competitor2Id = 26, DrawId = 7, GlobalOrder = 9 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 16, Order = 2, Competitor1Id = 27, WinnerId = 27, DrawId = 7, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 17, Order = 3, Competitor1Id = 28, Competitor2Id = 29, DrawId = 7, GlobalOrder = 10 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 18, Order = 4, Competitor1Id = 30, WinnerId = 30, DrawId = 7, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 19, Order = 5, Competitor2Id = 27, DrawId = 7, GlobalOrder = 40 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 20, Order = 6, Competitor2Id = 30, DrawId = 7, GlobalOrder = 41 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 21, Order = 7, DrawId = 6, GlobalOrder = 61 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Seniors / -75kg
        category = new CategoryModel()
        {
            Id = 8,
            AgeRangeId = 3,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 75,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 31,
            FirstName = "Mauri",
            LastName = "Berete",
            Club = "French Federation of Chinese",
            BirthDate = new DateTime(2007, 6, 11),
            Genre = Genre.Men,
            Weight = 70,
            CountryIsoCode = "GN"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 32,
            FirstName = "Michaël",
            LastName = "Pras",
            Club = "La voie du Tigre Blanc",
            BirthDate = new DateTime(2007, 8, 20),
            Genre = Genre.Men,
            Weight = 70,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 33,
            FirstName = "Nathan",
            LastName = "Poux",
            Club = "Ecole Kim Dieu",
            BirthDate = new DateTime(2007, 9, 15),
            Genre = Genre.Men,
            Weight = 71,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 34,
            FirstName = "Lucas",
            LastName = "Gilmas",
            Club = "Association Kajyn",
            BirthDate = new DateTime(2007, 6, 23),
            Genre = Genre.Men,
            Weight = 72,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 35,
            FirstName = "Alexis",
            LastName = "Othman",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(2007, 7, 10),
            Genre = Genre.Men,
            Weight = 73,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 36,
            FirstName = "Tounka",
            LastName = "Carilien Subma",
            Club = "Wulin Assocation 31",
            BirthDate = new DateTime(2007, 8, 11),
            Genre = Genre.Men,
            Weight = 70,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 8, CompetitorId = 31 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 8, CompetitorId = 32 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 8, CompetitorId = 33 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 8, CompetitorId = 34 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 8, CompetitorId = 35 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 8, CompetitorId = 36 });

        draw = new DrawModel() { Id = 8, CategoryId = 8 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 22, Order = 1, Competitor1Id = 31, Competitor2Id = 32, DrawId = 8, GlobalOrder = 11 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 23, Order = 2, Competitor1Id = 33, WinnerId = 33, DrawId = 8, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 24, Order = 3, Competitor1Id = 34, Competitor2Id = 35, DrawId = 8, GlobalOrder = 12 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 25, Order = 4, Competitor1Id = 36, WinnerId = 36, DrawId = 8, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 26, Order = 5, Competitor2Id = 33, DrawId = 8, GlobalOrder = 42 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 27, Order = 6, Competitor2Id = 36, DrawId = 8, GlobalOrder = 43 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 28, Order = 7, DrawId = 8, GlobalOrder = 62 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Vétérans / -65kg
        category = new CategoryModel()
        {
            Id = 9,
            AgeRangeId = 5,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 65,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 37,
            FirstName = "Frédéric",
            LastName = "Thomas",
            Club = "Shaolin Alençon",
            BirthDate = new DateTime(1990, 1, 1),
            Genre = Genre.Men,
            Weight = 65,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 38,
            FirstName = "Clément",
            LastName = "Guigout",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(1990, 2, 20),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 39,
            FirstName = "Simon",
            LastName = "Boissard",
            Club = "Kung Fu / Jeet Kune Do",
            BirthDate = new DateTime(1990, 9, 15),
            Genre = Genre.Men,
            Weight = 63,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 40,
            FirstName = "Salim",
            LastName = "Ballout",
            Club = "Kung Fu Niort 1171",
            BirthDate = new DateTime(1990, 6, 23),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 41,
            FirstName = "Shivany",
            LastName = "Coupama",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(1990, 7, 10),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 42,
            FirstName = "Bruno",
            LastName = "Bertholiet",
            Club = "Courbevoie Kung Fu",
            BirthDate = new DateTime(1990, 8, 11),
            Genre = Genre.Men,
            Weight = 63,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 9, CompetitorId = 37 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 9, CompetitorId = 38 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 9, CompetitorId = 39 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 9, CompetitorId = 40 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 9, CompetitorId = 41 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 9, CompetitorId = 42 });

        draw = new DrawModel() { Id = 9, CategoryId = 9 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 29, Order = 1, Competitor1Id = 37, Competitor2Id = 38, DrawId = 9, GlobalOrder = 13 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 30, Order = 2, Competitor1Id = 39, WinnerId = 39, DrawId = 9, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 31, Order = 3, Competitor1Id = 40, Competitor2Id = 41, DrawId = 9, GlobalOrder = 14 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 32, Order = 4, Competitor1Id = 42, WinnerId = 42, DrawId = 9, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 33, Order = 5, Competitor2Id = 39, DrawId = 9, GlobalOrder = 46 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 34, Order = 6, Competitor2Id = 42, DrawId = 9, GlobalOrder = 47 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 35, Order = 7, DrawId = 9, GlobalOrder = 72 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Masculin / Seniors / -65kg
        category = new CategoryModel()
        {
            Id = 10,
            AgeRangeId = 3,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 60,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 43,
            FirstName = "Mikail",
            LastName = "Dagci",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(2007, 1, 1),
            Genre = Genre.Men,
            Weight = 60,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 44,
            FirstName = "Etienne",
            LastName = "Lemaitre",
            Club = "Tiger Boxing Club",
            BirthDate = new DateTime(2007, 2, 20),
            Genre = Genre.Men,
            Weight = 59,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 45,
            FirstName = "Laurent",
            LastName = "Dat",
            Club = "Wulin Association 31",
            BirthDate = new DateTime(2007, 9, 15),
            Genre = Genre.Men,
            Weight = 60,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 46,
            FirstName = "Yanis",
            LastName = "Montaud",
            Club = "Ecole Kim Dieu",
            BirthDate = new DateTime(2007, 6, 23),
            Genre = Genre.Men,
            Weight = 58,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 47,
            FirstName = "Trianhlanh",
            LastName = "Yang",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(2007, 7, 10),
            Genre = Genre.Men,
            Weight = 60,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);


        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 10, CompetitorId = 43 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 10, CompetitorId = 44 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 10, CompetitorId = 45 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 10, CompetitorId = 46 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 10, CompetitorId = 47 });

        draw = new DrawModel() { Id = 10, CategoryId = 10 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 36, Order = 1, Competitor1Id = 43, Competitor2Id = 44, DrawId = 10, GlobalOrder = 15 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 37, Order = 2, Competitor1Id = 45, WinnerId = 45, DrawId = 10, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 38, Order = 3, Competitor1Id = 46, WinnerId = 46, DrawId = 10, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 39, Order = 4, Competitor1Id = 47, WinnerId = 47, DrawId = 10, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 40, Order = 5, Competitor2Id = 45, DrawId = 10, GlobalOrder = 26 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 41, Order = 6, Competitor1Id = 46, Competitor2Id = 47, DrawId = 10, GlobalOrder = 27 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 42, Order = 7, DrawId = 10, GlobalOrder = 66 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Masculin / Seniors / -75kg
        category = new CategoryModel()
        {
            Id = 11,
            AgeRangeId = 3,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 75,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 48,
            FirstName = "Abdallah",
            LastName = "Allouane",
            Club = "KungFuFighter",
            BirthDate = new DateTime(2007, 7, 3),
            Genre = Genre.Men,
            Weight = 75,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 49,
            FirstName = "Kevin",
            LastName = "Gallon",
            Club = "Takedown - Cluses MMA",
            BirthDate = new DateTime(2007, 6, 22),
            Genre = Genre.Men,
            Weight = 74,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 50,
            FirstName = "Nassim",
            LastName = "Hallou",
            Club = "S'Fight Academy",
            BirthDate = new DateTime(2007, 9, 15),
            Genre = Genre.Men,
            Weight = 73,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 51,
            FirstName = "Milo",
            LastName = "Arpino",
            Club = "Association Kajyn",
            BirthDate = new DateTime(2007, 6, 23),
            Genre = Genre.Men,
            Weight = 70,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 52,
            FirstName = "Yoan",
            LastName = "Benbedra",
            Club = "Kung Arts",
            BirthDate = new DateTime(2007, 7, 10),
            Genre = Genre.Men,
            Weight = 74,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);


        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 11, CompetitorId = 48 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 11, CompetitorId = 49 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 11, CompetitorId = 50 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 11, CompetitorId = 51 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 11, CompetitorId = 52 });

        draw = new DrawModel() { Id = 11, CategoryId = 11 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 43, Order = 1, Competitor1Id = 48, WinnerId = 48, DrawId = 11, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 44, Order = 2, Competitor1Id = 49, WinnerId = 49, DrawId = 11, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 45, Order = 3, Competitor1Id = 50, Competitor2Id = 51, DrawId = 11, GlobalOrder = 16 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 46, Order = 4, Competitor1Id = 52, WinnerId = 52, DrawId = 11, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 47, Order = 5, Competitor1Id = 48, Competitor2Id = 49, DrawId = 11, GlobalOrder = 28 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 48, Order = 6, Competitor2Id = 52, DrawId = 11, GlobalOrder = 29 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 49, Order = 7, DrawId = 11, GlobalOrder = 69 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Juniors / -70kg
        category = new CategoryModel()
        {
            Id = 12,
            AgeRangeId = 2,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 70,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 53,
            FirstName = "Lucas",
            LastName = "Gillet",
            Club = "Association Tigre du Sud",
            BirthDate = new DateTime(2009, 7, 3),
            Genre = Genre.Men,
            Weight = 70,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 54,
            FirstName = "Theo",
            LastName = "Garcia",
            Club = "Wulin Association 31",
            BirthDate = new DateTime(2009, 7, 25),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 55,
            FirstName = "Lucien",
            LastName = "Magendie",
            Club = "La voie du Tigre Blanc",
            BirthDate = new DateTime(2009, 9, 13),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 56,
            FirstName = "Dorian",
            LastName = "Gimenez",
            Club = "S'Fight Academy",
            BirthDate = new DateTime(2009, 7, 23),
            Genre = Genre.Men,
            Weight = 68,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 57,
            FirstName = "Yanis",
            LastName = "Aloui",
            Club = "Kung Arts",
            BirthDate = new DateTime(2009, 7, 10),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);


        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 12, CompetitorId = 53 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 12, CompetitorId = 54 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 12, CompetitorId = 55 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 12, CompetitorId = 56 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 12, CompetitorId = 57 });

        draw = new DrawModel() { Id = 12, CategoryId = 12 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 50, Order = 1, Competitor1Id = 53, Competitor2Id = 54, DrawId = 12, GlobalOrder = 17 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 51, Order = 2, Competitor1Id = 55, WinnerId = 55, DrawId = 12, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 52, Order = 3, Competitor1Id = 56, WinnerId = 56, DrawId = 12, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 53, Order = 4, Competitor1Id = 57, WinnerId = 57, DrawId = 12, IsFinished = true };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 54, Order = 5, Competitor2Id = 55, DrawId = 12, GlobalOrder = 38 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 55, Order = 6, Competitor1Id = 56, Competitor2Id = 57, DrawId = 12, GlobalOrder = 39 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 56, Order = 7, DrawId = 12, GlobalOrder = 55 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Masculin / Seniors / -80kg
        category = new CategoryModel()
        {
            Id = 13,
            AgeRangeId = 3,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 80,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 58,
            FirstName = "Gary",
            LastName = "Gouala",
            Club = "KungFuFighter",
            BirthDate = new DateTime(2007, 7, 5),
            Genre = Genre.Men,
            Weight = 80,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 59,
            FirstName = "Adrien",
            LastName = "Rebelo",
            Club = "Wulin Association 31",
            BirthDate = new DateTime(2007, 7, 23),
            Genre = Genre.Men,
            Weight = 75,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 60,
            FirstName = "Mehdi",
            LastName = "Ladjadj",
            Club = "Takedown - Cluses MMA",
            BirthDate = new DateTime(2007, 9, 11),
            Genre = Genre.Men,
            Weight = 78,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 61,
            FirstName = "Pierre-Emmanuel",
            LastName = "Tokenel",
            Club = "Association Kajyn",
            BirthDate = new DateTime(2007, 6, 25),
            Genre = Genre.Men,
            Weight = 72,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 13, CompetitorId = 58 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 13, CompetitorId = 59 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 13, CompetitorId = 60 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 13, CompetitorId = 61 });

        draw = new DrawModel() { Id = 13, CategoryId = 13 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 57, Order = 1, Competitor1Id = 58, Competitor2Id = 59, DrawId = 13, GlobalOrder = 34 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 58, Order = 2, Competitor1Id = 60, Competitor2Id = 61, DrawId = 13, GlobalOrder = 35 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 59, Order = 3, DrawId = 13, GlobalOrder = 70 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Juniors / -65kg
        category = new CategoryModel()
        {
            Id = 14,
            AgeRangeId = 2,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 65,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 62,
            FirstName = "Titouan",
            LastName = "Cattelin",
            Club = "La voie du Tigre Blanc",
            BirthDate = new DateTime(2009, 7, 5),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 63,
            FirstName = "Adam",
            LastName = "Sartre",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(2009, 7, 23),
            Genre = Genre.Men,
            Weight = 63,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 64,
            FirstName = "Phileas",
            LastName = "Bracq",
            Club = "Courbevoie Kung Fu",
            BirthDate = new DateTime(2009, 9, 11),
            Genre = Genre.Men,
            Weight = 65,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 65,
            FirstName = "Yiann",
            LastName = "Stosse",
            Club = "Team KCM 83",
            BirthDate = new DateTime(2009, 6, 25),
            Genre = Genre.Men,
            Weight = 65,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 14, CompetitorId = 62 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 14, CompetitorId = 63 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 14, CompetitorId = 64 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 14, CompetitorId = 65 });

        draw = new DrawModel() { Id = 14, CategoryId = 14 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 60, Order = 1, Competitor1Id = 62, Competitor2Id = 63, DrawId = 14, GlobalOrder = 36 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 61, Order = 2, Competitor1Id = 64, Competitor2Id = 65, DrawId = 14, GlobalOrder = 37 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 62, Order = 3, DrawId = 14, GlobalOrder = 54 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Vétérans / -60kg
        category = new CategoryModel()
        {
            Id = 15,
            AgeRangeId = 5,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 60,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 66,
            FirstName = "Sebastien",
            LastName = "Girardot-Blanc",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(1990, 7, 11),
            Genre = Genre.Men,
            Weight = 60,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 67,
            FirstName = "David",
            LastName = "Lombardo",
            Club = "ICAM SGL",
            BirthDate = new DateTime(1990, 1, 23),
            Genre = Genre.Men,
            Weight = 59,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 68,
            FirstName = "Patrice",
            LastName = "Dias Serrao",
            Club = "Association Tigre du Sud",
            BirthDate = new DateTime(1990, 9, 11),
            Genre = Genre.Men,
            Weight = 60,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 69,
            FirstName = "Alexandre",
            LastName = "Paturel",
            Club = "L'empreinte de l'Ours",
            BirthDate = new DateTime(1990, 6, 25),
            Genre = Genre.Men,
            Weight = 59,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 15, CompetitorId = 66 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 15, CompetitorId = 67 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 15, CompetitorId = 68 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 15, CompetitorId = 69 });

        draw = new DrawModel() { Id = 15, CategoryId = 15 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 63, Order = 1, Competitor1Id = 66, Competitor2Id = 67, DrawId = 15, GlobalOrder = 44 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 64, Order = 2, Competitor1Id = 68, Competitor2Id = 69, DrawId = 15, GlobalOrder = 45 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 65, Order = 3, DrawId = 15, GlobalOrder = 71 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Vétérans / -70kg
        category = new CategoryModel()
        {
            Id = 16,
            AgeRangeId = 5,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 70,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 70,
            FirstName = "Thibault",
            LastName = "Olivieri",
            Club = "Tiger Boxing Club",
            BirthDate = new DateTime(1990, 7, 11),
            Genre = Genre.Men,
            Weight = 70,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 71,
            FirstName = "Julien",
            LastName = "Barral-Cadic",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(1990, 5, 23),
            Genre = Genre.Men,
            Weight = 69,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 72,
            FirstName = "Clément",
            LastName = "Monnier",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(1990, 9, 11),
            Genre = Genre.Men,
            Weight = 65,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 73,
            FirstName = "Michael",
            LastName = "Porteiro",
            Club = "Shaolin Wu Gong",
            BirthDate = new DateTime(1990, 6, 25),
            Genre = Genre.Men,
            Weight = 66,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 16, CompetitorId = 70 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 16, CompetitorId = 71 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 16, CompetitorId = 72 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 16, CompetitorId = 73 });

        draw = new DrawModel() { Id = 16, CategoryId = 16 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 66, Order = 1, Competitor1Id = 70, Competitor2Id = 71, DrawId = 16, GlobalOrder = 48 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 67, Order = 2, Competitor1Id = 72, Competitor2Id = 73, DrawId = 16, GlobalOrder = 49 };
        await _drawKnockouts.AddAsync(drawKnockout);

        drawKnockout = new DrawKnockoutModel() { Id = 68, Order = 3, DrawId = 16, GlobalOrder = 73 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Feminin / Cadet / -52kg
        category = new CategoryModel()
        {
            Id = 17,
            AgeRangeId = 1,
            Genre = Genre.Women,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 52,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 74,
            FirstName = "Melinda",
            LastName = "Song",
            Club = "Wulin Assocaition 31",
            BirthDate = new DateTime(2013, 7, 11),
            Genre = Genre.Women,
            Weight = 52,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 75,
            FirstName = "Lola",
            LastName = "Gemme Bascolan",
            Club = "Team LB Kung Fu",
            BirthDate = new DateTime(2013, 5, 23),
            Genre = Genre.Women,
            Weight = 51,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 17, CompetitorId = 74 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 17, CompetitorId = 75 });

        draw = new DrawModel() { Id = 17, CategoryId = 17 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 69, Order = 1, Competitor1Id = 74, Competitor2Id = 75, DrawId = 17, GlobalOrder = 50 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Masculin / Cadet / -56kg
        category = new CategoryModel()
        {
            Id = 18,
            AgeRangeId = 1,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 56,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 76,
            FirstName = "Mohamed",
            LastName = "Kerouani",
            Club = "Kung Fu Sanda Vaulx-en-Velin",
            BirthDate = new DateTime(2013, 7, 11),
            Genre = Genre.Men,
            Weight = 53,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 77,
            FirstName = "Baptiste",
            LastName = "Auguy",
            Club = "Sporting Club 390",
            BirthDate = new DateTime(2013, 5, 23),
            Genre = Genre.Men,
            Weight = 53,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 18, CompetitorId = 76 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 18, CompetitorId = 77 });

        draw = new DrawModel() { Id = 18, CategoryId = 18 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 70, Order = 1, Competitor1Id = 76, Competitor2Id = 77, DrawId = 18, GlobalOrder = 51 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Masculin / Cadet / -65kg
        category = new CategoryModel()
        {
            Id = 19,
            AgeRangeId = 1,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 65,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 78,
            FirstName = "Sevan",
            LastName = "Nadembega",
            Club = "Kung Fu Sanda Vaulx-en-Velin",
            BirthDate = new DateTime(2013, 7, 11),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 79,
            FirstName = "Yanis",
            LastName = "Goumi",
            Club = "Kung Fu Sanda Vaulx-en-Velin",
            BirthDate = new DateTime(2013, 5, 23),
            Genre = Genre.Men,
            Weight = 65,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 19, CompetitorId = 78 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 19, CompetitorId = 79 });

        draw = new DrawModel() { Id = 19, CategoryId = 19 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 71, Order = 1, Competitor1Id = 78, Competitor2Id = 79, DrawId = 19, GlobalOrder = 52 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Juniors / -56kg
        category = new CategoryModel()
        {
            Id = 20,
            AgeRangeId = 2,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 56,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 80,
            FirstName = "Nolan",
            LastName = "Baylet",
            Club = "S'Fight Academy",
            BirthDate = new DateTime(2009, 7, 11),
            Genre = Genre.Men,
            Weight = 55,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 81,
            FirstName = "Zyed",
            LastName = "Berrouiguet",
            Club = "Dojo Ricamandois",
            BirthDate = new DateTime(2009, 5, 23),
            Genre = Genre.Men,
            Weight = 54,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 20, CompetitorId = 80 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 20, CompetitorId = 81 });

        draw = new DrawModel() { Id = 20, CategoryId = 20 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 72, Order = 1, Competitor1Id = 80, Competitor2Id = 81, DrawId = 20, GlobalOrder = 53 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Feminin / Juniors / -52kg
        category = new CategoryModel()
        {
            Id = 21,
            AgeRangeId = 2,
            Genre = Genre.Women,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 52,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 82,
            FirstName = "Shirel",
            LastName = "Hassine",
            Club = "Team LB Kung Fu",
            BirthDate = new DateTime(2009, 7, 11),
            Genre = Genre.Women,
            Weight = 52,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 83,
            FirstName = "Clara",
            LastName = "Fernandes",
            Club = "Dragon des 3 Rivières",
            BirthDate = new DateTime(2009, 5, 23),
            Genre = Genre.Women,
            Weight = 51,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 21, CompetitorId = 82 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 21, CompetitorId = 83 });

        draw = new DrawModel() { Id = 21, CategoryId = 21 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 73, Order = 1, Competitor1Id = 82, Competitor2Id = 83, DrawId = 21, GlobalOrder = 56 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Masculin / Juniors / -60kg
        category = new CategoryModel()
        {
            Id = 22,
            AgeRangeId = 2,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 60,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 84,
            FirstName = "Adem",
            LastName = "Es Sebbani",
            Club = "Takedown - Cluses MMA",
            BirthDate = new DateTime(2009, 8, 17),
            Genre = Genre.Men,
            Weight = 60,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 85,
            FirstName = "Wael",
            LastName = "Hamou",
            Club = "Courbevoie Kung Fu",
            BirthDate = new DateTime(2009, 7, 13),
            Genre = Genre.Men,
            Weight = 59,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 22, CompetitorId = 84 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 22, CompetitorId = 85 });

        draw = new DrawModel() { Id = 22, CategoryId = 22 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 74, Order = 1, Competitor1Id = 84, Competitor2Id = 85, DrawId = 22, GlobalOrder = 57 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Masculin / Juniors / -65kg
        category = new CategoryModel()
        {
            Id = 23,
            AgeRangeId = 2,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 65,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 86,
            FirstName = "Gianni",
            LastName = "Espero",
            Club = "KungFuFighter",
            BirthDate = new DateTime(2009, 9, 10),
            Genre = Genre.Men,
            Weight = 65,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 87,
            FirstName = "Sandro",
            LastName = "Lechelle",
            Club = "Courbevoie Kung Fu",
            BirthDate = new DateTime(2009, 7, 17),
            Genre = Genre.Men,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 23, CompetitorId = 86 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 23, CompetitorId = 87 });

        draw = new DrawModel() { Id = 23, CategoryId = 23 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 75, Order = 1, Competitor1Id = 86, Competitor2Id = 87, DrawId = 23, GlobalOrder = 58 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Masculin / Juniors / -75kg
        category = new CategoryModel()
        {
            Id = 24,
            AgeRangeId = 2,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 75,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 88,
            FirstName = "Ziad",
            LastName = "Richard",
            Club = "Drähcirz Team KZ",
            BirthDate = new DateTime(2009, 9, 12),
            Genre = Genre.Men,
            Weight = 75,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 89,
            FirstName = "Kylien",
            LastName = "Amer Ouali Ozer",
            Club = "Hu Bei Chuan 60",
            BirthDate = new DateTime(2009, 10, 11),
            Genre = Genre.Men,
            Weight = 74,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 24, CompetitorId = 88 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 24, CompetitorId = 89 });

        draw = new DrawModel() { Id = 24, CategoryId = 24 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 76, Order = 1, Competitor1Id = 88, Competitor2Id = 89, DrawId = 24, GlobalOrder = 59 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Feminin / Juniors / -60kg
        category = new CategoryModel()
        {
            Id = 25,
            AgeRangeId = 2,
            Genre = Genre.Women,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 60,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 90,
            FirstName = "Feryel",
            LastName = "Chehih",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(2009, 7, 10),
            Genre = Genre.Women,
            Weight = 60,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 91,
            FirstName = "Celia",
            LastName = "Kaiser",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(2009, 8, 22),
            Genre = Genre.Women,
            Weight = 59,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 25, CompetitorId = 90 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 25, CompetitorId = 91 });

        draw = new DrawModel() { Id = 25, CategoryId = 25 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 77, Order = 1, Competitor1Id = 90, Competitor2Id = 91, DrawId = 25, GlobalOrder = 60 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Seniors / -100kg
        category = new CategoryModel()
        {
            Id = 26,
            AgeRangeId = 3,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 100,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 92,
            FirstName = "Dosan",
            LastName = "Vignolo",
            Club = "Team KCM 83",
            BirthDate = new DateTime(2007, 7, 10),
            Genre = Genre.Men,
            Weight = 100,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 93,
            FirstName = "Peter",
            LastName = "N'doye",
            Club = "Association Tigre du Sud",
            BirthDate = new DateTime(2007, 8, 11),
            Genre = Genre.Men,
            Weight = 95,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 26, CompetitorId = 92 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 26, CompetitorId = 93 });

        draw = new DrawModel() { Id = 26, CategoryId = 26 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 78, Order = 1, Competitor1Id = 92, Competitor2Id = 93, DrawId = 26, GlobalOrder = 63 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Feminin / Seniors / -56kg
        category = new CategoryModel()
        {
            Id = 27,
            AgeRangeId = 3,
            Genre = Genre.Women,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 56,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 94,
            FirstName = "Melissa",
            LastName = "Bouamrane",
            Club = "Kung Fu Vaulx-en-Velin",
            BirthDate = new DateTime(2007, 7, 7),
            Genre = Genre.Women,
            Weight = 56,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 95,
            FirstName = "Nabia",
            LastName = "Kherbach",
            Club = "S'Fight Academy",
            BirthDate = new DateTime(2007, 9, 21),
            Genre = Genre.Women,
            Weight = 55,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 27, CompetitorId = 94 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 27, CompetitorId = 95 });

        draw = new DrawModel() { Id = 27, CategoryId = 27 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 79, Order = 1, Competitor1Id = 94, Competitor2Id = 95, DrawId = 27, GlobalOrder = 64 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda / Feminin / Seniors / -65kg
        category = new CategoryModel()
        {
            Id = 28,
            AgeRangeId = 3,
            Genre = Genre.Women,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.Sanda,
            WeightMax = 65,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 96,
            FirstName = "Kassandra",
            LastName = "Pezzillo",
            Club = "AVG KS-Team",
            BirthDate = new DateTime(2007, 8, 1),
            Genre = Genre.Women,
            Weight = 65,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 97,
            FirstName = "Emmanuelle",
            LastName = "Moisan",
            Club = "Ecole Kim Dieu",
            BirthDate = new DateTime(2007, 9, 2),
            Genre = Genre.Women,
            Weight = 64,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 28, CompetitorId = 96 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 28, CompetitorId = 97 });

        draw = new DrawModel() { Id = 28, CategoryId = 28 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 80, Order = 1, Competitor1Id = 96, Competitor2Id = 97, DrawId = 28, GlobalOrder = 65 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Veterans / -80kg
        category = new CategoryModel()
        {
            Id = 29,
            AgeRangeId = 5,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMax = 80,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 98,
            FirstName = "Sébastien",
            LastName = "Henrion",
            Club = "Trident des 9 Dragons",
            BirthDate = new DateTime(1990, 8, 1),
            Genre = Genre.Men,
            Weight = 80,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 99,
            FirstName = "Antoine",
            LastName = "Beucler",
            Club = "Kung Fu / Jeet Kune Do To",
            BirthDate = new DateTime(1990, 9, 2),
            Genre = Genre.Men,
            Weight = 79,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 29, CompetitorId = 98 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 29, CompetitorId = 99 });

        draw = new DrawModel() { Id = 29, CategoryId = 29 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 81, Order = 1, Competitor1Id = 98, Competitor2Id = 99, DrawId = 29, GlobalOrder = 74 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion

        #region Sanda Light / Masculin / Veterans / +100kg
        category = new CategoryModel()
        {
            Id = 30,
            AgeRangeId = 5,
            Genre = Genre.Men,
            RoundType = RoundType.Knockouts,
            Type = CategoryType.SandaLight,
            WeightMin = 100,
            CompetitionId = 1
        };
        await _categories.AddAsync(category);


        competitor = new CompetitorModel()
        {
            Id = 100,
            FirstName = "Rilcy",
            LastName = "Miguel",
            Club = "Kung Arts",
            BirthDate = new DateTime(1990, 8, 1),
            Genre = Genre.Men,
            Weight = 105,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        competitor = new CompetitorModel()
        {
            Id = 101,
            FirstName = "Lyes",
            LastName = "Amer Ouali",
            Club = "Hu Bei Chan",
            BirthDate = new DateTime(1990, 9, 2),
            Genre = Genre.Men,
            Weight = 112,
            CountryIsoCode = "FR"
        };
        await _competitors.AddAsync(competitor);

        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 30, CompetitorId = 100 });
        await _competitorsCategories.AddAsync(new CompetitorCategoryModel() { CategoryId = 30, CompetitorId = 101 });

        draw = new DrawModel() { Id = 30, CategoryId = 30 };
        await _draws.AddAsync(draw);

        drawKnockout = new DrawKnockoutModel() { Id = 82, Order = 1, Competitor1Id = 100, Competitor2Id = 101, DrawId = 30, GlobalOrder = 75 };
        await _drawKnockouts.AddAsync(drawKnockout);
        #endregion
    }
    #endregion
}
