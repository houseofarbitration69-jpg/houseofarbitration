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
}
