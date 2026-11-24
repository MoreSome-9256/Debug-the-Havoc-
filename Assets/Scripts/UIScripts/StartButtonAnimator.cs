using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StartButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("悬停两层阴影 (使用绿底板同款切图改灰)")]
    [SerializeField] private RectTransform mainBodyRect;
    [SerializeField] private RectTransform shadowLightRect;
    [SerializeField] private CanvasGroup shadowLightGroup;
    [SerializeField] private RectTransform shadowDeepRect;
    [SerializeField] private CanvasGroup shadowDeepGroup;

    [Header("悬停偏移参数")]
    [SerializeField] private Vector2 bodyHoverOffset = new Vector2(4f, 4f);
    [SerializeField] private Vector2 shadow1Offset = new Vector2(-10f, -14f);
    [SerializeField] private Vector2 shadow2Offset = new Vector2(-20f, -28f);
    [SerializeField] private float hoverDuration = 0.2f;

    [Header("点击中心浅色扩散 (放在带 Mask 的 Main_Body 下)")]
    [SerializeField] private RectTransform rippleCircleRect;
    [SerializeField] private Image rippleCircleImage;
    [SerializeField] private float rippleMaxScale = 3.2f;
    [SerializeField] private float rippleDuration = 0.38f;

    [Header("点击顺时针橙色描边 (Image Type = Filled / Radial 360)")]
    [SerializeField] private Image orangeOutlineImage;
    [SerializeField] private float outlineFillDuration = 0.32f;
    [SerializeField] private float outlineHoldDuration = 0.25f;

    private Sequence _hoverSeq;
    private Sequence _clickSeq;
    private Vector2 _initBodyPos;
    private Vector2 _initShadow1Pos;
    private Vector2 _initShadow2Pos;

    private void Awake()
    {
        if (mainBodyRect != null) _initBodyPos = mainBodyRect.anchoredPosition;
        if (shadowLightRect != null) _initShadow1Pos = shadowLightRect.anchoredPosition;
        if (shadowDeepRect != null) _initShadow2Pos = shadowDeepRect.anchoredPosition;

        ResetAllImmediate();
    }

    private void ResetAllImmediate()
    {
        if (shadowLightGroup != null) shadowLightGroup.alpha = 0f;
        if (shadowDeepGroup != null) shadowDeepGroup.alpha = 0f;

        if (rippleCircleRect != null) rippleCircleRect.localScale = Vector3.zero;
        if (rippleCircleImage != null)
        {
            Color c = rippleCircleImage.color;
            c.a = 0f;
            rippleCircleImage.color = c;
        }

        if (orangeOutlineImage != null)
        {
            orangeOutlineImage.fillAmount = 0f;
            Color c = orangeOutlineImage.color;
            c.a = 1f;
            orangeOutlineImage.color = c;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hoverSeq?.Kill();
        _hoverSeq = DOTween.Sequence();

        // 本体轻微向右上浮起
        if (mainBodyRect != null)
            _hoverSeq.Join(mainBodyRect.DOAnchorPos(_initBodyPos + bodyHoverOffset, hoverDuration).SetEase(Ease.OutQuart));

        // 第一层浅灰阴影展开
        if (shadowLightRect != null)
            _hoverSeq.Join(shadowLightRect.DOAnchorPos(_initShadow1Pos + shadow1Offset, hoverDuration).SetEase(Ease.OutBack, 1.1f));
        if (shadowLightGroup != null)
            _hoverSeq.Join(shadowLightGroup.DOFade(0.65f, hoverDuration * 0.8f));

        // 第二层深灰阴影错位延迟一点展开，形成层次感
        if (shadowDeepRect != null)
            _hoverSeq.Insert(0.03f, shadowDeepRect.DOAnchorPos(_initShadow2Pos + shadow2Offset, hoverDuration).SetEase(Ease.OutBack, 1.15f));
        if (shadowDeepGroup != null)
            _hoverSeq.Insert(0.03f, shadowDeepGroup.DOFade(0.45f, hoverDuration * 0.8f));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hoverSeq?.Kill();
        _hoverSeq = DOTween.Sequence();

        if (mainBodyRect != null)
            _hoverSeq.Join(mainBodyRect.DOAnchorPos(_initBodyPos, hoverDuration * 0.85f).SetEase(Ease.OutQuad));

        if (shadowLightRect != null)
            _hoverSeq.Join(shadowLightRect.DOAnchorPos(_initShadow1Pos, hoverDuration * 0.85f).SetEase(Ease.OutQuad));
        if (shadowLightGroup != null)
            _hoverSeq.Join(shadowLightGroup.DOFade(0f, hoverDuration * 0.75f));

        if (shadowDeepRect != null)
            _hoverSeq.Join(shadowDeepRect.DOAnchorPos(_initShadow2Pos, hoverDuration * 0.85f).SetEase(Ease.OutQuad));
        if (shadowDeepGroup != null)
            _hoverSeq.Join(shadowDeepGroup.DOFade(0f, hoverDuration * 0.75f));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayClickEffect();
    }

    public void PlayClickEffect()
    {
        _clickSeq?.Kill();
        _clickSeq = DOTween.Sequence();

        // 1. 本体轻微受击按压回弹
        if (mainBodyRect != null)
        {
            mainBodyRect.DOKill(true);
            mainBodyRect.DOPunchScale(new Vector3(-0.04f, -0.04f, 0f), 0.22f, 6, 0.5f);
        }

        // 2. 中心浅色圆形波纹扩散并渐隐（被 Main_Body 的 Mask 自动裁成斜切椭圆）
        if (rippleCircleRect != null && rippleCircleImage != null)
        {
            rippleCircleRect.localScale = Vector3.zero;
            Color c = rippleCircleImage.color;
            c.a = 0.55f;
            rippleCircleImage.color = c;

            _clickSeq.Join(rippleCircleRect.DOScale(rippleMaxScale, rippleDuration).SetEase(Ease.OutQuart));
            _clickSeq.Join(rippleCircleImage.DOFade(0f, rippleDuration).SetEase(Ease.InQuad));
        }

        // 3. 橙色细线描边顺时针绕一圈填满 -> 短暂停留后淡出复位
        if (orangeOutlineImage != null)
        {
            orangeOutlineImage.fillAmount = 0f;
            Color oc = orangeOutlineImage.color;
            oc.a = 1f;
            orangeOutlineImage.color = oc;

            _clickSeq.Join(orangeOutlineImage.DOFillAmount(1f, outlineFillDuration).SetEase(Ease.OutCubic));
            _clickSeq.AppendInterval(outlineHoldDuration);
            _clickSeq.Append(orangeOutlineImage.DOFade(0f, 0.18f));
            _clickSeq.OnComplete(() => orangeOutlineImage.fillAmount = 0f);
        }
    }
}