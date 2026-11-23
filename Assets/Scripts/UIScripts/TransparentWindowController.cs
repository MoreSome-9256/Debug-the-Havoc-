using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

public class TransparentWindowController : MonoBehaviour
{
    public static TransparentWindowController Instance { get; private set; }

    [Header("核心引用")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("仅在 Unity 编辑器中显示的辅助预览背景，打包后自动隐藏")]
    [SerializeField] private GameObject editorPreviewBackdrop;

    [Header("窗口行为配置")]
    [SerializeField] private bool startTopmost = true;
    [SerializeField] private int activeFPS = 60;
    [SerializeField] private int unfocusedFPS = 20;

    public bool IsTopmost { get; private set; }
    public bool IsPointerOverUI { get; private set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    #region Win32 API 互操作声明
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;

    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_TOOLWINDOW = 0x00000080; // 可选：隐藏 Alt+Tab 缩略图，仅保留系统托盘

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private IntPtr _hWnd;
    private bool _currentClickThroughState = false;
    #endregion
#endif

    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
    private PointerEventData _pointerEventData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (mainCamera == null) mainCamera = Camera.main;

        Application.runInBackground = true;
        Application.targetFrameRate = activeFPS;

        InitializeCamera();
        InitializeWindowStyle();
    }

    private void InitializeCamera()
    {
        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            // 关键：Alpha 必须为 0，DWM 才会将其合成为全透明
            mainCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        if (editorPreviewBackdrop != null)
        {
#if UNITY_EDITOR
            editorPreviewBackdrop.SetActive(true);
#else
            editorPreviewBackdrop.SetActive(false);
#endif
        }
    }

    private void InitializeWindowStyle()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        _hWnd = GetActiveWindow();

        // 1. 强制无边框弹出窗口样式
        SetWindowLong(_hWnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        // 2. 开启分层窗口属性（支持每像素 Alpha）
        uint exStyle = GetWindowLong(_hWnd, GWL_EXSTYLE);
        SetWindowLong(_hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);

        // 3. 扩展 DWM 玻璃区域至整个客户区 (-1 表示全覆盖)
        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(_hWnd, ref margins);

        // 4. 避免全屏独占触发任务栏遮挡：覆盖当前主显示器工作区，或高度减 1px
        int width = Display.main.systemWidth;
        int height = Display.main.systemHeight - 1;
        SetWindowPos(_hWnd, startTopmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, width, height, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        IsTopmost = startTopmost;
#endif
    }

    private void Update()
    {
        // 实时检测鼠标是否悬停在有效 UGUI 控件上
        IsPointerOverUI = CheckPointerOverUI();

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // 当没有命中任何 UI 时，开启鼠标穿透
        SetClickThrough(!IsPointerOverUI);
#endif
    }

    /// <summary>
    /// 使用 GraphicRaycaster 精确检测鼠标下是否有 RaycastTarget = true 的 UI
    /// </summary>
    private bool CheckPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        if (_pointerEventData == null)
            _pointerEventData = new PointerEventData(EventSystem.current);

        _pointerEventData.position = Input.mousePosition;
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(_pointerEventData, _raycastResults);

        return _raycastResults.Count > 0;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private void SetClickThrough(bool clickThrough)
    {
        if (_currentClickThroughState == clickThrough) return;
        _currentClickThroughState = clickThrough;

        uint exStyle = GetWindowLong(_hWnd, GWL_EXSTYLE);
        if (clickThrough)
        {
            SetWindowLong(_hWnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
        }
        else
        {
            SetWindowLong(_hWnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
        }
    }
#endif

    /// <summary>
    /// 供 UI 顶栏“图钉/置顶”按钮调用
    /// </summary>
    public void SetTopmost(bool topmost)
    {
        IsTopmost = topmost;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        SetWindowPos(_hWnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
#endif
    }

    public void ToggleTopmost() => SetTopmost(!IsTopmost);

    private void OnApplicationFocus(bool hasFocus)
    {
        // 失焦挂机时自动降帧，大幅降低 GPU 占用
        Application.targetFrameRate = hasFocus ? activeFPS : unfocusedFPS;
    }
}
