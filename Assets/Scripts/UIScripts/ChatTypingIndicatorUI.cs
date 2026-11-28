using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class ChatTypingIndicatorUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txtTyping;
    [SerializeField] private RectTransform[] animatedDots; // 可选：3个跳动圆点

    private Sequence _dotsSeq;

    private void OnEnable()
    {
        StartDotsAnimation();
    }

    private void OnDisable()
    {
        _dotsSeq?.Kill();
    }

    /// <summary>
    /// 刷新当前正在输入的成员列表（最多展示前2人名字，超过2人用省略号概括）
    /// </summary>
    public void UpdateTypers(List<string> typingSpeakerIds, CharacterDatabaseSO database)
    {
        if (typingSpeakerIds == null || typingSpeakerIds.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // 确保永远处于消息列表最底部

        StringBuilder sb = new StringBuilder();
        int displayCount = Mathf.Min(2, typingSpeakerIds.Count);

        for (int i = 0; i < displayCount; i++)
        {
            if (i > 0) sb.Append("、");

            string id = typingSpeakerIds[i];
            CharacterProfileSO profile = database != null ? database.GetProfile(id) : null;
            if (profile != null)
            {
                string hex = ColorUtility.ToHtmlStringRGB(profile.nameColor);
                sb.Append($"<color=#{hex}><b>{profile.displayName}</b></color>");
            }
            else
            {
                sb.Append($"<b>{id}</b>");
            }
        }

        if (typingSpeakerIds.Count > 2)
        {
            sb.Append($" 等人正在输入...");
        }
        else
        {
            sb.Append(" 正在输入...");
        }

        if (txtTyping != null)
            txtTyping.text = sb.ToString();
    }

    private void StartDotsAnimation()
    {
        if (animatedDots == null || animatedDots.Length == 0) return;

        _dotsSeq?.Kill();
        _dotsSeq = DOTween.Sequence();

        for (int i = 0; i < animatedDots.Length; i++)
        {
            if (animatedDots[i] == null) continue;
            RectTransform dot = animatedDots[i];
            dot.anchoredPosition = new Vector2(dot.anchoredPosition.x, 0f);

            // 每个点错开 0.12s 上下弹跳
            _dotsSeq.Insert(i * 0.12f,
                dot.DOAnchorPosY(6f, 0.25f).SetEase(Ease.OutQuad).SetLoops(2, LoopType.Yoyo));
        }

        _dotsSeq.AppendInterval(0.3f).SetLoops(-1);
    }
}