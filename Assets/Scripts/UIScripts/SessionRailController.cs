using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SessionRailController : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private RectTransform contentList;
    [SerializeField] private RectTransform activeOrangeFrame;     // 最底层：橙色五边形底托
    [SerializeField] private RectTransform activeForegroundRing;  // 最顶层：外圈细圆环
    [SerializeField] private TextMeshProUGUI txtChannelTitle;
    [SerializeField] private SessionItemUI sessionItemPrefab;

    [Header("框体追踪速度")]
    [SerializeField] private float trackSmoothSpeed = 20f;

    [Header("编辑器测试开关")]
    [Tooltip("勾选后：Start时自动读取场景里已摆好的测试头像；取消勾选：正式游戏流程，开局仅显示大群")]
    [SerializeField] private bool useSceneDebugItemsOnStart = false;
    [SerializeField]
    private List<SessionData> debugPresetSessions = new List<SessionData>()
    {
        new SessionData { sessionId = "group_dh", displayName = "THIS IS DH!" },
        new SessionData { sessionId = "user_1", displayName = "Zero_Null" },
        new SessionData { sessionId = "user_2", displayName = "Kestrel" }
    };

    // 事件：当玩家点击切换频道时，通知聊天内容区刷新对应频道的历史记录
    public event Action<string> OnSessionSelected;

    private readonly List<SessionItemUI> _allSessions = new List<SessionItemUI>();
    // 全角色档案库（用于首次收到某人私聊时按 ID 动态生成侧边栏头像）
    private readonly Dictionary<string, SessionData> _sessionRegistry = new Dictionary<string, SessionData>();

    private SessionItemUI _groupSession;
    private SessionItemUI _currentSelected;

    public string CurrentSessionId => _currentSelected != null ? _currentSelected.Data.sessionId : string.Empty;

    private void Start()
    {
        if (useSceneDebugItemsOnStart && _allSessions.Count == 0 && contentList != null)
        {
            SessionItemUI[] existingItems = contentList.GetComponentsInChildren<SessionItemUI>(true);
            for (int i = 0; i < existingItems.Length; i++)
            {
                SessionItemUI item = existingItems[i];
                bool isGroup = (i == 0);
                SessionData data = (i < debugPresetSessions.Count)
                    ? debugPresetSessions[i]
                    : new SessionData { sessionId = $"user_{i}", displayName = $"Member_{i}" };

                item.Setup(data, isGroup, () => SelectSession(item, immediate: false));
                _allSessions.Add(item);
                if (isGroup) _groupSession = item;
            }

            if (_allSessions.Count > 0)
            {
                RefreshHierarchyOrder();
                SelectSession(_allSessions[0], immediate: true);
            }
        }
    }

    /// <summary>
    /// 正式游戏初始化：只生成大群头像，所有私聊角色存入注册表等待首次发消息时解锁
    /// </summary>
    /// <param name="groupData">置顶大群配置</param>
    /// <param name="allCharacterProfiles">全员角色配置SO列表</param>
    /// <param name="unlockedPrivateIds">读档恢复时已解锁的私聊ID列表（新游戏传 null）</param>
    public void InitializeForGame(SessionData groupData, List<CharacterProfileSO> allCharacterProfiles, List<string> unlockedPrivateIds = null)
    {
        // 清理场景中残留的测试占位头像
        if (contentList != null)
        {
            foreach (Transform child in contentList)
            {
                if (child == activeOrangeFrame || child == activeForegroundRing) continue;
                Destroy(child.gameObject);
            }
        }
        _allSessions.Clear();
        _sessionRegistry.Clear();

        // 1. 注册大群并立即创建显示
        _sessionRegistry[groupData.sessionId] = groupData;
        _groupSession = CreateSessionItem(groupData, isGroup: true, playPopAnim: false);
        _allSessions.Add(_groupSession);

        // 2. 将所有角色注册进字典，暂不创建 UI
        if (allCharacterProfiles != null)
        {
            foreach (var profile in allCharacterProfiles)
            {
                if (profile == null || string.IsNullOrEmpty(profile.characterId)) continue;
                _sessionRegistry[profile.characterId] = new SessionData
                {
                    sessionId = profile.characterId,
                    displayName = profile.displayName,
                    avatarSprite = profile.avatarSprite,
                    lastMessageTime = 0f
                };
            }
        }

        // 3. 如果是读档恢复，把存档里已经解锁过的私聊头像还原出来
        if (unlockedPrivateIds != null)
        {
            foreach (string id in unlockedPrivateIds)
            {
                if (_sessionRegistry.TryGetValue(id, out SessionData savedData))
                {
                    var item = CreateSessionItem(savedData, isGroup: false, playPopAnim: false);
                    _allSessions.Add(item);
                }
            }
        }

        RefreshHierarchyOrder();
        SelectSession(_groupSession, immediate: true);
    }

    private SessionItemUI CreateSessionItem(SessionData data, bool isGroup, bool playPopAnim)
    {
        SessionItemUI item = Instantiate(sessionItemPrefab, contentList);
        item.Setup(data, isGroup, () => SelectSession(item, immediate: false));

        if (playPopAnim)
        {
            // 新角色第一次私聊弹出时的 Q 弹出现动效
            item.transform.localScale = Vector3.zero;
            item.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }

        return item;
    }

    public void SelectSession(SessionItemUI target, bool immediate = false)
    {
        if (target == null) return;

        bool isSameSession = (_currentSelected == target);

        if (_currentSelected != null && !isSameSession)
        {
            _currentSelected.SetSelectedVisual(false, immediate);
        }

        _currentSelected = target;
        _currentSelected.SetSelectedVisual(true, immediate);
        _currentSelected.SetUnread(false);

        if (txtChannelTitle != null)
        {
            txtChannelTitle.text = target.Data.displayName;
        }

        if (immediate)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentList);
            SyncIndicatorPositions(immediate: true);
        }

        // 通知消息流视图切换到该频道
        if (!isSameSession || immediate)
        {
            OnSessionSelected?.Invoke(target.Data.sessionId);
        }
    }

    /// <summary>
    /// 收到新消息时调用：若该私聊角色尚未出现在侧边栏，自动解锁生成并排到私聊第一位
    /// </summary>
    public void OnSessionReceivedNewMessage(string sessionId, double virtualTimestamp)
    {
        SessionItemUI target = _allSessions.Find(s => s.Data.sessionId == sessionId);

        // 第一次收到该角色的私聊消息 -> 现场解锁并实例化头像！
        if (target == null)
        {
            if (!_sessionRegistry.TryGetValue(sessionId, out SessionData data))
            {
                Debug.LogWarning($"[SessionRail] 收到未知频道 ID 的消息: {sessionId}");
                return;
            }

            target = CreateSessionItem(data, isGroup: false, playPopAnim: true);
            _allSessions.Add(target);
        }

        target.Data.lastMessageTime = virtualTimestamp;

        // 如果玩家当前没有停留在看这个频道，点亮未读红点
        if (_currentSelected != target)
        {
            target.SetUnread(true);
        }

        // 私聊按最新消息时间重新排位（最新收到消息的排在紧挨大群的最上方）
        if (!target.IsGroup)
        {
            RefreshHierarchyOrder();
        }
    }

    private void RefreshHierarchyOrder()
    {
        // 1. 橙色底托锁死在最底层 (Index 0)
        if (activeOrangeFrame != null)
            activeOrangeFrame.SetSiblingIndex(0);

        // 2. 大群永远置顶在第一个头像位 (Index 1)
        if (_groupSession != null)
            _groupSession.transform.SetSiblingIndex(1);

        // 3. 已解锁的私聊按最新消息时间降序排列
        List<SessionItemUI> privateChats = _allSessions.FindAll(s => !s.IsGroup);
        privateChats.Sort((a, b) => b.Data.lastMessageTime.CompareTo(a.Data.lastMessageTime));

        for (int i = 0; i < privateChats.Count; i++)
        {
            privateChats[i].transform.SetSiblingIndex(i + 2);
        }

        // 4. 顶层细圆环永远锁死在最顶层（最后一个子物体）
        if (activeForegroundRing != null)
            activeForegroundRing.SetAsLastSibling();

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentList);
    }

    private void LateUpdate()
    {
        SyncIndicatorPositions(immediate: false);
    }

    private void SyncIndicatorPositions(bool immediate)
    {
        if (_currentSelected == null) return;

        Vector3 targetWorldCenter = _currentSelected.RectTransform.TransformPoint(_currentSelected.RectTransform.rect.center);
        Vector3 localInContent = contentList.InverseTransformPoint(targetWorldCenter);

        if (activeOrangeFrame != null)
        {
            Vector2 pos = activeOrangeFrame.anchoredPosition;
            pos.y = immediate ? localInContent.y : Mathf.Lerp(pos.y, localInContent.y, Time.deltaTime * trackSmoothSpeed);
            activeOrangeFrame.anchoredPosition = pos;
        }

        if (activeForegroundRing != null)
        {
            Vector2 ringPos = activeForegroundRing.anchoredPosition;
            ringPos.y = immediate ? localInContent.y : Mathf.Lerp(ringPos.y, localInContent.y, Time.deltaTime * trackSmoothSpeed);
            activeForegroundRing.anchoredPosition = ringPos;
        }
    }
}