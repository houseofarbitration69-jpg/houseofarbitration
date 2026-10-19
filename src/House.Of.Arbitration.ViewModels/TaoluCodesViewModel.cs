#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class TaoluCodesViewModel : BaseViewModel
{
    #region Services
    private readonly IRepository<MvtCodeModel> _mvtCodesRepository;
    private readonly IAlertService _alertService;
    #endregion

    #region Attributes
    private string _searchText = string.Empty;
    private string _selectedGroup = string.Empty;
    #endregion

    #region Properties
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                ApplyFilters();
            }
        }
    }

    public ObservableCollection<MvtCodeModel> Codes { get; } = new();
    public ObservableCollection<MvtCodeModel> FilteredCodes { get; } = new();
    public ObservableCollection<string> Groups { get; } = new();
    #endregion

    #region Constructors
    public TaoluCodesViewModel(
        ILogger<TaoluCodesViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService,
        IAlertService alertService,
        IRepository<MvtCodeModel> mvtCodesRepository
    ) : base(logger, resourceProvider, popupService)
    {
        _alertService = alertService;
        _mvtCodesRepository = mvtCodesRepository;
        _title = Resources.TAOLU_CODES;
    }
    #endregion

    #region Lifecycle
    public override async Task OnAppearing()
    {
        await base.OnAppearing();
        await LoadCodesAsync();
    }
    #endregion

    #region Methods
    public async Task LoadCodesAsync()
    {
        try
        {
            IsBusy = true;
            var list = await _mvtCodesRepository.GetAllAsync();

            // Si la base est vide ou contient les anciens codes de test sans Movement/GroupName
            if (list == null || !list.Any() || list.All(c => string.IsNullOrEmpty(c.Movement) && string.IsNullOrEmpty(c.GroupName)))
            {
                await SeedDefaultCodesInternal();
                list = await _mvtCodesRepository.GetAllAsync();
            }

            Codes.Clear();
            if (list != null)
            {
                foreach (var code in list.OrderBy(c => c.Code))
                {
                    Codes.Add(code);
                }
            }

            UpdateGroupsList();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du chargement des codes Taolu");
            await _alertService.ShowToast("Erreur lors du chargement des codes");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateGroupsList()
    {
        var currentSelection = SelectedGroup;
        Groups.Clear();
        Groups.Add(Resources.ALL_GROUPS);

        var distinctGroups = Codes
            .Select(c => c.GroupLabel)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct()
            .OrderBy(g => g);

        foreach (var grp in distinctGroups)
        {
            Groups.Add(grp);
        }

        if (!string.IsNullOrEmpty(currentSelection) && Groups.Contains(currentSelection))
        {
            SelectedGroup = currentSelection;
        }
        else
        {
            SelectedGroup = Groups.FirstOrDefault() ?? Resources.ALL_GROUPS;
        }
    }

    private void ApplyFilters()
    {
        FilteredCodes.Clear();

        var query = Codes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedGroup) && SelectedGroup != Resources.ALL_GROUPS)
        {
            query = query.Where(c => string.Equals(c.GroupLabel, SelectedGroup, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim().ToLowerInvariant();
            query = query.Where(c =>
                (c.Code != null && c.Code.ToLowerInvariant().Contains(search)) ||
                (c.Movement != null && c.Movement.ToLowerInvariant().Contains(search)) ||
                (c.Description != null && c.Description.ToLowerInvariant().Contains(search)) ||
                (c.GroupLabel != null && c.GroupLabel.ToLowerInvariant().Contains(search)));
        }

        foreach (var item in query.OrderBy(c => c.Code))
        {
            FilteredCodes.Add(item);
        }
    }

    private async Task SeedDefaultCodesInternal()
    {
        var existing = await _mvtCodesRepository.GetAllAsync();
        if (existing != null && existing.Any())
        {
            foreach (var item in existing.ToList())
            {
                await _mvtCodesRepository.DeleteAsync(item);
            }
        }

        foreach (var code in MvtCodeModel.DefaultCodes)
        {
            var clone = new MvtCodeModel
            {
                Code = code.Code,
                GroupName = code.GroupName,
                Movement = code.Movement,
                Description = code.Description,
                Value = code.Value,
                Category = code.Category
            };
            await _mvtCodesRepository.AddAsync(clone);
        }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Close()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task AddCode()
    {
        var newCode = new MvtCodeModel
        {
            GroupName = (SelectedGroup != null && SelectedGroup != Resources.ALL_GROUPS) ? SelectedGroup : "Techniques de main",
            Value = 0.1
        };

        var query = new Dictionary<string, object>
        {
            [nameof(MvtCodeModel)] = newCode
        };

        var result = await _popupService.ShowPopupAsync<TaoluCodePopupViewModel, MvtCodeModel?>(
            Shell.Current, 
            options: new PopupOptions() { Shadow = null },
            shellParameters: query
        );

        if (result != null && result.Result != null)
        {
            var codeToSave = result.Result;

            // Vérifier doublon de code
            var duplicate = Codes.FirstOrDefault(c => string.Equals(c.Code, codeToSave.Code, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
                await _alertService.ShowToast($"Le code '{codeToSave.Code}' existe déjà !");
                return;
            }

            var saved = await _mvtCodesRepository.AddAsync(codeToSave);
            if (saved != null)
            {
                Codes.Add(saved);

                UpdateGroupsList();
                ApplyFilters();
                await _alertService.ShowToast($"Code '{saved.Code}' ajouté");
            }
        }
    }

    [RelayCommand]
    private async Task EditCode(MvtCodeModel code)
    {
        if (code == null) return;

        var query = new Dictionary<string, object>
        {
            [nameof(MvtCodeModel)] = code
        };

        var result = await _popupService.ShowPopupAsync<TaoluCodePopupViewModel, MvtCodeModel?>(
            Shell.Current, 
            options: new PopupOptions() { Shadow = null }, 
            shellParameters: query
        );

        if (result != null && result.Result != null)
        {
            var updated = result.Result;
            code.Code = updated.Code;
            code.GroupName = updated.GroupName;
            code.Movement = updated.Movement;
            code.Description = updated.Description;
            code.Value = updated.Value;

            await _mvtCodesRepository.UpdateAsync(code);
            UpdateGroupsList();
            ApplyFilters();
            await _alertService.ShowToast($"Code '{code.Code}' modifié");
        }
    }

    [RelayCommand]
    private async Task DeleteCode(MvtCodeModel code)
    {
        if (code == null) return;

        var confirm = await DisplayConfirmation(
            Resources.CONFIRM_DELETE,
            $"{Resources.CONFIRM_DELETE_CODE}\n\nCode {code.Code} - {code.Movement}",
            Resources.YES,
            Resources.NO);

        if (!confirm) return;

        await _mvtCodesRepository.DeleteAsync(code);
        Codes.Remove(code);
        UpdateGroupsList();
        ApplyFilters();
        await _alertService.ShowToast($"Code '{code.Code}' supprimé");
    }

    [RelayCommand]
    private async Task ResetDefaultCodes()
    {
        var confirm = await DisplayConfirmation(
            Resources.RESET_DEFAULT_CODES,
            Resources.CONFIRM_RESET_CODES,
            Resources.YES,
            Resources.NO);

        if (!confirm) return;

        try
        {
            IsBusy = true;
            await SeedDefaultCodesInternal();
            await LoadCodesAsync();
            await _alertService.ShowToast("Codes rétablis par défaut");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la réinitialisation des codes");
            await _alertService.ShowToast("Erreur lors de la réinitialisation");
        }
        finally
        {
            IsBusy = false;
        }
    }
    #endregion
}
