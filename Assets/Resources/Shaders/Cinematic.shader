Shader "VoxelWilds/Cinematic"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BloomTex;
        float4 _MainTex_TexelSize;
        float2 _BlurDirection;
        float _Exposure, _BloomIntensity;

        half3 SampleScene(float2 uv)
        {
            half3 color = tex2D(_MainTex, uv).rgb;
            #ifdef UNITY_COLORSPACE_GAMMA
                color = GammaToLinearSpace(color);
            #endif
            return max(color, 0);
        }

        half4 Prefilter(v2f_img i) : SV_Target
        {
            float2 texel = abs(_MainTex_TexelSize.xy) * .5;
            half3 color = SampleScene(i.uv + float2(-texel.x, -texel.y));
            color += SampleScene(i.uv + float2(texel.x, -texel.y));
            color += SampleScene(i.uv + float2(-texel.x, texel.y));
            color += SampleScene(i.uv + texel);
            color = min(color * (.25 * _Exposure), 64);
            half brightness = max(color.r, max(color.g, color.b));
            half soft = clamp(brightness - .55, 0, 1);
            soft = soft * soft * .5;
            half contribution = max(brightness - 1.05, soft) / max(brightness, .0001);
            return half4(color * contribution, 1);
        }

        half4 Downsample(v2f_img i) : SV_Target
        {
            float2 texel = abs(_MainTex_TexelSize.xy) * .5;
            half3 color = tex2D(_MainTex, i.uv + float2(-texel.x, -texel.y)).rgb;
            color += tex2D(_MainTex, i.uv + float2(texel.x, -texel.y)).rgb;
            color += tex2D(_MainTex, i.uv + float2(-texel.x, texel.y)).rgb;
            color += tex2D(_MainTex, i.uv + texel).rgb;
            return half4(color * .25, 1);
        }

        half4 Blur(v2f_img i) : SV_Target
        {
            half3 color = tex2D(_MainTex, i.uv).rgb * .227027;
            color += tex2D(_MainTex, i.uv + _BlurDirection * 1.384615).rgb * .316216;
            color += tex2D(_MainTex, i.uv - _BlurDirection * 1.384615).rgb * .316216;
            color += tex2D(_MainTex, i.uv + _BlurDirection * 3.230769).rgb * .070270;
            color += tex2D(_MainTex, i.uv - _BlurDirection * 3.230769).rgb * .070270;
            return half4(color, 1);
        }

        half3 NeutralToneMap(half3 color)
        {
            half peak = max(color.r, max(color.g, color.b));
            half shoulder = max(peak - .65, 0);
            half mapped = min(peak, .65) + shoulder / (1 + shoulder / .35);
            return color * (mapped / max(peak, .0001));
        }

        half4 Composite(v2f_img i) : SV_Target
        {
            float2 bloomUv = i.uv;
            #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) bloomUv.y = 1 - bloomUv.y;
            #endif
            half3 color = SampleScene(i.uv) * _Exposure;
            color += tex2D(_BloomTex, bloomUv).rgb * _BloomIntensity;
            color = NeutralToneMap(color);
            #ifdef UNITY_COLORSPACE_GAMMA
                color = LinearToGammaSpace(color);
            #endif
            return half4(color, tex2D(_MainTex, i.uv).a);
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Prefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Downsample
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Blur
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Composite
            ENDCG
        }
    }
    Fallback Off
}
