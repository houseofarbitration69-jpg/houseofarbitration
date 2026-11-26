using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Server.Cli;

public class MatchItem
{
    public int Id { get; set; }
    public RoundType Type { get; set; } = RoundType.Knockouts;
    public string Category { get; set; } = "Senior -70kg";
    public string RedCompetitor { get; set; } = "Combattant Rouge";
    public string BlueCompetitor { get; set; } = "Combattant Bleu";
    public int RedScore { get; set; } = 0;
    public int BlueScore { get; set; } = 0;
    public bool IsFinished { get; set; }
    public string? Winner { get; set; }
    public Constants.Side? WinnerSide { get; set; }
    public double FinalScore { get; set; }
    public string? DecisionReason { get; set; }
    public int? NextMatchId { get; set; }
    public Constants.Side? NextMatchSide { get; set; }
    public int? NextLoserMatchId { get; set; }
    public Constants.Side? NextLoserMatchSide { get; set; }
    public bool IsBronzeMatch { get; set; }
    public string? StageName { get; set; }
    public int CurrentRound { get; set; } = 1;
    public int MaxRounds { get; set; } = 3;
    public List<RoundResultModel> RoundResults { get; } = new();
    public List<double> JudgeScores { get; set; } = new();
    public Dictionary<string, double> JudgeScoresByJudge { get; set; } = new();
}

/// <summary>
/// Gestionnaire central de la session d'arbitrage côté serveur.
/// Gère les juges, les matchs, le chronomètre et le protocole de communication.
/// </summary>
public class ArbitrationServerSession : IDisposable
{
    private readonly WindowsBluetoothServer _bluetoothServer;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };
    private readonly Dictionary<string, JudgeModel> _clientJudgeMapping = new();
    private readonly HashSet<string> _monitorClients = new();
    private readonly List<MatchItem> _matches = new();
    private readonly List<CategoryModel> _loadedCategories = new();
    private readonly List<JudgeScoreEntry> _judgeScoreHistory = new();

    private System.Threading.Timer? _timer;
    private TimeSpan _timeLeft = TimeSpan.FromMinutes(2);
    private TimeSpan _totalRoundTime = TimeSpan.FromMinutes(2);
    private int _currentMatchIndex = 0;
    private int _timeSpeedMultiplier = 1;

    public WindowsBluetoothServer BluetoothServer => _bluetoothServer;
    public IReadOnlyList<CategoryModel> LoadedCategories => _loadedCategories;
    public IReadOnlyList<JudgeScoreEntry> JudgeScoreHistory => _judgeScoreHistory;

    public int TimeSpeedMultiplier => _timeSpeedMultiplier;
    public string TimeSpeedDisplay => $"x{_timeSpeedMultiplier}";
    public bool IsJudgeBotEnabled { get; private set; }
    public string JudgeBotStrategy { get; private set; } = "balanced";

    public CompetitionModel CurrentCompetition { get; private set; } = new()
    {
        Id = 1,
        Name = "Championnat National Wushu",
        Type = CompetitionType.Sanda,
        Date = DateTime.Today
    };

    public CompetitionType CurrentCompetitionType { get; private set; } = CompetitionType.Sanda;
    public ObservableCollection<JudgeModel> Judges { get; } = new();
    public ObservableCollection<JudgeGroupModel> JudgeGroups { get; } = new();

    public MatchItem? CurrentMatch => _matches.Count > 0 && _currentMatchIndex < _matches.Count ? _matches[_currentMatchIndex] : null;
    public IReadOnlyList<MatchItem> Matches => _matches;

    public bool IsTimerRunning { get; private set; }
    public string TimeLeftDisplay => $"{(int)_timeLeft.TotalMinutes:D2}:{_timeLeft.Seconds:D2}";

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<JudgeModel>? JudgeConnected;
    public event EventHandler<JudgeModel>? JudgeDisconnected;
    public event EventHandler<(JudgeModel Judge, string Details)>? ScoreUpdated;
    public event EventHandler<string>? TimerTick;
    public event EventHandler<(MatchItem Match, string Winner, string Reason)>? WinnerDeclared;
    public event EventHandler<JudgeScoreEntry>? JudgeScoreRecorded;
    public event EventHandler<(MatchItem Match, int RoundNumber)>? RoundChanged;

    public ArbitrationServerSession(WindowsBluetoothServer bluetoothServer)
    {
        _bluetoothServer = bluetoothServer;
        _bluetoothServer.MessageReceived += OnBluetoothMessageReceived;
        _bluetoothServer.ClientDisconnected += OnClientDisconnected;
        _bluetoothServer.ClientConnected += async (sender, clientId) =>
        {
            try
            {
                await SendCompetitionDataToClientAsync(clientId);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Erreur lors de l'envoi de la compétition au client {clientId} : {ex.Message}");
            }
        };

        // Initialisation des données par défaut
        SetupDefaultMatches();
        SetupJudges(CompetitionType.Sanda);
    }

    /// <summary>
    /// Configure la session pour Sanda ou Taolu.
    /// </summary>
    public async Task SetCompetitionTypeAsync(CompetitionType type)
    {
        CurrentCompetitionType = type;
        CurrentCompetition.Type = type;
        SetupJudges(type);
        StatusChanged?.Invoke(this, $"Mode de compétition défini sur : {type}");

        if (_bluetoothServer.IsAdvertising)
        {
            await BroadcastCompetitionDataAsync();
            await BroadcastMatchInfoAsync();
        }
    }

    /// <summary>
    /// Charge une compétition avec sa liste de matchs (par exemple importée d'un fichier Excel).
    /// </summary>
    public async Task LoadCompetitionAsync(CompetitionModel competition, IEnumerable<MatchItem> matches, IEnumerable<CategoryModel>? categories = null)
    {
        CurrentCompetition = competition;
        if (categories != null && (competition.Categories == null || competition.Categories.Count == 0))
        {
            competition.Categories = categories.ToList();
        }
        CurrentCompetitionType = competition.Type;
        SetupJudges(competition.Type);

        _matches.Clear();
        _matches.AddRange(matches);
        _currentMatchIndex = 0;
        ResetMatchScores();

        _loadedCategories.Clear();
        if (categories != null)
        {
            _loadedCategories.AddRange(categories);
        }
        else if (competition.Categories != null && competition.Categories.Count > 0)
        {
            _loadedCategories.AddRange(competition.Categories);
        }

        StatusChanged?.Invoke(this, $"Compétition '{competition.Name}' ({competition.Type}) chargée avec {_matches.Count} match(s).");

        if (_bluetoothServer.IsAdvertising)
        {
            await BroadcastCompetitionDataAsync();
            await BroadcastMatchInfoAsync();
        }
    }

    private void SetupJudges(CompetitionType type)
    {
        Judges.Clear();
        JudgeGroups.Clear();
        _clientJudgeMapping.Clear();

        if (type == CompetitionType.Taolu)
        {
            var groupA = new JudgeGroupModel("Groupe A", "Qualité", Constants.DEFAULT_SCORE_GROUP_A);
            var groupB = new JudgeGroupModel("Groupe B", "Performance", Constants.DEFAULT_SCORE_GROUP_B);
            var groupC = new JudgeGroupModel("Groupe C", "Difficulté", Constants.DEFAUTL_SCORE_GROUP_C);

            for (int i = 1; i <= 3; i++)
            {
                var judge = new JudgeModel { Name = $"JUGE A{i}", Number = i, Group = "A", Score = Constants.DEFAULT_SCORE_GROUP_A };
                Judges.Add(judge);
                groupA.Judges.Add(judge);
            }
            for (int i = 1; i <= 3; i++)
            {
                var judge = new JudgeModel { Name = $"JUGE B{i}", Number = i, Group = "B", Score = Constants.DEFAULT_SCORE_GROUP_B };
                Judges.Add(judge);
                groupB.Judges.Add(judge);
            }
            for (int i = 1; i <= 3; i++)
            {
                var judge = new JudgeModel { Name = $"JUGE C{i}", Number = i, Group = "C", Score = Constants.DEFAUTL_SCORE_GROUP_C };
                Judges.Add(judge);
                groupC.Judges.Add(judge);
            }

            JudgeGroups.Add(groupA);
            JudgeGroups.Add(groupB);
            JudgeGroups.Add(groupC);
        }
        else
        {
            // Sanda : 5 juges
            for (int i = 1; i <= 5; i++)
            {
                Judges.Add(new JudgeModel
                {
                    Name = $"JUGE {i}",
                    Number = i,
                    Group = null,
                    RedPoints = 0,
                    BluePoints = 0
                });
            }
        }
    }

    private void SetupDefaultMatches()
    {
        _matches.Clear();
        _matches.Add(new MatchItem { Id = 1, Type = RoundType.Knockouts, Category = "Sanda -65kg", RedCompetitor = "Dupont (Rouge)", BlueCompetitor = "Martin (Bleu)" });
        _matches.Add(new MatchItem { Id = 2, Type = RoundType.Knockouts, Category = "Sanda -70kg", RedCompetitor = "Zhang (Rouge)", BlueCompetitor = "Leclerc (Bleu)" });
        _matches.Add(new MatchItem { Id = 3, Type = RoundType.Order, Category = "Taolu Changquan", RedCompetitor = "Wang (Athlète)", BlueCompetitor = "" });
        _matches.Add(new MatchItem { Id = 4, Type = RoundType.Order, Category = "Taolu Nanquan", RedCompetitor = "Dubois (Athlète)", BlueCompetitor = "" });

        CurrentCompetition.Categories.Clear();

        var comp1 = new CompetitorModel { Id = 1, FirstName = "Dupont", LastName = "Rouge", CountryIsoCode = "FR" };
        var comp2 = new CompetitorModel { Id = 2, FirstName = "Martin", LastName = "Bleu", CountryIsoCode = "FR" };
        var cat1 = new CategoryModel { Id = 1, Type = CategoryType.Sanda, Genre = Genre.Men, WeightMax = 65, RoundType = RoundType.Knockouts, CompetitionId = CurrentCompetition.Id };
        var draw1 = new DrawModel { Id = 1, CategoryId = 1, Category = cat1 };
        draw1.DrawKnockouts = new List<DrawKnockoutModel>
        {
            new DrawKnockoutModel { Id = 1, Order = 1, GlobalOrder = 1, DrawId = 1, Competitor1Id = 1, Competitor1 = comp1, Competitor2Id = 2, Competitor2 = comp2 }
        };
        cat1.Draw = draw1;
        cat1.Competitors = new List<CompetitorCategoryModel>
        {
            new() { Id = 1, CategoryId = 1, CompetitorId = 1, Competitor = comp1 },
            new() { Id = 2, CategoryId = 1, CompetitorId = 2, Competitor = comp2 }
        };

        var comp3 = new CompetitorModel { Id = 3, FirstName = "Zhang", LastName = "Rouge", CountryIsoCode = "CN" };
        var comp4 = new CompetitorModel { Id = 4, FirstName = "Leclerc", LastName = "Bleu", CountryIsoCode = "FR" };
        var cat2 = new CategoryModel { Id = 2, Type = CategoryType.Sanda, Genre = Genre.Men, WeightMax = 70, RoundType = RoundType.Knockouts, CompetitionId = CurrentCompetition.Id };
        var draw2 = new DrawModel { Id = 2, CategoryId = 2, Category = cat2 };
        draw2.DrawKnockouts = new List<DrawKnockoutModel>
        {
            new DrawKnockoutModel { Id = 2, Order = 1, GlobalOrder = 2, DrawId = 2, Competitor1Id = 3, Competitor1 = comp3, Competitor2Id = 4, Competitor2 = comp4 }
        };
        cat2.Draw = draw2;
        cat2.Competitors = new List<CompetitorCategoryModel>
        {
            new() { Id = 3, CategoryId = 2, CompetitorId = 3, Competitor = comp3 },
            new() { Id = 4, CategoryId = 2, CompetitorId = 4, Competitor = comp4 }
        };

        var comp5 = new CompetitorModel { Id = 5, FirstName = "Wang", LastName = "Athlète", CountryIsoCode = "CN" };
        var cat3 = new CategoryModel { Id = 3, Type = CategoryType.Taolu, Genre = Genre.Men, RoundType = RoundType.Order, CompetitionId = CurrentCompetition.Id };
        var draw3 = new DrawModel { Id = 3, CategoryId = 3, Category = cat3 };
        draw3.DrawOrders = new List<DrawOrderModel>
        {
            new DrawOrderModel { Id = 3, Order = 1, GlobalOrder = 3, DrawId = 3, CompetitorId = 5, Competitor = comp5 }
        };
        cat3.Draw = draw3;
        cat3.Competitors = new List<CompetitorCategoryModel>
        {
            new() { Id = 5, CategoryId = 3, CompetitorId = 5, Competitor = comp5 }
        };

        var comp6 = new CompetitorModel { Id = 6, FirstName = "Dubois", LastName = "Athlète", CountryIsoCode = "FR" };
        var cat4 = new CategoryModel { Id = 4, Type = CategoryType.Taolu, Genre = Genre.Men, RoundType = RoundType.Order, CompetitionId = CurrentCompetition.Id };
        var draw4 = new DrawModel { Id = 4, CategoryId = 4, Category = cat4 };
        draw4.DrawOrders = new List<DrawOrderModel>
        {
            new DrawOrderModel { Id = 4, Order = 1, GlobalOrder = 4, DrawId = 4, CompetitorId = 6, Competitor = comp6 }
        };
        cat4.Draw = draw4;
        cat4.Competitors = new List<CompetitorCategoryModel>
        {
            new() { Id = 6, CategoryId = 4, CompetitorId = 6, Competitor = comp6 }
        };

        CurrentCompetition.Categories.Add(cat1);
        CurrentCompetition.Categories.Add(cat2);
        CurrentCompetition.Categories.Add(cat3);
        CurrentCompetition.Categories.Add(cat4);

        _loadedCategories.Clear();
        _loadedCategories.AddRange(CurrentCompetition.Categories);
    }

    public async Task NextMatchAsync()
    {
        // En mode Taolu, finaliser et enregistrer le match précédent avant de passer au suivant
        if (CurrentMatch != null && (CurrentCompetitionType == CompetitionType.Taolu || CurrentMatch.Type == RoundType.Order) && !CurrentMatch.IsFinished)
        {
            await SetTaoluScoreAsync();
        }

        if (_currentMatchIndex + 1 < _matches.Count)
        {
            _currentMatchIndex++;
            if (CurrentMatch != null && !CurrentMatch.IsFinished)
            {
                ResetMatchScores();
            }
            else
            {
                ResetRoundScores();
                RestoreJudgeScoresForMatch(CurrentMatch);
            }
            StatusChanged?.Invoke(this, $"Match suivant : #{CurrentMatch?.Id} - {CurrentMatch?.Category}");
            await BroadcastMatchInfoAsync();
        }
        else if (_currentMatchIndex < _matches.Count)
        {
            _currentMatchIndex = _matches.Count;
            ResetRoundScores();
            StatusChanged?.Invoke(this, "🏁 Tous les matchs de la compétition sont terminés !");
            await BroadcastMatchInfoAsync();
        }
        else
        {
            StatusChanged?.Invoke(this, "Il n'y a plus de match à disputer.");
        }
    }

    public async Task PreviousMatchAsync()
    {
        if (_currentMatchIndex > 0)
        {
            // En mode Taolu, finaliser le match en cours si non terminé
            if (CurrentMatch != null && (CurrentCompetitionType == CompetitionType.Taolu || CurrentMatch.Type == RoundType.Order) && !CurrentMatch.IsFinished)
            {
                await SetTaoluScoreAsync();
            }

            _currentMatchIndex--;
            if (CurrentMatch != null && !CurrentMatch.IsFinished)
            {
                ResetMatchScores();
            }
            else
            {
                ResetRoundScores();
                RestoreJudgeScoresForMatch(CurrentMatch);
            }
            StatusChanged?.Invoke(this, $"Match précédent : #{CurrentMatch?.Id} - {CurrentMatch?.Category}");
            await BroadcastMatchInfoAsync();
        }
    }

    public async Task SetMatchIndexAsync(int index)
    {
        if (index >= 0 && index <= _matches.Count)
        {
            // En mode Taolu, finaliser le match en cours si non terminé avant de changer d'index
            if (index != _currentMatchIndex && CurrentMatch != null && (CurrentCompetitionType == CompetitionType.Taolu || CurrentMatch.Type == RoundType.Order) && !CurrentMatch.IsFinished)
            {
                await SetTaoluScoreAsync();
            }

            _currentMatchIndex = index;
            if (CurrentMatch != null)
            {
                if (!CurrentMatch.IsFinished)
                {
                    ResetMatchScores();
                }
                else
                {
                    ResetRoundScores();
                    RestoreJudgeScoresForMatch(CurrentMatch);
                }
                StatusChanged?.Invoke(this, $"Match actif : #{CurrentMatch?.Id} - {CurrentMatch?.Category}");
            }
            else
            {
                ResetRoundScores();
                StatusChanged?.Invoke(this, "🏁 Tous les matchs de la compétition sont terminés !");
            }
            await BroadcastMatchInfoAsync();
        }
    }

    private void RestoreJudgeScoresForMatch(MatchItem? match)
    {
        if (match == null || !(CurrentCompetitionType == CompetitionType.Taolu || match.Type == RoundType.Order))
            return;

        var activeJudges = GetActiveJudges();

        if (match.JudgeScoresByJudge.Count > 0)
        {
            foreach (var j in activeJudges)
            {
                if (!string.IsNullOrWhiteSpace(j.Name) && match.JudgeScoresByJudge.TryGetValue(j.Name, out double savedScore))
                {
                    j.Score = savedScore;
                }
            }
        }
        else if (match.JudgeScores.Count > 0)
        {
            for (int i = 0; i < Math.Min(activeJudges.Count, match.JudgeScores.Count); i++)
            {
                activeJudges[i].Score = match.JudgeScores[i];
            }
        }
    }

    public void ResetMatchScores()
    {
        if (CurrentMatch != null)
        {
            CurrentMatch.CurrentRound = 1;
            CurrentMatch.RoundResults.Clear();
            UpdateUnderlyingDrawRound(CurrentMatch);
        }

        ResetRoundScores();
        StatusChanged?.Invoke(this, "Scores, points et rounds réinitialisés pour le match en cours.");
    }

    /// <summary>
    /// Réinitialise les scores des juges pour le round en cours sans modifier l'historique des rounds du match.
    /// </summary>
    public void ResetRoundScores()
    {
        foreach (var j in Judges)
        {
            j.RedPoints = 0;
            j.BluePoints = 0;
            j.Codes.Clear();

            if (j.Group == "A") j.Score = Constants.DEFAULT_SCORE_GROUP_A;
            else if (j.Group == "B") j.Score = Constants.DEFAULT_SCORE_GROUP_B;
            else if (j.Group == "C") j.Score = Constants.DEFAUTL_SCORE_GROUP_C;
            else j.Score = 10.0;
        }
    }

    /// <summary>
    /// Valide le round en cours et passe au round suivant pour le match actif.
    /// Enregistre les scores du round (RoundResultModel), réinitialise les points des juges,
    /// remet le chronomètre à zéro et diffuse l'état aux juges.
    /// Si un combattant remporte 2 rounds sur 3 (ou le maximum de rounds), déclare la fin du match.
    /// </summary>
    /// <param name="forcedWinner">Vainqueur forcé du round ("red", "blue", "draw") ou null pour calcul automatique selon les juges.</param>
    /// <returns>True si le round a été validé et passé au suivant (ou match terminé), false en cas d'erreur.</returns>
    public async Task<bool> NextRoundAsync(string? forcedWinner = null)
    {
        var match = CurrentMatch;
        if (match == null)
        {
            StatusChanged?.Invoke(this, "[ERREUR] Aucun match actif.");
            return false;
        }

        if (match.IsFinished)
        {
            StatusChanged?.Invoke(this, $"[ATTENTION] Le match #{match.Id} est déjà terminé (Vainqueur : {match.Winner}).");
            return false;
        }

        int redVotes = (int)Judges.Sum(j => j.RedPoints);
        int blueVotes = (int)Judges.Sum(j => j.BluePoints);

        string winnerColor;
        if (!string.IsNullOrWhiteSpace(forcedWinner))
        {
            string f = forcedWinner.Trim().ToLowerInvariant();
            if (f is "red" or "rouge" or "r") winnerColor = "Red";
            else if (f is "blue" or "bleu" or "b") winnerColor = "Blue";
            else if (f is "draw" or "nul" or "egalite" or "d") winnerColor = "Draw";
            else winnerColor = "Draw";
        }
        else
        {
            // Évaluation automatique selon la majorité des juges ou départage aux points
            var eval = EvaluateSandaScores();
            if (eval.Leader == "Rouge") winnerColor = "Red";
            else if (eval.Leader == "Bleu") winnerColor = "Blue";
            else winnerColor = "Draw";
        }

        int completedRound = match.CurrentRound;

        var roundResult = new RoundResultModel
        {
            RoundNumber = completedRound,
            RedVotes = redVotes,
            BlueVotes = blueVotes,
            WinnerColor = winnerColor
        };
        match.RoundResults.Add(roundResult);

        // Synchroniser avec l'entité sous-jacente du tirage si existante
        UpdateUnderlyingDrawRound(match);

        // Calcul des victoires de rounds
        int redWins = match.RoundResults.Count(r => r.WinnerColor == "Red");
        int blueWins = match.RoundResults.Count(r => r.WinnerColor == "Blue");

        string roundWinnerName = winnerColor == "Red" ? match.RedCompetitor : (winnerColor == "Blue" ? match.BlueCompetitor : "Égalité");
        StatusChanged?.Invoke(this, $"🏁 [ROUND {completedRound} VALIDÉ] Vainqueur : {roundWinnerName} ({winnerColor}) - Rouge {redVotes} vs {blueVotes} Bleu | Total rounds : Rouge {redWins} - {blueWins} Bleu");

        // Vérification fin de match (best of 3 en Sanda)
        if (redWins >= 2 || blueWins >= 2 || match.CurrentRound >= match.MaxRounds)
        {
            Constants.Side? finalWinnerSide = null;
            if (redWins > blueWins) finalWinnerSide = Constants.Side.Red;
            else if (blueWins > redWins) finalWinnerSide = Constants.Side.Blue;

            if (finalWinnerSide.HasValue)
            {
                int winnerWins = finalWinnerSide == Constants.Side.Red ? redWins : blueWins;
                int loserWins = finalWinnerSide == Constants.Side.Red ? blueWins : redWins;
                await SetWinnerSideAsync(finalWinnerSide.Value, $"Victoire {winnerWins} rounds à {loserWins}");
            }
            else
            {
                match.WinnerSide = null;
                match.Winner = "Égalité (Match nul)";
                match.IsFinished = true;
                match.DecisionReason = $"Égalité {redWins}-{blueWins} à l'issue de {match.CurrentRound} round(s)";
                StatusChanged?.Invoke(this, $"⚖️ [MATCH NUL] Match #{match.Id} : {match.DecisionReason}");
                WinnerDeclared?.Invoke(this, (match, match.Winner, match.DecisionReason));
                _ = BroadcastMatchResultAsync(match);
            }

            await StopTimerAsync();
            RoundChanged?.Invoke(this, (match, match.CurrentRound));
            return true;
        }

        // Passage au round suivant
        match.CurrentRound++;

        // Réinitialiser les scores des juges pour le nouveau round
        ResetRoundScores();

        // Réinitialiser le chronomètre pour le nouveau round
        await StopTimerAsync();
        _timeLeft = _totalRoundTime;

        // Diffuser aux juges BLE
        if (_bluetoothServer.IsAdvertising)
        {
            await _bluetoothServer.SendToAllAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");
            await BroadcastMatchInfoAsync();
        }

        RoundChanged?.Invoke(this, (match, match.CurrentRound));
        StatusChanged?.Invoke(this, $"🔔 [ROUND {match.CurrentRound}] Début du round {match.CurrentRound}/{match.MaxRounds} pour le match #{match.Id} ({match.RedCompetitor} vs {match.BlueCompetitor}). Chronomètre réinitialisé à {TimeLeftDisplay}.");

        return true;
    }

    private void UpdateUnderlyingDrawRound(MatchItem match)
    {
        foreach (var cat in _loadedCategories)
        {
            if (cat.Draw?.DrawKnockouts != null)
            {
                var ko = cat.Draw.DrawKnockouts.FirstOrDefault(k => k.Id == match.Id);
                if (ko != null)
                {
                    ko.CurrentRound = match.CurrentRound;
                    ko.RoundResultsJson = JsonSerializer.Serialize(match.RoundResults, _jsonOptions);
                    return;
                }
            }

            if (cat.Draw?.DrawPools != null)
            {
                var pool = cat.Draw.DrawPools.FirstOrDefault(p => p.Id == match.Id);
                if (pool != null)
                {
                    pool.CurrentRound = match.CurrentRound;
                    pool.RoundResultsJson = JsonSerializer.Serialize(match.RoundResults, _jsonOptions);
                    return;
                }
            }
        }
    }

    private void UpdateUnderlyingDrawOrder(MatchItem match)
    {
        var allCats = CurrentCompetition.Categories.Concat(_loadedCategories).Distinct();
        foreach (var cat in allCats)
        {
            if (cat.Draw?.DrawOrders != null)
            {
                var order = cat.Draw.DrawOrders.FirstOrDefault(o => o.Id == match.Id);
                if (order != null)
                {
                    order.IsFinished = match.IsFinished;
                    order.CurrentRound = match.CurrentRound;
                    order.RoundResultsJson = match.FinalScore.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    return;
                }
            }
        }
    }

    #region Winner & Scoring Methods

    /// <summary>
    /// Récupère la liste des juges actifs (qui se sont connectés au moins une fois, ou actuellement connectés).
    /// Si aucun juge ne s'est connecté (mode hors-ligne / tests unitaires), renvoie tous les juges configurés.
    /// </summary>
    public IReadOnlyList<JudgeModel> GetActiveJudges()
    {
        var connected = Judges.Where(j => j.IsConnected || j.HasConnected).ToList();
        return connected.Count > 0 ? connected : Judges.ToList();
    }

    /// <summary>
    /// Évalue la situation actuelle des scores pour le match Sanda actif.
    /// Renvoie les votes des juges, le total des points et une synthèse.
    /// </summary>
    public (string Leader, int RedVotes, int BlueVotes, int TotalRedPts, int TotalBluePts, string Summary) EvaluateSandaScores()
    {
        var activeJudges = GetActiveJudges();
        int redVotes = 0;
        int blueVotes = 0;
        int drawVotes = 0;

        foreach (var j in activeJudges)
        {
            if (j.RedPoints > j.BluePoints) redVotes++;
            else if (j.BluePoints > j.RedPoints) blueVotes++;
            else drawVotes++;
        }

        int totalRed = (int)activeJudges.Sum(j => j.RedPoints);
        int totalBlue = (int)activeJudges.Sum(j => j.BluePoints);

        string leader;
        string reason;

        if (redVotes > blueVotes)
        {
            leader = "Rouge";
            reason = $"Majorité des juges ({redVotes} vs {blueVotes}) - Total points : {totalRed}-{totalBlue}";
        }
        else if (blueVotes > redVotes)
        {
            leader = "Bleu";
            reason = $"Majorité des juges ({blueVotes} vs {redVotes}) - Total points : {totalBlue}-{totalRed}";
        }
        else
        {
            // Égalité des juges : départage au total des points
            if (totalRed > totalBlue)
            {
                leader = "Rouge";
                reason = $"Égalité des juges ({redVotes}-{blueVotes}), départage aux points ({totalRed}-{totalBlue})";
            }
            else if (totalBlue > totalRed)
            {
                leader = "Bleu";
                reason = $"Égalité des juges ({blueVotes}-{redVotes}), départage aux points ({totalBlue}-{totalRed})";
            }
            else
            {
                leader = "Égalité";
                reason = $"Égalité parfaite ({redVotes}-{blueVotes} juges, {totalRed}-{totalBlue} points)";
            }
        }

        return (leader, redVotes, blueVotes, totalRed, totalBlue, reason);
    }

    /// <summary>
    /// Calcule la note finale pour une performance Taolu selon les groupes de juges (A, B, C).
    /// </summary>
    public (double TotalScore, double ScoreA, double ScoreB, double ScoreC, string Summary) EvaluateTaoluScores()
    {
        var activeJudges = GetActiveJudges();

        var groupA = activeJudges.Where(j => string.Equals(j.Group, "A", StringComparison.OrdinalIgnoreCase)).ToList();
        var groupB = activeJudges.Where(j => string.Equals(j.Group, "B", StringComparison.OrdinalIgnoreCase)).ToList();
        var groupC = activeJudges.Where(j => string.Equals(j.Group, "C", StringComparison.OrdinalIgnoreCase)).ToList();

        if (groupA.Count == 0 && groupB.Count == 0 && groupC.Count == 0 && activeJudges.Count > 0)
        {
            double avg = Math.Round(activeJudges.Average(j => j.Score), 3);
            return (avg, 0, 0, 0, $"Note finale : {avg:F2}");
        }

        bool hasConnectedAny = Judges.Any(j => j.IsConnected || j.HasConnected);

        double scoreA = groupA.Count > 0 ? groupA.Average(j => j.Score) : (hasConnectedAny ? 0.0 : Constants.DEFAULT_SCORE_GROUP_A);
        double scoreB = groupB.Count > 0 ? groupB.Average(j => j.Score) : (hasConnectedAny ? 0.0 : Constants.DEFAULT_SCORE_GROUP_B);
        double scoreC = groupC.Count > 0 ? groupC.Average(j => j.Score) : (hasConnectedAny ? 0.0 : Constants.DEFAUTL_SCORE_GROUP_C);

        double total = Math.Round(scoreA + scoreB + scoreC, 3);
        string summary = $"Note finale : {total:F2} (Qualité A: {scoreA:F2} | Perf B: {scoreB:F2} | Diff C: {scoreC:F2})";

        return (total, scoreA, scoreB, scoreC, summary);
    }

    /// <summary>
    /// Définit le camp vainqueur (Rouge ou Bleu) pour le match en cours.
    /// </summary>
    public Task<bool> SetWinnerSideAsync(Constants.Side side, string? customReason = null)
    {
        var match = CurrentMatch;
        if (match == null)
        {
            StatusChanged?.Invoke(this, "[ERREUR] Aucun match actif pour désigner un vainqueur.");
            return Task.FromResult(false);
        }

        match.WinnerSide = side;
        match.Winner = (side == Constants.Side.Red) ? match.RedCompetitor : match.BlueCompetitor;

        var activeJudges = GetActiveJudges();
        int judgesRed = (int)activeJudges.Sum(j => j.RedPoints);
        int judgesBlue = (int)activeJudges.Sum(j => j.BluePoints);
        if (judgesRed > 0 || judgesBlue > 0 || (match.RedScore == 0 && match.BlueScore == 0))
        {
            match.RedScore = judgesRed;
            match.BlueScore = judgesBlue;
        }

        match.IsFinished = true;
        match.DecisionReason = !string.IsNullOrWhiteSpace(customReason)
            ? customReason
            : (side == Constants.Side.Red ? $"Victoire Coin Rouge ({match.RedScore}-{match.BlueScore})" : $"Victoire Coin Bleu ({match.BlueScore}-{match.RedScore})");

        PropagateWinnerToNextMatch(match);

        StatusChanged?.Invoke(this, $"🏆 [VAINQUEUR DÉCLARÉ] Match #{match.Id} : {match.Winner} ({side}) - {match.DecisionReason}");
        WinnerDeclared?.Invoke(this, (match, match.Winner, match.DecisionReason));
        _ = BroadcastMatchResultAsync(match);

        return Task.FromResult(true);
    }

    /// <summary>
    /// Calcule et valide automatiquement le vainqueur selon les scores des juges.
    /// En Sanda : décision par majorité des juges ou départage aux points.
    /// En Taolu : calcule la note totale de l'athlète.
    /// </summary>
    public Task<bool> SetAutoWinnerAsync()
    {
        var match = CurrentMatch;
        if (match == null)
        {
            StatusChanged?.Invoke(this, "[ERREUR] Aucun match actif.");
            return Task.FromResult(false);
        }

        if (CurrentCompetitionType == CompetitionType.Taolu || match.Type == RoundType.Order)
        {
            return SetTaoluScoreAsync();
        }

        var eval = EvaluateSandaScores();
        if (eval.Leader == "Rouge")
        {
            return SetWinnerSideAsync(Constants.Side.Red, eval.Summary);
        }
        else if (eval.Leader == "Bleu")
        {
            return SetWinnerSideAsync(Constants.Side.Blue, eval.Summary);
        }
        else
        {
            // Match nul
            match.WinnerSide = null;
            match.Winner = "Égalité (Match nul)";
            match.RedScore = eval.TotalRedPts;
            match.BlueScore = eval.TotalBluePts;
            match.IsFinished = true;
            match.DecisionReason = eval.Summary;

            StatusChanged?.Invoke(this, $"⚖️ [MATCH NUL] Match #{match.Id} : {eval.Summary}");
            WinnerDeclared?.Invoke(this, (match, match.Winner, eval.Summary));
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Valide la note finale pour un match de Taolu.
    /// </summary>
    public Task<bool> SetTaoluScoreAsync(double? manualScore = null, string? customReason = null)
    {
        var match = CurrentMatch;
        if (match == null)
        {
            StatusChanged?.Invoke(this, "[ERREUR] Aucun match actif.");
            return Task.FromResult(false);
        }

        var eval = EvaluateTaoluScores();
        double finalScore = manualScore ?? eval.TotalScore;
        string reason = customReason ?? (manualScore.HasValue ? $"Note manuelle : {finalScore:F2}" : eval.Summary);

        var activeJudges = GetActiveJudges();

        // Capturer les notes individuelles par juge connecté uniquement
        match.JudgeScores.Clear();
        match.JudgeScoresByJudge.Clear();
        foreach (var j in activeJudges)
        {
            match.JudgeScores.Add(j.Score);
            if (!string.IsNullOrWhiteSpace(j.Name))
                match.JudgeScoresByJudge[j.Name] = j.Score;
        }

        match.Winner = match.RedCompetitor; // En Taolu individuel, le compétiteur passe
        match.WinnerSide = null;
        match.FinalScore = finalScore;
        match.IsFinished = true;
        match.DecisionReason = reason;

        UpdateUnderlyingDrawOrder(match);

        // Enregistrer la note de chaque juge connecté dans l'historique si non déjà loguée
        foreach (var j in activeJudges)
        {
            bool hasLog = _judgeScoreHistory.Any(h => h.MatchId == match.Id && h.JudgeName == j.Name);
            if (!hasLog)
            {
                string desc = $"Note finale du juge : {j.Score:F2}";
                RecordJudgeScore(j, null, j.Score, j.Score, null, "Score Final", desc, match.CurrentRound);
            }
        }

        PropagateWinnerToNextMatch(match);

        StatusChanged?.Invoke(this, $"🏆 [SCORE TAOLU VALIDÉ] Match #{match.Id} - {match.RedCompetitor} : {reason}");
        WinnerDeclared?.Invoke(this, (match, match.RedCompetitor, reason));
        _ = BroadcastMatchResultAsync(match);

        return Task.FromResult(true);
    }

    /// <summary>
    /// Méthode polyvalente pour définir le vainqueur à partir d'une chaîne ("red", "blue", "auto", "draw", etc.).
    /// </summary>
    public async Task<bool> SetWinnerAsync(string target, string? customReason = null)
    {
        if (string.IsNullOrWhiteSpace(target))
            return await SetAutoWinnerAsync();

        string t = target.Trim().ToLowerInvariant();
        if (t is "r" or "red" or "rouge" or "1")
        {
            return await SetWinnerSideAsync(Constants.Side.Red, customReason);
        }
        else if (t is "b" or "blue" or "bleu" or "2")
        {
            return await SetWinnerSideAsync(Constants.Side.Blue, customReason);
        }
        else if (t is "a" or "auto" or "points" or "calc")
        {
            return await SetAutoWinnerAsync();
        }
        else if (t is "draw" or "nul" or "egalite" or "égalité" or "d")
        {
            var match = CurrentMatch;
            if (match == null) return false;
            match.WinnerSide = null;
            match.Winner = "Égalité (Match nul)";
            match.IsFinished = true;
            match.DecisionReason = customReason ?? "Décision de match nul";
            StatusChanged?.Invoke(this, $"⚖️ [MATCH NUL] Match #{match.Id}");
            WinnerDeclared?.Invoke(this, (match, match.Winner, match.DecisionReason));
            return true;
        }
        else if (double.TryParse(t, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedScore))
        {
            return await SetTaoluScoreAsync(parsedScore, customReason);
        }
        else
        {
            // Recherche par nom de compétiteur
            var match = CurrentMatch;
            if (match != null)
            {
                if (match.RedCompetitor.Contains(target, StringComparison.OrdinalIgnoreCase))
                    return await SetWinnerSideAsync(Constants.Side.Red, customReason);
                if (match.BlueCompetitor.Contains(target, StringComparison.OrdinalIgnoreCase))
                    return await SetWinnerSideAsync(Constants.Side.Blue, customReason);
            }

            StatusChanged?.Invoke(this, $"[ERREUR] Choix de vainqueur non reconnu : '{target}'. Utilisez 'red', 'blue', ou 'auto'.");
            return false;
        }
    }

    /// <summary>
    /// Réinitialise l'état de vainqueur du match en cours.
    /// </summary>
    public Task<bool> ClearWinnerAsync()
    {
        var match = CurrentMatch;
        if (match == null) return Task.FromResult(false);

        ClearWinnerFromNextMatch(match);

        match.Winner = null;
        match.WinnerSide = null;
        match.IsFinished = false;
        match.FinalScore = 0;
        match.DecisionReason = null;

        StatusChanged?.Invoke(this, $"Décision de vainqueur annulée pour le match #{match.Id}.");
        return Task.FromResult(true);
    }

    private void PropagateWinnerToNextMatch(MatchItem match)
    {
        if (string.IsNullOrWhiteSpace(match.Winner)) return;

        // Trouver le DrawKnockout correspondant au match courant pour récupérer directement les CompetitorModel
        var allCats = CurrentCompetition.Categories.Concat(_loadedCategories).Distinct();
        DrawKnockoutModel? currentK = null;
        foreach (var cat in allCats)
        {
            if (cat.Draw?.DrawKnockouts != null)
            {
                currentK = cat.Draw.DrawKnockouts.FirstOrDefault(k => k.Id == match.Id);
                if (currentK != null) break;
            }
        }

        CompetitorModel? winnerComp = match.WinnerSide == Constants.Side.Red
            ? currentK?.Competitor1
            : (match.WinnerSide == Constants.Side.Blue ? currentK?.Competitor2 : null);

        CompetitorModel? loserComp = match.WinnerSide == Constants.Side.Red
            ? currentK?.Competitor2
            : (match.WinnerSide == Constants.Side.Blue ? currentK?.Competitor1 : null);

        // Vainqueur -> NextMatchId
        if (match.NextMatchId.HasValue)
        {
            var nextMatch = _matches.FirstOrDefault(m => m.Id == match.NextMatchId.Value);
            if (nextMatch != null)
            {
                if (match.NextMatchSide == Constants.Side.Red)
                {
                    nextMatch.RedCompetitor = match.Winner;
                }
                else if (match.NextMatchSide == Constants.Side.Blue)
                {
                    nextMatch.BlueCompetitor = match.Winner;
                }

                string stageInfo = !string.IsNullOrWhiteSpace(nextMatch.StageName) ? nextMatch.StageName : $"Match #{nextMatch.Id}";
                StatusChanged?.Invoke(this, $"➡️ {match.Winner} qualifié pour {stageInfo} (Coin {match.NextMatchSide})");

                UpdateDrawKnockoutCompetitor(nextMatch.Id, match.NextMatchSide, match.Winner, nextMatch.Category, winnerComp);
            }
        }

        // Perdant -> NextLoserMatchId (Petite finale pour la 3e place)
        if (match.NextLoserMatchId.HasValue)
        {
            var loserMatch = _matches.FirstOrDefault(m => m.Id == match.NextLoserMatchId.Value);
            if (loserMatch != null)
            {
                string? loser = string.Equals(match.RedCompetitor?.Trim(), match.Winner?.Trim(), StringComparison.OrdinalIgnoreCase)
                    ? match.BlueCompetitor?.Trim()
                    : match.RedCompetitor?.Trim();

                if (!string.IsNullOrWhiteSpace(loser) && IsValidCompetitorName(loser))
                {
                    if (match.NextLoserMatchSide == Constants.Side.Red)
                    {
                        loserMatch.RedCompetitor = loser;
                    }
                    else if (match.NextLoserMatchSide == Constants.Side.Blue)
                    {
                        loserMatch.BlueCompetitor = loser;
                    }

                    string stageInfo = !string.IsNullOrWhiteSpace(loserMatch.StageName) ? loserMatch.StageName : $"Match #{loserMatch.Id}";
                    StatusChanged?.Invoke(this, $"🥉 {loser} qualifié pour {stageInfo} (Coin {match.NextLoserMatchSide})");

                    UpdateDrawKnockoutCompetitor(loserMatch.Id, match.NextLoserMatchSide, loser, loserMatch.Category, loserComp);
                }
            }
        }
    }

    private void ClearWinnerFromNextMatch(MatchItem match)
    {
        string stageName = !string.IsNullOrWhiteSpace(match.StageName) ? match.StageName : $"Match #{match.Id}";

        // Nettoyage vers le match du vainqueur
        if (match.NextMatchId.HasValue)
        {
            var nextMatch = _matches.FirstOrDefault(m => m.Id == match.NextMatchId.Value);
            if (nextMatch != null)
            {
                string placeholder = $"Vainqueur {stageName}";

                if (match.NextMatchSide == Constants.Side.Red)
                {
                    nextMatch.RedCompetitor = placeholder;
                }
                else if (match.NextMatchSide == Constants.Side.Blue)
                {
                    nextMatch.BlueCompetitor = placeholder;
                }

                ClearDrawKnockoutCompetitor(nextMatch.Id, match.NextMatchSide);

                // Si le match suivant était déjà validé, réinitialiser en cascade
                if (nextMatch.IsFinished)
                {
                    ClearWinnerFromNextMatch(nextMatch);
                    nextMatch.IsFinished = false;
                    nextMatch.Winner = null;
                    nextMatch.WinnerSide = null;
                    nextMatch.RedScore = 0;
                    nextMatch.BlueScore = 0;
                    nextMatch.DecisionReason = null;
                }
            }
        }

        // Nettoyage vers le match du perdant (Petite finale)
        if (match.NextLoserMatchId.HasValue)
        {
            var loserMatch = _matches.FirstOrDefault(m => m.Id == match.NextLoserMatchId.Value);
            if (loserMatch != null)
            {
                string placeholder = $"Perdant {stageName}";

                if (match.NextLoserMatchSide == Constants.Side.Red)
                {
                    loserMatch.RedCompetitor = placeholder;
                }
                else if (match.NextLoserMatchSide == Constants.Side.Blue)
                {
                    loserMatch.BlueCompetitor = placeholder;
                }

                ClearDrawKnockoutCompetitor(loserMatch.Id, match.NextLoserMatchSide);

                if (loserMatch.IsFinished)
                {
                    ClearWinnerFromNextMatch(loserMatch);
                    loserMatch.IsFinished = false;
                    loserMatch.Winner = null;
                    loserMatch.WinnerSide = null;
                    loserMatch.RedScore = 0;
                    loserMatch.BlueScore = 0;
                    loserMatch.DecisionReason = null;
                }
            }
        }
    }

    public CompetitorModel? FindCompetitor(string? name, CategoryModel? preferredCat = null)
    {
        if (string.IsNullOrWhiteSpace(name) || !IsValidCompetitorName(name))
            return null;

        string search = name.Trim();

        // 1. Chercher dans la catégorie préférée
        if (preferredCat?.Competitors != null)
        {
            foreach (var cc in preferredCat.Competitors)
            {
                var c = cc.Competitor;
                if (c == null) continue;
                if (MatchCompetitorName(c, search))
                    return c;
            }
        }

        // 2. Chercher dans toutes les catégories chargées
        var allCats = CurrentCompetition.Categories.Concat(_loadedCategories).Distinct();
        foreach (var cat in allCats)
        {
            if (cat.Competitors != null)
            {
                foreach (var cc in cat.Competitors)
                {
                    var c = cc.Competitor;
                    if (c == null) continue;
                    if (MatchCompetitorName(c, search))
                        return c;
                }
            }

            if (cat.Draw?.DrawKnockouts != null)
            {
                foreach (var k in cat.Draw.DrawKnockouts)
                {
                    if (k.Competitor1 != null && MatchCompetitorName(k.Competitor1, search))
                        return k.Competitor1;
                    if (k.Competitor2 != null && MatchCompetitorName(k.Competitor2, search))
                        return k.Competitor2;
                }
            }
        }

        return null;
    }

    private static bool MatchCompetitorName(CompetitorModel? c, string search)
    {
        if (c == null) return false;

        string full1 = $"{c.LastName} {c.FirstName}".Trim();
        string full2 = $"{c.FirstName} {c.LastName}".Trim();

        if (string.Equals(full1, search, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(full2, search, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(c.Club))
        {
            string withClub1 = $"{full1} ({c.Club})".Trim();
            string withClub2 = $"{full2} ({c.Club})".Trim();
            if (string.Equals(withClub1, search, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(withClub2, search, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(c.LastName) && search.Contains(c.LastName, StringComparison.OrdinalIgnoreCase) &&
            (!string.IsNullOrWhiteSpace(c.FirstName) && search.Contains(c.FirstName, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!string.IsNullOrWhiteSpace(c.LastName) && string.Equals(c.LastName, search, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(c.FirstName) && string.Equals(c.FirstName, search, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public CompetitorModel ResolveOrCreateCompetitor(string name, CategoryModel? preferredCat = null, int fallbackId = 1)
    {
        var found = FindCompetitor(name, preferredCat);
        if (found != null) return found;

        string trimmed = name.Trim();
        string club = "";
        int parenIdx = trimmed.LastIndexOf('(');
        if (parenIdx > 0 && trimmed.EndsWith(')'))
        {
            club = trimmed.Substring(parenIdx + 1, trimmed.Length - parenIdx - 2).Trim();
            trimmed = trimmed.Substring(0, parenIdx).Trim();
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string lastName = parts.Length > 0 ? parts[0] : trimmed;
        string firstName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";

        var comp = new CompetitorModel
        {
            Id = fallbackId,
            LastName = lastName,
            FirstName = firstName,
            Club = club
        };

        if (preferredCat != null)
        {
            if (preferredCat.Competitors == null)
                preferredCat.Competitors = new List<CompetitorCategoryModel>();

            if (!preferredCat.Competitors.Any(cc => cc.CompetitorId == comp.Id || MatchCompetitorName(cc.Competitor, name)))
            {
                preferredCat.Competitors.Add(new CompetitorCategoryModel
                {
                    CategoryId = preferredCat.Id,
                    CompetitorId = comp.Id,
                    Competitor = comp
                });
            }
        }

        return comp;
    }

    private void UpdateDrawKnockoutCompetitor(int matchId, Constants.Side? side, string competitorName, string? categoryName, CompetitorModel? knownCompetitor = null)
    {
        var allCats = CurrentCompetition.Categories.Concat(_loadedCategories).Distinct();
        CategoryModel? targetCat = null;
        DrawKnockoutModel? targetK = null;

        foreach (var cat in allCats)
        {
            if (cat.Draw?.DrawKnockouts != null)
            {
                targetK = cat.Draw.DrawKnockouts.FirstOrDefault(k => k.Id == matchId);
                if (targetK != null)
                {
                    targetCat = cat;
                    break;
                }
            }
        }

        if (targetK != null)
        {
            int fallbackId = matchId * 2 - (side == Constants.Side.Red ? 1 : 0);
            var comp = knownCompetitor ?? ResolveOrCreateCompetitor(competitorName, targetCat, fallbackId);
            if (side == Constants.Side.Red)
            {
                targetK.Competitor1Id = comp.Id;
                targetK.Competitor1 = comp;
            }
            else if (side == Constants.Side.Blue)
            {
                targetK.Competitor2Id = comp.Id;
                targetK.Competitor2 = comp;
            }
        }
    }

    private void ClearDrawKnockoutCompetitor(int matchId, Constants.Side? side)
    {
        var allCats = CurrentCompetition.Categories.Concat(_loadedCategories).Distinct();
        foreach (var cat in allCats)
        {
            if (cat.Draw?.DrawKnockouts != null)
            {
                var targetK = cat.Draw.DrawKnockouts.FirstOrDefault(k => k.Id == matchId);
                if (targetK != null)
                {
                    if (side == Constants.Side.Red)
                    {
                        targetK.Competitor1Id = null;
                        targetK.Competitor1 = null;
                    }
                    else if (side == Constants.Side.Blue)
                    {
                        targetK.Competitor2Id = null;
                        targetK.Competitor2 = null;
                    }
                    break;
                }
            }
        }
    }

    #endregion

    #region Timer Methods
    public async Task StartTimerAsync()
    {
        if (IsTimerRunning) return;

        IsTimerRunning = true;
        _timer?.Dispose();
        int intervalMs = (int)(1000.0 / Math.Max(1, _timeSpeedMultiplier));
        _timer = new System.Threading.Timer(OnTimerCallback, null, intervalMs, intervalMs);

        StatusChanged?.Invoke(this, $"Chronomètre DÉMARRÉ ({TimeLeftDisplay}, Vitesse: x{_timeSpeedMultiplier})");
        await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_START);
    }

    public async Task PauseTimerAsync()
    {
        if (!IsTimerRunning) return;

        IsTimerRunning = false;
        _timer?.Dispose();
        _timer = null;

        StatusChanged?.Invoke(this, $"Chronomètre EN PAUSE ({TimeLeftDisplay})");
        await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_PAUSE);
    }

    public async Task StopTimerAsync()
    {
        IsTimerRunning = false;
        _timer?.Dispose();
        _timer = null;
        _timeLeft = TimeSpan.Zero;

        StatusChanged?.Invoke(this, "Chronomètre ARRÊTÉ.");
        await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_STOP);
    }

    public async Task SetTimerDurationAsync(TimeSpan duration)
    {
        _totalRoundTime = duration;
        _timeLeft = duration;
        StatusChanged?.Invoke(this, $"Durée du chronomètre définie à : {TimeLeftDisplay}");
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");
    }

    public async Task SetTimeSpeedMultiplierAsync(int multiplier)
    {
        if (multiplier is not (1 or 2 or 4 or 6 or 8)) return;

        _timeSpeedMultiplier = multiplier;
        StatusChanged?.Invoke(this, $"Vitesse du chronomètre réglée à x{_timeSpeedMultiplier}");

        if (IsTimerRunning && _timer != null)
        {
            int intervalMs = (int)(1000.0 / _timeSpeedMultiplier);
            _timer.Change(intervalMs, intervalMs);
        }
    }

    public void ToggleJudgeBot()
    {
        IsJudgeBotEnabled = !IsJudgeBotEnabled;
        StatusChanged?.Invoke(this, $"🤖 Robot simulateur de juge : {(IsJudgeBotEnabled ? "ACTIVÉ" : "DÉSACTIVÉ")} (Stratégie: {JudgeBotStrategy})");
    }

    public void SetJudgeBot(bool enabled, string? strategy = null)
    {
        IsJudgeBotEnabled = enabled;
        if (!string.IsNullOrWhiteSpace(strategy))
        {
            JudgeBotStrategy = strategy.Trim().ToLowerInvariant();
        }
        StatusChanged?.Invoke(this, $"🤖 Robot simulateur de juge : {(IsJudgeBotEnabled ? "ACTIVÉ" : "DÉSACTIVÉ")} (Stratégie: {JudgeBotStrategy})");
    }

    public async Task ProcessJudgeBotTickAsync()
    {
        if (!IsJudgeBotEnabled || Judges.Count == 0 || CurrentMatch == null) return;

        // 40% de chance d'action par seconde écoulée
        if (Random.Shared.Next(100) >= 40) return;

        var availableJudges = Judges.ToList();
        if (availableJudges.Count == 0) return;

        var judge = availableJudges[Random.Shared.Next(availableJudges.Count)];

        bool isRed;
        switch (JudgeBotStrategy.ToLowerInvariant())
        {
            case "red":
                isRed = Random.Shared.Next(100) < 75;
                break;
            case "blue":
                isRed = Random.Shared.Next(100) >= 75;
                break;
            case "random":
            case "balanced":
            default:
                isRed = Random.Shared.Next(2) == 0;
                break;
        }

        Constants.Side side = isRed ? Constants.Side.Red : Constants.Side.Blue;
        double points = 1.0;

        if (isRed)
        {
            judge.RedPoints += points;
            string desc = $"🤖 [ROBOT {judge.Name}] +{points:F0} Rouge (Total: {judge.RedPoints:F0})";
            RecordJudgeScore(judge, side, points, judge.RedPoints, null, "Point Rouge", desc, CurrentMatch.CurrentRound);
            ScoreUpdated?.Invoke(this, (judge, desc));
        }
        else
        {
            judge.BluePoints += points;
            string desc = $"🤖 [ROBOT {judge.Name}] +{points:F0} Bleu (Total: {judge.BluePoints:F0})";
            RecordJudgeScore(judge, side, points, judge.BluePoints, null, "Point Bleu", desc, CurrentMatch.CurrentRound);
            ScoreUpdated?.Invoke(this, (judge, desc));
        }

        // 30% de chance qu'un second juge valide le point
        if (availableJudges.Count > 1 && Random.Shared.Next(100) < 30)
        {
            var otherJudges = availableJudges.Where(j => j != judge).ToList();
            if (otherJudges.Count > 0)
            {
                var secondJudge = otherJudges[Random.Shared.Next(otherJudges.Count)];
                if (isRed)
                {
                    secondJudge.RedPoints += points;
                    string desc = $"🤖 [ROBOT {secondJudge.Name}] +{points:F0} Rouge (Total: {secondJudge.RedPoints:F0})";
                    RecordJudgeScore(secondJudge, side, points, secondJudge.RedPoints, null, "Point Rouge", desc, CurrentMatch.CurrentRound);
                    ScoreUpdated?.Invoke(this, (secondJudge, desc));
                }
                else
                {
                    secondJudge.BluePoints += points;
                    string desc = $"🤖 [ROBOT {secondJudge.Name}] +{points:F0} Bleu (Total: {secondJudge.BluePoints:F0})";
                    RecordJudgeScore(secondJudge, side, points, secondJudge.BluePoints, null, "Point Bleu", desc, CurrentMatch.CurrentRound);
                    ScoreUpdated?.Invoke(this, (secondJudge, desc));
                }
            }
        }
    }

    private async void OnTimerCallback(object? state)
    {
        if (_timeLeft.TotalSeconds > 0)
        {
            _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(1));
            TimerTick?.Invoke(this, TimeLeftDisplay);

            if (IsJudgeBotEnabled)
            {
                await ProcessJudgeBotTickAsync();
            }
        }
        else
        {
            await StopTimerAsync();
        }
    }
    #endregion

    #region Protocol & Broadcasts
    /// <summary>
    /// Retourne une copie de la compétition contenant uniquement les informations globales,
    /// les catégories et les compétiteurs, sans les matchs des tirages.
    /// </summary>
    public CompetitionModel GetCompetitionWithoutMatches()
    {
        var comp = new CompetitionModel
        {
            Id = CurrentCompetition.Id,
            Name = CurrentCompetition.Name,
            Date = CurrentCompetition.Date,
            Type = CurrentCompetition.Type,
            Categories = new List<CategoryModel>()
        };

        foreach (var cat in CurrentCompetition.Categories)
        {
            var newCat = new CategoryModel
            {
                Id = cat.Id,
                Type = cat.Type,
                Genre = cat.Genre,
                WeightMin = cat.WeightMin,
                WeightMax = cat.WeightMax,
                AgeRangeId = cat.AgeRangeId,
                AgeRange = cat.AgeRange,
                RoundType = cat.RoundType,
                HasBronzeMatch = cat.HasBronzeMatch,
                CompetitionId = cat.CompetitionId,
                Competitors = cat.Competitors != null
                    ? new List<CompetitorCategoryModel>(cat.Competitors)
                    : new List<CompetitorCategoryModel>()
            };

            if (cat.Draw != null)
            {
                newCat.Draw = new DrawModel
                {
                    Id = cat.Draw.Id,
                    CategoryId = cat.Id,
                    DrawKnockouts = new List<DrawKnockoutModel>(),
                    DrawPools = new List<DrawPoolsModel>(),
                    DrawOrders = new List<DrawOrderModel>()
                };
            }

            comp.Categories.Add(newCat);
        }

        return comp;
    }

    /// <summary>
    /// Construit le modèle de données complet d'un match pour le téléchargement au fur et à mesure par le juge.
    /// </summary>
    public MatchInfoData CreateMatchInfoData(MatchItem match)
    {
        var data = new MatchInfoData
        {
            Id = match.Id,
            Type = match.Type,
            Round = match.CurrentRound,
            CategoryName = match.Category,
            StageName = match.StageName
        };

        CategoryModel? matchedCat = null;
        var allCats = CurrentCompetition.Categories.Concat(_loadedCategories).Distinct().ToList();
        foreach (var cat in allCats)
        {
            if (string.Equals(cat.Name, match.Category, StringComparison.OrdinalIgnoreCase) ||
                (cat.Draw != null && (
                    (cat.Draw.DrawKnockouts != null && cat.Draw.DrawKnockouts.Any(k => k.Id == match.Id)) ||
                    (cat.Draw.DrawPools != null && cat.Draw.DrawPools.Any(p => p.Id == match.Id)) ||
                    (cat.Draw.DrawOrders != null && cat.Draw.DrawOrders.Any(o => o.Id == match.Id))
                )))
            {
                matchedCat = cat;
                data.CategoryId = cat.Id;
                data.CategoryName = cat.Name;
                break;
            }
        }

        if (matchedCat != null && matchedCat.Draw != null)
        {
            if (match.Type == RoundType.Knockouts && matchedCat.Draw.DrawKnockouts != null)
            {
                var k = matchedCat.Draw.DrawKnockouts.FirstOrDefault(x => x.Id == match.Id);
                if (k != null)
                {
                    data.DrawKnockout = k;
                    data.Competitor1 = k.Competitor1;
                    data.Competitor2 = k.Competitor2;
                }
            }
            else if (match.Type == RoundType.Pools && matchedCat.Draw.DrawPools != null)
            {
                var p = matchedCat.Draw.DrawPools.FirstOrDefault(x => x.Id == match.Id);
                if (p != null)
                {
                    data.DrawPool = p;
                    data.Competitor1 = p.Competitor1;
                    data.Competitor2 = p.Competitor2;
                }
            }
            else if (match.Type == RoundType.Order && matchedCat.Draw.DrawOrders != null)
            {
                var o = matchedCat.Draw.DrawOrders.FirstOrDefault(x => x.Id == match.Id);
                if (o != null)
                {
                    data.DrawOrder = o;
                    data.Competitor1 = o.Competitor;
                }
            }
        }

        // Si Competitor1 est manquant mais qu'un compétiteur valide est renseigné dans le match
        if (data.Competitor1 == null && IsValidCompetitorName(match.RedCompetitor))
        {
            int c1Id = data.DrawKnockout?.Competitor1Id ?? (match.Id * 2 - 1);
            var c1 = ResolveOrCreateCompetitor(match.RedCompetitor, matchedCat, c1Id);
            data.Competitor1 = c1;
            if (data.DrawKnockout != null)
            {
                data.DrawKnockout.Competitor1 = c1;
                data.DrawKnockout.Competitor1Id = c1.Id;
            }
            if (data.DrawPool != null)
            {
                data.DrawPool.Competitor1 = c1;
                data.DrawPool.Competitor1Id = c1.Id;
            }
            if (data.DrawOrder != null)
            {
                data.DrawOrder.Competitor = c1;
                data.DrawOrder.CompetitorId = c1.Id;
            }
        }

        // Si Competitor2 est manquant mais qu'un compétiteur valide est renseigné dans le match
        if (data.Competitor2 == null && IsValidCompetitorName(match.BlueCompetitor))
        {
            int c2Id = data.DrawKnockout?.Competitor2Id ?? (match.Id * 2);
            var c2 = ResolveOrCreateCompetitor(match.BlueCompetitor, matchedCat, c2Id);
            data.Competitor2 = c2;
            if (data.DrawKnockout != null)
            {
                data.DrawKnockout.Competitor2 = c2;
                data.DrawKnockout.Competitor2Id = c2.Id;
            }
            if (data.DrawPool != null)
            {
                data.DrawPool.Competitor2 = c2;
                data.DrawPool.Competitor2Id = c2.Id;
            }
        }

        int drawId = matchedCat?.Draw?.Id ?? (matchedCat?.Id ?? 1);

        if (data.DrawKnockout == null && data.DrawPool == null && data.DrawOrder == null)
        {
            if (match.Type == RoundType.Order)
            {
                var c = data.Competitor1 ?? (IsValidCompetitorName(match.RedCompetitor) ? ResolveOrCreateCompetitor(match.RedCompetitor, matchedCat, match.Id) : null);
                data.Competitor1 = c;
                data.DrawOrder = new DrawOrderModel
                {
                    Id = match.Id,
                    Order = match.Id,
                    GlobalOrder = match.Id,
                    DrawId = drawId,
                    CompetitorId = c?.Id ?? 0,
                    Competitor = c,
                    CurrentRound = match.CurrentRound
                };
            }
            else if (match.Type == RoundType.Pools)
            {
                var c1 = data.Competitor1 ?? (IsValidCompetitorName(match.RedCompetitor) ? ResolveOrCreateCompetitor(match.RedCompetitor, matchedCat, match.Id * 2 - 1) : null);
                var c2 = data.Competitor2 ?? (IsValidCompetitorName(match.BlueCompetitor) ? ResolveOrCreateCompetitor(match.BlueCompetitor, matchedCat, match.Id * 2) : null);
                data.Competitor1 = c1;
                data.Competitor2 = c2;
                data.DrawPool = new DrawPoolsModel
                {
                    Id = match.Id,
                    Order = match.Id,
                    GlobalOrder = match.Id,
                    DrawId = drawId,
                    Competitor1Id = c1?.Id,
                    Competitor1 = c1,
                    Competitor2Id = c2?.Id,
                    Competitor2 = c2,
                    CurrentRound = match.CurrentRound
                };
            }
            else
            {
                var c1 = data.Competitor1 ?? (IsValidCompetitorName(match.RedCompetitor) ? ResolveOrCreateCompetitor(match.RedCompetitor, matchedCat, match.Id * 2 - 1) : null);
                var c2 = data.Competitor2 ?? (IsValidCompetitorName(match.BlueCompetitor) ? ResolveOrCreateCompetitor(match.BlueCompetitor, matchedCat, match.Id * 2) : null);
                data.Competitor1 = c1;
                data.Competitor2 = c2;
                data.DrawKnockout = new DrawKnockoutModel
                {
                    Id = match.Id,
                    Order = match.Id,
                    GlobalOrder = match.Id,
                    DrawId = drawId,
                    Competitor1Id = c1?.Id,
                    Competitor1 = c1,
                    Competitor2Id = c2?.Id,
                    Competitor2 = c2,
                    CurrentRound = match.CurrentRound,
                    IsBronzeMatch = match.IsBronzeMatch
                };
            }
        }

        return data;
    }

    public async Task BroadcastMatchInfoAsync()
    {
        if (CurrentMatch == null)
        {
            await _bluetoothServer.SendToAllAsync($"{Constants.Message.MATCH_INFO}{{}}");
            return;
        }

        var data = CreateMatchInfoData(CurrentMatch);
        string json = JsonSerializer.Serialize(data, _jsonOptions);
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.MATCH_INFO}{json}");
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");
    }

    public async Task BroadcastCompetitionDataAsync()
    {
        if (!string.IsNullOrWhiteSpace(_bluetoothServer.DeviceName))
        {
            await _bluetoothServer.SendToAllAsync($"{Constants.Message.SERVER_NAME}{_bluetoothServer.DeviceName}");
        }

        var lightComp = GetCompetitionWithoutMatches();
        string json = JsonSerializer.Serialize(lightComp, _jsonOptions);
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.COMPETITION_DATA}{json}");
    }

    public async Task SendMatchInfoToClientAsync(string clientId)
    {
        if (CurrentMatch == null)
        {
            await _bluetoothServer.SendToClientAsync(clientId, $"{Constants.Message.MATCH_INFO}{{}}");
            return;
        }

        var data = CreateMatchInfoData(CurrentMatch);
        string json = JsonSerializer.Serialize(data, _jsonOptions);
        await _bluetoothServer.SendToClientAsync(clientId, $"{Constants.Message.MATCH_INFO}{json}");
        await _bluetoothServer.SendToClientAsync(clientId, $"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");
    }

    public async Task SendCompetitionDataToClientAsync(string clientId)
    {
        if (!string.IsNullOrWhiteSpace(_bluetoothServer.DeviceName))
        {
            await _bluetoothServer.SendToClientAsync(clientId, $"{Constants.Message.SERVER_NAME}{_bluetoothServer.DeviceName}");
        }

        var lightComp = GetCompetitionWithoutMatches();
        string json = JsonSerializer.Serialize(lightComp, _jsonOptions);
        await _bluetoothServer.SendToClientAsync(clientId, $"{Constants.Message.COMPETITION_DATA}{json}");
    }
    #endregion

    #region Incoming Bluetooth Message Handler
    private async void OnBluetoothMessageReceived(object? sender, (string ClientId, string Message) args)
    {
        string clientId = args.ClientId;
        string message = args.Message;

        try
        {
            StatusChanged?.Invoke(this, $"[Received] : '{message}'.");

            if (message.StartsWith(Constants.Message.JUDGE_POSITION))
            {
                var posStr = message.Substring(Constants.Message.JUDGE_POSITION.Length).Trim();

                // Support session ID pour instances multiples sur même PC (ex: "1#instA")
                string sessionKey = clientId;
                if (posStr.Contains('#'))
                {
                    var segs = posStr.Split('#', 2);
                    posStr = segs[0];
                    sessionKey = $"{clientId}#{segs[1]}";
                }

                JudgeModel? judge = null;

                var parts = posStr.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[1], out int num))
                {
                    judge = Judges.FirstOrDefault(j => string.Equals(j.Group, parts[0], StringComparison.OrdinalIgnoreCase) && j.Number == num);
                }
                else if (int.TryParse(posStr, out int position))
                {
                    judge = Judges.FirstOrDefault(j => string.IsNullOrEmpty(j.Group) && j.Number == position)
                         ?? Judges.FirstOrDefault(j => j.Number == position);
                }
                else
                {
                    judge = Judges.FirstOrDefault(j => string.Equals(j.Name, posStr, StringComparison.OrdinalIgnoreCase));
                }

                if (judge != null)
                {
                    // Dissocie l'ancien juge éventuel de cette session client
                    if (_clientJudgeMapping.TryGetValue(sessionKey, out var oldJudge) && oldJudge != judge)
                    {
                        oldJudge.IsConnected = false;
                    }

                    // Dissocie un autre client qui aurait eu ce même juge
                    var existing = _clientJudgeMapping.FirstOrDefault(x => x.Value == judge);
                    if (existing.Key != null && existing.Key != sessionKey)
                    {
                        _clientJudgeMapping.Remove(existing.Key);
                    }

                    judge.IsConnected = true;
                    _clientJudgeMapping[sessionKey] = judge;

                    StatusChanged?.Invoke(this, $"[JUGE ENREGISTRÉ] {judge.Name} (Position: {posStr}) connecté via [{sessionKey}].");
                    JudgeConnected?.Invoke(this, judge);

                    // Envoi immédiat du match en cours au juge enregistré
                    await SendMatchInfoToClientAsync(clientId);
                }
                else
                {
                    StatusChanged?.Invoke(this, $"[ATTENTION] Position juge '{posStr}' demandée par [{clientId}] mais introuvable dans la liste.");
                }
            }
            else if (message.StartsWith(Constants.Message.JUDGE_SCORE))
            {
                bool isTaolu = CurrentCompetition?.Type == CompetitionType.Taolu || CurrentMatch?.Type == RoundType.Order;
                if (!isTaolu && (!IsTimerRunning || _timeLeft.TotalSeconds <= 0))
                {
                    StatusChanged?.Invoke(this, $"[SCORE REFUSÉ] Note reçue de {clientId} alors que le chronomètre est inactif.");
                    return;
                }

                string json = message.Substring(Constants.Message.JUDGE_SCORE.Length);
                var scoreModel = JsonSerializer.Deserialize<TransfertScoreModel>(json, _jsonOptions);

                JudgeModel? judge = null;
                // Recherche directe par position spécifiée
                if (!string.IsNullOrEmpty(scoreModel?.JudgePosition))
                {
                    var parts = scoreModel.JudgePosition.Split('_');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int num))
                    {
                        judge = Judges.FirstOrDefault(j => string.Equals(j.Group, parts[0], StringComparison.OrdinalIgnoreCase) && j.Number == num);
                    }
                    else if (int.TryParse(scoreModel.JudgePosition, out int posNum))
                    {
                        judge = Judges.FirstOrDefault(j => string.IsNullOrEmpty(j.Group) && j.Number == posNum)
                             ?? Judges.FirstOrDefault(j => j.Number == posNum);
                    }
                    else
                    {
                        judge = Judges.FirstOrDefault(j => string.Equals(j.Name, scoreModel.JudgePosition, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (judge == null)
                {
                    _clientJudgeMapping.TryGetValue(clientId, out judge);
                }

                if (scoreModel != null && judge != null)
                {
                    if (scoreModel.Side == Constants.Side.Red)
                    {
                        judge.RedPoints += scoreModel.Score;
                        string desc = $"+{scoreModel.Score:F0} Rouge (Total: {judge.RedPoints:F0})";
                        RecordJudgeScore(judge, scoreModel.Side, scoreModel.Score, judge.RedPoints, null, "Point Rouge", desc, scoreModel.Round, relayToMonitors: false);
                        ScoreUpdated?.Invoke(this, (judge, desc));
                    }
                    else if (scoreModel.Side == Constants.Side.Blue)
                    {
                        judge.BluePoints += scoreModel.Score;
                        string desc = $"+{scoreModel.Score:F0} Bleu (Total: {judge.BluePoints:F0})";
                        RecordJudgeScore(judge, scoreModel.Side, scoreModel.Score, judge.BluePoints, null, "Point Bleu", desc, scoreModel.Round, relayToMonitors: false);
                        ScoreUpdated?.Invoke(this, (judge, desc));
                    }
                    else
                    {
                        judge.Score = scoreModel.Score;
                        if (scoreModel.Code != null)
                        {
                            judge.Codes.Insert(0, scoreModel.Code);
                            string desc = $"Déduction {scoreModel.Code.Code?.Code} (-{scoreModel.Code.Code?.Value:F1}) => Nouveau score: {judge.Score:F2}";
                            RecordJudgeScore(judge, null, -(scoreModel.Code.Code?.Value ?? 0), judge.Score, scoreModel.Code, "Déduction", desc, scoreModel.Round, relayToMonitors: false);
                            ScoreUpdated?.Invoke(this, (judge, desc));
                        }
                        else
                        {
                            string desc = $"Score fixé à {judge.Score:F2}";
                            RecordJudgeScore(judge, null, scoreModel.Score, judge.Score, null, "Score", desc, scoreModel.Round, relayToMonitors: false);
                            ScoreUpdated?.Invoke(this, (judge, desc));
                        }
                    }

                    // Relayer la note aux moniteurs enregistrés via SendToAllAsync
                    // (nécessaire lorsque Judge.Cli et Monitor.Cli partagent le même adaptateur BLE)
                    if (_monitorClients.Count > 0)
                    {
                        try { await _bluetoothServer.SendToAllAsync(message); }
                        catch { }
                    }
                }
            }
            else if (message == Constants.Message.GET_MATCH)
            {
                await SendMatchInfoToClientAsync(clientId);
            }
            else if (message == Constants.Message.GET_COMPETITION)
            {
                await SendCompetitionDataToClientAsync(clientId);
            }
            else if (message == Constants.Message.PING)
            {
                await _bluetoothServer.SendToClientAsync(clientId, Constants.Message.PONG);
            }
            else if (message.StartsWith(Constants.Message.JUDGE_DISCONNECT))
            {
                var payload = message.Substring(Constants.Message.JUDGE_DISCONNECT.Length).TrimStart(':');
                List<string> keysToRemove = new();

                if (!string.IsNullOrEmpty(payload))
                {
                    string posPart = payload;
                    string? instPart = null;
                    if (payload.Contains('#'))
                    {
                        var segs = payload.Split('#', 2);
                        posPart = segs[0];
                        instPart = segs[1];
                    }

                    if (!string.IsNullOrEmpty(instPart))
                    {
                        string targetKey = $"{clientId}#{instPart}";
                        if (_clientJudgeMapping.TryGetValue(targetKey, out var j))
                        {
                            j.IsConnected = false;
                            keysToRemove.Add(targetKey);
                            JudgeDisconnected?.Invoke(this, j);
                            StatusChanged?.Invoke(this, $"[DÉCONNEXION] Juge {j.Name} s'est déconnecté.");
                        }
                    }

                    if (keysToRemove.Count == 0 && !string.IsNullOrEmpty(posPart))
                    {
                        foreach (var kvp in _clientJudgeMapping.Where(k => k.Key == clientId || k.Key.StartsWith(clientId + "#")))
                        {
                            var j = kvp.Value;
                            if (j.Name == posPart || j.Number.ToString() == posPart || $"{j.Group}_{j.Number}" == posPart)
                            {
                                j.IsConnected = false;
                                keysToRemove.Add(kvp.Key);
                                JudgeDisconnected?.Invoke(this, j);
                                StatusChanged?.Invoke(this, $"[DÉCONNEXION] Juge {j.Name} s'est déconnecté.");
                            }
                        }
                    }
                }

                if (keysToRemove.Count == 0)
                {
                    foreach (var kvp in _clientJudgeMapping.Where(k => k.Key == clientId || k.Key.StartsWith(clientId + "#")))
                    {
                        kvp.Value.IsConnected = false;
                        keysToRemove.Add(kvp.Key);
                        JudgeDisconnected?.Invoke(this, kvp.Value);
                        StatusChanged?.Invoke(this, $"[DÉCONNEXION] Juge {kvp.Value.Name} s'est déconnecté.");
                    }
                }

                foreach (var k in keysToRemove)
                {
                    _clientJudgeMapping.Remove(k);
                }
            }
            else if (message.StartsWith(Constants.Message.MONITOR_REGISTER))
            {
                _monitorClients.Add(clientId);
                StatusChanged?.Invoke(this, $"[MONITOR] Client Monitor enregistré : [{clientId}] (total: {_monitorClients.Count})");
            }
            else if (message.StartsWith(Constants.Message.CORRECTION_WINNER))
            {
                var payload = message.Substring(Constants.Message.CORRECTION_WINNER.Length);
                var correction = JsonSerializer.Deserialize<WinnerCorrectionModel>(payload, _jsonOptions);
                if (correction != null)
                {
                    var match = correction.MatchId == 0
                        ? CurrentMatch
                        : _matches.FirstOrDefault(m => m.Id == correction.MatchId);

                    if (match != null)
                    {
                        match.WinnerSide = correction.WinningSide;
                        match.Winner = correction.WinningSide == Constants.Side.Red ? match.RedCompetitor : match.BlueCompetitor;
                        match.IsFinished = true;
                        match.DecisionReason = $"[CORRECTION MONITOR] {correction.Reason ?? correction.WinningSide.ToString()}";
                        PropagateWinnerToNextMatch(match);
                        StatusChanged?.Invoke(this, $"🛠 [CORRECTION WINNER] Match #{match.Id} → {match.Winner ?? "?"} ({correction.WinningSide})");
                        WinnerDeclared?.Invoke(this, (match, match.Winner ?? string.Empty, match.DecisionReason ?? string.Empty));
                        await BroadcastMatchResultAsync(match);
                        await BroadcastMatchInfoAsync();
                    }
                    else
                    {
                        StatusChanged?.Invoke(this, $"[CORRECTION WINNER] Match #{correction.MatchId} introuvable.");
                    }
                }
            }
            else if (message.StartsWith(Constants.Message.CORRECTION_SCORE))
            {
                var payload = message.Substring(Constants.Message.CORRECTION_SCORE.Length);
                var correction = JsonSerializer.Deserialize<ScoreCorrectionModel>(payload, _jsonOptions);
                if (correction != null)
                {
                    var judge = Judges.FirstOrDefault(j =>
                        string.Equals(j.Name, correction.JudgePosition, StringComparison.OrdinalIgnoreCase) ||
                        j.Number.ToString() == correction.JudgePosition ||
                        $"{j.Group}_{j.Number}" == correction.JudgePosition);

                    if (judge != null)
                    {
                        if (correction.Side == Constants.Side.Red) judge.RedPoints = correction.NewScore;
                        else if (correction.Side == Constants.Side.Blue) judge.BluePoints = correction.NewScore;
                        else judge.Score = correction.NewScore;

                        string desc = $"[CORRECTION MONITOR] Score → {correction.NewScore:F2}";
                        RecordJudgeScore(judge, correction.Side, correction.NewScore, correction.NewScore, null, "Correction", desc, correction.Round);
                        ScoreUpdated?.Invoke(this, (judge, desc));
                        StatusChanged?.Invoke(this, $"🛠 [CORRECTION SCORE] Juge {judge.Name} ({correction.Side?.ToString() ?? "Global"}) → {correction.NewScore:F2}");
                    }
                    else
                    {
                        StatusChanged?.Invoke(this, $"[CORRECTION SCORE] Juge '{correction.JudgePosition}' introuvable.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Erreur traitement message de [{clientId}] : {ex.Message}");
        }
    }

    private void OnClientDisconnected(object? sender, string clientId)
    {
        // Si le serveur possède encore un client abonné actif pour cette même adresse ou session, ne pas déconnecter le juge
        if (_bluetoothServer.ConnectedClientIds.Any(c => c.Equals(clientId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var matchingKeys = _clientJudgeMapping
            .Where(kvp => kvp.Key == clientId || kvp.Key.StartsWith(clientId + "#"))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in matchingKeys)
        {
            if (_clientJudgeMapping.TryGetValue(key, out var judge))
            {
                judge.IsConnected = false;
                _clientJudgeMapping.Remove(key);
                JudgeDisconnected?.Invoke(this, judge);
                StatusChanged?.Invoke(this, $"Liaison BLE rompue avec {judge.Name} [{key}].");
            }
        }
    }
    #endregion

    /// <summary>
    /// Réinitialise l'état de connexion de tous les juges lors de l'arrêt du serveur.
    /// </summary>
    public void ResetJudgesConnection()
    {
        foreach (var judge in Judges)
        {
            judge.IsConnected = false;
        }
        _clientJudgeMapping.Clear();
    }

    #region Category History & Standings
    /// <summary>
    /// Récupère la liste résumée de toutes les catégories présentes dans la session.
    /// </summary>
    public IReadOnlyList<CategorySummary> GetCategoriesSummary()
    {
        var summaries = new List<CategorySummary>();
        var grouped = _matches.GroupBy(m => m.Category?.Trim() ?? string.Empty).ToList();

        int index = 1;
        foreach (var group in grouped)
        {
            string catName = string.IsNullOrWhiteSpace(group.Key) ? "Catégorie Générale" : group.Key;
            var matches = group.ToList();
            int total = matches.Count;
            int completed = matches.Count(m => m.IsFinished);
            var roundType = matches.FirstOrDefault()?.Type ?? RoundType.Knockouts;

            string? leaderOrWinner = null;
            if (roundType == RoundType.Order)
            {
                var best = matches
                    .Where(m => m.IsFinished && m.FinalScore > 0)
                    .OrderByDescending(m => m.FinalScore)
                    .FirstOrDefault();
                if (best != null)
                {
                    leaderOrWinner = $"{best.RedCompetitor} ({best.FinalScore:F2})";
                }
            }
            else if (roundType == RoundType.Knockouts)
            {
                var lastMatch = matches.LastOrDefault();
                if (lastMatch != null && lastMatch.IsFinished && !string.IsNullOrWhiteSpace(lastMatch.Winner))
                {
                    leaderOrWinner = lastMatch.Winner;
                }
                else
                {
                    var finished = matches.Where(m => m.IsFinished && !string.IsNullOrWhiteSpace(m.Winner)).ToList();
                    if (finished.Count > 0)
                    {
                        var topWin = finished
                            .GroupBy(m => m.Winner!)
                            .OrderByDescending(g => g.Count())
                            .FirstOrDefault()?.Key;
                        leaderOrWinner = topWin;
                    }
                }
            }
            else // Pools
            {
                var standings = ComputePoolStandings(matches);
                if (standings.Count > 0)
                {
                    leaderOrWinner = standings[0].CompetitorName;
                }
            }

            bool hasBronze = matches.Any(m => m.IsBronzeMatch) ||
                _loadedCategories.FirstOrDefault(c => c.Name.Equals(catName, StringComparison.OrdinalIgnoreCase))?.HasBronzeMatch == true;

            summaries.Add(new CategorySummary
            {
                Index = index++,
                Name = catName,
                RoundType = roundType,
                HasBronzeMatch = hasBronze,
                TotalMatches = total,
                CompletedMatches = completed,
                LeaderOrWinner = leaderOrWinner,
                Matches = matches
            });
        }

        return summaries;
    }

    /// <summary>
    /// Récupère le rapport détaillé et le classement pour une catégorie donnée (par nom ou index 1-based).
    /// Si categoryNameOrId est null ou vide, utilise la catégorie du match courant.
    /// </summary>
    public CategoryHistoryReport? GetCategoryHistory(string? categoryNameOrId = null)
    {
        var summaries = GetCategoriesSummary();
        if (summaries.Count == 0) return null;

        CategorySummary? selectedSummary = null;

        if (string.IsNullOrWhiteSpace(categoryNameOrId))
        {
            string currentCat = CurrentMatch?.Category ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(currentCat))
            {
                selectedSummary = summaries.FirstOrDefault(s => s.Name.Equals(currentCat, StringComparison.OrdinalIgnoreCase))
                               ?? summaries.FirstOrDefault(s => s.Name.Contains(currentCat, StringComparison.OrdinalIgnoreCase));
            }
            selectedSummary ??= summaries[0];
        }
        else if (int.TryParse(categoryNameOrId, out int index) && index >= 1 && index <= summaries.Count)
        {
            selectedSummary = summaries[index - 1];
        }
        else
        {
            selectedSummary = summaries.FirstOrDefault(s => s.Name.Equals(categoryNameOrId, StringComparison.OrdinalIgnoreCase))
                           ?? summaries.FirstOrDefault(s => s.Name.Contains(categoryNameOrId, StringComparison.OrdinalIgnoreCase));
        }

        if (selectedSummary == null) return null;

        var matches = selectedSummary.Matches;
        var standings = new List<CategoryStandingItem>();
        string? categoryWinner = null;
        string? decisionReason = null;

        if (selectedSummary.RoundType == RoundType.Order)
        {
            standings = ComputeTaoluStandings(matches);
            if (standings.Count > 0 && selectedSummary.IsFinished)
            {
                categoryWinner = standings[0].CompetitorName;
                decisionReason = $"1er avec une note de {standings[0].Score:F2} / 10.00";
            }
            else if (standings.Count > 0 && standings[0].HasFinished)
            {
                categoryWinner = standings[0].CompetitorName;
                decisionReason = $"En tête (provisoire) avec {standings[0].Score:F2} / 10.00";
            }
        }
        else if (selectedSummary.RoundType == RoundType.Pools)
        {
            standings = ComputePoolStandings(matches);
            if (standings.Count > 0 && selectedSummary.IsFinished)
            {
                categoryWinner = standings[0].CompetitorName;
                decisionReason = $"Vainqueur de poule ({standings[0].Wins} victoires, {standings[0].PointDifference:+0;-0;0} diff)";
            }
            else if (standings.Count > 0 && standings[0].Wins > 0)
            {
                categoryWinner = standings[0].CompetitorName;
                decisionReason = $"En tête de poule ({standings[0].Wins} victoires)";
            }
        }
        else
        {
            standings = ComputeKnockoutStandings(matches, out categoryWinner, out decisionReason);
        }

        return new CategoryHistoryReport
        {
            Summary = selectedSummary,
            Matches = matches,
            Standings = standings,
            CategoryWinner = categoryWinner,
            DecisionReason = decisionReason
        };
    }

    private static List<CategoryStandingItem> ComputeTaoluStandings(IReadOnlyList<MatchItem> matches)
    {
        var list = new List<CategoryStandingItem>();
        foreach (var m in matches)
        {
            string compName = !string.IsNullOrWhiteSpace(m.RedCompetitor) ? m.RedCompetitor : $"Passage #{m.Id}";
            list.Add(new CategoryStandingItem
            {
                CompetitorName = compName,
                MatchesPlayed = m.IsFinished ? 1 : 0,
                Score = m.FinalScore,
                JudgeScores = new List<double>(m.JudgeScores),
                JudgeScoresByJudge = new Dictionary<string, double>(m.JudgeScoresByJudge),
                HasFinished = m.IsFinished,
                Status = m.IsFinished ? $"Validé ({m.FinalScore:F2})" : "En attente"
            });
        }

        var finished = list.Where(x => x.HasFinished).ToList();
        var pending = list.Where(x => !x.HasFinished).ToList();

        // Tri avec départage sur les notes individuelles des juges
        var sortedFinished = finished
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.JudgeScores.Count > 0 ? x.JudgeScores.Max() : 0)
            .ThenByDescending(x => x.JudgeScores.Count > 0 ? x.JudgeScores.Min() : 0)
            .ToList();

        // Attribution des rangs avec gestion des ex-æquo
        int rank = 1;
        for (int i = 0; i < sortedFinished.Count; i++)
        {
            if (i > 0)
            {
                var prev = sortedFinished[i - 1];
                var curr = sortedFinished[i];
                bool sameTotal = Math.Abs(prev.Score - curr.Score) < 0.001;
                bool sameMax = prev.JudgeScores.Count == 0 || curr.JudgeScores.Count == 0
                    || Math.Abs(prev.JudgeScores.Max() - curr.JudgeScores.Max()) < 0.001;
                bool sameMin = prev.JudgeScores.Count == 0 || curr.JudgeScores.Count == 0
                    || Math.Abs(prev.JudgeScores.Min() - curr.JudgeScores.Min()) < 0.001;
                if (!(sameTotal && sameMax && sameMin))
                    rank = i + 1;
            }
            sortedFinished[i].Rank = rank;
        }

        // Les non-terminés viennent après tous les classés
        int pendingStart = sortedFinished.Count > 0 ? sortedFinished.Max(x => x.Rank) + 1 : 1;
        for (int i = 0; i < pending.Count; i++)
            pending[i].Rank = pendingStart + i;

        return sortedFinished.Concat(pending).ToList();
    }

    private static List<CategoryStandingItem> ComputePoolStandings(IReadOnlyList<MatchItem> matches)
    {
        var competitors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in matches)
        {
            if (!string.IsNullOrWhiteSpace(m.RedCompetitor)) competitors.Add(m.RedCompetitor.Trim());
            if (!string.IsNullOrWhiteSpace(m.BlueCompetitor)) competitors.Add(m.BlueCompetitor.Trim());
        }

        var list = new List<CategoryStandingItem>();
        foreach (var comp in competitors)
        {
            int played = 0, wins = 0, losses = 0, draws = 0, ptsFor = 0, ptsAgainst = 0;

            foreach (var m in matches.Where(m => m.IsFinished))
            {
                bool isRed = string.Equals(m.RedCompetitor?.Trim(), comp, StringComparison.OrdinalIgnoreCase);
                bool isBlue = string.Equals(m.BlueCompetitor?.Trim(), comp, StringComparison.OrdinalIgnoreCase);

                if (!isRed && !isBlue) continue;

                played++;
                int myScore = isRed ? m.RedScore : m.BlueScore;
                int oppScore = isRed ? m.BlueScore : m.RedScore;
                ptsFor += myScore;
                ptsAgainst += oppScore;

                if (!string.IsNullOrWhiteSpace(m.Winner))
                {
                    if (string.Equals(m.Winner?.Trim(), comp, StringComparison.OrdinalIgnoreCase))
                    {
                        wins++;
                    }
                    else if (m.Winner!.Contains("Nul", StringComparison.OrdinalIgnoreCase) || m.Winner!.Contains("Draw", StringComparison.OrdinalIgnoreCase))
                    {
                        draws++;
                    }
                    else
                    {
                        losses++;
                    }
                }
                else
                {
                    if (myScore > oppScore) wins++;
                    else if (myScore < oppScore) losses++;
                    else draws++;
                }
            }

            list.Add(new CategoryStandingItem
            {
                CompetitorName = comp,
                MatchesPlayed = played,
                Wins = wins,
                Losses = losses,
                Draws = draws,
                PointsFor = ptsFor,
                PointsAgainst = ptsAgainst,
                HasFinished = played > 0,
                Status = played > 0 ? $"{wins}V {draws}N {losses}D" : "À venir"
            });
        }

        var sorted = list
            .OrderByDescending(x => x.Wins)
            .ThenByDescending(x => x.PointDifference)
            .ThenByDescending(x => x.PointsFor)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }

        return sorted;
    }

    private static bool IsValidCompetitorName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string trimmed = name.Trim();
        if (trimmed.Contains("EXEMPTÉ", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.StartsWith("Vainqueur", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.StartsWith("Perdant", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.StartsWith("[", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("]")) return false;
        if (trimmed.Equals("À déterminer", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static List<CategoryStandingItem> ComputeKnockoutStandings(
        IReadOnlyList<MatchItem> matches,
        out string? champion,
        out string? reason)
    {
        champion = null;
        reason = null;

        var competitors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in matches)
        {
            if (IsValidCompetitorName(m.RedCompetitor))
                competitors.Add(m.RedCompetitor.Trim());
            if (IsValidCompetitorName(m.BlueCompetitor))
                competitors.Add(m.BlueCompetitor.Trim());
        }

        // Match de finale (dernier match qui n'est pas la petite finale)
        var finalMatch = matches.LastOrDefault(m => !m.IsBronzeMatch) ?? matches.LastOrDefault();
        string? finalWinner = (finalMatch != null && finalMatch.IsFinished && !string.IsNullOrWhiteSpace(finalMatch.Winner))
            ? finalMatch.Winner.Trim()
            : null;

        string? finalLoser = null;
        if (finalWinner != null && finalMatch != null)
        {
            champion = finalWinner;
            reason = finalMatch.DecisionReason ?? "Victoire en Finale";
            finalLoser = string.Equals(finalMatch.RedCompetitor?.Trim(), finalWinner, StringComparison.OrdinalIgnoreCase)
                ? finalMatch.BlueCompetitor?.Trim()
                : finalMatch.RedCompetitor?.Trim();
        }

        // Match de petite finale (3e place) s'il existe
        var bronzeMatch = matches.FirstOrDefault(m => m.IsBronzeMatch);
        string? bronzeWinner = null;
        string? bronzeLoser = null;
        if (bronzeMatch != null && bronzeMatch.IsFinished && !string.IsNullOrWhiteSpace(bronzeMatch.Winner))
        {
            bronzeWinner = bronzeMatch.Winner.Trim();
            bronzeLoser = string.Equals(bronzeMatch.RedCompetitor?.Trim(), bronzeWinner, StringComparison.OrdinalIgnoreCase)
                ? bronzeMatch.BlueCompetitor?.Trim()
                : bronzeMatch.RedCompetitor?.Trim();
        }

        // Identifier les perdants des demi-finales
        var semiFinalLosers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (finalMatch != null)
        {
            var semiMatches = matches.Where(m => !m.IsBronzeMatch && m.NextMatchId == finalMatch.Id).ToList();
            if (semiMatches.Count == 0 && matches.Count >= 3)
            {
                // En cas d'arborescence directe
                semiMatches = matches.Where(m => !m.IsBronzeMatch && m.Id != finalMatch.Id).TakeLast(2).ToList();
            }

            foreach (var sm in semiMatches.Where(m => m.IsFinished))
            {
                if (!string.IsNullOrWhiteSpace(sm.Winner))
                {
                    string? loser = string.Equals(sm.RedCompetitor?.Trim(), sm.Winner.Trim(), StringComparison.OrdinalIgnoreCase)
                        ? sm.BlueCompetitor?.Trim()
                        : sm.RedCompetitor?.Trim();

                    if (IsValidCompetitorName(loser))
                        semiFinalLosers.Add(loser!);
                }
            }
        }

        var list = new List<CategoryStandingItem>();
        foreach (var comp in competitors)
        {
            int played = 0, wins = 0, losses = 0, ptsFor = 0, ptsAgainst = 0;

            foreach (var m in matches.Where(m => m.IsFinished))
            {
                bool isRed = string.Equals(m.RedCompetitor?.Trim(), comp, StringComparison.OrdinalIgnoreCase);
                bool isBlue = string.Equals(m.BlueCompetitor?.Trim(), comp, StringComparison.OrdinalIgnoreCase);

                if (!isRed && !isBlue) continue;

                played++;
                int myScore = isRed ? m.RedScore : m.BlueScore;
                int oppScore = isRed ? m.BlueScore : m.RedScore;
                ptsFor += myScore;
                ptsAgainst += oppScore;

                if (!string.IsNullOrWhiteSpace(m.Winner) && string.Equals(m.Winner?.Trim(), comp, StringComparison.OrdinalIgnoreCase))
                {
                    wins++;
                }
                else if (m.IsFinished)
                {
                    losses++;
                }
            }

            list.Add(new CategoryStandingItem
            {
                CompetitorName = comp,
                MatchesPlayed = played,
                Wins = wins,
                Losses = losses,
                PointsFor = ptsFor,
                PointsAgainst = ptsAgainst,
                HasFinished = played > 0,
                Status = "En lice"
            });
        }

        // Déterminer la 3e place :
        // 1. Si Petite finale jouée : vainqueur de la petite finale = 3e 🥉, perdant = 4e
        // 2. Si PAS de petite finale : départage entre les deux perdants de demi-finale au cumul de points marqués (PointsFor puis PointDifference)
        string? bronzeByPoints = null;
        string? fourthByPoints = null;
        bool hasTiedBronze = false;

        if (bronzeMatch == null && semiFinalLosers.Count > 0)
        {
            var semiLosersList = list
                .Where(c => semiFinalLosers.Contains(c.CompetitorName))
                .OrderByDescending(c => c.PointsFor)
                .ThenByDescending(c => c.PointDifference)
                .ToList();

            if (semiLosersList.Count >= 2 &&
                semiLosersList[0].PointsFor == semiLosersList[1].PointsFor &&
                semiLosersList[0].PointDifference == semiLosersList[1].PointDifference)
            {
                // Égalité parfaite de points cumulés -> 3e place ex-aequo
                hasTiedBronze = true;
            }
            else
            {
                if (semiLosersList.Count >= 1)
                    bronzeByPoints = semiLosersList[0].CompetitorName;
                if (semiLosersList.Count >= 2)
                    fourthByPoints = semiLosersList[1].CompetitorName;
            }
        }

        foreach (var item in list)
        {
            string comp = item.CompetitorName;

            if (finalWinner != null && string.Equals(comp, finalWinner, StringComparison.OrdinalIgnoreCase))
            {
                item.Status = "CHAMPION 🥇";
            }
            else if (finalLoser != null && string.Equals(comp, finalLoser, StringComparison.OrdinalIgnoreCase))
            {
                item.Status = "FINALISTE 🥈";
            }
            else if (bronzeWinner != null && string.Equals(comp, bronzeWinner, StringComparison.OrdinalIgnoreCase))
            {
                item.Status = "3e place (Petite finale) 🥉";
            }
            else if (bronzeLoser != null && string.Equals(comp, bronzeLoser, StringComparison.OrdinalIgnoreCase))
            {
                item.Status = "4e place (Petite finale)";
            }
            else if (bronzeMatch != null && (string.Equals(comp, bronzeMatch.RedCompetitor?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(comp, bronzeMatch.BlueCompetitor?.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                item.Status = "Qualifié Petite finale 🥉";
            }
            else if (bronzeMatch == null && hasTiedBronze && semiFinalLosers.Contains(comp))
            {
                item.Status = "Demi-finaliste 🥉";
            }
            else if (bronzeMatch == null && bronzeByPoints != null && string.Equals(comp, bronzeByPoints, StringComparison.OrdinalIgnoreCase))
            {
                item.Status = "3e place (Points cumulés) 🥉";
            }
            else if (bronzeMatch == null && fourthByPoints != null && string.Equals(comp, fourthByPoints, StringComparison.OrdinalIgnoreCase))
            {
                item.Status = "4e place";
            }
            else if (semiFinalLosers.Contains(comp))
            {
                item.Status = "Demi-finaliste";
            }
            else if (item.Losses > 0)
            {
                item.Status = "Éliminé";
            }
        }

        var sorted = list.OrderBy(c =>
        {
            if (finalWinner != null && string.Equals(c.CompetitorName, finalWinner, StringComparison.OrdinalIgnoreCase)) return 1;
            if (finalLoser != null && string.Equals(c.CompetitorName, finalLoser, StringComparison.OrdinalIgnoreCase)) return 2;
            if (bronzeWinner != null && string.Equals(c.CompetitorName, bronzeWinner, StringComparison.OrdinalIgnoreCase)) return 3;
            if (bronzeLoser != null && string.Equals(c.CompetitorName, bronzeLoser, StringComparison.OrdinalIgnoreCase)) return 4;
            if (bronzeMatch == null && hasTiedBronze && semiFinalLosers.Contains(c.CompetitorName)) return 3;
            if (bronzeMatch == null && bronzeByPoints != null && string.Equals(c.CompetitorName, bronzeByPoints, StringComparison.OrdinalIgnoreCase)) return 3;
            if (bronzeMatch == null && fourthByPoints != null && string.Equals(c.CompetitorName, fourthByPoints, StringComparison.OrdinalIgnoreCase)) return 4;
            if (semiFinalLosers.Contains(c.CompetitorName)) return 5;
            return 6;
        })
        .ThenByDescending(c => c.Wins)
        .ThenBy(c => c.Losses)
        .ThenByDescending(c => c.PointsFor)
        .ThenByDescending(c => c.PointDifference)
        .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            if (finalWinner != null && string.Equals(sorted[i].CompetitorName, finalWinner, StringComparison.OrdinalIgnoreCase))
                sorted[i].Rank = 1;
            else if (finalLoser != null && string.Equals(sorted[i].CompetitorName, finalLoser, StringComparison.OrdinalIgnoreCase))
                sorted[i].Rank = 2;
            else if (bronzeWinner != null && string.Equals(sorted[i].CompetitorName, bronzeWinner, StringComparison.OrdinalIgnoreCase))
                sorted[i].Rank = 3;
            else if (bronzeLoser != null && string.Equals(sorted[i].CompetitorName, bronzeLoser, StringComparison.OrdinalIgnoreCase))
                sorted[i].Rank = 4;
            else if (bronzeMatch == null && hasTiedBronze && semiFinalLosers.Contains(sorted[i].CompetitorName))
                sorted[i].Rank = 3;
            else if (bronzeMatch == null && bronzeByPoints != null && string.Equals(sorted[i].CompetitorName, bronzeByPoints, StringComparison.OrdinalIgnoreCase))
                sorted[i].Rank = 3;
            else if (bronzeMatch == null && fourthByPoints != null && string.Equals(sorted[i].CompetitorName, fourthByPoints, StringComparison.OrdinalIgnoreCase))
                sorted[i].Rank = 4;
            else
                sorted[i].Rank = i + 1;
        }

        return sorted;
    }
    #endregion

    #region Judge Score History
    /// <summary>
    /// Enregistre une action de notation dans l'historique des notes des juges.
    /// </summary>
    public JudgeScoreEntry RecordJudgeScore(
        JudgeModel judge,
        Constants.Side? side,
        double deltaPoints,
        double resultingScore,
        MvtTimeCodeModel? deductionCode,
        string actionType,
        string description,
        int? round = null,
        bool relayToMonitors = true)
    {
        var match = CurrentMatch;
        int nextId = _judgeScoreHistory.Count + 1;
        string competitors = match != null
            ? (match.Type == RoundType.Order ? match.RedCompetitor : $"{match.RedCompetitor} vs {match.BlueCompetitor}")
            : "N/A";

        var elapsed = _totalRoundTime > _timeLeft ? (_totalRoundTime - _timeLeft) : TimeSpan.Zero;
        string elapsedDisplay = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";

        int roundNumber = (round.HasValue && round.Value > 0)
            ? round.Value
            : (match?.CurrentRound ?? 1);

        var entry = new JudgeScoreEntry
        {
            Id = nextId,
            Timestamp = DateTime.Now,
            MatchTimer = elapsed,
            MatchTimeDisplay = elapsedDisplay,
            MatchId = match?.Id ?? 0,
            Round = roundNumber,
            MatchCategory = match?.Category ?? "N/A",
            MatchCompetitors = competitors,
            JudgeName = judge.Name,
            JudgeGroup = judge.Group,
            JudgeNumber = judge.Number,
            CompetitionType = CurrentCompetitionType,
            Side = side,
            DeltaPoints = deltaPoints,
            ResultingScore = resultingScore,
            DeductionCode = deductionCode,
            ActionType = actionType,
            Description = description
        };

        _judgeScoreHistory.Add(entry);
        JudgeScoreRecorded?.Invoke(this, entry);

        if (relayToMonitors && _monitorClients.Count > 0)
        {
            var transferModel = new TransfertScoreModel
            {
                Score = CurrentCompetitionType == CompetitionType.Taolu ? resultingScore : deltaPoints,
                Side = side,
                Code = deductionCode,
                JudgePosition = !string.IsNullOrEmpty(judge.Group) ? $"{judge.Group}_{judge.Number}" : judge.Number.ToString(),
                Round = roundNumber
            };
            string json = JsonSerializer.Serialize(transferModel, _jsonOptions);
            string msg = $"{Constants.Message.JUDGE_SCORE}{json}";
            _ = Task.Run(async () =>
            {
                try { await _bluetoothServer.SendToAllAsync(msg); }
                catch { }
            });
        }

        return entry;
    }

    /// <summary>
    /// Diffuse le résultat d'un match terminé aux clients (notamment Monitor CLI).
    /// </summary>
    private async Task BroadcastMatchResultAsync(MatchItem match)
    {
        if (_monitorClients.Count == 0) return;

        int? winnerId = null;
        if (!string.IsNullOrWhiteSpace(match.Winner))
        {
            var winnerComp = FindCompetitor(match.Winner);
            winnerId = winnerComp?.Id;
        }

        var result = new MatchResultModel
        {
            MatchId = match.Id,
            WinnerId = winnerId,
            WinnerName = match.Winner,
            WinnerSide = match.WinnerSide,
            RedScore = match.RedScore,
            BlueScore = match.BlueScore,
            Reason = match.DecisionReason,
            IsFinished = match.IsFinished
        };

        string json = JsonSerializer.Serialize(result, _jsonOptions);
        try
        {
            await _bluetoothServer.SendToAllAsync($"{Constants.Message.MATCH_RESULT}{json}");
        }
        catch { }
    }

    /// <summary>
    /// Récupère l'historique des notes des juges avec filtres optionnels sur le match ou sur le juge.
    /// </summary>
    public IReadOnlyList<JudgeScoreEntry> GetJudgeScoresHistory(int? matchId = null, string? judgeNameOrNum = null)
    {
        var query = _judgeScoreHistory.AsEnumerable();

        if (matchId.HasValue && matchId.Value > 0)
        {
            query = query.Where(e => e.MatchId == matchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(judgeNameOrNum))
        {
            string search = judgeNameOrNum.Trim();
            if (int.TryParse(search, out int num))
            {
                query = query.Where(e => e.JudgeNumber == num || e.JudgeName.Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                query = query.Where(e => e.JudgeName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                         (e.JudgeGroup != null && search.Equals(e.JudgeGroup, StringComparison.OrdinalIgnoreCase)));
            }
        }

        return query.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// Génère un résumé synthétique de notation pour un match donné ou global.
    /// </summary>
    public JudgeScoreSummary GetJudgeScoreSummary(int? matchId = null)
    {
        var entries = GetJudgeScoresHistory(matchId);
        return new JudgeScoreSummary
        {
            TotalEntries = entries.Count,
            RedEntries = entries.Count(e => e.Side == Constants.Side.Red),
            BlueEntries = entries.Count(e => e.Side == Constants.Side.Blue),
            DeductionEntries = entries.Count(e => e.DeductionCode != null || e.ActionType == "Déduction"),
            Entries = entries
        };
    }

    /// <summary>
    /// Efface l'historique des notes des juges.
    /// </summary>
    public void ClearJudgeScoresHistory()
    {
        _judgeScoreHistory.Clear();
        StatusChanged?.Invoke(this, "Historique des notes des juges effacé.");
    }

    /// <summary>
    /// Exporte les résultats et classements de la compétition dans un fichier Excel (.xlsx) avec un onglet par catégorie.
    /// </summary>
    /// <param name="filePath">Chemin cible optionnel (par défaut auto-généré)</param>
    /// <returns>Chemin absolu du fichier généré</returns>
    public string ExportResults(string? filePath = null)
    {
        if (CurrentMatch != null && (CurrentCompetitionType == CompetitionType.Taolu || CurrentMatch.Type == RoundType.Order) && !CurrentMatch.IsFinished)
        {
            _ = SetTaoluScoreAsync();
        }
        return ExcelCompetitionLoader.ExportResults(this, filePath);
    }

    /// <summary>
    /// Exporte l'historique complet des notes des juges (format RefereeData) dans un fichier Excel (.xlsx) avec un onglet par catégorie.
    /// </summary>
    /// <param name="filePath">Chemin cible optionnel (par défaut auto-généré)</param>
    /// <returns>Chemin absolu du fichier généré</returns>
    public string ExportJudgeScores(string? filePath = null)
    {
        if (CurrentMatch != null && (CurrentCompetitionType == CompetitionType.Taolu || CurrentMatch.Type == RoundType.Order) && !CurrentMatch.IsFinished)
        {
            _ = SetTaoluScoreAsync();
        }
        return ExcelCompetitionLoader.ExportJudgeScores(this, filePath);
    }
    #endregion

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _bluetoothServer.Dispose();
        GC.SuppressFinalize(this);
    }
}
