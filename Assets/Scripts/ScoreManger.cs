using UnityEngine;
using System;
using System.Collections.Generic;

public class ScoreManager : MonoBehaviour
{
    [Serializable]
    public sealed class RankingEntry
    {
        public string playerName;
        public int score;

        public RankingEntry() { }

        public RankingEntry(string name, int value)
        {
            playerName = name;
            score = value;
        }
    }

    [Serializable]
    private sealed class RankingData
    {
        public List<RankingEntry> entries = new List<RankingEntry>();
    }

    public static ScoreManager Instance { get; private set; }

    private const string HighScoreKey = "HighScore";
    private const string RankingKey = "LocalTop10Ranking";
    private const string LastPlayerNameKey = "LastRankingPlayerName";
    private const int MaxRankingEntries = 10;

    public int KillCount { get; private set; }
    public int HighScore { get; private set; }
    public IReadOnlyList<RankingEntry> Rankings => rankings;
    public string LastPlayerName { get; private set; }
    public int LastSubmittedRank { get; private set; }
    public bool HasSubmittedRun => runSubmitted;
    private bool highScoreDirty;
    private bool runSubmitted;
    private readonly List<RankingEntry> rankings = new List<RankingEntry>(MaxRankingEntries);

    public event Action<int> OnKillCountChanged;
    public event Action<int> OnHighScoreChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        LoadRankings();
        LastPlayerName = SanitizePlayerName(PlayerPrefs.GetString(LastPlayerNameKey, "PILOT"));
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        if (rankings.Count > 0)
            HighScore = Mathf.Max(HighScore, rankings[0].score);
    }

    public void AddKill(int amount = 1)
    {
        KillCount += amount;
        OnKillCountChanged?.Invoke(KillCount);

        if (KillCount > HighScore)
        {
            HighScore = KillCount;
            PlayerPrefs.SetInt(HighScoreKey, HighScore);
            highScoreDirty = true;

            OnHighScoreChanged?.Invoke(HighScore);
        }
    }

    public void ResetScore()
    {
        KillCount = 0;
        OnKillCountChanged?.Invoke(KillCount);
    }

    public void ResetHighScore()
    {
        HighScore = 0;
        PlayerPrefs.DeleteKey(HighScoreKey);
        highScoreDirty = true;

        OnHighScoreChanged?.Invoke(HighScore);
    }

    public int SubmitCurrentRun(string playerName)
    {
        if (runSubmitted)
            return LastSubmittedRank;

        runSubmitted = true;

        string safeName = SanitizePlayerName(playerName);
        LastPlayerName = safeName;

        int insertIndex = 0;
        while (insertIndex < rankings.Count && rankings[insertIndex].score >= KillCount)
            insertIndex++;

        rankings.Insert(insertIndex, new RankingEntry(safeName, KillCount));
        LastSubmittedRank = insertIndex < MaxRankingEntries ? insertIndex + 1 : 0;

        if (rankings.Count > MaxRankingEntries)
            rankings.RemoveRange(MaxRankingEntries, rankings.Count - MaxRankingEntries);

        RankingData rankingData = new RankingData();
        rankingData.entries.AddRange(rankings);
        PlayerPrefs.SetString(RankingKey, JsonUtility.ToJson(rankingData));
        PlayerPrefs.SetString(LastPlayerNameKey, safeName);
        highScoreDirty = true;
        return LastSubmittedRank;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveHighScoreIfNeeded();
    }

    void OnApplicationQuit()
    {
        SaveHighScoreIfNeeded();
    }

    void OnDestroy()
    {
        if (Instance != this)
            return;

        SaveHighScoreIfNeeded();
        Instance = null;
    }

    void SaveHighScoreIfNeeded()
    {
        if (!highScoreDirty)
            return;

        PlayerPrefs.Save();
        highScoreDirty = false;
    }

    void LoadRankings()
    {
        rankings.Clear();
        string savedRanking = PlayerPrefs.GetString(RankingKey, string.Empty);
        if (string.IsNullOrWhiteSpace(savedRanking))
            return;

        if (savedRanking.TrimStart().StartsWith("{"))
        {
            RankingData rankingData = JsonUtility.FromJson<RankingData>(savedRanking);
            if (rankingData != null && rankingData.entries != null)
            {
                foreach (RankingEntry entry in rankingData.entries)
                {
                    if (rankings.Count >= MaxRankingEntries)
                        break;

                    if (entry != null && entry.score >= 0)
                    {
                        entry.playerName = SanitizePlayerName(entry.playerName);
                        rankings.Add(entry);
                    }
                }
            }
        }
        else
        {
            string[] values = savedRanking.Split(',');
            foreach (string value in values)
            {
                if (rankings.Count >= MaxRankingEntries)
                    break;

                if (int.TryParse(value, out int score) && score >= 0)
                    rankings.Add(new RankingEntry("PILOT", score));
            }
        }

        rankings.Sort((a, b) => b.score.CompareTo(a.score));
    }

    string SanitizePlayerName(string playerName)
    {
        string safeName = string.IsNullOrWhiteSpace(playerName) ? "PILOT" : playerName.Trim();
        safeName = safeName.Replace("\n", " ").Replace("\r", " ");
        return safeName.Length > 16 ? safeName.Substring(0, 16) : safeName;
    }
}
