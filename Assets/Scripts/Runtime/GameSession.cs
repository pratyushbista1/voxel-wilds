using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    [Serializable] public sealed class EditSave { public int X,Y,Z,Id;public byte Level; }
    [Serializable] public sealed class ContainerSave { public int X,Y,Z;public ItemStack[] Slots;public Furnace Furnace; }
    [Serializable] public sealed class DropSave { public float X,Y,Z,Life;public ItemStack Stack; }
    [Serializable] public sealed class DimensionSave
    {
        public int Id;public List<EditSave> Edits=new List<EditSave>();public List<ContainerSave> Containers=new List<ContainerSave>();
        public List<DropSave> Drops=new List<DropSave>();public MobSnapshot[] Mobs;public string[] MobMarkers;
    }
    [Serializable] public sealed class SessionSave
    {
        public int Version=4, Seed, Dimension, Difficulty=2;public string Name;
        public float X=8.5f,Y=33.1f,Z=8.5f,Yaw,Pitch,Health=20,Hunger=20,Saturation=5,Day=.35f;
        public bool Creative,EndDragonDefeated,HasBed;public int BedX,BedY,BedZ;
        public Inventory Inventory=new Inventory();public ItemStack[] TransientItems;public List<DimensionSave> Dimensions=new List<DimensionSave>();public List<EditSave> EndEyes=new List<EditSave>();
    }
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        public World World { get; private set; }
        public PlayerController Player { get; private set; }
        public WorldRenderer Renderer { get; private set; }
        public MobDirector Mobs { get; private set; }
        public GameHud Hud { get; private set; }
        public GameSettings Settings=new GameSettings();
        public float TimeOfDay => save?.Day??.35f;
        public int Difficulty => save?.Difficulty??2;
        public int RenderDistance => Settings.ViewDistance;
        public bool EndDragonDefeated => save!=null&&save.EndDragonDefeated;
        public bool Playing => World!=null&&!Paused&&!Sleeping&&Player!=null&&!Player.Dead;
        public bool Paused { get; private set; }
        public bool Sleeping { get; private set; }
        public float DeathRemaining { get; private set; }
        public string WorldName=>save?.Name??"";
        public string LastSaveError { get; private set; }
        public string SaveDirectory { get; private set; }
        private SessionSave save;
        private string savePath;
        private FluidSimulation fluids;
        private PortalSimulation portals;
        private Light sun;
        private float autosave,portalTime,portalCooldown,sleepTimer;
        private readonly Dictionary<Cell,ContainerSave> containers=new Dictionary<Cell,ContainerSave>();
        private readonly List<DroppedItem> drops=new List<DroppedItem>();
        private readonly Dictionary<int,Sprite> dropSprites=new Dictionary<int,Sprite>();
        private ItemIconAtlas dropIcons;
        private GameObject clouds;
        private Material skyMaterial,cloudMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap(){if(FindFirstObjectByType<GameSession>()==null)new GameObject("Voxel Wilds").AddComponent<GameSession>();}
        private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;}
        private void Start()
        {
            SaveDirectory=Argument("-voxel-saves")??Path.GetFullPath(Path.Combine(Application.dataPath,"..","saves","unity"));
            try{Directory.CreateDirectory(SaveDirectory);}catch(Exception){SaveDirectory=Path.Combine(Application.persistentDataPath,"saves");Directory.CreateDirectory(SaveDirectory);}
            string settingsPath=Path.Combine(SaveDirectory,"settings.json");
            if(File.Exists(settingsPath))try{JsonUtility.FromJsonOverwrite(File.ReadAllText(settingsPath),Settings);}catch(Exception){Settings=new GameSettings();}
            Renderer=new GameObject("Voxel chunks").AddComponent<WorldRenderer>();
            Player=new GameObject("Player").AddComponent<PlayerController>();Player.Init(this);
            Player.gameObject.SetActive(false);
            Mobs=new GameObject("Mobs").AddComponent<MobDirector>();
            Hud=gameObject.AddComponent<GameHud>();Hud.Init(this);
            sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.96f,.86f);
            skyMaterial=new Material(Shader.Find("VoxelWilds/Sky"));RenderSettings.skybox=skyMaterial;
            cloudMaterial=new Material(Shader.Find("Standard"));cloudMaterial.color=new Color(.93f,.95f,.98f);cloudMaterial.SetFloat("_Glossiness",0);
            Player.Eye.clearFlags=CameraClearFlags.Skybox;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;ApplySettings();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(Argument("-voxel-smoke")!=null || Environment.GetCommandLineArgs().Contains("-voxel-smoke"))gameObject.AddComponent<SimulationSmoke>();
            if(Environment.GetCommandLineArgs().Contains("-voxel-visual-check"))gameObject.AddComponent<GraphicsSmoke>();
            if(Environment.GetCommandLineArgs().Contains("-voxel-interaction-check"))gameObject.AddComponent<InteractionSmoke>();
            if(Environment.GetCommandLineArgs().Contains("-voxel-inventory-check"))gameObject.AddComponent<InventorySmoke>();
        }
        public static string Argument(string key)
        {
            var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]==key)return args[i+1];return null;
        }
        public void ApplySettings()
        {
            GraphicsOptions.Apply(Settings,Player!=null?Player.Eye:null,sun,Renderer);
            if(World!=null)UpdateSky();
            if(!Application.isEditor && Screen.fullScreen!=Settings.Fullscreen)Screen.fullScreen=Settings.Fullscreen;
            if(SaveDirectory!=null)try{File.WriteAllText(Path.Combine(SaveDirectory,"settings.json"),JsonUtility.ToJson(Settings,true));}catch(Exception error){Notify("Settings could not be saved: "+error.Message);}
        }
        public void NewWorld(string name,int seed,bool creative)
        {
            save=new SessionSave{Name=string.IsNullOrWhiteSpace(name)?"New world":name.Trim(),Seed=seed,Creative=creative};
            savePath=Path.Combine(SaveDirectory,"world-"+Guid.NewGuid().ToString("N")+".vws");
            StartSavedWorld();SaveWorld();
        }
        public void LoadWorld(string path)
        {
            try
            {
                string full=Path.GetFullPath(path);if(!full.StartsWith(Path.GetFullPath(SaveDirectory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("World is outside the save directory.");
                var candidate=JsonUtility.FromJson<SessionSave>(SaveFile.Read(full,out bool recovered));Validate(candidate);
                save=candidate;savePath=full;StartSavedWorld();if(recovered)Notify("Recovered the world from its last valid backup.");
            }
            catch(Exception error){Notify("Could not open world: "+error.Message);}
        }
        private static void Validate(SessionSave data)
        {
            if(data==null||data.Version!=4||data.Inventory==null||data.Inventory.Slots?.Length!=36||data.Inventory.Armor?.Length!=4||data.Dimensions==null||data.Dimensions.Count>3||data.EndEyes==null)throw new FormatException("Invalid Unity world data.");
            if(!Finite(data.X)||!Finite(data.Y)||!Finite(data.Z)||Mathf.Abs(data.X)>1000000||Mathf.Abs(data.Z)>1000000||!Finite(data.Day)||!Finite(data.Health)||!Finite(data.Hunger))throw new FormatException("Invalid player coordinates or health.");
            foreach(var stack in data.Inventory.Slots.Concat(data.Inventory.Armor).Concat(new[]{data.Inventory.Offhand}))ValidateStack(stack);
            if(data.TransientItems!=null){if(data.TransientItems.Length>10)throw new FormatException("Invalid crafting data.");foreach(var stack in data.TransientItems)ValidateStack(stack);}
            foreach(var dim in data.Dimensions)
            {
                if(dim.Id<0||dim.Id>2||dim.Edits==null||dim.Edits.Count>1000000||dim.Containers==null||dim.Drops==null)throw new FormatException("Invalid dimension.");
                foreach(var entry in dim.Edits)if(entry.Y<0||entry.Y>=World.Height||entry.Id<0||entry.Id>(int)Block.EndFrame||Mathf.Abs((float)entry.X)>1000000||Mathf.Abs((float)entry.Z)>1000000||entry.Level>(entry.Id==(int)Block.Door?15:8))throw new FormatException("Invalid block edit.");
                foreach(var box in dim.Containers){if(box.Slots!=null){if(box.Slots.Length!=27)throw new FormatException("Invalid chest.");foreach(var stack in box.Slots)ValidateStack(stack);}if(box.Furnace!=null){ValidateStack(box.Furnace.Input);ValidateStack(box.Furnace.Fuel);ValidateStack(box.Furnace.Output);}}
                foreach(var drop in dim.Drops)ValidateStack(drop.Stack);
            }
            data.Dimension=Mathf.Clamp(data.Dimension,0,2);data.Inventory.Selected=Mathf.Clamp(data.Inventory.Selected,0,8);data.Day=Mathf.Repeat(data.Day,1);data.Difficulty=Mathf.Clamp(data.Difficulty,0,3);
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static void ValidateStack(ItemStack stack){if(stack!=null&&!stack.Empty&&(!Items.Exists(stack.Id)||stack.Count>Items.MaxStack(stack.Id)||stack.Count<0||stack.Durability<0||stack.Durability>Items.Durability(stack.Id)))throw new FormatException("Invalid item stack.");}
        private void StartSavedWorld()
        {
            Hud.Close();Player.Inventory=save.Inventory;Player.IsCreative=save.Creative;Player.Health=Mathf.Clamp(save.Health,0,20);Player.Hunger=Mathf.Clamp(save.Hunger,0,20);Player.Saturation=Mathf.Clamp(save.Saturation,0,20);
            Player.Pitch=save.Pitch;Player.transform.rotation=Quaternion.Euler(0,save.Yaw,0);
            LoadDimension((Dimension)save.Dimension,new Vector3(save.X,save.Y,save.Z));
            if(save.TransientItems!=null){foreach(var stack in save.TransientItems){if(stack==null||stack.Empty)continue;int left=Player.Inventory.Add(stack.Id,stack.Count,stack.Durability);if(left>0)DropStack(Player.transform.position+Vector3.up,new ItemStack(stack.Id,left,stack.Durability));}save.TransientItems=null;}
            if(Player.Dead){Player.ResetVitals();Respawn();}
            Paused=false;Sleeping=false;autosave=0;Hud.ShowGame();LockCursor();
        }
        private void LoadDimension(Dimension dimension,Vector3 landing,bool resolveLanding=true)
        {
            fluids?.Dispose();portals?.Dispose();Mobs.Clear();Renderer.Clear();foreach(var drop in drops)if(drop!=null)Destroy(drop.gameObject);drops.Clear();containers.Clear();
            World=new World(save.Seed,dimension);var state=save.Dimensions.Find(x=>x.Id==(int)dimension);
            if(state!=null)
            {
                World.ApplyEdits(state.Edits.Select(e=>new KeyValuePair<Cell,Voxel>(new Cell(e.X,e.Y,e.Z),new Voxel((Block)e.Id,e.Level))));
                foreach(var box in state.Containers)containers[new Cell(box.X,box.Y,box.Z)]=box;
            }
            portals=new PortalSimulation(World);portals.Tick();
            Renderer.Init(World);if(resolveLanding)landing=FindSafe(landing);Renderer.EnsureImmediate(landing);Player.gameObject.SetActive(true);Player.Teleport(landing);
            fluids=new FluidSimulation(World);Mobs.Init(this);
            if(state!=null){Mobs.RestoreMarkers(state.MobMarkers);Mobs.Restore(state.Mobs);foreach(var drop in state.Drops)if(drop.Stack!=null&&!drop.Stack.Empty&&drop.Life>0)DropStack(new Vector3(drop.X,drop.Y,drop.Z),drop.Stack,drop.Life);}
            save.Dimension=(int)dimension;portalCooldown=3;portalTime=0;BuildClouds();UpdateSky();
        }
        public Vector3 FindSafe(Vector3 preferred)
        {
            int px=Mathf.FloorToInt(preferred.x),pz=Mathf.FloorToInt(preferred.z),py=Mathf.Clamp(Mathf.FloorToInt(preferred.y),1,World.Height-3);
            for(int radius=0;radius<9;radius++)for(int z=-radius;z<=radius;z++)for(int x=-radius;x<=radius;x++)
            {
                if(radius>0&&Math.Max(Math.Abs(x),Math.Abs(z))!=radius)continue;
                for(int offset=0;offset<World.Height;offset++)
                {
                    int y=offset%2==0?py+offset/2:py-(offset+1)/2;if(y<1||y>World.Height-3)continue;
                    Cell p=new Cell(px+x,y,pz+z);
                    if(World.Solid(p.Down)&&World.GetBlock(p)==Block.Air&&World.GetBlock(p.Up)==Block.Air)return new Vector3(p.X+.5f,y+.08f,p.Z+.5f);
                }
            }
            int floor=World.Dimension==Dimension.End?44:32;
            for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++){World.Set(new Cell(px+x,floor,pz+z),World.Dimension==Dimension.End?Block.Obsidian:Block.Cobble);for(int y=1;y<=3;y++)World.Set(new Cell(px+x,floor+y,pz+z),Block.Air);}
            return new Vector3(px+.5f,floor+1.1f,pz+.5f);
        }
        private void Update()
        {
            if(World==null)return;
            float dt=Mathf.Min(Time.deltaTime,.1f);
            if(Input.GetKeyDown(KeyCode.F11)){Settings.Fullscreen=!Settings.Fullscreen;ApplySettings();}
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                if(Sleeping){Sleeping=false;LockCursor();}
                else if(Hud.IsOpen)Hud.Close();else SetPaused(!Paused);
            }
            if(Input.GetKeyDown(KeyCode.E)&&!Hud.TextInputFocused&&!Paused&&!Sleeping&&!Player.Dead){if(Hud.IsOpen)Hud.Close();else Hud.OpenInventory();}
            if(Player.Dead)
            {
                DeathRemaining-=Time.unscaledDeltaTime;if(DeathRemaining<=0)Respawn();return;
            }
            if(Sleeping){sleepTimer+=dt;if(sleepTimer>=2){save.Day=.27f;Sleeping=false;Notify("A new day begins.");SaveWorld();LockCursor();}return;}
            if(!Playing)return;
            save.Day=Mathf.Repeat(save.Day+dt/1200,1);UpdateSky();Renderer.Tick(Player.transform.position,Settings.ViewDistance);fluids.Tick(dt,512);
            foreach(var pair in containers)if(pair.Value.Furnace!=null&&World.GetBlock(pair.Key)==Block.Furnace)pair.Value.Furnace.Tick(dt);
            portals.Tick();portalCooldown-=dt;CheckPortal(dt);autosave+=dt;if(autosave>=20){autosave=0;SaveWorld();}
        }
        private void UpdateSky()
        {
            bool overworld=World.Dimension==Dimension.Overworld;
            float sunHeight=Mathf.Sin((save.Day-.25f)*Mathf.PI*2),day=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.16f,.3f,sunHeight));
            float twilight=(1-Mathf.Clamp01(Mathf.Abs(sunHeight)*3))*day;
            Color top=Color.Lerp(new Color(.018f,.033f,.075f),new Color(.25f,.52f,.8f),day);
            Color horizon=Color.Lerp(new Color(.065f,.085f,.15f),new Color(.73f,.83f,.88f),day);
            horizon=Color.Lerp(horizon,new Color(.9f,.49f,.26f),twilight*.78f);
            Color ambientSky=Color.Lerp(new Color(.18f,.23f,.34f),new Color(.62f,.70f,.80f),day)*Settings.Brightness;
            Color ambientGround=Color.Lerp(new Color(.09f,.11f,.16f),new Color(.34f,.32f,.28f),day)*Settings.Brightness;
            sun.transform.rotation=Quaternion.Euler(save.Day*360-90,30,0);Vector3 sunDirection=-sun.transform.forward;
            sun.color=Color.Lerp(new Color(.49f,.63f,1),Color.Lerp(new Color(1,.96f,.87f),new Color(1,.64f,.36f),twilight),day);
            sun.intensity=Mathf.Lerp(.18f,1.1f,day)*Settings.Brightness;
            if(sunHeight<-.1f)sun.transform.rotation=Quaternion.LookRotation(sunDirection);
            if(!overworld)
            {
                bool nether=World.Dimension==Dimension.Nether;
                top=nether?new Color(.105f,.018f,.012f):new Color(.012f,.006f,.026f);
                horizon=nether?new Color(.3f,.075f,.025f):new Color(.055f,.025f,.1f);
                ambientSky=(nether?new Color(.42f,.25f,.20f):new Color(.38f,.30f,.49f))*Settings.Brightness;
                ambientGround=ambientSky*.6f;sun.intensity=.3f*Settings.Brightness;sun.color=nether?new Color(1,.48f,.22f):new Color(.64f,.49f,.94f);sun.transform.rotation=Quaternion.Euler(55,30,0);
            }
            if(skyMaterial)
            {
                skyMaterial.SetColor("_SkyTop",top);skyMaterial.SetColor("_SkyHorizon",horizon);skyMaterial.SetColor("_SkyGround",Color.Lerp(horizon,new Color(.17f,.23f,.25f),.65f));
                skyMaterial.SetVector("_SunDirection",sunDirection);skyMaterial.SetColor("_SunColor",Color.Lerp(new Color(1,.96f,.77f),new Color(1,.48f,.17f),twilight)*2.5f);
                skyMaterial.SetFloat("_Night",1-day);skyMaterial.SetFloat("_Dimension",overworld?0:1);
            }
            Player.Eye.backgroundColor=horizon;RenderSettings.fog=Settings.Fog;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=horizon;
            RenderSettings.fogStartDistance=Settings.ViewDistance*9;RenderSettings.fogEndDistance=Settings.ViewDistance*16;
            RenderSettings.ambientSkyColor=ambientSky;RenderSettings.ambientEquatorColor=Color.Lerp(ambientSky,ambientGround,.4f);RenderSettings.ambientGroundColor=ambientGround;
            Shader.SetGlobalColor("_VoxelAmbientSky",ambientSky.linear);Shader.SetGlobalColor("_VoxelAmbientGround",ambientGround.linear);Shader.SetGlobalColor("_VoxelSkyReflection",horizon.linear);
            if(clouds){clouds.SetActive(Settings.Clouds&&overworld);clouds.transform.position=new Vector3(Player.transform.position.x+Mathf.Sin(Time.time*.003f)*16,79,Player.transform.position.z);}
        }
        private void BuildClouds()
        {
            if(clouds)Destroy(clouds);clouds=new GameObject("Clouds");
            for(int i=0;i<24;i++){var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.GetComponent<Collider>().enabled=false;Destroy(box.GetComponent<Collider>());box.transform.SetParent(clouds.transform,false);box.transform.localPosition=new Vector3((i%6)*35-87,Mathf.Sin(i*2)*1.5f,(i/6)*44-66);box.transform.localScale=new Vector3(13+i%3*5,1.3f+i%2,7+i%5);var renderer=box.GetComponent<Renderer>();renderer.sharedMaterial=cloudMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
        }
        public void SetPaused(bool paused){if(Player.Dead)return;Paused=paused;if(paused){Hud.Close();SaveWorld();}LockCursor();}
        public void LockCursor(){bool locked=World!=null&&!Paused&&!Sleeping&&!Player.Dead&&!Hud.IsOpen;Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;}
        public void Notify(string message){if(Hud!=null)Hud.Notify(message);Debug.Log(message);}
        public void SetDifficulty(int difficulty){if(save!=null)save.Difficulty=Mathf.Clamp(difficulty,0,3);}
        public bool SaveWorld()
        {
            if(World==null||save==null)return true;
            try
            {
                CaptureDimension();save.Inventory=Player.Inventory;save.TransientItems=Hud.CaptureTransient();save.X=Player.transform.position.x;save.Y=Player.transform.position.y;save.Z=Player.transform.position.z;save.Yaw=Player.transform.eulerAngles.y;save.Pitch=Player.Pitch;save.Health=Player.Health;save.Hunger=Player.Hunger;save.Saturation=Player.Saturation;
                Validate(save);SaveFile.Write(savePath,JsonUtility.ToJson(save));LastSaveError=null;return true;
            }
            catch(Exception error){LastSaveError=error.Message;Notify("Save failed. Keep the game open: "+error.Message);return false;}
        }
        private void CaptureDimension()
        {
            portals?.Tick();
            var state=new DimensionSave{Id=(int)World.Dimension,Mobs=Mobs.Capture(),MobMarkers=Mobs.CaptureMarkers()};
            foreach(var pair in World.Edits)state.Edits.Add(new EditSave{X=pair.Key.X,Y=pair.Key.Y,Z=pair.Key.Z,Id=(int)pair.Value.Id,Level=pair.Value.Level});
            state.Containers.AddRange(containers.Values);
            foreach(var drop in drops)if(drop!=null&&drop.Stack!=null&&!drop.Stack.Empty)state.Drops.Add(new DropSave{X=drop.transform.position.x,Y=drop.transform.position.y,Z=drop.transform.position.z,Life=drop.Life,Stack=drop.Stack.Clone()});
            save.Dimensions.RemoveAll(x=>x.Id==state.Id);save.Dimensions.Add(state);
        }
        public void ReturnToTitle(){Hud.Close();if(!SaveWorld())return;fluids?.Dispose();portals?.Dispose();Mobs.Clear();Renderer.Clear();foreach(var drop in drops)if(drop)Destroy(drop.gameObject);drops.Clear();Player.gameObject.SetActive(false);World=null;Paused=false;Hud.ShowTitle();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        public void Die(){Hud.Close();DeathRemaining=3;Sleeping=false;Paused=false;foreach(var item in Player.Inventory.TakeAll())DropStack(Player.transform.position+Vector3.up,item);SaveWorld();LockCursor();}
        private void Respawn()
        {
            Hud.Close();CaptureDimension();Vector3 destination=new Vector3(8.5f,33.1f,8.5f);
            if(World.Dimension!=Dimension.Overworld)LoadDimension(Dimension.Overworld,destination);
            if(save.HasBed&&BedRules.IsComplete(World,new Cell(save.BedX,save.BedY,save.BedZ)))destination=new Vector3(save.BedX+.5f,save.BedY+1,save.BedZ+.5f);
            else if(save.HasBed){save.HasBed=false;Notify("Your bed is missing. Returning to world spawn.");}
            Player.ResetVitals();destination=FindSafe(destination);Renderer.EnsureImmediate(destination);Player.Teleport(destination);DeathRemaining=0;SaveWorld();LockCursor();
        }
        public bool Use(bool hasTarget,Cell target,Cell adjacent)
        {
            var held=Player.Inventory.Held;int id=held?.Id??0;Block block=hasTarget?World.GetBlock(target):Block.Air;
            bool sneak=Input.GetKey(KeyCode.LeftShift);
            if(hasTarget&&!sneak)
            {
                if(block==Block.Door){DoorRules.Toggle(World,target);return true;}
                if(block==Block.Workbench){Hud.OpenCrafting();return true;}
                if(block==Block.Chest){Hud.OpenChest(Container(target).Slots);return true;}
                if(block==Block.Furnace){Hud.OpenFurnace(Container(target).Furnace);return true;}
                if(Blocks.IsBed(block)){Sleep(target);return true;}
                if(block==Block.EndFrame&&id==Items.EyeEnder)
                {
                    if(save.EndEyes.Any(p=>p.X==target.X&&p.Y==target.Y&&p.Z==target.Z)){Notify("This frame already has an eye.");return true;}
                    save.EndEyes.Add(new EditSave{X=target.X,Y=target.Y,Z=target.Z});Player.ConsumeHeld();Notify(save.EndEyes.Count+" / 12 eyes placed");SaveWorld();return true;
                }
            }
            if(id==Items.EnderPearl)
            {
                if(Player.Trace(Player.Eye.transform.position,Player.Eye.transform.forward,48,out var pearlHit,out var previous))
                {
                    Vector3 landing=FindSafe(new Vector3(previous.X+.5f,previous.Y+.1f,previous.Z+.5f));Renderer.EnsureImmediate(landing);Player.Teleport(landing);Player.ConsumeHeld();Player.Damage(5,landing);return true;
                }
            }
            if(!hasTarget)return false;
            if(id==Items.FlintSteel&&TryIgnite(adjacent)){if(!Player.IsCreative)Player.Inventory.WearSelected(1);return true;}
            if(id==Items.EmptyBucket && Blocks.IsFluid(block))
            {
                if(World.Get(target).Level!=0){Notify("Buckets collect source blocks, not flowing fluid.");return true;}
                int filled=block==Block.Water?Items.WaterBucket:Items.LavaBucket;
                if(!Player.IsCreative){Player.ConsumeHeld();int left=Player.Inventory.Add(filled,1);if(left>0)DropLoot(Player.transform.position,filled,left);}
                World.Set(target,Block.Air);return true;
            }
            if(Items.SpawnMob(id)!=null)
            {
                if(Player.IsCreative){var kind=MobRules.Parse(Items.SpawnMob(id));if(Items.SpawnMob(id)=="dragon")kind=MobKind.EndDragon;Mobs.Spawn(kind,new Vector3(adjacent.X+.5f,adjacent.Y,adjacent.Z+.5f));return true;}Notify("Spawn eggs are available in Creative.");return true;
            }
            if(id==Items.IronHoe&&(block==Block.Grass||block==Block.Dirt)&&World.GetBlock(target.Up)==Block.Air){World.Set(target,Block.Farmland);if(!Player.IsCreative)Player.Inventory.WearSelected(1);return true;}
            Block place=Items.PlaceBlock(id);if(place==Block.Air)return false;
            if(id==Items.WaterBucket&&World.Dimension==Dimension.Nether){Notify("The water evaporates in the Nether.");if(!Player.IsCreative)Player.Inventory.Slots[Player.Inventory.Selected]=new ItemStack(Items.EmptyBucket);return true;}
            if(!Blocks.IsReplaceable(World.GetBlock(adjacent)))return false;
            if(adjacent.Y<=0||adjacent.Y>=World.Height-1)return false;
            if(place==Block.Crop&&World.GetBlock(adjacent.Down)!=Block.Farmland){Notify("Plant seeds on tilled soil.");return true;}
            if(Blocks.IsSolid(place)&&new Bounds(new Vector3(adjacent.X+.5f,adjacent.Y+.5f,adjacent.Z+.5f),Vector3.one).Intersects(new Bounds(Player.transform.position+Vector3.up*.9f,new Vector3(.6f,1.8f,.6f))))return false;
            if(place==Block.Door)
            {
                var doorBounds=new Bounds(new Vector3(adjacent.X+.5f,adjacent.Y+1,adjacent.Z+.5f),new Vector3(1,2,1));
                if(doorBounds.Intersects(new Bounds(Player.transform.position+Vector3.up*.9f,new Vector3(.6f,1.8f,.6f))))return false;
                int facing=(4-Mathf.RoundToInt(Player.Eye.transform.eulerAngles.y/90))&3;
                if(!DoorRules.TryPlace(World,adjacent,facing)){Notify("Doors need a solid floor and two clear blocks of space.");return false;}
            }
            else if(Blocks.IsBed(place))
            {
                int facing=Mathf.RoundToInt(Player.Eye.transform.eulerAngles.y/90)&3;
                Cell second=adjacent+BedRules.Direction(facing);
                if(new Bounds(new Vector3(second.X+.5f,second.Y+BedRules.Height*.5f,second.Z+.5f),new Vector3(1,BedRules.Height,1)).Intersects(new Bounds(Player.transform.position+Vector3.up*.9f,new Vector3(.6f,1.8f,.6f))))return false;
                if(!BedRules.TryPlace(World,adjacent,facing))return false;
            }
            else World.Set(adjacent,place);
            if(!Player.IsCreative){if(id==Items.WaterBucket||id==Items.LavaBucket)Player.Inventory.Slots[Player.Inventory.Selected]=new ItemStack(Items.EmptyBucket);else Player.ConsumeHeld();}
            return true;
        }
        public void BreakBlock(Cell target)
        {
            Block id=World.GetBlock(target);if(float.IsInfinity(Blocks.Hardness(id)))return;
            Voxel supported=World.Get(target.Up);
            if(id!=Block.Door&&supported.Id==Block.Door&&!DoorRules.IsUpper(supported))BreakBlock(target.Up);
            if(containers.TryGetValue(target,out var box))
            {
                if(box.Slots!=null)foreach(var stack in box.Slots)if(stack!=null&&!stack.Empty)DropStack(new Vector3(target.X+.5f,target.Y+.5f,target.Z+.5f),stack);
                if(box.Furnace!=null)foreach(var stack in new[]{box.Furnace.Input,box.Furnace.Fuel,box.Furnace.Output})if(stack!=null&&!stack.Empty)DropStack(new Vector3(target.X+.5f,target.Y+.5f,target.Z+.5f),stack);
                containers.Remove(target);
            }
            if(id==Block.Door)DoorRules.Remove(World,target);else World.Set(target,Block.Air);
            if(!Player.IsCreative&&Items.CanHarvest(Player.Inventory.Held?.Id??0,id))
            {
                int drop=Blocks.Drop(id),count=id==Block.Clay?4:1;
                if(id==Block.Crop){drop=Items.Wheat;DropLoot(new Vector3(target.X+.5f,target.Y+.5f,target.Z+.5f),Items.Seeds,2);}
                if(id==Block.Gravel&&UnityEngine.Random.value<.1f)drop=Items.Flint;
                if(drop>0)DropLoot(new Vector3(target.X+.5f,target.Y+.5f,target.Z+.5f),drop,count);
            }
            portals.Tick();
        }
        private ContainerSave Container(Cell p)
        {
            if(containers.TryGetValue(p,out var container))return container;
            container=new ContainerSave{X=p.X,Y=p.Y,Z=p.Z};
            if(World.GetBlock(p)==Block.Furnace)container.Furnace=new Furnace();
            else
            {
                container.Slots=new ItemStack[27];var marker=World.Markers.FirstOrDefault(m=>m.Position==p&&m.Kind=="chest");
                if(marker.Kind=="chest")
                {
                    container.Slots[0]=new ItemStack(Items.Bread,4);container.Slots[1]=new ItemStack(Items.IronIngot,3);container.Slots[2]=new ItemStack(Items.Coal,6);container.Slots[3]=new ItemStack(Items.Seeds,8);
                    if(marker.Mob=="bastion"||marker.Mob=="fortress"){container.Slots[4]=new ItemStack(Items.GoldIngot,8);container.Slots[5]=new ItemStack(Items.Crystal,2);container.Slots[6]=new ItemStack(Items.IronSword);}
                }
            }
            containers.Add(p,container);return container;
        }
        private void Sleep(Cell p)
        {
            if(!BedRules.IsComplete(World,p)){Notify("This bed is incomplete.");return;}
            if(World.Dimension!=Dimension.Overworld){World.Set(p,Block.Air);Explode(new Vector3(p.X+.5f,p.Y+.5f,p.Z+.5f),4);return;}
            save.HasBed=true;save.BedX=p.X;save.BedY=p.Y;save.BedZ=p.Z;
            if(!MobRules.IsNight(TimeOfDay)){Notify("Respawn point set. You can sleep at night.");SaveWorld();return;}
            if(Mobs.HostileNear(Player.transform.position,8)){Notify("Monsters are too close to sleep.");return;}
            Sleeping=true;sleepTimer=0;LockCursor();
        }
        private bool TryIgnite(Cell p)
        {
            if(World.Dimension==Dimension.End)return false;
            if(PortalRules.TryIgnite(World,p,out _)){portals.Tick();return true;}
            Notify("Use flint and steel inside a complete obsidian rectangle.");return false;
        }
        private bool TouchesPortal(out Cell touched)
        {
            Vector3 feet=Player.transform.position;
            Cell low=PlayerController.ToCell(feet+new Vector3(-.3f,.05f,-.3f)),high=PlayerController.ToCell(feet+new Vector3(.3f,1.75f,.3f));
            var body=new Bounds(feet+Vector3.up*.9f,new Vector3(.6f,1.8f,.6f));
            for(int y=low.Y;y<=high.Y;y++)for(int z=low.Z;z<=high.Z;z++)for(int x=low.X;x<=high.X;x++)
            {
                Cell p=new Cell(x,y,z);Block id=World.GetBlock(p);if(!PortalRules.IsPortal(id))continue;
                var sheet=new Bounds(new Vector3(x+.5f,y+.5f,z+.5f),id==Block.PortalX?new Vector3(1,1,.125f):new Vector3(.125f,1,1));
                if(body.Intersects(sheet)){touched=p;return true;}
            }
            touched=default;return false;
        }
        private void CheckPortal(float dt)
        {
            Cell p=PlayerController.ToCell(Player.transform.position+Vector3.up*.1f);Block block=World.GetBlock(p);Block floor=World.GetBlock(p.Down);
            bool touching=TouchesPortal(out _);
            if(portalCooldown>0){if(touching)portalCooldown=3;portalTime=0;return;}
            if(block==Block.EndPortal||floor==Block.EndPortal)
            {
                if(World.Dimension==Dimension.End){if(EndDragonDefeated)Transfer(Dimension.Overworld);else{Notify("Defeat the dragon to open the exit.");portalCooldown=3;}}
                else if(Player.IsCreative||save.EndEyes.Count>=12)Transfer(Dimension.End);
                else{Notify("Place eyes in 12 portal frames to open the End.");portalCooldown=3;}return;
            }
            if(touching){portalTime+=dt;if(portalTime>=(Player.IsCreative?.05f:4))Transfer(World.Dimension==Dimension.Nether?Dimension.Overworld:Dimension.Nether);}
            else portalTime=0;
        }
        public void Transfer(Dimension destination)
        {
            if(destination==World.Dimension)return;
            Hud.Close();portals.Tick();CaptureDimension();Dimension from=World.Dimension;Vector3 old=Player.transform.position,position;
            bool alongX=!TouchesPortal(out Cell entry)||World.GetBlock(entry)==Block.PortalX;
            if(destination==Dimension.End)position=new Vector3(8.5f,45.1f,8.5f);
            else if(from==Dimension.End)position=new Vector3(8.5f,33.1f,8.5f);
            else{float factor=destination==Dimension.Nether?.125f:8;position=new Vector3(Mathf.Clamp(old.x*factor,-999900,999900),Mathf.Clamp(old.y,2,World.Height-6),Mathf.Clamp(old.z*factor,-999900,999900));}
            bool netherTravel=destination!=Dimension.End&&from!=Dimension.End;
            LoadDimension(destination,position,!netherTravel);
            if(netherTravel)
            {
                int radius=destination==Dimension.Nether?16:128;
                if(!PortalRules.TryNearest(World,PlayerController.ToCell(position),radius,out var arrival)&&!TryBuildArrivalPortal(position,alongX,out arrival))
                {
                    LoadDimension(from,old);Notify("No safe space for a destination portal.");return;
                }
                Vector3 landing=new Vector3(arrival.Origin.X+(arrival.AlongX?(arrival.Width+2)*.5f:.5f),arrival.Origin.Y+1.08f,arrival.Origin.Z+(arrival.AlongX?.5f:(arrival.Width+2)*.5f));
                Player.Teleport(landing);portals.Tick();portalCooldown=3;portalTime=0;
                Renderer.EnsureImmediate(Player.transform.position);
            }
            SaveWorld();Notify("Entered the "+destination+".");LockCursor();
        }
        private bool TryBuildArrivalPortal(Vector3 target,bool alongX,out PortalFrame frame)
        {
            Cell center=PlayerController.ToCell(target);frame=default;
            int maxY=World.Dimension==Dimension.Nether?78:World.Height-6;
            int preferred=Mathf.Clamp(center.Y-1,2,maxY);
            var protectedEdits=new HashSet<Cell>(World.Edits.Where(e=>e.Value.Id!=Block.Air).Select(e=>e.Key));
            for(int radius=0;radius<=16;radius++)for(int z=-radius;z<=radius;z++)for(int x=-radius;x<=radius;x++)
            {
                if(radius>0&&Math.Max(Math.Abs(x),Math.Abs(z))!=radius)continue;
                for(int offset=0;offset<World.Height*2;offset++)
                {
                    int y=offset%2==0?preferred+offset/2:preferred-(offset+1)/2;if(y<2||y>maxY)continue;
                    var candidate=new PortalFrame(new Cell(center.X+x,y,center.Z+z),2,3,alongX);
                    if(!ClearArrivalVolume(candidate,false,protectedEdits))continue;
                    BuildArrivalPortal(candidate,false);frame=candidate;return true;
                }
            }
            for(int radius=0;radius<=16;radius++)for(int z=-radius;z<=radius;z++)for(int x=-radius;x<=radius;x++)
            {
                if(radius>0&&Math.Max(Math.Abs(x),Math.Abs(z))!=radius)continue;
                var candidate=new PortalFrame(new Cell(center.X+x,preferred,center.Z+z),2,3,alongX);
                if(!ClearArrivalVolume(candidate,true,protectedEdits))continue;
                BuildArrivalPortal(candidate,true);frame=candidate;return true;
            }
            return false;
        }
        private bool ClearArrivalVolume(PortalFrame frame,bool excavate,HashSet<Cell> protectedEdits)
        {
            for(int depth=-1;depth<=1;depth++)for(int y=0;y<=4;y++)for(int x=0;x<=3;x++)
            {
                Cell p=frame.At(x,y)+new Cell(frame.AlongX?0:depth,0,frame.AlongX?depth:0);Block id=World.GetBlock(p);
                if(protectedEdits.Contains(p))return false;
                if(!excavate)
                {
                    if(y==0?!World.Solid(p):id!=Block.Air)return false;
                }
                else if(id==Block.Bedrock||id==Block.Chest||id==Block.Furnace||id==Block.Spawner||id==Block.Door||Blocks.IsBed(id)||id==Block.EndFrame||PortalRules.IsPortal(id))return false;
            }
            return true;
        }
        private void BuildArrivalPortal(PortalFrame frame,bool excavate)
        {
            if(excavate)for(int depth=-1;depth<=1;depth++)for(int y=0;y<=4;y++)for(int x=0;x<=3;x++)
                World.Set(frame.At(x,y)+new Cell(frame.AlongX?0:depth,0,frame.AlongX?depth:0),y==0?Block.Obsidian:Block.Air);
            for(int y=0;y<=4;y++)for(int x=0;x<=3;x++)World.Set(frame.At(x,y),x==0||x==3||y==0||y==4?Block.Obsidian:frame.Portal);
        }
        public void DefeatDragon(){save.EndDragonDefeated=true;Notify("The dragon has fallen. The exit portal is open.");SaveWorld();}
        public void Explode(Vector3 position,float radius)
        {
            Cell center=PlayerController.ToCell(position);
            for(int y=-Mathf.CeilToInt(radius);y<=radius;y++)for(int z=-Mathf.CeilToInt(radius);z<=radius;z++)for(int x=-Mathf.CeilToInt(radius);x<=radius;x++)
            {
                if(x*x+y*y+z*z>radius*radius)continue;Cell p=center+new Cell(x,y,z);Block id=World.GetBlock(p);
                if(id!=Block.Obsidian&&id!=Block.Bedrock&&id!=Block.EndFrame&&!float.IsInfinity(Blocks.Hardness(id))&&id!=Block.Air)World.Set(p,Block.Air);
            }
            float distance=Vector3.Distance(Player.transform.position,position);if(distance<radius*2)Player.Damage((1-distance/(radius*2))*16,position);
            Mobs.DamageInRadius(position,radius*2,24);Notify("Explosion!");
        }
        public void DropLoot(Vector3 position,int id,int count)=>DropStack(position,new ItemStack(id,count));
        public void DropStack(Vector3 position,ItemStack stack,float life=300)
        {
            if(stack==null||stack.Empty)return;int cap=Items.MaxStack(stack.Id);if(cap<=0)return;
            drops.RemoveAll(item=>item==null);
            int remaining=stack.Count;
            while(remaining>0)
            {
                int count=Mathf.Min(cap,remaining);remaining-=count;
                var obj=new GameObject(Items.Name(stack.Id));obj.transform.position=position;
                var visual=new GameObject("Item");visual.transform.SetParent(obj.transform,false);
                Block block=Items.PlaceBlock(stack.Id);
                if(stack.Id<=(int)Block.EndFrame&&Blocks.IsSolid(block)&&!Blocks.IsBed(block)&&block!=Block.Door&&block!=Block.Chest)
                {
                    visual.AddComponent<MeshFilter>().sharedMesh=Renderer.BlockPreview(block);
                    visual.AddComponent<MeshRenderer>().sharedMaterial=Renderer.TerrainMaterial;
                    visual.transform.localScale=Vector3.one*.25f;
                }
                else
                {
                    if(!dropSprites.TryGetValue(stack.Id,out var sprite))
                    {
                        if(dropIcons==null)dropIcons=new ItemIconAtlas();
                        Texture2D texture=dropIcons.Get(stack.Id);
                        sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,32);
                        dropSprites.Add(stack.Id,sprite);
                    }
                    visual.AddComponent<SpriteRenderer>().sprite=sprite;
                    visual.transform.localScale=Vector3.one*.45f;
                }
                var drop=obj.AddComponent<DroppedItem>();drop.Init(this,new ItemStack(stack.Id,count,stack.Durability),life,visual.transform);drops.Add(drop);
            }
        }
        private void OnApplicationFocus(bool focused){if(!focused&&World!=null&&!Player.Dead)SetPaused(true);}
        private void OnApplicationQuit(){Hud?.Close();SaveWorld();}
        private void OnDestroy(){fluids?.Dispose();portals?.Dispose();foreach(var sprite in dropSprites.Values)if(sprite)Destroy(sprite);dropSprites.Clear();dropIcons?.Dispose();if(skyMaterial)Destroy(skyMaterial);if(cloudMaterial)Destroy(cloudMaterial);if(Instance==this)Instance=null;}
    }
    public sealed class DroppedItem : MonoBehaviour
    {
        public ItemStack Stack;public float Life;private GameSession game;private float age,vertical;private Transform visual;
        public void Init(GameSession session,ItemStack stack,float life,Transform model=null){game=session;Stack=stack;Life=life;visual=model;}
        private void Update()
        {
            if(!game.Playing)return;float dt=Mathf.Min(Time.deltaTime,.1f);Life-=dt;age+=dt;if(Life<=0){Destroy(gameObject);return;}
            if(visual){visual.Rotate(0,dt*55,0);visual.localPosition=Vector3.up*(.05f+Mathf.Sin(age*2)*.025f);}
            Cell below=PlayerController.ToCell(transform.position-Vector3.up*.14f);Block id=game.World.GetBlock(below);
            if(id==Block.Lava){Destroy(gameObject);return;}
            Vector3 position=transform.position;
            position.y=DropPhysics.Fall(game.World,position.x,position.y,position.z,ref vertical,dt);
            transform.position=position;
            if(age>1&&Vector3.Distance(transform.position,game.Player.transform.position+Vector3.up*.7f)<1.8f)
            {
                int left=game.Player.Inventory.Add(Stack.Id,Stack.Count,Stack.Durability);Stack.Count=left;if(left==0)Destroy(gameObject);
            }
        }
    }
}
