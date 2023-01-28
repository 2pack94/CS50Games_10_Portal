// The original shader was taken from:
// https://github.com/Brackeys/Portal-In-Unity/blob/master/Portal/Assets/ScreenCutoutShader.shader

// Additions to the shader:
// The inputs _Mask, _BorderMask, _BorderColour were added to make the portal texture look oval and give it a border.

Shader "Unlit/PortalShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Mask ("Culling Mask", 2D) = "white" {}
        _BorderMask ("Border Mask", 2D) = "white" {}
        _BorderColour("Border Colour", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Lighting Off
        Cull Back
        ZWrite On
        ZTest Less
        Blend SrcAlpha OneMinusSrcAlpha
        
        Fog{ Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            sampler2D _Mask;
            sampler2D _BorderMask;
            uniform float4 _BorderColour;

            fixed4 frag (v2f i) : SV_Target
            {
                i.screenPos /= i.screenPos.w;
                // Multiply the main texture (sampled in screen coordinates) with the mask texture
                // (sampled in uv coordinates). A black pixel in the mask will make the output pixel transparent.
                // This works because of the mask texture import settings: "alpha source: from grayscale".
                fixed4 col = tex2D(_MainTex, float2(i.screenPos.x, i.screenPos.y)) * tex2D(_Mask, i.uv);

                // Draw the Border color where the Border Mask texture is not black.
                if (tex2D(_BorderMask, i.uv).r > 0 || tex2D(_BorderMask, i.uv).g > 0 || tex2D(_BorderMask, i.uv).b > 0)
                    col = _BorderColour;

                return col;
            }
            ENDCG
        }
    }
}
