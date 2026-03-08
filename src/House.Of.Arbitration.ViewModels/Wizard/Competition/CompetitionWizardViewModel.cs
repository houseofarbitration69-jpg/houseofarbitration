using House.Of.Arbitration.Models;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using Microsoft.Extensions.Logging;

namespace House.Of.Arbitration.ViewModels.Wizard.Competition;

public class CompetitionWizardViewModel : WizardViewModel<CompetitionModel>
{
    public CompetitionWizardViewModel(
        ILogger<CompetitionWizardViewModel> logger, 
        ResourceProvider resourceProvider, 
        IRepository<CompetitionModel> repository) 
        : base(logger, resourceProvider, repository)
    {
    }

    public override async Task OnAppearing()
    {
        if (CompetitionId > 0)
        {
            var competition = await _repository.GetByIdAsync(CompetitionId, "Categories.Competitors");
            //var competition = (await _repository.GetAllAsync(c => c.Categories, c => c.Categories.Select(cat => cat.Competitors)))?.FirstOrDefault(c => c.Id == CompetitionId);
            if (competition != null)
            {
                Model = competition;

                // Propagation explicite au cas où le setter ne suffise pas
                foreach (var step in Steps)
                {
                    step.Model = Model;
                }
            }
        }

        await base.OnAppearing();
    }

}
