# House of Arbitration — Manuel Utilisateur

Bienvenue dans le guide officiel de l'application **House of Arbitration**, suite logicielle professionnelle dédiée à la gestion, au chronométrage et à l'arbitrage électronique des compétitions d'arts martiaux chinois (**Kung-Fu Wushu**, **Sanda** et **Taolu**).

---

## Sommaire

1. [Introduction et Architecture du Système](#1-introduction-et-architecture-du-système)
2. [Installation et Prérequis](#2-installation-et-prérequis)
3. [Prise en Main et Écran d'Accueil](#3-prise-en-main-et-écran-daccueil)
4. [Espace Master : Gestion des Compétitions](#4-espace-master--gestion-des-compétitions)
   - [4.1 Création d'une compétition (Assistant)](#41-création-dune-compétition-assistant)
   - [4.2 Gestion des catégories et compétiteurs](#42-gestion-des-catégories-et-compétiteurs)
   - [4.3 Gestion des tirages au sort et tableaux](#43-gestion-des-tirages-au-sort-et-tableaux)
5. [Espace Serveur : Table de Marque et Chronométrage](#5-espace-serveur--table-de-marque-et-chronométrage)
   - [5.1 Démarrage du serveur Bluetooth (BLE)](#51-démarrage-du-serveur-bluetooth-ble)
   - [5.2 Suivi des juges connectés](#52-suivi-des-juges-connectés)
   - [5.3 Arbitrage d'un match Sanda](#53-arbitrage-dun-match-sanda)
   - [5.4 Arbitrage d'un passage Taolu](#54-arbitrage-dun-passage-taolu)
   - [5.5 Arrêt du serveur](#55-arrêt-du-serveur)
6. [Espace Juge : Saisie des Notes et Décisions](#6-espace-juge--saisie-des-notes-et-décisions)
   - [6.1 Connexion au serveur de table](#61-connexion-au-serveur-de-table)
   - [6.2 Choix du poste de juge](#62-choix-du-poste-de-juge)
   - [6.3 Notation Sanda](#63-notation-sanda)
   - [6.4 Notation Taolu](#64-notation-taolu)
   - [6.5 Mode hors-ligne et synchronisation](#65-mode-hors-ligne-et-synchronisation)
7. [Consultation des Résultats et Podiums](#7-consultation-des-résultats-et-podiums)
8. [Historique et Journal des Notations](#8-historique-et-journal-des-notations)
9. [Gestion des Codes de Déduction Taolu](#9-gestion-des-codes-de-déduction-taolu)
10. [Outils en Ligne de Commande (CLI Windows)](#10-outils-en-ligne-de-commande-cli-windows)
11. [Dépannage et Bonnes Pratiques](#11-dépannage-et-bonnes-pratiques)

---

## 1. Introduction et Architecture du Système

**House of Arbitration** repose sur une architecture sans fil décentralisée utilisant le protocole **Bluetooth Low Energy (BLE)**. Aucune connexion Internet ni routeur Wi-Fi n'est requis sur le plateau de compétition.

```
       ┌───────────────────────────────────────────────┐
       │             TABLE CENTRALE (MASTER)           │
       │     MAUI Android / Windows ou PC Serveur      │
       │        - Gestion de la compétition            │
       │        - Chronomètre & Affichage public       │
       │        - Centralisation des scores            │
       └───────────────────────┬───────────────────────┘
                               │ (Liaison Bluetooth BLE)
      ┌────────────────────────┼────────────────────────┐
      ▼                        ▼                        ▼
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│  TABLETTE 1  │         │  TABLETTE 2  │         │  TABLETTE N  │
│ Juge 1 (Coin)│         │ Juge 2 (Coin)│         │ Juge Groupe A│
└──────────────┘         └──────────────┘         └──────────────┘
```

### Disciplines prises en charge
* **Sanda** : Combat pieds-poings-projections sur leitai (tournoi par élimination directe ou poules). Arbitrage par 3 à 5 juges de coin.
* **Sanda Light / Wushu / Tradi** : Variantes adaptées aux différentes catégories d'âge et de poids.
* **Taolu** : Épreuves techniques individuelles (formes traditionnelles et modernes du Nord et du Sud). Arbitrage structuré en 3 collèges :
  - **Groupe A (Qualité du mouvement)** : Déductions sur les fautes techniques (base 5.0 points).
  - **Groupe B (Performance générale)** : Évaluation globale de la vitesse, puissance et coordination (base 3.0 points).
  - **Groupe C (Degré de difficulté)** : Validation des mouvements difficiles et acrobaties (base 2.0 points).

---

## 2. Installation et Prérequis

### Appareils Android (Tablettes arbitres et table de marque)
* Android 8.0 (Oreo) ou supérieur.
* Puce Bluetooth 4.2 ou supérieure compatible BLE (Bluetooth Low Energy).
* **Autorisations requises** :
  * Localisation (obligatoire sous Android pour le scan Bluetooth BLE).
  * Appareils à proximité (Bluetooth Scan & Connect sous Android 12+).

### Postes Windows (Table de marque / Arbitres sur PC portables)
* Windows 10 (version 19041+) ou Windows 11 avec Bluetooth 4.0+.
* .NET 10 Runtime (si exécution des binaires natifs ou CLI).

---

## 3. Prise en Main et Écran d'Accueil

Au lancement de l'application, l'écran principal offre 4 accès rapides :

1. **MASTER** : Accès à la gestion des compétitions, pesées, tirages au sort et lancement de la table de marque.
2. **JUGE** : Passage en mode terminal arbitre (détection du serveur et saisie des scores).
3. **HISTORIQUE** : Consultation détaillée de toutes les frappes, fautes et déductions logguées avec horodatage.
4. **CODES TAOLU** : Consultation et modification de la base officielle des codes de déductions martiales.

---

## 4. Espace Master : Gestion des Compétitions

Accessible via le bouton **MASTER** de l'accueil.

### 4.1 Création d'une compétition (Assistant)
Cliquez sur le bouton **"+"** (Ajouter une compétition) pour ouvrir l'assistant étape par étape :
1. **Paramètres généraux** : Nom de la compétition, date, lieu, type principal (Sanda ou Taolu).
2. **Catégories** : Définition des catégories d'âge (Poussins, Minimes, Cadets, Juniors, Seniors, Vétérans), de sexe (Masculin, Féminin, Mixte), et des tranches de poids minimum/maximum.
3. **Compétiteurs** : Saisie des athlètes, club affilié, pesée certifiée et pays (sélection avec drapeau).
4. **Confirmation & Génération** : Validation des inscriptions et création automatique des tableaux de compétition.

### 4.2 Gestion des catégories et compétiteurs
Dans la liste des compétitions, chaque carte permet :
* **DÉTAILS** : Consultation du récapitulatif de l'événement et de la liste des catégories inscrites.
* **ÉDITION** (icône stylet) : Modification des paramètres ou des inscrits.
* **SUPPRESSION** (icône corbeille) : Suppression définitive d'une compétition avec demande de confirmation.

### 4.3 Gestion des tirages au sort et tableaux
Cliquez sur le bouton **TIRAGES** sur la compétition souhaitée :
* **Mode Knockout (Élimination directe)** :
  * Visualisation de l'arbre des duels (1/8, 1/4, 1/2, Finale).
  * Possibilité de réorganiser l'ordre des matchs ou de positionner les têtes de série par glisser-déposer.
* **Mode Pools (Poules)** :
  * Organisation de mini-championnats où chaque compétiteur affronte tous les autres de sa poule.
* **Mode Order (Ordre de passage Taolu)** :
  * Liste ordonnée des passages individuels.
  * Boutons de réordonnancement (Monter / Descendre) pour adapter l'ordre selon l'échauffement des athlètes.

---

## 5. Espace Serveur : Table de Marque et Chronométrage

Pour lancer la table de marque d'une compétition, cliquez sur le bouton **DÉMARRER** (ou bouton rouge) de la compétition. Selon la discipline, l'écran Sanda ou Taolu s'ouvre.

### 5.1 Démarrage du serveur Bluetooth (BLE)
1. Cliquez sur **DÉMARRER LE SERVEUR**.
2. Une boîte de dialogue vous permet de nommer le serveur (par exemple : `Tapis 1 - Sanda`).
3. L'application émet alors son signal BLE. Les tablettes juges peuvent maintenant s'y connecter.

### 5.2 Suivi des juges connectés
En haut de l'écran, les pastilles des juges indiquent en temps réel leur état :
* **Pastille Verte** : Juge connecté et prêt.
* **Pastille Rouge / Grisée** : Juge déconnecté ou hors de portée.
* Dès qu'un juge se connecte ou se déconnecte, l'écran se met à jour instantanément.

### 5.3 Arbitrage d'un match Sanda
L'écran serveur Sanda est divisé en 3 colonnes :
* **Coin Rouge (Gauche)** : Nom, club, pays du combattant rouge, points cumulés des juges, avertissements.
* **Centre (Chronomètre & Rounds)** :
  * Affichage des rounds en haut (`Round 1`, `Round 2`, `Round 3`).
  * Chronomètre central avec contrôles : `Démarrer`, `Pause`, `Réinitialiser`.
  * **Bouton EX-ÆQUO** : Positionné en bas au-dessus du chronomètre. Permet de déclarer une égalité parfaite sur le round en cours et de basculer automatiquement au round suivant.
* **Coin Bleu (Droite)** : Nom, club, pays du combattant bleu, points cumulés des juges, avertissements.
* **Attribution du vainqueur** :
  * Cliquez sur le bouton **VAINQUEUR ROUGE** ou **VAINQUEUR BLEU**.
  * Le résultat du match est enregistré et le compétiteur vainqueur est qualifié pour le tour suivant.

### 5.4 Arbitrage d'un passage Taolu
L'écran serveur Taolu affiche :
* Les informations de l'athlète en cours de passage.
* Le décompte des 3 groupes de juges :
  * **Groupe A** : Déductions courantes (ex: -0.20).
  * **Groupe B** : Note de performance moyenne des juges B.
  * **Groupe C** : Note de difficulté validée.
* **Score Global** : Calculé en direct selon la formule officielle :
  $$\text{Score Final} = \text{Score A} + \text{Score B} + \text{Score C}$$
* À la fin de la routine, cliquez sur **VALIDER LE PASSAGE** pour enregistrer la note définitive et appeler l'athlète suivant.

### 5.5 Arrêt du serveur
Lorsque la session est terminée, cliquez sur **ARRÊTER LE SERVEUR** :
* Le serveur envoie un signal instantané `SERVER_STOPPED` à tous les terminaux arbitres.
* Les juges reçoivent la notification, arrêtent leur écoute et se déconnectent proprement sans tenter de reconnexion intempestive.

---

## 6. Espace Juge : Saisie des Notes et Décisions

Accessible depuis l'écran d'accueil en sélectionnant **JUGE**.

### 6.1 Connexion au serveur de table
1. L'application recherche automatiquement les serveurs de tapis à proximité.
2. La liste affiche le nom du tapis (ex: *Tapis 1 - Sanda*) et la puissance du signal (RSSI).
3. Touchez le serveur de votre tapis pour vous connecter.

### 6.2 Choix du poste de juge
Après la connexion, sélectionnez votre affectation selon les consignes du responsable d'arbitrage :
* **En Sanda** : Touchez `Juge 1`, `Juge 2`, `Juge 3`, `Juge 4` ou `Juge 5`.
* **En Taolu** : Sélectionnez votre groupe (`Groupe A`, `Groupe B` ou `Groupe C`) et votre numéro au sein du groupe.

### 6.3 Notation Sanda
L'écran de notation Sanda présente deux grandes zones tactiles ergonomiques :
* **Bouton Rouge (+1)** : Touchez pour attribuer un point au coin Rouge lors d'une touche nette ou d'une amenée au sol.
* **Bouton Bleu (+1)** : Touchez pour attribuer un point au coin Bleu.
* Le chronomètre officiel de la table s'affiche en temps réel.
* Chaque touche est immédiatement transmise à la table centrale par Bluetooth.

### 6.4 Notation Taolu
L'écran de notation Taolu permet une saisie rapide sans quitter l'athlète des yeux :
* **Recherche et application des codes** :
  * Saisissez le numéro ou les initiales de la faute sur le pavé numérique (ex: `11` pour déséquilibre, `24` pour main au sol).
  * Les libellés officiels s'affichent automatiquement.
  * Validez pour appliquer la pénalité correspondante.
* **Ajustement manuel** : Des boutons rapides permettent d'ajouter ou déduire 0.1, 0.2 ou 0.5 point.

### 6.5 Mode hors-ligne et synchronisation
Si la tablette juge perd momentanément la liaison Bluetooth avec la table de marque :
1. Un bandeau orange/rouge **⚠️ Serveur Hors-Ligne** apparaît en haut de l'écran.
2. **Continuez à arbitrer normalement** : toutes vos décisions sont conservées dans la mémoire locale de la tablette.
3. Dès que le signal est rétabli, l'application synchronise l'ensemble des points en attente avec le serveur de manière transparente.

---

## 7. Consultation des Résultats et Podiums

Sur l'écran des compétitions (**MASTER**), chaque compétition dispose d'un bouton d'action **RÉSULTATS** (icône Trophée 🏆).

Cet écran offre :
* **Un sélecteur de catégorie** : Menu déroulant permettant de passer instantanément d'une catégorie à une autre.
* **Un affichage graphique du Podium 🏆** :
  * 🥇 **Champion (1ère Place)** : Mis en valeur au centre sur fond doré avec score ou titre.
  * 🥈 **Finaliste (2ème Place)** : À gauche sur fond argent.
  * 🥉 **Médaille de Bronze (3ème Place)** : À droite sur fond bronze.
* **Le classement complet et ordonné** :
  * Tableau détaillé listant tous les compétiteurs du premier au dernier.
  * Affichage de la position (`1er`, `2e`, `3e`...), du pays d'origine avec son drapeau, du club affilié, du score final ou bilan victoires/défaites, et du statut de qualification.

---

## 8. Historique et Journal des Notations

Accessible via le bouton **HISTORIQUE** sur la page d'accueil.

* **Audit complet de l'arbitrage** : Permet à la commission d'arbitrage ou aux chefs de tapis de vérifier l'ensemble des points attribués au cours de la journée.
* **Filtres multicritères** :
  * Filtrage par compétition.
  * Filtrage par catégorie.
* **Colonnes du journal** :
  * Horodatage exact (date, heure, seconde).
  * Identifiant de l'arbitre (ex: *Juge 2*, *Juge A_1*).
  * Compétiteur concerné.
  * Round concerné.
  * Valeur du point ou de la déduction.
  * Code technique associé.

---

## 9. Gestion des Codes de Déduction Taolu

Accessible via le bouton **CODES TAOLU** sur la page d'accueil.

* Fournit le répertoire officiel des codes de mouvements et fautes techniques d'après le règlement de la Fédération Internationale de Wushu (IWUF).
* Permet aux arbitres et organisateurs :
  * De rechercher un code par numéro ou par mot-clé (ex: *Équilibre*, *Saut*, *Arme*).
  * De consulter la valeur de pénalité associée.
  * D'ajouter ou modifier des codes spécifiques pour des épreuves régionales ou traditionnelles.

---

## 10. Outils en Ligne de Commande (CLI Windows)

Pour les postes administratifs ou les environnements de test sous Windows, deux outils console autonomes sont inclus :

### 10.1 CLI Serveur (`House.Of.Arbitration.Server.Cli`)
```bash
# Lancement simple avec nom de périphérique personnalisé
House.Of.Arbitration.Server.Cli.exe --auto-start --name "Tapis-A" --mode sanda
```
Commandes interactives disponibles dans la console :
* `start [nom]` : Démarre l'advertising Bluetooth.
* `stop` : Avertit les juges et coupe le serveur.
* `mode <sanda|taolu>` : Bascule la discipline du tapis.
* `timer <start|pause|reset|set mm:ss>` : Contrôle du chronomètre.
* `judges` : Affiche la liste des juges actuellement connectés.
* `match` : Affiche ou modifie le match actif.

### 10.2 CLI Juge (`House.Of.Arbitration.Judge.Cli`)
```bash
# Scan et connexion automatique à un poste
House.Of.Arbitration.Judge.Cli.exe --scan --connect "Tapis-A" --position 1
```
Commandes interactives disponibles :
* `red` / `+r` : Point pour le coin Rouge.
* `blue` / `+b` : Point pour le coin Bleu.
* `score <valeur>` : Définition de la note (Taolu).
* `ping` : Test de latence aller-retour avec le serveur en millisecondes.
* `disconnect` : Déconnexion propre de la session d'arbitrage.

---

## 11. Dépannage et Bonnes Pratiques

| Problème rencontré | Cause probable | Solution recommandée |
| :--- | :--- | :--- |
| **Le serveur n'apparaît pas dans la liste de scan du juge.** | Bluetooth désactivé ou autorisations refusées sur Android. | Activer le Bluetooth et la Localisation (GPS). Vérifier dans les paramètres Android que l'application dispose de l'autorisation *Appareils à proximité*. |
| **Une tablette juge affiche "Serveur Hors-Ligne".** | L'arbitre s'est trop éloigné du plateau ou interférences 2.4 GHz. | Rapprocher la tablette du serveur. La tablette continue de noter en local et synchronisera dès son retour à portée. |
| **Deux tablettes tentent d'occuper le même poste de juge.** | Deux arbitres ont sélectionné par exemple `Juge 1`. | Le serveur réaffecte automatiquement le poste au dernier connecté. Demander au premier juge de sélectionner le poste qui lui a été attribué. |
| **Le chronomètre ne défile pas sur la tablette juge.** | Le serveur n'a pas lancé le combat. | Le chronomètre de l'arbitre est asservi à la table de marque centrale. Dès que la table clique sur *Démarrer*, le chrono s'anime sur toutes les tablettes. |
| **Le bouton Ex-æquo en Sanda.** | Égalité parfaite de votes entre les juges sur le round. | Cliquez sur le bouton central `EX-ÆQUO` pour consigner le match nul du round et ouvrir le round décisif. |

---

*Document rédigé pour la version 1.0 de House of Arbitration — Tous droits réservés.*
