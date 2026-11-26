Shader "Custom/UI_Glitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0
        _SliceDensity ("Slice Density", Range(2, 50)) = 18
        _ColorSplit ("RGB Split Amount", Range(0, 0.1)) = 0.035

        // UGUI Mask 兼容属性
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _GlitchIntensity;
            float _SliceDensity;
            float _ColorSplit;

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                if (_GlitchIntensity <= 0.001)
                {
                    return tex2D(_MainTex, uv) * i.color;
                }

                // 1. 按时间步进量化，制造卡顿抽帧感
                float timeStep = floor(_Time.y * 24.0);
                float sliceId = floor(uv.y * _SliceDensity);
                float randVal = hash12(float2(sliceId, timeStep));

                // 2. 水平块状撕裂偏移
                float threshold = 1.0 - _GlitchIntensity * 0.75;
                if (randVal > threshold)
                {
                    float offset = (hash12(float2(sliceId + 17.3, timeStep)) - 0.5) * 0.25 * _GlitchIntensity;
                    uv.x += offset;
                }

                // 3. RGB 通道分离（保留透明底上的红蓝残影）
                float split = _ColorSplit * _GlitchIntensity;
                fixed4 colG = tex2D(_MainTex, uv);
                fixed4 colR = tex2D(_MainTex, uv + float2(split, 0));
                fixed4 colB = tex2D(_MainTex, uv - float2(split, 0));

                fixed alpha = max(colG.a, max(colR.a, colB.a) * 0.75);
                fixed4 finalCol = fixed4(colR.r, colG.g, colB.b, alpha) * i.color;

                return finalCol;
            }
            ENDCG
        }
    }
}