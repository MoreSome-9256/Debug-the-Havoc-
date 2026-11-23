using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatMessageItemUI : MonoBehaviour
{
    [Header("节点引用")]
    [SerializeField] private GameObject avatarGroup;
    [SerializeField] private Image imgAvatar;
    [SerializeField] private Image imgAvatarShadow;        // 头像背后的彩色阴影
    [SerializeField] private LayoutElement leftColumnLayout; // 用于切换同人连发时的高度压缩
    [SerializeField] private GameObject headerRow;
    [SerializeField] private TextMeshProUGUI txtHeader;      // 合并后的用户名+时间戳（或独立文本框）
    [SerializeField] private TextMeshProUGUI txtMessageBody;

    [Header("排版间距配置")]
    [SerializeField] private float fullAvatarMinHeight = 115f;
    [SerializeField] private float newSpeakerTopPadding = 18f;
    [SerializeField] private float compactTopPadding = 0f;

    private HorizontalLayoutGroup _rootLayout;

    private void Awake()
    {
        _rootLayout = GetComponent<HorizontalLayoutGroup>();
    }

    public void Setup(CharacterProfileSO profile, string timestamp, string content, bool isGroupedWithPrevious)
    {
        if (txtMessageBody != null)
            txtMessageBody.text = content;

        bool showHeader = !isGroupedWithPrevious;

        if (avatarGroup != null) avatarGroup.SetActive(showHeader);
        if (headerRow != null) headerRow.SetActive(showHeader);

        // 动态切换左侧列最小高度，防止连发短消息被强行撑出大空隙
        if (leftColumnLayout != null)
            leftColumnLayout.minHeight = showHeader ? fullAvatarMinHeight : 0f;

        if (showHeader && profile != null)
        {
            if (imgAvatar != null && profile.avatarSprite != null)
                imgAvatar.sprite = profile.avatarSprite;

            if (imgAvatarShadow != null)
                imgAvatarShadow.color = profile.shadowColor;

            if (txtHeader != null)
            {
                string hexNameColor = ColorUtility.ToHtmlStringRGB(profile.nameColor);
                txtHeader.text = $"<color=#{hexNameColor}><b>{profile.displayName}</b></color>  <size=25><color=#FFFFFF8C>{timestamp}</color></size>";
            }
        }

        if (_rootLayout != null)
        {
            RectOffset pad = _rootLayout.padding;
            pad.top = Mathf.RoundToInt(showHeader ? newSpeakerTopPadding : compactTopPadding);
            _rootLayout.padding = pad;
        }
    }
}