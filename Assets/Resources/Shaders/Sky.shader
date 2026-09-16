Shader "VoxelWilds/Sky"
{
    Properties { _SkyTop("Zenith",Color)=(.22,.5,.82,1) _SkyHorizon("Horizon",Color)=(.74,.84,.9,1) _SkyGround("Ground",Color)=(.37,.46,.49,1) _SunColor("Sun",Color)=(1,.91,.65,1) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos:SV_POSITION;float3 direction:TEXCOORD0; };
            float4 _SkyTop,_SkyHorizon,_SkyGround,_SunColor,_SunDirection;float _Night,_Dimension;
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;}
            float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
            fixed4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.direction);float h=pow(saturate(abs(d.y)),.55);
                float3 c=lerp(_SkyHorizon.rgb,d.y>=0?_SkyTop.rgb:_SkyGround.rgb,h);
                if(_Dimension<.5)
                {
                    float sun=dot(d,normalize(_SunDirection.xyz));float moon=-sun;
                    c+=_SunColor.rgb*pow(saturate(sun),28)*.13;
                    c=lerp(c,_SunColor.rgb,smoothstep(.99935,.99965,sun));
                    c=lerp(c,float3(.71,.78,.91),smoothstep(.9995,.9997,moon)*_Night);
                    float stars=step(.9974,hash(floor(d*420)))*pow(saturate(d.y),.35)*_Night;
                    c+=stars*.75;
                }
                return fixed4(c,1);
            }
            ENDCG
        }
    }
}
