using System;
using EHS;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrapplingHook
{
    /// <summary>
    /// Resident component: aims and fires the hook in Update, swings on it in FixedUpdate and
    /// draws the aiming mark. Needs the IntPtr constructor for IL2CPP interop.
    ///
    /// Holding the key is being hooked. Pressing it arms the hook, and it fires the moment
    /// there is something in range under the aim, so pressing a little early and sweeping the
    /// camera onto a wall works. Being dropped by the game - a menu, a level hook, breaking -
    /// disarms it, so you do not re-attach behind its back while still holding the key.
    ///
    /// Letting go of the rope in the air gives the jump back: the next jump press before you
    /// land jumps from mid-air, through the game's own external jump, so it is the same jump
    /// the ground gives. Only letting go yourself earns it; being dropped by the game does not.
    /// It is gone on landing, where the ground gives the jump back anyway, and on hooking again.
    ///
    /// Achievements are blocked from here for as long as the mod is loaded.
    /// </summary>
    public class GrapplingBehaviour : MonoBehaviour
    {
        public GrapplingBehaviour(IntPtr ptr) : base(ptr) { }

        private static readonly Color AimIdle = new(1f, 1f, 1f, 0.35f);
        private static readonly Color AimReady = new(0.95f, 0.78f, 0.42f, 0.95f);

        private Aim aim;
        private Rope rope;
        private RopeSync sync;
        private BindMenu menu;

        private int sceneHandle;
        private int playerId;
        private bool armed;
        private bool reeling;
        private float pendingScroll;
        private bool showAim;
        private bool airJump;

        private void Awake()
        {
            aim = new Aim();
            rope = new Rope();
            sync = new RopeSync();
            menu = new BindMenu(GrapplingPlugin.Settings);
        }

        private void Update()
        {
            try
            {
                GrapplingSettings settings = GrapplingPlugin.Settings;
                showAim = false;

                AchievementBlocker.Tick();
                WatchForSceneChange();

                PlayerRef player = GameRefs.LocalPlayer;
                WatchForNewPlayer(player);

                // The menu has the input while it is open: the hook bind pressed while rebinding
                // is a bind, not a hook.
                menu.Tick();

                if (player == null)
                {
                    aim.Clear();
                    return;
                }

                if (menu.IsOpen || !settings.Enabled.Value || GameRefs.IsBusy(player) ||
                    GameRefs.IsAttachedByGame(player) || GameRefs.IsDestroyed(player))
                {
                    Release(player);
                    armed = false;
                    airJump = false;
                    aim.Clear();
                    return;
                }

                if (settings.Hook.WasPressedThisFrame())
                {
                    armed = true;
                }

                if (!settings.Hook.IsHeld())
                {
                    if (rope.Attached)
                    {
                        airJump = true;
                    }

                    armed = false;
                    Release(player);
                }

                if (rope.Attached)
                {
                    aim.Clear();
                    reeling = settings.Reel.IsHeld();
                    pendingScroll += settings.Shorten.Notches() - settings.Lengthen.Notches();

                    if (settings.SyncRope.Value)
                    {
                        sync.Show(player, rope.Anchor);
                    }
                    else
                    {
                        sync.Hide(player);
                    }

                    return;
                }

                aim.Tick(settings, player);
                showAim = settings.ShowCrosshair.Value;

                if (armed && aim.HasTarget)
                {
                    Rigidbody rb = player.Rb;

                    if (rb != null)
                    {
                        rope.Attach(settings, aim.Point, aim.Collider, rb.worldCenterOfMass);
                        airJump = false;
                        reeling = false;
                        pendingScroll = 0f;
                    }
                }
            }
            catch (Exception ex)
            {
                GrapplingPlugin.Logger.LogError($"Error in Update: {ex}");
            }
        }

        private void FixedUpdate()
        {
            try
            {
                GrapplingSettings settings = GrapplingPlugin.Settings;
                PlayerRef player = GameRefs.LocalPlayer;

                if (player == null || player.GetInstanceID() != playerId)
                {
                    return;
                }

                if (airJump)
                {
                    TickAirJump(player);
                }

                if (!rope.Attached)
                {
                    return;
                }

                Rigidbody rb = player.Rb;

                if (rb == null || rb.isKinematic || !rope.TrackAnchor())
                {
                    Release(player);
                    return;
                }

                // Twice the range can only be a teleport - a checkpoint, a beacon - and a rope
                // that survived one would haul you back across the level.
                if (Vector3.Distance(rb.worldCenterOfMass, rope.Anchor) > settings.MaxRange.Value * 2f)
                {
                    Release(player);
                    armed = false;
                    return;
                }

                rope.FixedTick(settings, rb, reeling, pendingScroll, Pump(player));
                pendingScroll = 0f;
            }
            catch (Exception ex)
            {
                GrapplingPlugin.Logger.LogError($"Error in FixedUpdate: {ex}");
            }
        }

        private void OnGUI()
        {
            try
            {
                menu.Draw();

                if (!showAim)
                {
                    return;
                }

                Color color = aim.HasTarget ? AimReady : AimIdle;
                float x = Screen.width * 0.5f;
                float y = Screen.height * 0.5f;

                Gui.Fill(new Rect(x - 9f, y - 1f, 6f, 2f), color);
                Gui.Fill(new Rect(x + 3f, y - 1f, 6f, 2f), color);
                Gui.Fill(new Rect(x - 1f, y - 9f, 2f, 6f), color);
                Gui.Fill(new Rect(x - 1f, y + 3f, 2f, 6f), color);

                if (aim.HasTarget)
                {
                    Gui.Fill(new Rect(x - 2f, y - 2f, 4f, 4f), color);
                }
            }
            catch (Exception ex)
            {
                GrapplingPlugin.Logger.LogError($"Error drawing the aim and the menu: {ex}");
            }
        }

        private void OnDestroy()
        {
            try
            {
                menu?.Close();
                GrapplingPlugin.Settings?.Save();
            }
            catch (Exception ex)
            {
                GrapplingPlugin.Logger.LogError($"Error shutting down: {ex}");
            }
        }

        /// <summary>
        /// Spends the jump that letting go of the rope gave, on the game's own jump press. Read
        /// in FixedUpdate from the fixed-update mask, the same one the game's jump reads, so a
        /// press is seen exactly once. The game itself never jumps here: it only jumps off a
        /// surface, and touching one ends the air jump first.
        /// </summary>
        private void TickAirJump(PlayerRef player)
        {
            GroundContact ground = player.GroundContact;

            if (ground != null && ground.IsTouching())
            {
                airJump = false;
                return;
            }

            PlayerInputHandler input = player.InputHandler;

            if (input == null || !input.WasPressed(PlayerButtons.ButtonType.Jump, true))
            {
                return;
            }

            airJump = false;
            player.MovementJump?.TryActivateExternalJump();
        }

        /// <summary>
        /// Where the movement keys point, the way the game reads them: relative to the camera's
        /// yaw (CameraManager.AlignByYaw flattens the camera's forward and right the same way).
        /// </summary>
        private static Vector3 Pump(PlayerRef player)
        {
            PlayerInputHandler input = player.InputHandler;
            Camera camera = GameRefs.Camera(player);

            if (input == null || camera == null)
            {
                return Vector3.zero;
            }

            Vector2 move = input.MoveInput;
            Transform view = camera.transform;

            Vector3 forward = view.forward;
            forward.y = 0f;
            Vector3 right = view.right;
            right.y = 0f;

            return Vector3.ClampMagnitude(forward.normalized * move.y + right.normalized * move.x, 1f);
        }

        private void Release(PlayerRef player)
        {
            if (rope.Attached)
            {
                rope.Detach();
            }

            sync.Hide(player);
        }

        /// <summary>
        /// A respawn hands us a new player object. The rope goes without telling the network:
        /// the old player's rope visual was destroyed with it.
        /// </summary>
        private void WatchForNewPlayer(PlayerRef player)
        {
            int id = player == null ? 0 : player.GetInstanceID();

            if (id == playerId)
            {
                return;
            }

            playerId = id;
            rope.Detach();
            sync.Forget();
            armed = false;
            airJump = false;
        }

        private void WatchForSceneChange()
        {
            int handle = SceneManager.GetActiveScene().handle;

            if (handle == sceneHandle)
            {
                return;
            }

            sceneHandle = handle;
            rope.Detach();
            sync.Forget();
            aim.Clear();
            armed = false;
            airJump = false;
        }
    }
}
