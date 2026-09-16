using System;
using UnityEngine;

namespace VoxelWilds
{
    [Serializable]
    public sealed class GameSettings
    {
        public int ViewDistance=4, Fps=0, Shadows=2;
        public float FieldOfView=78, Sensitivity=2, Brightness=1, Volume=.7f, EntityDistance=64;
        public bool Bobbing=true, Fog=true, Clouds=true, Fullscreen=false;
        public int Preset=4, AntiAliasing=4, ShadowResolution=2, TextureFiltering=0, Anisotropy=4;
        public float ShadowDistance=96, AmbientOcclusion=.75f, Bloom=.18f, Exposure=0;
        public bool VSync=false, AnimatedWater=true, Cinematic=true;
    }

    public static class GraphicsOptions
    {
        public static readonly string[] PresetNames={"Low","Medium","High","Ultra","Custom"};

        public static void Sanitize(GameSettings settings)
        {
            settings.ViewDistance=Mathf.Clamp(settings.ViewDistance,2,8);
            settings.Fps=Mathf.Clamp(settings.Fps,0,360);
            settings.Shadows=Mathf.Clamp(settings.Shadows,0,2);
            settings.Preset=Mathf.Clamp(settings.Preset,0,4);
            settings.AntiAliasing=settings.AntiAliasing>=8?8:settings.AntiAliasing>=4?4:settings.AntiAliasing>=2?2:0;
            settings.ShadowResolution=Mathf.Clamp(settings.ShadowResolution,0,3);
            settings.TextureFiltering=Mathf.Clamp(settings.TextureFiltering,0,1);
            settings.Anisotropy=Mathf.Clamp(settings.Anisotropy,0,8);
            settings.ShadowDistance=Clamp(settings.ShadowDistance,24,128,96);
            settings.AmbientOcclusion=Clamp(settings.AmbientOcclusion,0,1,.75f);
            settings.Bloom=Clamp(settings.Bloom,0,2,.18f);
            settings.Exposure=Clamp(settings.Exposure,-2,2,0);
            settings.FieldOfView=Clamp(settings.FieldOfView,55,110,78);
            settings.Sensitivity=Clamp(settings.Sensitivity,.2f,6,2);
            settings.Brightness=Clamp(settings.Brightness,.35f,1.8f,1);
            settings.Volume=Clamp(settings.Volume,0,1,.7f);
            settings.EntityDistance=Clamp(settings.EntityDistance,24,120,64);
        }

        public static void SetPreset(GameSettings settings,int preset)
        {
            preset=Mathf.Clamp(preset,0,3);
            settings.Preset=preset;
            settings.ViewDistance=new[]{3,4,5,8}[preset];
            settings.EntityDistance=new[]{48f,64f,96f,120f}[preset];
            settings.AntiAliasing=new[]{0,2,4,8}[preset];
            settings.Shadows=new[]{0,1,2,2}[preset];
            settings.ShadowResolution=new[]{0,1,2,3}[preset];
            settings.ShadowDistance=new[]{32f,64f,96f,128f}[preset];
            settings.Anisotropy=new[]{0,2,4,8}[preset];
            settings.AmbientOcclusion=new[]{.35f,.6f,.75f,.85f}[preset];
            settings.TextureFiltering=0;
            settings.Clouds=preset>0;
            settings.Fog=true;
            settings.AnimatedWater=preset>0;
            settings.Cinematic=preset>=2;
            settings.Bloom=new[]{0f,0f,.18f,.28f}[preset];
        }

        public static void Apply(GameSettings settings,Camera camera,Light sun,WorldRenderer renderer)
        {
            Sanitize(settings);
            QualitySettings.vSyncCount=settings.VSync?1:0;
            Application.targetFrameRate=settings.VSync||settings.Fps==0?-1:settings.Fps;
            QualitySettings.antiAliasing=settings.AntiAliasing;
            QualitySettings.shadows=settings.Shadows==0?ShadowQuality.Disable:settings.Shadows==1?ShadowQuality.HardOnly:ShadowQuality.All;
            QualitySettings.shadowResolution=(ShadowResolution)settings.ShadowResolution;
            QualitySettings.shadowDistance=settings.Shadows==0?0:settings.ShadowDistance;
            QualitySettings.shadowCascades=settings.ShadowDistance>=80?4:2;
            QualitySettings.shadowProjection=ShadowProjection.StableFit;
            QualitySettings.anisotropicFiltering=settings.Anisotropy>0?AnisotropicFiltering.Enable:AnisotropicFiltering.Disable;
            Shader.SetGlobalFloat("_VoxelAOStrength",settings.AmbientOcclusion);
            Shader.SetGlobalFloat("_VoxelWaterMotion",settings.AnimatedWater?1:0);
            renderer?.SetTextureFiltering(settings.TextureFiltering,settings.Anisotropy);
            if(camera!=null)
            {
                camera.fieldOfView=settings.FieldOfView;
                camera.farClipPlane=settings.ViewDistance*16+40;
                camera.allowMSAA=settings.AntiAliasing>0;
                camera.allowHDR=settings.Cinematic;
                camera.renderingPath=RenderingPath.Forward;
                var effects=camera.GetComponent<CinematicEffects>();
                if(effects==null)effects=camera.gameObject.AddComponent<CinematicEffects>();
                effects.Configure(settings.Cinematic,settings.Bloom,settings.Exposure);
            }
            if(sun!=null)
            {
                sun.shadows=settings.Shadows==0?LightShadows.None:settings.Shadows==1?LightShadows.Hard:LightShadows.Soft;
                sun.shadowBias=.035f;
                sun.shadowNormalBias=.3f;
                sun.shadowStrength=.85f;
            }
            AudioListener.volume=settings.Volume;
            if(!Application.isEditor&&Screen.fullScreen!=settings.Fullscreen)Screen.fullScreen=settings.Fullscreen;
        }

        private static float Clamp(float value,float minimum,float maximum,float fallback)
            =>float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp(value,minimum,maximum);
    }
}
