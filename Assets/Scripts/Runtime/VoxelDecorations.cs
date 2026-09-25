using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public static class VoxelDecorations
    {
        private static readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
        private static Material torchWood,torchFire,torchCore;

        public static bool Supports(Block id)
            =>id==Block.Chest||id==Block.Lantern||id==Block.Campfire||id==Block.Torch||Blocks.IsBed(id);

        public static GameObject Create(Block id,Transform parent,Vector3 localOrigin)
        {
            if(!Supports(id))return null;
            if(id==Block.Torch)return CreateTorch(parent,localOrigin);
            bool bed=Blocks.IsBed(id);
            string name=bed?((int)id%2==0?"bed_head":"bed_foot"):id==Block.Chest?"chest":id==Block.Lantern?"lantern":"campfire";
            if(!prefabs.TryGetValue(name,out var prefab))
            {
                prefab=Resources.Load<GameObject>("Models/"+name);
                prefabs[name]=prefab;
            }
            if(prefab==null)return null;
            var root=new GameObject(Blocks.Name(id)+" model");
            var fitted=new GameObject("Model bounds").transform;
            fitted.SetParent(root.transform,false);
            var model=Object.Instantiate(prefab,fitted,false);
            RemoveColliders(model);
            var renderers=model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds=new Bounds();bool found=false;
            foreach(var renderer in renderers)
            {
                if(!found){bounds=renderer.bounds;found=true;}
                else bounds.Encapsulate(renderer.bounds);
                renderer.shadowCastingMode=ShadowCastingMode.On;
                renderer.receiveShadows=true;
            }
            if(found)
            {
                Vector3 target=bed?new Vector3(1,.5625f,1):id==Block.Chest?new Vector3(.86f,.8f,.86f):id==Block.Lantern?new Vector3(.38f,.65f,.38f):new Vector3(.8f,.6f,.8f);
                Vector3 factor=new Vector3(target.x/Mathf.Max(.001f,bounds.size.x),target.y/Mathf.Max(.001f,bounds.size.y),target.z/Mathf.Max(.001f,bounds.size.z));
                if(id==Block.Lantern||id==Block.Campfire)factor=Vector3.one*Mathf.Min(factor.x,Mathf.Min(factor.y,factor.z));
                fitted.localScale=factor;
                fitted.localPosition=Vector3.Scale(new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z),factor);
            }
            root.transform.SetParent(parent,false);
            root.transform.localPosition=localOrigin+new Vector3(.5f,0,.5f);
            if(bed)root.transform.localRotation=Quaternion.Euler(0,((int)id-(int)Block.Bed)/2*90,0);
            return root;
        }

        private static GameObject CreateTorch(Transform parent,Vector3 origin)
        {
            if(torchWood==null)torchWood=Material("Torch wood",new Color(.39f,.22f,.095f),false);
            if(torchFire==null)torchFire=Material("Torch flame",new Color(1,.33f,.035f),true);
            if(torchCore==null)torchCore=Material("Torch core",new Color(1,.85f,.34f),true);
            var root=new GameObject("Torch model");
            root.transform.SetParent(parent,false);
            root.transform.localPosition=origin+new Vector3(.5f,0,.5f);
            Cube(root.transform,"Wood",new Vector3(0,.26f,0),new Vector3(.13f,.52f,.13f),torchWood);
            Cube(root.transform,"Flame",new Vector3(0,.62f,0),new Vector3(.19f,.23f,.19f),torchFire);
            Cube(root.transform,"Bright tip",new Vector3(0,.72f,0),new Vector3(.105f,.15f,.105f),torchCore);
            return root;
        }

        private static void Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name=name;
            part.transform.SetParent(parent,false);
            part.transform.localPosition=position;
            part.transform.localScale=scale;
            RemoveColliders(part);
            var renderer=part.GetComponent<Renderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=material==torchWood?ShadowCastingMode.On:ShadowCastingMode.Off;
        }

        private static Material Material(string name,Color color,bool emissive)
        {
            var material=new Material(Shader.Find("Standard")){name=name,hideFlags=HideFlags.DontSave};
            material.color=color;
            material.SetFloat("_Glossiness",.05f);
            if(emissive){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*1.6f);}
            return material;
        }

        private static void RemoveColliders(GameObject model)
        {
            foreach(var collider in model.GetComponentsInChildren<Collider>(true)){collider.enabled=false;Object.Destroy(collider);}
        }
    }
}
