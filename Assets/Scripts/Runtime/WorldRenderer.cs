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
            public readonly Dictionary<Cell,Decoration> Decorations=new Dictionary<Cell,Decoration>();
            public readonly List<Glow> Glows=new List<Glow>();
        }
        private sealed class Decoration { public Block Id;public GameObject Root; }
        private struct Glow { public Vector3 Position;public Block Id; }
        private readonly Dictionary<Cell, ChunkView> views = new Dictionary<Cell, ChunkView>();
        private readonly Queue<Cell> dirty = new Queue<Cell>();
        private readonly HashSet<Cell> pending = new HashSet<Cell>();
        private World world;
        private Material terrain, water;
        private Texture2D atlas;
        private int filtering,anisotropy=4;
        private float lightTimer;
        private readonly List<Light> lights=new List<Light>();
        private readonly Dictionary<Block,Mesh> previews=new Dictionary<Block,Mesh>();
        public Material TerrainMaterial=>terrain;
        public Texture2D Atlas=>atlas;
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
                atlas=BlockTextureAtlas.Create();terrain.mainTexture=atlas;water.mainTexture=atlas;
                SetTextureFiltering(filtering,anisotropy);
            }
        }
        public void SetTextureFiltering(int mode,int aniso)
        {
            filtering=Mathf.Clamp(mode,0,1);anisotropy=Mathf.Clamp(aniso,0,8);
            if(atlas){atlas.filterMode=filtering==0?FilterMode.Point:FilterMode.Bilinear;atlas.anisoLevel=anisotropy;}
        }
        public void Clear()
        {
            if (world != null) world.Changed -= Changed;
            foreach (var chunk in views.Values) Release(chunk);
            views.Clear(); dirty.Clear(); pending.Clear(); lastCenter = new Cell(int.MaxValue, 0, 0);
            foreach(var light in lights)if(light)light.enabled=false;
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
            if((p.X-cx*16==0||p.X-cx*16==15)&&(p.Z-cz*16==0||p.Z-cz*16==15))Queue(new Cell(cx+(p.X-cx*16==0?-1:1),0,cz+(p.Z-cz*16==0?-1:1)));
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
            lightTimer-=Time.unscaledDeltaTime;if(lightTimer<=0){lightTimer=.35f;UpdateLights(position);}
        }
        private void Build(Cell key)
        {
            if(world==null)return;
            world.EnsureChunk(key.X,key.Z);
            if(!views.TryGetValue(key,out var view))
            {
                view=new ChunkView{Root=new GameObject("Chunk "+key.X+", "+key.Z)};
                view.Root.transform.SetParent(transform,false); view.Root.transform.position=new Vector3(key.X*16,0,key.Z*16);
                view.Root.AddComponent<MeshFilter>();var terrainRenderer=view.Root.AddComponent<MeshRenderer>();terrainRenderer.sharedMaterial=terrain;terrainRenderer.shadowCastingMode=ShadowCastingMode.TwoSided;terrainRenderer.receiveShadows=true;view.Root.AddComponent<MeshCollider>();
                var liquid=new GameObject("Water and lava");liquid.transform.SetParent(view.Root.transform,false);
                liquid.AddComponent<MeshFilter>();liquid.AddComponent<MeshRenderer>().sharedMaterial=water;
                views.Add(key,view);
            }
            var solid=new MeshBuilder();var fluid=new MeshBuilder();var collision=new MeshBuilder();
            var decorationCells=new HashSet<Cell>();view.Glows.Clear();
            for(int y=0;y<World.Height;y++)for(int z=0;z<16;z++)for(int x=0;x<16;x++)
            {
                var p=new Cell(key.X*16+x,y,key.Z*16+z);var voxel=world.Get(p);Block id=voxel.Id;
                if(id==Block.Air)continue;
                if(id==Block.Door)
                {
                    if(DoorRules.IsUpper(voxel)&&DoorRules.Paired(voxel,world.Get(p.Down)))continue;
                    decorationCells.Add(p);
                    if(!view.Decorations.TryGetValue(p,out var existingDoor)||existingDoor.Id!=id)
                    {
                        if(existingDoor!=null&&existingDoor.Root){existingDoor.Root.SetActive(false);Destroy(existingDoor.Root);}
                        var model=new GameObject("Wooden door");model.transform.SetParent(view.Root.transform,false);model.transform.localPosition=new Vector3(x,y,z);model.AddComponent<DoorVisual>();
                        existingDoor=new Decoration{Id=id,Root=model};view.Decorations[p]=existingDoor;
                    }
                    existingDoor.Root.GetComponent<DoorVisual>().Configure(voxel,!DoorRules.IsUpper(voxel)&&DoorRules.Paired(voxel,world.Get(p.Up)));
                    continue;
                }
                bool liquid=Blocks.IsFluid(id),portal=id==Block.PortalX||id==Block.PortalZ||id==Block.EndPortal;
                var builder=liquid||portal?fluid:solid;
                float height=liquid?(voxel.Level==8||Blocks.IsFluid(world.GetBlock(p.Up))?1:1-(voxel.Level+1)/9f):1;
                if(Blocks.IsBed(id))height=.55f;
                if(id==Block.Farmland)height=.9375f;
                if(id==Block.EndPortal)height=.1f;
                bool decoration=VoxelDecorations.Supports(id);
                if(decoration)
                {
                    decorationCells.Add(p);
                    if(!view.Decorations.TryGetValue(p,out var existing)||existing.Id!=id)
                    {
                        if(existing!=null&&existing.Root)Destroy(existing.Root);
                        var model=VoxelDecorations.Create(id,view.Root.transform,new Vector3(x,y,z));
                        view.Decorations[p]=new Decoration{Id=id,Root=model};
                    }
                }
                if(id==Block.Crop||id==Block.NetherWart){solid.Cross(new Vector3(x,y,z),id,id==Block.Crop?.85f:.55f,false);continue;}
                if(Emission(id)>0&&(id!=Block.Lava||world.GetBlock(p.Up)==Block.Air)&&(id!=Block.Lava||VoxelWilds.Core.Terrain.Hash(p.X,p.Y,p.Z,71)%19==0))view.Glows.Add(new Glow{Position=new Vector3(p.X+.5f,p.Y+.7f,p.Z+.5f),Id=id});
                if(id==Block.Grass&&world.GetBlock(p.Up)==Block.Air&&VoxelWilds.Core.Terrain.Hash(p.X,p.Y,p.Z,41)%9==0)solid.Cross(new Vector3(x,y+1,z),id,.24f,true);
                for(int face=0;face<6;face++)
                {
                    var offset=new Cell((int)normals[face].x,(int)normals[face].y,(int)normals[face].z);
                    Block neighbor=world.GetBlock(p+offset);
                    if(neighbor==id && (liquid||id==Block.Glass||portal))continue;
                    if(Blocks.IsSolid(neighbor)&&!Blocks.IsTransparent(neighbor)&&!(face==2 && height<1))continue;
                    if(!decoration)builder.Face(new Vector3(x,y,z),corners[face],normals[face],id,face,height,liquid||portal?Vector4.one:CornerShade(p,face));
                    if(Blocks.IsSolid(id))collision.Face(new Vector3(x,y,z),corners[face],normals[face],id,face,height,Vector4.one);
                }
            }
            var obsolete=new List<Cell>();foreach(var entry in view.Decorations)if(!decorationCells.Contains(entry.Key)){if(entry.Value.Root){entry.Value.Root.SetActive(false);Destroy(entry.Value.Root);}obsolete.Add(entry.Key);}foreach(var p in obsolete)view.Decorations.Remove(p);
            if(view.Solid)Destroy(view.Solid);if(view.Liquid)Destroy(view.Liquid);if(view.Collision)Destroy(view.Collision);
            view.Solid=solid.Mesh("Terrain "+key);view.Liquid=fluid.Mesh("Fluid "+key);
            view.Collision=collision.Mesh("Collision "+key);
            view.Root.GetComponent<MeshFilter>().sharedMesh=view.Solid;
            var collider=view.Root.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=view.Collision;
            view.Root.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh=view.Liquid;
        }
        private bool Occludes(Cell p){Block id=world.GetBlock(p);return Blocks.IsSolid(id)&&!Blocks.IsTransparent(id);}
        private Vector4 CornerShade(Cell p,int face)
        {
            Vector4 shade=Vector4.one;Vector3 n=normals[face];Cell outside=p+new Cell((int)n.x,(int)n.y,(int)n.z);
            for(int i=0;i<4;i++)
            {
                Vector3 v=corners[face][i];Cell a,b;
                if(face<2){a=new Cell(0,v.y==0?-1:1,0);b=new Cell(0,0,v.z==0?-1:1);}
                else if(face<4){a=new Cell(v.x==0?-1:1,0,0);b=new Cell(0,0,v.z==0?-1:1);}
                else{a=new Cell(v.x==0?-1:1,0,0);b=new Cell(0,v.y==0?-1:1,0);}
                bool side1=Occludes(outside+a),side2=Occludes(outside+b);int blocked=side1&&side2?3:(side1?1:0)+(side2?1:0)+(Occludes(outside+a+b)?1:0);
                shade[i]=1-blocked*.18f;
            }
            return shade;
        }
        private static float Emission(Block id)=>id==Block.Glowstone?.9f:id==Block.Torch||id==Block.Lantern||id==Block.Campfire?1.1f:id==Block.Lava?1:0;
        private void UpdateLights(Vector3 position)
        {
            var candidates=new List<Glow>();foreach(var view in views.Values)foreach(var glow in view.Glows)if((glow.Position-position).sqrMagnitude<28*28)candidates.Add(glow);
            candidates.Sort((a,b)=>(a.Position-position).sqrMagnitude.CompareTo((b.Position-position).sqrMagnitude));
            int count=Mathf.Min(12,candidates.Count);
            while(lights.Count<count){var obj=new GameObject("Warm block light");obj.transform.SetParent(transform,false);var light=obj.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForceVertex;lights.Add(light);}
            for(int i=0;i<lights.Count;i++)
            {
                var light=lights[i];light.enabled=i<count;if(i>=count)continue;var glow=candidates[i];light.transform.position=glow.Position;
                light.color=glow.Id==Block.Lava?new Color(1,.35f,.075f):new Color(1,.67f,.3f);light.range=glow.Id==Block.Lava?5:9;light.intensity=1.55f+(glow.Id==Block.Campfire||glow.Id==Block.Torch?Mathf.Sin(Time.time*8+i)*.08f:0);
            }
        }
        public Mesh BlockPreview(Block id)
        {
            if(previews.TryGetValue(id,out var mesh))return mesh;
            var builder=new MeshBuilder();for(int face=0;face<6;face++)builder.Face(-Vector3.one*.5f,corners[face],normals[face],id,face,1,Vector4.one);
            mesh=builder.Mesh("Held "+id);previews.Add(id,mesh);return mesh;
        }
        private sealed class MeshBuilder
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Color> colors=new List<Color>();readonly List<Vector2> uvs=new List<Vector2>(),surfaces=new List<Vector2>();readonly List<int> indices=new List<int>();
            public void Face(Vector3 origin,Vector3[] points,Vector3 normal,Block id,int face,float height,Vector4 shade)
            {
                int start=vertices.Count;
                for(int i=0;i<4;i++)
                {
                    var p=points[i];float u=face<2?p.z:p.x,v=face==2||face==3?p.z:p.y;
                    p.y*=height;vertices.Add(origin+p);normals.Add(normal);colors.Add(new Color(shade[i],shade[i],shade[i],1));uvs.Add(BlockTextureAtlas.Uv(id,face,u,v));surfaces.Add(new Vector2(Emission(id),id==Block.Lava?1:id==Block.PortalX||id==Block.PortalZ||id==Block.EndPortal?2:0));
                }
                if(shade.x+shade.z>shade.y+shade.w){indices.Add(start);indices.Add(start+1);indices.Add(start+3);indices.Add(start+1);indices.Add(start+2);indices.Add(start+3);}
                else{indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);}
            }
            public void Cross(Vector3 origin,Block id,float height,bool grass)
            {
                for(int face=0;face<2;face++)
                {
                    int start=vertices.Count;Vector3 a=new Vector3(.14f,0,face==0?.14f:.86f),b=new Vector3(.86f,0,face==0?.86f:.14f);
                    Vector3[] points={a,b,b+Vector3.up*height,a+Vector3.up*height};
                    for(int i=0;i<4;i++){vertices.Add(origin+points[i]);normals.Add(Vector3.up);colors.Add(Color.white);float u=i==1||i==2?1:0,v=i>=2?1:0;uvs.Add(grass?BlockTextureAtlas.GrassUv(u,v):BlockTextureAtlas.Uv(id,5,u,v));surfaces.Add(Vector2.zero);}
                    indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.SetUVs(1,surfaces);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
            }
        }
        private void OnDestroy(){Clear();foreach(var mesh in previews.Values)if(mesh)Destroy(mesh);if(atlas)Destroy(atlas);if(terrain)Destroy(terrain);if(water)Destroy(water);}
    }
}
