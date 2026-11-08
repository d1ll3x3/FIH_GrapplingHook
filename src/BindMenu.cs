using System;
using EHS;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GrapplingHook
{
    /// <summary>
    /// In-game bind menu: one row per action, a keyboard-and-mouse column and a gamepad column.
    /// Each cell edits its BepInEx ConfigEntry directly, so a bind changed here is already the
    /// saved one.
    ///
    /// It works from either device: arrows and Enter, or the D-pad and A. A cell only takes the
    /// device of its column, so pressing a pad button while rebinding the keyboard column does
    /// nothing instead of quietly putting a controller button in the keyboard slot.
    ///
    /// Drawing is labels and filled rectangles only: GUI.Button and GUI.TextField are stripped
    /// in this IL2CPP build and crash the game, and input is read in Update like the rest of
    /// the mod rather than from the IMGUI events.
    /// </summary>
    internal sealed class BindMenu
    {
        private const float RowHeight = 22f;
        private const float Width = 520f;
        private const float Padding = 18f;
        private const float LabelWidth = 170f;

        private readonly GrapplingSettings settings;

        private int row;
        private int column;
        private bool capturing;
        private float upRepeat;
        private float downRepeat;

        private CursorLockMode savedLockState = CursorLockMode.Locked;
        private bool savedCursorVisible;
        private bool blockingGameInput;

        // Blocker token for the game's own jump block list, created on first use.
        private Il2CppSystem.Object jumpToken;

        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle columnStyle;
        private GUIStyle hintStyle;

        public BindMenu(GrapplingSettings settings)
        {
            this.settings = settings;
        }

        public bool IsOpen { get; private set; }

        private Bind Selected
        {
            get
            {
                BindAction action = settings.Actions[row];
                return column == 0 ? action.KeyboardMouse : action.Gamepad;
            }
        }

        /// <summary>Opens and closes on the menu bind, and runs the menu while it is open.</summary>
        public void Tick()
        {
            if (!IsOpen)
            {
                if (settings.Menu.WasPressedThisFrame())
                {
                    Open();
                }

                return;
            }

            // The game grabs the cursor back every frame, so it has to be re-released.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Re-asserted every frame rather than only on open: the player can respawn into a
            // fresh instance while the menu is up, and the block lives on the instance.
            SetGameInputBlocked(true);

            // Before the menu bind: while capturing, that bind is just another key to take.
            if (capturing)
            {
                Capture();
                return;
            }

            if (settings.Menu.WasPressedThisFrame() || InputReader.WasPressedThisFrame(GamepadButton.East))
            {
                Close();
                return;
            }

            int rows = settings.Actions.Length;

            if (InputReader.WasPressedOrRepeating(Key.UpArrow, GamepadButton.DpadUp, ref upRepeat))
            {
                row = (row - 1 + rows) % rows;
            }

            if (InputReader.WasPressedOrRepeating(Key.DownArrow, GamepadButton.DpadDown, ref downRepeat))
            {
                row = (row + 1) % rows;
            }

            if (InputReader.WasPressedThisFrame(Key.LeftArrow) || InputReader.WasPressedThisFrame(GamepadButton.DpadLeft))
            {
                column = 0;
            }

            if (InputReader.WasPressedThisFrame(Key.RightArrow) || InputReader.WasPressedThisFrame(GamepadButton.DpadRight))
            {
                column = 1;
            }

            // Takes effect next frame, so the Enter or A that started it is not read as the new bind.
            if (InputReader.WasPressedThisFrame(Key.Enter) || InputReader.WasPressedThisFrame(GamepadButton.South))
            {
                capturing = true;
            }

            if (InputReader.WasPressedThisFrame(Key.R) || InputReader.WasPressedThisFrame(GamepadButton.West))
            {
                Selected.Reset();
            }

            if (InputReader.WasPressedThisFrame(Key.Backspace) || InputReader.WasPressedThisFrame(GamepadButton.North))
            {
                Selected.Set(Binding.None);
            }
        }

        public void Draw()
        {
            if (!IsOpen)
            {
                return;
            }

            EnsureStyles();

            BindAction[] actions = settings.Actions;
            float height = Padding * 2f + RowHeight * (actions.Length + 4);
            float x = (Screen.width - Width) * 0.5f;
            float y = Mathf.Max(20f, (Screen.height - height) * 0.5f);

            Gui.Fill(new Rect(x, y, Width, height), Gui.Backdrop);

            float rowX = x + Padding;
            float rowWidth = Width - Padding * 2f;
            float cellWidth = (rowWidth - LabelWidth) * 0.5f;
            float cursorY = y + Padding;

            GUI.Label(new Rect(rowX, cursorY, rowWidth, RowHeight), "GRAPPLING HOOK  ·  BINDS", titleStyle);
            cursorY += RowHeight * 1.4f;

            GUI.Label(new Rect(rowX + LabelWidth, cursorY, cellWidth, RowHeight), "keyboard / mouse", columnStyle);
            GUI.Label(new Rect(rowX + LabelWidth + cellWidth, cursorY, cellWidth, RowHeight), "controller", columnStyle);
            cursorY += RowHeight;

            for (int i = 0; i < actions.Length; i++)
            {
                GUI.Label(new Rect(rowX, cursorY, LabelWidth, RowHeight), actions[i].Label, labelStyle);
                DrawCell(new Rect(rowX + LabelWidth, cursorY, cellWidth, RowHeight), actions[i].KeyboardMouse, i, 0);
                DrawCell(new Rect(rowX + LabelWidth + cellWidth, cursorY, cellWidth, RowHeight), actions[i].Gamepad, i, 1);
                cursorY += RowHeight;
            }

            cursorY += RowHeight * 0.3f;
            GUI.Label(new Rect(rowX, cursorY, rowWidth, RowHeight),
                capturing
                    ? (column == 0 ? "press a key, a mouse button or turn the wheel" : "press a controller button") +
                      "   ·   Esc / Start = cancel"
                    : "arrows / D-pad = move   ·   Enter / A = rebind   ·   R / X = default",
                hintStyle);
            cursorY += RowHeight;
            GUI.Label(new Rect(rowX, cursorY, rowWidth, RowHeight),
                capturing
                    ? string.Empty
                    : $"Backspace / Y = clear   ·   {settings.Menu.KeyboardMouse.Value.Label} / B = close",
                hintStyle);
        }

        /// <summary>Called on unload so a menu left open does not keep the game's input blocked.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            capturing = false;
            settings.Save();
            SetGameInputBlocked(false);
            Cursor.lockState = savedLockState;
            Cursor.visible = savedCursorVisible;
        }

        private void Open()
        {
            IsOpen = true;

            // Remembered rather than assumed, so closing the menu from a screen that already
            // had a free cursor does not lock it away.
            savedLockState = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void DrawCell(Rect rect, Bind bind, int cellRow, int cellColumn)
        {
            bool selected = cellRow == row && cellColumn == column;

            if (selected)
            {
                Gui.Fill(rect, Gui.Selection);
            }

            string text = selected && capturing ? "press…" : bind.Value.Label;
            GUI.Label(rect, text, valueStyle);
        }

        /// <summary>
        /// Takes the next input of this column's devices. Escape and Start cancel, so neither can
        /// be bound - the one way out of a capture has to stay a way out.
        /// </summary>
        private void Capture()
        {
            if (InputReader.WasPressedThisFrame(Key.Escape) || InputReader.WasPressedThisFrame(GamepadButton.Start))
            {
                capturing = false;
                return;
            }

            Binding pressed = column == 0 ? InputReader.ReadKeyboardOrMouse() : InputReader.ReadPad();

            if (pressed.Device == BindDevice.None)
            {
                return;
            }

            capturing = false;
            Selected.Set(pressed);
            GrapplingPlugin.Logger.LogInfo($"{settings.Actions[row].Label} bound to {pressed.Label}.");
        }

        /// <summary>Stops the input used in the menu from reaching the game while it is up.</summary>
        private void SetGameInputBlocked(bool blocked)
        {
            if (!blocked && !blockingGameInput)
            {
                return;
            }

            try
            {
                PlayerRef player = GameRefs.LocalPlayer;

                if (player?.Movement != null)
                {
                    player.Movement.BlockInput = blocked;
                }

                if (player?.MovementJump != null)
                {
                    jumpToken ??= new Il2CppSystem.Object();

                    if (blocked)
                    {
                        player.MovementJump.AddJumpBlock(jumpToken);
                    }
                    else
                    {
                        player.MovementJump.RemoveJumpBlock(jumpToken);
                    }
                }

                blockingGameInput = blocked;
            }
            catch (Exception ex)
            {
                GrapplingPlugin.Logger.LogError($"Could not {(blocked ? "block" : "restore")} game input: {ex}");
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = Gui.Style(16, FontStyle.Bold, Gui.Accent, TextAnchor.UpperLeft);
            labelStyle = Gui.Style(13, FontStyle.Normal, Gui.Normal, TextAnchor.MiddleLeft);
            valueStyle = Gui.Style(13, FontStyle.Bold, Gui.Highlight, TextAnchor.MiddleCenter);
            columnStyle = Gui.Style(11, FontStyle.Bold, Gui.Dimmed, TextAnchor.MiddleCenter);
            hintStyle = Gui.Style(11, FontStyle.Normal, Gui.Dimmed, TextAnchor.MiddleLeft);
        }
    }
}
