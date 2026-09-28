using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VoxelWilds.Core;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace VoxelWilds
{
    public sealed class PerformanceSmoke : MonoBehaviour
    {
        [Serializable] public sealed class Sample
        {
            public string Name;
            public int Frames, Chunks;
            public double CpuP50Ms, CpuP95Ms, CpuMaxMs, CpuTotalMs, AllocatedMb;
        }
        [Serializable] public sealed class Report
        {
            public string Cpu, Gpu;
            public bool AllocationCounterAvailable;
            public List<Sample> Samples = new List<Sample>();
            public int Checks;
        }
        private GameSession game;
        private string output;
        private readonly Report report = new Report();
        private bool finished;
        private float started;
        private void Start()
        {
            started=Time.realtimeSinceStartup;Application.runInBackground=true;
            Application.logMessageReceived+=OnLog;StartCoroutine(Guarded(Exercise()));
        }
        private void Update(){if(!finished&&Time.realtimeSinceStartup-started>180)Fail("Performance watchdog expired");}
        private IEnumerator Guarded(IEnumerator scenario)
        {
            var stack=new Stack<IEnumerator>();stack.Push(scenario);
            while(stack.Count>0&&!finished)
            {
                object value=null;bool moved=false;Exception error=null;
                try{moved=stack.Peek().MoveNext();if(moved)value=stack.Peek().Current;}catch(Exception caught){error=caught;}
                if(error!=null){Fail(error.ToString());yield break;}
                if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(value is IEnumerator nested)stack.Push(nested);else yield return value;
            }
            if(finished)yield break;
            File.WriteAllText(Path.Combine(output,"performance.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(output,"result.txt"),"VOXEL_PERFORMANCE_PASS "+report.Checks+" checks");
            Debug.Log("VOXEL_PERFORMANCE_PASS "+report.Checks+" checks");finished=true;Application.Quit(0);
        }
        private IEnumerator Exercise()
        {
            game=GameSession.Instance;
            output=Path.GetFullPath(GameSession.Argument("-voxel-artifacts")??throw new InvalidOperationException("Artifacts path required"));
            string saves=Path.GetFullPath(game.SaveDirectory),root=FindRoot();
            Require(output.StartsWith(Path.Combine(root,"artifacts")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
                &&saves.StartsWith(Path.Combine(root,".cache")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
                &&!Directory.EnumerateFiles(saves,"*.vws").Any(),"Performance test uses fresh isolated saves and artifacts");
            Directory.CreateDirectory(output);report.Cpu=SystemInfo.processorType;report.Gpu=SystemInfo.graphicsDeviceName;
            long allocationProbe=GC.GetAllocatedBytesForCurrentThread();var probe=new byte[4096];
            report.AllocationCounterAvailable=GC.GetAllocatedBytesForCurrentThread()>allocationProbe;GC.KeepAlive(probe);
            game.Settings.Fps=0;game.Settings.VSync=false;game.Settings.Fullscreen=false;
            GraphicsOptions.SetPreset(game.Settings,1);game.ApplySettings();
            game.NewWorld("Performance check",1453,true);game.SetPaused(true);
            game.Player.enabled=false;game.Mobs.enabled=false;game.Hud.enabled=false;game.Renderer.gameObject.SetActive(false);
            Type baseline=typeof(PerformanceSmoke).Assembly.GetType("VoxelWilds.LegacyRendererBenchmark");
            if(baseline!=null)yield return GeometryParity(baseline);
            if(baseline!=null)yield return Streaming(baseline,"baseline-2.0.5");
            yield return Streaming(typeof(WorldRenderer),"incremental-2.0.6");
            game.Renderer.gameObject.SetActive(true);
            for(int i=0;i<2000;i++)game.World.Set(new Cell(i%50,65+i/500,i/50%10),Block.Planks);
            Require(game.RequestAutosave()&&game.SavingInBackground,"Autosave submits an immutable snapshot to its background writer");
            game.Player.Inventory.Add(Items.Apple,3);
            Require(game.SaveWorld()&&!game.SavingInBackground,"Explicit save waits for the earlier background write before saving newer state");
            string path=Directory.EnumerateFiles(saves,"*.vws").Single();
            var saved=JsonUtility.FromJson<SessionSave>(SaveFile.Read(path,out _));
            Require(saved.Inventory.Count(Items.Apple)==3,"Background save cannot overwrite a newer explicit save");
        }
        private IEnumerator Streaming(Type type,string name)
        {
            var root=new GameObject(name);Component renderer=root.AddComponent(type);
            var world=new World(1453,Dimension.Overworld);type.GetMethod("Init").Invoke(renderer,new object[]{world});
            var tick=(Action<Vector3,int>)Delegate.CreateDelegate(typeof(Action<Vector3,int>),renderer,type.GetMethod("Tick"));
            var count=type.GetProperty("ChunkCount");var pending=type.GetProperty("PendingChunkCount");
            var samples=new List<double>();long allocated=0;Vector3 origin=new Vector3(8.5f,34,8.5f);
            for(int frame=0;frame<1200;frame++)
            {
                long memory=GC.GetAllocatedBytesForCurrentThread(),timer=Stopwatch.GetTimestamp();tick(origin,4);
                samples.Add((Stopwatch.GetTimestamp()-timer)*1000.0/Stopwatch.Frequency);allocated+=GC.GetAllocatedBytesForCurrentThread()-memory;
                yield return null;
                if((int)count.GetValue(renderer)>=81&&(pending==null||(int)pending.GetValue(renderer)==0))break;
            }
            Require((int)count.GetValue(renderer)==81,name+" streams all 81 requested chunks");
            Record(name+"-streaming",samples,allocated,(int)count.GetValue(renderer));
            if(renderer is WorldRenderer current)
            {
                Cell changed=new Cell(2,70,2);world.Set(changed,Block.Stone);current.Tick(origin,4);world.Set(changed,Block.GoldBlock);
                samples.Clear();allocated=0;
                for(int frame=0;frame<300&&current.PendingChunkCount>0;frame++)
                {
                    long memory=GC.GetAllocatedBytesForCurrentThread(),timer=Stopwatch.GetTimestamp();current.Tick(origin,4);
                    samples.Add((Stopwatch.GetTimestamp()-timer)*1000.0/Stopwatch.Frequency);allocated+=GC.GetAllocatedBytesForCurrentThread()-memory;yield return null;
                }
                Physics.SyncTransforms();
                Require(current.PendingChunkCount==0&&Physics.Raycast(new Vector3(2.5f,72,2.5f),Vector3.down,out var hit,1.5f)
                    &&Mathf.Abs(hit.point.y-71)<.01f,"Edits during incremental meshing publish the latest block and collider");
                world.Set(changed,Block.Air);
                for(int frame=0;frame<300&&current.PendingChunkCount>0;frame++){current.Tick(origin,4);yield return null;}
                Physics.SyncTransforms();
                Require(!Physics.Raycast(new Vector3(2.5f,72,2.5f),Vector3.down,1.5f),"Removing a block removes its old collider after remeshing");
                Record(name+"-editing",samples,allocated,current.ChunkCount);
                Cell distant=new Cell(34,70,2);world.Set(distant,Block.GoldBlock);
                current.Tick(origin,1);
                for(int frame=0;frame<300&&current.PendingChunkCount>0;frame++){current.Tick(origin,1);yield return null;}
                Require(root.transform.Find("Chunk 2, 0")!=null,"The dirty edge chunk stays retained just outside the smaller view radius");
                current.Tick(origin,2);
                for(int frame=0;frame<300&&current.PendingChunkCount>0;frame++){current.Tick(origin,2);yield return null;}
                Physics.SyncTransforms();
                Require(current.PendingChunkCount==0&&Physics.Raycast(new Vector3(34.5f,72,2.5f),Vector3.down,out var distantHit,1.5f)
                    &&Mathf.Abs(distantHit.point.y-71)<.01f,"A retained chunk rebuilds its pending edit when the view radius grows again");
            }
            type.GetMethod("Clear").Invoke(renderer,null);Destroy(root);yield return null;GC.Collect();yield return null;
        }
        private IEnumerator GeometryParity(Type baseline)
        {
            GameObject legacyRoot=new GameObject("Legacy mesh parity"),currentRoot=new GameObject("Incremental mesh parity");
            legacyRoot.SetActive(false);currentRoot.SetActive(false);
            Component legacy=legacyRoot.AddComponent(baseline);var current=currentRoot.AddComponent<WorldRenderer>();
            World expected=ParityWorld(),actual=ParityWorld();
            baseline.GetMethod("Init").Invoke(legacy,new object[]{expected});current.Init(actual);
            var position=new Vector3(40.5f,35,40.5f);
            baseline.GetMethod("EnsureImmediate").Invoke(legacy,new object[]{position});current.EnsureImmediate(position);
            Require(legacyRoot.transform.childCount==currentRoot.transform.childCount,"Synchronous meshing keeps the same chunk and light roots");
            for(int i=0;i<currentRoot.transform.childCount;i++)
            {
                Transform newChunk=currentRoot.transform.GetChild(i),oldChunk=legacyRoot.transform.Find(newChunk.name);
                Require(oldChunk!=null,"Legacy counterpart exists for "+newChunk.name);
                CompareMesh(oldChunk.GetComponent<MeshFilter>().sharedMesh,newChunk.GetComponent<MeshFilter>().sharedMesh,false,newChunk.name+" terrain");
                CompareMesh(oldChunk.GetChild(0).GetComponent<MeshFilter>().sharedMesh,newChunk.GetChild(0).GetComponent<MeshFilter>().sharedMesh,false,newChunk.name+" fluid");
                CompareMesh(oldChunk.GetComponent<MeshCollider>().sharedMesh,newChunk.GetComponent<MeshCollider>().sharedMesh,true,newChunk.name+" collision");
                var oldNames=new List<string>();var newNames=new List<string>();
                for(int child=1;child<oldChunk.childCount;child++)oldNames.Add(oldChunk.GetChild(child).name);
                for(int child=1;child<newChunk.childCount;child++)newNames.Add(newChunk.GetChild(child).name);
                oldNames.Sort(StringComparer.Ordinal);newNames.Sort(StringComparer.Ordinal);
                Require(oldNames.SequenceEqual(newNames),newChunk.name+" keeps every decoration model");
                yield return null;
            }
            baseline.GetMethod("Clear").Invoke(legacy,null);current.Clear();Destroy(legacyRoot);Destroy(currentRoot);yield return null;
        }
        private static World ParityWorld()
        {
            var world=new World(1453,Dimension.Overworld);
            for(int value=1;value<=(int)Block.EndFrame;value++)
            {
                Block block=(Block)value;world.Set(new Cell(24+value%12*2,80,24+value/12*2),block,Blocks.IsFluid(block)?(byte)(value%7):(byte)0);
            }
            world.Set(new Cell(31,79,31),Block.Planks);
            world.Set(new Cell(31,80,31),Block.Door,DoorRules.State(1,true));
            world.Set(new Cell(31,81,31),Block.Door,DoorRules.State(1,true,true));
            return world;
        }
        private void CompareMesh(Mesh expected,Mesh actual,bool collision,string label)
        {
            Require(expected.vertices.SequenceEqual(actual.vertices)&&expected.triangles.SequenceEqual(actual.triangles),label+" preserves every vertex and triangle");
            if(collision)return;
            Require(expected.normals.SequenceEqual(actual.normals)&&expected.colors.SequenceEqual(actual.colors)
                &&expected.uv.SequenceEqual(actual.uv)&&expected.uv2.SequenceEqual(actual.uv2),label+" preserves lighting, texture, and fluid attributes");
        }
        private void Record(string name,List<double> values,long bytes,int chunks)
        {
            values.Sort();report.Samples.Add(new Sample{Name=name,Frames=values.Count,Chunks=chunks,CpuP50Ms=values[values.Count/2],
                CpuP95Ms=values[Math.Min(values.Count-1,(int)(values.Count*.95))],CpuMaxMs=values[values.Count-1],CpuTotalMs=values.Sum(),AllocatedMb=bytes/1048576.0});
            Debug.Log("PERFORMANCE "+JsonUtility.ToJson(report.Samples[report.Samples.Count-1]));
        }
        private static string FindRoot()
        {
            for(var path=new DirectoryInfo(Application.dataPath);path!=null;path=path.Parent)
                if(Directory.Exists(Path.Combine(path.FullName,"Assets","Scripts"))&&Directory.Exists(Path.Combine(path.FullName,"ProjectSettings")))return path.FullName;
            throw new DirectoryNotFoundException("Run inside the Game project");
        }
        private void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);report.Checks++;Debug.Log("PERFORMANCE_CHECK "+message);}
        private void OnLog(string message,string trace,LogType type){if(!finished&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))Fail(message+"\n"+trace);}
        private void Fail(string message){if(finished)return;finished=true;Application.logMessageReceived-=OnLog;if(output!=null)File.WriteAllText(Path.Combine(output,"result.txt"),"VOXEL_PERFORMANCE_FAIL "+message);Debug.LogError(message);Application.Quit(1);}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
