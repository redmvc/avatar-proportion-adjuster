using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(Red.Proportions.ProportionAdjusterPlugin))]

namespace Red.Proportions
{
    // What ProportionAdjuster will do to an avatar. Shared by the build pass, the preview and the
    // inspector.
    internal struct ProportionPlan
    {
        // The avatar as it is.
        public ProportionAdjuster.Measurements now;
        // How far the hips come down, and each segment's length factor. torsoScale applies to
        // every segment from the spine to the neck and balances the neck change. scale is what
        // the whole avatar is then multiplied by.
        public float delta, thighScale, shinScale, upperArmScale, forearmScale, neckScale, torsoScale, scale;

        public float NewEye => now.eye - delta;

        // Real size in cm, once everything has been applied, of a length in root units.
        public float FinalCm(float units) => units * now.rootScale * scale * 100f;
        public float FinalKnee => now.ankle + (now.knee - now.ankle) * shinScale;

        // Returns an error message, or null when the plan is usable.
        public static string Compute(ProportionAdjuster a, Transform root, out ProportionPlan plan)
        {
            plan = default;
            if (!ProportionAdjuster.TryMeasure(root, out plan.now)) return "The avatar root needs a descriptor and a humanoid Animator with leg and arm bones.";
            var m = plan.now;
            plan.thighScale = plan.shinScale = plan.upperArmScale = plan.forearmScale = plan.neckScale = plan.torsoScale = plan.scale = 1f;

            if (a.eyeHeightCm <= 0f) return "Eye height must be positive.";
            if (a.adjustLegs)
            {
                float hipRatio = a.hipHeightCm / a.eyeHeightCm, kneeRatio = a.kneeHeightCm / a.eyeHeightCm;
                if (!(kneeRatio > 0f && kneeRatio < hipRatio && hipRatio < 1f)) return "Measurements must satisfy 0 < knee < hip < eye.";

                // Lowering the hips lowers the eyes by the same amount: solve (hip - d) / (eye - d) = hipRatio.
                plan.delta = (m.hip - hipRatio * m.eye) / (1f - hipRatio);
                float newKnee = kneeRatio * plan.NewEye;
                if (newKnee <= m.ankle) return "The knee would end up below the ankle.";
                plan.thighScale = (m.thigh - plan.delta - newKnee) / (m.thigh - m.knee);
                plan.shinScale = (newKnee - m.ankle) / (m.knee - m.ankle);
            }
            if (a.adjustArms)
            {
                if (a.shoulderToElbowCm <= 0f || a.elbowToWristCm <= 0f) return "Arm lengths must be positive.";
                plan.upperArmScale = a.shoulderToElbowCm / a.eyeHeightCm * plan.NewEye / m.upperArm;
                plan.forearmScale = a.elbowToWristCm / a.eyeHeightCm * plan.NewEye / m.forearm;
            }

            if (!Mathf.Approximately(a.neckLength, 1f))
            {
                var anim = root.GetComponent<Animator>();
                var neck = anim.GetBoneTransform(HumanBodyBones.Neck);
                if (!neck) return "Neck length needs a Neck bone in the humanoid mapping.";
                plan.neckScale = a.neckLength;
                // The head drops by what the neck loses; stretching the spine-to-neck chain by one
                // factor puts it back. Both measured along each bone's stretch axis, in root space.
                float headDrop = (1f - plan.neckScale) * ProportionReshaper.AxisRise(root, neck, anim.GetBoneTransform(HumanBodyBones.Head));
                float torsoRise = ProportionReshaper.TorsoChain(anim).Sum(seg => ProportionReshaper.AxisRise(root, anim.GetBoneTransform(seg.bone), anim.GetBoneTransform(seg.next)));
                if (torsoRise < 0.01f) return "The spine to neck chain is too short to balance the neck.";
                plan.torsoScale = 1f + headDrop / torsoRise;
            }

            if (plan.NewEye <= 0f) return "The eyes would end up below the floor.";
            if (a.scaleToMeasurements)
            {
                plan.scale = a.eyeHeightCm / 100f / (plan.NewEye * m.rootScale);
                // Rounding in the measurements alone shouldn't scale an untouched avatar.
                if (Mathf.Abs(plan.scale - 1f) < 0.0005f) plan.scale = 1f;
            }

            var scales = new[] { plan.thighScale, plan.shinScale, plan.upperArmScale, plan.forearmScale, plan.neckScale, plan.torsoScale };
            if (scales.Any(s => !(s > 0.3f && s < 3f))) return $"Lengths out of range (thigh {plan.thighScale:F3}, shin {plan.shinScale:F3}, upper arm {plan.upperArmScale:F3}, forearm {plan.forearmScale:F3}, neck {plan.neckScale:F3}, torso {plan.torsoScale:F3}).";
            return null;
        }
    }

    // Reshapes an avatar in place: the build copy (ProportionAdjusterPlugin) or the preview copy
    // (ProportionPreview). All armatures under the root get the same edit, with bones matched by
    // the humanoid bones' names, so VRCFury later links clothing onto bones that match.
    internal static class ProportionReshaper
    {
        // beforeSkin runs once the joints have moved but before the skin proxies exist. Created
        // proxies are added to proxiesOut when given. Returns an error message, or null.
        public static string Reshape(ProportionAdjuster a, Transform root, System.Action<Animator> beforeSkin, List<Transform> proxiesOut, out string summary)
        {
            summary = null;
            var error = ProportionPlan.Compute(a, root, out var plan);
            if (error != null) return error;

            var anim = root.GetComponent<Animator>();
            var all = root.GetComponentsInChildren<Transform>(true);
            Transform[] Named(HumanBodyBones b) => all.Where(t => t.name == anim.GetBoneTransform(b).name).ToArray();

            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            var headBefore = root.InverseTransformPoint(head.position);
            var hips = Named(HumanBodyBones.Hips);
            foreach (var h in hips) h.position -= root.TransformVector(0f, plan.delta, 0f);

            // childSkin: the bone's other children are twist bones that share its skin stretch.
            // Off for the neck and torso, whose other children (shoulders, breasts, helpers) only move.
            var segments = new List<(HumanBodyBones bone, HumanBodyBones next, float length, float thickness, bool childSkin)>();
            if (a.adjustLegs)
            {
                segments.Add((HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, plan.thighScale, a.thighThickness, true));
                segments.Add((HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, plan.thighScale, a.thighThickness, true));
                segments.Add((HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, plan.shinScale, a.shinThickness, true));
                segments.Add((HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, plan.shinScale, a.shinThickness, true));
            }
            if (a.adjustArms)
            {
                segments.Add((HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, plan.upperArmScale, a.upperArmThickness, true));
                segments.Add((HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, plan.upperArmScale, a.upperArmThickness, true));
                segments.Add((HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, plan.forearmScale, a.forearmThickness, true));
                segments.Add((HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, plan.forearmScale, a.forearmThickness, true));
            }
            if (!Mathf.Approximately(plan.neckScale, 1f))
            {
                segments.Add((HumanBodyBones.Neck, HumanBodyBones.Head, plan.neckScale, a.neckThickness, false));
                foreach (var seg in TorsoChain(anim)) segments.Add((seg.bone, seg.next, plan.torsoScale, 1f, false));
            }

            // Each segment's skin is stretched in the bone's own frame: `length` along the axis
            // pointing at the next joint, `thickness` across it. The bone's children (next joint,
            // twist bones, helpers) move by the same stretch, so nothing is scaled except the skin.
            var skinScale = new Dictionary<Transform, System.Func<Vector3>>();
            foreach (var seg in segments)
            {
                var mainBone = anim.GetBoneTransform(seg.bone);
                var next = anim.GetBoneTransform(seg.next);
                int axis = DominantAxis(mainBone.InverseTransformPoint(next.position));
                var stretch = Vector3.one * seg.thickness;
                stretch[axis] = seg.length;

                foreach (var bone in Named(seg.bone))
                {
                    skinScale[bone] = () => stretch;
                    foreach (Transform child in bone)
                    {
                        child.localPosition = Vector3.Scale(child.localPosition, stretch);
                        // Twist bones; helpers get an entry too but never a proxy, as no mesh uses them.
                        if (seg.childSkin && child.name != next.name) skinScale[child] = () => InChildFrame(child, stretch);
                    }
                }
            }

            // The view point follows the head: down with the hips, and back up by the torso when the
            // neck changes (a slanted torso can also shift it slightly forward or back).
            var desc = root.GetComponent<VRCAvatarDescriptor>();
            desc.ViewPosition += root.TransformVector(root.InverseTransformPoint(head.position) - headBefore);
            beforeSkin?.Invoke(anim);

            // A leaf child at the bone's origin, scaled, takes over the bone's skin weights. Its
            // bindpose is the bone's, so vertices are stretched about the bone's own origin.
            var proxies = new Dictionary<Transform, Transform>();
            int rebound = 0;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = smr.bones;
                bool changed = false;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (!bones[i] || !skinScale.TryGetValue(bones[i], out var scale)) continue;
                    if (!proxies.TryGetValue(bones[i], out var proxy))
                    {
                        proxy = new GameObject(bones[i].name + " (stretched skin)").transform;
                        proxy.SetParent(bones[i], false);
                        proxy.localScale = scale();
                        proxies[bones[i]] = proxy;
                    }
                    bones[i] = proxy;
                    changed = true;
                }
                if (changed) { smr.bones = bones; rebound++; }
            }
            proxiesOut?.AddRange(proxies.Values);

            // Last, so the rebuilt Avatar keeps the armature's own units. Even scaling is fine for
            // SPS, and the feet stay on the floor as it scales about the root.
            root.localScale *= plan.scale;
            desc.ViewPosition *= plan.scale;

            ProportionAdjuster.TryMeasure(root, out var f);
            float Cm(float units) => units * f.rootScale * 100f;
            summary = $"final cm: eye {Cm(f.eye):F1}, hips {Cm(f.hip):F1}, knee {Cm(f.knee):F1}, upper arm {Cm(f.upperArm):F1}, forearm {Cm(f.forearm):F1}, neck {Cm(f.neck):F1}; " +
                      $"whole avatar x{plan.scale:F4}; " +
                      $"hips {-plan.delta * 100f:+0.0;-0.0} cm before scaling on {hips.Length} armatures, view Y {desc.ViewPosition.y:F4}; " +
                      $"length thigh x{plan.thighScale:F3}, shin x{plan.shinScale:F3}, upper arm x{plan.upperArmScale:F3}, forearm x{plan.forearmScale:F3}, " +
                      $"neck x{plan.neckScale:F3}, torso x{plan.torsoScale:F3}; {proxies.Count} skin proxies on {rebound} meshes";
            return null;
        }

        // The segments from the spine up to the neck, skipping the optional chest bones when the
        // avatar lacks them. The hips are left out: their skin must not stretch.
        public static List<(HumanBodyBones bone, HumanBodyBones next)> TorsoChain(Animator anim)
        {
            var present = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck }
                .Where(b => anim.GetBoneTransform(b)).ToList();
            var chain = new List<(HumanBodyBones, HumanBodyBones)>();
            for (int i = 0; i + 1 < present.Count; i++) chain.Add((present[i], present[i + 1]));
            return chain;
        }

        // How much the next joint rises in root space per unit of stretch along the bone's stretch
        // axis (the one pointing at the next joint): that axis component of the joint's offset.
        public static float AxisRise(Transform root, Transform bone, Transform next)
        {
            var local = bone.InverseTransformPoint(next.position);
            int axis = DominantAxis(local);
            var alongAxis = Vector3.zero;
            alongAxis[axis] = local[axis];
            return root.InverseTransformVector(bone.TransformVector(alongAxis)).y;
        }

        static int DominantAxis(Vector3 v)
        {
            v = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return v.x > v.y && v.x > v.z ? 0 : v.y > v.z ? 1 : 2;
        }

        // The parent-frame stretch expressed in the child's frame. Twist bones share their limb's
        // axes, so this is exact for them; for anything rotated off-axis it's an approximation.
        static Vector3 InChildFrame(Transform child, Vector3 stretch)
        {
            var result = Vector3.one;
            for (int k = 0; k < 3; k++)
            {
                var axisInParent = child.localRotation * new Vector3(k == 0 ? 1 : 0, k == 1 ? 1 : 0, k == 2 ? 1 : 0);
                int j = DominantAxis(axisInParent);
                if (Mathf.Abs(axisInParent[j]) < 0.95f) Debug.LogWarning($"Proportion Adjuster: {child.name} isn't aligned with its parent's axes; its skin stretch is approximate.", child);
                result[k] = stretch[j];
            }
            return result;
        }
    }

    // Applies ProportionAdjuster to the build copy. NDMF's Transforming phase runs before VRCFury
    // (callback order -11000 vs -10000), so the clothing link armatures are still separate copies
    // here and get reshaped along with the main armature.
    public class ProportionAdjusterPlugin : Plugin<ProportionAdjusterPlugin>
    {
        public override string QualifiedName => "red.proportion-adjuster";
        public override string DisplayName => "Proportion Adjuster";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run("Adjust proportions", ctx =>
            {
                var adjuster = ctx.AvatarRootObject.GetComponentInChildren<ProportionAdjuster>(true);
                if (!adjuster) return;
                if (!adjuster.IsMeasured)
                {
                    Debug.LogWarning("Proportion Adjuster: not set up (select it in the inspector to measure the avatar). Skipped.", ctx.AvatarRootObject);
                    Remove(adjuster);
                    return;
                }

                var root = ctx.AvatarRootTransform;
                // The humanoid Avatar is rebuilt before the proxies exist, so they stay out of its skeleton.
                var error = ProportionReshaper.Reshape(adjuster, root, anim =>
                {
                    if (!adjuster.rebuildHumanoidAvatar) return;
                    var avatar = BuildAvatarFromScene(anim);
                    if (!avatar) return;
                    ctx.AssetSaver.SaveAsset(avatar);
                    SetAvatarKeepingPose(anim, avatar);
                }, null, out var summary);

                if (error != null) Debug.LogError($"Proportion Adjuster: {error} Skipped.", root);
                else Debug.Log($"Proportion Adjuster: {summary}", root);
                Remove(adjuster);
            });
        }

        // Assigning an Avatar makes the Animator rebind, which also puts its humanoid bones back
        // where they were at the start of the frame: in a build, before the reshape. The binding
        // is what's wanted, so the pose is restored afterwards, as Modular Avatar's own rebind
        // does. The preview never assigns an Avatar, so it never showed this.
        static void SetAvatarKeepingPose(Animator anim, Avatar avatar)
        {
            var pose = anim.GetComponentsInChildren<Transform>(true)
                .Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToList();
            anim.avatar = avatar;
            foreach (var (t, position, rotation, scale) in pose)
            {
                t.SetLocalPositionAndRotation(position, rotation);
                t.localScale = scale;
            }
        }

        // The avatar root holds several armatures with the same bone names (VRCFury link
        // armatures), so the builder works on a transform-only copy of the main armature.
        // Rotations come from the original Avatar: its reference pose can differ from the scene's
        // rest pose (an enforced T-pose versus the model's A-pose), and replacing it rotates every
        // animated limb by the difference. Positions come from the reshaped armature.
        static Avatar BuildAvatarFromScene(Animator anim)
        {
            var armature = anim.GetBoneTransform(HumanBodyBones.Hips).parent;
            var copyRoot = new GameObject(anim.name);
            try
            {
                CopyTransforms(armature, copyRoot.transform);
                var hd = anim.avatar.humanDescription;
                var referenceRotations = hd.skeleton.GroupBy(b => b.name).ToDictionary(g => g.Key, g => g.First().rotation);
                hd.skeleton = copyRoot.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = referenceRotations.TryGetValue(t.name, out var r) ? r : t.localRotation,
                    scale = t.localScale,
                }).ToArray();

                var avatar = AvatarBuilder.BuildHumanAvatar(copyRoot, hd);
                if (!avatar.isValid || !avatar.isHuman) { Debug.LogError("Proportion Adjuster: rebuilt Avatar is not a valid humanoid; keeping the original.", anim); return null; }
                avatar.name = $"{anim.name} Proportion Adjuster Avatar";
                return avatar;
            }
            finally { Object.DestroyImmediate(copyRoot); }
        }

        // Also removes the prefab's own GameObject, which is empty once the component is gone.
        static void Remove(ProportionAdjuster adjuster)
        {
            var go = adjuster.gameObject;
            Object.DestroyImmediate(adjuster);
            if (!go.GetComponent<VRCAvatarDescriptor>() && go.transform.childCount == 0 && go.GetComponents<Component>().Length == 1)
                Object.DestroyImmediate(go);
        }

        static void CopyTransforms(Transform src, Transform parent)
        {
            var t = new GameObject(src.name).transform;
            t.SetParent(parent, false);
            t.localPosition = src.localPosition;
            t.localRotation = src.localRotation;
            t.localScale = src.localScale;
            foreach (Transform child in src) CopyTransforms(child, t);
        }
    }

    // Shows what the build will do, measured from the avatar in the scene, and drives the preview.
    [CustomEditor(typeof(ProportionAdjuster))]
    internal class ProportionAdjusterEditor : Editor
    {
        string previewError;

        // A component from the prefab arrives unmeasured; fill in the avatar it was dropped on.
        void OnEnable()
        {
            var adjuster = (ProportionAdjuster)target;
            if (adjuster.IsMeasured || !adjuster.GetComponentInParent<VRCAvatarDescriptor>()) return;
            Undo.RecordObject(adjuster, "Measure Avatar");
            if (adjuster.MeasureAvatar()) PrefabUtility.RecordPrefabInstancePropertyModifications(adjuster);
        }

        // What an input's reset button sets it to, or null when there's nothing to set it from.
        // Each input is independent; the plan's error covers combinations that don't work.
        internal static float? AvatarValue(string field, Transform root)
        {
            switch (field)
            {
                case nameof(ProportionAdjuster.thighThickness):
                case nameof(ProportionAdjuster.shinThickness):
                case nameof(ProportionAdjuster.upperArmThickness):
                case nameof(ProportionAdjuster.forearmThickness):
                case nameof(ProportionAdjuster.neckLength):
                case nameof(ProportionAdjuster.neckThickness):
                    return 1f;
            }
            if (!root || !ProportionAdjuster.TryMeasure(root, out var m)) return null;

            float Cm(float units) => Mathf.Round(units * m.rootScale * 1000f) / 10f;
            switch (field)
            {
                case nameof(ProportionAdjuster.eyeHeightCm): return Cm(m.eye);
                case nameof(ProportionAdjuster.hipHeightCm): return Cm(m.hip);
                case nameof(ProportionAdjuster.kneeHeightCm): return Cm(m.knee);
                case nameof(ProportionAdjuster.shoulderToElbowCm): return Cm(m.upperArm);
                case nameof(ProportionAdjuster.elbowToWristCm): return Cm(m.forearm);
            }
            return null;
        }

        // Everything but the Script field, with a reset button beside each number and slider.
        void DrawFields(Transform root)
        {
            const float buttonWidth = 20f;
            var icon = EditorGUIUtility.IconContent("Refresh").image;
            var prop = serializedObject.GetIterator();
            prop.NextVisible(true);
            do
            {
                if (prop.name == "m_Script") continue;
                if (prop.propertyType != SerializedPropertyType.Float)
                {
                    EditorGUILayout.PropertyField(prop, true);
                    continue;
                }

                // The height includes any header above the input, so the button sits on the last line.
                var rect = EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(prop, true));
                var field = rect;
                field.width -= buttonWidth + 2f;
                var button = new Rect(rect.xMax - buttonWidth, rect.yMax - EditorGUIUtility.singleLineHeight, buttonWidth, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(field, prop, true);

                var value = AvatarValue(prop.name, root);
                var tooltip = prop.name.EndsWith("Cm") ? "Back to the avatar's own measurement." : "Back to 1, the avatar's own.";
                using (new EditorGUI.DisabledScope(!value.HasValue || Mathf.Abs(prop.floatValue - value.Value) < 0.001f))
                {
                    if (GUI.Button(button, new GUIContent(icon, tooltip), EditorStyles.iconButton))
                    {
                        prop.floatValue = value.Value;
                        GUI.changed = true;
                    }
                }
            } while (prop.NextVisible(false));
        }

        public override void OnInspectorGUI()
        {
            var adjuster = (ProportionAdjuster)target;
            var desc = adjuster.GetComponentInParent<VRCAvatarDescriptor>();
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawFields(desc ? desc.transform : null);
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            if (!desc) { EditorGUILayout.HelpBox("Put this on (or under) an avatar root.", MessageType.Warning); return; }

            bool previewing = ProportionPreview.IsShowing(adjuster);
            if (changed && previewing) previewError = ProportionPreview.Refresh();

            EditorGUILayout.Space();
            if (GUILayout.Button(previewing ? "Stop Preview" : "Preview", GUILayout.Height(24)))
            {
                if (previewing) ProportionPreview.End();
                else previewError = ProportionPreview.Begin(adjuster);
            }
            if (ProportionPreview.IsShowing(adjuster))
                EditorGUILayout.HelpBox("Previewing on a temporary copy; the avatar itself is hidden in the Scene view, not changed. " +
                                        "The preview ends when you select something else, enter play mode or build.", MessageType.None);
            if (previewError != null && ProportionPreview.IsShowing(adjuster)) EditorGUILayout.HelpBox(previewError, MessageType.Error);

            var error = ProportionPlan.Compute(adjuster, desc.transform, out var p);
            if (error != null) { EditorGUILayout.HelpBox(error, MessageType.Error); return; }

            string Ratio(float avatar, float player) => $"{avatar:F3} -> {player:F3}";
            string Shape(float length, float thickness) => $"length x{length:F3}, thickness x{thickness:F2}";
            float e = adjuster.eyeHeightCm;
            var m = p.now;
            var text =
                "Ratios to eye height (avatar now -> you):\n" +
                $"  Hip {Ratio(m.hip / m.eye, adjuster.hipHeightCm / e)}, knee {Ratio(m.knee / m.eye, adjuster.kneeHeightCm / e)}\n" +
                $"  Upper arm {Ratio(m.upperArm / m.eye, adjuster.shoulderToElbowCm / e)}, forearm {Ratio(m.forearm / m.eye, adjuster.elbowToWristCm / e)}\n\n" +
                $"Final cm: eye {p.FinalCm(p.NewEye):F1}, hips {p.FinalCm(m.hip - p.delta):F1}, knee {p.FinalCm(p.FinalKnee):F1}\n" +
                $"  Upper arm {p.FinalCm(m.upperArm * p.upperArmScale):F1}, forearm {p.FinalCm(m.forearm * p.forearmScale):F1}, neck {p.FinalCm(m.neck * p.neckScale):F1}\n\n" +
                $"Build will: hips {-p.delta * 100f:+0.0;-0.0} cm, then scale the whole avatar x{p.scale:F3}\n" +
                (adjuster.adjustLegs ? $"  Thigh {Shape(p.thighScale, adjuster.thighThickness)}\n  Shin {Shape(p.shinScale, adjuster.shinThickness)}\n" : "") +
                (adjuster.adjustArms ? $"  Upper arm {Shape(p.upperArmScale, adjuster.upperArmThickness)}\n  Forearm {Shape(p.forearmScale, adjuster.forearmThickness)}\n" : "") +
                (p.neckScale != 1f ? $"  Neck {Shape(p.neckScale, adjuster.neckThickness)}: {m.neck * m.rootScale * 100f:F1} -> {p.FinalCm(m.neck * p.neckScale):F1} cm\n  Spine to neck length x{p.torsoScale:F3} to keep the head height" : "");
            EditorGUILayout.HelpBox(text.TrimEnd('\n'), MessageType.Info);
        }
    }
}
