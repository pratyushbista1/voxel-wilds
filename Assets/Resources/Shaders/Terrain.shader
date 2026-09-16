Shader "VoxelWilds/Terrain"
{
    Properties { _MainTex ("Block atlas", 2D) = "white" {} _Cutoff ("Cutout", Range(0,1)) = 0.4 }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; float2 uv:TEXCOORD0; float2 surface:TEXCOORD1; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; float3 world:TEXCOORD2; float3 localLight:TEXCOORD3; float2 shade:TEXCOORD4; SHADOW_COORDS(5) UNITY_FOG_COORDS(6) };
            sampler2D _MainTex; float _Cutoff,_VoxelAOStrength; float4 _VoxelAmbientSky,_VoxelAmbientGround;
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;
                o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.shade=float2(v.color.r,v.surface.x);
                o.localLight=0;
                #ifdef VERTEXLIGHT_ON
                o.localLight=Shade4PointLights(unity_4LightPosX0,unity_4LightPosY0,unity_4LightPosZ0,unity_LightColor[0].rgb,unity_LightColor[1].rgb,unity_LightColor[2].rgb,unity_LightColor[3].rgb,unity_4LightAtten0,o.world,o.normal);
                #endif
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 albedo=tex2D(_MainTex,i.uv);clip(albedo.a-_Cutoff);
                float3 n=normalize(i.normal);float ao=lerp(1,i.shade.x,saturate(_VoxelAOStrength));
                float3 ambient=lerp(_VoxelAmbientGround.rgb,_VoxelAmbientSky.rgb,n.y*.5+.5);
                float shadow=SHADOW_ATTENUATION(i);
                float3 light=ambient*ao+_LightColor0.rgb*saturate(dot(n,normalize(_WorldSpaceLightPos0.xyz)))*shadow*lerp(1,ao,.65)+i.localLight;
                fixed4 c=fixed4(albedo.rgb*(light+i.shade.y),1);UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            CGPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Cutoff;
            struct shadowInput { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct shadowOutput { V2F_SHADOW_CASTER; float2 uv:TEXCOORD1; };
            shadowOutput shadowVert(shadowInput v)
            {
                shadowOutput o; o.uv=v.uv; TRANSFER_SHADOW_CASTER_NORMALOFFSET(o); return o;
            }
            float4 shadowFrag(shadowOutput i):SV_Target
            {
                clip(tex2D(_MainTex,i.uv).a-_Cutoff); SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
