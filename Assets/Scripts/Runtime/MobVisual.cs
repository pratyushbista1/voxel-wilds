using System.Collections.Generic;
using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class MobVisual : MonoBehaviour
    {
        enum JointKind { Leg, Arm, WingTip, Wing, Tail, Head, Jaw, Crystal, Rod }
        sealed class Joint
        {
            public Transform Transform;
            public Quaternion Rest;
            public JointKind Kind;
            public int Side;
            public int Index;
            public int GaitSign;
            public Vector3 Right, Up, Forward;
        }
        readonly List<Joint> joints = new List<Joint>();
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Color> colors = new List<Color>();
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        Transform model;
        Vector3 restPosition;
        MobKind kind;
        float phase, stride, idlePhase;
        float moveSpeed;
        int appliedTint = -1;
        bool visible = true;

        public void Init(MobKind mobKind)
        {
            kind = mobKind;
            moveSpeed = Mathf.Max(.1f, MobRules.Definition(kind).Speed);
            var prefab = Resources.Load<GameObject>("Models/" + MobRules.ModelName(kind));
            if (prefab != null)
            {
                model = Instantiate(prefab, transform, false).transform;
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                foreach (var collider in model.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
                Bounds bounds = new Bounds();
                bool found = false;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (found && bounds.size.y > .001f)
                {
                    float scale = MobRules.Definition(kind).Height / bounds.size.y;
                    model.localScale *= scale;
                    model.localPosition = new Vector3(0, (transform.position.y - bounds.min.y) * scale, 0);
                }
            }
            else
            {
                model = new GameObject("Model").transform;
                model.SetParent(transform, false);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(model, false);
                cube.transform.localPosition = Vector3.up * MobRules.Definition(kind).Height * .5f;
                cube.transform.localScale = new Vector3(.5f, MobRules.Definition(kind).Height, .4f);
                Destroy(cube.GetComponent<Collider>());
            }
            restPosition = model.localPosition;
            foreach (var part in model.GetComponentsInChildren<Transform>())
            {
                string name = part.name.ToLowerInvariant();
                if (name.StartsWith("leg_") && !name.Contains("mesh") || name.StartsWith("arm_") && !name.Contains("mesh") ||
                    name.StartsWith("wing_") && !name.Contains("membrane") && !name.Contains("upper") && !name.Contains("outer") ||
                    name.StartsWith("wingtip_") || name.StartsWith("tail_") && !name.Contains("mesh") && !name.Contains("spine") ||
                    name == "head_joint" || name == "jaw" || name == "crystal_spin" || name.StartsWith("rod_"))
                {
                    string[] pieces = name.Split('_');
                    int index = 0;
                    if (pieces.Length > 1) int.TryParse(pieces[1], out index);
                    joints.Add(new Joint {
                        Transform = part, Rest = part.localRotation, Kind = ClassifyJoint(name), Side = index < 0 ? -1 : 1, Index = index,
                        GaitSign = MobRules.GaitSign(kind, name),
                        Right = part.parent.InverseTransformDirection(transform.right).normalized,
                        Up = part.parent.InverseTransformDirection(transform.up).normalized,
                        Forward = part.parent.InverseTransformDirection(transform.forward).normalized
                    });
                }
            }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderers.Add(renderer);
                Color color = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color") ? renderer.sharedMaterial.color : Color.white;
                if (kind == MobKind.WitherSkeleton) color *= .25f;
                if (kind == MobKind.Brute) color = Color.Lerp(color, new Color(.22f, .16f, .12f), .3f);
                colors.Add(color);
            }
            phase = Random.value * Mathf.PI * 2;
            idlePhase = phase;
        }

        public void Animate(float dt, float speed, float attack, float hurt, float fuse, bool angry)
        {
            if (model == null) return;
            stride = Mathf.Lerp(stride, Mathf.Clamp01(speed / moveSpeed), 1 - Mathf.Exp(-dt * 8));
            idlePhase += dt * (kind == MobKind.EndDragon ? 4 : 1.8f);
            phase += Mathf.Max(0, speed) * dt * (kind == MobKind.Chicken ? 7 : 4.2f);
            float attackArc = Mathf.Sin(Mathf.Clamp01(attack) * Mathf.PI);
            float jointBlend = 1 - Mathf.Exp(-dt * 18);
            model.localPosition = restPosition + Vector3.up * (kind == MobKind.EndCrystal ? .08f * Mathf.Sin(idlePhase) : kind == MobKind.Blaze ? .09f * Mathf.Sin(idlePhase) : Mathf.Abs(Mathf.Sin(phase)) * stride * .012f);
            foreach (var joint in joints)
            {
                float angle = 0;
                Vector3 axis = joint.Right;
                if (joint.Kind == JointKind.Leg)
                {
                    angle = Mathf.Sin(phase) * joint.GaitSign * 32 * stride;
                    if (kind == MobKind.Spider) { axis = joint.Up; angle *= .65f; }
                }
                else if (joint.Kind == JointKind.Arm)
                {
                    angle = Mathf.Sin(phase + (joint.Side < 0 ? 0 : Mathf.PI)) * 23 * stride;
                    if (kind == MobKind.Zombie) angle = -78 + Mathf.Sin(idlePhase) * 2 + angle * .12f;
                    if (kind == MobKind.Skeleton && angry) angle = -78 + angle * .08f;
                    if (kind == MobKind.Villager) angle = 0;
                    else angle -= attackArc * (joint.Side > 0 ? 70 : 20);
                }
                else if (joint.Kind == JointKind.WingTip) { axis = joint.Forward; angle = Mathf.Sin(idlePhase - .7f) * 24 * joint.Side; }
                else if (joint.Kind == JointKind.Wing) { axis = joint.Forward; angle = Mathf.Sin(idlePhase) * 36 * joint.Side; }
                else if (joint.Kind == JointKind.Tail) { axis = joint.Up; angle = Mathf.Sin(idlePhase * .5f - joint.Index * .6f) * 7; }
                else if (joint.Kind == JointKind.Head) { axis = joint.Up; angle = Mathf.Sin(idlePhase * .24f) * (angry ? 2 : 5); }
                else if (joint.Kind == JointKind.Jaw) angle = 5 + attackArc * 25;
                else if (joint.Kind == JointKind.Crystal) { axis = joint.Up; angle = idlePhase * 28; }
                else if (joint.Kind == JointKind.Rod) { axis = joint.Up; angle = idlePhase * 35; }
                Quaternion pose = Quaternion.AngleAxis(angle, axis) * joint.Rest;
                joint.Transform.localRotation = Quaternion.Slerp(joint.Transform.localRotation, pose, jointBlend);
            }
            bool hurtTint = hurt > 0;
            bool fuseTint = fuse > 0 && Mathf.Sin(fuse * 32) > 0;
            int tint = (hurtTint ? 1 : 0) | (fuseTint ? 2 : 0);
            if (appliedTint == tint) return;
            appliedTint = tint;
            for (int i = 0; i < renderers.Count; i++)
            {
                Color color = hurtTint ? Color.Lerp(colors[i], new Color(1, .13f, .11f), .65f) : colors[i];
                if (fuseTint) color = Color.Lerp(color, Color.white, .8f);
                properties.SetColor("_Color", color);
                properties.SetColor("_BaseColor", color);
                renderers[i].SetPropertyBlock(properties);
            }
        }
        static JointKind ClassifyJoint(string name)
        {
            if (name.StartsWith("leg_")) return JointKind.Leg;
            if (name.StartsWith("arm_")) return JointKind.Arm;
            if (name.StartsWith("wingtip_")) return JointKind.WingTip;
            if (name.StartsWith("wing_")) return JointKind.Wing;
            if (name.StartsWith("tail_")) return JointKind.Tail;
            if (name == "head_joint") return JointKind.Head;
            if (name == "jaw") return JointKind.Jaw;
            if (name == "crystal_spin") return JointKind.Crystal;
            return JointKind.Rod;
        }
        public void SetVisible(bool visible)
        {
            if (this.visible == visible) return;
            this.visible = visible;
            foreach (var renderer in renderers) renderer.enabled = visible;
        }
    }
}
