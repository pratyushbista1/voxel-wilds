using System.Collections.Generic;
using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class MobVisual : MonoBehaviour
    {
        sealed class Joint
        {
            public Transform Transform;
            public Quaternion Rest;
            public string Name;
            public int Side;
        }
        readonly List<Joint> joints = new List<Joint>();
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Color> colors = new List<Color>();
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        Transform model;
        Vector3 restPosition;
        MobKind kind;
        float phase, stride;
        bool visible = true;

        public void Init(MobKind mobKind)
        {
            kind = mobKind;
            var prefab = Resources.Load<GameObject>("Models/" + MobRules.ModelName(kind));
            if (prefab != null)
            {
                model = Instantiate(prefab, transform, false).transform;
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                foreach (var collider in model.GetComponentsInChildren<Collider>()) Destroy(collider);
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
                    joints.Add(new Joint { Transform = part, Rest = part.localRotation, Name = name, Side = name.Contains("_-1") ? -1 : 1 });
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
        }

        public void Animate(float dt, float speed, float attack, float hurt, float fuse, bool angry)
        {
            if (model == null) return;
            stride = Mathf.Lerp(stride, Mathf.Clamp01(speed / Mathf.Max(.1f, MobRules.Definition(kind).Speed)), 1 - Mathf.Exp(-dt * 8));
            phase += dt * (kind == MobKind.EndDragon ? 4 : 4 + speed * 2.3f);
            float attackArc = Mathf.Sin(Mathf.Clamp01(attack) * Mathf.PI);
            model.localPosition = restPosition + Vector3.up * (kind == MobKind.EndCrystal ? .08f * Mathf.Sin(phase) : kind == MobKind.Blaze ? .09f * Mathf.Sin(phase) : Mathf.Abs(Mathf.Sin(phase)) * stride * .035f);
            foreach (var joint in joints)
            {
                float angle = 0;
                Vector3 axis = transform.right;
                if (joint.Name.StartsWith("leg_"))
                {
                    int alternate = joint.Name.EndsWith("_-1") ? -1 : 1;
                    angle = Mathf.Sin(phase + (joint.Side * alternate > 0 ? 0 : Mathf.PI)) * 28 * stride;
                    if (kind == MobKind.Spider) { axis = transform.up; angle *= .65f; }
                }
                else if (joint.Name.StartsWith("arm_"))
                {
                    angle = Mathf.Sin(phase + (joint.Side < 0 ? 0 : Mathf.PI)) * 23 * stride;
                    if (kind == MobKind.Zombie && angry) angle -= 65;
                    if (kind != MobKind.Villager) angle -= attackArc * (joint.Side > 0 ? 105 : 35);
                }
                else if (joint.Name.StartsWith("wingtip_")) { axis = transform.forward; angle = Mathf.Sin(phase - .7f) * 24 * joint.Side; }
                else if (joint.Name.StartsWith("wing_")) { axis = transform.forward; angle = Mathf.Sin(phase) * 36 * joint.Side; }
                else if (joint.Name.StartsWith("tail_")) { axis = transform.up; angle = Mathf.Sin(phase * .5f - joint.Transform.GetSiblingIndex() * .6f) * 8; }
                else if (joint.Name == "head_joint") { axis = transform.up; angle = Mathf.Sin(phase * .24f) * (angry ? 2 : 7); }
                else if (joint.Name == "jaw") angle = 5 + attackArc * 25;
                else if (joint.Name == "crystal_spin") { axis = transform.up; angle = phase * 28; }
                else if (joint.Name.StartsWith("rod_")) { axis = transform.up; angle = phase * 35; }
                Vector3 localAxis = joint.Transform.parent != null ? joint.Transform.parent.InverseTransformDirection(axis).normalized : axis;
                joint.Transform.localRotation = Quaternion.AngleAxis(angle, localAxis) * joint.Rest;
            }
            for (int i = 0; i < renderers.Count; i++)
            {
                Color color = hurt > 0 ? Color.Lerp(colors[i], new Color(1, .13f, .11f), .65f) : colors[i];
                if (fuse > 0 && Mathf.Sin(fuse * 32) > 0) color = Color.Lerp(color, Color.white, .8f);
                properties.SetColor("_Color", color);
                properties.SetColor("_BaseColor", color);
                renderers[i].SetPropertyBlock(properties);
            }
        }
        public void SetVisible(bool visible)
        {
            if (this.visible == visible) return;
            this.visible = visible;
            foreach (var renderer in renderers) renderer.enabled = visible;
        }
    }
}
