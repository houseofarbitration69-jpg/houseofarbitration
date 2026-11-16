namespace House.Of.Arbitration.Services.Abstractions;

/// <summary>
/// Interface pour le service d'alertes à l'utilisateur.
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    Task ShowToast(string message);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="title"></param>
    /// <param name="message"></param>
    /// <param name="cancel"></param>
    /// <returns></returns>
    Task ShowAlert(string title, string message, string cancel = "OK");

    /// <summary>
    /// Affiche une boîte de dialogue de confirmation modale avec deux options.
    /// </summary>
    /// <param name="title">Titre de la boîte de dialogue.</param>
    /// <param name="message">Message d'avertissement ou de confirmation.</param>
    /// <param name="accept">Libellé du bouton de confirmation.</param>
    /// <param name="cancel">Libellé du bouton d'annulation.</param>
    /// <returns>True si l'utilisateur a confirmé, False s'il a annulé.</returns>
    Task<bool> ShowConfirmation(string title, string message, string accept = "Oui", string cancel = "Non");
}
