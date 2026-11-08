using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GrapplingHook
{
    internal enum BindDevice
    {
        None,
        Keyboard,
        Mouse,
        Gamepad,
    }

    internal enum MouseInput
    {
        Left,
        Right,
        Middle,
        Back,
        Forward,
        WheelUp,
        WheelDown,
    }

    /// <summary>
    /// One input a bind can be: a key, a mouse button or wheel direction, or a gamepad button.
    ///
    /// Stored in the config as "Key:Q", "Mouse:Right", "Pad:RightTrigger" or "None". A bare
    /// key name ("Q") is read as a key too, which is how the files written when the binds were
    /// keys only keep working.
    /// </summary>
    internal readonly struct Binding
    {
        /// <summary>
        /// The gamepad buttons a bind can use, with the name stored in the config and the one
        /// shown in the menu. A list rather than Enum.GetValues: GamepadButton gives the face
        /// buttons several names (South, A and Cross are one value), so its names are ambiguous.
        /// </summary>
        public static readonly (GamepadButton Button, string Name, string Label)[] PadButtons =
        {
            (GamepadButton.South, "South", "A"),
            (GamepadButton.East, "East", "B"),
            (GamepadButton.West, "West", "X"),
            (GamepadButton.North, "North", "Y"),
            (GamepadButton.LeftShoulder, "LeftShoulder", "LB"),
            (GamepadButton.RightShoulder, "RightShoulder", "RB"),
            (GamepadButton.LeftTrigger, "LeftTrigger", "LT"),
            (GamepadButton.RightTrigger, "RightTrigger", "RT"),
            (GamepadButton.DpadUp, "DpadUp", "D-pad up"),
            (GamepadButton.DpadDown, "DpadDown", "D-pad down"),
            (GamepadButton.DpadLeft, "DpadLeft", "D-pad left"),
            (GamepadButton.DpadRight, "DpadRight", "D-pad right"),
            (GamepadButton.LeftStick, "LeftStick", "L3"),
            (GamepadButton.RightStick, "RightStick", "R3"),
            (GamepadButton.Start, "Start", "Start"),
            (GamepadButton.Select, "Select", "Select"),
        };

        private Binding(BindDevice device, Key key, MouseInput mouse, GamepadButton pad)
        {
            Device = device;
            Key = key;
            Mouse = mouse;
            Pad = pad;
        }

        public BindDevice Device { get; }
        public Key Key { get; }
        public MouseInput Mouse { get; }
        public GamepadButton Pad { get; }

        public static Binding None => default;

        public static Binding OfKey(Key key) => new(BindDevice.Keyboard, key, default, default);

        public static Binding OfMouse(MouseInput mouse) => new(BindDevice.Mouse, Key.None, mouse, default);

        public static Binding OfPad(GamepadButton pad) => new(BindDevice.Gamepad, Key.None, default, pad);

        public bool IsWheel => Device == BindDevice.Mouse && Mouse is MouseInput.WheelUp or MouseInput.WheelDown;

        /// <summary>What the menu shows.</summary>
        public string Label => Device switch
        {
            BindDevice.Keyboard => Key.ToString(),
            BindDevice.Mouse => Mouse switch
            {
                MouseInput.WheelUp => "Wheel up",
                MouseInput.WheelDown => "Wheel down",
                _ => $"Mouse {Mouse}",
            },
            BindDevice.Gamepad => PadEntry(Pad)?.Label ?? Pad.ToString(),
            _ => "-",
        };

        /// <summary>What the config file stores.</summary>
        public override string ToString() => Device switch
        {
            BindDevice.Keyboard => $"Key:{Key}",
            BindDevice.Mouse => $"Mouse:{Mouse}",
            BindDevice.Gamepad => $"Pad:{PadEntry(Pad)?.Name ?? Pad.ToString()}",
            _ => "None",
        };

        public static bool TryParse(string text, out Binding binding)
        {
            binding = None;
            text = text?.Trim() ?? string.Empty;

            if (text.Length == 0 || text.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int colon = text.IndexOf(':');
            string device = colon < 0 ? "Key" : text.Substring(0, colon).Trim();
            string name = colon < 0 ? text : text.Substring(colon + 1).Trim();

            if (device.Equals("Key", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse(name, true, out Key key) && key != Key.None)
                {
                    binding = OfKey(key);
                    return true;
                }

                return false;
            }

            if (device.Equals("Mouse", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse(name, true, out MouseInput mouse))
                {
                    binding = OfMouse(mouse);
                    return true;
                }

                return false;
            }

            if (device.Equals("Pad", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var entry in PadButtons)
                {
                    if (entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                        entry.Label.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        binding = OfPad(entry.Button);
                        return true;
                    }
                }
            }

            return false;
        }

        private static (GamepadButton Button, string Name, string Label)? PadEntry(GamepadButton pad)
        {
            foreach (var entry in PadButtons)
            {
                if (entry.Button == pad)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
