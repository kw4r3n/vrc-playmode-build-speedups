using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using Debug = UnityEngine.Debug;

namespace VrcPlayModeBuildSpeedups
{
    // Logs how long entering play mode took, split by the avatar build stages that usually dominate it
    // (NDMF's two hooks, VRCFury, and everything else), so the effect of a tool or setting can be measured.
    // Off by default. Toggle from Tools.
    [InitializeOnLoad]
    public static class PlayModeBuildTimer
    {
        private const string PrefKey = "VrcPlayModeBuildSpeedups.PlayModeBuildTimer";
        private const string MenuPath = "Tools/Kw4r3n/VRC Play Mode Build Speedups/Log Play Mode Build Time";

        // Marks sit between the callback orders of the stages they separate:
        // NDMF preprocess -11000, VRCFury -10000, NDMF optimize -1025.
        internal static readonly (int order, string stage)[] Marks =
        {
            (-11001, "start"),
            (-10001, "NDMF"),
            (-9999, "VRCFury"),
            (-1026, "other"),
            (-1024, "NDMF optimizing"),
            (int.MaxValue, "after"),
        };

        private static readonly Stopwatch Clock = new Stopwatch();
        private static readonly Dictionary<string, long> Durations = new Dictionary<string, long>();
        private static long buildStart = -1, lastMark;

        internal static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static PlayModeBuildTimer()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!Enabled) return;
                if (state == PlayModeStateChange.ExitingEditMode)
                {
                    Clock.Restart();
                    Durations.Clear();
                    buildStart = -1;
                }
                else if (state == PlayModeStateChange.EnteredPlayMode && Clock.IsRunning)
                {
                    Clock.Stop();
                    var stages = string.Join(", ", Marks.Select(m => m.stage).Where(Durations.ContainsKey)
                        .Select(s => $"{s} {Seconds(Durations[s])}"));
                    var build = buildStart >= 0 ? $" (avatar build {Seconds(lastMark - buildStart)}: {stages})" : "";
                    Debug.Log($"[PlayModeBuildTimer] Entered play mode in {Seconds(Clock.ElapsedMilliseconds)}{build}");
                }
            };
        }

        internal static void Mark(int order)
        {
            if (!Enabled || !Clock.IsRunning || !EditorApplication.isPlayingOrWillChangePlaymode) return;
            var now = Clock.ElapsedMilliseconds;
            var index = System.Array.FindIndex(Marks, m => m.order == order);
            if (index == 0)
            {
                // The first mark only starts the avatar build; earlier callbacks are not timed
                buildStart = lastMark = now;
                return;
            }
            if (buildStart < 0) return;
            var stage = Marks[index].stage;
            Durations[stage] = (Durations.TryGetValue(stage, out var d) ? d : 0) + now - lastMark;
            lastMark = now;
        }

        private static string Seconds(long ms) => $"{ms / 1000f:0.0}s";

        [MenuItem(MenuPath)]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }

    public abstract class PlayModeBuildTimerMark : IVRCSDKPreprocessAvatarCallback
    {
        public abstract int callbackOrder { get; }

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            PlayModeBuildTimer.Mark(callbackOrder);
            return true;
        }
    }

    public class PlayModeBuildTimerMarkBeforeNdmf : PlayModeBuildTimerMark { public override int callbackOrder => -11001; }
    public class PlayModeBuildTimerMarkAfterNdmf : PlayModeBuildTimerMark { public override int callbackOrder => -10001; }
    public class PlayModeBuildTimerMarkAfterVrcfury : PlayModeBuildTimerMark { public override int callbackOrder => -9999; }
    public class PlayModeBuildTimerMarkBeforeOptimizing : PlayModeBuildTimerMark { public override int callbackOrder => -1026; }
    public class PlayModeBuildTimerMarkAfterOptimizing : PlayModeBuildTimerMark { public override int callbackOrder => -1024; }
    public class PlayModeBuildTimerMarkEnd : PlayModeBuildTimerMark { public override int callbackOrder => int.MaxValue; }
}
