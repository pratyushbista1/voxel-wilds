using UnityEngine;

namespace VoxelWilds
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CinematicEffects : MonoBehaviour
    {
        public bool EffectsEnabled { get; private set; } = true;
        public float BloomIntensity { get; private set; } = .24f;
        public float Exposure { get; private set; } = .1f;
        public bool IsSupported => effectMaterial != null && effectMaterial.shader.isSupported;

        private Material effectMaterial;
        private Camera eye;
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly int BloomTextureId = Shader.PropertyToID("_BloomTex");
        private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");

        public void Configure(bool effectsEnabled, float bloom, float exposure)
        {
            EffectsEnabled = effectsEnabled;
            BloomIntensity = float.IsNaN(bloom) ? .24f : Mathf.Clamp(bloom, 0, 2);
            Exposure = float.IsNaN(exposure) ? 0 : Mathf.Clamp(exposure, -2, 2);
            if (eye == null) eye = GetComponent<Camera>();
            eye.allowHDR = effectsEnabled;
        }

        private void OnEnable()
        {
            eye = GetComponent<Camera>();
            Shader shader = Shader.Find("VoxelWilds/Cinematic");
            if (shader != null && shader.isSupported)
                effectMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void OnDisable()
        {
            if (effectMaterial != null) Destroy(effectMaterial);
            effectMaterial = null;
        }

        [ImageEffectTransformsToLDR]
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            bool previousSrgbWrite = GL.sRGBWrite;
            RenderTexture half = null, bloom = null, scratch = null;
            try
            {
                if (!EffectsEnabled || !IsSupported)
                {
                    SetColorWrite(destination);
                    Graphics.Blit(source, destination);
                    return;
                }

                effectMaterial.SetFloat(ExposureId, Mathf.Pow(2, Exposure));
                effectMaterial.SetFloat(BloomIntensityId, BloomIntensity);
                effectMaterial.SetTexture(BloomTextureId, Texture2D.blackTexture);

                if (BloomIntensity > .001f)
                {
                    RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                        ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.Default;
                    int halfWidth = Mathf.Max(1, source.width / 2), halfHeight = Mathf.Max(1, source.height / 2);
                    int quarterWidth = Mathf.Max(1, source.width / 4), quarterHeight = Mathf.Max(1, source.height / 4);
                    half = Temporary(halfWidth, halfHeight, format);
                    bloom = Temporary(quarterWidth, quarterHeight, format);
                    scratch = Temporary(quarterWidth, quarterHeight, format);
                    GL.sRGBWrite = false;
                    Graphics.Blit(source, half, effectMaterial, 0);
                    Graphics.Blit(half, bloom, effectMaterial, 1);
                    for (int i = 0; i < 2; i++)
                    {
                        float radius = 1 + i;
                        effectMaterial.SetVector(BlurDirectionId, new Vector4(radius / quarterWidth, 0, 0, 0));
                        Graphics.Blit(bloom, scratch, effectMaterial, 2);
                        effectMaterial.SetVector(BlurDirectionId, new Vector4(0, radius / quarterHeight, 0, 0));
                        Graphics.Blit(scratch, bloom, effectMaterial, 2);
                    }
                    effectMaterial.SetTexture(BloomTextureId, bloom);
                }

                SetColorWrite(destination);
                Graphics.Blit(source, destination, effectMaterial, 3);
            }
            finally
            {
                if (effectMaterial != null) effectMaterial.SetTexture(BloomTextureId, Texture2D.blackTexture);
                if (half != null) RenderTexture.ReleaseTemporary(half);
                if (bloom != null) RenderTexture.ReleaseTemporary(bloom);
                if (scratch != null) RenderTexture.ReleaseTemporary(scratch);
                GL.sRGBWrite = previousSrgbWrite;
            }
        }

        private static RenderTexture Temporary(int width, int height, RenderTextureFormat format)
        {
            var texture = RenderTexture.GetTemporary(width, height, 0, format, RenderTextureReadWrite.Linear);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static void SetColorWrite(RenderTexture destination)
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear &&
                (destination == null || destination.sRGB);
        }
    }
}
