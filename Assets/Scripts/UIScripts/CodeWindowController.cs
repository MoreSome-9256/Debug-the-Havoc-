using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CodeWindowController : DraggableWindow
{
    [Header("顶部操作按钮")]
    [SerializeField] private Button btnRun;
    [SerializeField] private Button btnClose;

    [Header("左侧栏 Tabs (INFO / TOOLS)")]
    [SerializeField] private Button tabInfo;
    [SerializeField] private Button tabTools;
    [SerializeField] private RectTransform leftActiveCursor;
    [SerializeField] private TextMeshProUGUI txtTabInfo;
    [SerializeField] private TextMeshProUGUI txtTabTools;
    [SerializeField] private GameObject panelInfo;
    [SerializeField] private GameObject panelTools;

    [Header("上方绿框右侧 Tabs (CONSOLE / HISTORY)")]
    [SerializeField] private Button tabConsole;
    [SerializeField] private Button tabHistory;
    [SerializeField] private RectTransform rightActiveCursor;
    [SerializeField] private TextMeshProUGUI txtTabConsole;
    [SerializeField] private TextMeshProUGUI txtTabHistory;
    [SerializeField] private GameObject panelCodeEditor;
    [SerializeField] private GameObject panelHistory;

    [Header("Tab 滑块动画与文字颜色")]
    [SerializeField] private float tabSlideSpeed = 22f;
    [SerializeField] private Color activeTextColor = Color.white;
    [SerializeField] private Color inactiveTextColor = new Color(1f, 1f, 1f, 0.55f);

    [Header("代码输入与下方橙框输出")]
    [SerializeField] private TMP_InputField inputCodeEditor;
    [SerializeField] private RectTransform codeContentRect;
    [SerializeField] private TextMeshProUGUI txtOutputLog;
    [SerializeField] private ScrollRect scrollOutput;

    [Header("历史记录列表（Prefab 实例化）")]
    [SerializeField] private RectTransform contentHistory;
    [SerializeField] private CodeHistoryItemUI historyItemPrefab;

    [Header("窗口开关动效")]
    [SerializeField] private CanvasGroup windowCanvasGroup;
    [SerializeField] private float openDuration = 0.24f;
    [SerializeField] private float closeDuration = 0.16f;

    private Sequence _windowTweenSeq;

    public event Action<string> OnRunCodeClicked;

    private RectTransform _targetLeftTab;
    private RectTransform _targetRightTab;

    protected override void Awake()
    {
        base.Awake();

        BindEvents();

        if (inputCodeEditor != null)
        {
            inputCodeEditor.richText = false;
            inputCodeEditor.onValueChanged.AddListener(_ => RebuildCodeScrollLayout());
        }

        SwitchLeftTab(showInfo: true, immediate: true);
        SwitchEditorTab(showConsole: true, immediate: true);
    }

    private void BindEvents()
    {
        if (btnRun != null)
        {
            btnRun.onClick.AddListener(() =>
            {
                string code = inputCodeEditor != null ? inputCodeEditor.text : string.Empty;
                RecordToHistory(code);
                SwitchEditorTab(showConsole: true, immediate: false);
                OnRunCodeClicked?.Invoke(code);
            });
        }

        if (btnClose != null)
            btnClose.onClick.AddListener(CloseWindow);

        if (tabInfo != null) tabInfo.onClick.AddListener(() => SwitchLeftTab(true, false));
        if (tabTools != null) tabTools.onClick.AddListener(() => SwitchLeftTab(false, false));

        if (tabConsole != null) tabConsole.onClick.AddListener(() => SwitchEditorTab(true, false));
        if (tabHistory != null) tabHistory.onClick.AddListener(() => SwitchEditorTab(false, false));
    }

    public void SwitchLeftTab(bool showInfo, bool immediate = false)
    {
        if (panelInfo != null) panelInfo.SetActive(showInfo);
        if (panelTools != null) panelTools.SetActive(!showInfo);

        if (txtTabInfo != null) txtTabInfo.color = showInfo ? activeTextColor : inactiveTextColor;
        if (txtTabTools != null) txtTabTools.color = !showInfo ? activeTextColor : inactiveTextColor;

        _targetLeftTab = showInfo
            ? (tabInfo != null ? tabInfo.GetComponent<RectTransform>() : null)
            : (tabTools != null ? tabTools.GetComponent<RectTransform>() : null);

        if (immediate)
            SyncCursorPosition(leftActiveCursor, _targetLeftTab, immediate: true);
    }

    public void SwitchEditorTab(bool showConsole, bool immediate = false)
    {
        if (panelCodeEditor != null) panelCodeEditor.SetActive(showConsole);
        if (panelHistory != null) panelHistory.SetActive(!showConsole);

        if (txtTabConsole != null) txtTabConsole.color = showConsole ? activeTextColor : inactiveTextColor;
        if (txtTabHistory != null) txtTabHistory.color = !showConsole ? activeTextColor : inactiveTextColor;

        _targetRightTab = showConsole
            ? (tabConsole != null ? tabConsole.GetComponent<RectTransform>() : null)
            : (tabHistory != null ? tabHistory.GetComponent<RectTransform>() : null);

        if (immediate)
            SyncCursorPosition(rightActiveCursor, _targetRightTab, immediate: true);
    }

    private void RecordToHistory(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || contentHistory == null || historyItemPrefab == null)
            return;

        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        CodeHistoryItemUI item = Instantiate(historyItemPrefab, contentHistory);
        item.transform.SetAsFirstSibling();

        item.Setup(timestamp, code, restoredCode =>
        {
            if (inputCodeEditor != null)
            {
                inputCodeEditor.text = restoredCode;
                RebuildCodeScrollLayout();
            }
            SwitchEditorTab(showConsole: true, immediate: false);
        });

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentHistory);
    }

    private void RebuildCodeScrollLayout()
    {
        if (codeContentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(codeContentRect);
    }

    private void LateUpdate()
    {
        SyncCursorPosition(leftActiveCursor, _targetLeftTab, immediate: false);
        SyncCursorPosition(rightActiveCursor, _targetRightTab, immediate: false);
    }

    private void SyncCursorPosition(RectTransform cursor, RectTransform targetTab, bool immediate)
    {
        if (cursor == null || targetTab == null) return;

        Vector3 worldCenter = targetTab.TransformPoint(targetTab.rect.center);
        Vector3 localTarget = cursor.parent.InverseTransformPoint(worldCenter);

        Vector2 currentPos = cursor.anchoredPosition;
        if (immediate)
        {
            cursor.anchoredPosition = new Vector2(localTarget.x, localTarget.y);
        }
        else
        {
            float t = Time.deltaTime * tabSlideSpeed;
            cursor.anchoredPosition = new Vector2(
                Mathf.Lerp(currentPos.x, localTarget.x, t),
                Mathf.Lerp(currentPos.y, localTarget.y, t)
            );
        }
    }

    public void AppendOutputLog(string line)
    {
        if (txtOutputLog == null) return;
        txtOutputLog.text += (string.IsNullOrEmpty(txtOutputLog.text) ? "" : "\n") + line;

        if (scrollOutput != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollOutput.verticalNormalizedPosition = 0f;
        }
    }

    public void ClearOutput()
    {
        if (txtOutputLog != null) txtOutputLog.text = string.Empty;
    }

    public override void OpenWindow()
    {
        // 如果已经处于激活且完整展开状态，直接置顶并轻微跳动提示即可
        if (gameObject.activeSelf && (_windowTweenSeq == null || !_windowTweenSeq.IsActive()) && transform.localScale.y > 0.99f)
        {
            transform.SetAsLastSibling();
            transform.DOKill();
            transform.DOPunchScale(new Vector3(0.015f, 0.015f, 0f), 0.2f, 6, 0.5f);
            return;
        }

        // 调用父类方法激活物体并置顶层级
        base.OpenWindow();

        _windowTweenSeq?.Kill();
        _windowTweenSeq = DOTween.Sequence();

        // 初始状态：压扁成一条水平扫描线 + 透明
        transform.localScale = new Vector3(0.82f, 0.03f, 1f);
        if (windowCanvasGroup != null)
        {
            windowCanvasGroup.alpha = 0f;
            windowCanvasGroup.blocksRaycasts = false;
        }

        float step1 = openDuration * 0.35f;
        _windowTweenSeq.Append(transform.DOScaleX(1f, step1).SetEase(Ease.OutQuart));
        if (windowCanvasGroup != null)
            _windowTweenSeq.Join(windowCanvasGroup.DOFade(1f, step1));

        float step2 = openDuration * 0.65f;
        _windowTweenSeq.Append(transform.DOScaleY(1f, step2).SetEase(Ease.OutBack, 1.15f));

        _windowTweenSeq.OnComplete(() =>
        {
            if (windowCanvasGroup != null)
                windowCanvasGroup.blocksRaycasts = true;
        });
    }

    public override void CloseWindow()
    {
        if (!gameObject.activeSelf) return;

        _windowTweenSeq?.Kill();
        _windowTweenSeq = DOTween.Sequence();

        if (windowCanvasGroup != null)
            windowCanvasGroup.blocksRaycasts = false;

        _windowTweenSeq.Join(transform.DOScaleY(0.02f, closeDuration).SetEase(Ease.InQuart))
                       .Join(transform.DOScaleX(0.9f, closeDuration).SetEase(Ease.InQuad));

        if (windowCanvasGroup != null)
            _windowTweenSeq.Join(windowCanvasGroup.DOFade(0f, closeDuration * 0.9f));

        _windowTweenSeq.OnComplete(() =>
        {
            base.CloseWindow();
            transform.localScale = Vector3.one;
            if (windowCanvasGroup != null)
            {
                windowCanvasGroup.alpha = 1f;
                windowCanvasGroup.blocksRaycasts = true;
            }
        });
    }
}