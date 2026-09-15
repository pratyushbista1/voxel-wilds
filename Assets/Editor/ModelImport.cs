using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace VoxelWilds.Editor
{
    public sealed class ModelImport : AssetPostprocessor
    {
        bool IsGameModel => assetPath.StartsWith("Assets/Resources/Models/") && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        void OnPreprocessModel()
        {
            if (!IsGameModel) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1;
            importer.useFileUnits = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
        public override int GetPostprocessOrder() => 100;
        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!IsGameModel) return;
            material.shader = Shader.Find("Standard");
            if (description.TryGetProperty("DiffuseColor", out Vector4 diffuse))
                material.color = new Color(diffuse.x, diffuse.y, diffuse.z, 1);
            else if (description.TryGetProperty("BaseColor", out Vector4 basis))
                material.color = new Color(basis.x, basis.y, basis.z, 1);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Glossiness", 0.12f);
            if (description.TryGetProperty("EmissiveColor", out Vector4 emission) && emission.sqrMagnitude > 0.001f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(emission.x, emission.y, emission.z, 1));
            }
        }
    }
}
