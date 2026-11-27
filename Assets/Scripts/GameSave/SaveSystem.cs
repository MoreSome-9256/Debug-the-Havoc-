using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class GameSaveData
{
    public string gameMode = "sync";          // "sync"（挂机同步）或 "overclock"（剧情超频）
    public string currentChapterId = "ch01";  // 当前所处章节/阶段
    public long createdUnixTime;              // 开档时的现实时间戳（秒）
    public long lastSavedUnixTime;            // 上次自动保存时的现实时间戳（用于计算离线时长）

    // 后续做聊天与桌面事件时直接往这里追加字段：
    public List<string> triggeredEventIds = new List<string>();
    public List<ChatMessageRecord> chatHistory = new List<ChatMessageRecord>();
}

[Serializable]
public class ChatMessageRecord
{
    public string senderId;
    public string contentKeyOrText;
    public long timestamp;
}

public static class SaveManager
{
    private const string SaveFileName = "dh_autosave.json";
    private static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static GameSaveData CurrentSave { get; private set; }

    /// <summary>
    /// 检测本地是否存在有效自动存档（决定“继续潜入”按钮是否可用）
    /// </summary>
    public static bool HasSave()
    {
        return File.Exists(SaveFilePath);
    }

    /// <summary>
    /// 开启新游戏：创建全新存档并立即写入本地（覆盖旧档）
    /// </summary>
    public static GameSaveData CreateNewSave(string modeId)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        CurrentSave = new GameSaveData
        {
            gameMode = modeId,
            currentChapterId = "ch01_intro",
            createdUnixTime = now,
            lastSavedUnixTime = now
        };

        AutoSave();
        return CurrentSave;
    }

    /// <summary>
    /// 读取现有存档（点击“继续潜入”时调用）
    /// </summary>
    public static GameSaveData LoadSave()
    {
        if (!HasSave())
        {
            Debug.LogWarning("[SaveManager] 未找到存档文件，无法继续潜入。");
            return null;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            CurrentSave = JsonUtility.FromJson<GameSaveData>(json);
            return CurrentSave;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 读取存档失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 触发自动存档（在关键对话结束、选项确认、游戏退出时调用）
    /// </summary>
    public static void AutoSave()
    {
        if (CurrentSave == null) return;

        CurrentSave.lastSavedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string json = JsonUtility.ToJson(CurrentSave, prettyPrint: true);

        // 先写入临时文件再替换，避免强退导致 JSON 截断损坏
        string tempPath = SaveFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
        }
        File.Move(tempPath, SaveFilePath);
    }

    /// <summary>
    /// 计算距离上次保存过去了多少现实秒数（供 sync 挂机模式推进离线事件）
    /// </summary>
    public static long GetOfflineElapsedSeconds()
    {
        if (CurrentSave == null) return 0;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Math.Max(0, now - CurrentSave.lastSavedUnixTime);
    }

    /// <summary>
    /// 调试/重置用：删除当前存档
    /// </summary>
    public static void DeleteSave()
    {
        CurrentSave = null;
        if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
    }
}