using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class DraggableWindow : MonoBehaviour, IPointerDownHandler
{
    [Header("窗口基础配置")]
    [SerializeField] private WindowLayer windowLayer = WindowLayer.Social;
    [SerializeField] private RectTransform dragHandle;
    [SerializeField] private bool clampToScreen = true;
    [SerializeField] private float screenPadding = 10f;

    [Header("异形边缘精准点击 (需贴图开启 Read/Write)")]
    [SerializeField] private Image[] alphaHitTestImages;
    [SerializeField, Range(0f, 1f)] private float alphaThreshold = 0.1f;

    public WindowLayer Layer => windowLayer;
    protected RectTransform RectTransform { get; private set; }
    protected CanvasGroup CanvasGroup { get; private set; }

    private Vector2 _lastPointerLocalPos;
    private bool _isDragging;

    protected virtual void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        CanvasGroup = GetComponent<CanvasGroup>();

        SetupAlphaHitTest();
        SetupDragHandle();
    }

    private void SetupAlphaHitTest()
    {
        if (alphaHitTestImages == null) return;
        foreach (var img in alphaHitTestImages)
        {
            if (img != null && img.sprite != null && img.sprite.texture.isReadable)
            {
                img.alphaHitTestMinimumThreshold = alphaThreshold;
            }
        }
    }

    private void SetupDragHandle()
    {
        if (dragHandle == null) return;

        EventTrigger trigger = dragHandle.GetComponent<EventTrigger>();
        if (trigger == null) trigger = dragHandle.gameObject.AddComponent<EventTrigger>();

        var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginDrag.callback.AddListener((data) => OnBeginDrag((PointerEventData)data));
        trigger.triggers.Add(beginDrag);

        var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        drag.callback.AddListener((data) => OnDrag((PointerEventData)data));
        trigger.triggers.Add(drag);

        var endDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endDrag.callback.AddListener((data) => _isDragging = false);
        trigger.triggers.Add(endDrag);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        FocusWindow();
    }

    public void FocusWindow()
    {
        if (WindowManager.Instance != null)
        {
            WindowManager.Instance.BringToFront(this);
        }
    }

    private void OnBeginDrag(PointerEventData eventData)
    {
        FocusWindow();
        RectTransform parentRect = RectTransform.parent as RectTransform;
        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out _lastPointerLocalPos))
        {
            _isDragging = true;
        }
    }

    private void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        RectTransform parentRect = RectTransform.parent as RectTransform;
        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out Vector2 currentPointerLocalPos))
        {
            // 采用增量位移，完全不受 Anchors 锚点位置影响
            Vector2 delta = currentPointerLocalPos - _lastPointerLocalPos;
            _lastPointerLocalPos = currentPointerLocalPos;

            RectTransform.anchoredPosition += delta;

            if (clampToScreen)
            {
                ClampWithinParent(parentRect);
            }
        }
    }

    private void ClampWithinParent(RectTransform parentRect)
    {
        Rect pRect = parentRect.rect;
        Rect wRect = RectTransform.rect;
        Vector2 pos = RectTransform.anchoredPosition;

        // 计算当前 anchoredPosition 原点相对于父容器中心的偏移
        Vector2 anchorCenter = (RectTransform.anchorMin + RectTransform.anchorMax) * 0.5f;
        Vector2 anchorOriginInParent = new Vector2(
            pRect.xMin + pRect.width * anchorCenter.x,
            pRect.yMin + pRect.height * anchorCenter.y
        );

        // 窗口四边在父容器坐标系下的真实边界
        float left = anchorOriginInParent.x + pos.x + wRect.xMin;
        float right = anchorOriginInParent.x + pos.x + wRect.xMax;
        float bottom = anchorOriginInParent.y + pos.y + wRect.yMin;
        float top = anchorOriginInParent.y + pos.y + wRect.yMax;

        // 仅当窗口小于父容器时才限制边界，防止大窗口被死锁
        if (wRect.width + screenPadding * 2f <= pRect.width)
        {
            if (left < pRect.xMin + screenPadding) pos.x += (pRect.xMin + screenPadding) - left;
            if (right > pRect.xMax - screenPadding) pos.x -= right - (pRect.xMax - screenPadding);
        }

        if (wRect.height + screenPadding * 2f <= pRect.height)
        {
            if (bottom < pRect.yMin + screenPadding) pos.y += (pRect.yMin + screenPadding) - bottom;
            if (top > pRect.yMax - screenPadding) pos.y -= top - (pRect.yMax - screenPadding);
        }

        RectTransform.anchoredPosition = pos;
    }

    public virtual void OpenWindow()
    {
        gameObject.SetActive(true);
        FocusWindow();
    }

    public virtual void CloseWindow()
    {
        gameObject.SetActive(false);
    }
}
