using BepInEx.Configuration;
using UnityEngine;

namespace GrapplingHook
{
    /// <summary>
    /// Everything the hook can be told to do, stored in GrapplingHook.cfg next to the dll.
    /// All of it is local: the swing is your own rigidbody, and the only thing the lobby sees
    /// is the rope, through the game's own network sync.
    /// </summary>
    internal sealed class GrapplingSettings
    {
        private readonly ConfigFile file;

        public ConfigEntry<bool> Enabled { get; }

        public BindAction Hook { get; }
        public BindAction Reel { get; }
        public BindAction Shorten { get; }
        public BindAction Lengthen { get; }
        public BindAction Menu { get; }

        /// <summary>Every action, in the order the menu lists them.</summary>
        public BindAction[] Actions { get; }

        public ConfigEntry<float> MaxRange { get; }
        public ConfigEntry<float> MinLength { get; }
        public ConfigEntry<float> ReelSpeed { get; }
        public ConfigEntry<float> ScrollStep { get; }
        public ConfigEntry<float> SwingForce { get; }
        public ConfigEntry<float> Correction { get; }

        public ConfigEntry<bool> SyncRope { get; }
        public ConfigEntry<bool> ShowCrosshair { get; }

        /// <summary>Which defaults this file was written with, so a change of default can reach it.</summary>
        private ConfigEntry<int> ConfigVersion { get; }

        public GrapplingSettings(ConfigFile configFile)
        {
            file = configFile;

            Enabled = file.Bind("General", "Enabled", true,
                "Whether the hook works at all.");

            // Bound by name in two slots each: KeyboardMouse takes keys, mouse buttons and the
            // wheel, Gamepad takes controller buttons. The keyboard slots keep the names they had
            // when they held keys only, so the files written then still load their binds.
            Hook = Action("Hook (hold)", "HookKey", "Key:Q", "HookPad", "Pad:RightTrigger",
                "Fires the hook at the middle of the screen. Hold it to stay hooked, let go to drop " +
                "off with whatever speed you have.");
            Reel = Action("Reel in (hold)", "ReelKey", "Key:F", "ReelPad", "Pad:LeftTrigger",
                "Hold it while hooked to wind the rope in and be pulled towards the point.");
            Shorten = Action("Shorten rope", "ShortenKey", "Mouse:WheelUp", "ShortenPad", "Pad:DpadUp",
                "Winds the rope in by ScrollStep per wheel notch, or steadily while a button is held.");
            Lengthen = Action("Lengthen rope", "LengthenKey", "Mouse:WheelDown", "LengthenPad", "Pad:DpadDown",
                "Lets the rope out by ScrollStep per wheel notch, or steadily while a button is held.");
            Menu = Action("This menu", "MenuKey", "Key:F8", "MenuPad", "Pad:RightStick",
                "Opens the bind menu, where every bind above can be changed in game.");

            Actions = new[] { Hook, Reel, Shorten, Lengthen, Menu };

            MaxRange = file.Bind("Rope", "MaxRange", 60f,
                "Furthest a hook can land, in metres from you.");
            MinLength = file.Bind("Rope", "MinLength", 1.5f,
                "Shortest the rope can be wound to, in metres.");
            ReelSpeed = file.Bind("Rope", "ReelSpeed", 4.5f,
                "How fast holding the reel bind pulls you in, in m/s.");
            ScrollStep = file.Bind("Rope", "ScrollStep", 0.375f,
                "Metres of rope one wheel notch winds in or lets out. A held button winds about " +
                "eight notches a second.");
            SwingForce = file.Bind("Rope", "SwingForce", 14f,
                "How hard the movement keys pump the swing, in m/s². Only the part across the " +
                "rope counts, so it pushes the swing and never stretches it.");
            Correction = file.Bind("Rope", "Correction", 0.25f,
                "Fraction of any overstretch taken back each physics step. Higher holds the " +
                "length tighter, too high and the rope jitters.");

            SyncRope = file.Bind("Display", "SyncRope", true,
                "Show the rope through the game's own hook rope, which the game syncs to the " +
                "whole lobby. Off, nobody sees the rope, you included.");
            ShowCrosshair = file.Bind("Display", "ShowCrosshair", true,
                "Draw an aiming mark in the middle of the screen, lit when something is in range.");

            ConfigVersion = file.Bind("Internal", "ConfigVersion", 0,
                "Which defaults this file was written with. Not a setting; leave it alone.");
        }

        private BindAction Action(string label, string keyName, string keyDefault, string padName,
            string padDefault, string description)
        {
            const string Format = " Write it as Key:Q, Mouse:Left / Right / Middle / Back / Forward / " +
                                  "WheelUp / WheelDown, or None.";
            const string PadFormat = " Write it as Pad: South (A), East (B), West (X), North (Y), " +
                                     "LeftShoulder, RightShoulder, LeftTrigger, RightTrigger, DpadUp, DpadDown, " +
                                     "DpadLeft, DpadRight, LeftStick, RightStick, Start, Select, or None.";

            var keyboardMouse = new Bind(file.Bind("Binds", keyName, keyDefault, description + Format),
                BindDevice.Keyboard, BindDevice.Mouse);
            var gamepad = new Bind(file.Bind("Binds", padName, padDefault, description + PadFormat),
                BindDevice.Gamepad);

            return new BindAction(label, keyboardMouse, gamepad);
        }

        public void Validate()
        {
            Migrate();

            MaxRange.Value = Mathf.Clamp(MaxRange.Value, 5f, 500f);
            MinLength.Value = Mathf.Clamp(MinLength.Value, 0.5f, 20f);
            ReelSpeed.Value = Mathf.Clamp(ReelSpeed.Value, 1f, 100f);
            ScrollStep.Value = Mathf.Clamp(ScrollStep.Value, 0.1f, 20f);
            SwingForce.Value = Mathf.Clamp(SwingForce.Value, 0f, 100f);

            // Past 1 the correction overshoots the length every step, which is the jitter.
            Correction.Value = Mathf.Clamp(Correction.Value, 0f, 1f);
        }

        /// <summary>
        /// A changed default only reaches files that were still on the old one: a value that was
        /// tuned by hand is left alone.
        /// </summary>
        private void Migrate()
        {
            const int Current = 1;

            if (ConfigVersion.Value >= Current)
            {
                return;
            }

            // Version 1: reeling and the wheel were four times too fast to control a swing with.
            ResetIf(ReelSpeed, 18f);
            ResetIf(ScrollStep, 1.5f);

            ConfigVersion.Value = Current;
        }

        private static void ResetIf(ConfigEntry<float> entry, float oldDefault)
        {
            if (Mathf.Approximately(entry.Value, oldDefault))
            {
                entry.Value = (float)entry.DefaultValue;
                GrapplingPlugin.Logger.LogInfo($"{entry.Definition.Key} moved to the new default, {entry.Value:0.###}.");
            }
        }

        public void Save()
        {
            try
            {
                file.Save();
            }
            catch (System.Exception ex)
            {
                GrapplingPlugin.Logger.LogError($"Could not save the settings: {ex.Message}");
            }
        }

        public string Summary() =>
            $"mod={(Enabled.Value ? "on" : "off")} hook={Hook.KeyboardMouse.Value.Label}/{Hook.Gamepad.Value.Label} " +
            $"menu={Menu.KeyboardMouse.Value.Label}/{Menu.Gamepad.Value.Label} " +
            $"range={MaxRange.Value:0.#}m sync={(SyncRope.Value ? "on" : "off")}";
    }
}
