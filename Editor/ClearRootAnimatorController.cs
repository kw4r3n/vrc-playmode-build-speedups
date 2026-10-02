using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VrcPlayModeBuildSpeedups
{
    // VRChat (and Gesture Manager in play mode) ignore the root Animator's controller, but VRCFury points
    // it at its generated FX when it is set, and NDMF then clones that FX a second time as a separate
    // controller after VRCFury. Clearing it on the avatar being built skips that redundant clone.
    // Only the build clone is touched; the prefab keeps its preview controller. Toggle from Tools.
    public class ClearRootAnimatorController : IVRCSDKPreprocessAvatarCallback
    {
        private const string PrefKey = "VrcPlayModeBuildSpeedups.ClearRootAnimatorController";
        private const string MenuPath = "Tools/Clear Root Animator Controller On Build";

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        // Before NDMF's preprocess hook (-11000) so NDMF never starts tracking the controller.
        public int callbackOrder => -11100;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (!Enabled) return true;
            var animator = avatarGameObject.GetComponent<Animator>();
            if (animator != null) animator.runtimeAnimatorController = null;
            return true;
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
