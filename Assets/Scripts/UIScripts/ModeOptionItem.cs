using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum TooltipSide { Left, Right }

[RequireComponent(typeof(Button))]
public class ModeOptionItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("模式配置")]
    public string modeId = "sync";
    public string descLocalizationKey = "mode_sync_desc";
    public TooltipSide popupSide = TooltipSide.Left;
    [Tooltip("拖入对应的 Anchor_Left 或 Anchor_Right")]
    public RectTransform popupAnchor;

    [Header("绿色底框吸附点（留空则默认对齐按钮自身中心）")]
    [SerializeField] private RectTransform highlightAnchor;

    [Header("悬停缩放目标（留空则自动缩放按钮自身）")]
    [SerializeField] private RectTransform scaleTarget;

    public Vector3 HighlightWorldPos =>
        highlightAnchor != null ? highlightAnchor.position : transform.position;

    private ModeSelectionController _controller;
    private Button _btn;

    public void Init(ModeSelectionController controller)
    {
        _controller = controller;
        _btn = GetComponent<Button>();

        if (scaleTarget == null)
        {
            scaleTarget = GetComponent<RectTransform>();
        }

        // 若按钮图片开启了 Read/Write，则按斜切轮廓精准响应鼠标悬停
        Image img = GetComponent<Image>();
        if (img != null && img.sprite != null && img.sprite.texture != null && img.sprite.texture.isReadable)
        {
            img.alphaHitTestMinimumThreshold = 0.1f;
        }

        _btn.onClick.RemoveAllListeners();
        _btn.onClick.AddListener(() => _controller.SelectMode(modeId));

        ResetVisualImmediate();
    }

    public void ResetVisualImmediate()
    {
        if (scaleTarget != null)
        {
            scaleTarget.DOKill();
            scaleTarget.localScale = Vector3.one;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (scaleTarget != null)
        {
            scaleTarget.DOKill();
            scaleTarget.DOScale(1.04f, 0.15f).SetEase(Ease.OutQuad);
        }

        _controller?.OnOptionEnter(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (scaleTarget != null)
        {
            scaleTarget.DOKill();
            scaleTarget.DOScale(1f, 0.14f).SetEase(Ease.OutQuad);
        }

        _controller?.OnOptionExit(this);
    }
}