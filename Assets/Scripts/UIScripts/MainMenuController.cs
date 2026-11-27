using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("主菜单核心视觉元素")]
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField] private RectTransform titleLogoRect;
    [SerializeField] private RectTransform mascotCatRect;
    [SerializeField] private Button btnMascotCat;
    [SerializeField] private MenuSkewButtonAnimator[] menuButtons;

    [Header("破窗出场：黑猫 Glitch 与斜上方跳跃")]
    [SerializeField] private Material glitchMaterialTemplate;   // 拖入之前的 Mat_UI_Glitch
    [Tooltip("斜上方冲刺的额外过冲偏移（正 X 向右、正 Y 向上）")]
    [SerializeField] private float catLeapDuration = 0.65f;     // 黑猫从左下飞跃到主菜单原位的总时长（秒）
    [SerializeField] private float catGlitchDuration = 0.45f;   // 同步拉长一点 Glitch 覆盖整个起跳过程

    private Material _catGlitchMat;
    private static readonly int GlitchIntensityId = Shader.PropertyToID("_GlitchIntensity");

    [Header("功能按钮事件绑定")]
    [SerializeField] private Button btnInitialize;   // 新建连接
    [SerializeField] private Button btnReconnect;    // 继续潜入
    [SerializeField] private Button btnConfig;       // 系统配置
    [SerializeField] private Button btnArchive;      // 内部档案
    [SerializeField] private Button btnTerminate;    // 断开连接

    [Header("进入游戏后的桌面窗口")]
    [SerializeField] private ChatWindowController chatWindow;
    [SerializeField] private ModeSelectionController modeSelectionController;

    private Sequence _entranceSeq;
    private Tween _catFloatTween;
    private Tween _catTiltTween;
    private Vector2 _catBasePos;

    [Header("可选：背景底框与视差联动")]
    [SerializeField] private RectTransform window1Rect;
    [SerializeField] private MenuParallaxController parallaxController;

    private Vector3 _titleBaseLocalPos;
    private Vector3 _catBaseLocalPos;
    private Vector3 _window1BaseLocalPos;
    private bool _isBurstTriggered = false;

    private void Awake()
    {
        if (titleLogoRect != null) _titleBaseLocalPos = titleLogoRect.localPosition;
        if (mascotCatRect != null)
        {
            _catBaseLocalPos = mascotCatRect.localPosition;
            _catBasePos = mascotCatRect.anchoredPosition;
        }
        if (window1Rect != null) _window1BaseLocalPos = window1Rect.localPosition;

        // 自动获取 Cat 节点下的所有 Image（含橙绿阴影和本体），统一挂上同一个 Glitch 材质实例
        if (glitchMaterialTemplate != null && mascotCatRect != null)
        {
            _catGlitchMat = new Material(glitchMaterialTemplate);
            _catGlitchMat.SetFloat(GlitchIntensityId, 0f);

            Image[] catImages = mascotCatRect.GetComponentsInChildren<Image>(true);
            foreach (var img in catImages)
            {
                img.material = _catGlitchMat;
            }
        }
    }
    private void OnDestroy()
    {
        if (_catGlitchMat != null) Destroy(_catGlitchMat);
    }

    private void Start()
    {
        if (mascotCatRect != null)
        {
            Image catImg = mascotCatRect.GetComponent<Image>();
            if (catImg != null && catImg.sprite != null && catImg.sprite.texture.isReadable)
                catImg.alphaHitTestMinimumThreshold = 0.05f;
        }

        BindButtonEvents();

        // 如果不是由 CMD 破窗触发的（例如二次启动跳过开场），才走常规入场
        if (!_isBurstTriggered)
        {
            PlayMenuEntrance();
        }
    }

    /// <summary>
    /// 破窗瞬间调用：所有主菜单元素从 CMD 窗口中心错峰弹出
    /// </summary>
    public void PlayBurstEntranceFromWorldPos(Vector3 originWorldPos)
    {
        _isBurstTriggered = true;
        _entranceSeq?.Kill();
        _entranceSeq = DOTween.Sequence();

        if (parallaxController != null) parallaxController.SetParallaxEnabled(false);

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.blocksRaycasts = true;
        }

        // 1. 黑猫带着 Glitch 从左下斜向上飞跃到主菜单原位
        if (mascotCatRect != null)
        {
            if (_catGlitchMat != null)
            {
                StartCoroutine(PlayCatGlitchBurst(catGlitchDuration, 1.0f));
            }

            mascotCatRect.position = originWorldPos;
            mascotCatRect.localScale = Vector3.one * 0.65f;
            mascotCatRect.localRotation = Quaternion.Euler(0f, 0f, -15f);

            // 位移、缩放、旋转按 catLeapDuration 同步推进
            _entranceSeq.Insert(0f, mascotCatRect.DOLocalMove(_catBaseLocalPos, catLeapDuration).SetEase(Ease.OutBack, 1.05f));
            _entranceSeq.Insert(0f, mascotCatRect.DOScale(1f, catLeapDuration * 0.9f).SetEase(Ease.OutBack, 1.1f));
            _entranceSeq.Insert(0f, mascotCatRect.DOLocalRotate(Vector3.zero, catLeapDuration * 0.95f).SetEase(Ease.OutQuart));
        }

        // 2. 背景底框 Window1 紧随其后展开
        if (window1Rect != null)
        {
            window1Rect.position = originWorldPos;
            window1Rect.localScale = Vector3.zero;
            _entranceSeq.Insert(0.03f, window1Rect.DOLocalMove(_window1BaseLocalPos, 0.42f).SetEase(Ease.OutQuart));
            _entranceSeq.Insert(0.03f, window1Rect.DOScale(1f, 0.38f).SetEase(Ease.OutBack, 1.1f));
        }

        // 3. 游戏主标题从中心弹出
        if (titleLogoRect != null)
        {
            titleLogoRect.position = originWorldPos;
            titleLogoRect.localScale = Vector3.zero;
            _entranceSeq.Insert(0.05f, titleLogoRect.DOLocalMove(_titleBaseLocalPos, 0.44f).SetEase(Ease.OutBack, 1.2f));
            _entranceSeq.Insert(0.05f, titleLogoRect.DOScale(1f, 0.4f).SetEase(Ease.OutBack, 1.3f));
        }

        // 4. 五个斜切选项按极短间隔依次从中心炸出
        if (menuButtons != null)
        {
            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i] == null) continue;
                float delay = 0.06f + i * 0.04f;
                _entranceSeq.Insert(delay, menuButtons[i].PlayBurstFromWorldPos(originWorldPos, 0.42f));
            }
        }

        // 全部就位后，启动黑猫漂浮循环并重新校准开启 2.5D 视差
        _entranceSeq.OnComplete(() =>
        {
            StartCatIdleFloat();
            if (parallaxController != null)
            {
                parallaxController.RecordBasePositions();
                parallaxController.SetParallaxEnabled(true);
            }
        });
    }
    private System.Collections.IEnumerator PlayCatGlitchBurst(float duration, float peakIntensity)
    {
        if (_catGlitchMat == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float decay = 1f - (elapsed / duration);
            float val = UnityEngine.Random.Range(0.4f, 1f) * peakIntensity * decay;
            _catGlitchMat.SetFloat(GlitchIntensityId, val);

            yield return new WaitForSeconds(0.03f);
            elapsed += 0.03f;
        }

        _catGlitchMat.SetFloat(GlitchIntensityId, 0f);
    }

    private void BindButtonEvents()
    {
        if (btnInitialize != null) btnInitialize.onClick.AddListener(() => OnNewGameClicked());
        if (btnReconnect != null) btnReconnect.onClick.AddListener(() => EnterDesktopSession(isNewGame: false));
        if (btnTerminate != null) btnTerminate.onClick.AddListener(QuitApplication);

        if (btnMascotCat != null)
        {
            btnMascotCat.onClick.AddListener(() =>
            {
                if (mascotCatRect == null) return;
                mascotCatRect.DOKill();
                mascotCatRect.localScale = Vector3.one;
                mascotCatRect.DOPunchScale(new Vector3(0.12f, -0.1f, 0f), 0.35f, 8, 0.6f)
                    .OnComplete(StartCatIdleFloat);
            });
        }
    }
    private void OnNewGameClicked()
    {
        if (modeSelectionController != null)
        {
            modeSelectionController.Open();
        }
    }

    public void PlayMenuEntrance()
    {
        _entranceSeq?.Kill();
        _entranceSeq = DOTween.Sequence();

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.blocksRaycasts = true;
        }

        // 1. 标题重击砸入
        if (titleLogoRect != null)
        {
            titleLogoRect.localScale = Vector3.one * 1.35f;
            CanvasGroup titleCg = titleLogoRect.GetComponent<CanvasGroup>();
            if (titleCg != null)
            {
                titleCg.alpha = 0f;
                _entranceSeq.Join(titleCg.DOFade(1f, 0.22f));
            }
            _entranceSeq.Join(titleLogoRect.DOScale(1f, 0.32f).SetEase(Ease.OutBack, 1.4f));
        }

        // 2. 黑猫从底部弹跳入场，完成后启动失重悬浮循环
        if (mascotCatRect != null)
        {
            mascotCatRect.anchoredPosition = _catBasePos + new Vector2(0f, -180f);
            mascotCatRect.localScale = Vector3.one * 0.7f;
            _entranceSeq.Insert(0.12f, mascotCatRect.DOAnchorPos(_catBasePos, 0.38f).SetEase(Ease.OutBack, 1.3f));
            _entranceSeq.Insert(0.12f, mascotCatRect.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
        }

        // 3. 五个斜切选项错峰切入
        if (menuButtons != null)
        {
            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i] == null) continue;
                float delay = 0.18f + i * 0.06f;
                _entranceSeq.Insert(delay, menuButtons[i].PlayEntrance(0.32f));
            }
        }

        _entranceSeq.OnComplete(StartCatIdleFloat);
    }

    private void StartCatIdleFloat()
    {
        if (mascotCatRect == null) return;
        _catFloatTween?.Kill();
        _catTiltTween?.Kill();

        mascotCatRect.anchoredPosition = _catBasePos;
        mascotCatRect.localRotation = Quaternion.Euler(0f, 0f, -2f);

        // 上下缓慢失重漂浮
        _catFloatTween = mascotCatRect.DOAnchorPosY(_catBasePos.y + 14f, 1.8f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        // 轻微歪头呼吸
        _catTiltTween = mascotCatRect.DOLocalRotate(new Vector3(0f, 0f, 2.5f), 2.3f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>
    /// 点击“新建连接”或“继续潜入”后，主菜单向两侧散开淡出，唤起桌面聊天窗口
    /// </summary>
    private void EnterDesktopSession(bool isNewGame)
    {
        if (menuCanvasGroup != null)
            menuCanvasGroup.blocksRaycasts = false;

        _catFloatTween?.Kill();
        _catTiltTween?.Kill();

        Sequence exitSeq = DOTween.Sequence();
        if (titleLogoRect != null)
            exitSeq.Join(titleLogoRect.DOAnchorPosX(titleLogoRect.anchoredPosition.x - 400f, 0.28f).SetEase(Ease.InBack));

        if (mascotCatRect != null)
            exitSeq.Join(mascotCatRect.DOScale(0f, 0.25f).SetEase(Ease.InBack));

        if (menuCanvasGroup != null)
            exitSeq.Join(menuCanvasGroup.DOFade(0f, 0.28f));

        exitSeq.OnComplete(() =>
        {
            gameObject.SetActive(false);
            if (chatWindow != null)
            {
                chatWindow.gameObject.SetActive(true);
                chatWindow.transform.SetAsLastSibling();
            }
        });
    }

    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}