namespace House.Of.Arbitration.Services.Abstractions;

public interface IWarningService
{
    /// <summary>
    /// Recalculates and updates warnings for all competitors in a specific category.
    /// </summary>
    Task UpdateWarningsForCategoryAsync(int categoryId);

    /// <summary>
    /// Recalculates and updates warnings for a specific competitor across all their categories.
    /// </summary>
    Task UpdateWarningsForCompetitorAsync(int competitorId);
}
