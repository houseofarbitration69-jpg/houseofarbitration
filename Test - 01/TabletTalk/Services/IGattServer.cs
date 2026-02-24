namespace TabletTalk.Services
{
    /// <summary>
    /// Définit les fonctionnalités d'un serveur GATT multiplateforme.
    /// </summary>
    public interface IGattServer
    {
        /// <summary>
        /// Déclenché lorsqu'un client écrit des données dans notre caractéristique.
        /// </summary>
        event EventHandler<byte[]> MessageReceived;

        /// <summary>
        /// Déclenché pour rapporter des changements de statut.
        /// </summary>
        event EventHandler<string> StatusChanged;

        /// <summary>
        /// Démarre le serveur, publie le service et commence la publicité.
        /// </summary>
        /// <param name="serviceUuid">L'UUID du service à publier.</param>
        /// <param name="characteristicUuid">L'UUID de la caractéristique pour la communication.</param>
        Task Start(Guid serviceUuid, Guid characteristicUuid);

        /// <summary>
        /// Arrête le serveur et la publicité.
        /// </summary>
        Task Stop();

        /// <summary>
        /// Envoie un message à tous les clients abonnés.
        /// </summary>
        /// <param name="message">Le message à envoyer.</param>
        Task SendMessageToSubscribers(byte[] message);
    }
}
