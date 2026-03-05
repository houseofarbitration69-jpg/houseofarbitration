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
}
