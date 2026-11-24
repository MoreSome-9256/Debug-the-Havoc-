using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 临时占位！后期会根据实际情况修改
public class CodeHistoryItemUI : MonoBehaviour
{
    [SerializeField] private Button btnRestore;
    [SerializeField] private TextMeshProUGUI txtTimestamp;
    [SerializeField] private TextMeshProUGUI txtCodeSnippet;

    private string _rawCode;

    public void Setup(string timestamp, string rawCode, Action<string> onRestoreClicked)
    {
        _rawCode = rawCode;

        if (txtTimestamp != null)
            txtTimestamp.text = $"<b>[{timestamp}]</b>  <size=20><color=#FFFFFF66>点击载入</color></size>";

        if (txtCodeSnippet != null)
        {
            txtCodeSnippet.richText = false;
            txtCodeSnippet.text = rawCode;
        }

        if (btnRestore != null)
        {
            btnRestore.onClick.RemoveAllListeners();
            btnRestore.onClick.AddListener(() => onRestoreClicked?.Invoke(_rawCode));
        }
    }
}