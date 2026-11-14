using System.Collections.ObjectModel;
using System.Text.Json;
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
}

/// <summary>
/// Gestionnaire central de la session d'arbitrage côté serveur.
/// Gère les juges, les matchs, le chronomètre et le protocole de communication.
/// </summary>
public class ArbitrationServerSession : IDisposable
{
    private readonly WindowsBluetoothServer _bluetoothServer;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly Dictionary<string, JudgeModel> _clientJudgeMapping = new();
    private readonly List<MatchItem> _matches = new();
    private readonly List<CategoryModel> _loadedCategories = new();
    private readonly List<JudgeScoreEntry> _judgeScoreHistory = new();

    private System.Threading.Timer? _timer;
    private TimeSpan _timeLeft = TimeSpan.FromMinutes(2);
    private int _currentMatchIndex = 0;

    public WindowsBluetoothServer BluetoothServer => _bluetoothServer;
    public IReadOnlyList<CategoryModel> LoadedCategories => _loadedCategories;
    public IReadOnlyList<JudgeScoreEntry> JudgeScoreHistory => _judgeScoreHistory;

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

    public ArbitrationServerSession(WindowsBluetoothServer bluetoothServer)
    {
        _bluetoothServer = bluetoothServer;
        _bluetoothServer.MessageReceived += OnBluetoothMessageReceived;
        _bluetoothServer.ClientDisconnected += OnClientDisconnected;

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
    }

    public async Task NextMatchAsync()
    {
        if (_currentMatchIndex + 1 < _matches.Count)
        {
            _currentMatchIndex++;
            ResetMatchScores();
            StatusChanged?.Invoke(this, $"Match suivant : #{CurrentMatch?.Id} - {CurrentMatch?.Category}");
            await BroadcastMatchInfoAsync();
        }
        else
        {
            StatusChanged?.Invoke(this, "Dernier match déjà atteint.");
        }
    }

    public async Task PreviousMatchAsync()
    {
        if (_currentMatchIndex > 0)
        {
            _currentMatchIndex--;
            ResetMatchScores();
            StatusChanged?.Invoke(this, $"Match précédent : #{CurrentMatch?.Id} - {CurrentMatch?.Category}");
            await BroadcastMatchInfoAsync();
        }
    }

    public async Task SetMatchIndexAsync(int index)
    {
        if (index >= 0 && index < _matches.Count)
        {
            _currentMatchIndex = index;
            ResetMatchScores();
            StatusChanged?.Invoke(this, $"Match actif : #{CurrentMatch?.Id} - {CurrentMatch?.Category}");
            await BroadcastMatchInfoAsync();
        }
    }

    public void ResetMatchScores()
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
        StatusChanged?.Invoke(this, "Scores et points réinitialisés pour le match en cours.");
    }

    #region Winner & Scoring Methods

    /// <summary>
    /// Évalue la situation actuelle des scores pour le match Sanda actif.
    /// Renvoie les votes des juges, le total des points et une synthèse.
    /// </summary>
    public (string Leader, int RedVotes, int BlueVotes, int TotalRedPts, int TotalBluePts, string Summary) EvaluateSandaScores()
    {
        int redVotes = 0;
        int blueVotes = 0;
        int drawVotes = 0;

        foreach (var j in Judges)
        {
            if (j.RedPoints > j.BluePoints) redVotes++;
            else if (j.BluePoints > j.RedPoints) blueVotes++;
            else drawVotes++;
        }

        int totalRed = (int)Judges.Sum(j => j.RedPoints);
        int totalBlue = (int)Judges.Sum(j => j.BluePoints);

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
        var groupA = Judges.Where(j => string.Equals(j.Group, "A", StringComparison.OrdinalIgnoreCase)).ToList();
        var groupB = Judges.Where(j => string.Equals(j.Group, "B", StringComparison.OrdinalIgnoreCase)).ToList();
        var groupC = Judges.Where(j => string.Equals(j.Group, "C", StringComparison.OrdinalIgnoreCase)).ToList();

        double scoreA = groupA.Count > 0 ? groupA.Average(j => j.Score) : Constants.DEFAULT_SCORE_GROUP_A;
        double scoreB = groupB.Count > 0 ? groupB.Average(j => j.Score) : Constants.DEFAULT_SCORE_GROUP_B;
        double scoreC = groupC.Count > 0 ? groupC.Average(j => j.Score) : Constants.DEFAUTL_SCORE_GROUP_C;

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

        int judgesRed = (int)Judges.Sum(j => j.RedPoints);
        int judgesBlue = (int)Judges.Sum(j => j.BluePoints);
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

        match.Winner = match.RedCompetitor; // En Taolu individuel, le compétiteur passe
        match.WinnerSide = null;
        match.FinalScore = finalScore;
        match.IsFinished = true;
        match.DecisionReason = reason;

        PropagateWinnerToNextMatch(match);

        StatusChanged?.Invoke(this, $"🏆 [SCORE TAOLU VALIDÉ] Match #{match.Id} - {match.RedCompetitor} : {reason}");
        WinnerDeclared?.Invoke(this, (match, match.RedCompetitor, reason));

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

    #endregion

    #region Timer Methods
    public async Task StartTimerAsync()
    {
        if (IsTimerRunning) return;

        IsTimerRunning = true;
        _timer?.Dispose();
        _timer = new System.Threading.Timer(OnTimerCallback, null, 1000, 1000);

        StatusChanged?.Invoke(this, $"Chronomètre DÉMARRÉ ({TimeLeftDisplay})");
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
        _timeLeft = duration;
        StatusChanged?.Invoke(this, $"Durée du chronomètre définie à : {TimeLeftDisplay}");
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");
    }

    private async void OnTimerCallback(object? state)
    {
        if (_timeLeft.TotalSeconds > 0)
        {
            _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(1));
            TimerTick?.Invoke(this, TimeLeftDisplay);
        }
        else
        {
            await StopTimerAsync();
        }
    }
    #endregion

    #region Protocol & Broadcasts
    public async Task BroadcastMatchInfoAsync()
    {
        if (CurrentMatch == null) return;

        var data = new MatchInfoData
        {
            Id = CurrentMatch.Id,
            Type = CurrentMatch.Type
        };

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

        string json = JsonSerializer.Serialize(CurrentCompetition, _jsonOptions);
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.COMPETITION_DATA}{json}");
    }

    public async Task SendMatchInfoToClientAsync(string clientId)
    {
        if (CurrentMatch == null) return;

        var data = new MatchInfoData
        {
            Id = CurrentMatch.Id,
            Type = CurrentMatch.Type
        };

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

        string json = JsonSerializer.Serialize(CurrentCompetition, _jsonOptions);
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

                    // Envoi immédiat des infos de match et compétition
                    await SendCompetitionDataToClientAsync(clientId);
                    await SendMatchInfoToClientAsync(clientId);
                }
                else
                {
                    StatusChanged?.Invoke(this, $"[ATTENTION] Position juge '{posStr}' demandée par [{clientId}] mais introuvable dans la liste.");
                }
            }
            else if (message.StartsWith(Constants.Message.JUDGE_SCORE))
            {
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
                        RecordJudgeScore(judge, scoreModel.Side, scoreModel.Score, judge.RedPoints, null, "Point Rouge", desc);
                        ScoreUpdated?.Invoke(this, (judge, desc));
                    }
                    else if (scoreModel.Side == Constants.Side.Blue)
                    {
                        judge.BluePoints += scoreModel.Score;
                        string desc = $"+{scoreModel.Score:F0} Bleu (Total: {judge.BluePoints:F0})";
                        RecordJudgeScore(judge, scoreModel.Side, scoreModel.Score, judge.BluePoints, null, "Point Bleu", desc);
                        ScoreUpdated?.Invoke(this, (judge, desc));
                    }
                    else
                    {
                        judge.Score = scoreModel.Score;
                        if (scoreModel.Code != null)
                        {
                            judge.Codes.Insert(0, scoreModel.Code);
                            string desc = $"Déduction {scoreModel.Code.Code?.Code} (-{scoreModel.Code.Code?.Value:F1}) => Nouveau score: {judge.Score:F2}";
                            RecordJudgeScore(judge, null, -(scoreModel.Code.Code?.Value ?? 0), judge.Score, scoreModel.Code, "Déduction", desc);
                            ScoreUpdated?.Invoke(this, (judge, desc));
                        }
                        else
                        {
                            string desc = $"Score fixé à {judge.Score:F2}";
                            RecordJudgeScore(judge, null, scoreModel.Score, judge.Score, null, "Score", desc);
                            ScoreUpdated?.Invoke(this, (judge, desc));
                        }
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
                HasFinished = m.IsFinished,
                Status = m.IsFinished ? $"Validé ({m.FinalScore:F2})" : "En attente"
            });
        }

        var sorted = list
            .OrderByDescending(x => x.HasFinished)
            .ThenByDescending(x => x.Score)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }

        return sorted;
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
        string description)
    {
        var match = CurrentMatch;
        int nextId = _judgeScoreHistory.Count + 1;
        string competitors = match != null
            ? (match.Type == RoundType.Order ? match.RedCompetitor : $"{match.RedCompetitor} vs {match.BlueCompetitor}")
            : "N/A";

        var entry = new JudgeScoreEntry
        {
            Id = nextId,
            Timestamp = DateTime.Now,
            MatchTimeDisplay = TimeLeftDisplay,
            MatchId = match?.Id ?? 0,
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
        return entry;
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
        return ExcelCompetitionLoader.ExportResults(this, filePath);
    }

    /// <summary>
    /// Exporte l'historique complet des notes des juges (format RefereeData) dans un fichier Excel (.xlsx) avec un onglet par catégorie.
    /// </summary>
    /// <param name="filePath">Chemin cible optionnel (par défaut auto-généré)</param>
    /// <returns>Chemin absolu du fichier généré</returns>
    public string ExportJudgeScores(string? filePath = null)
    {
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
