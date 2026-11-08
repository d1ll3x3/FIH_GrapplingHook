using System;
using EHS;
using UnityEngine;

namespace GrapplingHook
{
    /// <summary>
    /// Shows the rope through the game's own hook rope, so the whole lobby sees it.
    ///
    /// Every player carries a SyncVisualsHook. SetHookedClient(hooked, upper, lower) is an owner
    /// call that goes to the server as an RPC and comes back on every client as a SyncVar, whose
    /// change handler turns the player's rope and its sound on or off. From then on each client
    /// draws it every frame from wherever that player is to the upper point, so the lower point
    /// only matters for the first frame and nothing has to be sent while you swing. That is also
    /// why other players see it without having the mod: it is the game's own code drawing it.
    ///
    /// It only touches visuals and sound (ApplyVisualState: HookVisualEffect and the hook sound),
    /// never the player's PhoneContext, so the game does not think a level hook is holding you.
    /// </summary>
    internal sealed class RopeSync
    {
        /// <summary>How far a moving anchor may drift before the upper point is sent again, in metres.</summary>
        private const float ResendDistance = 0.1f;

        /// <summary>Shortest gap between two sends, in seconds: it is a server RPC each time.</summary>
        private const float ResendInterval = 0.1f;

        private bool shown;
        private bool broken;
        private Vector3 sentAnchor;
        private float sentAt;

        public void Show(PlayerRef player, Vector3 anchor)
        {
            if (broken)
            {
                return;
            }

            bool moved = Vector3.Distance(anchor, sentAnchor) > ResendDistance;

            if (shown && (!moved || Time.unscaledTime - sentAt < ResendInterval))
            {
                return;
            }

            if (Send(player, true, anchor))
            {
                shown = true;
                sentAnchor = anchor;
                sentAt = Time.unscaledTime;
            }
        }

        public void Hide(PlayerRef player)
        {
            if (!shown)
            {
                return;
            }

            shown = false;
            Send(player, false, sentAnchor);
        }

        /// <summary>
        /// The player the rope was shown on is gone - respawned or a new scene - and its rope went
        /// with it, so there is nothing to hide. Also gives sending another try.
        /// </summary>
        public void Forget()
        {
            shown = false;
            broken = false;
        }

        private bool Send(PlayerRef player, bool hooked, Vector3 anchor)
        {
            try
            {
                SyncVisualsHook visuals = player.SyncVisualsHook;

                if (visuals == null)
                {
                    return false;
                }

                visuals.SetHookedClient(hooked, anchor, player.transform.position);
                return true;
            }
            catch (Exception ex)
            {
                // Once is enough: retrying a call that throws ten times a second only buries the
                // log. It gets another chance with the next player or scene.
                broken = true;
                GrapplingPlugin.Logger.LogError(
                    $"Showing the rope through the game's hook visuals failed, the rope stays " +
                    $"invisible until the next respawn or scene. The swing still works. {ex}");
                return false;
            }
        }
    }
}
