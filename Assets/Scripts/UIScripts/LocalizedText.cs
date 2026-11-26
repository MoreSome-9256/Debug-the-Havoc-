using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string localizationKey;
    private TMP_Text _tmpText;

    private void Awake()
    {
        _tmpText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= Refresh;
    }

    public void SetKey(string newKey)
    {
        localizationKey = newKey;
        Refresh();
    }

    public void Refresh()
    {
        if (_tmpText != null && !string.IsNullOrEmpty(localizationKey))
        {
            _tmpText.text = LocalizationManager.GetText(localizationKey);
        }
    }
}