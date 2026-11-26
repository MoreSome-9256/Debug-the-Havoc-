using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public enum GameLanguage
{
    ZH,
    EN
}

public static class LocalizationManager
{
    private const string PrefKeyLang = "DH_Language";
    private const string CsvFileName = "Localization.csv";

    public static GameLanguage CurrentLanguage { get; private set; } = GameLanguage.ZH;
    public static event Action OnLanguageChanged;

    // Key -> (ZH, EN)
    private static Dictionary<string, (string zh, string en)> _table;
    private static bool _isInitialized = false;

    private static void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        int savedLang = PlayerPrefs.GetInt(PrefKeyLang, 0);
        CurrentLanguage = (GameLanguage)savedLang;

        LoadCsv();
    }

    public static void SetLanguage(GameLanguage lang)
    {
        EnsureInitialized();
        if (CurrentLanguage == lang) return;

        CurrentLanguage = lang;
        PlayerPrefs.SetInt(PrefKeyLang, (int)lang);
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// 供所有脚本调用的统一取词接口
    /// </summary>
    public static string GetText(string key)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(key)) return string.Empty;

        if (_table != null && _table.TryGetValue(key, out var entry))
        {
            string result = CurrentLanguage == GameLanguage.EN ? entry.en : entry.zh;
            // 如果英文还没翻完留空了，自动回退显示中文防穿帮
            return string.IsNullOrEmpty(result) ? entry.zh : result;
        }

        Debug.LogWarning($"[I18N] 未找到翻译 Key: {key}");
        return $"#{key}";
    }

    /// <summary>
    /// 运行时热重载表格（方便你朋友改完 CSV 按快捷键刷新预览）
    /// </summary>
    public static void Reload()
    {
        LoadCsv();
        OnLanguageChanged?.Invoke();
    }

    private static void LoadCsv()
    {
        _table = new Dictionary<string, (string zh, string en)>();
        string path = Path.Combine(Application.streamingAssetsPath, CsvFileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[I18N] 找不到翻译表: {path}");
            return;
        }

        // 1. 以共享只读模式读取原始字节（WPS/Excel 开着也能读）
        byte[] rawBytes;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            rawBytes = new byte[fs.Length];
            int totalRead = 0;
            while (totalRead < rawBytes.Length)
            {
                int read = fs.Read(rawBytes, totalRead, rawBytes.Length - totalRead);
                if (read == 0) break;
                totalRead += read;
            }
        }

        // 2. 优先按严格 UTF-8 解码，失败则自动回退到 WPS 默认的 GBK (CodePage 936)
        string content;
        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            content = strictUtf8.GetString(rawBytes);

            // 顺手剥离可能存在的 UTF-8 BOM 头
            if (content.Length > 0 && content[0] == '\uFEFF')
            {
                content = content.Substring(1);
            }
        }
        catch (DecoderFallbackException)
        {
            content = Encoding.GetEncoding(936).GetString(rawBytes);
        }

        List<List<string>> rows = ParseCsv(content);
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count < 2 || string.IsNullOrWhiteSpace(row[0])) continue;

            string key = row[0].Trim();
            string zh = row[1].Replace("\\n", "\n");
            string en = row.Count >= 3 ? row[2].Replace("\\n", "\n") : string.Empty;

            _table[key] = (zh, en);
        }
    }

    // 兼容双引号、单元格内逗号与换行的标准 CSV 解析器
    private static List<List<string>> ParseCsv(string text)
    {
        var result = new List<List<string>>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    result.Add(currentRow);
                    currentRow = new List<string>();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            result.Add(currentRow);
        }

        return result;
    }
}