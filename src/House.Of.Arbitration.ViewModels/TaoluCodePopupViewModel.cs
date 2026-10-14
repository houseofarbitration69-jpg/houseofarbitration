#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Globalization;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class TaoluCodePopupViewModel : BaseViewModel, IQueryAttributable
{
    #region Attributes
    private int _id;
    private string _code = string.Empty;
    private string _groupName = string.Empty;
    private string _movement = string.Empty;
    private string _description = string.Empty;
    private string _valueText = "0.1";
    private string _selectedGroup = string.Empty;
    private bool _isEditMode;
    private string _title = string.Empty;
    #endregion

    #region Properties
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Code
    {
        get => _code;
        set
        {
            SetProperty(ref _code, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public string GroupName
    {
        get => _groupName;
        set
        {
            SetProperty(ref _groupName, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public string Movement
    {
        get => _movement;
        set
        {
            SetProperty(ref _movement, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            SetProperty(ref _description, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public string ValueText
    {
        get => _valueText;
        set
        {
            SetProperty(ref _valueText, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public string SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            SetProperty(ref _selectedGroup, value);
            if (!string.IsNullOrWhiteSpace(value) && value != "Autre...")
            {
                GroupName = value;
            }
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public ObservableCollection<string> AvailableGroups { get; } = new();
    #endregion

    #region Constructors
    public TaoluCodePopupViewModel(
        ILogger<TaoluCodePopupViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService
    ) : base(logger, resourceProvider, popupService)
    {
        foreach (var grp in MvtCodeModel.StandardGroups)
        {
            AvailableGroups.Add(grp);
        }
        AvailableGroups.Add("Autre...");

        SelectedGroup = AvailableGroups.FirstOrDefault() ?? string.Empty;
        GroupName = SelectedGroup;
        Title = Resources.ADD_CODE;
    }
    #endregion

    #region Query Attributes
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(MvtCodeModel), out var codeObj) && codeObj is MvtCodeModel model)
        {
            Id = model.Id;
            Code = model.Code;
            Movement = model.Movement;
            Description = model.Description;
            ValueText = model.Value.ToString("0.0#", CultureInfo.InvariantCulture);
            GroupName = model.GroupLabel;

            if (AvailableGroups.Contains(model.GroupLabel))
            {
                SelectedGroup = model.GroupLabel;
            }
            else
            {
                SelectedGroup = "Autre...";
            }

            IsEditMode = model.Id > 0;
            Title = IsEditMode ? Resources.EDIT_CODE : Resources.ADD_CODE;
        }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Close()
    {
        await _popupService.ClosePopupAsync(Shell.Current);
    }

    [RelayCommand(CanExecute = nameof(CanValidate))]
    private async Task Validate()
    {
        var result = GetResult();
        await _popupService.ClosePopupAsync<MvtCodeModel>(Shell.Current, result);
    }

    private bool CanValidate()
    {
        if (string.IsNullOrWhiteSpace(Code)) return false;
        if (string.IsNullOrWhiteSpace(Movement)) return false;
        if (string.IsNullOrWhiteSpace(GroupName)) return false;
        if (string.IsNullOrWhiteSpace(Description)) return false;

        if (!TryParseValue(out var parsed) || parsed <= 0) return false;

        return true;
    }

    private bool TryParseValue(out double result)
    {
        var cleaned = ValueText?.Trim().Replace(',', '.') ?? string.Empty;
        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private MvtCodeModel GetResult()
    {
        TryParseValue(out var parsedValue);

        return new MvtCodeModel
        {
            Id = Id,
            Code = Code?.Trim() ?? string.Empty,
            GroupName = GroupName?.Trim() ?? string.Empty,
            Movement = Movement?.Trim() ?? string.Empty,
            Description = Description?.Trim() ?? string.Empty,
            Value = Math.Round(parsedValue, 2),
            Category = "Taolu"
        };
    }
    #endregion
}
