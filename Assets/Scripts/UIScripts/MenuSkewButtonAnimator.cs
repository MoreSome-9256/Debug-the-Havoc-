using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuSkewButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("子节点引用 (对应 Shadow 和 Button)")]
    [SerializeField] private RectTransform visualRootRect; // 包裹整体或留空默认用自身
    [SerializeField] private Image buttonBodyImage;        // 子物体 Button 的 Image
    [SerializeField] private Image shadowImage;            // 子物体 Shadow 的 Image

    [Header("整体尺寸缩放 (默认略小，悬停放大)")]
    [SerializeField] private float normalScale = 0.88f;
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float animDuration = 0.2f;

    [Header("Shadow 独立变色、放大与错位参数")]
    [SerializeField] private Color normalShadowColor = new Color(0.98f, 0.55f, 0.15f, 1f); // 警示橙
    [SerializeField] private Color hoverShadowColor = new Color(0.24f, 0.88f, 0.45f, 1f);  // 毒液绿
    [Tooltip("Shadow 在父节点整体缩放基础上额外放大的比例")]
    [SerializeField] private float shadowHoverExtraScale = 1.05f;
    [Tooltip("Shadow 相对按钮本体的额外错位偏移 (在自身局部坐标系下，比如向右下远离填 (12, -12))")]
    [SerializeField] private Vector2 shadowHoverOffset = new Vector2(12f, -12f);

    [Header("整体悬停微移与入场参数")]
    [Tooltip("沿自身局部 X 轴抽出的距离：右侧按钮填负数(-24)向左抽出，左下 Terminate 填正数(24)向右抽出")]
    [SerializeField] private float hoverSlideX = -24f;
    [Tooltip("入场时的初始局部偏移：右侧按钮填正数(450)从右侧切入，左下填负数(-450)从左侧切入")]
    [SerializeField] private float entranceOffsetX = 450f;
    [SerializeField] private float alphaThreshold = 0.05f;

    private RectTransform _selfRect;
    private RectTransform _shadowRect;
    private Vector3 _baseLocalPos;
    private Vector2 _baseShadowPos;
    private Sequence _hoverSeq;
    private bool _isInteractable = false;

    private void Awake()
    {
        _selfRect = GetComponent<RectTransform>();
        if (visualRootRect == null) visualRootRect = _selfRect;
        _baseLocalPos = _selfRect.localPosition;

        if (buttonBodyImage != null && buttonBodyImage.sprite != null && buttonBodyImage.sprite.texture.isReadable)
        {
            buttonBodyImage.alphaHitTestMinimumThreshold = alphaThreshold;
        }

        if (shadowImage != null)
        {
            _shadowRect = shadowImage.rectTransform;
            _baseShadowPos = _shadowRect.anchoredPosition;
            shadowImage.raycastTarget = false;
            shadowImage.color = normalShadowColor;
            _shadowRect.localScale = Vector3.one;
        }

        visualRootRect.localScale = Vector3.one * normalScale;
    }

    public Tween PlayEntrance(float duration)
    {
        _isInteractable = false;
        _selfRect.DOKill();
        visualRootRect.DOKill();

        visualRootRect.localScale = Vector3.one * normalScale;
        if (shadowImage != null)
        {
            shadowImage.color = normalShadowColor;
            _shadowRect.anchoredPosition = _baseShadowPos;
            _shadowRect.localScale = Vector3.one;
        }

        _selfRect.localPosition = _baseLocalPos + transform.right * entranceOffsetX;

        return _selfRect.DOLocalMove(_baseLocalPos, duration)
            .SetEase(Ease.OutQuart)
            .OnComplete(() => _isInteractable = true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isInteractable) return;

        _hoverSeq?.Kill();
        _hoverSeq = DOTween.Sequence();

        // 1. 整体放大并沿斜切方向抽出
        _hoverSeq.Join(visualRootRect.DOScale(hoverScale, animDuration).SetEase(Ease.OutBack, 1.35f));
        Vector3 targetPos = _baseLocalPos + transform.right * hoverSlideX;
        _hoverSeq.Join(_selfRect.DOLocalMove(targetPos, animDuration).SetEase(Ease.OutQuart));

        // 2. Shadow 独立变绿 + 额外放大 + 向反方向远离本体
        if (shadowImage != null && _shadowRect != null)
        {
            _hoverSeq.Join(shadowImage.DOColor(hoverShadowColor, animDuration * 0.85f).SetEase(Ease.OutQuad));
            _hoverSeq.Join(_shadowRect.DOScale(shadowHoverExtraScale, animDuration).SetEase(Ease.OutBack, 1.4f));
            _hoverSeq.Join(_shadowRect.DOAnchorPos(_baseShadowPos + shadowHoverOffset, animDuration).SetEase(Ease.OutBack, 1.35f));
        }
    }

    /// <summary>
    /// 从指定世界坐标（如 CMD 窗口中心）缩放弹出到原始排位
    /// </summary>
    public Tween PlayBurstFromWorldPos(Vector3 originWorldPos, float duration)
    {
        _isInteractable = false;
        _selfRect.DOKill();
        visualRootRect.DOKill();

        if (shadowImage != null)
        {
            shadowImage.color = normalShadowColor;
            _shadowRect.anchoredPosition = _baseShadowPos;
            _shadowRect.localScale = Vector3.one;
        }

        // 先把按钮瞬间挪到 CMD 中心并缩成 0
        _selfRect.position = originWorldPos;
        visualRootRect.localScale = Vector3.zero;

        Sequence seq = DOTween.Sequence();
        seq.Join(_selfRect.DOLocalMove(_baseLocalPos, duration).SetEase(Ease.OutBack, 1.15f));
        seq.Join(visualRootRect.DOScale(normalScale, duration * 0.85f).SetEase(Ease.OutBack, 1.3f));
        seq.OnComplete(() => _isInteractable = true);

        return seq;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_isInteractable) return;

        _hoverSeq?.Kill();
        _hoverSeq = DOTween.Sequence();

        // 1. 整体缩回并滑回原位
        _hoverSeq.Join(visualRootRect.DOScale(normalScale, animDuration * 0.9f).SetEase(Ease.OutQuad));
        _hoverSeq.Join(_selfRect.DOLocalMove(_baseLocalPos, animDuration * 0.9f).SetEase(Ease.OutQuad));

        // 2. Shadow 恢复橙色、原尺寸和原始贴合位置
        if (shadowImage != null && _shadowRect != null)
        {
            _hoverSeq.Join(shadowImage.DOColor(normalShadowColor, animDuration * 0.9f).SetEase(Ease.OutQuad));
            _hoverSeq.Join(_shadowRect.DOScale(1f, animDuration * 0.9f).SetEase(Ease.OutQuad));
            _hoverSeq.Join(_shadowRect.DOAnchorPos(_baseShadowPos, animDuration * 0.9f).SetEase(Ease.OutQuad));
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isInteractable) return;

        visualRootRect.DOKill(true);
        visualRootRect.DOPunchScale(new Vector3(-0.06f, -0.06f, 0f), 0.2f, 8, 0.6f);
    }
}