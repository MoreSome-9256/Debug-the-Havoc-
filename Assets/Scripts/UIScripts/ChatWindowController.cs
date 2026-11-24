using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatWindowController : DraggableWindow
{
    [Header("展开态独立节点 (折叠时统一隐藏)")]
    [Tooltip("依次拖入: TitleBackground, ChatWindow, Left_SessionRail, StartButton")]
    [SerializeField] private GameObject[] normalStateObjects;

    [Header("折叠态专属 Preview 界面")]
    [SerializeField] private GameObject miniPreviewBarRoot;
    [SerializeField] private CanvasGroup miniPreviewCanvasGroup;
    [SerializeField] private Button btnClickPreviewToExpand;

    [Header("顶部累计未读胶囊 (+N new)")]
    [SerializeField] private GameObject unreadSummaryBanner;
    [SerializeField] private Image[] unreadAvatarSlots;          // 最多拖入 3 个叠放的迷你头像 Image
    [SerializeField] private TextMeshProUGUI txtUnreadCount;

    [Header("下方最新消息单条预览")]
    [SerializeField] private Image imgPreviewAvatar;
    [SerializeField] private Image imgPreviewAvatarShadow;
    [SerializeField] private TextMeshProUGUI txtPreviewHeader;
    [SerializeField] private TextMeshProUGUI txtPreviewBody;
    [SerializeField] private Image imgChannelDot;
    [SerializeField] private Color groupChatDotColor = new Color(0.98f, 0.55f, 0.15f, 1f); // 橙点：群聊
    [SerializeField] private Color directMsgDotColor = new Color(0.25f, 0.88f, 0.45f, 1f); // 绿点：私聊

    [Header("核心交互按钮")]
    [SerializeField] private Button btnCollapse;
    [SerializeField] private RectTransform collapseIcon;
    [SerializeField] private Button btnStartCode;
    [SerializeField] private TextMeshProUGUI txtChannelTitle;
    [SerializeField] private Button btnSettings;
    public event System.Action OnSettingsClicked;

    [Header("半隐藏位移与动效参数 (基于 4K 画布)")]
    [SerializeField] private float collapsedPosY = -920f;
    [SerializeField] private float animDuration = 0.28f;

    [Header("联动窗口")]
    [SerializeField] private DraggableWindow codeEditorWindow;

    private Sequence _foldSeq;
    private bool _isCollapsed = false;
    private Vector2 _expandedAnchoredPos;
    private bool _defaultClampState;

    // 折叠期间累计的未读数与最近发信人头像队列
    private int _unreadTotalCount = 0;
    private readonly List<Sprite> _recentUnreadAvatars = new List<Sprite>();

    public bool IsCollapsed => _isCollapsed;

    protected override void Awake()
    {
        base.Awake();
        _defaultClampState = clampToScreen;
        _expandedAnchoredPos = windowRect.anchoredPosition;

        if (btnCollapse != null)
            btnCollapse.onClick.AddListener(ToggleCollapse);

        if (btnClickPreviewToExpand != null)
            btnClickPreviewToExpand.onClick.AddListener(() => { if (_isCollapsed) ToggleCollapse(); });

        if (btnStartCode != null)
            btnStartCode.onClick.AddListener(OpenCodeWindow);

        if (btnSettings != null)
            btnSettings.onClick.AddListener(() => OnSettingsClicked?.Invoke());

        // 初始保持展开态
        if (miniPreviewBarRoot != null) miniPreviewBarRoot.SetActive(false);
        ResetUnreadState();
    }

    public void ToggleCollapse()
    {
        _isCollapsed = !_isCollapsed;
        _foldSeq?.Kill();
        _foldSeq = DOTween.Sequence();

        if (_isCollapsed)
        {
            _expandedAnchoredPos = windowRect.anchoredPosition;
            clampToScreen = false;

            // 隐藏展开态的 4 个模块
            SetNormalObjectsActive(false);

            // 整窗向屏幕底部下沉，只留出上半部分
            Vector2 targetCollapsedPos = new Vector2(_expandedAnchoredPos.x, collapsedPosY);
            _foldSeq.Join(windowRect.DOAnchorPos(targetCollapsedPos, animDuration).SetEase(Ease.OutQuart));

            if (collapseIcon != null)
                _foldSeq.Join(collapseIcon.DOLocalRotate(Vector3.zero, animDuration).SetEase(Ease.OutBack));

            // 显示折叠态 Preview 界面
            if (miniPreviewBarRoot != null)
            {
                miniPreviewBarRoot.SetActive(true);
                RefreshUnreadBannerUI();
                if (miniPreviewCanvasGroup != null)
                {
                    miniPreviewCanvasGroup.alpha = 0f;
                    _foldSeq.Join(miniPreviewCanvasGroup.DOFade(1f, animDuration * 0.8f));
                }
            }
        }
        else
        {
            // 关闭 Preview 并清零未读计数
            if (miniPreviewBarRoot != null) miniPreviewBarRoot.SetActive(false);
            ResetUnreadState();

            // 恢复展开态的 4 个模块
            SetNormalObjectsActive(true);

            // 整窗滑回展开前的坐标
            _foldSeq.Join(windowRect.DOAnchorPos(_expandedAnchoredPos, animDuration).SetEase(Ease.OutBack, 1.05f));

            if (collapseIcon != null)
                _foldSeq.Join(collapseIcon.DOLocalRotate(new Vector3(0f, 0f, 180f), animDuration).SetEase(Ease.OutBack));

            _foldSeq.OnComplete(() =>
            {
                clampToScreen = _defaultClampState;
            });
        }
    }

    /// <summary>
    /// 外部发消息时调用：更新底部单条预览卡片；若当前处于折叠态，自动累加未读数与头像栈
    /// </summary>
    public void OnNewMessageReceived(CharacterProfileSO sender, string timestamp, string content, bool isGroupChat)
    {
        // 1. 刷新下方单条最新消息预览卡片
        if (sender != null)
        {
            if (imgPreviewAvatar != null && sender.avatarSprite != null)
                imgPreviewAvatar.sprite = sender.avatarSprite;

            if (imgPreviewAvatarShadow != null)
                imgPreviewAvatarShadow.color = sender.shadowColor;

            if (txtPreviewHeader != null)
            {
                string hexColor = ColorUtility.ToHtmlStringRGB(sender.nameColor);
                txtPreviewHeader.text = $"<color=#{hexColor}><b>{sender.displayName}</b></color>  <size=22><color=#FFFFFF8C>{timestamp}</color></size>";
            }
        }

        if (txtPreviewBody != null)
            txtPreviewBody.text = content;

        if (imgChannelDot != null)
            imgChannelDot.color = isGroupChat ? groupChatDotColor : directMsgDotColor;

        // 2. 若窗口处于折叠态，累计未读计数并更新顶部 "+N new" 胶囊
        if (_isCollapsed)
        {
            _unreadTotalCount++;
            if (sender != null && sender.avatarSprite != null)
            {
                // 去重并把最新发言人排在最前面
                _recentUnreadAvatars.Remove(sender.avatarSprite);
                _recentUnreadAvatars.Insert(0, sender.avatarSprite);
            }
            RefreshUnreadBannerUI();

            // 收到新消息时给 Preview 栏一个轻微的提示跳动
            if (miniPreviewBarRoot != null)
            {
                miniPreviewBarRoot.transform.DOKill();
                miniPreviewBarRoot.transform.localScale = Vector3.one;
                miniPreviewBarRoot.transform.DOPunchScale(new Vector3(0.03f, 0.03f, 0f), 0.25f, 6, 0.5f);
            }
        }
    }

    private void RefreshUnreadBannerUI()
    {
        if (unreadSummaryBanner == null) return;

        bool hasUnread = _unreadTotalCount > 0;
        unreadSummaryBanner.SetActive(hasUnread);
        if (!hasUnread) return;

        if (txtUnreadCount != null)
            txtUnreadCount.text = $"+{_unreadTotalCount} new";

        if (unreadAvatarSlots != null)
        {
            for (int i = 0; i < unreadAvatarSlots.Length; i++)
            {
                if (unreadAvatarSlots[i] == null) continue;
                bool showSlot = i < _recentUnreadAvatars.Count;
                unreadAvatarSlots[i].gameObject.SetActive(showSlot);
                if (showSlot)
                    unreadAvatarSlots[i].sprite = _recentUnreadAvatars[i];
            }
        }
    }

    private void ResetUnreadState()
    {
        _unreadTotalCount = 0;
        _recentUnreadAvatars.Clear();
        if (unreadSummaryBanner != null)
            unreadSummaryBanner.SetActive(false);
    }

    private void SetNormalObjectsActive(bool active)
    {
        if (normalStateObjects == null) return;
        foreach (var obj in normalStateObjects)
        {
            if (obj != null) obj.SetActive(active);
        }
    }

    private void OpenCodeWindow()
    {
        if (codeEditorWindow == null) return;
        codeEditorWindow.gameObject.SetActive(true);
        codeEditorWindow.transform.SetAsLastSibling();
    }
}