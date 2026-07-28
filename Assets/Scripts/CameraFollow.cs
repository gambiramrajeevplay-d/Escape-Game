using UnityEngine;
using System.Linq;
/// <summary>
/// Third-person follow camera with a fixed yaw.
/// The camera keeps the same viewing direction and only follows the player's position.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Targets")]
    public Transform mainPlayerTarget;
    public Transform ragdollTarget;

    private Transform currentTarget;

    [Header("Position")]
    [Tooltip("Offset from the player, in the camera's facing direction.")]
    public Vector3 offset = new Vector3(0f, 6f, -8f);
    [Tooltip("How quickly the camera catches up.")]
    public float positionSmoothTime = 0.15f;
    [Header("Rotation")]
    [Tooltip("Point the camera looks at relative to the player.")]
    public Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);
    [Header("Camera Angle")]
    public float pitch = 15f;
    private Vector3 positionVelocity;
    private float yaw;
    [Header("Rotation")]
    public float rotationSmoothSpeed = 8f;

    [Header("Cinematic Pullback")]
    [Tooltip("Offset the camera blends to once TriggerCinematicPullback() is called, e.g. on player death — usually farther back and higher for a wider, more dramatic shot.")]
    public Vector3 cinematicOffset = new Vector3(0f, 9f, -14f);
    [Tooltip("Pitch the camera blends to during the cinematic pullback.")]
    public float cinematicPitch = 25f;
    [Tooltip("How long the blend from normal framing to the cinematic framing takes.")]
    public float cinematicBlendTime = 1.2f;
    [Tooltip("If true, position smoothing (positionSmoothTime) is slowed down during the cinematic so the pullback itself reads as a slow, deliberate drift instead of snapping at the normal follow speed.")]
    public bool useSlowerSmoothingDuringCinematic = true;
    [Tooltip("Position smooth time used while the cinematic pullback is active, when useSlowerSmoothingDuringCinematic is true.")]
    public float cinematicPositionSmoothTime = 0.6f;
    [Tooltip("Single overall speed dial for the whole cinematic pullback. 1 = normal (uses cinematicBlendTime/cinematicPositionSmoothTime as-is). Lower = slower — e.g. 0.3 makes both the blend and the camera's catch-up take roughly 3x longer. Higher = faster. Adjust this first if the pullback feels too fast; it scales everything together instead of needing cinematicBlendTime and cinematicPositionSmoothTime tuned separately.")]
    [Range(0.05f, 2f)]
    public float cinematicSpeed = 0.3f;

    private bool cinematicActive = false;
    private float cinematicBlendElapsed = 0f;
    private Vector3 offsetAtCinematicStart;
    private float pitchAtCinematicStart;
    private Vector3 currentOffset;
    private float currentPitch;
    // What the camera actually looks at during the cinematic. Defaults to
    // `player` if TriggerCinematicPullback is called without one — but pass
    // the ragdoll's root/hips transform in for the common case, since the
    // player's own transform freezes in place the moment ObstacleRagdollDeath
    // disables it, while the ragdoll physically falls away from that spot.
    private Transform cinematicLookTarget;

    void Start()
    {
        if (mainPlayerTarget == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null)
                mainPlayerTarget = obj.transform;
        }
      

        if (ragdollTarget == null)
        {
            ragdollTarget = Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(t => t.CompareTag("LookAt"));
        }
        currentTarget = mainPlayerTarget;

        if (currentTarget != null)
            yaw = currentTarget.eulerAngles.y;

        currentOffset = offset;
        currentPitch = pitch;
    }

    /// <summary>
    /// Call this once (e.g. from ObstacleRagdollDeath.Die()) to start a
    /// smooth camera pullback to a wider, more dramatic angle. Safe to call
    /// multiple times — only the first call after a reset does anything.
    /// </summary>
    /// <param name="lookTarget">
    /// What the camera should keep looking at during the cinematic. Pass the
    /// ragdoll's transform so the camera actually frames the falling body —
    /// leave null to keep looking at `player` as normal.
    /// </param>
    public void TriggerCinematicPullback(Transform lookTarget = null)
    {
        if (cinematicActive)
            return;

        cinematicActive = true;
        cinematicBlendElapsed = 0f;

        // If no target was supplied, try to find the ragdoll by tag.
        if (lookTarget == null)
        {
            GameObject ragdoll = GameObject.FindGameObjectWithTag("LookAt");
            if (ragdoll != null)
                lookTarget = ragdoll.transform;
        }

        cinematicLookTarget = lookTarget;

        offsetAtCinematicStart = currentOffset;
        pitchAtCinematicStart = currentPitch;
    }
    /// <summary>
    /// Reverts to normal chase-cam framing, e.g. on respawn/level restart.
    /// </summary>
    public void ResetCinematicPullback()
    {
        cinematicActive = false;
        cinematicBlendElapsed = 0f;
        cinematicLookTarget = null;
    }

    void LateUpdate()
    {
        if (currentTarget == null)
            return;

        if (cinematicActive && cinematicBlendTime > 0f)
        {
            // cinematicSpeed scales how fast elapsed time accumulates toward
            // cinematicBlendTime — at 0.3 it takes ~3.3x longer to reach the
            // same blend progress, without needing to hand-tune
            // cinematicBlendTime itself every time this feels off.
            cinematicBlendElapsed += Time.deltaTime * cinematicSpeed;
            float t = Mathf.Clamp01(cinematicBlendElapsed / cinematicBlendTime);
            // Ease out so the pullback settles gently instead of stopping abruptly.
            t = 1f - (1f - t) * (1f - t);
            currentOffset = Vector3.Lerp(offsetAtCinematicStart, cinematicOffset, t);
            currentPitch = Mathf.Lerp(pitchAtCinematicStart, cinematicPitch, t);
        }
        else if (!cinematicActive)
        {
            currentOffset = offset;
            currentPitch = pitch;
        }

        // During the cinematic, orbit around and look at the ragdoll (if one
        // was passed to TriggerCinematicPullback) instead of the player root
        // — the player's transform freezes in place the instant
        // ObstacleRagdollDeath disables it, while the ragdoll actually falls
        // away from that spot, so framing off the frozen root would leave
        // the camera looking at an empty point instead of the fall.
        Transform target = (cinematicActive && cinematicLookTarget != null)
      ? cinematicLookTarget
      : currentTarget;

        Vector3 focusPosition = target.position;
        // Follow the player's facing direction
        yaw = Mathf.LerpAngle(
     yaw,
     target.eulerAngles.y,
     rotationSmoothSpeed * Time.deltaTime);
        Quaternion orbitRotation = Quaternion.Euler(currentPitch, yaw, 0f);
        Vector3 desiredPosition = focusPosition + orbitRotation * currentOffset;

        float smoothTime = (cinematicActive && useSlowerSmoothingDuringCinematic)
            // SmoothDamp's smoothTime is "time to reach target", so slower
            // desired speed means a BIGGER smoothTime — divide, don't
            // multiply, by cinematicSpeed. At cinematicSpeed 0.3, this
            // roughly triples cinematicPositionSmoothTime.
            ? cinematicPositionSmoothTime / Mathf.Max(cinematicSpeed, 0.01f)
            : positionSmoothTime;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            smoothTime);
        Vector3 lookTarget = focusPosition + lookAtOffset;
        transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
    }
    public void SwitchTargetToLookAt()
    {
        GameObject lookAt = GameObject.FindGameObjectWithTag("LookAt");

        if (lookAt == null)
            return;

        currentTarget = lookAt.transform;
        cinematicLookTarget = lookAt.transform;
        yaw = lookAt.transform.eulerAngles.y;
    }
    public void SwitchToRagdollTarget()
    {
        if (ragdollTarget == null)
            return;

        currentTarget = ragdollTarget;
        cinematicLookTarget = ragdollTarget;
        yaw = ragdollTarget.eulerAngles.y;
    }

    public void SwitchToMainPlayer()
    {
        if (mainPlayerTarget == null)
            return;

        currentTarget = mainPlayerTarget;
        cinematicLookTarget = null;
        yaw = mainPlayerTarget.eulerAngles.y;
    }
}