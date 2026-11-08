using UnityEngine;

namespace GrapplingHook
{
    /// <summary>
    /// The rope while it is attached: a pendulum on the local rigidbody, run in FixedUpdate.
    ///
    /// It is a rope, not a rod and not a spring. Inside its length nothing happens. At its
    /// length the speed going away from the anchor is taken out and the speed across the rope
    /// is kept, which is the whole swing: falling turns into an arc instead of being stopped.
    /// What a step overshoots the length by is pulled back as velocity (Correction), never as
    /// position - placing the body would throw away momentum and could put it through a floor.
    ///
    /// The game's own air control adds forces and never sets the velocity, so it keeps working
    /// on top of the rope; the only thing it overwrites is falling faster than MaxFallSpeed.
    ///
    /// The anchor is held in the hit object's local space, so a hook on something that moves
    /// travels with it. Its speed is measured from where it was a step ago and the rope works on
    /// the speed relative to it, so riding a moving platform's rope does not read as stretch.
    /// </summary>
    internal sealed class Rope
    {
        /// <summary>Below this the direction to the anchor is noise, and normalising it is a divide by zero.</summary>
        private const float MinSeparation = 0.001f;

        /// <summary>
        /// Fastest the overstretch correction may pull, in m/s. Winding several wheel notches in
        /// at once is a tug, not a launch.
        /// </summary>
        private const float MaxCorrectionSpeed = 30f;

        private Transform anchorBody;
        private bool followsBody;
        private Vector3 localAnchor;
        private Vector3 previousAnchor;

        public bool Attached { get; private set; }
        public Vector3 Anchor { get; private set; }
        public float Length { get; private set; }

        public void Attach(GrapplingSettings settings, Vector3 point, Collider collider, Vector3 from)
        {
            anchorBody = collider != null ? collider.transform : null;
            followsBody = anchorBody != null;
            localAnchor = followsBody ? anchorBody.InverseTransformPoint(point) : point;

            Anchor = point;
            previousAnchor = point;
            Length = Mathf.Clamp(Vector3.Distance(from, point), settings.MinLength.Value, settings.MaxRange.Value);
            Attached = true;
        }

        public void Detach()
        {
            Attached = false;
            anchorBody = null;
            followsBody = false;
        }

        /// <summary>
        /// Follows the anchor's object. False when the object is gone, which is the rope's end:
        /// there is nothing left to hang from.
        /// </summary>
        public bool TrackAnchor()
        {
            if (!followsBody)
            {
                return true;
            }

            // A destroyed object compares equal to null on the Unity side even though the field
            // still holds a wrapper, which is why the flag above has to be separate.
            if (anchorBody == null)
            {
                return false;
            }

            Anchor = anchorBody.TransformPoint(localAnchor);
            return true;
        }

        /// <param name="scrollNotches">Wheel notches since the last step, up positive (shorter).</param>
        /// <param name="pump">Where the movement keys point, camera relative, at most 1 long.</param>
        public void FixedTick(GrapplingSettings settings, Rigidbody rb, bool reeling, float scrollNotches, Vector3 pump)
        {
            float dt = Time.fixedDeltaTime;
            Vector3 anchorVelocity = (Anchor - previousAnchor) / dt;
            previousAnchor = Anchor;

            Vector3 toAnchor = Anchor - rb.worldCenterOfMass;
            float distance = toAnchor.magnitude;

            if (distance < MinSeparation)
            {
                return;
            }

            Vector3 direction = toAnchor / distance;
            float minLength = settings.MinLength.Value;

            if (scrollNotches != 0f)
            {
                Length = Mathf.Clamp(Length - scrollNotches * settings.ScrollStep.Value, minLength, settings.MaxRange.Value);
            }

            float reelSpeed = settings.ReelSpeed.Value;

            if (reeling)
            {
                // From the distance, not the old length: reeling a slack rope starts pulling
                // straight away instead of first winding in the slack at reel speed.
                Length = Mathf.Max(minLength, Mathf.Min(Length, distance) - reelSpeed * dt);
            }

            Vector3 velocity = rb.linearVelocity;

            // Only the part across the rope: pushing along it would stretch or slacken the rope
            // instead of swinging on it.
            Vector3 across = pump - direction * Vector3.Dot(pump, direction);
            velocity += across * (settings.SwingForce.Value * dt);

            if (distance > Length)
            {
                float inward = Vector3.Dot(velocity - anchorVelocity, direction);

                if (inward < 0f)
                {
                    velocity -= direction * inward;
                }

                float correction = Mathf.Min((distance - Length) * settings.Correction.Value / dt, MaxCorrectionSpeed);
                velocity += direction * correction;
            }

            if (reeling && distance > minLength)
            {
                float inward = Vector3.Dot(velocity - anchorVelocity, direction);

                if (inward < reelSpeed)
                {
                    velocity += direction * (reelSpeed - inward);
                }
            }

            rb.linearVelocity = velocity;
        }
    }
}
