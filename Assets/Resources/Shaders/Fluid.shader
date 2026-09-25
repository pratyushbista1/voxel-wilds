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
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0;float2 surface:TEXCOORD1; };
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;float kind:TEXCOORD3;SHADOW_COORDS(4) UNITY_FOG_COORDS(5) };
            sampler2D _MainTex;float _VoxelWaterMotion;float4 _VoxelSkyReflection,_VoxelAmbientSky;
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);o.kind=v.surface.y;TRANSFER_SHADOW(o);UNITY_TRANSFER_FOG(o,o.pos);return o;}
            float2 AnimatedTileUv(float2 uv,float2 offset)
            {
                float2 cell=floor(uv*8);
                float2 local=frac(uv*8)*2-.5;
                return (cell+.25+frac(local+offset)*.5)/8;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float time=_Time.y;
                float waterTime=time*_VoxelWaterMotion;
                float fluidTime=i.kind<.5?waterTime:time;
                float ripple=sin(i.world.x*2.7+i.world.z*1.9+fluidTime*1.7)*sin(i.world.z*3.2-i.world.x*.8-fluidTime);
                float2 uv=i.uv;
                if(i.kind>1.5)uv=AnimatedTileUv(uv,float2(sin(i.world.y*.75+time*.35)*.06,-time*.035));
                float3 albedo=tex2D(_MainTex,uv).rgb;fixed4 c;
                if(i.kind<.5)
                {
                    float3 n=normalize(i.normal+float3(cos(i.world.x*3+waterTime)*.025,0,sin(i.world.z*3-waterTime)*.025));
                    float3 view=normalize(_WorldSpaceCameraPos-i.world);float fresnel=pow(1-saturate(dot(n,view)),4);
                    float direct=saturate(dot(n,_WorldSpaceLightPos0.xyz))*SHADOW_ATTENUATION(i);
                    float sparkle=pow(saturate(dot(reflect(-normalize(_WorldSpaceLightPos0.xyz),n),view)),96)*direct;
                    float3 lit=albedo*(_VoxelAmbientSky.rgb+_LightColor0.rgb*direct*.65)*(1+ripple*.025);
                    c=fixed4(lerp(lit,_VoxelSkyReflection.rgb,fresnel*.4)+_LightColor0.rgb*sparkle*.18,.68+fresnel*.18);
                }
                else if(i.kind<1.5)c=fixed4(albedo*(1.35+ripple*.28)+float3(.17,.025,0),1);
                else if(i.kind<2.5)c=fixed4(albedo*(1.15+.12*sin(i.world.y*2+time)),.78);
                else c=fixed4(albedo*1.4,.98);
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
}
