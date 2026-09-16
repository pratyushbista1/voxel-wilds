using UnityEngine;
using UnityEngine.Rendering;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class DoorVisual : MonoBehaviour
    {
        private static Material wood, inset, iron, handle;
        private Transform hinge;
        private Quaternion closed, desired;
        private bool initialized, fullHeight;
        public bool IsOpen { get; private set; }

        public void Configure(Voxel door, bool tall)
        {
            if (hinge && tall != fullHeight) { hinge.gameObject.SetActive(false); Destroy(hinge.gameObject); hinge=null; initialized=false; }
            fullHeight=tall;
            if (!hinge) Build(tall);
            int facing = DoorRules.Facing(door);
            float edge = DoorRules.Thickness * .5f;
            hinge.localPosition = new Vector3(facing == 1 || facing == 2 ? 1-edge : edge, 0, facing >= 2 ? 1-edge : edge);
            closed = Quaternion.Euler(0, -90 * facing, 0);
            IsOpen = DoorRules.IsOpen(door);
            desired = closed * Quaternion.Euler(0, IsOpen ? -90 : 0, 0);
            if (!initialized) { hinge.localRotation = desired; initialized = true; }
        }
        private void Update()
        {
            if (!hinge) return;
            hinge.localRotation = Quaternion.RotateTowards(hinge.localRotation, desired, 600 * Time.deltaTime);
        }
        private void Build(bool tall)
        {
            if (!wood)
            {
                wood = Material("Door oak frame", new Color(.56f,.34f,.15f));
                inset = Material("Door inset panels", new Color(.43f,.245f,.105f));
                iron = Material("Door hinges", new Color(.22f,.225f,.21f));
                handle = Material("Door handle", new Color(.73f,.54f,.18f));
            }
            hinge = new GameObject("Door hinge").transform;
            hinge.SetParent(transform,false);
            float height = tall ? 2 : 1;
            float width = 1 - DoorRules.Thickness;
            Part("Left stile",new Vector3(.055f,height*.5f,0),new Vector3(.11f,height,.125f),wood);
            Part("Right stile",new Vector3(width-.055f,height*.5f,0),new Vector3(.11f,height,.125f),wood);
            Part("Top rail",new Vector3(width*.5f,height-.06f,0),new Vector3(width,.12f,.125f),wood);
            Part("Bottom rail",new Vector3(width*.5f,.06f,0),new Vector3(width,.12f,.125f),wood);
            Part("Middle rail",new Vector3(width*.5f,height*.48f,0),new Vector3(width,.12f,.125f),wood);
            Part("Bottom inset",new Vector3(width*.5f,height*.25f,0),new Vector3(width-.18f,height*.38f,.075f),inset);
            Part("Window divider",new Vector3(width*.5f,height*.74f,0),new Vector3(.045f,height*.43f,.095f),wood);
            Part("Window crossbar",new Vector3(width*.5f,height*.76f,0),new Vector3(width-.18f,.055f,.095f),wood);
            foreach (float y in new[]{height*.2f,height*.8f})
                Part("Hinge",new Vector3(.025f,y,0),new Vector3(.07f,.16f,.15f),iron);
            foreach (float z in new[]{-.09f,.09f})
                Part("Handle",new Vector3(width-.17f,height*.47f,z),new Vector3(.09f,.085f,.075f),handle);
            var collider=hinge.gameObject.AddComponent<BoxCollider>();
            collider.center=new Vector3(width*.5f,height*.5f,0);
            collider.size=new Vector3(width,height,DoorRules.Thickness);
        }
        private void Part(string label,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name=label;part.transform.SetParent(hinge,false);part.transform.localPosition=position;part.transform.localScale=scale;
            var collider=part.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=part.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;
        }
        private static Material Material(string name,Color color)
        {
            var material=new Material(Shader.Find("Standard")){name=name,hideFlags=HideFlags.DontSave};
            material.color=color;material.SetFloat("_Glossiness",.1f);return material;
        }
    }
}
