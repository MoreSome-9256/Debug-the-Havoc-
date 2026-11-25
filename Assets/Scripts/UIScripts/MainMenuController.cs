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

    [Header("功能按钮事件绑定")]
    [SerializeField] private Button btnInitialize;   // 新建连接
    [SerializeField] private Button btnReconnect;    // 继续潜入
    [SerializeField] private Button btnConfig;       // 系统配置
    [SerializeField] private Button btnArchive;      // 内部档案
    [SerializeField] private Button btnTerminate;    // 断开连接

    [Header("进入游戏后的桌面窗口")]
    [SerializeField] private ChatWindowController chatWindow;

    private Sequence _entranceSeq;
    private Tween _catFloatTween;
    private Tween _catTiltTween;
    private Vector2 _catBasePos;

    private void Start()
    {
        if (mascotCatRect != null)
        {
            _catBasePos = mascotCatRect.anchoredPosition;
            // 开启异形像素点击
            Image catImg = mascotCatRect.GetComponent<Image>();
            if (catImg != null && catImg.sprite != null && catImg.sprite.texture.isReadable)
                catImg.alphaHitTestMinimumThreshold = 0.05f;
        }

        BindButtonEvents();
        PlayMenuEntrance();
    }

    private void BindButtonEvents()
    {
        if (btnInitialize != null) btnInitialize.onClick.AddListener(() => EnterDesktopSession(isNewGame: true));
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