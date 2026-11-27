using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ModeSelectionController : MonoBehaviour
{
    [Header("面板与遮罩")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private RectTransform mainPanelRect;
    [SerializeField] private Button backdropCloseBtn;

    [Header("模式选项列表")]
    [SerializeField] private ModeOptionItem[] modeItems;

    [Header("共用绿色高亮底框（Shared_Hover_GreenBg）")]
    [SerializeField] private RectTransform sharedGreenHighlight;
    [SerializeField] private float betweenButtonsScale = 0.78f; // 离开按钮移动时的缩小比例
    [SerializeField] private float snapDuration = 0.16f;        // 吸附到按钮的耗时
    [SerializeField] private float pointerFollowLerp = 18f;     // 跟随鼠标的平滑系数
    [Tooltip("在两按钮坐标极值基础上向外扩展的额外边距（默认 0 即严格限制在两按钮中心构成的矩形内）")]
    [SerializeField] private Vector2 boundsPadding = Vector2.zero;

    [Header("侧边临时说明弹窗（Side_Tooltip_Panel）")]
    [SerializeField] private RectTransform tooltipPanelRect;
    [SerializeField] private CanvasGroup tooltipCanvasGroup;
    [SerializeField] private RectTransform tooltipBgVisual;
    [SerializeField] private TMP_Text txtModeDesc;

    [Header("选定模式后的场景切换")]
    [SerializeField] private GameObject mainMenuLayer;     // 拖入 Layer_MainMenu
    [SerializeField] private GameObject chatWindowLayer;   // 拖入聊天窗口层级（如 Layer_Social / Layer_Workspace）
    [Tooltip("可在此直接绑定聊天控制器的启动方法，参数为 sync 或 overclock")]
    [SerializeField] private UnityEvent<string> onModeConfirmed;

    private ModeOptionItem _activeOptionItem;
    private CanvasGroup _greenHighlightGroup;
    private RectTransform _highlightParentRect;
    private Canvas _parentCanvas;
    private bool _isOpen;

    private void Awake()
    {
        if (sharedGreenHighlight != null)
        {
            _highlightParentRect = sharedGreenHighlight.parent as RectTransform;
            _parentCanvas = GetComponentInParent<Canvas>();

            // 自动获取或补全 CanvasGroup，并确保不拦截鼠标射线
            _greenHighlightGroup = sharedGreenHighlight.GetComponent<CanvasGroup>();
            if (_greenHighlightGroup == null)
            {
                _greenHighlightGroup = sharedGreenHighlight.gameObject.AddComponent<CanvasGroup>();
            }
            _greenHighlightGroup.blocksRaycasts = false;
            _greenHighlightGroup.interactable = false;
        }

        if (modeItems != null)
        {
            foreach (var item in modeItems)
            {
                if (item != null) item.Init(this);
            }
        }

        if (backdropCloseBtn != null)
        {
            backdropCloseBtn.onClick.RemoveAllListeners();
            backdropCloseBtn.onClick.AddListener(Close);
        }

        if (tooltipPanelRect != null && tooltipCanvasGroup == null)
        {
            tooltipCanvasGroup = tooltipPanelRect.GetComponent<CanvasGroup>();
            if (tooltipCanvasGroup == null)
            {
                tooltipCanvasGroup = tooltipPanelRect.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (tooltipCanvasGroup != null)
        {
            tooltipCanvasGroup.alpha = 0f;
            tooltipCanvasGroup.blocksRaycasts = false;
            tooltipCanvasGroup.interactable = false;
        }

        SetGreenHighlightVisible(false, instant: true);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_isOpen || sharedGreenHighlight == null || _highlightParentRect == null) return;

        // 鼠标在两个按钮之间移动（未悬停任一按钮）时，缩小并在两按钮极值矩形内平滑随光标移动
        if (_activeOptionItem == null && _greenHighlightGroup != null && _greenHighlightGroup.alpha > 0.01f)
        {
            Camera cam = (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _parentCanvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _highlightParentRect, Input.mousePosition, cam, out Vector2 mouseLocalPos) &&
                TryGetHighlightLocalBounds(out Vector2 minBounds, out Vector2 maxBounds))
            {
                Vector3 targetLocalPos = new Vector3(
                    Mathf.Clamp(mouseLocalPos.x, minBounds.x - boundsPadding.x, maxBounds.x + boundsPadding.x),
                    Mathf.Clamp(mouseLocalPos.y, minBounds.y - boundsPadding.y, maxBounds.y + boundsPadding.y),
                    sharedGreenHighlight.localPosition.z
                );

                float t = 1f - Mathf.Exp(-pointerFollowLerp * Time.unscaledDeltaTime);
                sharedGreenHighlight.localPosition = Vector3.Lerp(sharedGreenHighlight.localPosition, targetLocalPos, t);
            }
        }
    }

    /// <summary>
    /// 计算所有模式按钮在 _highlightParentRect 局部空间下的 X/Y 最小值与最大值
    /// </summary>
    private bool TryGetHighlightLocalBounds(out Vector2 minBounds, out Vector2 maxBounds)
    {
        minBounds = new Vector2(float.MaxValue, float.MaxValue);
        maxBounds = new Vector2(float.MinValue, float.MinValue);
        bool hasValidItem = false;

        if (modeItems != null)
        {
            for (int i = 0; i < modeItems.Length; i++)
            {
                if (modeItems[i] == null) continue;

                Vector3 itemLocal = _highlightParentRect.InverseTransformPoint(modeItems[i].HighlightWorldPos);
                if (itemLocal.x < minBounds.x) minBounds.x = itemLocal.x;
                if (itemLocal.y < minBounds.y) minBounds.y = itemLocal.y;
                if (itemLocal.x > maxBounds.x) maxBounds.x = itemLocal.x;
                if (itemLocal.y > maxBounds.y) maxBounds.y = itemLocal.y;
                hasValidItem = true;
            }
        }

        return hasValidItem;
    }

    public void Open()
    {
        gameObject.SetActive(true);
        _isOpen = true;
        _activeOptionItem = null;

        if (modeItems != null)
        {
            foreach (var item in modeItems) item?.ResetVisualImmediate();
        }

        SetGreenHighlightVisible(false, instant: true);
        HideTooltipImmediate();

        rootGroup.DOKill();
        rootGroup.alpha = 0f;
        rootGroup.interactable = true;
        rootGroup.blocksRaycasts = true;
        rootGroup.DOFade(1f, 0.18f);

        if (mainPanelRect != null)
        {
            mainPanelRect.DOKill();
            mainPanelRect.localScale = Vector3.one * 0.9f;
            mainPanelRect.DOScale(1f, 0.24f).SetEase(Ease.OutBack, 1.2f);
        }
    }

    public void Close()
    {
        _isOpen = false;
        _activeOptionItem = null;
        SetGreenHighlightVisible(false, instant: false);
        HideTooltipImmediate();

        rootGroup.DOKill();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        rootGroup.DOFade(0f, 0.15f).OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    public void OnOptionEnter(ModeOptionItem item)
    {
        if (item == null) return;
        _activeOptionItem = item;

        if (sharedGreenHighlight != null)
        {
            bool wasHidden = _greenHighlightGroup != null && _greenHighlightGroup.alpha < 0.05f;
            SetGreenHighlightVisible(true, instant: false);
            sharedGreenHighlight.DOKill();

            if (wasHidden)
            {
                // 首次移入按钮时，直接定位到按钮下方并从小到大弹出，避免从远处飞入
                sharedGreenHighlight.position = item.HighlightWorldPos;
                sharedGreenHighlight.localScale = Vector3.one * 0.65f;
            }
            else
            {
                sharedGreenHighlight.DOMove(item.HighlightWorldPos, snapDuration).SetEase(Ease.OutBack, 1.25f);
            }

            sharedGreenHighlight.DOScale(Vector3.one, snapDuration).SetEase(Ease.OutBack, 1.3f);
        }

        ShowTooltip(item);
    }

    public void OnOptionExit(ModeOptionItem item)
    {
        if (_activeOptionItem != item) return;
        _activeOptionItem = null;

        if (sharedGreenHighlight != null)
        {
            sharedGreenHighlight.DOKill();
            sharedGreenHighlight.DOScale(Vector3.one * betweenButtonsScale, 0.14f).SetEase(Ease.OutQuad);
        }

        HideTooltip(item);
    }

    private void SetGreenHighlightVisible(bool visible, bool instant)
    {
        if (_greenHighlightGroup == null) return;
        _greenHighlightGroup.DOKill();

        if (instant)
        {
            _greenHighlightGroup.alpha = visible ? 1f : 0f;
            if (visible && sharedGreenHighlight != null)
                sharedGreenHighlight.localScale = Vector3.one * betweenButtonsScale;
        }
        else
        {
            _greenHighlightGroup.DOFade(visible ? 1f : 0f, 0.12f);
        }
    }

    private void ShowTooltip(ModeOptionItem item)
    {
        if (item == null || tooltipPanelRect == null) return;

        // 左侧展开：Pivot 设在右边缘 (1, 0.5)，底图不翻转；右侧展开：Pivot 设在左边缘 (0, 0.5)，底图水平镜像
        if (item.popupSide == TooltipSide.Left)
        {
            tooltipPanelRect.pivot = new Vector2(1f, 0.5f);
            if (tooltipBgVisual != null) tooltipBgVisual.localScale = new Vector3(1f, 1f, 1f);
        }
        else
        {
            tooltipPanelRect.pivot = new Vector2(0f, 0.5f);
            if (tooltipBgVisual != null) tooltipBgVisual.localScale = new Vector3(-1f, 1f, 1f);
        }

        if (item.popupAnchor != null)
        {
            tooltipPanelRect.position = item.popupAnchor.position;
        }

        if (txtModeDesc != null)
        {
            txtModeDesc.text = LocalizationManager.GetText(item.descLocalizationKey);
        }

        tooltipPanelRect.DOKill();
        tooltipCanvasGroup.DOKill();

        tooltipPanelRect.localScale = new Vector3(0f, 1f, 1f);
        tooltipPanelRect.DOScaleX(1f, 0.2f).SetEase(Ease.OutQuart);
        tooltipCanvasGroup.DOFade(1f, 0.15f);
    }

    private void HideTooltip(ModeOptionItem item)
    {
        if (_activeOptionItem != null) return;

        if (tooltipPanelRect != null)
        {
            tooltipPanelRect.DOKill();
            tooltipPanelRect.DOScaleX(0f, 0.14f).SetEase(Ease.InQuad);
        }
        if (tooltipCanvasGroup != null)
        {
            tooltipCanvasGroup.DOKill();
            tooltipCanvasGroup.DOFade(0f, 0.12f);
        }
    }

    private void HideTooltipImmediate()
    {
        if (tooltipPanelRect != null)
        {
            tooltipPanelRect.DOKill();
            tooltipPanelRect.localScale = new Vector3(0f, 1f, 1f);
        }
        if (tooltipCanvasGroup != null)
        {
            tooltipCanvasGroup.DOKill();
            tooltipCanvasGroup.alpha = 0f;
        }
    }

    public void SelectMode(string modeId)
    {
        _isOpen = false;
        _activeOptionItem = null;

        // 1. 持久化当前存档选择的模式（"sync" 或 "overclock"）
        GameSaveData newSave = SaveManager.CreateNewSave(modeId);
        PlayerPrefs.Save();

        // 2. 锁定交互并淡出模式选择层
        rootGroup.DOKill();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        SetGreenHighlightVisible(false, instant: false);
        HideTooltipImmediate();

        rootGroup.DOFade(0f, 0.16f).OnComplete(() =>
        {
            gameObject.SetActive(false);

            // 3. 隐藏主菜单，正式唤醒聊天窗口并通知游戏系统当前模式
            if (mainMenuLayer != null) mainMenuLayer.SetActive(false);
            if (chatWindowLayer != null) chatWindowLayer.SetActive(true);

            onModeConfirmed?.Invoke(modeId);
        });
    }
}