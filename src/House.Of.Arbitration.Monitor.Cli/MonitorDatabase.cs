using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Serialization;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Monitor.Cli;

/// <summary>
/// Base de données SQLite locale — miroir du serveur d'arbitrage.
/// </summary>
public class MonitorDatabase : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private SqliteConnection? _connection;

    public MonitorDatabase(string dbPath)
    {
        _dbPath = dbPath;
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        await _connection.OpenAsync();

        await ExecuteAsync("PRAGMA journal_mode=WAL;");

        await ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS competitions (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                type TEXT NOT NULL,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS matches (
                id INTEGER PRIMARY KEY,
                type TEXT,
                round INTEGER,
                category_id INTEGER,
                category_name TEXT,
                stage_name TEXT,
                competitor1_id INTEGER,
                competitor1_name TEXT,
                competitor2_id INTEGER,
                competitor2_name TEXT,
                winner_id INTEGER,
                is_finished INTEGER DEFAULT 0,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS judge_scores (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                match_id INTEGER,
                judge_position TEXT,
                competitor_id INTEGER,
                side TEXT,
                score REAL,
                round INTEGER,
                json TEXT NOT NULL,
                received_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                type TEXT NOT NULL,
                payload TEXT,
                received_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS corrections (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                type TEXT NOT NULL,
                payload TEXT NOT NULL,
                sent_at TEXT NOT NULL,
                ack_at TEXT
            );
        ");
    }

    // ── Competitions ──────────────────────────────────────────────────────────

    public async Task SaveCompetitionAsync(CompetitionModel competition)
    {
        string json = JsonSerializer.Serialize(competition, JsonOptions);
        await ExecuteAsync(
            @"INSERT INTO competitions (id, name, type, json, updated_at)
              VALUES (@id, @name, @type, @json, @ts)
              ON CONFLICT(id) DO UPDATE SET name=excluded.name, type=excluded.type, json=excluded.json, updated_at=excluded.updated_at",
            ("@id", competition.Id),
            ("@name", competition.Name),
            ("@type", competition.Type.ToString()),
            ("@json", json),
            ("@ts", DateTime.UtcNow.ToString("O")));
    }

    // ── Matches ───────────────────────────────────────────────────────────────

    public async Task SaveMatchAsync(MatchInfoData match)
    {
        string json = JsonSerializer.Serialize(match, JsonOptions);
        string? c1Name = match.Competitor1?.Name;
        string? c2Name = match.Competitor2?.Name;
        int? winnerId = match.DrawKnockout?.WinnerId ?? match.DrawPool?.WinnerId;
        bool isFinished = match.DrawKnockout?.IsFinished ?? match.DrawPool?.IsFinished ?? match.DrawOrder?.IsFinished ?? false;

        await ExecuteAsync(
            @"INSERT INTO matches (id, type, round, category_id, category_name, stage_name,
                competitor1_id, competitor1_name, competitor2_id, competitor2_name,
                winner_id, is_finished, json, updated_at)
              VALUES (@id, @type, @round, @catId, @catName, @stage,
                      @c1id, @c1name, @c2id, @c2name, @win, @fin, @json, @ts)
              ON CONFLICT(id) DO UPDATE SET
                type=excluded.type, round=excluded.round,
                category_id=excluded.category_id, category_name=excluded.category_name,
                stage_name=excluded.stage_name,
                competitor1_id=excluded.competitor1_id, competitor1_name=excluded.competitor1_name,
                competitor2_id=excluded.competitor2_id, competitor2_name=excluded.competitor2_name,
                winner_id=COALESCE(excluded.winner_id, matches.winner_id),
                is_finished=MAX(excluded.is_finished, matches.is_finished),
                json=excluded.json, updated_at=excluded.updated_at",
            ("@id", match.Id),
            ("@type", match.Type.ToString()),
            ("@round", match.Round),
            ("@catId", (object?)match.CategoryId ?? DBNull.Value),
            ("@catName", (object?)match.CategoryName ?? DBNull.Value),
            ("@stage", (object?)match.StageName ?? DBNull.Value),
            ("@c1id", (object?)match.Competitor1?.Id ?? DBNull.Value),
            ("@c1name", (object?)c1Name ?? DBNull.Value),
            ("@c2id", (object?)match.Competitor2?.Id ?? DBNull.Value),
            ("@c2name", (object?)c2Name ?? DBNull.Value),
            ("@win", (object?)winnerId ?? DBNull.Value),
            ("@fin", isFinished ? 1 : 0),
            ("@json", json),
            ("@ts", DateTime.UtcNow.ToString("O")));
    }

    public async Task MarkMatchFinishedAsync(int matchId, int? winnerId)
    {
        await ExecuteAsync(
            "UPDATE matches SET is_finished=1, winner_id=@win, updated_at=@ts WHERE id=@id",
            ("@win", (object?)winnerId ?? DBNull.Value),
            ("@ts", DateTime.UtcNow.ToString("O")),
            ("@id", matchId));
    }

    public async Task<List<MatchRow>> GetMatchHistoryAsync(int limit = 50)
    {
        var rows = new List<MatchRow>();
        await _dbLock.WaitAsync();
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = $"SELECT id, category_name, stage_name, competitor1_name, competitor2_name, winner_id, is_finished, updated_at FROM matches ORDER BY updated_at DESC LIMIT {limit}";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new MatchRow(
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? "" : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    reader.GetInt32(6) == 1,
                    reader.GetString(7)));
            }
        }
        finally
        {
            _dbLock.Release();
        }
        return rows;
    }

    // ── Scores ────────────────────────────────────────────────────────────────

    public async Task SaveJudgeScoreAsync(int? matchId, TransfertScoreModel score)
    {
        string json = JsonSerializer.Serialize(score, JsonOptions);
        await ExecuteAsync(
            @"INSERT INTO judge_scores (match_id, judge_position, competitor_id, side, score, round, json, received_at)
              VALUES (@mid, @pos, @cid, @side, @score, @round, @json, @ts)",
            ("@mid", (object?)matchId ?? DBNull.Value),
            ("@pos", (object?)score.JudgePosition ?? DBNull.Value),
            ("@cid", (object?)score.CompetitorId ?? DBNull.Value),
            ("@side", (object?)score.Side?.ToString() ?? DBNull.Value),
            ("@score", score.Score),
            ("@round", (object?)score.Round ?? DBNull.Value),
            ("@json", json),
            ("@ts", DateTime.UtcNow.ToString("O")));
    }

    public async Task<List<ScoreRow>> GetScoresForMatchAsync(int matchId)
    {
        var rows = new List<ScoreRow>();
        await _dbLock.WaitAsync();
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT judge_position, competitor_id, side, score, round, received_at FROM judge_scores WHERE match_id=@id ORDER BY received_at";
            cmd.Parameters.AddWithValue("@id", matchId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new ScoreRow(
                    reader.IsDBNull(0) ? "" : reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.GetDouble(3),
                    reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    reader.GetString(5)));
            }
        }
        finally
        {
            _dbLock.Release();
        }
        return rows;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public async Task LogEventAsync(string type, string? payload = null)
    {
        await ExecuteAsync(
            "INSERT INTO events (type, payload, received_at) VALUES (@type, @payload, @ts)",
            ("@type", type),
            ("@payload", (object?)payload ?? DBNull.Value),
            ("@ts", DateTime.UtcNow.ToString("O")));
    }

    // ── Corrections ───────────────────────────────────────────────────────────

    public async Task<long> LogCorrectionSentAsync(string type, string payload)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "INSERT INTO corrections (type, payload, sent_at) VALUES (@type, @payload, @ts); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@type", type);
            cmd.Parameters.AddWithValue("@payload", payload);
            cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O"));
            var result = await cmd.ExecuteScalarAsync();
            return result is long l ? l : 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task MarkCorrectionAckedAsync(long id)
    {
        await ExecuteAsync(
            "UPDATE corrections SET ack_at=@ts WHERE id=@id",
            ("@ts", DateTime.UtcNow.ToString("O")),
            ("@id", id));
    }

    // ── Export CSV ────────────────────────────────────────────────────────────

    public async Task ExportToCsvAsync(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        // Matches
        var matches = await GetMatchHistoryAsync(10000);
        await File.WriteAllLinesAsync(Path.Combine(outputDir, "matches.csv"),
            new[] { "id,category,stage,competitor1,competitor2,winner_id,finished,updated_at" }
            .Concat(matches.Select(m => $"{m.Id},{CsvEsc(m.CategoryName)},{CsvEsc(m.StageName)},{CsvEsc(m.Competitor1Name)},{CsvEsc(m.Competitor2Name)},{m.WinnerId},{m.IsFinished},{m.UpdatedAt}")));

        // Scores
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT match_id, judge_position, competitor_id, side, score, round, received_at FROM judge_scores ORDER BY received_at";
        using var reader = await cmd.ExecuteReaderAsync();
        var scoreLines = new List<string> { "match_id,judge_position,competitor_id,side,score,round,received_at" };
        while (await reader.ReadAsync())
        {
            string mId = reader.IsDBNull(0) ? "" : reader.GetInt32(0).ToString();
            string pos = reader.IsDBNull(1) ? "" : reader.GetString(1);
            string cId = reader.IsDBNull(2) ? "" : reader.GetInt32(2).ToString();
            string side = reader.IsDBNull(3) ? "" : reader.GetString(3);
            double sc = reader.GetDouble(4);
            string rnd = reader.IsDBNull(5) ? "" : reader.GetInt32(5).ToString();
            string ts = reader.GetString(6);
            scoreLines.Add($"{mId},{CsvEsc(pos)},{cId},{CsvEsc(side)},{sc},{rnd},{ts}");
        }
        await File.WriteAllLinesAsync(Path.Combine(outputDir, "scores.csv"), scoreLines);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };

    private static string CsvEsc(string? s) => s == null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";

    private async Task ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
}

// ── Row DTOs ──────────────────────────────────────────────────────────────────

public record MatchRow(int Id, string CategoryName, string StageName, string Competitor1Name, string Competitor2Name, int? WinnerId, bool IsFinished, string UpdatedAt);

public record ScoreRow(string JudgePosition, int? CompetitorId, string Side, double Score, int? Round, string ReceivedAt);
