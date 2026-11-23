using UnityEngine;

[CreateAssetMenu(fileName = "Char_New", menuName = "DH/Character Profile")]
public class CharacterProfileSO : ScriptableObject
{
    [Header("基础身份")]
    public string characterId;      // 唯一ID，如 "eli", "zero", "kestrel"
    public string displayName;      // 聊天中显示的用户名，如 "Eli", "^v^"
    public Sprite avatarSprite;     // 角色圆形头像贴图

    [Header("视觉主题色")]
    public Color nameColor = Color.white;                    // SenderName 字体颜色
    public Color shadowColor = new Color(1f, 0.3f, 0.3f, 1f); // Avatar_Shadow 阴影颜色
}