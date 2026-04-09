#region Imports
using CommunityToolkit.Maui.Alerts;
using House.Of.Arbitration.Services.Abstractions;
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
        return Toast.Make(message).Show();
    }

    /// <summary>
    /// Affiche une boîte de dialogue d'alerte modale.
    /// </summary>
    /// <param name="title">Le titre de l'alerte.</param>
    /// <param name="message">Le corps du message.</param>
    /// <param name="cancel">Le texte du bouton d'annulation (par défaut "OK").</param>
    /// <returns>Une tâche représentant l'opération asynchrone.</returns>
    public async Task ShowAlert(string title, string message, string cancel = "OK")
    {
        if (Application.Current != null && Application.Current.Windows != null && Application.Current.Windows.Count > 0 && Application.Current!.Windows[0].Page != null)
        {
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync(title, message, cancel);
        }
    }
    #endregion
}
