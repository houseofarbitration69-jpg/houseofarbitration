using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace House.Of.Arbitration.Services;

public class WarningService : IWarningService
{
    private readonly IRepository<WarningModel> _warningRepository;
    private readonly IRepository<CompetitorCategoryModel> _competitorCategoryRepository;
    private readonly IRepository<CategoryModel> _categoryRepository;
    private readonly IRepository<CompetitorModel> _competitorRepository;
    private readonly ILogger<WarningService> _logger;

    public WarningService(
        IRepository<WarningModel> warningRepository,
        IRepository<CompetitorCategoryModel> competitorCategoryRepository,
        IRepository<CategoryModel> categoryRepository,
        IRepository<CompetitorModel> competitorRepository,
        ILogger<WarningService> logger)
    {
        _warningRepository = warningRepository;
        _competitorCategoryRepository = competitorCategoryRepository;
        _categoryRepository = categoryRepository;
        _competitorRepository = competitorRepository;
        _logger = logger;
    }

    public async Task UpdateWarningsForCategoryAsync(int categoryId)
    {
        try
        {
            _warningRepository.ClearTracker();
            
            // 1. Get all registration links for this category
            var links = await _competitorCategoryRepository.GetAllAsync("Competitor", "Category.AgeRange");
            var categoryLinks = links?.Where(l => l.CategoryId == categoryId).ToList();

            if (categoryLinks == null) return;

            foreach (var link in categoryLinks)
            {
                await ProcessLinkWarningsAsync(link);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating warnings for category {categoryId}");
        }
    }

    public async Task UpdateWarningsForCompetitorAsync(int competitorId)
    {
        try
        {
            _warningRepository.ClearTracker();

            // 1. Get all registration links for this competitor
            var links = await _competitorCategoryRepository.GetAllAsync("Competitor", "Category.AgeRange");
            var competitorLinks = links?.Where(l => l.CompetitorId == competitorId).ToList();

            if (competitorLinks == null) return;

            foreach (var link in competitorLinks)
            {
                await ProcessLinkWarningsAsync(link);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating warnings for competitor {competitorId}");
        }
    }

    private async Task ProcessLinkWarningsAsync(CompetitorCategoryModel link)
    {
        // 1. Delete existing warnings for this specific registration link
        var allWarnings = await _warningRepository.GetAllAsync();
        var existingWarnings = allWarnings?.Where(w => 
            w.CompetitorId == link.CompetitorId && 
            w.CategoryId == link.CategoryId).ToList();

        if (existingWarnings != null)
        {
            foreach (var w in existingWarnings)
            {
                await _warningRepository.DeleteAsync(w);
            }
        }

        // 2. Calculate new warnings
        var warnings = CalculateWarnings(link.Competitor, link.Category);

        // 3. Persist new warnings
        foreach (var warning in warnings)
        {
            // Set CompetitorCategoryId as shadow property since it's used in AppDbContext
            // We use a dictionary for AddAsync if the repo supports it, 
            // but here we just rely on EF to handle the link if we can or use the repository directly
            // Actually WarningModel has CategoryId and CompetitorId properties too.
            
            warning.CategoryId = link.CategoryId;
            warning.CompetitorId = link.CompetitorId;
            warning.CompetitorCategoryId = link.Id;
            
            await _warningRepository.AddAsync(warning);
        }
    }

    private List<WarningModel> CalculateWarnings(CompetitorModel competitor, CategoryModel category)
    {
        var warnings = new List<WarningModel>();

        // Genre Warning
        if (category.Genre != Genre.Mixte && competitor.Genre != category.Genre)
        {
            warnings.Add(new WarningModel 
            { 
                Label = "Le genre ne correspond pas à la catégorie",
                CompetitorId = competitor.Id,
                CategoryId = category.Id
            });
        }

        // Age Warning
        if (category.AgeRange != null)
        {
            int age = CalculateAge(competitor.BirthDate);
            if (age < category.AgeRange.MinAge || age > category.AgeRange.MaxAge)
            {
                warnings.Add(new WarningModel 
                { 
                    Label = $"L'âge ({age} ans) n'est pas dans la tranche {category.AgeRange.Label} ({category.AgeRange.MinAge}-{category.AgeRange.MaxAge} ans)",
                    CompetitorId = competitor.Id,
                    CategoryId = category.Id
                });
            }
        }

        // Weight Warning (only for Sanda style categories)
        if (category.Type == CategoryType.Sanda || category.Type == CategoryType.SandaLight)
        {
            if (competitor.Weight < category.WeightMin || competitor.Weight > category.WeightMax)
            {
                warnings.Add(new WarningModel 
                { 
                    Label = $"Le poids ({competitor.Weight}kg) est hors limites ({category.WeightMin}-{category.WeightMax}kg)",
                    CompetitorId = competitor.Id,
                    CategoryId = category.Id
                });
            }
        }

        return warnings;
    }

    private int CalculateAge(DateTime birthDate)
    {
        DateTime today = DateTime.Today;
        int age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }
}
