using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class PlayerController : MonoBehaviour
    {
        public Camera Eye { get; private set; }
        public FirstPersonView View { get; private set; }
        public Inventory Inventory = new Inventory();
        public bool IsCreative, Flying;
        public float Health = 20, Hunger = 20, Air = 10, Saturation = 5;
        public bool Dead => Health <= 0;
        public float MiningProgress { get; private set; }
        public bool HasTarget { get; private set; }
        public Cell Target { get; private set; }
        public Cell Adjacent { get; private set; }
        public float Pitch;
        public bool InWater { get; private set; }
        public bool Grounded { get; private set; }
        public bool WalkingOnGround { get; private set; }
        public bool IsSprinting { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public bool Blocking => game.Playing && !game.Hud.IsOpen && Input.GetMouseButton(1) && (Inventory.Held?.Id == Items.Shield || Inventory.Offhand?.Id == Items.Shield);
        private GameSession game;
        private CharacterController controller;
        private Vector3 velocity, planarVelocity;
        private float hitCooldown, attackCooldown, useCooldown, regeneration, foodTimer, hazardTimer, fallStart, previousSpace, bowCharge;
        private Cell mining;
        private int lastUseItem;
        private float gait, cameraWalkWeight, sprintViewWeight;
        private LineRenderer selection;
        public void Init(GameSession session)
        {
            game=session;
            controller=gameObject.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.3f;controller.center=new Vector3(0,.9f,0);controller.stepOffset=.05f;controller.skinWidth=.025f;controller.slopeLimit=45;
            var cameraObject=new GameObject("Eyes");cameraObject.transform.SetParent(transform,false);cameraObject.transform.localPosition=new Vector3(0,1.62f,0);
            Eye=cameraObject.AddComponent<Camera>();Eye.clearFlags=CameraClearFlags.SolidColor;Eye.nearClipPlane=.055f;Eye.farClipPlane=200;Eye.tag="MainCamera";cameraObject.AddComponent<AudioListener>();
            View=cameraObject.AddComponent<FirstPersonView>();View.Init(this,game.Renderer);
            var outline=new GameObject("Target outline");selection=outline.AddComponent<LineRenderer>();selection.material=new Material(Shader.Find("Sprites/Default"));selection.startColor=selection.endColor=new Color(.08f,.10f,.1f,.8f);selection.startWidth=selection.endWidth=.012f;selection.positionCount=16;selection.useWorldSpace=true;
        }
        public void Teleport(Vector3 position)
        {
            foodTimer=bowCharge=0;
            controller.enabled=false;transform.position=position;controller.enabled=true;velocity=planarVelocity=Vector3.zero;fallStart=position.y;MiningProgress=0;gait=cameraWalkWeight=sprintViewWeight=0;HorizontalSpeed=0;Grounded=WalkingOnGround=IsSprinting=false;View.ResetMotion();Eye.transform.localPosition=new Vector3(0,1.62f,0);Eye.fieldOfView=game.Settings.FieldOfView;Physics.SyncTransforms();
        }
        public void ResetVitals(){Health=Hunger=20;Air=10;Saturation=5;hitCooldown=1;foodTimer=hazardTimer=regeneration=0;Flying=false;}
        private void Update()
        {
            if(game==null || game.World==null)return;
            float dt=Mathf.Min(Time.deltaTime,.1f);hitCooldown-=dt;attackCooldown-=dt;useCooldown-=dt;
            if(!game.Playing){selection.enabled=false;foodTimer=bowCharge=0;return;}
            var feet=ToCell(transform.position+Vector3.up*.1f);Block fluid=game.World.GetBlock(feet);
            InWater=fluid==Block.Water;bool swimming=InWater||fluid==Block.Lava;
            bool control=!game.Hud.IsOpen;
            Vector2 lookDelta=Vector2.zero;
            if(control)
            {
                float mx=Input.GetAxisRaw("Mouse X")*game.Settings.Sensitivity,my=Input.GetAxisRaw("Mouse Y")*game.Settings.Sensitivity;
                lookDelta=new Vector2(mx,my);
                transform.Rotate(0,mx,0);Pitch=Mathf.Clamp(Pitch-my,-89,89);Eye.transform.localRotation=Quaternion.Euler(Pitch,0,0);
                for(int i=0;i<9;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))Inventory.Selected=i;
                float scroll=Input.mouseScrollDelta.y;if(scroll!=0)Inventory.Selected=(Inventory.Selected+(scroll>0?8:1))%9;
                if(Input.GetKeyDown(KeyCode.F)){var held=Inventory.Held;Inventory.Slots[Inventory.Selected]=Inventory.Offhand;Inventory.Offhand=held;}
                if(Input.GetKeyDown(KeyCode.Q))DropHeld(Input.GetKey(KeyCode.LeftControl));
                if(IsCreative && Input.GetKeyDown(KeyCode.G))Flying=!Flying;
                if(Input.GetKeyDown(KeyCode.Space)) { if(IsCreative && Time.time-previousSpace<.27f)Flying=!Flying;previousSpace=Time.time; }
            }
            Vector2 input=Vector2.zero;
            if(control)input=new Vector2((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0));
            int heldItem=Inventory.Held?.Id??0;
            bool usingItem=control&&Input.GetMouseButton(1)&&(Blocking||heldItem==Items.Bow||(Items.IsFood(heldItem)&&Hunger<20));
            float walkedDistance=StepMovement(dt,input,Input.GetKey(KeyCode.LeftControl),Input.GetKey(KeyCode.LeftShift),Input.GetKeyDown(KeyCode.Space),Input.GetKey(KeyCode.Space),control,swimming,usingItem);
            Vitals(dt,HorizontalSpeed,fluid);
            if(transform.position.y < -20)Damage(100,transform.position);
            if(!control){MiningProgress=0;HasTarget=false;selection.enabled=false;bowCharge=foodTimer=0;AnimateHand(dt,0,false,Vector2.zero,false);return;}
            HasTarget=Trace(Eye.transform.position,Eye.transform.forward,IsCreative?6:4.5f,out var hit,out var previous,Inventory.Held?.Id==Items.EmptyBucket);
            Target=hit;Adjacent=previous;DrawSelection();
            if(Input.GetMouseButton(0))MineOrAttack(dt);else MiningProgress=0;
            if(Input.GetMouseButtonDown(2)&&IsCreative&&HasTarget){int id=(int)game.World.GetBlock(Target);Inventory.Slots[Inventory.Selected]=new ItemStack(id,Items.MaxStack(id));}
            int item=Inventory.Held?.Id??0;
            if(item!=lastUseItem||!Input.GetMouseButton(1))foodTimer=0;
            if(item!=Items.Bow)bowCharge=0;
            lastUseItem=item;
            bool usedBlock=Input.GetMouseButtonDown(1)&&useCooldown<=0&&HasTarget
                &&ItemUseRules.BlockTakesPriority(game.World.GetBlock(Target),Input.GetKey(KeyCode.LeftShift));
            if(usedBlock){usedBlock=game.Use(true,Target,Adjacent);if(usedBlock){useCooldown=.2f;View.TriggerSwing(FirstPersonView.Action.Use);foodTimer=bowCharge=0;}}
            if(!usedBlock&&item==Items.Bow)
            {
                if(Input.GetMouseButton(1))bowCharge=Mathf.Min(1,bowCharge+dt);
                if(Input.GetMouseButtonUp(1))
                {
                    if(bowCharge>.15f&&(IsCreative||Inventory.Remove(Items.Arrow,1))){game.Mobs.ShootArrow(Eye.transform.position+Eye.transform.forward*.5f,Eye.transform.forward,bowCharge);if(!IsCreative)Inventory.WearSelected(1);View.TriggerSwing(FirstPersonView.Action.Attack);}
                    bowCharge=0;
                }
            }
            else if(!usedBlock&&Input.GetMouseButton(1)&&Items.IsFood(item))
            {
                if(Hunger<20){foodTimer+=dt;if(foodTimer>=1.6f){Hunger=Mathf.Min(20,Hunger+Items.Food(item));Saturation=Mathf.Min(20,Saturation+Items.Saturation(item));ConsumeHeld();foodTimer=0;}}else foodTimer=0;
            }
            else if(!usedBlock&&Input.GetMouseButtonDown(1)&&useCooldown<=0){if(game.Use(HasTarget,Target,Adjacent)){useCooldown=.2f;View.TriggerSwing(FirstPersonView.Action.Use);}}
            else foodTimer=0;
            AnimateHand(dt,walkedDistance,WalkingOnGround,lookDelta,Input.GetMouseButton(1)&&Items.IsFood(item)&&Hunger<20);
        }
        public float StepMovement(float dt,Vector2 input,bool sprintHeld,bool sneak,bool jumpPressed,bool riseHeld,bool hasControl,bool swimming,bool usingItem=false)
        {
            dt=Mathf.Clamp(dt,0,.1f);
            if(dt<=0)return 0;
            if(!hasControl){input=Vector2.zero;sprintHeld=sneak=jumpPressed=riseHeld=false;}
            PlayerMotion.NormalizeInput(ref input.x,ref input.y);
            bool grounded=controller.isGrounded&&velocity.y<=0;
            bool wantsSprint=PlayerMotion.CanSprint(input.y,sprintHeld,sneak,IsCreative,Hunger,swimming,Flying,usingItem);
            float speed=Flying?10:swimming?2.5f:sneak?PlayerMotion.SneakSpeed:wantsSprint?PlayerMotion.SprintSpeed:PlayerMotion.WalkSpeed;
            Vector3 target=transform.TransformDirection(new Vector3(input.x,0,input.y))*speed;
            float response=Flying||swimming?8:grounded?(input.sqrMagnitude>.001f?18:22):input.sqrMagnitude>.001f?3:.35f;
            if(!hasControl)response=22;
            float dx=PlayerMotion.IntegrateVelocity(planarVelocity.x,target.x,response,dt,out planarVelocity.x);
            float dz=PlayerMotion.IntegrateVelocity(planarVelocity.z,target.z,response,dt,out planarVelocity.z);
            float dy;
            bool jumped=false;
            if(Flying){velocity.y=((riseHeld?1:0)-(sneak?1:0))*speed;dy=velocity.y*dt;fallStart=transform.position.y;}
            else if(swimming){velocity.y=Mathf.MoveTowards(velocity.y,riseHeld?3:-1.8f,12*dt);dy=velocity.y*dt;fallStart=transform.position.y;}
            else if(grounded)
            {
                fallStart=transform.position.y;
                if(jumpPressed)
                {
                    velocity.y=PlayerMotion.JumpSpeed;
                    dy=PlayerMotion.IntegrateGravity(velocity.y,dt,out velocity.y);
                    jumped=true;
                }
                else {velocity.y=-2;dy=velocity.y*dt;}
            }
            else
            {
                fallStart=Mathf.Max(fallStart,transform.position.y);
                dy=PlayerMotion.IntegrateGravity(velocity.y,dt,out velocity.y);
            }
            if(sneak&&grounded&&!Flying&&!swimming)
            {
                Vector3 projected=transform.position+new Vector3(dx,0,dz);
                if(!game.World.Solid(ToCell(projected+new Vector3(0,-.2f,0)))){dx=dz=0;planarVelocity=Vector3.zero;}
            }
            Vector3 beforeMove=transform.position;
            float impactSpeed=velocity.y;
            Vector3 knockback=new Vector3(velocity.x,0,velocity.z)*dt;
            CollisionFlags collisions=controller.Move(new Vector3(dx,dy,dz)+knockback);
            Vector3 travelled=transform.position-beforeMove;travelled.y=0;
            float walkedDistance=travelled.magnitude;
            HorizontalSpeed=walkedDistance/dt;
            Grounded=(collisions&CollisionFlags.Below)!=0&&!Flying&&!swimming&&velocity.y<=0;
            if((collisions&CollisionFlags.Above)!=0&&velocity.y>0)velocity.y=0;
            if((collisions&CollisionFlags.Sides)!=0)
            {
                if(Mathf.Abs(travelled.x)<Mathf.Abs(dx)*.6f)planarVelocity.x=0;
                if(Mathf.Abs(travelled.z)<Mathf.Abs(dz)*.6f)planarVelocity.z=0;
            }
            if(Grounded)
            {
                if(!grounded&&impactSpeed<-12&&fallStart-transform.position.y>3)Damage(Mathf.Floor(fallStart-transform.position.y-3),transform.position);
                velocity.y=-2;fallStart=transform.position.y;
            }
            velocity.x=Mathf.MoveTowards(velocity.x,0,12*dt);velocity.z=Mathf.MoveTowards(velocity.z,0,12*dt);
            WalkingOnGround=Grounded&&!jumped&&input.sqrMagnitude>.001f&&HorizontalSpeed>.08f;
            IsSprinting=wantsSprint&&HorizontalSpeed>PlayerMotion.WalkSpeed*.7f;
            sprintViewWeight=Mathf.Lerp(sprintViewWeight,IsSprinting?1:0,1-Mathf.Exp(-8*dt));
            Eye.fieldOfView=game.Settings.FieldOfView*(1+.075f*sprintViewWeight);
            return walkedDistance;
        }
        private void Vitals(float dt,float moving,Block fluid)
        {
            if(IsCreative){Health=Hunger=20;Air=10;return;}
            if(game.Difficulty==0){Health=Mathf.Min(20,Health+dt*.5f);Hunger=Mathf.Min(20,Hunger+dt);}
            if(moving>1){Saturation-=dt*.025f;if(Saturation<0){Hunger=Mathf.Max(0,Hunger+Saturation);Saturation=0;}}
            Block head=game.World.GetBlock(ToCell(Eye.transform.position));
            Air=head==Block.Water?Mathf.Max(0,Air-dt):Mathf.Min(10,Air+dt*4);
            hazardTimer+=dt;
            if(hazardTimer>=1)
            {
                hazardTimer-=1;
                if(fluid==Block.Lava||head==Block.Lava)Damage(4,transform.position);
                else if(Air<=0)Damage(2,transform.position);
                else if(Hunger<=0&&(game.Difficulty>=3||Health>1))Damage(1,transform.position);
            }
            if(Hunger>=18&&Health<20){regeneration+=dt;if(regeneration>=4){Health=Mathf.Min(20,Health+1);Hunger=Mathf.Max(0,Hunger-.5f);regeneration=0;}}else regeneration=0;
        }
        public void Damage(float amount,Vector3 source)
        {
            if(IsCreative||Dead||hitCooldown>0||amount<=0)return;
            if(Blocking)
            {
                Vector3 toward=source-transform.position;
                if(toward.sqrMagnitude>.01f&&Vector3.Dot(transform.forward,toward.normalized)>.1f)
                {
                    var shield=Inventory.Held?.Id==Items.Shield?Inventory.Held:Inventory.Offhand;
                    shield.Durability-=Mathf.Max(1,Mathf.CeilToInt(amount));
                    if(shield.Durability<=0){if(ReferenceEquals(shield,Inventory.Offhand))Inventory.Offhand=null;else Inventory.Slots[Inventory.Selected]=null;}
                    hitCooldown=.35f;return;
                }
            }
            int armor=0;
            for(int i=0;i<Inventory.Armor.Length;i++)if(Inventory.Armor[i]!=null){armor+=Items.Protection(Inventory.Armor[i].Id);Inventory.Armor[i].Durability--;if(Inventory.Armor[i].Durability<=0)Inventory.Armor[i]=null;}
            Health=Mathf.Max(0,Health-amount*(1-Mathf.Min(20,armor)*.04f));hitCooldown=.5f;
            Vector3 knock=transform.position-source;knock.y=0;if(knock.sqrMagnitude>.01f)velocity+=knock.normalized*4+Vector3.up*2;
            if(Dead)game.Die();
        }
        public void ConsumeHeld(int amount=1)
        {
            if(IsCreative)return;var stack=Inventory.Held;if(stack==null)return;stack.Count-=amount;if(stack.Empty)Inventory.Slots[Inventory.Selected]=null;
        }
        public void DropHeld(bool all)
        {
            var stack=Inventory.Held;if(stack==null)return;int count=all?stack.Count:1;
            game.DropStack(transform.position+transform.forward*.8f+Vector3.up,new ItemStack(stack.Id,count,stack.Durability));stack.Count-=count;if(stack.Empty)Inventory.Slots[Inventory.Selected]=null;
        }
        private void MineOrAttack(float dt)
        {
            if(attackCooldown<=0 && game.Mobs.Attack(new Ray(Eye.transform.position,Eye.transform.forward),IsCreative?6:3.3f,Items.AttackDamage(Inventory.Held?.Id??0)))
            {attackCooldown=.55f;View.TriggerSwing(FirstPersonView.Action.Attack);MiningProgress=0;if(!IsCreative)Inventory.WearSelected(ItemUseRules.AttackWear(Inventory.Held?.Id??0));return;}
            if(!HasTarget){MiningProgress=0;View.TriggerSwing(FirstPersonView.Action.Attack);return;}
            if(Target!=mining){mining=Target;MiningProgress=0;}
            Block id=game.World.GetBlock(Target);float hardness=Blocks.Hardness(id);
            View.TriggerSwing(FirstPersonView.Action.Mine);
            if(float.IsInfinity(hardness))return;
            float duration=IsCreative?.12f:Mathf.Max(.1f,hardness*(Items.CanHarvest(Inventory.Held?.Id??0,id)?1.5f:5)/Items.MiningSpeed(Inventory.Held?.Id??0,id));
            MiningProgress+=dt/duration;
            if(MiningProgress>=1){int tool=Inventory.Held?.Id??0;game.BreakBlock(Target);MiningProgress=0;if(!IsCreative)Inventory.WearSelected(ItemUseRules.MiningWear(tool));}
        }
        public bool Trace(Vector3 origin,Vector3 direction,float range,out Cell hit,out Cell previous,bool fluids=false)
        {
            previous=ToCell(origin);hit=previous;
            for(float distance=0;distance<=range;distance+=.025f)
            {
                Vector3 point=origin+direction*distance;Cell cell=ToCell(point);Voxel voxel=game.World.Get(cell);Block id=voxel.Id;
                if(id==Block.Door&&!DoorRules.Contains(voxel,point.x-cell.X,point.y-cell.Y,point.z-cell.Z)){previous=cell;continue;}
                if(Blocks.IsBed(id)&&point.y-cell.Y>BedRules.Height){previous=cell;continue;}
                if(id!=Block.Air && (fluids||!Blocks.IsFluid(id)) && id!=Block.PortalX&&id!=Block.PortalZ&&id!=Block.EndPortal){hit=cell;return true;}
                previous=cell;
            }
            return false;
        }
        private void AnimateHand(float dt,float walkedDistance,bool moving,Vector2 lookDelta,bool eating)
        {
            moving&=game.Settings.Bobbing;
            float smooth=1-Mathf.Exp(-12*dt);
            float walkAmount=moving&&dt>0?Mathf.Clamp01(walkedDistance/dt/PlayerMotion.WalkSpeed):0;
            cameraWalkWeight=Mathf.Lerp(cameraWalkWeight,walkAmount,smooth);
            gait=PlayerMotion.AdvanceGait(gait,walkedDistance,moving);
            float bobX=Mathf.Sin(gait)*.010f*cameraWalkWeight;
            float bobY=(Mathf.Abs(Mathf.Cos(gait))-.5f)*.014f*cameraWalkWeight;
            Eye.transform.localPosition=Vector3.Lerp(Eye.transform.localPosition,new Vector3(bobX,1.62f+bobY,0),smooth);
            View.Advance(dt,walkedDistance,moving,lookDelta,eating,bowCharge,Blocking);
        }
        private void DrawSelection()
        {
            selection.enabled=HasTarget;if(!HasTarget)return;
            Bounds bounds=BlockShape.Bounds(Target,game.World.Get(Target));Vector3 p=bounds.min-Vector3.one*.003f,size=bounds.size+Vector3.one*.006f;
            Vector3[] points={new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,0,1),new Vector3(0,0,1),new Vector3(0,0,0),new Vector3(0,1,0),new Vector3(1,1,0),new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(1,1,1),new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,1),new Vector3(0,0,1),new Vector3(0,1,1),new Vector3(0,1,0)};
            for(int i=0;i<points.Length;i++)selection.SetPosition(i,p+Vector3.Scale(points[i],size));
        }
        public static Cell ToCell(Vector3 p)=>new Cell(Mathf.FloorToInt(p.x),Mathf.FloorToInt(p.y),Mathf.FloorToInt(p.z));
        private void OnDestroy(){if(selection)Destroy(selection.gameObject);}
    }
}
