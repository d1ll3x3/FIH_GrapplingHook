# Grappling Hook

A BepInEx mod for **Flipping is Hard** that gives you a swinging grappling hook: aim at
anything, hold the button, swing off it, let go and fly.

Only you need the mod to swing. The rope is drawn through the game's own hook rope, which the
game syncs to the lobby, so everyone sees it — with or without the mod.

## Installing

Build with `build.bat`, or copy the `GrapplingHook` folder into `BepInEx\plugins`. The config
file (`GrapplingHook.cfg`) lives next to the dll and is created on the first run.

## Playing

| Keyboard / mouse | Controller | What it does |
|---|---|---|
| Hold `Q` | Hold `RT` | Fires the hook at whatever is in the middle of the screen and keeps you on it. Let go to drop off with your speed. |
| Movement keys | Left stick | Pump the swing, relative to the camera. |
| Hold `F` | Hold `LT` | Reel in: you are pulled towards the point. |
| Wheel up / down | D-pad up / down | Shorten / lengthen the rope. |
| `F8` | `R3` | Opens the bind menu. |

**Letting go gives your jump back.** Release the rope in the air and the next jump before you
land jumps from mid-air — the game's own jump, so it goes where the stick points. Hooking again
or landing uses it up. Being dropped by the game (a menu, breaking, a teleport) does not earn it.

The mark in the middle of the screen lights up when something is within range. Pressing a
little early is fine: the hook fires the moment the aim lands on something while the bind is
held.

The hook stays out of the way of the level's own hooks, ziplines, swing bars, suck pipes and
cannons, and lets go on its own in menus, when the phone breaks and on respawns.

**Achievements are blocked while the mod is loaded**, through the same switch the game uses for
Crowd Control runs. Remove the mod to earn them again.

## Binds

Every action has a keyboard-and-mouse bind and a controller bind, and both work at the same
time. The keyboard-and-mouse slot takes any key, any mouse button (left, right, middle, back,
forward) or a wheel direction; the controller slot takes any controller button, triggers and
stick clicks included.

In the menu: arrows or D-pad move, Enter or A rebinds the selected slot, R or X restores its
default, Backspace or Y clears it, the menu bind or B closes. While waiting for the new bind,
Esc or Start cancels.

In the config file they are written as `Key:Q`, `Mouse:Right`, `Mouse:WheelUp`,
`Pad:RightTrigger` or `None`.

## Settings

| Setting | Default | What it does |
|---|---|---|
| `Enabled` | on | Whether the hook works at all. |
| `HookKey` / `HookPad` | Q / RT | Hold to stay hooked. |
| `ReelKey` / `ReelPad` | F / LT | Hold to reel in. |
| `ShortenKey` / `ShortenPad` | Wheel up / D-pad up | Wind the rope in. |
| `LengthenKey` / `LengthenPad` | Wheel down / D-pad down | Let the rope out. |
| `MenuKey` / `MenuPad` | F8 / R3 | Opens the bind menu. |
| `MaxRange` | 60 m | Furthest a hook can land, measured from you. |
| `MinLength` | 1.5 m | Shortest the rope winds to. |
| `ReelSpeed` | 4.5 m/s | How fast reeling pulls you in. |
| `ScrollStep` | 0.375 m | Rope per wheel notch. A held button winds about eight notches a second. |
| `SwingForce` | 14 m/s² | How hard the movement keys pump the swing. |
| `Correction` | 0.25 | How firmly the rope holds its length. Too high jitters. |
| `SyncRope` | on | Show the rope through the game's synced hook rope. |
| `ShowCrosshair` | on | Draw the aiming mark. |

## How it works

**The swing is a rope, not a spring.** Inside its length nothing happens. At its length the
speed away from the anchor is removed and the speed across the rope is kept, which is what
turns a fall into an arc. Overstretch is taken back as velocity, never by moving the body, so
momentum is kept and collisions stay the physics engine's job.

**It only ever touches your own rigidbody's velocity**, in FixedUpdate. The game's air control
adds forces rather than setting the velocity, so it keeps working on top of the rope; the only
thing the game overrides is falling faster than its maximum fall speed.

**A hook on something that moves goes with it.** The point is stored in the hit object's own
space, and the rope works on your speed relative to it.

**The rope you see is the game's.** `SyncVisualsHook.SetHookedClient` is the owner call the
level hooks use; it reaches every client through a server RPC and a SyncVar, and each client
then draws the rope from that player to the point every frame. It only drives visuals and
sound, so the game never believes one of its own hooks is holding you. No Harmony patches.
