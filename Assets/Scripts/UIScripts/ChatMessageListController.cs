using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChatMessageListController : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentMessages;
    [SerializeField] private ChatMessageItemUI messageItemPrefab;

    [Header("8名固定角色档案库")]
    [SerializeField] private List<CharacterProfileSO> characterRoster = new List<CharacterProfileSO>();

    private readonly Dictionary<string, CharacterProfileSO> _profileMap = new Dictionary<string, CharacterProfileSO>();
    private readonly List<ChatMessageItemUI> _spawnedItems = new List<ChatMessageItemUI>();
    private string _lastSenderId = string.Empty;

    private void Awake()
    {
        _profileMap.Clear();
        foreach (var profile in characterRoster)
        {
            if (profile != null && !string.IsNullOrEmpty(profile.characterId))
            {
                _profileMap[profile.characterId] = profile;
            }
        }
    }

    /// <summary>
    /// 只需传入角色ID、时间戳和台词，自动匹配头像、阴影色与名字颜色
    /// </summary>
    public void AppendMessage(string characterId, string timestamp, string content)
    {
        _profileMap.TryGetValue(characterId, out CharacterProfileSO profile);

        bool isGrouped = !string.IsNullOrEmpty(_lastSenderId) && (_lastSenderId == characterId);
        _lastSenderId = characterId;

        ChatMessageItemUI item = Instantiate(messageItemPrefab, contentMessages);
        item.Setup(profile, timestamp, content, isGrouped);
        _spawnedItems.Add(item);

        StartCoroutine(RebuildAndScrollToBottom());
    }

    public void ClearMessages()
    {
        foreach (var item in _spawnedItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        _spawnedItems.Clear();
        _lastSenderId = string.Empty;
    }

    private IEnumerator RebuildAndScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentMessages);
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

#if UNITY_EDITOR
    [Header("编辑器实时测试面板（Play模式下可用）")]
    [SerializeField] private string debugCharacterId = "eli";
    [SerializeField] private string debugTimestamp = "16:42";
    [TextArea(2, 4)]
    [SerializeField] private string debugMessageText = "这是一条测试消息。";

    [ContextMenu("测试：发送当前输入框消息")]
    public void DebugSendSingleMessage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("请先进入 Play 模式再测试发送消息。");
            return;
        }
        AppendMessage(debugCharacterId, debugTimestamp, debugMessageText);
    }

    [ContextMenu("测试：一键模拟多角色对话（含连续追发）")]
    public void DebugPlaySampleConversation()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("请先进入 Play 模式再测试对话流。");
            return;
        }

        ClearMessages();

        // 若档案库已配置，自动取前两个角色 ID 进行模拟；否则使用默认值
        string firstId = characterRoster.Count > 0 ? characterRoster[0].characterId : "eli";
        string secondId = characterRoster.Count > 1 ? characterRoster[1].characterId : "zero";

        AppendMessage(firstId, "16:39", "你好呀~");
        AppendMessage(firstId, "16:39", "欢迎来到 DH！刚刚是不是被桌面上突然弹出来的窗口吓了一跳？"); // 同人连发：自动隐藏头像与名字
        AppendMessage(secondId, "16:40", "欢迎新人。"); // 新发言人：重新显示头像与名字
        AppendMessage(secondId, "16:40", "右边的 START 按钮可以打开终端，先熟悉一下环境吧。"); // 同人连发
    }
#endif
}