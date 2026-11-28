using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "DH/Character Database")]
public class CharacterDatabaseSO : ScriptableObject
{
    [Header("置顶大群配置")]
    public string groupSessionId = "group_dh";
    public string groupDisplayName = "THIS IS DH!";
    public Sprite groupAvatarSprite;

    [Header("玩家自身配置 (用于右侧气泡或记录)")]
    public CharacterProfileSO playerProfile;

    [Header("全部 NPC 角色列表")]
    public List<CharacterProfileSO> characters = new List<CharacterProfileSO>();

    private Dictionary<string, CharacterProfileSO> _lookupCache;

    public void InitializeCache()
    {
        _lookupCache = new Dictionary<string, CharacterProfileSO>();

        if (playerProfile != null && !string.IsNullOrEmpty(playerProfile.characterId))
        {
            _lookupCache[playerProfile.characterId] = playerProfile;
        }

        foreach (var profile in characters)
        {
            if (profile != null && !string.IsNullOrEmpty(profile.characterId))
            {
                _lookupCache[profile.characterId] = profile;
            }
        }
    }

    public CharacterProfileSO GetProfile(string characterId)
    {
        if (_lookupCache == null) InitializeCache();
        if (string.IsNullOrEmpty(characterId)) return null;

        _lookupCache.TryGetValue(characterId, out var profile);
        return profile;
    }

    public SessionData CreateGroupSessionData()
    {
        return new SessionData
        {
            sessionId = groupSessionId,
            displayName = groupDisplayName,
            avatarSprite = groupAvatarSprite,
            lastMessageTime = 0d
        };
    }
}