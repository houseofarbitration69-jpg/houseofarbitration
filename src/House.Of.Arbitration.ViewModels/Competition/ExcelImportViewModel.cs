#region Imports
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels.Competition;

/// <summary>
/// ViewModel gérant l'import/export de données de compétition via fichier Excel
/// </summary>
public partial class ExcelImportViewModel : BaseViewModel
{
    #region Services
    private readonly IExcelImportExportService _excelService;
    private readonly IFileDownloadService _fileDownloadService;
    #endregion

    #region Attributes
    private bool _isImporting;
    private bool _isExporting;
    private string _statusMessage = string.Empty;
    private bool _hasResult;
    private bool _importSuccess;
    private List<string> _errorMessages = new();
    private List<string> _warningMessages = new();
    private int _importedCategories;
    private int _importedCompetitors;
    private int _importedDraws;
    #endregion

    #region Properties
    /// <summary>Indique si un import est en cours</summary>
    public bool IsImporting
    {
        get => _isImporting;
        set => SetProperty(ref _isImporting, value);
    }

    /// <summary>Indique si un export est en cours</summary>
    public bool IsExporting
    {
        get => _isExporting;
        set => SetProperty(ref _isExporting, value);
    }

    /// <summary>Message de statut de l'opération en cours</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Indique si un résultat d'import est disponible</summary>
    public bool HasResult
    {
        get => _hasResult;
        set => SetProperty(ref _hasResult, value);
    }

    /// <summary>Indique si l'import s'est déroulé avec succès</summary>
    public bool ImportSuccess
    {
        get => _importSuccess;
        set => SetProperty(ref _importSuccess, value);
    }

    /// <summary>Liste des messages d'erreur suite à l'import</summary>
    public List<string> ErrorMessages
    {
        get => _errorMessages;
        set => SetProperty(ref _errorMessages, value);
    }

    /// <summary>Liste des avertissements suite à l'import</summary>
    public List<string> WarningMessages
    {
        get => _warningMessages;
        set => SetProperty(ref _warningMessages, value);
    }

    /// <summary>Nombre de catégories importées avec succès</summary>
    public int ImportedCategories
    {
        get => _importedCategories;
        set => SetProperty(ref _importedCategories, value);
    }

    /// <summary>Nombre de compétiteurs importés avec succès</summary>
    public int ImportedCompetitors
    {
        get => _importedCompetitors;
        set => SetProperty(ref _importedCompetitors, value);
    }

    /// <summary>Nombre de tirages générés avec succès</summary>
    public int ImportedDraws
    {
        get => _importedDraws;
        set => SetProperty(ref _importedDraws, value);
    }
    #endregion

    #region Constructors
    public ExcelImportViewModel(
        ILogger<ExcelImportViewModel> logger,
        House.Of.Arbitration.Localization.ResourceProvider resourceProvider,
        CommunityToolkit.Maui.IPopupService popupService,
        IExcelImportExportService excelService,
        IFileDownloadService fileDownloadService)
        : base(logger, resourceProvider, popupService)
    {
        _excelService = excelService;
        _fileDownloadService = fileDownloadService;
    }
    #endregion

    #region Commands
    /// <summary>
    /// Génère et télécharge le template Excel directement dans le dossier Téléchargements
    /// </summary>
    [RelayCommand]
    private async Task ExportTemplate()
    {
        IsExporting = true;
        StatusMessage = string.Empty;
        try
        {
            var stream = await _excelService.ExportTemplateAsync();
            var fileName = $"template_competition_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            const string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            var savedPath = await _fileDownloadService.SaveToDownloadsAsync(stream, fileName, mimeType);

            if (savedPath != null)
            {
                StatusMessage = $"✅ Template enregistré dans vos Téléchargements : {fileName}";
            }
            else
            {
                // Fallback : partage si l'enregistrement direct a échoué
                stream.Position = 0;
                var fallbackPath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
                using (var fs = File.Create(fallbackPath))
                    await stream.CopyToAsync(fs);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Template Compétition Excel",
                    File = new ShareFile(fallbackPath)
                });
                StatusMessage = "Template partagé avec succès.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur lors de l'export : {ex.Message}";
            _logger.LogError(ex, "Erreur export template Excel");
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>
    /// Ouvre le sélecteur de fichier, importe le fichier Excel et affiche les résultats
    /// </summary>
    [RelayCommand]
    private async Task Import()
    {
        IsImporting = true;
        HasResult = false;
        StatusMessage = string.Empty;
        ErrorMessages = new List<string>();
        WarningMessages = new List<string>();

        try
        {
            // Ouvrir le sélecteur de fichier
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Sélectionner le fichier Excel de la compétition",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".xlsx" } },
                    { DevicePlatform.Android, new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" } },
                    { DevicePlatform.iOS, new[] { "com.microsoft.excel.xlsx" } },
                })
            });

            if (fileResult == null)
            {
                StatusMessage = "Aucun fichier sélectionné.";
                return;
            }

            using var stream = await fileResult.OpenReadAsync();
            var result = await _excelService.ImportAsync(stream);

            HasResult = true;
            ImportSuccess = result.Success;
            ImportedCategories = result.ImportedCategories;
            ImportedCompetitors = result.ImportedCompetitors;
            ImportedDraws = result.ImportedDraws;
            ErrorMessages = result.Errors;
            WarningMessages = result.Warnings;

            if (result.Success)
            {
                StatusMessage = $"Import réussi ! {result.ImportedCategories} catégorie(s), {result.ImportedCompetitors} compétiteur(s) et {result.ImportedDraws} tirage(s) générés.";
            }
            else
            {
                StatusMessage = $"Import terminé avec {result.Errors.Count} erreur(s). Vérifiez les détails ci-dessous.";
            }
        }
        catch (Exception ex)
        {
            HasResult = true;
            ImportSuccess = false;
            StatusMessage = $"Erreur lors de l'import : {ex.Message}";
            ErrorMessages = new List<string> { ex.Message };
            _logger.LogError(ex, "Erreur import Excel");
        }
        finally
        {
            IsImporting = false;
        }
    }
    #endregion
}
