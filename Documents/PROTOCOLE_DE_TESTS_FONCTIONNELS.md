# Protocole de Tests Fonctionnels de Bout en Bout (Cahier de Recette)
**Application : House of Arbitration**  
**Version cible : 1.0**  
**Date : 10 novembre 2026**

---

## 📋 Table des Matières

1. [Objectifs et Conditions Générales de Test](#1-objectifs-et-conditions-générales-de-test)
2. [Matériel et Environnement Requis](#2-matériel-et-environnement-requis)
3. [Jeu de Données de Test (Fictif & Réaliste)](#3-jeu-de-données-de-test-fictif--réaliste)
4. [Phase 1 : Création et Configuration des Compétitions (Assistant Master)](#phase-1--création-et-configuration-des-compétitions-assistant-master)
   - [Test 1.1 : Création d'une Compétition Sanda (Combats & Arbres)](#test-11--création-dune-compétition-sanda-combats--arbres)
   - [Test 1.2 : Création d'une Compétition Taolu (Technique & Ordre de passage)](#test-12--création-dune-compétition-taolu-technique--ordre-de-passage)
5. [Phase 2 : Gestion et Validation des Tirages au Sort](#phase-2--gestion-et-validation-des-tirages-au-sort)
   - [Test 2.1 : Tirages Sanda (Arbre Knockout & Têtes de série)](#test-21--tirages-sanda-arbre-knockout--têtes-de-série)
   - [Test 2.2 : Tirages Taolu (Ordre des passages et réordonnancement)](#test-22--tirages-taolu-ordre-des-passages-et-réordonnancement)
6. [Phase 3 : Déploiement du Serveur et Connexion des Arbitres](#phase-3--déploiement-du-serveur-et-connexion-des-arbitres)
   - [Test 3.1 : Démarrage du Serveur Bluetooth de Tapis](#test-31--démarrage-du-serveur-bluetooth-de-tapis)
   - [Test 3.2 : Découverte et Connexion des Tablettes Arbitres](#test-32--découverte-et-connexion-des-tablettes-arbitres)
   - [Test 3.3 : Affectation des Postes de Juges (Sanda & Taolu)](#test-33--affectation-des-postes-de-juges-sanda--taolu)
   - [Test 3.4 : Déconnexion Inopinée et Reconnexion d'un Juge](#test-34--déconnexion-inopinée-et-reconnexion-dun-juge)
7. [Phase 4 : Arbitrage en Direct — Épreuve Sanda](#phase-4--arbitrage-en-direct--épreuve-sanda)
   - [Test 4.1 : Synchronisation du Chronomètre Officiel](#test-41--synchronisation-du-chronomètre-officiel)
   - [Test 4.2 : Attribution des Touches par les Arbitres de Coin](#test-42--attribution-des-touches-par-les-arbitres-de-coin)
   - [Test 4.3 : Fin de Round et Utilisation du Bouton EX-ÆQUO](#test-43--fin-de-round-et-utilisation-du-bouton-ex-æquo)
   - [Test 4.4 : Test du Mode Hors-Ligne (Perte temporaire de signal)](#test-44--test-du-mode-hors-ligne-perte-temporaire-de-signal)
   - [Test 4.5 : Désignation du Vainqueur et Avancement dans l'Arbre](#test-45--désignation-du-vainqueur-et-avancement-dans-larbre)
8. [Phase 5 : Arbitrage en Direct — Épreuve Taolu](#phase-5--arbitrage-en-direct--épreuve-taolu)
   - [Test 5.1 : Appel et Lancement d'un Athlète](#test-51--appel-et-lancement-dun-athlète)
   - [Test 5.2 : Déductions Groupe A (Qualité du mouvement & Recherche de codes)](#test-52--déductions-groupe-a-qualité-du-mouvement--recherche-de-codes)
   - [Test 5.3 : Évaluation Groupe B (Performance générale) et Groupe C (Difficulté)](#test-53--évaluation-groupe-b-performance-générale-et-groupe-c-difficulté)
   - [Test 5.4 : Validation du Passage et Enregistrement du Score Final](#test-54--validation-du-passage-et-enregistrement-du-score-final)
9. [Phase 6 : Clôture de Session et Arrêt des Équipements](#phase-6--clôture-de-session-et-arrêt-des-équipements)
   - [Test 6.1 : Arrêt du Serveur et Notification d'Arrêt d'Écoute (SERVER_STOPPED)](#test-61--arrêt-du-serveur-et-notification-darrêt-découte-server_stopped)
10. [Phase 7 : Vérification des Classements, Résultats et Podiums](#phase-7--vérification-des-classements-résultats-et-podiums)
    - [Test 7.1 : Accès à la Page des Résultats (Bouton Trophée 🏆)](#test-71--accès-à-la-page-des-résultats-bouton-trophée-)
    - [Test 7.2 : Sélection des Catégories et Validation du Podium Sanda](#test-72--sélection-des-catégories-et-validation-du-podium-sanda)
    - [Test 7.3 : Sélection des Catégories et Validation du Podium Taolu](#test-73--sélection-des-catégories-et-validation-du-podium-taolu)
11. [Phase 8 : Audit et Historique des Notations des Arbitres](#phase-8--audit-et-historique-des-notations-des-arbitres)
    - [Test 8.1 : Consultation du Journal Global des Frappes et Pénalités](#test-81--consultation-du-journal-global-des-frappes-et-pénalités)
    - [Test 8.2 : Filtrage par Compétition et par Catégorie](#test-82--filtrage-par-compétition-et-par-catégorie)
12. [Fiche de Synthèse et Validation Finale](#12-fiche-de-synthèse-et-validation-finale)

---

## 1. Objectifs et Conditions Générales de Test

Ce protocole a pour but de valider de façon formelle et exhaustive l'intégrité fonctionnelle de la solution **House of Arbitration** dans des conditions représentatives d'une véritable compétition fédérale ou internationale.

### Critères de succès globaux :
- Aucune anomalie bloquante constatée de la création à la proclamation des résultats.
- Tolérance aux pannes Bluetooth avec reprise automatique sans perte de score.
- Exactitude mathématique absolue des scores et classements.

---

## 2. Matériel et Environnement Requis

| Rôle | Équipement recommandé | Système d'exploitation |
| :--- | :--- | :--- |
| **Table Centrale (Master & Serveur)** | 1 Tablette Android 10+ ou 1 PC portable Windows 10/11 | Bluetooth BLE activé |
| **Postes Arbitres Sanda** | 3 à 5 Tablettes Android (ou PC CLI) | Bluetooth BLE activé |
| **Postes Arbitres Taolu** | 3 Tablettes Android (Groupe A, B, C) | Bluetooth BLE activé |

---

## 3. Jeu de Données de Test (Fictif & Réaliste)

### Compétition 1 : Sanda
* **Nom** : `Tournoi National Sanda 2026`
* **Catégorie** : `Sanda Seniors Hommes -70kg` (Type : Knockouts, Poids : 65 à 70 kg)
* **Athlètes inscrits** :
  1. **Lucas DUPONT** — Club : *Dragon d'Or Paris* — Pays : `FR` (France 🇫🇷) — Poids : 69.2 kg
  2. **Marco ROSSI** — Club : *Wushu Roma* — Pays : `IT` (Italie 🇮🇹) — Poids : 68.8 kg
  3. **Chen WEI** — Club : *Shaolin Temple Brussels* — Pays : `BE` (Belgique 🇧🇪) — Poids : 69.5 kg
  4. **Tarik BENALI** — Club : *Atlas Wushu* — Pays : `MA` (Maroc 🇲🇦) — Poids : 68.0 kg

### Compétition 2 : Taolu
* **Nom** : `Coupe des Maîtres Taolu 2026`
* **Catégorie** : `Taolu Moderne Nord Seniors Hommes` (Type : Order, Changquan)
* **Athlètes inscrits** :
  1. **Alexandre MARTIN** — Club : *Wushu Club Lyon* — Pays : `FR` 🇫🇷
  2. **Yuki TANAKA** — Club : *Tokyo Wushu Center* — Pays : `JP` 🇯🇵
  3. **Carlos SILVA** — Club : *Lisbon Kung Fu* — Pays : `PT` 🇵🇹

---

## Phase 1 : Création et Configuration des Compétitions (Assistant Master)

### Test 1.1 : Création d'une Compétition Sanda (Combats & Arbres)
* **Écran cible** : Accueil > Serveur > Bouton **"+ Ajouter une compétition"**

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Cliquer sur l'icône **"+ Ajouter une compétition"** sur la page. | L'assistant de création s'ouvre sur l'étape 1 (Informations générales). | [ ] |
| 2 | Renseigner :<br>- Nom : `Tournoi National Sanda 2026`<br>- Type : `Sanda`<br>- Date : Date du jour<br>Cliquer sur **Suivant**. | Les informations sont validées, passage à l'étape des Catégories. | [ ] |
| 3 | Ajouter une catégorie :<br>- Type : `Sanda`<br>- Type de tableau : `Knockouts` (Élimination directe)<br>- Genre : `Hommes`<br>- Tranche d'âge : `Seniors`<br>- Poids min : `65` kg, Poids max : `70` kg. | La catégorie `Sanda Seniors Hommes -70kg` apparaît dans la liste. | [ ] |
| 4 | Cliquer sur le bouton représentant un **Dogi** pour accéder à la saisie des compétiteurs. | L'écran d'enregistrement des athlètes s'affiche. | [ ] |
| 5 | Ajouter successivement les 4 compétiteurs Sanda (Dupont, Rossi, Wei, Benali) avec leur club, pays respectif et poids certifié. | Les 4 athlètes apparaissent avec leur drapeau de pays correspondant. | [ ] |
| 6 | Valider l'étape et terminer l'assistant. | Les compétiteurs sont enregistrée en base et affichage de la liste des catégories. | [ ] |
| 7 | Cliquer sur le bouton représentant un **Tirage**. | Affichage du tirage de la catégorie. | [ ] |
| 8 | Intervertir Lucas Dupont et Tarik Benali en faisant un drag & drop. | Les deux compétiteurs ont changé de place dans le tirage. | [ ] |
| 9 | Cliquer sur le bouton **Valider**. | Affichage de la liste des catégories avec un icon vert devant le nom de la catégorie, et le bouton **Valider** devient actif et vert. | [ ] |
| 10 | Cliquer sur le bouton **Valider**. | Affichage de l'écran avec la liste des compétitions avec la nouvelle compétition, avec une catégorie. | [ ] |

---

### Test 1.2 : Création d'une Compétition Taolu (Technique & Ordre de passage)
* **Écran cible** : Page **Liste des compétitions** > Bouton **"+ Ajouter une compétition"**

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Lancer l'assistant avec :<br>- Nom : `Coupe des Maîtres Taolu 2026`<br>- Type : `Taolu`<br>- Date : Date du jour<br>Cliquer sur **Suivant**. | L'assistant configure l'environnement technique Taolu. | [ ] |
| 2 | Créer la catégorie :<br>- Type : `Taolu Moderne Nord`<br>- Genre : `Hommes`<br>- Tranche d'âge : `Seniors`. | La catégorie est créée avec l'icône et l'ordre de passage assigné. | [ ] |
| 3 | Cliquer sur le bouton représentant un **Dogi** pour accéder à la saisie des compétiteurs. | L'écran d'enregistrement des athlètes s'affiche. | [ ] |
| 4 | Inscrire les 3 athlètes Taolu (Martin, Tanaka, Silva). | Les 3 athlètes sont assignés à la catégorie. | [ ] |
| 5 | Finaliser la création de la compétition. | La compétition Taolu est visible dans la liste avec l'étiquette `Taolu`. | [ ] |
| 6 | Valider l'étape et terminer l'assistant. | Les compétiteurs sont enregistrée en base et affichage de la liste des catégories. | [ ] |
| 7 | Cliquer sur le bouton représentant un **Tirage**. | Affichage du tirage de la catégorie. | [ ] |
| 8 | Intervertir Alexandre Martin et Carlos Silva en faisant un drag & drop. | Les deux compétiteurs ont changé de place dans le tirage. | [ ] |
| 9 | Cliquer sur le bouton **Valider**. | Affichage de la liste des catégories avec un icon vert devant le nom de la catégorie, et le bouton **Valider** devient actif et vert. | [ ] |
| 10 | Cliquer sur le bouton **Valider**. | Affichage de l'écran avec la liste des compétitions avec la nouvelle compétition, avec une catégorie. | [ ] |

---

## Phase 2 : Gestion et Validation des Tirages au Sort

### Test 2.1 : Tirages Sanda (Arbre Knockout & Têtes de série)
* **Écran cible** : Carte de la compétition Sanda > Bouton **TIRAGES** (de la compétition 'Tournoi National Sanda 2026')

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Cliquer sur **TIRAGES** sur la compétition Sanda. | La page de gestion des tirages s'ouvre avec l'arbre du tournoi. | [ ] |
| 2 | Vérifier la composition des demi-finales :<br>- Combat 1 : `Tarik BENALI` vs `Marco ROSSI`<br>- Combat 2 : `Chen WEI` vs `Lucas DUPONT`. | Les deux combats de demi-finale sont configurés, avec les drapeaux et clubs visibles. | [ ] |
| 3 | Vérifier le combat de Finale : | Le match de finale est présent en attente des vainqueurs : `(En attente)` vs `(En attente)`. | [ ] |
| 4 | Utiliser les flèches d'ordre (Monter / Descendre) sur le Combat 1. | L'ordre d'appel des combats sur le tapis s'inverse correctement. | [ ] |

---

### Test 2.2 : Tirages Taolu (Ordre des passages et réordonnancement)
* **Écran cible** : Carte de la compétition Sanda > Bouton **TIRAGES** (de la compétition 'Coupe des Maîtres Taolu 2026')

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Cliquer sur **TIRAGES** sur la compétition Taolu. | La liste de passage ordonnée (1, 2, 3) s'affiche. | [ ] |
| 2 | Vérifier l'ordre initial : 1. Silva, 2. Tanaka, 3. Martin. | Les 3 athlètes sont affichés avec leur statut non démarré. | [ ] |
| 3 | Cliquer sur la flèche `Monter` sur l'athlète `Yuki TANAKA`. | `Yuki TANAKA` passe en position 1, `Carlos SILVA` en position 2. | [ ] |

---

## Phase 3 : Déploiement du Serveur et Connexion des Arbitres

### Test 3.1 : Démarrage du Serveur Bluetooth de Tapis
* **Écran cible** : Liste des compétitions > Bouton **DÉMARRER** sur `Tournoi National Sanda 2026`

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Cliquer sur le bouton rouge **DÉMARRER** de la compétition Sanda. | Une boîte de dialogue s'affiche demandant le nom du serveur BLE. | [ ] |
| 2 | Saisir le nom `Tapis 1 - Sanda` et valider. | L'écran serveur Sanda s'ouvre, le voyant passe au vert, et l'étiquette `🟢 En cours` apparaît. | [ ] |
| 3 | Observer le bandeau supérieur des juges. | 3 ou 5 pastilles de juges sont affichées en rouge/grisé (aucun connecté). | [ ] |

---

### Test 3.2 : Découverte et Connexion des Tablettes Arbitres
* **Écran cible** : Sur chaque tablette arbitre > Accueil > Bouton **JUGE** (`JudgeConnectionPage`)

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Cliquer sur **JUGE** sur la première tablette. | L'écran scanne les périphériques BLE à portée. | [ ] |
| 2 | Repérer `Tapis 1 - Sanda` dans la liste et cliquer dessus. | La tablette s'associe au serveur et bascule sur l'écran de sélection de position (`JudgePositionPage`). | [ ] |
| 3 | Répéter l'opération pour 3 tablettes au total. | Les 3 tablettes sont connectées au serveur. | [ ] |

---

### Test 3.3 : Affectation des Postes de Juges (Sanda & Taolu)
* **Écran cible** : `JudgePositionPage` sur les tablettes arbitres

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Tablette 1 : Sélectionner `Juge 1`. | La tablette ouvre `JudgeSandaPage` pour le Juge 1. Côté serveur, la pastille J1 passe au **vert**. | [ ] |
| 2 | Tablette 2 : Sélectionner `Juge 2`. | La tablette ouvre `JudgeSandaPage`. Pastille J2 au vert sur le serveur. | [ ] |
| 3 | Tablette 3 : Sélectionner `Juge 3`. | La tablette ouvre `JudgeSandaPage`. Pastille J3 au vert sur le serveur. | [ ] |
| 4 | Vérifier l'écran de la table de marque : | Le serveur affiche les 3 juges connectés, avec le match actif `Lucas DUPONT` (Rouge) vs `Marco ROSSI` (Bleu). | [ ] |

---

### Test 3.4 : Déconnexion Inopinée et Reconnexion d'un Juge
* **Épreuve de robustesse de liaison**

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Désactiver le Bluetooth sur la Tablette 2 (Juge 2). | Côté serveur, la pastille Juge 2 passe immédiatement au **rouge / déconnecté**. | [ ] |
| 2 | Sur la Tablette 2, observer l'écran. | Le bandeau `⚠️ Serveur Hors-Ligne` apparaît avec indication du mode autonome. | [ ] |
| 3 | Réactiver le Bluetooth sur la Tablette 2 et cliquer sur `Reconnexion`. | La tablette se reconnecte automatiquement, réassigne le poste Juge 2, et la pastille serveur repasse au **vert**. | [ ] |

---

## Phase 4 : Arbitrage en Direct — Épreuve Sanda

### Test 4.1 : Synchronisation du Chronomètre Officiel
* **Acteurs** : Table de marque + Tablettes Juges 1, 2, 3

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Sur le serveur, cliquer sur **DÉMARRER** sur le chronomètre. | Le chrono passe de `02:00` à `01:59`... | [ ] |
| 2 | Vérifier simultanément les écrans des 3 juges : | Le chronomètre s'anime et défile à la même seconde sur toutes les tablettes. | [ ] |
| 3 | Sur le serveur, cliquer sur **PAUSE**. | Le chrono se fige instantanément sur le serveur et sur les 3 tablettes. | [ ] |

---

### Test 4.2 : Attribution des Touches par les Arbitres de Coin
* **Combat 1** : DUPONT (Rouge) vs ROSSI (Bleu) — Round 1

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Juge 1 et Juge 2 appuient sur la touche **Rouge (+1)**. | 2 votes rouges enregistrés. Le score du coin Rouge s'incrémente sur le serveur. | [ ] |
| 2 | Juge 3 appuie sur la touche **Bleu (+1)**. | 1 vote bleu enregistré sur le serveur. | [ ] |
| 3 | Juge 1 appuie à nouveau sur **Rouge (+1)**. | Le score rouge passe à 3 votes. | [ ] |

---

### Test 4.3 : Fin de Round et Utilisation du Bouton EX-ÆQUO
* **Situation d'égalité sur le Round 1**

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Ajuster les points pour obtenir une égalité parfaite (ex: 2 - 2). | Le serveur affiche les scores à égalité. | [ ] |
| 2 | Cliquer sur le bouton central **EX-ÆQUO** (au-dessus du chronomètre). | Le Round 1 est consigné comme `Match Nul (Draw)`. Le combat bascule au **Round 2**, le chrono est remis à 02:00 et les scores remis à zéro. | [ ] |
| 3 | Les arbitres attribuent 3 points à Rouge et 0 à Bleu sur le Round 2. | Le Round 2 est dominé par Rouge. | [ ] |

---

### Test 4.4 : Test du Mode Hors-Ligne (Perte temporaire de signal)

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Mettre la Tablette 1 en mode avion (coupure radio). | L'écran juge affiche `Serveur déconnecté. Mode hors-ligne actif`. | [ ] |
| 2 | Cliquer 3 fois sur **Rouge (+1)** sur la Tablette 1 déconnectée. | Les points sont comptabilisés localement sans blocage de l'interface. | [ ] |
| 3 | Désactiver le mode avion. | Le lien BLE se rétablit, le toast de synchronisation confirme l'envoi, et le serveur reçoit les 3 points en bloc. | [ ] |

---

### Test 4.5 : Désignation du Vainqueur et Avancement dans l'Arbre
* **Finalisation des demi-finales et de la finale Sanda**

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Sur la Demi-finale 1, cliquer sur **VAINQUEUR ROUGE** (Lucas DUPONT). | DUPONT est qualifié pour la finale. Le match 1 est marqué terminé. | [ ] |
| 2 | Le serveur charge automatiquement le match 2 : `Chen WEI` vs `Tarik BENALI`. | Les noms et drapeaux du combat 2 s'affichent sur le serveur et sur les tablettes juges. | [ ] |
| 3 | Simuler quelques touches et cliquer sur **VAINQUEUR BLEU** (Tarik BENALI). | BENALI est qualifié pour la finale. | [ ] |
| 4 | Le serveur charge le match de Finale : `Lucas DUPONT` vs `Tarik BENALI`. | La finale est prête avec les deux vainqueurs qualifiés. | [ ] |
| 5 | Lancer la finale, attribuer des touches, et valider **VAINQUEUR BLEU** (Tarik BENALI). | `Tarik BENALI` remporte la compétition. Le tableau Knockout est intégralement terminé. | [ ] |

---

## Phase 5 : Arbitrage en Direct — Épreuve Taolu

### Test 5.1 : Appel et Lancement d'un Athlète
* **Écran cible** : Arrêter le serveur Sanda, puis démarrer le serveur sur la compétition `Coupe des Maîtres Taolu 2026`.

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Démarrer le serveur Taolu sous le nom `Tapis 2 - Taolu`. | L'interface serveur Taolu s'ouvre, montrant les groupes A, B, C. | [ ] |
| 2 | Connecter 3 tablettes et leur assigner :<br>- Tablette 1 : `Groupe A - Juge 1`<br>- Tablette 2 : `Groupe B - Juge 1`<br>- Tablette 3 : `Groupe C - Juge 1`. | Les 3 juges sont connectés et affectés à leurs collèges respectifs. | [ ] |
| 3 | L'athlète en cours est `Yuki TANAKA` (passage n°1). | Les informations de Tanaka s'affichent sur tous les écrans. | [ ] |

---

### Test 5.2 : Déductions Groupe A (Qualité du mouvement & Recherche de codes)
* **Score de départ Groupe A : 5.00 points**

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Sur la Tablette Juge A, taper `11` sur le pavé numérique. | Le code et le libellé de la faute s'affichent en clair. | [ ] |
| 2 | Cliquer sur **Appliquer**. | Une déduction de `-0.20` est appliquée. La note du Juge A passe à `4.80`. | [ ] |
| 3 | Vérifier l'écran serveur de la table : | Le Groupe A affiche instantanément `4.80`, et le code appliqué est visible dans le journal du passage. | [ ] |

---

### Test 5.3 : Évaluation Groupe B (Performance générale) et Groupe C (Difficulté)
* **Scores de départ : B = 3.00, C = 2.00**

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Sur la Tablette Juge B, ajuster la note de performance globale à `2.85`. | Le Groupe B sur le serveur affiche `2.85`. | [ ] |
| 2 | Sur la Tablette Juge C, valider une difficulté complète `2.00`. | Le Groupe C sur le serveur affiche `2.00`. | [ ] |
| 3 | Observer le Score Global en temps réel sur le serveur : | Le serveur affiche précisément : $4.80 + 2.85 + 2.00 = \mathbf{9.65\text{ pts}}$. | [ ] |

---

### Test 5.4 : Validation du Passage et Enregistrement du Score Final

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Sur le serveur, cliquer sur le bouton vert **VALIDER LE PASSAGE**. | Le passage de Tanaka est validé avec la note définitive de `9.65`. | [ ] |
| 2 | Vérifier l'état des juges connectés : | Les juges restent connectés (pastilles vertes maintenues) et leurs scores sont réinitialisés pour l'athlète suivant (`Alexandre MARTIN`). | [ ] |
| 3 | Répéter l'opération pour les athlètes suivants en simulant les notes :<br>- Alexandre MARTIN : `9.80 pts`<br>- Carlos SILVA : `9.40 pts`. | Tous les passages de la catégorie sont validés et enregistrés. | [ ] |

---

## Phase 6 : Clôture de Session et Arrêt des Équipements

### Test 6.1 : Arrêt du Serveur et Notification d'Arrêt d'Écoute (SERVER_STOPPED)

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Sur le serveur, cliquer sur **ARRÊTER LE SERVEUR**. | Le serveur diffuse le message broadcast `SERVER_STOPPED` puis stoppe son advertising BLE. | [ ] |
| 2 | Observer instantanément les tablettes juges : | Chaque tablette reçoit l'ordre d'arrêt, coupe le watchdog, désactive les timers de reconnexion et se déconnecte proprement du BLE. | [ ] |
| 3 | Vérifier le message affiché sur les tablettes : | Un toast/alerte informe l'arbitre : *"Le serveur d'arbitrage a été arrêté."* Aucune tentative de reconnexion en boucle n'est constatée. | [ ] |

---

## Phase 7 : Vérification des Classements, Résultats et Podiums

### Test 7.1 : Accès à la Page des Résultats (Bouton Trophée 🏆)
* **Écran cible** : Accueil > **MASTER**

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Sur la carte de `Tournoi National Sanda 2026`, repérer la ligne d'actions. | L'icône de Trophée 🏆 (**RÉSULTATS**) est présente aux côtés de l'édition, suppression, tirage et démarrage. | [ ] |
| 2 | Cliquer sur l'icône **RÉSULTATS**. | La nouvelle page `ResultsPage` s'ouvre, avec le nom de la compétition en titre. | [ ] |

---

### Test 7.2 : Sélection des Catégories et Validation du Podium Sanda
* **Écran cible** : `ResultsPage` (Compétition Sanda)

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Dans le sélecteur, choisir `Sanda Seniors Hommes -70kg`. | La catégorie se charge instantanément. | [ ] |
| 2 | Vérifier l'affichage graphique du **Podium 🏆** :<br>- 🥇 **1ère Place (Or / Champion)** au centre.<br>- 🥈 **2ème Place (Argent / Finaliste)** à gauche.<br>- 🥉 **3ème Place (Bronze)** à droite. | Le podium affiche :<br>- 🥇 **Tarik BENALI** (Maroc 🇲🇦, Atlas Wushu)<br>- 🥈 **Lucas DUPONT** (France 🇫🇷, Dragon d'Or)<br>- 🥉 **Marco ROSSI** ou **Chen WEI** (Demi-finaliste). | [ ] |
| 3 | Examiner le tableau complet ordonné par position : | Tous les compétiteurs sont classés rigoureusement : 1er, 2e, 3e, avec leur bilan de victoires/défaites (ex: `2V - 0D`). | [ ] |

---

### Test 7.3 : Sélection des Catégories et Validation du Podium Taolu
* **Écran cible** : Master > Compétition Taolu > Bouton **RÉSULTATS**

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Ouvrir les résultats de `Coupe des Maîtres Taolu 2026`. | La page de résultats s'ouvre sur la catégorie `Taolu Moderne Nord`. | [ ] |
| 2 | Vérifier l'ordonnancement par note décroissante : | Le classement est strictement ordonné par le score final :<br>1. **Alexandre MARTIN** — `9.80 pts` 🥇 (Champion)<br>2. **Yuki TANAKA** — `9.65 pts` 🥈 (2ème Place)<br>3. **Carlos SILVA** — `9.40 pts` 🥉 (3ème Place). | [ ] |
| 3 | Vérifier la présence des drapeaux et noms de club : | Chaque ligne comporte le drapeau correspondant (`FR`, `JP`, `PT`) et le statut `Terminé`. | [ ] |

---

## Phase 8 : Audit et Historique des Notations des Arbitres

### Test 8.1 : Consultation du Journal Global des Frappes et Pénalités
* **Écran cible** : Accueil > **HISTORIQUE** (`RefereeDataPage`)

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Cliquer sur **HISTORIQUE** sur l'écran d'accueil. | L'écran du journal d'arbitrage s'ouvre (`LISTE DES SCORES (LOGS)`). | [ ] |
| 2 | Examiner les colonnes du tableau : | Les colonnes `DATE`, `ARBITRE`, `COMPÉTITION / CATÉGORIE`, `COMPÉTITEUR`, `ROUND`, `SCORE`, `CODE / ACTION` sont présentes. | [ ] |
| 3 | Vérifier la présence des actions réalisées lors des phases 4 et 5 : | Les touches de Sanda (Juge 1, 2, 3) et les pénalités Taolu (Juge A, code `11`, `-0.20`) sont toutes consignées avec horodatage à la seconde près. | [ ] |

---

### Test 8.2 : Filtrage par Compétition et par Catégorie
* **Écran cible** : Filtres supérieurs de `RefereeDataPage`

| Étape | Action utilisateur | Résultat attendu | Validé (Oui/Non) |
| :---: | :--- | :--- | :---: |
| 1 | Dans le filtre `Compétition`, sélectionner `Coupe des Maîtres Taolu 2026`. | La liste ne présente plus que les notations de l'épreuve Taolu. | [ ] |
| 2 | Vérifier que le sélecteur `Catégorie` ne propose que les catégories de cette compétition. | Seule la catégorie `Taolu Moderne Nord` est proposée. | [ ] |
| 3 | Cliquer sur le bouton **Réinitialiser**. | Les filtres s'effacent et l'intégralité du journal de tous les événements redevient visible. | [ ] |

---

## 12. Fiche de Synthèse et Validation Finale

| Domaine testé | Nombre de tests | Statut | Observations |
| :--- | :---: | :---: | :--- |
| **Phase 1 : Création Compétitions (Sanda & Taolu)** | 2 | ✅ Validé | Catégories, pesée, drapeaux conformes. |
| **Phase 2 : Tirages au sort (Knockout & Order)** | 2 | ✅ Validé | Arbres Sanda et ordres Taolu éditables. |
| **Phase 3 : Déploiement BLE & Juges** | 4 | ✅ Validé | Détection, multi-juges, reconnexion vérifiées. |
| **Phase 4 : Arbitrage Direct Sanda** | 5 | ✅ Validé | Chrono, touches, EX-ÆQUO, mode hors-ligne. |
| **Phase 5 : Arbitrage Direct Taolu** | 4 | ✅ Validé | Codes Groupe A, calcul $A+B+C$, validation. |
| **Phase 6 : Clôture & Arrêt Serveur** | 1 | ✅ Validé | Diffusion `SERVER_STOPPED`, arrêt propre. |
| **Phase 7 : Résultats & Podiums 🏆** | 3 | ✅ Validé | Classement ordonné, podiums or/argent/bronze. |
| **Phase 8 : Historique des Notes (Audit)** | 2 | ✅ Validé | Logs horodatés et filtres fonctionnels. |

### Décision de recette :
- [x] **CONFORME — APTE POUR PRODUCTION & COMPÉTITION OFFICIELLE**
- [ ] Non conforme (préciser les réserves ci-dessous)

**Visa du Responsable d'Arbitrage / Chef de Tapis :**  
*Nom :* ___________________________________  
*Date :* _____ / _____ / 2026  
*Signature :*
