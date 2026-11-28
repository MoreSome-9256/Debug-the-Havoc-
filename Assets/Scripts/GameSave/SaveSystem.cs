using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class GameSaveData
{
    public string gameMode = "overclock";     // "sync"（挂机同步）或 "overclock"（剧情超频）
    public string currentChapterId = "ch01";  // 当前所处章节/阶段
    public long createdUnixTime;              // 开档时的现实时间戳（秒）
    public long lastSavedUnixTime;            // 上次自动保存时的现实时间戳（用于计算离线时长）

    // 虚拟时钟基准与进度
    public long virtualClockSeconds;          // 当前虚拟时钟累计秒数
    public string preferredActiveSlot = "night"; // 玩家偏好的活跃时段 ("morning" / "afternoon" / "night")

    // Ink 虚拟机状态快照（调用 _inkStory.state.ToJson() 存入，读档时可直接恢复对话位置与变量）
    public string inkStateJson;

    // 隐藏好感度与多周目档案
    public List<CharacterAffinityEntry> affinities = new List<CharacterAffinityEntry>();
    public List<string> unlockedPrivateSessions = new List<string>(); // 已解锁出现在侧边栏的私聊角色ID
    public bool isGameCompleted = false;      // 是否已通关（决定重启后是否进入只读离线归档界面）

    // 事件与全频道聊天记录
    public List<string> triggeredEventIds = new List<string>();
    public List<ChatMessageRecord> chatHistory = new List<ChatMessageRecord>();
}

[Serializable]
public class CharacterAffinityEntry
{
    public string characterId;
    public int affinity;
}

[Serializable]
public class ChatMessageRecord
{
    public string messageId;          // 唯一消息ID（用于支持消息撤回定位）
    public string sessionId;          // 所属频道 ("group_dh" 或私聊角色ID，为空时兼容默认大群)
    public string senderId;           // 发言人ID ("player" 或角色ID)
    public string contentKeyOrText;   // 文本内容 或 表情包/图片资源Key
    public string messageType = "text"; // "text" | "sticker" | "image" | "system"
    public long timestamp;            // 虚拟时间戳（秒）
    public bool isRecalled;           // 是否已被撤回
}

public static class SaveManager
{
    private const string SaveFileName = "dh_autosave.json";
    private static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static GameSaveData CurrentSave { get; private set; }

    public static bool HasSave()
    {
        return File.Exists(SaveFilePath);
    }

    public static GameSaveData CreateNewSave(string modeId)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        CurrentSave = new GameSaveData
        {
            gameMode = modeId,
            currentChapterId = "ch01_intro",
            createdUnixTime = now,
            lastSavedUnixTime = now,
            virtualClockSeconds = now
        };

        AutoSave();
        return CurrentSave;
    }

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

    public static void AutoSave()
    {
        if (CurrentSave == null) return;

        CurrentSave.lastSavedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string json = JsonUtility.ToJson(CurrentSave, prettyPrint: true);

        string tempPath = SaveFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
        }
        File.Move(tempPath, SaveFilePath);
    }

    public static GameSaveData PeekSave()
    {
        if (CurrentSave != null) return CurrentSave;
        if (!HasSave()) return null;

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            return JsonUtility.FromJson<GameSaveData>(json);
        }
        catch
        {
            return null;
        }
    }

    public static long GetOfflineElapsedSeconds()
    {
        if (CurrentSave == null) return 0;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Math.Max(0, now - CurrentSave.lastSavedUnixTime);
    }

    /// <summary>
    /// 修改指定角色隐藏好感度
    /// </summary>
    public static void ModifyAffinity(string characterId, int delta)
    {
        if (CurrentSave == null || string.IsNullOrEmpty(characterId)) return;

        var entry = CurrentSave.affinities.Find(a => a.characterId == characterId);
        if (entry == null)
        {
            entry = new CharacterAffinityEntry { characterId = characterId, affinity = 0 };
            CurrentSave.affinities.Add(entry);
        }
        entry.affinity += delta;
    }

    public static int GetAffinity(string characterId)
    {
        if (CurrentSave == null) return 0;
        var entry = CurrentSave.affinities.Find(a => a.characterId == characterId);
        return entry != null ? entry.affinity : 0;
    }

    public static void DeleteSave()
    {
        CurrentSave = null;
        if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
    }
}