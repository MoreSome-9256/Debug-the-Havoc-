using UnityEngine;

public enum WindowLayer
{
    Workspace, // 工作层（如代码编译器）
    Social     // 社交陪伴层（如聊天窗、视频通话小窗，始终高于工作层）
}

public class WindowManager : MonoBehaviour
{
    public static WindowManager Instance { get; private set; }

    [Header("Canvas 分层容器")]
    [SerializeField] private RectTransform workspaceLayer;
    [SerializeField] private RectTransform socialLayer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 将指定窗口在所属图层内置顶
    /// </summary>
    public void BringToFront(DraggableWindow window)
    {
        if (window == null) return;

        RectTransform targetParent = window.Layer == WindowLayer.Social ? socialLayer : workspaceLayer;
        if (targetParent != null && window.transform.parent != targetParent)
        {
            window.transform.SetParent(targetParent, false);
        }
        window.transform.SetAsLastSibling();
    }
}
