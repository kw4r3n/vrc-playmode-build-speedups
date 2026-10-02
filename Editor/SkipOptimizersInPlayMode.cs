using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VrcPlayModeBuildSpeedups
{
    // Play-mode test builds don't need final-quality optimization, and AAO Trace and Optimize plus
    // Avatar Compressor can add more than ten seconds to every build. Strip them from the avatar being
    // built when entering play mode; uploads (edit mode) always keep them. With them gone,
    // SkipNdmfOptimizingInPlayMode can also skip NDMF's optimizing phase. Toggle from Tools.
    public class SkipOptimizersInPlayMode : IVRCSDKPreprocessAvatarCallback
    {
        private const string PrefKey = "VrcPlayModeBuildSpeedups.SkipOptimizersInPlayMode";
        private const string MenuPath = "Tools/Kw4r3n/VRC Play Mode Build Speedups/Skip Optimizers In Play Mode";

        private static readonly string[] OptimizerTypes =
        {
            "Anatawa12.AvatarOptimizer.TraceAndOptimize",
            "dev.limitex.avatar.compressor.TextureCompressor",
        };

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        // Before NDMF's preprocess hook (-11000), which is where these components are consumed.
        public int callbackOrder => -11100;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (!Enabled || !EditorApplication.isPlayingOrWillChangePlaymode) return true;

            var components = avatarGameObject.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null).ToList();
            var stripped = components
                .Where(c => OptimizerTypes.Contains(c.GetType().FullName) || IsInvisibleMeshRemoval(c))
                .ToList();
            foreach (var component in stripped) Object.DestroyImmediate(component);

            if (stripped.Count > 0)
                Debug.Log($"[SkipOptimizersInPlayMode] Skipped {string.Join(", ", stripped.Select(c => c.GetType().Name))} on {avatarGameObject.name}");

            // Any AAO component left keeps the whole AAO plugin running, which costs an extra animator rebuild.
            var remainingAao = components.Where(c => c != null && c.GetType().Namespace?.StartsWith("Anatawa12.AvatarOptimizer") == true).ToList();
            if (remainingAao.Count > 0)
                Debug.Log($"[SkipOptimizersInPlayMode] AAO stays active because of {string.Join(", ", remainingAao.Select(c => $"{c.GetType().Name} on {c.name}"))}");
            return true;
        }

        // AAO RemoveMeshByBlendShape only deletes what its blend shapes already hide when they sit at 100,
        // so skipping it in play mode doesn't change how the avatar looks.
        private static bool IsInvisibleMeshRemoval(MonoBehaviour component)
        {
            if (component.GetType().FullName != "Anatawa12.AvatarOptimizer.RemoveMeshByBlendShape") return false;
            var renderer = component.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null || renderer.sharedMesh == null) return false;

            var shapes = new SerializedObject(component).FindProperty("shapeKeysSet.mainSet");
            if (shapes == null) return false;
            for (var i = 0; i < shapes.arraySize; i++)
            {
                var index = renderer.sharedMesh.GetBlendShapeIndex(shapes.GetArrayElementAtIndex(i).stringValue);
                if (index < 0 || renderer.GetBlendShapeWeight(index) < 99.9f) return false;
            }
            return true;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Debug.Log($"[SkipOptimizersInPlayMode] {(Enabled ? "enabled" : "disabled")}");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
