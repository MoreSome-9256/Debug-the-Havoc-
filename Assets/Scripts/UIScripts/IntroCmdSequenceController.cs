using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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

    [Header("选项组动态排版")]
    [SerializeField] private float choiceTopSpacing = 24f; // 按钮组距离上方文本最后一行的间距（像素）

    [Header("退出按钮劫持配置")]
    [SerializeField] private string abortReplyTextKey = "intro_cmd_abort_reply";
    [SerializeField] private Image cmdBgImage; // 拖入 CMD_Intact_Bg，用于触发终端底图 Glitch

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

    [Header("碎片飞散节奏控制")]
    [SerializeField] private float shardMoveDuration = 0.22f; // 所有碎片同时飞到目标位的耗时（越小越干脆）
    [SerializeField] private float shardHoldDuration = 0.08f; // 到达目标位后的极短定格时间（填 0 则到达立刻消失）
    [SerializeField] private float shardFadeDuration = 0.15f; // 消失耗时（填 0 则瞬间隐藏，不淡出）

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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
#endif

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

            if (cmdBgImage != null) cmdBgImage.material = _runtimeGlitchMat;
            if (catHeadImage != null) catHeadImage.material = _runtimeGlitchMat;
            if (catPawsImage != null) catPawsImage.material = _runtimeGlitchMat;
        }

        if (choiceGroup != null)
        {
            choiceGroup.alpha = 0f;
            choiceGroup.interactable = false;
            choiceGroup.blocksRaycasts = false;
        }

        if (btnContinue != null)
        {
            btnContinue.onClick.RemoveAllListeners();
            btnContinue.onClick.AddListener(OnConfirmContinue);
        }

        if (btnAbort != null)
        {
            btnAbort.onClick.RemoveAllListeners();
            btnAbort.onClick.AddListener(OnAbortClicked);
        }
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

    private static readonly Regex WaitTagRegex = new Regex(@"<wait=([0-9]*\.?[0-9]+)>", RegexOptions.IgnoreCase);

    private IEnumerator PlayTerminalWarningRoutine()
    {
        // CMD 窗口微弹出现
        cmdWindowRoot.localScale = Vector3.one * 0.92f;
        cmdWindowRoot.DOScale(1f, 0.22f).SetEase(Ease.OutBack);

        string rawWarning = LocalizationManager.GetText(warningTextKey)
            .Replace("{path}", GetRealGameDirectory());

        // 剥离 <wait=x> 标签，并计算每个停顿点对应的真实可见字符位置
        string cleanWarning = ExtractWaitPoints(rawWarning, txtTerminal, out Dictionary<int, float> pausePoints);

        txtTerminal.maxVisibleCharacters = 0;
        txtTerminal.text = cleanWarning;
        UpdateChoiceGroupPosition(smoothMove: false);
        txtTerminal.ForceMeshUpdate();

        int totalVisibleChars = txtTerminal.textInfo.characterCount;

        for (int i = 0; i <= totalVisibleChars; i++)
        {
            txtTerminal.maxVisibleCharacters = i;

            // 命中停顿点时暂停指定秒数
            if (pausePoints.TryGetValue(i, out float waitSeconds))
            {
                yield return new WaitForSeconds(waitSeconds);
            }
            else
            {
                yield return new WaitForSeconds(0.022f);
            }
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

    /// <summary>
    /// 根据 txtTerminal 当前文本的完整排版高度，将 Choice_Group 定位到文本正下方
    /// </summary>
    private void UpdateChoiceGroupPosition(bool smoothMove = false)
    {
        if (txtTerminal == null || choiceGroup == null) return;

        txtTerminal.ForceMeshUpdate();
        RectTransform textRect = txtTerminal.rectTransform;
        RectTransform choiceRect = choiceGroup.GetComponent<RectTransform>();

        // 计算文本框顶边在父容器中的局部 Y 坐标（不受 Pivot 影响）
        float textTopLocalY = textRect.localPosition.y + (1f - textRect.pivot.y) * textRect.rect.height;
        // 减去文本实际渲染总高度，得到最后一行文字的底边位置
        float textBottomLocalY = textTopLocalY - txtTerminal.preferredHeight;

        // 计算按钮组目标局部 Y 坐标，使其顶边与文本底边保持 choiceTopSpacing 间距
        float targetChoiceLocalY = textBottomLocalY - choiceTopSpacing - (1f - choiceRect.pivot.y) * choiceRect.rect.height;

        choiceRect.DOKill();
        if (smoothMove)
        {
            choiceRect.DOLocalMoveY(targetChoiceLocalY, 0.22f).SetEase(Ease.OutQuart);
        }
        else
        {
            Vector3 pos = choiceRect.localPosition;
            pos.y = targetChoiceLocalY;
            choiceRect.localPosition = pos;
        }
    }

    /// <summary>
    /// 解析文本中的 <wait=秒数> 标签，返回去除 wait 标签后的干净文本及停顿映射表
    /// （借助 TMP 的 ForceMeshUpdate 自动排除 <color> 等富文本标签对字数索引的干扰）
    /// </summary>
    private string ExtractWaitPoints(string rawText, TMP_Text targetTmp, out Dictionary<int, float> pausePoints)
    {
        pausePoints = new Dictionary<int, float>();
        StringBuilder cleanBuilder = new StringBuilder();
        int lastIndex = 0;

        targetTmp.maxVisibleCharacters = 0;

        foreach (Match match in WaitTagRegex.Matches(rawText))
        {
            cleanBuilder.Append(rawText, lastIndex, match.Index - lastIndex);

            if (float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float waitSec))
            {
                targetTmp.text = cleanBuilder.ToString();
                targetTmp.ForceMeshUpdate();
                int visibleIdx = targetTmp.textInfo.characterCount;

                if (pausePoints.ContainsKey(visibleIdx))
                    pausePoints[visibleIdx] += waitSec;
                else
                    pausePoints[visibleIdx] = waitSec;
            }

            lastIndex = match.Index + match.Length;
        }

        cleanBuilder.Append(rawText, lastIndex, rawText.Length - lastIndex);
        return cleanBuilder.ToString();
    }

    private void OnConfirmContinue()
    {
        StopAllCoroutines();
        if (_runtimeGlitchMat != null) _runtimeGlitchMat.SetFloat(GlitchIntensityId, 0f);

        if (choiceGroup != null)
        {
            choiceGroup.interactable = false;
            choiceGroup.blocksRaycasts = false;
            choiceGroup.DOFade(0f, 0.12f);
        }

        StartCoroutine(PlayMaterializeAndShatterRoutine());
    }
    private void OnAbortClicked()
    {
        // 1. 禁用退出按钮并加上删除线与变暗效果
        if (btnAbort != null)
        {
            btnAbort.interactable = false;
            TMP_Text abortTmp = btnAbort.GetComponentInChildren<TMP_Text>();
            if (abortTmp != null)
            {
                abortTmp.text = $"<s>{abortTmp.text}</s>";
                abortTmp.alpha = 0.35f;
            }
        }

        // 2. 立即隐藏选项组并锁定交互，防止乱码清空期间误触
        if (choiceGroup != null)
        {
            choiceGroup.DOKill();
            choiceGroup.alpha = 0f;
            choiceGroup.interactable = false;
            choiceGroup.blocksRaycasts = false;
        }

        StopAllCoroutines();
        StartCoroutine(PlayAbortRefusalRoutine());
    }

    private IEnumerator PlayAbortRefusalRoutine()
    {
        // 1. 终端底图触发 Glitch，文字乱码闪烁并横向错位
        if (_runtimeGlitchMat != null)
        {
            StartCoroutine(PlayGlitchBurst(0.24f, 0.9f));
        }

        RectTransform textRect = txtTerminal.rectTransform;
        Vector2 originPos = textRect.anchoredPosition;
        const string glitchChars = "#@%&*!?/\\|=-+~";

        for (int step = 0; step < 4; step++)
        {
            float offsetX = UnityEngine.Random.Range(-10f, 10f);
            textRect.anchoredPosition = originPos + new Vector2(offsetX, 0f);

            int len = Mathf.Min(txtTerminal.textInfo.characterCount, 120);
            StringBuilder sb = new StringBuilder(len);
            for (int c = 0; c < len; c++)
            {
                char orig = txtTerminal.textInfo.characterInfo[c].character;
                sb.Append(orig == '\n' ? '\n' : glitchChars[UnityEngine.Random.Range(0, glitchChars.Length)]);
            }
            txtTerminal.text = sb.ToString();
            txtTerminal.maxVisibleCharacters = len;

            yield return new WaitForSeconds(0.045f);
        }

        // 2. 复位坐标并清空屏幕
        textRect.anchoredPosition = originPos;
        txtTerminal.text = string.Empty;
        yield return new WaitForSeconds(0.15f);

        // 3. 准备劝留台词，并在选项组隐藏状态下静默更新到新文本正下方
        string rawReply = LocalizationManager.GetText(abortReplyTextKey);
        string cleanReply = ExtractWaitPoints(rawReply, txtTerminal, out Dictionary<int, float> pausePoints);

        txtTerminal.maxVisibleCharacters = 0;
        txtTerminal.text = cleanReply;
        UpdateChoiceGroupPosition(smoothMove: false);

        // 4. 逐字打出 DH_BOT 台词
        int totalVisibleChars = txtTerminal.textInfo.characterCount;
        for (int i = 0; i <= totalVisibleChars; i++)
        {
            txtTerminal.maxVisibleCharacters = i;

            if (pausePoints.TryGetValue(i, out float waitSeconds))
                yield return new WaitForSeconds(waitSeconds);
            else
                yield return new WaitForSeconds(0.025f);
        }

        // 5. 打字结束后，重新淡入显示选项组并恢复交互
        if (choiceGroup != null)
        {
            choiceGroup.interactable = true;
            choiceGroup.blocksRaycasts = true;
            choiceGroup.DOFade(1f, 0.2f);
        }

        if (btnContinue != null)
        {
            btnContinue.transform.DOPunchScale(Vector3.one * 0.08f, 0.25f, 6, 0.5f);
        }
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

        // 直接取猫头（或 CMD_Intact_Bg）的真实世界坐标，避免父容器中心点偏移
        Vector3 burstOrigin = catHeadRect != null ? catHeadRect.position : cmdWindowRoot.position;

        // 瞬间切换：隐藏完整 CMD，激活碎片组
        cmdWindowRoot.gameObject.SetActive(false);
        if (cmdShardsGroup != null) cmdShardsGroup.SetActive(true);

        // 碎片同时快速移动到目标位置，到位后再统一消失
        Sequence shatterSeq = DOTween.Sequence();
        float disappearStartTime = shardMoveDuration + shardHoldDuration;

        foreach (var s in shards)
        {
            if (s.shardRect == null) continue;

            // 1. 第 0 秒：所有碎片同时高速冲向目标位置并旋转
            shatterSeq.Insert(0f, s.shardRect
                .DOAnchorPos(s.shardRect.anchoredPosition + s.flyOffset, shardMoveDuration)
                .SetEase(Ease.OutExpo));

            shatterSeq.Insert(0f, s.shardRect
                .DOLocalRotate(new Vector3(0f, 0f, s.flyRotateZ), shardMoveDuration)
                .SetEase(Ease.OutExpo));

            // 2. 全部到达目标位置并定格后，再统一消失
            if (s.shardGroup != null)
            {
                s.shardGroup.alpha = 1f;
                if (shardFadeDuration > 0f)
                {
                    shatterSeq.Insert(disappearStartTime, s.shardGroup.DOFade(0f, shardFadeDuration));
                }
            }
        }

        // 如果 shardFadeDuration 设为 0（瞬间消失），确保序列至少等到定格结束再关闭物体
        if (shardFadeDuration <= 0f)
        {
            shatterSeq.InsertCallback(disappearStartTime, () => { });
        }

        shatterSeq.OnComplete(() =>
        {
            if (cmdShardsGroup != null) cmdShardsGroup.SetActive(false);
            gameObject.SetActive(false);
        });

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