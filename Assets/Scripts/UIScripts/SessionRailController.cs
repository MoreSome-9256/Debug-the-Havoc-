using System.Collections.Generic;
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

    [Header("编辑器直接测试用数据")]
    [SerializeField]
    private List<SessionData> debugPresetSessions = new List<SessionData>()
    {
        new SessionData { sessionId = "group_dh", displayName = "THIS IS DH!" },
        new SessionData { sessionId = "user_1", displayName = "Zero_Null" },
        new SessionData { sessionId = "user_2", displayName = "Kestrel" }
    };

    private readonly List<SessionItemUI> _allSessions = new List<SessionItemUI>();
    private SessionItemUI _groupSession;
    private SessionItemUI _currentSelected;

    private void Start()
    {
        if (_allSessions.Count == 0 && contentList != null)
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

    public void InitializeSessions(SessionData groupData, List<SessionData> memberDataList)
    {
        foreach (var old in _allSessions)
        {
            if (old != null) Destroy(old.gameObject);
        }
        _allSessions.Clear();

        _groupSession = CreateSessionItem(groupData, isGroup: true);
        _allSessions.Add(_groupSession);

        foreach (var member in memberDataList)
        {
            var item = CreateSessionItem(member, isGroup: false);
            _allSessions.Add(item);
        }

        RefreshHierarchyOrder();
        SelectSession(_groupSession, immediate: true);
    }

    private SessionItemUI CreateSessionItem(SessionData data, bool isGroup)
    {
        SessionItemUI item = Instantiate(sessionItemPrefab, contentList);
        item.Setup(data, isGroup, () => SelectSession(item, immediate: false));
        return item;
    }

    public void SelectSession(SessionItemUI target, bool immediate = false)
    {
        if (target == null) return;

        if (_currentSelected != null && _currentSelected != target)
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
    }

    public void OnSessionReceivedNewMessage(string sessionId, float virtualTimestamp)
    {
        SessionItemUI target = _allSessions.Find(s => s.Data.sessionId == sessionId);
        if (target == null) return;

        target.Data.lastMessageTime = virtualTimestamp;

        if (_currentSelected != target)
        {
            target.SetUnread(true);
        }

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

        // 2. 大群锁死在第一个头像位 (Index 1)
        if (_groupSession != null)
            _groupSession.transform.SetSiblingIndex(1);

        // 3. 私聊按时间降序排列
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

        // 同步底层橙色五边形框
        if (activeOrangeFrame != null)
        {
            Vector2 pos = activeOrangeFrame.anchoredPosition;
            pos.y = immediate ? localInContent.y : Mathf.Lerp(pos.y, localInContent.y, Time.deltaTime * trackSmoothSpeed);
            activeOrangeFrame.anchoredPosition = pos;
        }

        // 同步顶层细圆环
        if (activeForegroundRing != null)
        {
            Vector2 ringPos = activeForegroundRing.anchoredPosition;
            ringPos.y = immediate ? localInContent.y : Mathf.Lerp(ringPos.y, localInContent.y, Time.deltaTime * trackSmoothSpeed);
            activeForegroundRing.anchoredPosition = ringPos;
        }
    }
}