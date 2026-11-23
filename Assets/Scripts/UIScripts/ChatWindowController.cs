using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatWindowController : DraggableWindow
{
    [Header("折叠状态节点")]
    [SerializeField] private GameObject expandedBodyRoot;   // 完整聊天记录区 + 底部选项区 + 左侧栏
    [SerializeField] private GameObject miniPreviewBarRoot; // 折叠后的迷你消息条
    [SerializeField] private TextMeshProUGUI txtMiniLatestMsg;
    [SerializeField] private GameObject unreadBadgeObj;

    [Header("核心交互按钮")]
    [SerializeField] private Button btnCollapse;            // 左上绿色菱形折叠按钮
    [SerializeField] private RectTransform collapseIcon;    // 菱形内双箭头图标（用于旋转 180°）
    [SerializeField] private Button btnStartCode;           // 右侧绿色 △ START 按钮
    [SerializeField] private TextMeshProUGUI txtChannelTitle;

    [Header("高度动画参数 (基于 4K 画布)")]
    [SerializeField] private float expandedHeight = 1480f;
    [SerializeField] private float collapsedHeight = 160f;
    [SerializeField] private float animDuration = 0.22f;

    [Header("联动窗口")]
    [SerializeField] private DraggableWindow codeEditorWindow;

    public bool IsCollapsed { get; private set; } = false;
    private Coroutine _animCoroutine;

    protected override void Awake()
    {
        base.Awake();

        if (btnCollapse != null)
            btnCollapse.onClick.AddListener(ToggleCollapse);

        if (btnStartCode != null)
            btnStartCode.onClick.AddListener(OnStartCodeClicked);

        ApplyCollapseStateImmediate(false);
    }

    /// <summary>
    /// 切换展开 / 折叠为底部迷你消息条
    /// </summary>
    public void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateCollapseRoutine(IsCollapsed));
    }

    private IEnumerator AnimateCollapseRoutine(bool collapse)
    {
        float startHeight = RectTransform.sizeDelta.y;
        float targetHeight = collapse ? collapsedHeight : expandedHeight;

        float startAngle = collapseIcon != null ? collapseIcon.localEulerAngles.z : 0f;
        float targetAngle = collapse ? 180f : 0f;

        if (!collapse)
        {
            // 展开前先激活主体内容，清除未读红点
            if (expandedBodyRoot != null) expandedBodyRoot.SetActive(true);
            if (miniPreviewBarRoot != null) miniPreviewBarRoot.SetActive(false);
            SetUnreadBadge(false);
        }

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);

            Vector2 size = RectTransform.sizeDelta;
            size.y = Mathf.Lerp(startHeight, targetHeight, t);
            RectTransform.sizeDelta = size;

            if (collapseIcon != null)
            {
                float angle = Mathf.LerpAngle(startAngle, targetAngle, t);
                collapseIcon.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            yield return null;
        }

        ApplyCollapseStateImmediate(collapse);
    }

    private void ApplyCollapseStateImmediate(bool collapse)
    {
        Vector2 size = RectTransform.sizeDelta;
        size.y = collapse ? collapsedHeight : expandedHeight;
        RectTransform.sizeDelta = size;

        if (collapseIcon != null)
            collapseIcon.localEulerAngles = new Vector3(0f, 0f, collapse ? 180f : 0f);

        if (expandedBodyRoot != null) expandedBodyRoot.SetActive(!collapse);
        if (miniPreviewBarRoot != null) miniPreviewBarRoot.SetActive(collapse);
    }

    /// <summary>
    /// 切换群聊 / 私聊频道
    /// </summary>
    public void SwitchChannel(string channelDisplayName)
    {
        if (txtChannelTitle != null)
            txtChannelTitle.text = channelDisplayName;

        if (IsCollapsed)
            ToggleCollapse();
    }

    /// <summary>
    /// 收到新消息时更新迷你预览条与未读红点
    /// </summary>
    public void OnNewMessageReceived(string sender, string content)
    {
        if (txtMiniLatestMsg != null)
            txtMiniLatestMsg.text = $"<color=#FF8C24>[{sender}]</color> {content}";

        if (IsCollapsed)
            SetUnreadBadge(true);
    }

    public void SetUnreadBadge(bool hasUnread)
    {
        if (unreadBadgeObj != null)
            unreadBadgeObj.SetActive(hasUnread);
    }

    private void OnStartCodeClicked()
    {
        if (codeEditorWindow != null)
        {
            codeEditorWindow.OpenWindow();
        }
    }
}
