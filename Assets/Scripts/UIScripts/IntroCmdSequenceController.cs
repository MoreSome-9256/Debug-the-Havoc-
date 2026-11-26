using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class IntroCmdSequenceController : MonoBehaviour
{
    [Serializable]
    public class ShatterShard
    {
        public RectTransform shardRect;
        public CanvasGroup shardGroup;
        [Tooltip("碎块飞出的方向向量（像素），如左大块填 (-180, -40)，右上块填 (220, 160)")]
        public Vector2 flyOffset;
        [Tooltip("飞出时的旋转角度")]
        public float flyRotateZ = 25f;
    }

    [Header("调试与首次启动控制")]
    [SerializeField] private bool alwaysPlayInEditor = true;
    private const string PrefKeyIntroPlayed = "DH_HasPlayedIntro";

    [Header("阶段 1：仿 CMD 终端与选项")]
    [SerializeField] private RectTransform cmdWindowRoot;
    [SerializeField] private TMP_Text txtTerminal;
    [SerializeField] private TMP_Text txtAsciiArt; // 新增：专门显示 ASCII 的文本框
    [SerializeField] private CanvasGroup choiceGroup;
    [SerializeField] private Button btnContinue;
    [SerializeField] private Button btnAbort;

    [Header("ASCII 打印速度参数")]
    [SerializeField] private int asciiCharsPerStep = 8;      // 每次刷出的字符数（越大越快）
    [SerializeField] private float asciiStepInterval = 0.01f; // 每次刷新的时间间隔（秒）

    [Header("阶段 2 & 3：实体化猫头与扒边猫爪")]
    [SerializeField] private CanvasGroup catHeadGroup;
    [SerializeField] private RectTransform catHeadRect;
    [SerializeField] private RectTransform catPawsRect;

    [Header("Glitch 材质引用")]
    [SerializeField] private Material glitchMaterialTemplate; // 拖入 Mat_UI_Glitch
    [SerializeField] private Image catHeadImage;
    [SerializeField] private Image catPawsImage;

    private Material _runtimeGlitchMat;
    private static readonly int GlitchIntensityId = Shader.PropertyToID("_GlitchIntensity");

    [Header("阶段 4：破窗碎片与主菜单衔接")]
    [SerializeField] private GameObject cmdShardsGroup;
    [SerializeField] private ShatterShard[] shards;
    [SerializeField] private MainMenuController mainMenuController;
    [SerializeField] private RectTransform mainMenuCatRect; // 直接拖入 Layer_MainMenu 里的 Cat 容器

    [Header("终端文本 Key")]
    [SerializeField] private string warningTextKey = "intro_cmd_warning";

    [Header("各阶段画面停留时长 (秒)")]
    [SerializeField] private float holdAfterAscii = 0.75f;    // ASCII 打完后停留多久再切换猫头
    [SerializeField] private float holdAfterCatHead = 0.85f;  // 猫头出现后停留多久再出猫爪
    [SerializeField] private float holdAfterCatPaws = 0.6f;   // 猫爪扒边后停留多久再破窗

    [TextArea(6, 12)]
    [SerializeField]
    private string asciiCatArt =
        "      /\\         /\\\n" +
        "     /  \\_______/  \\\n" +
        "    /   ===     X   \\\n" +
        "    \\      \\_/      /\n" +
        "     \\_____________/";

    private Vector2 _menuCatTargetPos;

    private void Start()
    {
        // 记录主菜单黑猫的最终落点
        if (mainMenuCatRect != null)
            _menuCatTargetPos = mainMenuCatRect.anchoredPosition;

        bool hasPlayed = PlayerPrefs.GetInt(PrefKeyIntroPlayed, 0) == 1;
#if UNITY_EDITOR
        if (alwaysPlayInEditor) hasPlayed = false;
#endif

        if (hasPlayed)
        {
            SkipToMainMenu();
            return;
        }

        InitIntroState();
        StartCoroutine(PlayTerminalWarningRoutine());
    }

    private void InitIntroState()
    {
        if (mainMenuController != null) mainMenuController.gameObject.SetActive(false);
        if (cmdShardsGroup != null) cmdShardsGroup.SetActive(false);
        if (txtAsciiArt != null)
        {
            txtAsciiArt.text = string.Empty;
            txtAsciiArt.gameObject.SetActive(false);
        }
        if (catHeadGroup != null) catHeadGroup.alpha = 0f;
        if (catPawsRect != null) catPawsRect.gameObject.SetActive(false);
        if (glitchMaterialTemplate != null)
        {
            _runtimeGlitchMat = new Material(glitchMaterialTemplate);
            _runtimeGlitchMat.SetFloat(GlitchIntensityId, 0f);

            if (catHeadImage != null) catHeadImage.material = _runtimeGlitchMat;
            if (catPawsImage != null) catPawsImage.material = _runtimeGlitchMat;
        }

        if (choiceGroup != null)
        {
            choiceGroup.alpha = 0f;
            choiceGroup.interactable = false;
            choiceGroup.blocksRaycasts = false;
        }

        if (btnContinue != null) btnContinue.onClick.AddListener(OnConfirmContinue);
        if (btnAbort != null) btnAbort.onClick.AddListener(() => Application.Quit());
    }
    private string GetRealGameDirectory()
    {
        // Application.dataPath 在打包后指向 ".../游戏目录/GameName_Data"
        // 在编辑器里指向 ".../项目目录/Assets"
        // 取它的父级目录，刚好就是 .exe 所在的真实文件夹（或项目根目录）
        DirectoryInfo parentDir = Directory.GetParent(Application.dataPath);
        string rawPath = parentDir != null ? parentDir.FullName : Application.dataPath;

        // 统一转成 Windows CMD 标准的反斜杠 '\'
        return rawPath.Replace('/', '\\');
    }

    private IEnumerator PlayTerminalWarningRoutine()
    {
        // CMD 窗口微弹出现
        cmdWindowRoot.localScale = Vector3.one * 0.92f;
        cmdWindowRoot.DOScale(1f, 0.22f).SetEase(Ease.OutBack);

        // 逐字打出风险提示
        string localizedWarning = LocalizationManager.GetText(warningTextKey)
    .Replace("{path}", GetRealGameDirectory());

        txtTerminal.text = localizedWarning;
        txtTerminal.maxVisibleCharacters = 0;

        for (int i = 0; i <= localizedWarning.Length; i++)
        {
            txtTerminal.maxVisibleCharacters = i;
            yield return new WaitForSeconds(0.022f);
        }

        // 显示选项
        if (choiceGroup != null)
        {
            choiceGroup.DOFade(1f, 0.2f).OnComplete(() =>
            {
                choiceGroup.interactable = true;
                choiceGroup.blocksRaycasts = true;
            });
        }
    }

    private void OnConfirmContinue()
    {
        if (choiceGroup != null)
        {
            choiceGroup.interactable = false;
            choiceGroup.blocksRaycasts = false;
            choiceGroup.DOFade(0f, 0.12f);
        }

        StartCoroutine(PlayMaterializeAndShatterRoutine());
    }

    private IEnumerator PlayMaterializeAndShatterRoutine()
    {
        // 1. 隐藏原警告文本，启用独立 ASCII 文本框并高速刷出
        if (txtTerminal != null) txtTerminal.gameObject.SetActive(false);

        if (txtAsciiArt != null)
        {
            txtAsciiArt.gameObject.SetActive(true);
            Color c = txtAsciiArt.color;
            c.a = 1f;
            txtAsciiArt.color = c;

            txtAsciiArt.text = asciiCatArt;
            txtAsciiArt.maxVisibleCharacters = 0;

            int totalLen = asciiCatArt.Length;
            for (int i = 0; i < totalLen; i += asciiCharsPerStep)
            {
                txtAsciiArt.maxVisibleCharacters = Mathf.Min(i + asciiCharsPerStep, totalLen);
                yield return new WaitForSeconds(asciiStepInterval);
            }
            txtAsciiArt.maxVisibleCharacters = totalLen;
        }

        // 1. ASCII 打完后停留
        yield return new WaitForSeconds(holdAfterAscii);

        // 2. ASCII 故障闪烁切换为实体猫头
        if (catHeadGroup != null) catHeadGroup.alpha = 1f;
        if (catHeadRect != null) catHeadRect.localScale = Vector3.one;

        for (int i = 0; i < 2; i++)
        {
            if (txtAsciiArt != null) txtAsciiArt.enabled = false;
            if (catHeadGroup != null) catHeadGroup.alpha = 1f;
            if (_runtimeGlitchMat != null) _runtimeGlitchMat.SetFloat(GlitchIntensityId, 0.95f);
            yield return new WaitForSeconds(0.05f);

            if (txtAsciiArt != null) txtAsciiArt.enabled = true;
            if (catHeadGroup != null) catHeadGroup.alpha = 0f;
            yield return new WaitForSeconds(0.04f);
        }

        // 定格为实体猫头，故障收敛后停留
        if (txtAsciiArt != null) txtAsciiArt.gameObject.SetActive(false);
        if (catHeadGroup != null) catHeadGroup.alpha = 1f;
        yield return StartCoroutine(PlayGlitchBurst(0.25f, 0.85f));
        yield return new WaitForSeconds(holdAfterCatHead);

        // 3. 猫爪故障闪现，收敛后停留蓄力
        if (catPawsRect != null)
        {
            catPawsRect.gameObject.SetActive(true);
            catPawsRect.localScale = Vector3.one;
        }
        yield return StartCoroutine(PlayGlitchBurst(0.28f, 1.0f));
        yield return new WaitForSeconds(holdAfterCatPaws);

        // 4. 破窗爆发 + 唤起主菜单
        TriggerWindowShatter();
    }
    private IEnumerator PlayGlitchBurst(float duration, float peakIntensity = 0.9f)
    {
        if (_runtimeGlitchMat == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 高频随机脉冲，后半段逐渐衰减归零
            float progress = elapsed / duration;
            float decay = 1f - progress;
            float val = UnityEngine.Random.Range(0.35f, 1f) * peakIntensity * decay;

            _runtimeGlitchMat.SetFloat(GlitchIntensityId, val);
            yield return new WaitForSeconds(0.03f);
            elapsed += 0.03f;
        }

        _runtimeGlitchMat.SetFloat(GlitchIntensityId, 0f);
    }

    private void TriggerWindowShatter()
    {
        PlayerPrefs.SetInt(PrefKeyIntroPlayed, 1);
        PlayerPrefs.Save();

        // 记录 CMD 窗口中心的世界坐标
        Vector3 burstOrigin = cmdWindowRoot.position;

        // 瞬间切换：隐藏完整 CMD，激活碎片组
        cmdWindowRoot.gameObject.SetActive(false);
        if (cmdShardsGroup != null) cmdShardsGroup.SetActive(true);

        // 碎片向四周炸开并旋转淡出
        Sequence shatterSeq = DOTween.Sequence();
        foreach (var s in shards)
        {
            if (s.shardRect == null) continue;
            shatterSeq.Join(s.shardRect.DOAnchorPos(s.shardRect.anchoredPosition + s.flyOffset, 0.55f).SetEase(Ease.OutQuart));
            shatterSeq.Join(s.shardRect.DOLocalRotate(new Vector3(0f, 0f, s.flyRotateZ), 0.55f).SetEase(Ease.OutQuad));
            if (s.shardGroup != null)
                shatterSeq.Join(s.shardGroup.DOFade(0f, 0.45f).SetDelay(0.12f));
        }

        // 唤醒主菜单，所有元素从 CMD 窗口中心弹出
        if (mainMenuController != null)
        {
            mainMenuController.gameObject.SetActive(true);
            mainMenuController.PlayBurstEntranceFromWorldPos(burstOrigin);
        }

        shatterSeq.OnComplete(() => gameObject.SetActive(false));
    }

    private void SkipToMainMenu()
    {
        gameObject.SetActive(false);
        if (mainMenuController != null)
            mainMenuController.gameObject.SetActive(true);
    }
    private void OnDestroy()
    {
        if (_runtimeGlitchMat != null) Destroy(_runtimeGlitchMat);
    }
}