using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class EndingSmoke : MonoBehaviour
    {
        private GameSession game;
        private string artifactDirectory,stage="initialization";
        private float started;
        private bool finished;
        private readonly List<string> checks=new List<string>();

        private void Start()
        {
            started=Time.realtimeSinceStartup;
            Application.runInBackground=true;
            Application.logMessageReceived+=OnLog;
            StartCoroutine(Guarded(Exercise()));
        }

        private void Update()
        {
            if(!finished&&Time.realtimeSinceStartup-started>110)Fail("110-second watchdog expired during "+stage);
        }

        private IEnumerator Guarded(IEnumerator scenario)
        {
            var stack=new Stack<IEnumerator>();stack.Push(scenario);
            while(stack.Count>0&&!finished)
            {
                object next=null;bool moved=false;Exception error=null;
                try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                catch(Exception caught){error=caught;}
                if(error!=null){Fail(stage+": "+error);yield break;}
                if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(next is IEnumerator nested)stack.Push(nested);else yield return next;
            }
            if(!finished)Finish();
        }

        private IEnumerator Exercise()
        {
            game=GameSession.Instance;
            Require(game!=null&&game.Player!=null&&game.Hud!=null,"Native scene creates its ending-screen components");
            ValidateDirectories();
            Require(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"Ending checks have a real graphics device");
            GraphicsOptions.SetPreset(game.Settings,1);
            game.Settings.Fullscreen=false;game.Settings.Fps=60;game.Settings.VSync=false;game.Settings.ViewDistance=2;
            game.ApplySettings();
            game.NewWorld("Beyond the last horizon",1453,false);
            game.Mobs.enabled=false;
            Require(!game.Ending&&!game.Hud.EndingVisible,"A new world starts without the victory overlay");
            EnterEnd();
            game.Player.Inventory.Slots[0]=new ItemStack(Items.IronPickaxe,1,87);
            game.Player.Inventory.Slots[1]=new ItemStack(Items.Crystal,2);
            game.Hud.OpenCrafting();
            SetField(game.Hud,"cursor",new ItemStack((int)Block.Log,4));
            ItemStack[] grid=Field<ItemStack[]>(game.Hud,"grid");grid[0]=new ItemStack(Items.Crystal,3);
            game.DropStack(game.Player.transform.position+new Vector3(8,2,0),new ItemStack(Items.Bread,2),125);
            DroppedItem drop=Field<List<DroppedItem>>(game,"drops").Last();
            stage="first dragon victory";
            game.DefeatDragon();
            Require(game.EndDragonDefeated&&game.Ending&&game.Hud.EndingVisible,"The first dragon defeat in the End opens the dedicated victory screen");
            Require(!game.Playing&&!game.Sleeping&&!game.Player.Dead,"Victory suspends gameplay without entering the death or sleep state");
            Require(Cursor.lockState==CursorLockMode.None&&Cursor.visible,"Victory releases the mouse for its menu controls");
            Require(!game.Hud.IsOpen&&game.Hud.CaptureTransient().Length==0,"Victory safely closes the crafting grid and cursor stack");
            Require(game.Player.Inventory.Count((int)Block.Log)==4&&game.Player.Inventory.Count(Items.Crystal)==5,
                "Closing the victory screen's previous UI returns every transient item exactly once");
            Require(game.Player.Inventory.Slots[0].Durability==87,"Victory preserves the equipped tool's durability");
            Vector3 position=game.Player.transform.position,dropPosition=drop.transform.position;
            float day=game.TimeOfDay,life=drop.Life,health=game.Player.Health;
            game.SetPaused(true);
            Require(game.Ending&&game.Hud.EndingVisible,"A focus-pause request cannot replace the victory overlay");
            yield return Seconds(1.05f);
            Require(Vector3.Distance(position,game.Player.transform.position)<.001f&&Mathf.Approximately(day,game.TimeOfDay),
                "Player motion and world time stay suspended while reading the ending");
            Require(Mathf.Approximately(health,game.Player.Health)&&Mathf.Approximately(life,drop.Life)
                &&Vector3.Distance(dropPosition,drop.transform.position)<.001f,
                "The ending cannot damage the player or consume dropped-item lifetime");
            Rect keep=game.Hud.EndingContinueRect,leave=game.Hud.EndingTitleRect;
            Require(keep.width>200&&leave.width>200&&!keep.Overlaps(leave),"Victory actions have separate, usable hit areas");
            float scale=Mathf.Max(.01f,Mathf.Min(Screen.height/720f,Screen.width/960f));
            Require(keep.xMin>=0&&leave.xMax<=Screen.width/scale&&keep.yMin>=0&&leave.yMax<=Screen.height/scale,
                "Both ending controls fit inside the current viewport");
            yield return CaptureEnding();
            Require(game.SaveWorld()&&game.LastSaveError==null,"Victory and recovered inventory save successfully");
            string savePath=Field<string>(game,"savePath");
            SessionSave saved=JsonUtility.FromJson<SessionSave>(SaveFile.Read(savePath,out _));
            Require(saved.EndDragonDefeated&&saved.Dimension==(int)Dimension.End,"The save records dragon completion and the current dimension");
            Require(saved.Inventory.Count(Items.Crystal)==5&&saved.Inventory.Count((int)Block.Log)==4
                &&(saved.TransientItems==null||saved.TransientItems.Length==0),"Saved inventory contains no lost or duplicate crafting items");

            stage="continue and persisted victory";
            game.ContinueAfterEnding();
            Require(!game.Ending&&!game.Hud.EndingVisible&&game.Playing,"Keep exploring restores the normal playing state");
            Require(game.World.Dimension==Dimension.End&&Vector3.Distance(position,game.Player.transform.position)<.001f,
                "Keep exploring leaves the player in the End beside their remaining loot");
            game.ContinueAfterEnding();game.DefeatDragon();
            Require(!game.Ending&&game.Player.Inventory.Count(Items.Crystal)==5,"Repeated completion or continue events cannot reopen the ending or duplicate items");
            game.LoadWorld(savePath);game.Mobs.enabled=false;
            Require(game.EndDragonDefeated&&!game.Ending&&!game.Hud.EndingVisible,"Reloading a completed world does not replay the victory screen");
            Require(game.Player.Inventory.Count(Items.Crystal)==5&&game.Player.Inventory.Slots[0].Durability==87,
                "Completion reload preserves inventory counts and tool wear");
            Require(Field<List<DroppedItem>>(game,"drops").Any(item=>item&&item.Stack.Id==Items.Bread&&item.Stack.Count==2),
                "Loot beside the player survives completion and reload");
            game.DefeatDragon();Require(!game.Ending,"A duplicate dragon event after reload does not replay the ending");
            game.Transfer(Dimension.Overworld);
            Require(game.World.Dimension==Dimension.Overworld&&!game.Ending,"The completed world can return from the End normally");

            stage="save and title action";
            game.NewWorld("Victory title path",1601,false);game.Mobs.enabled=false;EnterEnd();
            game.Player.Inventory.Slots[0]=new ItemStack(Items.Bread,7);
            game.DefeatDragon();Require(game.Ending,"A different world's first victory gets its own ending");
            string secondPath=Field<string>(game,"savePath");
            game.ReturnToTitle();
            Require(game.World==null&&!game.Ending&&!game.Hud.EndingVisible,"Save and title leaves the victory screen and unloads the world");
            Require(Cursor.lockState==CursorLockMode.None&&Cursor.visible,"The title screen retains an unlocked visible cursor");
            game.LoadWorld(secondPath);game.Mobs.enabled=false;
            Require(game.World!=null&&game.EndDragonDefeated&&!game.Ending&&game.Player.Inventory.Count(Items.Bread)==7,
                "Save and title persists completion and inventory for the next session");

            stage="automatic death recovery";
            game.NewWorld("Posthumous victory check",1602,false);game.Mobs.enabled=false;EnterEnd();
            game.Player.Health=0;game.Die();game.DefeatDragon();
            Require(game.Player.Dead&&!game.Ending&&!game.Hud.EndingVisible,"A dragon death cannot cover the player's death countdown with a victory screen");
            yield return Seconds(3.5f);
            Require(!game.Player.Dead&&game.World.Dimension==Dimension.Overworld&&!game.Ending,
                "Automatic respawn still completes without any respawn button after a posthumous victory");
            Require(game.SaveWorld()&&game.LastSaveError==null,"Final test state saves without errors");
            game.NewWorld("Victory death ordering check",1603,false);game.Mobs.enabled=false;EnterEnd();
            game.DefeatDragon();game.Player.Health=0;game.Die();
            Require(!game.Ending&&game.Player.Dead,"A same-frame death dismisses the victory overlay instead of hiding the countdown");
            yield return Seconds(3.5f);
            Require(!game.Player.Dead&&!game.Ending&&game.World.Dimension==Dimension.Overworld,
                "Automatic respawn finishes when death follows victory in the same frame");
        }

        private void EnterEnd()
        {
            MethodInfo method=typeof(GameSession).GetMethod("LoadDimension",BindingFlags.Instance|BindingFlags.NonPublic);
            if(method==null)throw new MissingMethodException("GameSession.LoadDimension");
            method.Invoke(game,new object[]{Dimension.End,new Vector3(8.5f,46.08f,8.5f),true});
            game.Mobs.enabled=false;
            Require(game.World.Dimension==Dimension.End,"Victory fixture enters the real End dimension");
        }

        private IEnumerator CaptureEnding()
        {
            stage="rendered ending screenshot";
            yield return new WaitForEndOfFrame();
            Texture2D image=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Require(image!=null&&image.width>=640&&image.height>=360,"The ending has a usable captured framebuffer");
                File.WriteAllBytes(Path.Combine(artifactDirectory,"01-victory-screen.png"),image.EncodeToPNG());
                Color32[] pixels=image.GetPixels32();var colors=new HashSet<int>();int bright=0;
                for(int y=0;y<image.height;y+=3)for(int x=0;x<image.width;x+=3)
                {
                    Color32 pixel=pixels[y*image.width+x];
                    colors.Add(((pixel.r>>3)<<10)|((pixel.g>>3)<<5)|(pixel.b>>3));
                    if(pixel.r>150&&pixel.g>150&&pixel.b>150)bright++;
                }
                Require(colors.Count>20&&bright>200,"The screenshot includes the rendered victory artwork and readable light text, not a hidden-window black frame");
            }
            finally{if(image!=null)Destroy(image);}
        }

        private static IEnumerator Seconds(float seconds)
        {
            float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;
        }

        private static T Field<T>(object target,string name)
        {
            FieldInfo field=target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
            if(field==null)throw new MissingFieldException(target.GetType().Name,name);return (T)field.GetValue(target);
        }

        private static void SetField(object target,string name,object value)
        {
            FieldInfo field=target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
            if(field==null)throw new MissingFieldException(target.GetType().Name,name);field.SetValue(target,value);
        }

        private void ValidateDirectories()
        {
            string savesArgument=GameSession.Argument("-voxel-saves"),artifactArgument=GameSession.Argument("-voxel-artifacts");
            if(string.IsNullOrWhiteSpace(savesArgument)||string.IsNullOrWhiteSpace(artifactArgument))
                throw new InvalidOperationException("Ending checks require fresh -voxel-saves and -voxel-artifacts directories.");
            string project=FindProjectRoot(),saves=Path.GetFullPath(savesArgument);
            if(!saves.StartsWith(Path.Combine(project,".cache")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Ending-check saves must stay under Game/.cache.");
            if(!string.Equals(saves.TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(game.SaveDirectory).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The game did not use the isolated ending-check save directory.");
            if(Directory.EnumerateFiles(saves,"*.vws",SearchOption.AllDirectories).Any())
                throw new InvalidOperationException("Ending checks cannot use a directory containing existing worlds.");
            artifactDirectory=Path.GetFullPath(artifactArgument);
            if(!artifactDirectory.StartsWith(Path.Combine(project,"artifacts")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Ending screenshots must stay under Game/artifacts.");
            Directory.CreateDirectory(artifactDirectory);
            Require(true,"Ending saves and screenshots are isolated from existing player data");
        }

        private static string FindProjectRoot()
        {
            foreach(string start in new[]{Application.dataPath,Directory.GetCurrentDirectory()})
            for(var directory=new DirectoryInfo(start);directory!=null;directory=directory.Parent)
                if(Directory.Exists(Path.Combine(directory.FullName,"Assets","Scripts"))&&Directory.Exists(Path.Combine(directory.FullName,"ProjectSettings")))return directory.FullName;
            throw new DirectoryNotFoundException("Run the ending-check executable inside its Game project.");
        }

        private void Require(bool condition,string description)
        {
            if(!condition)throw new InvalidOperationException(description);checks.Add(description);Debug.Log("VOXEL_ENDING_CHECK "+description);
        }

        private void OnLog(string condition,string stackTrace,LogType type)
        {
            if(!finished&&(type==LogType.Exception||type==LogType.Error||type==LogType.Assert))Fail("Runtime error during "+stage+": "+condition+"\n"+stackTrace);
        }

        private void Finish()
        {
            string report="VOXEL_ENDING_PASS "+checks.Count+" checks in "+(Time.realtimeSinceStartup-started).ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" seconds";
            try{File.WriteAllText(Path.Combine(artifactDirectory,"result.txt"),report+Environment.NewLine+string.Join(Environment.NewLine,checks));}
            catch(Exception error){Fail("Could not write ending report: "+error);return;}
            finished=true;Application.logMessageReceived-=OnLog;Debug.Log(report);Exit(0);
        }

        private void Fail(string message)
        {
            if(finished)return;finished=true;Application.logMessageReceived-=OnLog;
            string report="VOXEL_ENDING_FAIL "+message;
            try{if(artifactDirectory!=null)File.WriteAllText(Path.Combine(artifactDirectory,"result.txt"),report+Environment.NewLine+string.Join(Environment.NewLine,checks));}
            catch(Exception error){Debug.LogWarning("Could not write ending report: "+error.Message);}
            Debug.LogError(report);Exit(1);
        }

        private static void Exit(int code)
        {
#if UNITY_EDITOR
            if(Application.isBatchMode)UnityEditor.EditorApplication.Exit(code);else UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit(code);
#endif
        }

        private void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
