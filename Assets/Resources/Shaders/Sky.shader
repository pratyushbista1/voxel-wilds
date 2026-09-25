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
                    float3 sunDirection=normalize(_SunDirection.xyz);
                    float sun=dot(d,sunDirection);float moon=-sun;
                    float3 up=abs(sunDirection.y)>.98?float3(0,0,1):float3(0,1,0);
                    float3 right=normalize(cross(up,sunDirection));up=cross(sunDirection,right);
                    float2 celestialUv=float2(dot(d,right),dot(d,up));
                    float square=1-smoothstep(.023,.024,max(abs(celestialUv.x),abs(celestialUv.y)));
                    c+=_SunColor.rgb*pow(saturate(sun),28)*.13;
                    c=lerp(c,_SunColor.rgb,square*step(.99,sun));
                    float moonDetail=.84+.16*hash(floor(float3(celestialUv*240,0)));
                    c=lerp(c,float3(.71,.78,.91)*moonDetail,square*step(.99,moon)*_Night);
                    float stars=step(.9974,hash(floor(d*420)))*pow(saturate(d.y),.35)*_Night;
                    c+=stars*.75;
                }
                return fixed4(c,1);
            }
            ENDCG
        }
    }
}
