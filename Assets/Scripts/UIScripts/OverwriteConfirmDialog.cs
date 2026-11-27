using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OverwriteConfirmDialog : MonoBehaviour
{
    [Header("面板组件")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Button backdropCloseBtn;

    [Header("正文与按钮")]
    [SerializeField] private TMP_Text txtMessage;
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnCancel;

    private Action _onConfirmCallback;

    private void Awake()
    {
        if (btnConfirm != null)
        {
            btnConfirm.onClick.RemoveAllListeners();
            btnConfirm.onClick.AddListener(HandleConfirm);
        }

        if (btnCancel != null)
        {
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(Close);
        }

        if (backdropCloseBtn != null)
        {
            backdropCloseBtn.onClick.RemoveAllListeners();
            backdropCloseBtn.onClick.AddListener(Close);
        }

        gameObject.SetActive(false);
    }

    public void Open(Action onConfirm)
    {
        _onConfirmCallback = onConfirm;
        RefreshTexts();

        gameObject.SetActive(true);
        rootGroup.DOKill();
        rootGroup.alpha = 0f;
        rootGroup.interactable = true;
        rootGroup.blocksRaycasts = true;
        rootGroup.DOFade(1f, 0.16f);

        if (panelRect != null)
        {
            panelRect.DOKill();
            panelRect.localScale = Vector3.one * 0.9f;
            panelRect.DOScale(1f, 0.22f).SetEase(Ease.OutBack, 1.25f);
        }
    }

    public void Close()
    {
        _onConfirmCallback = null;
        rootGroup.DOKill();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        rootGroup.DOFade(0f, 0.14f).OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    private void HandleConfirm()
    {
        Action callback = _onConfirmCallback;
        Close();
        callback?.Invoke();
    }

    private void RefreshTexts()
    {
        if (btnConfirm != null)
        {
            TMP_Text confirmTmp = btnConfirm.GetComponentInChildren<TMP_Text>();
            if (confirmTmp != null) confirmTmp.text = LocalizationManager.GetText("overwrite_btn_confirm");
        }

        if (btnCancel != null)
        {
            TMP_Text cancelTmp = btnCancel.GetComponentInChildren<TMP_Text>();
            if (cancelTmp != null) cancelTmp.text = LocalizationManager.GetText("overwrite_btn_cancel");
        }

        if (txtMessage != null)
        {
            GameSaveData existingSave = SaveManager.PeekSave();
            string modeKey = (existingSave != null && existingSave.gameMode == "overclock")
                ? "mode_label_overclock"
                : "mode_label_sync";
            string modeDisplay = LocalizationManager.GetText(modeKey);

            string timeDisplay = "--";
            if (existingSave != null && existingSave.lastSavedUnixTime > 0)
            {
                DateTime localTime = DateTimeOffset.FromUnixTimeSeconds(existingSave.lastSavedUnixTime).LocalDateTime;
                timeDisplay = localTime.ToString("yyyy-MM-dd HH:mm");
            }

            string template = LocalizationManager.GetText("overwrite_save_desc");
            txtMessage.text = string.Format(template, modeDisplay, timeDisplay);
        }
    }
}