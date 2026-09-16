Shader "VoxelWilds/FirstPerson"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        ZWrite On
        ZTest LEqual
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; };
            sampler2D _MainTex;
            float4 _Color, _VoxelAmbientSky, _VoxelAmbientGround;
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                #if defined(UNITY_REVERSED_Z)
                    o.pos.z = lerp(o.pos.w, o.pos.z, .001);
                #else
                    o.pos.z = lerp(UNITY_NEAR_CLIP_VALUE * o.pos.w, o.pos.z, .001);
                #endif
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                clip(albedo.a - .4);
                float3 normal = normalize(i.normal);
                float3 ambient = lerp(_VoxelAmbientGround.rgb, _VoxelAmbientSky.rgb, normal.y * .5 + .5);
                float3 direct = _LightColor0.rgb * saturate(dot(normal, normalize(_WorldSpaceLightPos0.xyz)));
                float3 light = max(float3(.19,.18,.17), ambient) + direct * .72;
                return fixed4(albedo.rgb * light, 1);
            }
            ENDCG
        }
    }
}
