Shader "VoxelWilds/Fluid"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR;float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1) };
            sampler2D _MainTex;
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv+float2(_Time.y*.035,_Time.y*.025);UNITY_TRANSFER_FOG(o,o.pos);return o;}
            fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv)*i.color;UNITY_APPLY_FOG(i.fogCoord,c);return c;}
            ENDCG
        }
    }
}
