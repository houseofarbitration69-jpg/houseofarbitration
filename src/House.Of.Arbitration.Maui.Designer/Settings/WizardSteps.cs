using CommunityToolkit.Mvvm.ComponentModel;

namespace House.Of.Arbitration.Maui.Designer.Settings;

public class CompetitionStepViewModel : WizardStepViewModel
{
    private string _name = String.Empty;
    private DateTime _date = DateTime.Now;
    private string _place = String.Empty;

    public override string Title => "Create Competition";

    public string Name
    {
        get => _name;
        set
        {
            SetProperty(ref _name, value);
            IsValid = !String.IsNullOrEmpty(Name) && !String.IsNullOrEmpty(Place);
        }
    }

    public DateTime Date
    {
        get => _date;
        set
        {
            SetProperty(ref _date, value);
            IsValid = !String.IsNullOrEmpty(Name) && !String.IsNullOrEmpty(Place);
        }
    }

    public string Place
    {
        get => _place;
        set
        {
            SetProperty(ref _place, value);
            IsValid = !String.IsNullOrEmpty(Name) && !String.IsNullOrEmpty(Place);
        }
    }
}

public class CategoriesStepViewModel : WizardStepViewModel
{
    public override string Title => "Catégories";
}

public class CompetitorsStepViewModel : WizardStepViewModel
{
    public override string Title => "Competitors";
}

public class CategoryViewModel
{
}

public class CompetitorViewModel
{
}

public class UserStepViewModel : WizardStepViewModel
{
    private string _name = String.Empty;
    public string Name
    {
        get => _name;
        set 
        { 
            _name = value; 
            OnPropertyChanged(); 
            IsValid = !string.IsNullOrWhiteSpace(_name) && _name.Length >= 3; 
        }
    }

    public override string Title => "User Info";
}

public class TermsStepViewModel : WizardStepViewModel
{
    private bool _isAccepted;
    public bool IsAccepted
    {
        get => _isAccepted;
        set 
        { 
            _isAccepted = value; 
            OnPropertyChanged(); 
            IsValid = _isAccepted; 
        }
    }

    public override string Title => "Terms & Conditions";
}

public class SummaryStepViewModel : WizardStepViewModel
{
    private readonly UserStepViewModel _userStep;
    private readonly TermsStepViewModel _termsStep;

    public string UserName => _userStep.Name;
    public string TermsAccepted => _termsStep.IsAccepted ? "Accepted" : "Not Accepted";

    public SummaryStepViewModel(UserStepViewModel userStep, TermsStepViewModel termsStep)
    {
        _userStep = userStep;
        _termsStep = termsStep;
        IsValid = true; // Always valid to finish
    }

    public override string Title => "Summary";
    
    // Call this to refresh values when showing the summary
    public void Refresh()
    {
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(TermsAccepted));
    }
}