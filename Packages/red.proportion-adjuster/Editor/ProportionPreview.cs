using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace Red.Proportions
{
    // Scene-view preview for ProportionAdjuster. The avatar itself is never touched: it is hidden
    // with scene visibility (editor state, not saved in the scene) and a copy that Unity never
    // saves (HideAndDontSave) is reshaped in its place. The copy is thrown away when the selection
    // leaves the avatar, before play mode, before a VRChat build and before scripts reload.
    [InitializeOnLoad]
    internal static class ProportionPreview
    {
        static ProportionAdjuster source;
        static GameObject original, copy;
        static List<(Transform src, Transform dst)> transforms;
        static List<(SkinnedMeshRenderer src, SkinnedMeshRenderer dst)> renderers;
        static Dictionary<Transform, Transform> toCopy;
        // Objects under the avatar that were already hidden, so ending the preview doesn't show them.
        static List<GameObject> hiddenBefore;
        static readonly List<Transform> proxies = new List<Transform>();

        static ProportionPreview()
        {
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) End(); };
            AssemblyReloadEvents.beforeAssemblyReload += End;
            Selection.selectionChanged += () =>
            {
                if (original && !Selection.gameObjects.Any(g => g.transform.IsChildOf(original.transform))) End();
            };
            Undo.undoRedoPerformed += () => Refresh();
        }

        public static bool IsShowing(ProportionAdjuster a) => copy && source == a;

        public static string Begin(ProportionAdjuster a)
        {
            End();
            var desc = a.GetComponentInParent<VRCAvatarDescriptor>();
            if (!desc) return "Put this on (or under) an avatar root.";

            source = a;
            original = desc.gameObject;
            copy = Object.Instantiate(original, original.transform.parent);
            copy.name = original.name + " (Proportion Preview)";

            // Same hierarchy, so the two lists pair up by index.
            var src = original.GetComponentsInChildren<Transform>(true);
            var dst = copy.GetComponentsInChildren<Transform>(true);
            foreach (var t in dst) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            transforms = src.Zip(dst, (s, d) => (s, d)).ToList();
            toCopy = transforms.ToDictionary(p => p.src, p => p.dst);
            renderers = original.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Zip(copy.GetComponentsInChildren<SkinnedMeshRenderer>(true), (s, d) => (s, d)).ToList();

            var visibility = SceneVisibilityManager.instance;
            hiddenBefore = src.Select(t => t.gameObject).Where(g => visibility.IsHidden(g)).ToList();
            visibility.Hide(original, true);
            foreach (var g in hiddenBefore) visibility.Hide(toCopy[g.transform].gameObject, false);
            visibility.DisablePicking(copy, true);

            return Refresh();
        }

        // Resets the copy to the avatar's current state and reshapes it with the current values.
        public static string Refresh()
        {
            if (!copy || !source || !original) return null;

            foreach (var p in proxies) if (p) Object.DestroyImmediate(p.gameObject);
            proxies.Clear();
            foreach (var (s, d) in transforms)
            {
                d.localPosition = s.localPosition;
                d.localRotation = s.localRotation;
                d.localScale = s.localScale;
            }
            foreach (var (s, d) in renderers)
                d.bones = s.bones.Select(b => b && toCopy.TryGetValue(b, out var c) ? c : b).ToArray();
            copy.GetComponent<VRCAvatarDescriptor>().ViewPosition = original.GetComponent<VRCAvatarDescriptor>().ViewPosition;

            var error = ProportionReshaper.Reshape(source, copy.transform, null, proxies, out _);
            foreach (var p in proxies) p.gameObject.hideFlags = HideFlags.HideAndDontSave;
            SceneView.RepaintAll();
            return error;
        }

        public static void End()
        {
            if (copy) Object.DestroyImmediate(copy);
            if (original)
            {
                var visibility = SceneVisibilityManager.instance;
                visibility.Show(original, true);
                foreach (var g in hiddenBefore ?? new List<GameObject>()) if (g) visibility.Hide(g, false);
            }
            source = null;
            original = copy = null;
            proxies.Clear();
        }
    }

    // Ends the preview before the SDK collects avatars, so the copy is never offered for upload.
    internal class ProportionPreviewBuildHook : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => int.MinValue;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            ProportionPreview.End();
            return true;
        }
    }
}
