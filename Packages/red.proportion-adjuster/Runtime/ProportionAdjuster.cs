using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Red.Proportions
{
    public enum LengthUnit
    {
        [InspectorName("cm")] Centimetres,
        [InspectorName("inches")] Inches,
    }

    // Reshapes the avatar at build time to the player's measurements
    // (Editor/ProportionAdjusterPlugin.cs), so the scene stays untouched. The whole armature is
    // scaled evenly until the eyes are at the eye height. At that size, the hips, knees and arm
    // segments go straight to their measurements, and the torso is stretched lengthwise to keep the
    // eyes where they are. The neck is a plain length factor, which the torso also balances.
    // Bones are never scaled (SPS refuses scaled parents above a socket, and a non-uniformly
    // scaled bone shears its children): joints are moved along the limb, and the skin is
    // stretched by scaled leaf proxies, so length and thickness are independent.
    [DisallowMultipleComponent]
    [AddComponentMenu("Proportion Adjuster")]
    public class ProportionAdjuster : MonoBehaviour, IEditorOnly
    {
        [Header("Settings")]
        [Tooltip("Move the hips and reshape thighs and shins to the hip and knee heights. Feet keep their size and stay on the floor; the view position moves with the hips. Off: the leg measurements and thickness sliders aren't used.")]
        public bool adjustLegs = true;
        [Tooltip("Reshape upper arms and forearms to the arm lengths. Hands keep their size. Off: the arm measurements and thickness sliders aren't used.")]
        public bool adjustArms = true;
        [Tooltip("Stretch the spine to the neck lengthwise so the eyes stay at the eye height when the hips move. Off: the eyes move with the hips, so they end up away from the eye height, and the Torso stretch sliders are only used to balance a changed Neck Length.")]
        public bool adjustTorso = true;
        [Tooltip("Rebuild the humanoid Avatar from the reshaped armature, so animations that place the hips (sitting, poses) use the new proportions. Leave this on unless the rebuilt Avatar causes problems.")]
        public bool rebuildHumanoidAvatar = true;

        // The measurements are in `units`. 0 means not measured yet: the avatar's own measurements
        // are filled in when the component is added to an avatar, or when it is first inspected
        // there (the prefab). Before 1.9 they were always cm, with Cm in their names.
        [Header("Player measurements (standing)")]
        [Tooltip("The unit of the measurements below. Switching converts them.")]
        public LengthUnit units = LengthUnit.Centimetres;
        [Tooltip("Floor to eyes. The whole armature is scaled evenly to this height first, and every other value is then applied at that size.")]
        [FormerlySerializedAs("eyeHeightCm")] public float eyeHeight;
        [Tooltip("Floor to where the avatar's Hips bone should end up.")]
        [FormerlySerializedAs("hipHeightCm")] public float hipHeight;
        [Tooltip("Floor to the knee joint (the gap at the side of the knee).")]
        [FormerlySerializedAs("kneeHeightCm")] public float kneeHeight;
        [Tooltip("Shoulder joint to elbow joint. The avatar's bones run joint centre to joint centre; the joint sits a little in from the outer edge of the shoulder.")]
        [FormerlySerializedAs("shoulderToElbowCm")] public float shoulderToElbow;
        [Tooltip("Elbow joint to wrist joint.")]
        [FormerlySerializedAs("elbowToWristCm")] public float elbowToWrist;

        // A locked measurement keeps its proportion to the eye height: the inspector scales it
        // whenever the eye height changes, and doesn't let it be edited.
        public bool lockHipHeight = true;
        public bool lockKneeHeight = true;
        public bool lockShoulderToElbow = true;
        public bool lockElbowToWrist = true;
        // The last eye height above 0, kept while the eye height isn't (mid-typing, or dragged past
        // 0), so the locked measurements can still follow once it is again. 0 otherwise.
        [HideInInspector] public float lastValidEyeHeight;

        [Header("Thickness (1 = unchanged)")]
        [Range(0.5f, 1.5f)] public float thighThickness = 1f;
        [Range(0.5f, 1.5f)] public float shinThickness = 1f;
        [Range(0.5f, 1.5f)] public float upperArmThickness = 1f;
        [Range(0.5f, 1.5f)] public float forearmThickness = 1f;

        [Header("Torso thickness (1 = unchanged)")]
        [Tooltip("Width and depth of the belly from the Hips bone up to the Spine bone, clothing included. It fades out from the Hips bone down to the top of the thighs, so the pelvis, genitals and legs stay as they are. Blends into Spine Thickness above.")]
        [Range(0.5f, 1.5f)] public float lowerBellyThickness = 1f;
        [Tooltip("Width and depth of the skin on the Spine bone. If shoulders or other bones hang from it, they move out with it.")]
        [Range(0.5f, 1.5f)] public float spineThickness = 1f;
        [Tooltip("Width and depth of the skin on the Chest bone. If the shoulders hang from it, they move out with it, so the arms stay attached and the shoulders widen.")]
        [Range(0.5f, 1.5f)] public float chestThickness = 1f;
        [Tooltip("Width and depth of the skin on the Upper Chest bone. If the shoulders hang from it, they move out with it, so the arms stay attached and the shoulders widen.")]
        [Range(0.5f, 1.5f)] public float upperChestThickness = 1f;

        [Header("Neck (1 = unchanged)")]
        [Tooltip("Neck length factor. The head keeps its height: what the neck loses, the spine, chest and upper chest gain in proportion, so the hips and eyes stay where they are. Needs a Neck bone in the humanoid mapping.")]
        [Range(0.3f, 1.5f)] public float neckLength = 1f;
        [Tooltip("Neck thickness factor.")]
        [Range(0.5f, 1.5f)] public float neckThickness = 1f;

        [Header("Torso stretch (1 = even, 0 = keeps its length)")]
        [Tooltip("How much of the torso's stretch the gap from the Hips bone to the Spine bone takes, relative to the others. Starts at 0, which keeps its length. Only the spine and everything above it move: the pelvis and legs stay put, and the belly stretches through its existing weights.")]
        [Range(0f, 5f)] public float hipsStretch = 0f;
        [Tooltip("How much of the torso's stretch the Spine bone takes, relative to the others. With all at 1 every bone is stretched by the same factor; higher takes more, and 0 keeps this bone's length. The total always adds up to what the eye height needs. The list below shows the result.")]
        [Range(0f, 5f)] public float spineStretch = 1f;
        [Tooltip("How much of the torso's stretch the Chest bone takes, relative to the others. See Spine Stretch.")]
        [Range(0f, 5f)] public float chestStretch = 1f;
        [Tooltip("How much of the torso's stretch the Upper Chest bone takes, relative to the others. See Spine Stretch.")]
        [Range(0f, 5f)] public float upperChestStretch = 1f;

        public const float CmPerInch = 2.54f;

        public static float CmPer(LengthUnit unit) => unit == LengthUnit.Inches ? CmPerInch : 1f;
        public float CmPerUnit => CmPer(units);
        public string UnitName => units == LengthUnit.Inches ? "in" : "cm";

        public float EyeHeightCm => eyeHeight * CmPerUnit;
        public float HipHeightCm => hipHeight * CmPerUnit;
        public float KneeHeightCm => kneeHeight * CmPerUnit;
        public float ShoulderToElbowCm => shoulderToElbow * CmPerUnit;
        public float ElbowToWristCm => elbowToWrist * CmPerUnit;

        // The avatar's own proportions in avatar root units, measured on the left side. Multiply
        // by rootScale for real size.
        public struct Measurements
        {
            // hip is the Hips bone; thigh is the top of the thigh, usually several cm below it.
            public float eye, hip, thigh, knee, ankle, upperArm, forearm;
            // Neck joint to head joint; 0 when the avatar has no Neck bone.
            public float neck;
            public float rootScale;
        }

        // False when the root lacks a descriptor, a humanoid Animator or one of the limb bones.
        public static bool TryMeasure(Transform root, out Measurements m)
        {
            m = default;
            var anim = root.GetComponent<Animator>();
            var desc = root.GetComponent<VRCAvatarDescriptor>();
            if (!anim || !anim.isHuman || !desc) return false;

            var limbBones = new[]
            {
                HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            };
            foreach (var b in limbBones) if (!anim.GetBoneTransform(b)) return false;

            Transform Bone(HumanBodyBones b) => anim.GetBoneTransform(b);
            float Height(HumanBodyBones b) => root.InverseTransformPoint(Bone(b).position).y;
            float Length(HumanBodyBones from, HumanBodyBones to) => root.InverseTransformVector(Bone(to).position - Bone(from).position).magnitude;

            // The view position is stored at real size, not in root units.
            m.rootScale = root.lossyScale.y;
            m.eye = root.InverseTransformVector(desc.ViewPosition).y;
            m.hip = Height(HumanBodyBones.Hips);
            m.thigh = Height(HumanBodyBones.LeftUpperLeg);
            m.knee = Height(HumanBodyBones.LeftLowerLeg);
            m.ankle = Height(HumanBodyBones.LeftFoot);
            m.upperArm = Length(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
            m.forearm = Length(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            m.neck = Bone(HumanBodyBones.Neck) ? Length(HumanBodyBones.Neck, HumanBodyBones.Head) : 0f;
            return true;
        }

        public bool IsMeasured => eyeHeight > 0f;

        // The thickness of a torso segment, named by the bone it starts from.
        public float TorsoThickness(HumanBodyBones bone) =>
            bone == HumanBodyBones.Hips ? lowerBellyThickness : bone == HumanBodyBones.Spine ? spineThickness : bone == HumanBodyBones.Chest ? chestThickness : upperChestThickness;

        // A length of the avatar's, from root units to the given unit, rounded to 0.1 cm or
        // 0.01 inches, which moves things by well under a millimetre.
        public static float FromAvatar(float rootUnits, float rootScale, LengthUnit unit)
        {
            float steps = unit == LengthUnit.Inches ? 100f : 10f;
            return Mathf.Round(rootUnits * rootScale * 100f / CmPer(unit) * steps) / steps;
        }

        // Unity calls this when the component is added (and on Reset in its context menu).
        void Reset() => MeasureAvatar();

        // Fills in the avatar's own proportions, which change nothing, so only what's typed in
        // afterwards takes effect. False when not on a measurable avatar.
        public bool MeasureAvatar()
        {
            var desc = GetComponentInParent<VRCAvatarDescriptor>();
            if (!desc || !TryMeasure(desc.transform, out var m)) return false;

            eyeHeight = FromAvatar(m.eye, m.rootScale, units);
            hipHeight = FromAvatar(m.hip, m.rootScale, units);
            kneeHeight = FromAvatar(m.knee, m.rootScale, units);
            shoulderToElbow = FromAvatar(m.upperArm, m.rootScale, units);
            elbowToWrist = FromAvatar(m.forearm, m.rootScale, units);
            lastValidEyeHeight = 0f;
            return true;
        }
    }
}
