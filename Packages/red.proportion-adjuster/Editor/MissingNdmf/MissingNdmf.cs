using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

// Compiled only when NDMF is missing (or older than 1.8), in which case the real editor
// assembly is left out entirely. Without NDMF nothing would apply the component, and VRChat
// strips it as editor-only, so the avatar would upload unchanged without a word. Say so
// instead, and refuse to build an avatar that has the component.
namespace Red.Proportions
{
    internal static class MissingNdmf
    {
        public const string Message =
            "Proportion Adjuster needs NDMF (Non-Destructive Modular Framework) 1.8 or later, which isn't installed. " +
            "Add NDMF, or Modular Avatar which includes it, from the VRChat Creator Companion or ALCOM.";
    }

    [InitializeOnLoad]
    internal static class MissingNdmfWarning
    {
        static MissingNdmfWarning() => Debug.LogError(MissingNdmf.Message);
    }

    [CustomEditor(typeof(ProportionAdjuster))]
    internal class ProportionAdjusterMissingNdmfEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(MissingNdmf.Message, MessageType.Error);
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }
    }

    internal class MissingNdmfBuildCheck : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => int.MinValue;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (!avatarGameObject.GetComponentInChildren<ProportionAdjuster>(true)) return true;
            Debug.LogError(MissingNdmf.Message + " The build was stopped so the avatar isn't uploaded without its proportions.", avatarGameObject);
            return false;
        }
    }
}
