using System;
using EHS;
using EHS.Achievements;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GrapplingHook
{
    /// <summary>
    /// No achievement or achievement stat can be earned while this mod is loaded: a hook that
    /// swings you past a level is an advantage. Same approach as the Official Speedrun Mod.
    ///
    /// The game has the switch for it: Crowd Control runs (a save's useStreamerEffects) block
    /// achievements through AchievementsCore.SetBlockingState, with an allowlist. Blocking with
    /// no allowlist (null sets) makes TryUnlock, TrySetProgress and the stat setters refuse
    /// every id.
    ///
    /// AchievementsManager.ApplyBlockingState recomputes the state from the save whenever a
    /// world is loaded, restarted or joined, which lifts the block, so it is checked every frame
    /// and put back. Polled instead of Harmony-patched: no game method is detoured.
    /// </summary>
    internal static class AchievementBlocker
    {
        /// <summary>How often to look for the AchievementsManager while it doesn't exist yet, in seconds.</summary>
        private const float SearchInterval = 1f;

        private static AchievementsManager manager;
        private static float nextSearch;
        private static bool loggedBlock;
        private static bool loggedFailure;

        public static void Tick()
        {
            try
            {
                if (manager == null)
                {
                    if (Time.unscaledTime < nextSearch)
                    {
                        return;
                    }

                    nextSearch = Time.unscaledTime + SearchInterval;
                    manager = Object.FindObjectOfType<AchievementsManager>();

                    if (manager == null)
                    {
                        return;
                    }
                }

                AchievementsCore core = manager.Core;

                if (core == null ||
                    (core.IsBlockingActive && core.allowedWhileBlocked == null && core.allowedStatsWhileBlocked == null))
                {
                    return;
                }

                core.SetBlockingState(true, null, null);

                if (!loggedBlock)
                {
                    GrapplingPlugin.Logger.LogInfo("Achievements blocked: they can't be earned while this mod is loaded.");
                    loggedBlock = true;
                }
            }
            catch (Exception ex)
            {
                // Retried at the search interval rather than every frame, and logged once: a
                // block that keeps failing would otherwise bury the log.
                manager = null;
                nextSearch = Time.unscaledTime + SearchInterval;

                if (!loggedFailure)
                {
                    loggedFailure = true;
                    GrapplingPlugin.Logger.LogError($"Could not block achievements, retrying every {SearchInterval:0}s: {ex}");
                }
            }
        }
    }
}
