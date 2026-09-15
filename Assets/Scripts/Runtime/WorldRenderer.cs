using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class WorldRenderer : MonoBehaviour
    {
        private sealed class ChunkView
        {
            public GameObject Root;
            public Mesh Solid, Liquid, Collision;
        }
        private readonly Dictionary<Cell, ChunkView> views = new Dictionary<Cell, ChunkView>();
        private readonly Queue<Cell> dirty = new Queue<Cell>();
        private readonly HashSet<Cell> pending = new HashSet<Cell>();
        private World world;
        private Material terrain, water;
        private Cell lastCenter = new Cell(int.MaxValue, 0, 0);
        private int lastRadius;
        public int ChunkCount => views.Count;
        private static readonly Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        private static readonly Vector3[][] corners = {
            new[] { new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(1,1,1),new Vector3(1,0,1) },
            new[] { new Vector3(0,0,1),new Vector3(0,1,1),new Vector3(0,1,0),new Vector3(0,0,0) },
            new[] { new Vector3(0,1,1),new Vector3(1,1,1),new Vector3(1,1,0),new Vector3(0,1,0) },
            new[] { new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,0,1),new Vector3(0,0,1) },
            new[] { new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,1),new Vector3(0,0,1) },
            new[] { new Vector3(0,0,0),new Vector3(0,1,0),new Vector3(1,1,0),new Vector3(1,0,0) }
        };
        public void Init(World value)
        {
            Clear(); world = value; world.Changed += Changed;
            if (!terrain)
            {
                terrain = new Material(Shader.Find("VoxelWilds/Terrain"));
                water = new Material(Shader.Find("VoxelWilds/Fluid"));
                var noise = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                var pixels = new Color[256];
                for (int i=0;i<pixels.Length;i++) { float n=.84f+(VoxelWilds.Core.Terrain.Hash(i%16,0,i/16,17)%100)*.0016f; pixels[i]=new Color(n,n,n,1); }
                noise.SetPixels(pixels); noise.Apply(); terrain.mainTexture=noise; water.mainTexture=noise;
            }
        }
        public void Clear()
        {
            if (world != null) world.Changed -= Changed;
            foreach (var chunk in views.Values) Release(chunk);
            views.Clear(); dirty.Clear(); pending.Clear(); lastCenter = new Cell(int.MaxValue, 0, 0);
        }
        private void Release(ChunkView view) { view.Root.SetActive(false); if (view.Solid) Destroy(view.Solid); if(view.Liquid) Destroy(view.Liquid); if(view.Collision) Destroy(view.Collision); Destroy(view.Root); }
        private void Queue(Cell key) { if (pending.Add(key)) dirty.Enqueue(key); }
        private void Changed(Cell p)
        {
            int cx=World.FloorDiv(p.X,16),cz=World.FloorDiv(p.Z,16);
            Queue(new Cell(cx,0,cz));
            if(p.X-cx*16==0) Queue(new Cell(cx-1,0,cz));
            if(p.X-cx*16==15) Queue(new Cell(cx+1,0,cz));
            if(p.Z-cz*16==0) Queue(new Cell(cx,0,cz-1));
            if(p.Z-cz*16==15) Queue(new Cell(cx,0,cz+1));
        }
        public void EnsureImmediate(Vector3 position)
        {
            int cx=Mathf.FloorToInt(position.x/16),cz=Mathf.FloorToInt(position.z/16);
            for(int z=cz-1;z<=cz+1;z++) for(int x=cx-1;x<=cx+1;x++) Build(new Cell(x,0,z));
            Physics.SyncTransforms();
        }
        public void Tick(Vector3 position,int radius)
        {
            if(world==null)return;
            Cell center=new Cell(Mathf.FloorToInt(position.x/16),0,Mathf.FloorToInt(position.z/16));
            if(center!=lastCenter || radius!=lastRadius)
            {
                lastCenter=center; lastRadius=radius;
                for(int ring=0;ring<=radius;ring++)
                    for(int z=-ring;z<=ring;z++) for(int x=-ring;x<=ring;x++)
                        if(Math.Max(Math.Abs(x),Math.Abs(z))==ring && !views.ContainsKey(center+new Cell(x,0,z))) Queue(center+new Cell(x,0,z));
                var remove=new List<Cell>();
                foreach(var pair in views) if(Math.Abs(pair.Key.X-center.X)>radius+1||Math.Abs(pair.Key.Z-center.Z)>radius+1) remove.Add(pair.Key);
                foreach(var key in remove){Release(views[key]);views.Remove(key);}
            }
            int count=0; float started=Time.realtimeSinceStartup;
            while(dirty.Count>0 && count<2 && Time.realtimeSinceStartup-started<.009f)
            {
                Cell key=dirty.Dequeue(); pending.Remove(key);
                if(Math.Abs(key.X-center.X)>radius || Math.Abs(key.Z-center.Z)>radius) continue;
                Build(key); count++;
            }
        }
        private void Build(Cell key)
        {
            if(world==null)return;
            world.EnsureChunk(key.X,key.Z);
            if(!views.TryGetValue(key,out var view))
            {
                view=new ChunkView{Root=new GameObject("Chunk "+key.X+", "+key.Z)};
                view.Root.transform.SetParent(transform,false); view.Root.transform.position=new Vector3(key.X*16,0,key.Z*16);
                view.Root.AddComponent<MeshFilter>();view.Root.AddComponent<MeshRenderer>().sharedMaterial=terrain;view.Root.AddComponent<MeshCollider>();
                var liquid=new GameObject("Water and lava");liquid.transform.SetParent(view.Root.transform,false);
                liquid.AddComponent<MeshFilter>();liquid.AddComponent<MeshRenderer>().sharedMaterial=water;
                views.Add(key,view);
            }
            var solid=new MeshBuilder();var fluid=new MeshBuilder();var collision=new MeshBuilder();
            for(int y=0;y<World.Height;y++)for(int z=0;z<16;z++)for(int x=0;x<16;x++)
            {
                var p=new Cell(key.X*16+x,y,key.Z*16+z);var voxel=world.Get(p);Block id=voxel.Id;
                if(id==Block.Air)continue;
                bool liquid=Blocks.IsFluid(id),portal=id==Block.PortalX||id==Block.PortalZ||id==Block.EndPortal;
                var builder=liquid||portal?fluid:solid;
                uint rgb=Blocks.ColorRgb(id); Color color=new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);
                if(liquid)color.a=id==Block.Water?.7f:1;
                if(portal)color.a=.82f;
                float height=liquid?(voxel.Level==8||Blocks.IsFluid(world.GetBlock(p.Up))?1:1-(voxel.Level+1)/9f):1;
                if(Blocks.IsBed(id))height=.55f;
                if(id==Block.Farmland)height=.9375f;
                if(id==Block.EndPortal)height=.1f;
                for(int face=0;face<6;face++)
                {
                    var offset=new Cell((int)normals[face].x,(int)normals[face].y,(int)normals[face].z);
                    Block neighbor=world.GetBlock(p+offset);
                    if(neighbor==id && (liquid||id==Block.Glass||portal))continue;
                    if(Blocks.IsSolid(neighbor)&&!Blocks.IsTransparent(neighbor)&&!(face==2 && height<1))continue;
                    Color tint=color;
                    if(id==Block.Grass && face!=2)tint=new Color(.51f,.36f,.22f);
                    float shade=face==2?1:face==3?.54f:face<2?.83f:.71f;
                    tint.r*=shade;tint.g*=shade;tint.b*=shade;
                    builder.Face(new Vector3(x,y,z),corners[face],normals[face],tint,height);
                    if(Blocks.IsSolid(id))collision.Face(new Vector3(x,y,z),corners[face],normals[face],tint,height);
                }
            }
            if(view.Solid)Destroy(view.Solid);if(view.Liquid)Destroy(view.Liquid);if(view.Collision)Destroy(view.Collision);
            view.Solid=solid.Mesh("Terrain "+key);view.Liquid=fluid.Mesh("Fluid "+key);
            view.Collision=collision.Mesh("Collision "+key);
            view.Root.GetComponent<MeshFilter>().sharedMesh=view.Solid;
            var collider=view.Root.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=view.Collision;
            view.Root.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh=view.Liquid;
        }
        private sealed class MeshBuilder
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Color> colors=new List<Color>();readonly List<Vector2> uvs=new List<Vector2>();readonly List<int> indices=new List<int>();
            public void Face(Vector3 origin,Vector3[] points,Vector3 normal,Color color,float height)
            {
                int start=vertices.Count;
                for(int i=0;i<4;i++){var p=points[i];p.y*=height;vertices.Add(origin+p);normals.Add(normal);colors.Add(color);uvs.Add(new Vector2(i==1||i==2?1:0,i>=2?1:0));}
                indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
            }
        }
        private void OnDestroy(){Clear();if(terrain)Destroy(terrain);if(water)Destroy(water);}
    }
}
