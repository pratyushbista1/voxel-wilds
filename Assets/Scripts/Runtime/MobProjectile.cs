using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class MobProjectile : MonoBehaviour
    {
        MobDirector director;
        MobActor owner;
        Vector3 velocity;
        float damage, life = 10;
        bool player, fire, breath, cloud;
        float pulse;
        Transform shapeTransform;
        Material material;
        public void Init(MobDirector mobs, Vector3 speed, float amount, bool fromPlayer, bool fireball, MobActor shooter)
        {
            director = mobs; velocity = speed; damage = amount; player = fromPlayer; fire = fireball; owner = shooter;
            breath = shooter != null && shooter.Kind == MobKind.EndDragon;
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shapeTransform = shape.transform;
            shape.transform.SetParent(transform, false);
            shape.transform.localScale = fire ? Vector3.one * .32f : new Vector3(.055f, .055f, .65f);
            Destroy(shape.GetComponent<Collider>());
            var shader = Shader.Find("Standard");
            if (shader != null)
            {
                material = new Material(shader);
                material.color = fire ? owner != null && owner.Kind == MobKind.EndDragon ? new Color(.65f, .15f, .9f) : new Color(1, .4f, .05f) : new Color(.58f, .42f, .24f);
                if (fire) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", material.color * 2); }
                shape.GetComponent<Renderer>().sharedMaterial = material;
            }
            if (velocity.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(velocity);
        }
        void OnDestroy() { if (material != null) Destroy(material); }
        public bool Tick(float dt)
        {
            life -= dt;
            if (life <= 0) return false;
            if (cloud)
            {
                pulse -= dt;
                shapeTransform.localScale = new Vector3(5 + Mathf.Sin(life * 3) * .25f, .12f, 5 + Mathf.Cos(life * 3) * .25f);
                if (material != null) { Color color = material.color; color.a = Mathf.Min(.5f, life * .2f); material.color = color; }
                if (pulse <= 0)
                {
                    pulse = 1;
                    var session = director.Game;
                    if (!session.Player.Dead && !session.Player.IsCreative &&
                        Vector3.Distance(session.Player.transform.position, transform.position) < 3 &&
                        director.LineClear(transform.position + Vector3.up * .5f, session.Player.transform.position + Vector3.up))
                        session.Player.Damage(MobRules.Damage(3, session.Difficulty), transform.position);
                    foreach (var actor in director.Actors)
                        if (actor != owner && !actor.Dead && actor.Kind != MobKind.EndCrystal && actor.Kind != MobKind.EndDragon &&
                            Vector3.Distance(actor.transform.position, transform.position) < 3)
                            actor.Hurt(3, transform.position, false);
                }
                return true;
            }
            if (!fire) velocity += Vector3.down * 12 * dt;
            Vector3 old = transform.position;
            Vector3 next = old + velocity * dt;
            Vector3 direction = next - old;
            float distance = direction.magnitude;
            var ray = new Ray(old, direction.normalized);
            bool hitMob = director.TraceMob(ray, distance, owner, out var mob, out float mobDistance);
            float playerDistance = float.PositiveInfinity;
            var game = director.Game;
            bool hitPlayer = !player && !game.Player.Dead && !game.Player.IsCreative &&
                new Bounds(game.Player.transform.position + Vector3.up * .9f, new Vector3(.65f, 1.8f, .65f)).IntersectRay(ray, out playerDistance) && playerDistance <= distance;
            float solidDistance = distance + 1;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance * 8));
            for (int i = 1; i <= steps; i++)
            {
                float fraction = i / (float)steps;
                var block = game.World.GetBlock(MobDirector.CellAt(Vector3.Lerp(old, next, fraction)));
                if (Blocks.IsSolid(block)) { solidDistance = distance * fraction; break; }
                if (block == Block.Water) velocity *= Mathf.Pow(.6f, dt / steps);
            }
            if (hitMob && mobDistance <= solidDistance && (!hitPlayer || mobDistance <= playerDistance))
            {
                mob.Hurt(damage, old, true);
                return LeaveCloud(ray.GetPoint(mobDistance));
            }
            if (hitPlayer && playerDistance <= solidDistance)
            {
                game.Player.Damage(MobRules.Damage(damage, game.Difficulty), old);
                return LeaveCloud(ray.GetPoint(playerDistance));
            }
            if (solidDistance <= distance)
            {
                Vector3 impact = ray.GetPoint(Mathf.Max(0, solidDistance - .2f));
                if (player && !fire && !game.Player.IsCreative) game.DropLoot(impact, Items.Arrow, 1);
                return LeaveCloud(impact);
            }
            transform.position = next;
            if (velocity.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(velocity);
            return true;
        }
        bool LeaveCloud(Vector3 impact)
        {
            if (!breath) return false;
            cloud = true; life = 8; velocity = Vector3.zero; pulse = .5f;
            transform.position = impact; transform.rotation = Quaternion.identity;
            if (material != null)
            {
                material.SetFloat("_Mode", 3);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = 3000;
            }
            return true;
        }
    }
}
