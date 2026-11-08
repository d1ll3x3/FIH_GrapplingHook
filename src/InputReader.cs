using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace GrapplingHook
{
    /// <summary>
    /// Reads a Binding from whichever device it is on.
    ///
    /// The mouse goes through the legacy Input class, which the game leaves enabled next to the
    /// Input System (the Map Editor reads it the same way). Keys and gamepad buttons go through
    /// the Input System. It reads physical key positions on a US layout, so a key bind is a
    /// position on the keyboard and not the letter printed on the cap.
    /// </summary>
    internal static class InputReader
    {
        // BepInEx's interop does not expose the Keyboard[Key] indexer, so the control is
        // resolved through the property Unity generates for each key, and then cached. The
        // Gamepad[GamepadButton] indexer is there and is used directly.
        private static readonly Dictionary<Key, PropertyInfo> Properties = new();

        private const float RepeatDelay = 0.4f;
        private const float RepeatInterval = 0.06f;

        /// <summary>Mouse buttons in legacy Input's numbering, 0 to 4.</summary>
        private static readonly MouseInput[] MouseButtons =
        {
            MouseInput.Left, MouseInput.Right, MouseInput.Middle, MouseInput.Back, MouseInput.Forward,
        };

        private static Key[] allKeys;

        /// <summary>Wheel notches this frame, up positive.</summary>
        public static float Scroll => Input.mouseScrollDelta.y;

        public static bool IsHeld(Binding binding) => binding.Device switch
        {
            BindDevice.Keyboard => KeyControlOf(binding.Key)?.isPressed ?? false,
            BindDevice.Mouse => binding.Mouse switch
            {
                MouseInput.WheelUp => Scroll > 0f,
                MouseInput.WheelDown => Scroll < 0f,
                _ => Input.GetMouseButton((int)binding.Mouse),
            },
            BindDevice.Gamepad => PadControlOf(binding.Pad)?.isPressed ?? false,
            _ => false,
        };

        public static bool WasPressedThisFrame(Binding binding) => binding.Device switch
        {
            BindDevice.Keyboard => KeyControlOf(binding.Key)?.wasPressedThisFrame ?? false,
            // A wheel notch has no hold: the frame it turns is both the press and all of it.
            BindDevice.Mouse when binding.IsWheel => IsHeld(binding),
            BindDevice.Mouse => Input.GetMouseButtonDown((int)binding.Mouse),
            BindDevice.Gamepad => PadControlOf(binding.Pad)?.wasPressedThisFrame ?? false,
            _ => false,
        };

        /// <summary>Wheel notches this frame in the binding's direction, 0 for anything that is not a wheel.</summary>
        public static float Notches(Binding binding)
        {
            if (!binding.IsWheel)
            {
                return 0f;
            }

            return binding.Mouse == MouseInput.WheelUp ? Mathf.Max(0f, Scroll) : Mathf.Max(0f, -Scroll);
        }

        public static bool WasPressedThisFrame(Key key) => WasPressedThisFrame(Binding.OfKey(key));

        public static bool WasPressedThisFrame(GamepadButton button) => WasPressedThisFrame(Binding.OfPad(button));

        /// <summary>
        /// True when either the key or the pad button repeats: fires once immediately, then
        /// steadily while held. Used by the menu so the selection can be run up and down.
        /// </summary>
        public static bool WasPressedOrRepeating(Key key, GamepadButton button, ref float nextRepeatAt)
        {
            if (WasPressedThisFrame(key) || WasPressedThisFrame(button))
            {
                nextRepeatAt = Time.unscaledTime + RepeatDelay;
                return true;
            }

            if (!IsHeld(Binding.OfKey(key)) && !IsHeld(Binding.OfPad(button)))
            {
                nextRepeatAt = 0f;
                return false;
            }

            if (Time.unscaledTime < nextRepeatAt)
            {
                return false;
            }

            nextRepeatAt = Time.unscaledTime + RepeatInterval;
            return true;
        }

        /// <summary>First key, mouse button or wheel turn this frame, for rebinding. None when there is none.</summary>
        public static Binding ReadKeyboardOrMouse()
        {
            if (Keyboard.current != null)
            {
                allKeys ??= (Key[])Enum.GetValues(typeof(Key));

                foreach (Key key in allKeys)
                {
                    if (key != Key.None && WasPressedThisFrame(key))
                    {
                        return Binding.OfKey(key);
                    }
                }
            }

            foreach (MouseInput mouse in MouseButtons)
            {
                if (Input.GetMouseButtonDown((int)mouse))
                {
                    return Binding.OfMouse(mouse);
                }
            }

            if (Scroll > 0f)
            {
                return Binding.OfMouse(MouseInput.WheelUp);
            }

            if (Scroll < 0f)
            {
                return Binding.OfMouse(MouseInput.WheelDown);
            }

            return Binding.None;
        }

        /// <summary>First gamepad button pressed this frame, for rebinding. None when there is none.</summary>
        public static Binding ReadPad()
        {
            foreach (var entry in Binding.PadButtons)
            {
                if (WasPressedThisFrame(entry.Button))
                {
                    return Binding.OfPad(entry.Button);
                }
            }

            return Binding.None;
        }

        private static ButtonControl PadControlOf(GamepadButton button)
        {
            Gamepad gamepad = Gamepad.current;
            return gamepad?[button];
        }

        private static KeyControl KeyControlOf(Key key)
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || key == Key.None)
            {
                return null;
            }

            if (!Properties.TryGetValue(key, out PropertyInfo property))
            {
                property = typeof(Keyboard).GetProperty(ToPropertyName(key),
                    BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                {
                    GrapplingPlugin.Logger.LogWarning($"Key '{key}' has no control on Keyboard, ignoring it.");
                }

                Properties[key] = property;
            }

            return property?.GetValue(keyboard) as KeyControl;
        }

        // Key.LeftBracket -> leftBracketKey, Key.Space -> spaceKey, Key.A -> aKey
        private static string ToPropertyName(Key key)
        {
            string name = key.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1) + "Key";
        }
    }
}
