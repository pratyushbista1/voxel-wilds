using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class GameHud : MonoBehaviour
    {
        public bool IsOpen => screen=="inventory"||screen=="crafting"||screen=="chest"||screen=="furnace";
        public bool TextInputFocused { get; private set; }
        private GameSession game;
        private string screen="title",previousScreen="title",worldName="New world",seed="1453",search="",message="",tooltip="";
        private float messageUntil,frames,frameTimer,fps;
        private bool creative;
        private ItemStack cursor;
        private ItemStack[] grid=new ItemStack[4],chest;
        private Furnace furnace;
        private Vector2 worldsScroll,catalogScroll,recipesScroll,settingsScroll;
        private int settingsTab;
        private readonly List<WorldEntry> worlds=new List<WorldEntry>();
        private ItemIconAtlas icons;
        private GUIStyle text,title,heading,small,button,field,number,inventoryText,inventorySmall;
        private Texture2D heart;
        private float width,height,scale;
        private readonly List<SlotTarget> dragTargets=new List<SlotTarget>();
        private int dragButton=-1;
        private bool recipeBookOpen,craftableOnly;
        private string recipeSearch="";
        private const float InventoryUnit=3;
        public Rect InventoryPanelRect { get; private set; }
        public Rect CraftingOutputRect { get; private set; }
        public Rect RecipeToggleRect { get; private set; }
        public Rect InventorySlotRect(int index)=>PanelRect(7+(index<9?index:(index-9)%9)*18,index<9?141:83+(index-9)/9*18,18,18);
        public Rect CraftingSlotRect(int index){int size=screen=="crafting"?3:2;return PanelRect((size==3?29:87)+index%size*18,(size==3?16:25)+index/size*18,18,18);}
        private sealed class SlotTarget
        {
            public string Key;
            public Func<ItemStack> Read;
            public Action<ItemStack> Write;
        }
        private sealed class WorldEntry { public string Path,Name,Details; }
        private static readonly Color Ink=new Color(.075f,.1f,.115f),Panel=new Color(.13f,.17f,.18f,.97f),Accent=new Color(.56f,.75f,.35f),Paper=new Color(.90f,.93f,.88f),Muted=new Color(.79f,.84f,.80f);
        public void Init(GameSession session){game=session;RefreshWorlds();}
        public void Notify(string value){message=value;messageUntil=Time.unscaledTime+7;}
        public ItemStack[] CaptureTransient()=>grid.Concat(new[]{cursor}).Where(s=>s!=null&&!s.Empty).Select(s=>s.Clone()).ToArray();
        public void ShowGame(){screen="game";ClearTextFocus();}
        public void ShowTitle(){screen="title";ClearTextFocus();RefreshWorlds();}
        public void OpenInventory(){Open("inventory",2);}
        public void OpenCrafting(){Open("crafting",3);}
        public void OpenChest(ItemStack[] slots){Open("chest",2);chest=slots;}
        public void OpenFurnace(Furnace value){Open("furnace",2);furnace=value;}
        private void Open(string value,int size){Close();screen=value;grid=new ItemStack[size*size];game.LockCursor();}
        public void Close()
        {
            if(game?.Player!=null)
            {
                ReturnStack(cursor);cursor=null;
                foreach(var stack in grid)ReturnStack(stack);Array.Clear(grid,0,grid.Length);
            }
            if(IsOpen||screen=="settings")screen=game?.World==null?"title":"game";
            chest=null;furnace=null;dragTargets.Clear();dragButton=-1;
            ClearTextFocus();
            if(game?.Hud!=null)game.LockCursor();
        }
        private void ClearTextFocus(){TextInputFocused=false;GUIUtility.keyboardControl=0;}
        private void UpdateTextFocus()
        {
            string focused=GUI.GetNameOfFocusedControl();
            TextInputFocused=focused=="CreativeSearch"||focused=="RecipeSearch"||focused=="WorldName"||focused=="WorldSeed";
        }
        private string TextField(Rect rect,string value,int maximum,string control)
        {
            GUI.SetNextControlName(control);string result=GUI.TextField(rect,value,maximum,field);UpdateTextFocus();return result;
        }
        private void ReturnStack(ItemStack stack)
        {
            if(stack==null||stack.Empty)return;int remaining=game.Player.Inventory.Add(stack.Id,stack.Count,stack.Durability);
            if(remaining>0&&game.World!=null)game.DropStack(game.Player.transform.position+Vector3.up,new ItemStack(stack.Id,remaining,stack.Durability));
        }
        private void RefreshWorlds()
        {
            worlds.Clear();if(game?.SaveDirectory==null)return;
            foreach(string path in Directory.GetFiles(game.SaveDirectory,"*.vws").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                try{var data=JsonUtility.FromJson<SessionSave>(SaveFile.Read(path,out bool recovered));worlds.Add(new WorldEntry{Path=path,Name=data.Name,Details=(data.Creative?"Creative":"Survival")+"  /  "+(Dimension)data.Dimension+"  /  "+File.GetLastWriteTime(path).ToString("dd MMM yyyy")+(recovered?"  /  backup":"")});}
                catch(Exception){worlds.Add(new WorldEntry{Path=path,Name=Path.GetFileNameWithoutExtension(path),Details="Unreadable world. Original file retained."});}
            }
        }
        private void Update(){frames++;frameTimer+=Time.unscaledDeltaTime;if(frameTimer>=.5f){fps=frames/frameTimer;frames=frameTimer=0;}}
        private void Styles()
        {
            if(text!=null)return;
            text=new GUIStyle(GUI.skin.label){fontSize=17,normal={textColor=Paper},wordWrap=true};
            title=new GUIStyle(text){fontSize=62,fontStyle=FontStyle.Bold,wordWrap=false};
            heading=new GUIStyle(text){fontSize=25,fontStyle=FontStyle.Bold};
            small=new GUIStyle(text){fontSize=13,normal={textColor=Muted}};
            number=new GUIStyle(text){fontSize=15,alignment=TextAnchor.LowerRight,fontStyle=FontStyle.Bold};
            inventoryText=new GUIStyle(text){fontSize=21,normal={textColor=new Color(.24f,.24f,.24f)},wordWrap=false};
            inventorySmall=new GUIStyle(inventoryText){fontSize=16,wordWrap=true};
            button=new GUIStyle(GUI.skin.button){fontSize=17,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            field=new GUIStyle(GUI.skin.textField){fontSize=19,padding=new RectOffset(10,10,10,7)};
            string[] pattern={"00000000","01100110","11111111","11111111","01111110","00111100","00011000","00000000"};
            heart=new Texture2D(8,8){filterMode=FilterMode.Point};var pixels=new Color[64];
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)pixels[(7-y)*8+x]=pattern[y][x]=='1'?Color.white:Color.clear;heart.SetPixels(pixels);heart.Apply();
        }
        private void OnGUI()
        {
            if(game==null)return;bool released=Event.current.rawType==EventType.MouseUp;Styles();scale=Mathf.Max(.01f,Mathf.Min(Screen.height/720f,Screen.width/960f));width=Screen.width/scale;height=Screen.height/scale;
            if(cursor!=null&&cursor.Empty)cursor=null;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));tooltip="";UpdateTextFocus();
            if(game.World==null&&screen!="settings")TitleScreen();
            else if(screen=="settings")SettingsScreen();
            else
            {
                Hud();
                if(game.Player.Dead){Shade();Label(new Rect(width/2-220,250,440,70),"You died",title,TextAnchor.MiddleCenter);Label(new Rect(width/2-250,335,500,40),"Returning in "+Mathf.CeilToInt(game.DeathRemaining)+"…",text,TextAnchor.MiddleCenter);}
                else if(game.Sleeping){Shade();Label(new Rect(width/2-250,300,500,50),"Sleeping until dawn",heading,TextAnchor.MiddleCenter);Label(new Rect(width/2-250,355,500,35),"Esc to leave bed",small,TextAnchor.MiddleCenter);}
                else if(IsOpen)InventoryScreen();
                else if(game.Paused)PauseScreen();
            }
            if(Time.unscaledTime<messageUntil)
            {
                Rect box=new Rect(width/2-330,28,660,60);Fill(box,new Color(.06f,.08f,.1f,.93f));Label(new Rect(box.x+16,box.y+9,box.width-32,44),message,text,TextAnchor.MiddleCenter);
            }
            if(!string.IsNullOrEmpty(game.LastSaveError))Label(new Rect(16,75,width-32,32),"SAVE ERROR: "+game.LastSaveError,text);
            Vector2 mouse=Event.current.mousePosition;
            if(cursor!=null&&!cursor.Empty&&IsOpen)
            {
                var shown=cursor.Clone();if(dragTargets.Count>0)shown.Count-=dragTargets.Sum(t=>InventoryTransfer.DistributionAmount(cursor,t.Read(),dragTargets.Count,dragButton==1));
                if(shown.Count>0)DrawStack(new Rect(mouse.x-21,mouse.y-21,48,48),shown);
            }
            else if(tooltip.Length>0)
            {
                float w=Mathf.Min(330,Mathf.Max(130,tooltip.Length*8));Rect tip=new Rect(Mathf.Min(mouse.x+14,width-w-8),Mathf.Min(mouse.y+20,670),w,38);Fill(tip,new Color(.03f,.04f,.055f,.97f));Label(new Rect(tip.x+8,tip.y+6,w-16,28),tooltip,small);
            }
            if(released)FinishDrag();
        }
        private void TitleScreen()
        {
            Fill(new Rect(0,0,width,height),new Color(.075f,.115f,.15f));
            for(int i=0;i<24;i++){float x=i*width/24;float h=85+(float)Math.Sin(i*.6f)*36;Fill(new Rect(x,height-h,width/24+1,h),new Color(.11f,.19f,.19f));Fill(new Rect(x,height-h, width/24+1,5),new Color(.20f,.30f,.23f));}
            float left=Mathf.Max(34,(width-1100)/2);
            Label(new Rect(left,42,600,28),"A WORLD OF YOUR OWN",small);
            Label(new Rect(left-2,73,740,85),"VOXEL WILDS",title);
            Label(new Rect(left,160,780,35),"Build a home. Explore the depths. Challenge the dragon.",text);
            Rect create=new Rect(left,230,350,365);PanelBox(create,"Create a world");
            Label(new Rect(left+22,292,305,24),"World name",small);worldName=TextField(new Rect(left+22,319,306,42),worldName,36,"WorldName");
            Label(new Rect(left+22,371,305,24),"Seed",small);seed=TextField(new Rect(left+22,397,306,40),seed,20,"WorldSeed");
            if(Button(new Rect(left+22,451,147,44),"Survival",!creative))creative=false;
            if(Button(new Rect(left+180,451,148,44),"Creative",creative))creative=true;
            if(Button(new Rect(left+22,521,306,49),"Create world",true)){int value;if(!int.TryParse(seed,out value)){unchecked{value=17;foreach(char c in seed)value=value*31+c;}}game.NewWorld(worldName,value,creative);}
            float wx=left+374,ww=Mathf.Max(300,width-left-wx);
            PanelBox(new Rect(wx,230,ww,365),"Saved worlds");
            if(worlds.Count==0)Label(new Rect(wx+24,310,ww-48,120),"Your Unity worlds will appear here.\n\nOlder JavaScript worlds are kept separately and are not overwritten.",text);
            else
            {
                worldsScroll=GUI.BeginScrollView(new Rect(wx+15,291,ww-30,287),worldsScroll,new Rect(0,0,ww-52,worlds.Count*76));
                for(int i=0;i<worlds.Count;i++)
                {
                    var entry=worlds[i];Rect row=new Rect(0,i*76,ww-54,68);Fill(row,new Color(.09f,.13f,.14f));
                    Label(new Rect(13,row.y+8,row.width-112,27),entry.Name,text);Label(new Rect(13,row.y+37,row.width-112,23),entry.Details,small);
                    if(Button(new Rect(row.width-91,row.y+13,80,41),"Play")){game.LoadWorld(entry.Path);break;}
                }
                GUI.EndScrollView();
            }
            if(Button(new Rect(left,621,155,43),"Settings")){previousScreen="title";screen="settings";}
            if(Button(new Rect(left+170,621,130,43),"Quit"))Application.Quit();
            Label(new Rect(width-355,647,320,36),"UNITY EDITION  /  2.0.3",small,TextAnchor.MiddleRight);
        }
        private void Hud()
        {
            if(IsOpen)return;
            var player=game.Player;float start=width/2-270,y=height-76;
            if(!IsOpen&&!game.Paused&&!player.Dead)
            {
                Fill(new Rect(width/2-7,height/2-1,14,2),new Color(1,1,1,.85f));Fill(new Rect(width/2-1,height/2-7,2,14),new Color(1,1,1,.85f));
                if(player.MiningProgress>0){Fill(new Rect(width/2-32,height/2+20,64,5),Ink);Fill(new Rect(width/2-32,height/2+20,64*Mathf.Clamp01(player.MiningProgress),5),Accent);}
            }
            for(int i=0;i<9;i++)Slot(new Rect(start+i*60,y,56,56),player.Inventory.Slots,i,false,i==player.Inventory.Selected);
            if(!player.IsCreative)
            {
                for(int i=0;i<10;i++)
                {
                    GUI.color=new Color(.22f,.10f,.12f);GUI.DrawTexture(new Rect(start+i*23,y-28,21,21),heart);
                    float amount=Mathf.Clamp01((player.Health-i*2)/2);if(amount>0){GUI.color=new Color(.91f,.24f,.28f);GUI.DrawTextureWithTexCoords(new Rect(start+i*23,y-28,21*amount,21),heart,new Rect(0,0,amount,1));}
                    GUI.color=Color.white;Fill(new Rect(start+309+i*23,y-24,17,14),new Color(.22f,.14f,.09f));if(player.Hunger>i*2)Fill(new Rect(start+309+i*23,y-24,17*Mathf.Clamp01((player.Hunger-i*2)/2),14),new Color(.77f,.48f,.24f));
                }
                if(player.Air<10){Fill(new Rect(width/2-110,y-45,220,6),Ink);Fill(new Rect(width/2-110,y-45,220*player.Air/10,6),new Color(.4f,.77f,.95f));}
            }
            if(ValidStack(player.Inventory.Held))BackedLabel(new Rect(width/2-260,y-65,520,30),Items.Name(player.Inventory.Held.Id),text,TextAnchor.MiddleCenter);
            if(ValidStack(player.Inventory.Offhand))DrawStack(new Rect(start-67,y,56,56),player.Inventory.Offhand);
            BackedLabel(new Rect(17,14,700,24),game.WorldName+"  /  "+game.World.Dimension+"  /  "+(player.IsCreative?"Creative":"Survival"),small);
            Vector3 p=player.transform.position;BackedLabel(new Rect(17,39,700,24),Mathf.FloorToInt(p.x)+", "+Mathf.FloorToInt(p.y)+", "+Mathf.FloorToInt(p.z)+"    "+Mathf.RoundToInt(fps)+" FPS",small);
            BackedLabel(new Rect(17,height-32,800,25),"WASD move   Space jump   E inventory   Esc pause",small);
            if(game.Mobs.DragonHealth>0)
            {
                Label(new Rect(width/2-250,79,500,29),"End Dragon",text,TextAnchor.MiddleCenter);Fill(new Rect(width/2-220,110,440,10),Ink);Fill(new Rect(width/2-220,110,440*game.Mobs.DragonHealth/game.Mobs.DragonMaxHealth,10),new Color(.65f,.31f,.80f));
            }
        }
        private void PauseScreen()
        {
            Shade();float x=width/2-180;PanelBox(new Rect(x-25,176,410,372),"Game paused");
            if(Button(new Rect(x,250,360,50),"Back to game",true))game.SetPaused(false);
            if(Button(new Rect(x,313,360,50),"Settings")){previousScreen="pause";screen="settings";}
            if(Button(new Rect(x,376,360,50),"Save world")){if(game.SaveWorld())Notify("World saved.");}
            if(Button(new Rect(x,439,360,50),"Save and return to title"))game.ReturnToTitle();
        }
        private void InventoryScreen()
        {
            Shade();bool craftingScreen=screen=="inventory"||screen=="crafting";
            float sidebar=game.Player.IsCreative||craftingScreen&&recipeBookOpen?128*InventoryUnit:0;
            InventoryPanelRect=new Rect((width-176*InventoryUnit-sidebar)/2+sidebar,(height-166*InventoryUnit)/2,176*InventoryUnit,166*InventoryUnit);
            float x=InventoryPanelRect.x,y=InventoryPanelRect.y;var inventory=game.Player.Inventory;
            PixelPanel(InventoryPanelRect);
            if(screen=="chest")
            {
                InventoryLabel(7,5,"Chest");
                for(int i=0;i<27;i++)Slot(PanelRect(7+i%9*18,17+i/9*18,18,18),chest,i,true);
            }
            else if(screen=="furnace")
            {
                InventoryLabel(66,5,"Furnace");
                var slots=new[]{furnace.Input,furnace.Fuel,furnace.Output};
                Slot(PanelRect(55,16,18,18),slots,0,true,accepts:s=>Furnace.SmeltingResult(s.Id)>0,region:"furnace");
                Slot(PanelRect(55,52,18,18),slots,1,true,accepts:s=>Furnace.FuelSeconds(s.Id)>0,region:"furnace");
                Slot(PanelRect(115,30,26,26),slots,2,true,false,true,region:"furnace");
                furnace.Input=slots[0];furnace.Fuel=slots[1];furnace.Output=slots[2];
                DrawArrow(PanelRect(79,35,24,16),new Color(.40f,.40f,.40f));
                GUI.BeginGroup(PanelRect(79,35,24*Mathf.Clamp01(furnace.CookProgress/Furnace.CookSeconds),16));DrawArrow(new Rect(0,0,24*InventoryUnit,16*InventoryUnit),Color.white);GUI.EndGroup();
                Fill(PanelRect(58,37,11,11),new Color(.34f,.34f,.34f));
                if(furnace.BurnTotal>0){float flame=Mathf.Clamp01(furnace.BurnRemaining/furnace.BurnTotal);Fill(PanelRect(58,48-11*flame,11,11*flame),new Color(1,.61f,.16f));Fill(PanelRect(61,45-6*flame,5,6*flame),new Color(1,.91f,.38f));}
            }
            else
            {
                int size=screen=="crafting"?3:2;
                if(size==2)
                {
                    DrawAvatar(PanelRect(26,7,50,70));
                    for(int i=0;i<4;i++)
                    {
                        Rect armorRect=PanelRect(7,7+i*18,18,18);Slot(armorRect,inventory.Armor,i,true,false,false,i);
                        if(!ValidStack(inventory.Armor[i])){Color old=GUI.color;GUI.color=new Color(.38f,.38f,.38f,.55f);GUI.DrawTexture(Inset(armorRect,6),Icon(Items.IronHelmet+i));GUI.color=old;}
                    }
                    var offhand=new[]{inventory.Offhand};Slot(PanelRect(77,61,18,18),offhand,0,true,region:"offhand");inventory.Offhand=offhand[0];
                    if(!ValidStack(inventory.Offhand)){Color old=GUI.color;GUI.color=new Color(.38f,.38f,.38f,.5f);GUI.DrawTexture(Inset(PanelRect(77,61,18,18),6),Icon(Items.Shield));GUI.color=old;}
                }
                InventoryLabel(size==3?29:87,5,"Crafting");
                for(int i=0;i<grid.Length;i++)Slot(CraftingSlotRect(i),grid,i,true);
                CraftingOutputRect=PanelRect(size==3?123:143,size==3?30:34,26,26);
                Rect output=CraftingOutputRect;SlotBackground(output);var result=Crafting.Preview(grid,size);if(result!=null)DrawStack(output,result);
                DrawArrow(PanelRect(size==3?90:126,size==3?35:39,size==3?24:14,16),new Color(.4f,.4f,.4f));
                if(output.Contains(Event.current.mousePosition)&&result!=null)
                {
                    tooltip=Items.Name(result.Id);
                    Event e=Event.current;
                    if(e.type==EventType.MouseDown&&e.button<=1)
                    {
                        ClearTextFocus();
                        if(e.shift){int maximum=0;while(maximum++<64&&Crafting.TryCraftInto(grid,size,inventory)){} }
                        else if(cursor==null || Inventory.Stackable(cursor,result)&&cursor.Count+result.Count<=Items.MaxStack(result.Id)){var crafted=Crafting.Craft(grid,size);if(cursor==null)cursor=crafted;else cursor.Count+=crafted.Count;}
                        e.Use();
                    }
                    else if(!TextInputFocused&&e.type==EventType.KeyDown&&e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha9)
                    {
                        int index=e.keyCode-KeyCode.Alpha1;var destination=ValidStack(inventory.Slots[index])?inventory.Slots[index]:null;
                        if(destination==null||Inventory.Stackable(destination,result)&&destination.Count+result.Count<=Items.MaxStack(result.Id))
                        {var crafted=Crafting.Craft(grid,size);if(destination==null)inventory.Slots[index]=crafted;else destination.Count+=crafted.Count;}
                        e.Use();
                    }
                }
                RecipeToggleRect=PanelRect(size==3?7:104,size==3?46:61,20,18);
                if(PixelButton(RecipeToggleRect,"",recipeBookOpen)){recipeBookOpen=!recipeBookOpen;ClearTextFocus();}
                DrawBook(Inset(RecipeToggleRect,9));
                if(RecipeToggleRect.Contains(Event.current.mousePosition))tooltip=recipeBookOpen?"Hide recipe book":"Show recipe book";
            }
            if(screen!="inventory")InventoryLabel(7,74,"Inventory");
            for(int i=9;i<36;i++)Slot(InventorySlotRect(i),inventory.Slots,i,true);
            for(int i=0;i<9;i++)Slot(InventorySlotRect(i),inventory.Slots,i,true);
            Label(new Rect(0,InventoryPanelRect.yMax+13,width,25),"Left: move / drag evenly   Right: split / drag one   Shift: transfer   1-9: swap",small,TextAnchor.MiddleCenter);
            Label(new Rect(0,InventoryPanelRect.yMax+37,width,25),"Double-click: collect matching items   Q: drop one   Ctrl+Q: drop stack   E / Esc: close",small,TextAnchor.MiddleCenter);
            if(game.Player.IsCreative)CreativeCatalog(x-128*InventoryUnit,y);
            else if(craftingScreen&&recipeBookOpen)RecipeBook(x-128*InventoryUnit,y);
            Rect combined=new Rect(x-sidebar,y,InventoryPanelRect.width+sidebar,InventoryPanelRect.height);
            if(Event.current.type==EventType.MouseDown&&Event.current.button<=1&&!combined.Contains(Event.current.mousePosition)&&cursor!=null)
            {
                int amount=Event.current.button==1?1:cursor.Count;game.DropStack(game.Player.transform.position+game.Player.transform.forward+Vector3.up,new ItemStack(cursor.Id,amount,cursor.Durability));
                cursor.Count-=amount;if(cursor.Empty)cursor=null;dragTargets.Clear();dragButton=-1;Event.current.Use();
            }
        }
        private void CreativeCatalog(float x,float y)
        {
            PixelPanel(new Rect(x,y,124*InventoryUnit,166*InventoryUnit));Label(new Rect(x+18,y+13,330,30),"Creative inventory",inventoryText);
            search=TextField(new Rect(x+18,y+49,336,34),search,40,"CreativeSearch");
            var ids=Items.CreateCreativeInventory().Where(id=>Items.Name(id).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            catalogScroll=GUI.BeginScrollView(new Rect(x+18,y+96,339,383),catalogScroll,new Rect(0,0,318,Mathf.CeilToInt(ids.Length/5f)*63));
            for(int i=0;i<ids.Length;i++)
            {
                Rect rect=new Rect(i%5*63,i/5*63,57,57);SlotBackground(rect);DrawStack(rect,new ItemStack(ids[i]));
                if(rect.Contains(Event.current.mousePosition))
                {
                    tooltip=Items.Name(ids[i]);
                    if(Event.current.type==EventType.MouseDown&&Event.current.button<=1){ClearTextFocus();int count=Event.current.button==1?1:Items.MaxStack(ids[i]);ReturnStack(cursor);cursor=new ItemStack(ids[i],count);dragTargets.Clear();dragButton=-1;Event.current.Use();}
                }
            }
            GUI.EndScrollView();
        }
        private void RecipeBook(float x,float y)
        {
            PixelPanel(new Rect(x,y,124*InventoryUnit,166*InventoryUnit));Label(new Rect(x+18,y+13,300,30),"Recipe book",inventoryText);
            recipeSearch=TextField(new Rect(x+18,y+49,336,34),recipeSearch,40,"RecipeSearch");
            if(PixelButton(new Rect(x+18,y+92,336,32),craftableOnly?"Craftable recipes":"All recipes",craftableOnly))craftableOnly=!craftableOnly;
            int size=screen=="crafting"?3:2;
            var recipes=Crafting.Recipes.Where(r=>RecipeFits(r,size)&&r.Name.IndexOf(recipeSearch,StringComparison.OrdinalIgnoreCase)>=0&&(!craftableOnly||CanFillRecipe(r))).ToArray();
            recipesScroll=GUI.BeginScrollView(new Rect(x+18,y+137,339,280),recipesScroll,new Rect(0,0,318,Mathf.CeilToInt(recipes.Length/5f)*63));
            for(int index=0;index<recipes.Length;index++)
            {
                var recipe=recipes[index];bool can=CanFillRecipe(recipe);Rect rect=new Rect(index%5*63,index/5*63,57,57);
                Fill(rect,can?new Color(.30f,.43f,.28f):new Color(.47f,.38f,.37f));SlotBackground(Inset(rect,3));
                DrawStack(Inset(rect,3),new ItemStack(recipe.Output,recipe.Count));
                if(rect.Contains(Event.current.mousePosition))
                {
                    tooltip=recipe.Name+(can?"":" (missing ingredients)");
                    if(Event.current.type==EventType.MouseDown&&Event.current.button<=1)
                    {ClearTextFocus();if(can)FillRecipe(recipe,size);else Notify("Needed: "+string.Join(", ",recipe.Ingredients.Select(p=>p.Value+" "+Items.Name(p.Key))));Event.current.Use();}
                }
            }
            GUI.EndScrollView();
            Label(new Rect(x+18,y+430,336,52),recipes.Length==0?"No matching recipes.":"Select a recipe to place its ingredients. Take the result to craft.",inventorySmall);
        }
        private bool RecipeFits(Recipe recipe,int size)=>recipe.Shapeless?recipe.Pattern.Length<=size*size:recipe.Width<=size&&recipe.Height<=size;
        private bool CanFillRecipe(Recipe recipe)=>recipe.Ingredients.All(p=>game.Player.Inventory.Count(p.Key)+grid.Where(s=>s!=null&&s.Id==p.Key).Sum(s=>s.Count)>=p.Value);
        private void FillRecipe(Recipe recipe,int size)
        {
            if(!RecipeFits(recipe,size)||!CanFillRecipe(recipe))return;
            var contents=grid.Where(s=>s!=null&&!s.Empty).Select(s=>s.Clone()).ToList();Array.Clear(grid,0,grid.Length);
            for(int i=0;i<recipe.Pattern.Length;i++)
            {
                int id=recipe.Pattern[i];if(id==0)continue;
                var available=contents.FirstOrDefault(s=>s.Id==id&&s.Count>0);
                if(available!=null)available.Count--;else game.Player.Inventory.Remove(id,1);
                int destination=recipe.Shapeless?i:i/recipe.Width*size+i%recipe.Width;grid[destination]=new ItemStack(id);
            }
            foreach(var stack in contents)ReturnStack(stack);
            dragTargets.Clear();dragButton=-1;
        }
        private void Slot(Rect rect,ItemStack[] items,int index,bool interactive,bool selected=false,bool output=false,int armor=-1,Func<ItemStack,bool> accepts=null,string region=null)
        {
            if(items==null||index<0||index>=items.Length)return;
            SlotBackground(rect,selected);
            var stack=ValidStack(items[index])?items[index]:null;string key=(region??items.GetHashCode().ToString())+":"+index;
            var shown=stack;
            if(dragTargets.Any(t=>t.Key==key)&&cursor!=null)
            {shown=stack?.Clone()??new ItemStack(cursor.Id,0,cursor.Durability);shown.Count+=InventoryTransfer.DistributionAmount(cursor,stack,dragTargets.Count,dragButton==1);}
            if(shown!=null&&!shown.Empty)DrawStack(rect,shown);
            if(!interactive||!rect.Contains(Event.current.mousePosition))return;
            Fill(Inset(rect,3),new Color(1,1,1,.19f));
            if(stack!=null)tooltip=Items.Name(stack.Id)+(Items.Durability(stack.Id)>0?"  "+stack.Durability+" / "+Items.Durability(stack.Id):"");
            Event e=Event.current;
            bool Accepts(ItemStack item)=>item==null||item.Empty||!output&&(armor<0||Items.ArmorSlot(item.Id)==armor)&&(accepts==null||accepts(item));
            if(!TextInputFocused&&e.type==EventType.KeyDown && e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha9)
            {
                int hotbar=e.keyCode-KeyCode.Alpha1;var target=ValidStack(game.Player.Inventory.Slots[hotbar])?game.Player.Inventory.Slots[hotbar]:null;
                if(!ReferenceEquals(items,game.Player.Inventory.Slots)||index!=hotbar)
                {InventoryTransfer.SwapHotbar(ref stack,ref target,output,Accepts);items[index]=stack;game.Player.Inventory.Slots[hotbar]=target;}
                e.Use();return;
            }
            if(!TextInputFocused&&e.type==EventType.KeyDown&&e.keyCode==KeyCode.Q&&stack!=null)
            {
                int amount=e.control?stack.Count:1;game.DropStack(game.Player.transform.position+game.Player.transform.forward+Vector3.up,new ItemStack(stack.Id,amount,stack.Durability));
                stack.Count-=amount;if(stack.Empty)items[index]=null;e.Use();return;
            }
            if(e.type==EventType.MouseDrag&&dragButton>=0&&cursor!=null)
            {
                if(Accepts(cursor)&&!output&&InventoryTransfer.CanDistribute(cursor,stack))AddDragTarget(items,index,key,region);
                e.Use();return;
            }
            if(e.type!=EventType.MouseDown||e.button>1)return;
            ClearTextFocus();dragTargets.Clear();dragButton=-1;
            if(e.clickCount>=2&&e.button==0&&cursor!=null&&Items.MaxStack(cursor.Id)>1)
            {
                CollectMatching(region=="furnace"?items:null);e.Use();return;
            }
            if(e.shift&&stack!=null)QuickMove(items,index);
            else if(cursor!=null&&!output&&Accepts(cursor)&&InventoryTransfer.CanDistribute(cursor,stack))
            {
                dragButton=e.button;AddDragTarget(items,index,key,region);
            }
            else{InventoryTransfer.Click(ref cursor,ref stack,e.button==1,output,Accepts);items[index]=stack;}
            if(items[index]!=null&&items[index].Empty)items[index]=null;e.Use();
        }
        private void AddDragTarget(ItemStack[] items,int index,string key,string region)
        {
            if(cursor==null||dragTargets.Count>=cursor.Count||dragTargets.Any(t=>t.Key==key))return;
            var target=new SlotTarget{Key=key,Read=()=>items[index],Write=s=>items[index]=s};
            if(region=="offhand"){target.Read=()=>game.Player.Inventory.Offhand;target.Write=s=>game.Player.Inventory.Offhand=s;}
            else if(region=="furnace")
            {
                var active=furnace;
                target.Read=()=>index==0?active.Input:index==1?active.Fuel:active.Output;
                target.Write=s=>{if(index==0)active.Input=s;else if(index==1)active.Fuel=s;else active.Output=s;};
            }
            dragTargets.Add(target);
        }
        private void FinishDrag()
        {
            if(cursor!=null&&dragTargets.Count>0)
            {
                var slots=dragTargets.Select(t=>t.Read()).ToArray();InventoryTransfer.Distribute(ref cursor,slots,dragButton==1);
                for(int i=0;i<slots.Length;i++)dragTargets[i].Write(slots[i]);
            }
            dragTargets.Clear();dragButton=-1;
        }
        private void QuickMove(ItemStack[] items,int index)
        {
            var stack=items[index];if(stack==null)return;
            if(ReferenceEquals(items,game.Player.Inventory.Slots))
            {
                int equipment=Items.ArmorSlot(stack.Id);
                if(chest!=null)stack.Count=AddTo(chest,stack);
                else if(furnace!=null)
                {
                    bool smelt=Furnace.SmeltingResult(stack.Id)>0,fuel=Furnace.FuelSeconds(stack.Id)>0;
                    if(smelt||fuel){var destination=smelt?new[]{furnace.Input}:new[]{furnace.Fuel};int left=AddTo(destination,stack);if(smelt)furnace.Input=destination[0];else furnace.Fuel=destination[0];stack.Count=left;}
                    else game.Player.Inventory.QuickMove(index);
                }
                else if(equipment>=0&&!ValidStack(game.Player.Inventory.Armor[equipment])){game.Player.Inventory.Armor[equipment]=stack;items[index]=null;}
                else game.Player.Inventory.QuickMove(index);
            }
            else stack.Count=game.Player.Inventory.Add(stack.Id,stack.Count,stack.Durability);
            if(items[index]!=null&&items[index].Empty)items[index]=null;
        }
        private void CollectMatching(ItemStack[] activeFurnaceSlots=null)
        {
            void Collect(ItemStack[] source)
            {
                if(source==null)return;
                for(int i=0;i<source.Length;i++)
                {
                    var stack=source[i];if(!Inventory.Stackable(cursor,stack))continue;
                    int amount=Mathf.Min(stack.Count,Items.MaxStack(cursor.Id)-cursor.Count);cursor.Count+=amount;stack.Count-=amount;if(stack.Empty)source[i]=null;
                }
            }
            Collect(game.Player.Inventory.Slots);Collect(grid);Collect(chest);
            if(furnace!=null){var slots=activeFurnaceSlots??new[]{furnace.Input,furnace.Fuel,furnace.Output};Collect(slots);furnace.Input=slots[0];furnace.Fuel=slots[1];furnace.Output=slots[2];}
        }
        private int AddTo(ItemStack[] destination,ItemStack source)
        {
            int left=source.Count,cap=Items.MaxStack(source.Id);
            for(int i=0;i<destination.Length;i++)if(Inventory.Stackable(destination[i],source)){int n=Mathf.Min(left,Mathf.Max(0,cap-destination[i].Count));destination[i].Count+=n;left-=n;}
            for(int i=0;i<destination.Length&&left>0;i++)if(destination[i]==null||destination[i].Empty){int n=Mathf.Min(left,cap);destination[i]=new ItemStack(source.Id,n,source.Durability);left-=n;}
            return left;
        }
        private void SettingsScreen()
        {
            if(game.World==null)Fill(new Rect(0,0,width,height),Ink);else Shade();float x=width/2-440;
            PanelBox(new Rect(x,30,880,660),"Settings");var s=game.Settings;
            if(Button(new Rect(x+24,91,404,38),"Graphics",settingsTab==0)){settingsTab=0;settingsScroll=Vector2.zero;ClearTextFocus();}
            if(Button(new Rect(x+448,91,404,38),"Gameplay",settingsTab==1)){settingsTab=1;settingsScroll=Vector2.zero;ClearTextFocus();}
            Label(new Rect(x+24,140,828,24),settingsTab==0?"Display and quality controls. Scroll for more options.":"Camera, sound and game preferences.",small);
            settingsScroll=GUI.BeginScrollView(new Rect(x+24,173,832,425),settingsScroll,new Rect(0,0,808,settingsTab==0?782:286));
            if(settingsTab==0)
            {
                if(SettingsChoice(0,0,"Quality preset",GraphicsOptions.PresetNames[s.Preset])){GraphicsOptions.SetPreset(s,s.Preset>=3?0:s.Preset+1);game.ApplySettings();}
                SettingsSlider(422,0,"View distance",ref s.ViewDistance,2,8," chunks",true);
                if(SettingsChoice(0,72,"Vertical sync",s.VSync?"On: match display":"Off")){s.VSync=!s.VSync;game.ApplySettings();}
                bool enabled=GUI.enabled;GUI.enabled=!s.VSync;
                if(SettingsChoice(422,72,"Frame limit",s.VSync?"Controlled by VSync":s.Fps==0?"Unlimited":s.Fps+" FPS"))
                {int[] limits={0,30,60,90,120,144,165,240,360};s.Fps=limits[(Array.IndexOf(limits,s.Fps)+1)%limits.Length];game.ApplySettings();}
                GUI.enabled=enabled;
                if(SettingsChoice(0,144,"Anti-aliasing (MSAA)",s.AntiAliasing==0?"Off":s.AntiAliasing+"x"))
                {int[] levels={0,2,4,8};s.AntiAliasing=levels[(Array.IndexOf(levels,s.AntiAliasing)+1)%levels.Length];SettingsChanged(true);}
                SettingsSlider(422,144,"Brightness",ref s.Brightness,.35f,1.8f,false);
                if(SettingsChoice(0,216,"Shadows",new[]{"Off","Hard","Soft"}[s.Shadows])){s.Shadows=(s.Shadows+1)%3;SettingsChanged(true);}
                enabled=GUI.enabled;GUI.enabled=s.Shadows>0;
                if(SettingsChoice(422,216,"Shadow resolution",new[]{"Low","Medium","High","Very high"}[s.ShadowResolution])){s.ShadowResolution=(s.ShadowResolution+1)%4;SettingsChanged(true);}
                SettingsSlider(0,288,"Shadow distance",ref s.ShadowDistance,24,128,true,"0"," blocks");
                GUI.enabled=enabled;
                if(SettingsChoice(422,288,"Texture filtering",s.TextureFiltering==0?"Crisp pixels":"Smooth")){s.TextureFiltering=1-s.TextureFiltering;SettingsChanged(true);}
                if(SettingsChoice(0,360,"Anisotropic filtering",s.Anisotropy==0?"Off":s.Anisotropy+"x"))
                {int[] levels={0,2,4,8};s.Anisotropy=levels[(Array.IndexOf(levels,s.Anisotropy)+1)%levels.Length];SettingsChanged(true);}
                SettingsSlider(422,360,"Block corner shading",ref s.AmbientOcclusion,0,1,true,"0%");
                if(SettingsChoice(0,432,"Clouds",s.Clouds?"On":"Off")){s.Clouds=!s.Clouds;SettingsChanged(true);}
                if(SettingsChoice(422,432,"Distance fog",s.Fog?"On":"Off")){s.Fog=!s.Fog;SettingsChanged(true);}
                if(SettingsChoice(0,504,"Animated water",s.AnimatedWater?"On":"Off")){s.AnimatedWater=!s.AnimatedWater;SettingsChanged(true);}
                if(SettingsChoice(422,504,"Display mode",s.Fullscreen?"Fullscreen":"Windowed")){s.Fullscreen=!s.Fullscreen;game.ApplySettings();}
                SettingsSlider(0,576,"Mob render distance",ref s.EntityDistance,24,120,true,"0"," blocks");
                Label(new Rect(422,578,385,48),"Higher quality uses more GPU power. Texture filtering keeps block art pixelated by default.",small);
                if(SettingsChoice(0,648,"Cinematic lighting",s.Cinematic?"On":"Off")){s.Cinematic=!s.Cinematic;SettingsChanged(true);}
                enabled=GUI.enabled;GUI.enabled=s.Cinematic;
                SettingsSlider(422,648,"Bloom strength",ref s.Bloom,0,2,true);
                SettingsSlider(0,720,"Exposure",ref s.Exposure,-2,2,false,"+0.0;-0.0;0.0"," EV");
                GUI.enabled=enabled;
                Label(new Rect(422,721,385,50),"Cinematic tonemapping adds softer highlights. Bloom gives bright lights a subtle glow.",small);
            }
            else
            {
                SettingsSlider(0,0,"Field of view",ref s.FieldOfView,55,110,false,"0");
                SettingsSlider(422,0,"Mouse sensitivity",ref s.Sensitivity,.2f,6,false);
                if(SettingsChoice(0,72,"View bobbing",s.Bobbing?"On":"Off")){s.Bobbing=!s.Bobbing;game.ApplySettings();}
                SettingsSlider(422,72,"Volume",ref s.Volume,0,1,false,"0%");
                if(game.World!=null&&SettingsChoice(0,144,"Difficulty",new[]{"Peaceful","Easy","Normal","Hard"}[game.Difficulty]))game.SetDifficulty((game.Difficulty+1)%4);
                if(game.World==null)Label(new Rect(0,154,385,55),"Difficulty can be changed while a world is open.",small);
                Label(new Rect(422,155,385,88),"WASD: move   Space: jump\nE: inventory   Esc: pause\nLeft mouse: mine or attack\nRight mouse: use or place",small);
            }
            GUI.EndScrollView();
            Label(new Rect(x+24,606,828,24),settingsTab==0?(s.VSync?"VSync follows your display refresh rate; the stored FPS limit is ignored.":"Unlimited removes the game's FPS cap; actual FPS depends on your hardware."):"Changes are saved automatically. Quality presets do not change these preferences.",small);
            if(Button(new Rect(x+24,638,828,35),"Done",true)){game.ApplySettings();screen=previousScreen=="title"?"title":"game";game.LockCursor();}
        }
        private bool SettingsChoice(float x,float y,string label,string value)
        {
            Label(new Rect(x,y,384,25),label,text);return Button(new Rect(x,y+27,384,32),value);
        }
        private void SettingsChanged(bool quality){if(quality)game.Settings.Preset=4;game.ApplySettings();}
        private void SettingsSlider(float x,float y,string label,ref float value,float min,float max,bool quality,string format="0.0",string suffix="")
        {
            Label(new Rect(x,y,384,25),label+": "+value.ToString(format)+suffix,text);float next=GUI.HorizontalSlider(new Rect(x,y+38,384,23),value,min,max);if(Mathf.Abs(next-value)>.001f){value=next;SettingsChanged(quality);}
        }
        private void SettingsSlider(float x,float y,string label,ref int value,int min,int max,string suffix,bool quality)
        {
            Label(new Rect(x,y,384,25),label+": "+value+suffix,text);int next=Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x,y+38,384,23),value,min,max));if(next!=value){value=next;SettingsChanged(quality);}
        }
        private void DrawStack(Rect rect,ItemStack stack)
        {
            if(!ValidStack(stack))return;GUI.DrawTexture(Inset(rect,3),Icon(stack.Id),ScaleMode.ScaleToFit);
            if(stack.Count>1)
            {
                Rect countRect=new Rect(rect.x+1,rect.y+2,rect.width-4,rect.height-3);var color=number.normal.textColor;number.normal.textColor=new Color(.1f,.1f,.1f);Label(new Rect(countRect.x+2,countRect.y+2,countRect.width,countRect.height),stack.Count.ToString(),number,TextAnchor.LowerRight);number.normal.textColor=Color.white;Label(countRect,stack.Count.ToString(),number,TextAnchor.LowerRight);number.normal.textColor=color;
            }
            int maximum=Items.Durability(stack.Id);if(maximum>0&&stack.Durability<maximum){Fill(new Rect(rect.x+7,rect.yMax-7,rect.width-14,4),Color.black);Fill(new Rect(rect.x+7,rect.yMax-7,(rect.width-14)*Mathf.Clamp01(stack.Durability/(float)maximum),4),Color.Lerp(new Color(.9f,.2f,.15f),Accent,stack.Durability/(float)maximum));}
        }
        private Texture2D Icon(int id)
        {
            if(icons==null)icons=new ItemIconAtlas();return icons.Get(id);
        }
        private void OnDestroy(){icons?.Dispose();if(heart!=null)Destroy(heart);}
        private static bool ValidStack(ItemStack stack)=>stack!=null&&!stack.Empty&&Items.Exists(stack.Id);
        private Rect PanelRect(float x,float y,float w,float h)=>new Rect(InventoryPanelRect.x+x*InventoryUnit,InventoryPanelRect.y+y*InventoryUnit,w*InventoryUnit,h*InventoryUnit);
        private static Rect Inset(Rect rect,float amount)=>new Rect(rect.x+amount,rect.y+amount,rect.width-2*amount,rect.height-2*amount);
        private void InventoryLabel(float x,float y,string value)=>Label(PanelRect(x,y,160-x,10),value,inventoryText);
        private void PixelPanel(Rect rect)
        {
            Fill(new Rect(rect.x+6,rect.y+6,rect.width,rect.height),new Color(0,0,0,.35f));
            Fill(rect,new Color(.12f,.12f,.12f));Fill(Inset(rect,3),new Color(.34f,.34f,.34f));Fill(new Rect(rect.x+3,rect.y+3,rect.width-9,rect.height-9),new Color(.94f,.94f,.94f));Fill(Inset(rect,9),new Color(.77f,.77f,.77f));
        }
        private static void SlotBackground(Rect rect,bool selected=false)
        {
            Fill(rect,selected?Color.white:new Color(.94f,.94f,.94f));
            Fill(new Rect(rect.x,rect.y,rect.width-3,rect.height-3),new Color(.23f,.23f,.23f));
            Fill(Inset(rect,3),new Color(.55f,.55f,.55f));
            if(selected){Fill(new Rect(rect.x-3,rect.y-3,rect.width+6,3),Color.white);Fill(new Rect(rect.x-3,rect.yMax,rect.width+6,3),Color.white);}
        }
        private bool PixelButton(Rect rect,string label,bool active=false)
        {
            bool hovered=rect.Contains(Event.current.mousePosition);Fill(rect,new Color(.18f,.18f,.18f));Fill(Inset(rect,3),new Color(.95f,.95f,.95f));Fill(new Rect(rect.x+6,rect.y+6,rect.width-9,rect.height-9),new Color(.38f,.38f,.38f));Fill(Inset(rect,6),active?new Color(.62f,.74f,.56f):hovered?new Color(.82f,.85f,.89f):new Color(.70f,.70f,.70f));
            if(label.Length>0)Label(Inset(rect,3),label,inventorySmall,TextAnchor.MiddleCenter);
            if(hovered&&Event.current.type==EventType.MouseDown&&Event.current.button==0){Event.current.Use();return true;}return false;
        }
        private static void DrawArrow(Rect rect,Color color)
        {
            Fill(new Rect(rect.x,rect.y+rect.height*.375f,rect.width*.65f,rect.height*.25f),color);
            for(int i=0;i<5;i++){float h=rect.height*(1-i*.2f);Fill(new Rect(rect.x+rect.width*(.55f+i*.09f),rect.center.y-h/2,rect.width*.1f,h),color);}
        }
        private static void DrawBook(Rect rect)
        {
            Fill(rect,new Color(.19f,.29f,.16f));Fill(Inset(rect,3),new Color(.40f,.55f,.25f));Fill(new Rect(rect.x+6,rect.y+5,rect.width-11,rect.height-10),new Color(.91f,.89f,.68f));Fill(new Rect(rect.center.x-1.5f,rect.y+5,3,rect.height-10),new Color(.56f,.46f,.29f));
        }
        private void DrawAvatar(Rect rect)
        {
            Fill(rect,new Color(.13f,.13f,.13f));Fill(Inset(rect,3),new Color(.055f,.065f,.06f));
            float unit=rect.width/50;Rect Part(float x,float y,float w,float h)=>new Rect(rect.x+x*unit,rect.y+y*unit,w*unit,h*unit);
            Color skin=new Color(.69f,.45f,.30f),shirt=new Color(.20f,.49f,.43f),pants=new Color(.25f,.29f,.39f),hair=new Color(.22f,.15f,.11f),metal=new Color(.71f,.74f,.75f);
            void Box(float x,float y,float w,float h,Color c){Fill(Part(x,y,w,h),c*.77f);Fill(Part(x,y,w-2,h-1),c);Fill(Part(x,y,w-2,1),Color.Lerp(c,Color.white,.18f));}
            var armor=game.Player.Inventory.Armor;bool chestplate=ValidStack(armor[1]),leggings=ValidStack(armor[2]),boots=ValidStack(armor[3]);
            Fill(Part(9,64,32,2),new Color(0,0,0,.5f));
            Box(17,43,8,20,leggings?metal:pants);Box(26,43,8,20,leggings?metal:pants);
            Box(17,59,8,5,boots?metal:new Color(.19f,.17f,.15f));Box(26,59,8,5,boots?metal:new Color(.19f,.17f,.15f));
            Box(16,24,19,21,chestplate?metal:shirt);Box(9,25,7,24,skin);Box(35,25,7,24,skin);
            Box(9,25,7,chestplate?19:10,chestplate?metal:shirt);Box(35,25,7,chestplate?19:10,chestplate?metal:shirt);
            float look=Mathf.Clamp((Event.current.mousePosition.x-rect.center.x)/80,-2,2);look=Mathf.Round(look);
            Box(18+look,9,16,16,skin);Fill(Part(18+look,8,16,5),hair);Fill(Part(18+look,12,3,6),hair);Fill(Part(31+look,12,3,6),hair);
            Fill(Part(22+look,16,3,2),new Color(.91f,.9f,.83f));Fill(Part(28+look,16,3,2),new Color(.91f,.9f,.83f));Fill(Part(23+look,16,1,2),new Color(.20f,.30f,.25f));Fill(Part(29+look,16,1,2),new Color(.20f,.30f,.25f));Fill(Part(25+look,21,4,1),new Color(.37f,.22f,.17f));
            if(ValidStack(armor[0])){Color helmet=armor[0].Id==Items.GoldHelmet?new Color(.88f,.66f,.22f):metal;Box(17+look,7,18,6,helmet);Fill(Part(17+look,12,3,8),helmet);Fill(Part(32+look,12,3,8),helmet*.77f);}
            if(ValidStack(game.Player.Inventory.Held))GUI.DrawTexture(Part(33,39,14,18),Icon(game.Player.Inventory.Held.Id),ScaleMode.ScaleToFit);
            if(ValidStack(game.Player.Inventory.Offhand))GUI.DrawTexture(Part(3,38,15,19),Icon(game.Player.Inventory.Offhand.Id),ScaleMode.ScaleToFit);
        }
        private void Shade()=>Fill(new Rect(0,0,width,height),new Color(.025f,.035f,.045f,.73f));
        private void PanelBox(Rect rect,string label){Fill(rect,Panel);Fill(new Rect(rect.x,rect.y,rect.width,3),Accent);Label(new Rect(rect.x+22,rect.y+18,rect.width-44,35),label,heading);}
        private bool Button(Rect rect,string label,bool active=false){Color old=GUI.backgroundColor;GUI.backgroundColor=active?Accent:new Color(.3f,.39f,.38f);bool clicked=GUI.Button(rect,label,button);GUI.backgroundColor=old;return clicked;}
        private static void Fill(Rect rect,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        private void BackedLabel(Rect rect,string value,GUIStyle style,TextAnchor alignment=TextAnchor.UpperLeft)
        {
            float span=Mathf.Min(rect.width,style.CalcSize(new GUIContent(value)).x+14);
            float left=alignment==TextAnchor.MiddleCenter?rect.center.x-span/2:rect.x-6;
            Fill(new Rect(left,rect.y-2,span,rect.height),new Color(.025f,.035f,.045f,.72f));
            Label(rect,value,style,alignment);
        }
        private void Label(Rect rect,string value,GUIStyle style,TextAnchor alignment=TextAnchor.UpperLeft){var before=style.alignment;style.alignment=alignment;GUI.Label(rect,value,style);style.alignment=before;}
    }
}
