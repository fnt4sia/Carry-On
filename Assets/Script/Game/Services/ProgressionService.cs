using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Persistent star/unlock save service. The JSON format uses lists rather than a
/// Dictionary so it remains compatible with Unity's built-in JsonUtility.
///
/// Progress lives in one of <see cref="SlotCount"/> save slots, one file each. Only the
/// active slot is held in memory; the lobby's save list reads the others straight off disk.
/// </summary>
public class ProgressionService : SingletonBehaviour<ProgressionService>
{
    public const int SlotCount = 4;

    [Serializable]
    private class SaveData
    {
        public int version = 1;
        public List<LevelProgress> levels = new();
    }

    [Serializable]
    private class LevelProgress
    {
        public string levelId;
        public int bestStars;
        public int bestScore;
        public bool unlocked;
    }

    /// <summary>What the save list needs to draw one row. Read-only view, never written back.</summary>
    public class SlotSummary
    {
        public int Slot;
        public bool Exists;
        public int LevelsPlayed;
        public int TotalStars;
    }

    private SaveData saveData;

    /// <summary>Slot the running game reads and writes. Survives scene loads with the service.</summary>
    public int ActiveSlot { get; private set; } = 1;

    private string SavePath => PathForSlot(ActiveSlot);

    private static string PathForSlot(int slot)
        => Path.Combine(Application.persistentDataPath, $"carry-on-save-{slot}.json");

    protected override bool PersistAcrossScenes => true;

    public event Action ProgressChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        new GameObject(nameof(ProgressionService)).AddComponent<ProgressionService>();
    }

    protected override void OnSingletonAwake()
    {
        Load();
    }

    public bool IsUnlocked(LevelConfig level)
    {
        if (level == null)
            return false;
        if (level.unlockedByDefault)
            return true;

        LevelProgress progress = Find(level.levelId);
        return progress != null && progress.unlocked;
    }

    public int GetBestStars(LevelConfig level)
        => level == null ? 0 : Find(level.levelId)?.bestStars ?? 0;

    public int GetBestScore(LevelConfig level)
        => level == null ? 0 : Find(level.levelId)?.bestScore ?? 0;

    public void RecordResult(LevelConfig level, GameResult result)
    {
        if (level == null || result == null)
            return;

        LevelProgress current = GetOrCreate(level.levelId);
        current.unlocked = true;
        current.bestStars = Mathf.Max(current.bestStars, result.Stars);
        current.bestScore = Mathf.Max(current.bestScore, result.Score);

        if (level.nextLevel != null)
            GetOrCreate(level.nextLevel.levelId).unlocked = true;

        Save();
        ProgressChanged?.Invoke();
    }

    public void ResetProgress()
    {
        saveData = new SaveData();
        Save();
        ProgressChanged?.Invoke();
    }

    // ---- Save slots ----

    /// <summary>Switch to an existing slot and read it in. Missing file just means empty progress.</summary>
    public void UseSlot(int slot)
    {
        ActiveSlot = Mathf.Clamp(slot, 1, SlotCount);
        Load();
        ProgressChanged?.Invoke();
    }

    /// <summary>Switch to a slot and wipe it. Writes immediately, so the slot exists from here on.</summary>
    public void StartNewGame(int slot)
    {
        ActiveSlot = Mathf.Clamp(slot, 1, SlotCount);
        saveData = new SaveData();
        Save();
        ProgressChanged?.Invoke();
    }

    public static bool SlotHasData(int slot) => File.Exists(PathForSlot(slot));

    /// <summary>
    /// One summary per slot, in slot order, for the lobby's save list. Reads every slot file
    /// from disk rather than the loaded slot, so the list shows all saves and not just this one.
    /// </summary>
    public static SlotSummary[] ReadSlotSummaries()
    {
        var summaries = new SlotSummary[SlotCount];

        for (int i = 0; i < SlotCount; i++)
        {
            int slot = i + 1;
            summaries[i] = new SlotSummary { Slot = slot };

            string path = PathForSlot(slot);
            if (!File.Exists(path))
                continue;

            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                summaries[i].Exists = true;
                if (data?.levels == null)
                    continue;

                foreach (LevelProgress level in data.levels)
                {
                    summaries[i].LevelsPlayed++;
                    summaries[i].TotalStars += level.bestStars;
                }
            }
            catch (Exception exception)
            {
                // A corrupt file still counts as an occupied slot — offering it as empty would
                // let New Game silently overwrite something the player may want to recover.
                summaries[i].Exists = true;
                Debug.LogWarning($"Save slot {slot} could not be read.\n{exception.Message}");
            }
        }

        return summaries;
    }

    private void Load()
    {
        if (!File.Exists(SavePath))
        {
            saveData = new SaveData();
            return;
        }

        try
        {
            saveData = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) ?? new SaveData();
            saveData.levels ??= new List<LevelProgress>();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not read Carry On save data. A fresh save will be used.\n{exception.Message}");
            saveData = new SaveData();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));

            // Write to a temp file and swap it in, so a crash mid-write cannot leave a
            // half-written save that Load() would reject and replace with empty progress.
            string tempPath = SavePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(saveData, true));

            if (File.Exists(SavePath))
                File.Replace(tempPath, SavePath, null);
            else
                File.Move(tempPath, SavePath);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not save Carry On progression.\n{exception.Message}");
        }
    }

    private LevelProgress Find(string levelId)
        => saveData?.levels?.Find(level => level.levelId == levelId);

    private LevelProgress GetOrCreate(string levelId)
    {
        saveData ??= new SaveData();
        saveData.levels ??= new List<LevelProgress>();

        LevelProgress progress = Find(levelId);
        if (progress != null)
            return progress;

        progress = new LevelProgress { levelId = levelId };
        saveData.levels.Add(progress);
        return progress;
    }
}
