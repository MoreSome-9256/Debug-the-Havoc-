using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class SessionData
{
    public string sessionId;
    public string displayName;
    public Sprite avatarSprite;
    public double lastMessageTime;
}

[RequireComponent(typeof(LayoutElement))]
public class SessionItemUI : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private Button btnClick;
    [SerializeField] private Image imgAvatar;
    [SerializeField] private Mask avatarMask;
    [SerializeField] private RectTransform avatarVisualRoot;
    [SerializeField] private GameObject unreadBadge;

    [Header("尺寸与间距配置 (基于 4K 画布)")]
    [SerializeField] private float normalSlotHeight = 110f;   // 未选中时的占位高度
    [SerializeField] private float selectedSlotHeight = 185f; // 选中时的占位高度（自动推开上下头像）
    [SerializeField] private float selectedVisualScale = 1.3f;

    public SessionData Data { get; private set; }
    public bool IsGroup { get; private set; }
    public RectTransform RectTransform { get; private set; }

    private LayoutElement _layoutElement;
    private float _targetSlotHeight;
    private Vector3 _targetScale;
    private RectTransform _parentContentRect;

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        _layoutElement = GetComponent<LayoutElement>();
        _parentContentRect = transform.parent as RectTransform;

        _targetSlotHeight = normalSlotHeight;
        _targetScale = Vector3.one;
        _layoutElement.preferredHeight = normalSlotHeight;
    }

    public void Setup(SessionData data, bool isGroup, Action onClickCallback)
    {
        Data = data;
        IsGroup = isGroup;
        _parentContentRect = transform.parent as RectTransform;

        if (imgAvatar != null && data.avatarSprite != null)
            imgAvatar.sprite = data.avatarSprite;

        // 大群图标若为异形 Logo，可自动关闭圆形遮罩
        // if (avatarMask != null)
        //     avatarMask.enabled = !isGroup;

        if (btnClick != null)
        {
            btnClick.onClick.RemoveAllListeners();
            btnClick.onClick.AddListener(() => onClickCallback?.Invoke());
        }
        SetUnread(false);
    }

    public void SetSelectedVisual(bool selected, bool immediate = false)
    {
        _targetSlotHeight = selected ? selectedSlotHeight : normalSlotHeight;
        _targetScale = Vector3.one * (selected ? selectedVisualScale : 1f);

        if (immediate)
        {
            _layoutElement.preferredHeight = _targetSlotHeight;
            if (avatarVisualRoot != null) avatarVisualRoot.localScale = _targetScale;
        }
    }

    public void SetUnread(bool hasUnread)
    {
        if (unreadBadge != null)
            unreadBadge.SetActive(hasUnread);
    }

    private void Update()
    {
        // 1. 平滑过渡视觉缩放
        if (avatarVisualRoot != null)
        {
            avatarVisualRoot.localScale = Vector3.Lerp(avatarVisualRoot.localScale, _targetScale, Time.deltaTime * 15f);
        }

        // 2. 平滑过渡布局占位高度，实时推开或收拢上下头像间距
        if (Mathf.Abs(_layoutElement.preferredHeight - _targetSlotHeight) > 0.1f)
        {
            _layoutElement.preferredHeight = Mathf.Lerp(_layoutElement.preferredHeight, _targetSlotHeight, Time.deltaTime * 15f);
            if (_parentContentRect != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(_parentContentRect);
            }
        }
    }
}