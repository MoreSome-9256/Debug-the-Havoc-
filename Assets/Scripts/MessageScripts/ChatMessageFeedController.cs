using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatMessageFeedController : MonoBehaviour
{
    [Header("核心数据与组件引用")]
    [SerializeField] private CharacterDatabaseSO characterDatabase;
    [SerializeField] private ChatWindowController chatWindow;
    [SerializeField] private SessionRailController sessionRail;

    [Header("消息列表 ScrollView 引用")]
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private RectTransform messageContainer;
    [SerializeField] private ChatMessageItemUI messageItemPrefab;

    // 1. 将原来的 typingIndicatorObj / txtTypingIndicator 替换为：
    [Header("对方正在输入提示条 (常驻 Content 最底部)")]
    [SerializeField] private ChatTypingIndicatorUI typingIndicatorUI;

    // 2. 新增：按 sessionId 记录当前正在输入的 speakerId 列表（保持先后顺序）
    private readonly Dictionary<string, List<string>> _channelTypingUsers = new Dictionary<string, List<string>>();

    [Header("连发折叠阈值 (虚拟秒)")]
    [SerializeField] private long groupMessageThresholdSeconds = 180;

    // 内存运行时索引（按 sessionId 分组缓存）
    private readonly Dictionary<string, List<ChatMessageRecord>> _channelHistories = new Dictionary<string, List<ChatMessageRecord>>();
    private readonly Dictionary<string, ChatMessageItemUI> _activeItemViews = new Dictionary<string, ChatMessageItemUI>();

    private string _activeSessionId;

    private void Awake()
    {
        if (characterDatabase != null)
            characterDatabase.InitializeCache();

        if (typingIndicatorUI != null) typingIndicatorUI.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (sessionRail != null)
            sessionRail.OnSessionSelected += HandleSessionSwitched;
    }

    private void OnDisable()
    {
        if (sessionRail != null)
            sessionRail.OnSessionSelected -= HandleSessionSwitched;
    }

    private void Start()
    {
        InitializeFromSave();
    }

    /// <summary>
    /// 启动时从 SaveManager 恢复历史聊天记录与已解锁私聊侧边栏
    /// </summary>
    public void InitializeFromSave()
    {
        if (characterDatabase == null) return;

        _channelHistories.Clear();
        _activeSessionId = characterDatabase.groupSessionId;
        _channelHistories[_activeSessionId] = new List<ChatMessageRecord>();

        List<string> unlockedPrivates = null;

        if (SaveManager.CurrentSave != null)
        {
            unlockedPrivates = SaveManager.CurrentSave.unlockedPrivateSessions;

            // 将扁平的存档历史记录按 sessionId 分发到各频道内存缓存中
            foreach (var msg in SaveManager.CurrentSave.chatHistory)
            {
                string sid = string.IsNullOrEmpty(msg.sessionId) ? characterDatabase.groupSessionId : msg.sessionId;
                if (!_channelHistories.TryGetValue(sid, out var list))
                {
                    list = new List<ChatMessageRecord>();
                    _channelHistories[sid] = list;
                }
                list.Add(msg);
            }
        }

        if (sessionRail != null)
        {
            sessionRail.InitializeForGame(
                characterDatabase.CreateGroupSessionData(),
                characterDatabase.characters,
                unlockedPrivates
            );
        }
    }

    /// <summary>
    /// 接收来自 Ink 对话引擎或玩家回复的新消息，并自动写入 SaveManager
    /// </summary>
    public void PushMessage(ChatMessageRecord record)
    {
        string groupId = characterDatabase.groupSessionId;
        if (string.IsNullOrEmpty(record.sessionId))
            record.sessionId = groupId;

        if (string.IsNullOrEmpty(record.messageId))
            record.messageId = Guid.NewGuid().ToString("N");

        // 消息正式发出时，自动将该发言人从对应频道的“正在输入”列表中移除
        if (record.senderId != "player")
        {
            SetTypingStatus(record.sessionId, record.senderId, false);
        }

        // ...（保留原有的写入历史、更新侧边栏、实例化气泡逻辑）...
        if (!_channelHistories.TryGetValue(record.sessionId, out var historyList))
        {
            historyList = new List<ChatMessageRecord>();
            _channelHistories[record.sessionId] = historyList;
        }
        bool isGrouped = ShouldGroupWithPrevious(historyList, record);
        historyList.Add(record);

        if (SaveManager.CurrentSave != null)
        {
            SaveManager.CurrentSave.chatHistory.Add(record);
            if (record.sessionId != groupId && !SaveManager.CurrentSave.unlockedPrivateSessions.Contains(record.sessionId))
            {
                SaveManager.CurrentSave.unlockedPrivateSessions.Add(record.sessionId);
            }
        }

        if (sessionRail != null)
            sessionRail.OnSessionReceivedNewMessage(record.sessionId, record.timestamp);

        if (record.sessionId == _activeSessionId)
        {
            SpawnMessageBubble(record, isGrouped);
            RefreshActiveTypingUI(); // 确保气泡插入后，提示条依然稳居最底部
            ScrollToBottom();
        }

        if (chatWindow != null && record.senderId != "player")
        {
            CharacterProfileSO sender = characterDatabase.GetProfile(record.senderId);
            bool isGroup = (record.sessionId == groupId);
            string timeText = FormatUnixToHHMM(record.timestamp);
            chatWindow.OnNewMessageReceived(sender, timeText, record.contentKeyOrText, isGroup);
        }
    }

    /// <summary>
    /// 撤回指定 messageId 的消息
    /// </summary>
    public void RecallMessage(string messageId)
    {
        if (string.IsNullOrEmpty(messageId)) return;

        // 更新存档记录状态
        if (SaveManager.CurrentSave != null)
        {
            var savedMsg = SaveManager.CurrentSave.chatHistory.Find(m => m.messageId == messageId);
            if (savedMsg != null) savedMsg.isRecalled = true;
        }

        // 若该气泡正在当前画面上显示，直接就地刷新文本
        if (_activeItemViews.TryGetValue(messageId, out var itemUI) && itemUI != null)
        {
            itemUI.Setup(null, string.Empty, "<color=#FFFFFF66><i>撤回了一条消息</i></color>", isGroupedWithPrevious: true);
        }
    }

    /// <summary>
    /// 设置某频道内指定角色的“正在输入”状态（支持多人并发）
    /// </summary>
    public void SetTypingStatus(string sessionId, string speakerId, bool isTyping)
    {
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(speakerId)) return;

        if (!_channelTypingUsers.TryGetValue(sessionId, out var typers))
        {
            typers = new List<string>();
            _channelTypingUsers[sessionId] = typers;
        }

        if (isTyping)
        {
            if (!typers.Contains(speakerId))
                typers.Add(speakerId);
        }
        else
        {
            typers.Remove(speakerId);
        }

        // 仅刷新当前正在查看的频道提示条
        if (sessionId == _activeSessionId)
        {
            RefreshActiveTypingUI();
            if (isTyping) ScrollToBottom();
        }
    }

    private void RefreshActiveTypingUI()
    {
        if (typingIndicatorUI == null) return;

        _channelTypingUsers.TryGetValue(_activeSessionId, out var activeTypers);
        typingIndicatorUI.UpdateTypers(activeTypers, characterDatabase);
    }

    private void HandleSessionSwitched(string newSessionId)
    {
        _activeSessionId = newSessionId;

        foreach (var kvp in _activeItemViews)
        {
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        }
        _activeItemViews.Clear();

        if (!_channelHistories.TryGetValue(newSessionId, out var historyList))
        {
            historyList = new List<ChatMessageRecord>();
            _channelHistories[newSessionId] = historyList;
        }

        for (int i = 0; i < historyList.Count; i++)
        {
            bool isGrouped = (i > 0) && IsSameSpeakerBurst(historyList[i - 1], historyList[i]);
            SpawnMessageBubble(historyList[i], isGrouped);
        }

        // 切换频道后，自动恢复新频道正在打字的人员提示条（例如切进私聊正好看到对方在打字）
        RefreshActiveTypingUI();
        ScrollToBottom();
    }

    private void SpawnMessageBubble(ChatMessageRecord record, bool isGrouped)
    {
        if (messageItemPrefab == null || messageContainer == null) return;

        ChatMessageItemUI itemUI = Instantiate(messageItemPrefab, messageContainer);
        CharacterProfileSO profile = characterDatabase.GetProfile(record.senderId);
        string timeText = FormatUnixToHHMM(record.timestamp);

        string displayContent = record.isRecalled
            ? "<color=#FFFFFF66><i>撤回了一条消息</i></color>"
            : record.contentKeyOrText;

        itemUI.Setup(profile, timeText, displayContent, isGrouped);

        if (!string.IsNullOrEmpty(record.messageId))
            _activeItemViews[record.messageId] = itemUI;
    }

    private bool ShouldGroupWithPrevious(List<ChatMessageRecord> history, ChatMessageRecord incoming)
    {
        if (history == null || history.Count == 0) return false;
        return IsSameSpeakerBurst(history[history.Count - 1], incoming);
    }

    private bool IsSameSpeakerBurst(ChatMessageRecord prev, ChatMessageRecord curr)
    {
        if (prev.isRecalled || curr.isRecalled) return false;
        if (prev.senderId != curr.senderId) return false;
        return (curr.timestamp - prev.timestamp) <= groupMessageThresholdSeconds;
    }

    private string FormatUnixToHHMM(long unixSeconds)
    {
        DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
        return dto.ToString("HH:mm");
    }

    private void ScrollToBottom()
    {
        if (chatScrollRect == null) return;
        StopAllCoroutines();
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        chatScrollRect.verticalNormalizedPosition = 0f;
    }
}