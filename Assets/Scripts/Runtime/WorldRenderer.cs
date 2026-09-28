using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelWilds.Core;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace VoxelWilds
{
    public sealed class WorldRenderer : MonoBehaviour
    {
        private sealed class ChunkView
        {
            public GameObject Root;
            public Mesh Solid, Liquid, Collision;
            public MeshFilter SolidFilter, LiquidFilter;
            public MeshCollider Collider;
            public readonly Dictionary<Cell,Decoration> Decorations=new Dictionary<Cell,Decoration>();
            public readonly List<Glow> Glows=new List<Glow>();
        }
        private sealed class Decoration { public Block Id;public GameObject Root; }
        private struct Glow { public Vector3 Position;public Block Id; }
        private struct DecorationRequest { public Cell Position; public Voxel Voxel; public bool PairedDoor; }
        private sealed class BuildJob
        {
            public Cell Key;
            public int Phase, Row, Neighbor;
            public bool Invalidated;
            public readonly ChunkMeshSnapshot Snapshot = new ChunkMeshSnapshot();
            public readonly MeshBuilder Solid = new MeshBuilder(), Fluid = new MeshBuilder(), Collision = new MeshBuilder(true);
            public readonly List<DecorationRequest> Decorations = new List<DecorationRequest>();
            public readonly HashSet<Cell> DecorationCells = new HashSet<Cell>();
            public readonly List<Glow> Glows = new List<Glow>();
            public Mesh SolidMesh, FluidMesh, CollisionMesh;
            public void Begin(World source, Cell key)
            {
                Key=key;Phase=Row=Neighbor=0;Invalidated=false;Snapshot.Begin(source,key);
                Solid.Clear();Fluid.Clear();Collision.Clear();Decorations.Clear();DecorationCells.Clear();Glows.Clear();
            }
        }
        private readonly Dictionary<Cell, ChunkView> views = new Dictionary<Cell, ChunkView>();
        private readonly Queue<Cell> dirty = new Queue<Cell>();
        private readonly Queue<Cell> priority = new Queue<Cell>();
        private readonly HashSet<Cell> pending = new HashSet<Cell>();
        private readonly HashSet<Cell> priorityPending = new HashSet<Cell>();
        private readonly HashSet<Cell> needsRebuild = new HashSet<Cell>();
        private readonly BuildJob build = new BuildJob();
        private bool building;
        private readonly List<Cell> obsolete = new List<Cell>();
        private readonly List<Glow> lightCandidates = new List<Glow>();
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
        public int PendingChunkCount => pending.Count + (building ? 1 : 0);
        public int CompletedBuildCount { get; private set; }
        public double LastTickMilliseconds { get; private set; }
        public double LastCommitMilliseconds { get; private set; }
        public double LastGenerationMilliseconds { get; private set; }
        public bool IsBuilding => building;
        private const double BuildBudgetMilliseconds = 3.0;
        private static double Milliseconds(long start) => (Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
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
            views.Clear(); dirty.Clear(); priority.Clear(); pending.Clear();priorityPending.Clear();needsRebuild.Clear(); lastCenter = new Cell(int.MaxValue, 0, 0);
            building=false;build.Solid.Clear();build.Fluid.Clear();build.Collision.Clear();
            build.Decorations.Clear();build.DecorationCells.Clear();build.Glows.Clear();
            CompletedBuildCount=0;LastTickMilliseconds=LastCommitMilliseconds=LastGenerationMilliseconds=0;
            foreach(var light in lights)if(light)light.enabled=false;
        }
        private void Release(ChunkView view) { view.Root.SetActive(false); if (view.Solid) Destroy(view.Solid); if(view.Liquid) Destroy(view.Liquid); if(view.Collision) Destroy(view.Collision); Destroy(view.Root); }
        private void Queue(Cell key,bool changed=false)
        {
            if(changed && building && build.Key==key)build.Invalidated=true;
            needsRebuild.Add(key);
            if (pending.Add(key)) dirty.Enqueue(key);
            if(changed && priorityPending.Add(key))priority.Enqueue(key);
        }
        private void Changed(Cell p)
        {
            int cx=World.FloorDiv(p.X,16),cz=World.FloorDiv(p.Z,16);
            Queue(new Cell(cx,0,cz),true);
            if(p.X-cx*16==0) Queue(new Cell(cx-1,0,cz),true);
            if(p.X-cx*16==15) Queue(new Cell(cx+1,0,cz),true);
            if(p.Z-cz*16==0) Queue(new Cell(cx,0,cz-1),true);
            if(p.Z-cz*16==15) Queue(new Cell(cx,0,cz+1),true);
            if((p.X-cx*16==0||p.X-cx*16==15)&&(p.Z-cz*16==0||p.Z-cz*16==15))Queue(new Cell(cx+(p.X-cx*16==0?-1:1),0,cz+(p.Z-cz*16==0?-1:1)),true);
        }
        public void EnsureImmediate(Vector3 position)
        {
            if(world==null)return;
            if(building){Queue(build.Key);building=false;}
            int cx=Mathf.FloorToInt(position.x/16),cz=Mathf.FloorToInt(position.z/16);
            for(int z=cz-1;z<=cz+1;z++) for(int x=cx-1;x<=cx+1;x++)
            {
                Cell key=new Cell(x,0,z);pending.Remove(key);build.Begin(world,key);building=true;
                while(building)StepBuild();
            }
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
                        if(Math.Max(Math.Abs(x),Math.Abs(z))==ring)
                        {
                            Cell key=center+new Cell(x,0,z);
                            if(!views.ContainsKey(key)||needsRebuild.Contains(key))Queue(key);
                        }
                obsolete.Clear();
                foreach(var pair in views) if(Math.Abs(pair.Key.X-center.X)>radius+1||Math.Abs(pair.Key.Z-center.Z)>radius+1) obsolete.Add(pair.Key);
                foreach(var key in obsolete){Release(views[key]);views.Remove(key);}
            }
            long started=Stopwatch.GetTimestamp();
            if(building && (build.Invalidated || Math.Abs(build.Key.X-center.X)>radius || Math.Abs(build.Key.Z-center.Z)>radius))building=false;
            while(Milliseconds(started)<BuildBudgetMilliseconds)
            {
                if(!building)
                {
                    while(priority.Count>0 || dirty.Count>0)
                    {
                        Cell key;
                        if(priority.Count>0){key=priority.Dequeue();priorityPending.Remove(key);}else key=dirty.Dequeue();
                        if(!pending.Remove(key))continue;
                        if(Math.Abs(key.X-center.X)>radius || Math.Abs(key.Z-center.Z)>radius)continue;
                        build.Begin(world,key);building=true;break;
                    }
                    if(!building)break;
                }
                if(StepBuild())break;
            }
            lightTimer-=Time.unscaledDeltaTime;if(lightTimer<=0){lightTimer=.35f;UpdateLights(position);}
            LastTickMilliseconds=Milliseconds(started);
        }
        private bool StepBuild()
        {
            if(build.Invalidated){building=false;return false;}
            if(build.Phase==0)
            {
                int cx=build.Key.X+build.Neighbor%3-1,cz=build.Key.Z+build.Neighbor/3-1;
                bool unloaded=!world.IsLoaded(new Cell(cx*16,0,cz*16));long started=Stopwatch.GetTimestamp();
                world.EnsureChunk(cx,cz);
                if(unloaded)LastGenerationMilliseconds=Milliseconds(started);
                if(++build.Neighbor==9)build.Phase++;
                return unloaded;
            }
            if(build.Phase==1){build.Snapshot.CaptureRows(16);if(build.Snapshot.Complete)build.Phase++;return false;}
            if(build.Phase==2)
            {
                int end=Math.Min(World.Height*16,build.Row+4);
                while(build.Row<end){int y=build.Row/16,z=build.Row%16;for(int x=0;x<16;x++)BuildVoxel(x,y,z);build.Row++;}
                if(build.Row==World.Height*16)build.Phase++;
                return false;
            }
            if(build.Phase==3){build.SolidMesh=build.Solid.Mesh("Terrain "+build.Key,build.SolidMesh);build.Phase++;return false;}
            if(build.Phase==4){build.FluidMesh=build.Fluid.Mesh("Fluid "+build.Key,build.FluidMesh);build.Phase++;return false;}
            if(build.Phase==5){build.CollisionMesh=build.Collision.Mesh("Collision "+build.Key,build.CollisionMesh);build.Phase++;return false;}
            long commitStart=Stopwatch.GetTimestamp();CommitBuild();LastCommitMilliseconds=Milliseconds(commitStart);building=false;CompletedBuildCount++;
            return true;
        }
        private void BuildVoxel(int x,int y,int z)
        {
                var p=new Cell(build.Key.X*16+x,y,build.Key.Z*16+z);var voxel=build.Snapshot.Get(x,y,z);Block id=voxel.Id;
                if(id==Block.Air)return;
                if(id==Block.Door)
                {
                    if(DoorRules.IsUpper(voxel)&&DoorRules.Paired(voxel,build.Snapshot.Get(x,y-1,z)))return;
                    build.DecorationCells.Add(p);
                    build.Decorations.Add(new DecorationRequest{Position=p,Voxel=voxel,PairedDoor=!DoorRules.IsUpper(voxel)&&DoorRules.Paired(voxel,build.Snapshot.Get(x,y+1,z))});
                    return;
                }
                bool liquid=Blocks.IsFluid(id),portal=id==Block.PortalX||id==Block.PortalZ||id==Block.EndPortal;
                var builder=liquid||portal?build.Fluid:build.Solid;
                if(portal)
                {
                    build.Fluid.Portal(new Vector3(x,y,z),id);
                    return;
                }
                float height=liquid?(voxel.Level==8||Blocks.IsFluid(build.Snapshot.BlockAt(x,y+1,z))?1:1-(voxel.Level+1)/9f):1;
                if(Blocks.IsBed(id))height=.5625f;
                if(id==Block.Farmland)height=.9375f;
                bool decoration=VoxelDecorations.Supports(id);
                if(decoration)
                {
                    build.DecorationCells.Add(p);
                    build.Decorations.Add(new DecorationRequest{Position=p,Voxel=voxel});
                }
                if(id==Block.Crop||id==Block.NetherWart){build.Solid.Cross(new Vector3(x,y,z),id,id==Block.Crop?.85f:.55f,false);return;}
                if(Emission(id)>0&&(id!=Block.Lava||build.Snapshot.BlockAt(x,y+1,z)==Block.Air)&&(id!=Block.Lava||VoxelWilds.Core.Terrain.Hash(p.X,p.Y,p.Z,71)%19==0))build.Glows.Add(new Glow{Position=new Vector3(p.X+.5f,p.Y+.7f,p.Z+.5f),Id=id});
                if(id==Block.Grass&&build.Snapshot.BlockAt(x,y+1,z)==Block.Air&&VoxelWilds.Core.Terrain.Hash(p.X,p.Y,p.Z,41)%17==0)build.Solid.Cross(new Vector3(x,y+1,z),id,.62f,true);
                for(int face=0;face<6;face++)
                {
                    var offset=new Cell((int)normals[face].x,(int)normals[face].y,(int)normals[face].z);
                    Block neighbor=build.Snapshot.BlockAt(x+offset.X,y+offset.Y,z+offset.Z);
                    if(neighbor==id && (liquid||id==Block.Glass||id==Block.Leaves))continue;
                    if(Blocks.IsSolid(neighbor)&&!Blocks.IsTransparent(neighbor)&&!(face==2 && height<1))continue;
                    if(!decoration)builder.Face(new Vector3(x,y,z),corners[face],normals[face],id,face,height,liquid||portal?Vector4.one:CornerShade(new Cell(x,y,z),face));
                    if(Blocks.IsSolid(id))build.Collision.Face(new Vector3(x,y,z),corners[face],normals[face],id,face,height,Vector4.one);
                }
        }
        private void CommitBuild()
        {
            Cell key=build.Key;
            if(!views.TryGetValue(key,out var view))
            {
                view=new ChunkView{Root=new GameObject("Chunk "+key.X+", "+key.Z)};
                view.Root.SetActive(false);view.Root.transform.SetParent(transform,false);view.Root.transform.position=new Vector3(key.X*16,0,key.Z*16);
                view.SolidFilter=view.Root.AddComponent<MeshFilter>();var renderer=view.Root.AddComponent<MeshRenderer>();renderer.sharedMaterial=terrain;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.receiveShadows=true;
                view.Collider=view.Root.AddComponent<MeshCollider>();
                var liquid=new GameObject("Water and lava");liquid.transform.SetParent(view.Root.transform,false);view.LiquidFilter=liquid.AddComponent<MeshFilter>();liquid.AddComponent<MeshRenderer>().sharedMaterial=water;
                views.Add(key,view);
            }
            foreach(var request in build.Decorations)
            {
                Cell p=request.Position;Block id=request.Voxel.Id;
                if(!view.Decorations.TryGetValue(p,out var existing)||existing.Id!=id)
                {
                    if(existing!=null&&existing.Root){existing.Root.SetActive(false);Destroy(existing.Root);}
                    var position=new Vector3(p.X-key.X*16,p.Y,p.Z-key.Z*16);GameObject model;
                    if(id==Block.Door){model=new GameObject("Wooden door");model.transform.SetParent(view.Root.transform,false);model.transform.localPosition=position;model.AddComponent<DoorVisual>();}
                    else model=VoxelDecorations.Create(id,view.Root.transform,position);
                    existing=new Decoration{Id=id,Root=model};view.Decorations[p]=existing;
                }
                if(id==Block.Door)existing.Root.GetComponent<DoorVisual>().Configure(request.Voxel,request.PairedDoor);
            }
            obsolete.Clear();foreach(var entry in view.Decorations)if(!build.DecorationCells.Contains(entry.Key)){if(entry.Value.Root){entry.Value.Root.SetActive(false);Destroy(entry.Value.Root);}obsolete.Add(entry.Key);}foreach(var p in obsolete)view.Decorations.Remove(p);
            view.Glows.Clear();view.Glows.AddRange(build.Glows);
            Mesh previousSolid=view.Solid,previousLiquid=view.Liquid,previousCollision=view.Collision;
            view.Solid=build.SolidMesh;view.Liquid=build.FluidMesh;view.Collision=build.CollisionMesh;
            view.SolidFilter.sharedMesh=view.Solid;view.LiquidFilter.sharedMesh=view.Liquid;view.Collider.sharedMesh=null;view.Collider.sharedMesh=view.Collision;
            build.SolidMesh=previousSolid;build.FluidMesh=previousLiquid;build.CollisionMesh=previousCollision;
            view.Root.SetActive(true);
            needsRebuild.Remove(key);pending.Remove(key);
        }
        private bool Occludes(Cell p){Block id=build.Snapshot.BlockAt(p.X,p.Y,p.Z);return Blocks.IsSolid(id)&&!Blocks.IsTransparent(id);}
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
            lightCandidates.Clear();foreach(var view in views.Values)foreach(var glow in view.Glows)if((glow.Position-position).sqrMagnitude<28*28)
            {
                float distance=(glow.Position-position).sqrMagnitude;int index=0;
                while(index<lightCandidates.Count&&(lightCandidates[index].Position-position).sqrMagnitude<=distance)index++;
                if(index<12){lightCandidates.Insert(index,glow);if(lightCandidates.Count>12)lightCandidates.RemoveAt(12);}
            }
            int count=lightCandidates.Count;
            while(lights.Count<count){var obj=new GameObject("Warm block light");obj.transform.SetParent(transform,false);var light=obj.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForceVertex;lights.Add(light);}
            for(int i=0;i<lights.Count;i++)
            {
                var light=lights[i];light.enabled=i<count;if(i>=count)continue;var glow=lightCandidates[i];light.transform.position=glow.Position;
                light.color=glow.Id==Block.Lava?new Color(1,.35f,.075f):new Color(1,.67f,.3f);light.range=glow.Id==Block.Lava?5:9;light.intensity=1.55f+(glow.Id==Block.Campfire||glow.Id==Block.Torch?Mathf.Sin(Time.time*8+i)*.08f:0);
            }
        }
        public Mesh BlockPreview(Block id)
        {
            if(previews.TryGetValue(id,out var mesh))return mesh;
            var builder=new MeshBuilder();
            if(id==Block.PortalX||id==Block.PortalZ||id==Block.EndPortal)builder.Portal(-Vector3.one*.5f,id);
            else for(int face=0;face<6;face++)builder.Face(-Vector3.one*.5f,corners[face],normals[face],id,face,1,Vector4.one);
            mesh=builder.Mesh("Held "+id);previews.Add(id,mesh);return mesh;
        }
        private sealed class MeshBuilder
        {
            private readonly bool collisionOnly;
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Color> colors=new List<Color>();readonly List<Vector2> uvs=new List<Vector2>(),surfaces=new List<Vector2>();readonly List<int> indices=new List<int>();
            public MeshBuilder(bool collision=false){collisionOnly=collision;}
            public void Clear(){vertices.Clear();normals.Clear();colors.Clear();uvs.Clear();surfaces.Clear();indices.Clear();}
            public void Face(Vector3 origin,Vector3[] points,Vector3 normal,Block id,int face,float height,Vector4 shade)
            {
                int start=vertices.Count;
                for(int i=0;i<4;i++)
                {
                    var p=points[i];float u=face<2?p.z:p.x,v=face==2||face==3?p.z:p.y;
                    p.y*=height;vertices.Add(origin+p);
                    if(!collisionOnly){normals.Add(normal);colors.Add(new Color(shade[i],shade[i],shade[i],1));uvs.Add(BlockTextureAtlas.Uv(id,face,u,v));surfaces.Add(new Vector2(Emission(id),id==Block.Lava?1:id==Block.EndPortal?3:id==Block.PortalX||id==Block.PortalZ?2:0));}
                }
                if(shade.x+shade.z>shade.y+shade.w){indices.Add(start);indices.Add(start+1);indices.Add(start+3);indices.Add(start+1);indices.Add(start+2);indices.Add(start+3);}
                else{indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);}
            }
            public void Portal(Vector3 origin,Block id)
            {
                int face=id==Block.PortalX?5:id==Block.PortalZ?0:2;
                Vector3 shift=id==Block.PortalX?new Vector3(0,0,.5f):id==Block.PortalZ?new Vector3(-.5f,0,0):new Vector3(0,-.25f,0);
                Face(origin+shift,corners[face],WorldRenderer.normals[face],id,face,1,Vector4.one);
            }
            public void Cross(Vector3 origin,Block id,float height,bool grass)
            {
                for(int face=0;face<2;face++)
                {
                    int start=vertices.Count;Vector3 a=new Vector3(.14f,0,face==0?.14f:.86f),b=new Vector3(.86f,0,face==0?.86f:.14f);
                    for(int i=0;i<4;i++){Vector3 point=i==0||i==3?a:b;if(i>=2)point.y+=height;vertices.Add(origin+point);normals.Add(Vector3.up);colors.Add(Color.white);float u=i==1||i==2?1:0,v=i>=2?1:0;uvs.Add(grass?BlockTextureAtlas.GrassUv(u,v):BlockTextureAtlas.Uv(id,5,u,v));surfaces.Add(Vector2.zero);}
                    indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
                }
            }
            public Mesh Mesh(string name,Mesh mesh=null)
            {
                if(!mesh)mesh=new Mesh();else mesh.Clear();
                mesh.name=name;mesh.indexFormat=vertices.Count>ushort.MaxValue?IndexFormat.UInt32:IndexFormat.UInt16;mesh.SetVertices(vertices);
                if(!collisionOnly){mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.SetUVs(1,surfaces);}
                mesh.SetTriangles(indices,0);return mesh;
            }
        }
        private void OnDestroy(){Clear();if(build.SolidMesh)Destroy(build.SolidMesh);if(build.FluidMesh)Destroy(build.FluidMesh);if(build.CollisionMesh)Destroy(build.CollisionMesh);foreach(var mesh in previews.Values)if(mesh)Destroy(mesh);if(atlas)Destroy(atlas);if(terrain)Destroy(terrain);if(water)Destroy(water);}
    }
}
