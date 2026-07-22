using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Persistent star/unlock save service. The JSON format uses lists rather than a
/// Dictionary so it remains compatible with Unity's built-in JsonUtility.
/// </summary>
public class ProgressionService : SingletonBehaviour<ProgressionService>
{
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

    private SaveData saveData;
    private string SavePath => Path.Combine(Application.persistentDataPath, "carry-on-save.json");

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
