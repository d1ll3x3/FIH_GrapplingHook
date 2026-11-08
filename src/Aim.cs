using EHS;
using UnityEngine;

namespace GrapplingHook
{
    /// <summary>
    /// Where the hook would land if it were fired now: straight down the middle of the screen.
    ///
    /// The camera sits behind and above the phone, so the ray is cast from the camera but the
    /// range is measured from the player - otherwise zooming the camera out would shorten the
    /// hook. The ray passes through players, ours and everyone else's: a remote player is moved
    /// by its NetworkTransform, and a rope tied to them would be tied to a lagging copy.
    /// Triggers are ignored too, they are zones, not surfaces.
    /// </summary>
    internal sealed class Aim
    {
        public bool HasTarget { get; private set; }
        public Vector3 Point { get; private set; }
        public Collider Collider { get; private set; }

        public void Clear()
        {
            HasTarget = false;
            Collider = null;
        }

        public void Tick(GrapplingSettings settings, PlayerRef player)
        {
            Clear();

            Rigidbody rb = player.Rb;
            Camera camera = GameRefs.Camera(player);

            if (rb == null || camera == null)
            {
                return;
            }

            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 origin = rb.worldCenterOfMass;
            float range = settings.MaxRange.Value;

            // Far enough to reach MaxRange past the player from wherever the camera is.
            float reach = range + Vector3.Distance(ray.origin, origin);

            var hits = Physics.RaycastAll(ray, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            RaycastHit best = default;
            bool found = false;

            foreach (RaycastHit hit in hits)
            {
                if (hit.distance >= nearest || IsPlayer(hit.collider, rb))
                {
                    continue;
                }

                nearest = hit.distance;
                best = hit;
                found = true;
            }

            if (!found || Vector3.Distance(origin, best.point) > range)
            {
                return;
            }

            HasTarget = true;
            Point = best.point;
            Collider = best.collider;
        }

        private static bool IsPlayer(Collider collider, Rigidbody ownBody)
        {
            if (collider == null)
            {
                return true;
            }

            Rigidbody body = collider.attachedRigidbody;

            return body != null && (body == ownBody || body.GetComponent<PlayerRef>() != null);
        }
    }
}
