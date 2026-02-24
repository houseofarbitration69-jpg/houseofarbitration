# TabletTalk - Solution de Communication pour Tablettes

TabletTalk est une application de démonstration construite avec .NET MAUI qui permet à plusieurs appareils (tablettes, téléphones) de communiquer entre eux via Wi-Fi ou Bluetooth.

## Table des matières
1. [Architecture](#architecture)
2. [Fonctionnement](#fonctionnement)
    - [Mode Wi-Fi](#mode-wi-fi)
    - [Mode Bluetooth](#mode-bluetooth)
3. [Comment utiliser l'application](#comment-utiliser-lapplication)
    - [Prérequis](#prérequis)
    - [Lancement](#lancement)
    - [Scénarios d'utilisation](#scénarios-dutilisation)
4. [Limitations et Points Clés](#limitations-et-points-clés)

---

## Architecture

L'application est basée sur une architecture **MVVM (Model-View-ViewModel)** propre et découplée, facilitée par le `CommunityToolkit.Mvvm`.

- **`Models`**: Contient les objets de données, comme `ChatMessage`.
- **`Views` (`MainPage.xaml`)**: Définit l'interface utilisateur. La vue est "stupide" et ne fait que se lier aux données et commandes du ViewModel.
- **`ViewModels` (`MainViewModel.cs`)**: Contient toute la logique de l'interface utilisateur (propriétés, commandes). Il ne sait pas *comment* les messages sont envoyés, il délègue cette tâche.
- **`Services`**: Le cœur de la logique de communication.
    - **`IConnectivityService.cs`**: Une interface commune qui définit les actions de communication (`Start`, `SendMessage`, etc.). Cela permet au reste de l'application d'être agnostique à la technologie.
    - **`WifiService.cs`**: Implémentation pour la communication Wi-Fi en utilisant des Sockets TCP (`TcpListener`, `TcpClient`).
    - **`BluetoothStarService.cs`**: Implémentation pour la communication Bluetooth LE. Utilise le package `Plugin.BLE`.
    - **`ConnectivityManager.cs`**: C'est le **chef d'orchestre**. Le ViewModel ne parle qu'à cette classe. Le `ConnectivityManager` est responsable de la gestion du service actif (Wi-Fi ou Bluetooth) et de la logique de basculement.

Ce design permet de changer facilement de mode de communication ou même d'en ajouter un nouveau (ex: NFC) sans modifier le reste de l'application, simplement en créant un nouveau service qui implémente `IConnectivityService`.

---

## Fonctionnement

### Mode Wi-Fi

- **Topologie**: Client-Serveur.
- **Fonctionnement**:
    1.  Un appareil est désigné comme **Serveur**. Il écoute les connexions entrantes sur un port spécifique (8888).
    2.  Les autres appareils (**Clients**) se connectent à l'adresse IP du serveur.
    3.  Lorsqu'un client envoie un message, le serveur le reçoit et le **diffuse (broadcast)** à tous les autres clients connectés.
- **Avantages**: Très stable, rapide et supporte un grand nombre d'appareils tant qu'ils sont sur le même réseau Wi-Fi.

### Mode Bluetooth

- **Topologie**: Étoile (Hub-and-Spoke), simulant un réseau maillé.
- **Fonctionnement**:
    1.  Un appareil est désigné comme **Hub** (Périphérique GATT). **ATTENTION**: la publication d'un service GATT n'est pas implémentée de manière multiplateforme dans cette démo et nécessite du code natif. L'application suppose qu'un Hub existe.
    2.  Les autres appareils (**Spokes** ou clients Centraux) scannent et se connectent à ce Hub.
    3.  Quand un Spoke envoie un message, il l'écrit dans une "caractéristique" GATT du Hub.
    4.  Le Hub est notifié de ce changement et relaie le message à tous les autres Spokes connectés via des notifications.
- **Avantages**: Ne nécessite pas de réseau Wi-Fi. Idéal pour des communications directes en extérieur.
- **Limites**: Moins rapide que le Wi-Fi et le nombre de connexions simultanées à un Périphérique est limité (généralement autour de 5-7 appareils, selon le matériel).

---

## Comment utiliser l'application

### Prérequis

- Au moins deux appareils Android (ou un émulateur et un appareil physique).
- Visual Studio 2022 avec la charge de travail .NET MAUI installée.
- Les appareils doivent être sur le même réseau Wi-Fi (pour le mode Wi-Fi).
- Le Bluetooth doit être activé sur les appareils (pour le mode Bluetooth).

### Lancement

1.  Ouvrez la solution `TabletTalk.sln` dans Visual Studio.
2.  Sélectionnez un appareil Android comme cible de déploiement.
3.  Appuyez sur `F5` ou le bouton de démarrage pour compiler et déployer l'application.
4.  Répétez pour le deuxième appareil.

### Scénarios d'utilisation

**Scénario 1 : Chat en Wi-Fi**

1.  **Sur l'appareil 1 (Serveur)**:
    - Appuyez sur `Mode: WiFi`.
    - Appuyez sur `Démarrer comme Serveur / Hub`. L'adresse IP locale s'affichera dans le statut.
2.  **Sur l'appareil 2 (Client)**:
    - Appuyez sur `Mode: WiFi`.
    - Dans la section "Connexion Manuelle", entrez l'adresse IP de l'appareil 1.
    - Appuyez sur `Se connecter au Peer`.
3.  Vous pouvez maintenant envoyer des messages de l'un à l'autre. Connectez d'autres appareils de la même manière.

**Scénario 2 : Chat en Bluetooth (Conceptuel)**

1.  **Sur l'appareil 1 (Hub)**:
    - Il faudrait lancer une version de l'application avec le code natif pour publier le service GATT.
    - Dans la démo actuelle, vous pouvez appuyer sur `Mode: Bluetooth` puis `Démarrer comme Serveur / Hub` pour voir le statut conceptuel.
2.  **Sur l'appareil 2 (Client/Spoke)**:
    - Appuyez sur `Mode: Bluetooth`.
    - Appuyez sur `Scanner les Peers`. Le nom du Hub devrait apparaître dans la liste.
    - Entrez le nom du Hub dans le champ de texte et appuyez sur `Se connecter au Peer`.
3.  Une fois la connexion établie, la communication est possible.

---

## Limitations et Points Clés

- **Serveur Bluetooth**: La plus grande limitation est l'absence d'un serveur GATT (Hub) multiplateforme. L'implémentation de cette partie est un défi majeur en développement mobile et nécessite souvent des bibliothèques commerciales ou du code natif spécifique à chaque plateforme (Android/iOS).
- **Découverte de service Wi-Fi**: En mode Wi-Fi, la connexion manuelle par IP est utilisée. Une application de production utiliserait des protocoles de découverte de réseau comme **mDNS/Zeroconf** pour trouver automatiquement le serveur sur le réseau.
- **Permissions**: L'application demandera des permissions pour le Bluetooth et la localisation. Celles-ci sont nécessaires pour que le scan Bluetooth fonctionne.
