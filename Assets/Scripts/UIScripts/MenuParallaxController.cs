using System;
using System.Collections.Generic;
using UnityEngine;

public class MenuParallaxController : MonoBehaviour
{
    [Serializable]
    public class ParallaxLayer
    {
        public string layerName;
        public RectTransform targetRect;
        [Tooltip("最大偏移像素：正数=前景（随鼠标同向移动），负数=背景（随鼠标反向错位）")]
        public Vector2 maxOffset = new Vector2(20f, 15f);
        [HideInInspector] public Vector2 baseAnchoredPos;
        [HideInInspector] public Vector2 currentAppliedOffset;
    }

    [Header("视差图层列表 (越靠前的图层 maxOffset 越大)")]
    [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

    [Header("阻尼平滑速度")]
    [SerializeField] private float smoothSpeed = 8f;

    [Header("入场动画期间暂停视差，避免抢占坐标")]
    [SerializeField] private bool enableParallax = true;

    private void Start()
    {
        RecordBasePositions();
    }

    /// <summary>
    /// 记录各层初始锚点位置（若开场动画结束后再开启视差，可调用此方法校准基准点）
    /// </summary>
    public void RecordBasePositions()
    {
        foreach (var layer in layers)
        {
            if (layer.targetRect != null)
            {
                layer.baseAnchoredPos = layer.targetRect.anchoredPosition;
                layer.currentAppliedOffset = Vector2.zero;
            }
        }
    }

    public void SetParallaxEnabled(bool active)
    {
        enableParallax = active;
    }

    private void LateUpdate()
    {
        if (!enableParallax || layers.Count == 0) return;

        // 将屏幕鼠标坐标归一化到 [-1, 1]（屏幕中心为 (0, 0)）
        float normX = Mathf.Clamp((Input.mousePosition.x / Screen.width) * 2f - 1f, -1f, 1f);
        float normY = Mathf.Clamp((Input.mousePosition.y / Screen.height) * 2f - 1f, -1f, 1f);
        Vector2 mouseNorm = new Vector2(normX, normY);

        foreach (var layer in layers)
        {
            if (layer.targetRect == null) continue;

            Vector2 targetOffset = new Vector2(
                mouseNorm.x * layer.maxOffset.x,
                mouseNorm.y * layer.maxOffset.y
            );

            Vector2 nextOffset = Vector2.Lerp(
                layer.currentAppliedOffset,
                targetOffset,
                Time.unscaledDeltaTime * smoothSpeed
            );

            // 使用增量叠加，完全不干扰 DOTween 对该物体或子物体做的其他位移
            Vector2 delta = nextOffset - layer.currentAppliedOffset;
            layer.targetRect.anchoredPosition += delta;
            layer.currentAppliedOffset = nextOffset;
        }
    }
}