Shader "VoxelWilds/Terrain"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_FOG_COORDS(1) };
            sampler2D _MainTex;
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;
                float3 n=UnityObjectToWorldNormal(v.normal);
                float lighting=max(.15, dot(n, _WorldSpaceLightPos0.xyz)*.55+.45);
                o.color=float4(v.color.rgb * (UNITY_LIGHTMODEL_AMBIENT.rgb + _LightColor0.rgb*lighting),v.color.a);
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv)*i.color;UNITY_APPLY_FOG(i.fogCoord,c);return c; }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    Fallback "VertexLit"
}
