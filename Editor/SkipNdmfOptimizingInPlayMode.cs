using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace VrcPlayModeBuildSpeedups
{
    // After VRCFury replaces every playable-layer controller, NDMF's optimizing phase has to clone all of
    // them again before its first pass, which costs seconds per play-mode build. When the avatar carries
    // no components from a plugin that does real work in that phase, nothing left in it changes how the
    // avatar behaves in play mode (Modular Avatar only garbage-collects unused objects), so finish the NDMF
    // build here instead. Avatars with such components still go through it. Uploads always run it.
    // Toggle from Tools.
    public class SkipNdmfOptimizingInPlayMode : IVRCSDKPreprocessAvatarCallback
    {
        private const string PrefKey = "VrcPlayModeBuildSpeedups.SkipNdmfOptimizingInPlayMode";
        private const string MenuPath = "Tools/Kw4r3n/VRC Play Mode Build Speedups/Skip NDMF Optimizing In Play Mode";

        // Plugins with optimizing-phase passes that act on their own components.
        private static readonly string[] OptimizingPluginNamespaces =
        {
            "Anatawa12.AvatarOptimizer",
            "dev.limitex.avatar.compressor",
            "net.rs64",
            "jp.lilxyzw",
            "KRT.VRCQuestTools",
        };

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        // After VRCFury (-10000) and ThiccWater (-9000), just before NDMF's optimize hook (-1025).
        public int callbackOrder => -1030;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (!Enabled || !EditorApplication.isPlayingOrWillChangePlaymode) return true;

            var blocker = avatarGameObject.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(c => c != null && OptimizingPluginNamespaces.Any(n => c.GetType().Namespace?.StartsWith(n) == true));
            if (blocker != null)
            {
                Debug.Log($"[SkipNdmfOptimizingInPlayMode] Running the optimizing phase because of {blocker.GetType().Name} on {blocker.name}");
                return true;
            }

            var holderType = Type.GetType("nadena.dev.ndmf.VRChat.ContextHolder, nadena.dev.ndmf.vrchat");
            var contextField = holderType?.GetField("context", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var holder = holderType != null ? avatarGameObject.GetComponent(holderType) : null;
            var context = holder != null ? contextField?.GetValue(holder) : null;
            var finish = context?.GetType().GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var successful = context?.GetType().GetProperty("Successful");
            if (finish == null || successful == null)
            {
                Debug.LogWarning("[SkipNdmfOptimizingInPlayMode] NDMF internals changed; running the optimizing phase");
                return true;
            }

            // The same cleanup BuildFrameworkOptimizeHook does after the optimizing phase; it then finds no
            // holder and returns early.
            finish.Invoke(context, null);
            Object.DestroyImmediate(holder);
            Debug.Log($"[SkipNdmfOptimizingInPlayMode] Skipped the NDMF optimizing phase on {avatarGameObject.name}");
            return (bool)successful.GetValue(context);
        }

        [MenuItem(MenuPath)]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
