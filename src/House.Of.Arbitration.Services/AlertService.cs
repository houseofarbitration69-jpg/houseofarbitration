#region Imports
using CommunityToolkit.Maui.Alerts;
using House.Of.Arbitration.Services.Abstractions;
using Microsoft.Maui.ApplicationModel;
#endregion

namespace House.Of.Arbitration.Services;

public class AlertService : IAlertService
{
    #region Implement IAlertService
    /// <summary>
    /// Affiche une notification éphémère (Toast) en bas de l'écran.
    /// </summary>
    /// <param name="message">Le message à afficher.</param>
    /// <returns>Une tâche représentant l'opération asynchrone.</returns>
    public Task ShowToast(string message)
    {
        return MainThread.InvokeOnMainThreadAsync(() => Toast.Make(message,CommunityToolkit.Maui.Core.ToastDuration.Long).Show());
    }

    /// <summary>
    /// Affiche une boîte de dialogue d'alerte modale.
    /// </summary>
    /// <param name="title">Le titre de l'alerte.</param>
    /// <param name="message">Le corps du message.</param>
    /// <param name="cancel">Le texte du bouton d'annulation (par défaut "OK").</param>
    /// <returns>Une tâche représentant l'opération asynchrone.</returns>
    public Task ShowAlert(string title, string message, string cancel = "OK")
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page != null)
            {
                await Application.Current.Windows[0].Page!.DisplayAlertAsync(title, message, cancel);
            }
        });
    }

    /// <summary>
    /// Affiche une boîte de dialogue de confirmation modale avec deux options.
    /// </summary>
    /// <param name="title">Titre de la boîte de dialogue.</param>
    /// <param name="message">Message d'avertissement ou de confirmation.</param>
    /// <param name="accept">Libellé du bouton de confirmation.</param>
    /// <param name="cancel">Libellé du bouton d'annulation.</param>
    /// <returns>True si l'utilisateur a confirmé, False s'il a annulé.</returns>
    public Task<bool> ShowConfirmation(string title, string message, string accept = "Oui", string cancel = "Non")
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page != null)
            {
                return await Application.Current.Windows[0].Page!.DisplayAlertAsync(title, message, accept, cancel);
            }
            if (Shell.Current != null)
            {
                return await Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
            }
            return false;
        });
    }
    #endregion
}
