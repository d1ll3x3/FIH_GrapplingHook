using EHS;
using UnityEngine;

namespace GrapplingHook
{
    /// <summary>
    /// Resolves the game objects the hook needs.
    ///
    /// PlayerRef.LocalPlayer is deliberately never cached across frames: an IL2CPP wrapper
    /// kept over a respawn points at freed memory and crashes the process.
    /// </summary>
    internal static class GameRefs
    {
        /// <summary>The local player once the game has finished setting it up, or null.</summary>
        public static PlayerRef LocalPlayer
        {
            get
            {
                PlayerRef player = PlayerRef.LocalPlayer;
                return player != null && player.IsFullyInitialized ? player : null;
            }
        }

        /// <summary>Instance id of the current local player, or 0 when there is none.</summary>
        public static int PlayerIdentity
        {
            get
            {
                PlayerRef player = LocalPlayer;
                return player == null ? 0 : player.GetInstanceID();
            }
        }

        /// <summary>The camera the player sees through, the one the hook aims with.</summary>
        public static Camera Camera(PlayerRef player)
        {
            Camera camera = player?.Movement?.camManager?.cam;
            return camera != null ? camera : UnityEngine.Camera.main;
        }

        /// <summary>
        /// True while the game is moving the player on its own terms - menus, respawns, the end
        /// of a run - or the phone's own screen is open and has the keys. A rope pulling at the
        /// same time only fights it.
        ///
        /// Not PlayerRef.IsCursorVisible, despite the name: it is true exactly while the phone
        /// screen is closed and the game is running (it returns !isPhoneOsActive), so it reads
        /// as busy all through normal play.
        /// </summary>
        public static bool IsBusy(PlayerRef player) =>
            GameCompat.IsGameEnded ||
            GameCompat.IsPauseMenuShown ||
            GameCompat.IsGameFrozen ||
            GameCompat.IsBeingRespawned ||
            GameCompat.IsBeingSummoned ||
            GameCompat.IsCustomizationScreenShown ||
            (player.PlayerPhoneOs?.IsPhoneOsActive ?? false);

        /// <summary>
        /// True while one of the level's own attachments has the player: its hooks, ziplines,
        /// swing bars, suck pipes or a cannon shot. Those drive the body themselves (PlayerMovement
        /// swaps air control for its own swing while a level hook holds you), so ours stays out.
        /// </summary>
        public static bool IsAttachedByGame(PlayerRef player)
        {
            PhoneContext context = player.PhoneContext;

            return context != null && (
                context.IsHooked ||
                context.IsZiplineAttached ||
                context.IsSwingBarAttached ||
                context.IsSuckPipeAttached ||
                context.IsFlyingFromCannon);
        }

        /// <summary>Broken phone: it is about to respawn, there is nothing to swing.</summary>
        public static bool IsDestroyed(PlayerRef player) => player.PhoneContext?.IsDestroyed ?? false;
    }
}
