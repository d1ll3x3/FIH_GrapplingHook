using BepInEx.Configuration;
using UnityEngine;

namespace GrapplingHook
{
    /// <summary>
    /// One bind in the config file, parsed once and re-parsed whenever it changes - from the
    /// menu, or from the file being edited while the game runs.
    /// </summary>
    internal sealed class Bind
    {
        private readonly BindDevice[] devices;

        public Bind(ConfigEntry<string> entry, params BindDevice[] devices)
        {
            Entry = entry;
            this.devices = devices;

            Parse();
            entry.SettingChanged += (_, _) => Parse();
        }

        public ConfigEntry<string> Entry { get; }

        public Binding Value { get; private set; }

        /// <summary>Whether this slot takes input from that device. None always fits: it is an empty slot.</summary>
        public bool Accepts(Binding binding) =>
            binding.Device == BindDevice.None || System.Array.IndexOf(devices, binding.Device) >= 0;

        public void Set(Binding binding) => Entry.Value = binding.ToString();

        public void Reset() => Entry.Value = (string)Entry.DefaultValue;

        private void Parse()
        {
            if (Binding.TryParse(Entry.Value, out Binding parsed) && Accepts(parsed))
            {
                Value = parsed;
                return;
            }

            Binding.TryParse((string)Entry.DefaultValue, out parsed);
            Value = parsed;

            GrapplingPlugin.Logger.LogWarning(
                $"{Entry.Definition.Key} = '{Entry.Value}' is not a bind this slot takes, using {parsed.Label}. " +
                "Write it as Key:Q, Mouse:Right, Mouse:WheelUp, Pad:RightTrigger or None.");
        }
    }

    /// <summary>
    /// Something the player does, with a keyboard-and-mouse bind and a gamepad bind. Either
    /// one works at any time, so switching between the two needs no setting.
    /// </summary>
    internal sealed class BindAction
    {
        /// <summary>
        /// Wheel notches a held button is worth per second, for the rope length. About what a
        /// steady spin of the wheel gives, so the two feel alike.
        /// </summary>
        private const float HeldNotchesPerSecond = 8f;

        public BindAction(string label, Bind keyboardMouse, Bind gamepad)
        {
            Label = label;
            KeyboardMouse = keyboardMouse;
            Gamepad = gamepad;
        }

        public string Label { get; }
        public Bind KeyboardMouse { get; }
        public Bind Gamepad { get; }

        public bool IsHeld() => InputReader.IsHeld(KeyboardMouse.Value) || InputReader.IsHeld(Gamepad.Value);

        public bool WasPressedThisFrame() =>
            InputReader.WasPressedThisFrame(KeyboardMouse.Value) || InputReader.WasPressedThisFrame(Gamepad.Value);

        /// <summary>How much of the action happened this frame, in wheel notches: turned on a wheel, or held on a button.</summary>
        public float Notches() => Notches(KeyboardMouse.Value) + Notches(Gamepad.Value);

        private static float Notches(Binding binding)
        {
            if (binding.IsWheel)
            {
                return InputReader.Notches(binding);
            }

            return InputReader.IsHeld(binding) ? HeldNotchesPerSecond * Time.deltaTime : 0f;
        }
    }
}
