using UnityEngine;

/// <summary>
/// Pushes/knocks the player via a trigger touch rather than physically
/// shoving the CharacterController. Requires a Rigidbody because Unity only
/// raises OnTriggerEnter if at least one of the two colliders involved has a
/// Rigidbody attached — the player's CharacterController doesn't count, and
/// without this the cube's trigger would never fire at all.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class MoveCubeX : MonoBehaviour
{
    [Header("Movement")]
    public float distance = 3f;
    public float speed = 2f;

    [Header("Ragdoll Push")]
    [Tooltip("Multiplies the cube's own travel speed to decide how hard it knocks the player into a ragdoll. Tune this rather than ObstacleRagdollDeath's force fields if the knockback feels too weak/strong.")]
    public float pushForceMultiplier = 6f;

    [Tooltip("Extra upward kick added on top of the horizontal push so the ragdoll pops up instead of just sliding, like a Roblox knock.")]
    public float upwardKick = 3f;

    private Rigidbody rb;
    private Collider col;
    private Vector3 startPosition;
    private Vector3 lastPosition;

    // Public so ObstacleRagdollDeath can read this when this cube is the
    // thing that killed the player, to know which way and how fast it was
    // moving at the moment of impact.
    public Vector3 Velocity { get; private set; }

    // Set true the instant the player is detected, so the trigger stops
    // reacting to anything further — no repeated OnTriggerEnter/Stay calls,
    // no double-processing if the ragdoll's own colliders happen to sweep
    // back through this trigger as it falls.
    private bool hasHitPlayer = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // This cube is moved entirely by script (PingPong), never by physics
        // forces, so it must stay kinematic. It still needs a Rigidbody purely
        // so Unity's trigger system generates OnTriggerEnter callbacks at all.
        rb.isKinematic = true;
        rb.useGravity = false;

        // Continuous Dynamic gives PhysX a swept collision test for this
        // object's motion every physics step, instead of only checking for
        // overlap at its position after the step. Without this, a fast cube
        // can fully cross the player's capsule within one frame and never
        // register a hit — this is the actual fix for that tunneling case.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Movement now happens in FixedUpdate via MovePosition (fixed 50Hz
        // by default) rather than every render frame, so interpolation keeps
        // it visually smooth instead of looking stepped/jittery.
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        col = GetComponent<Collider>();
        // Trigger, not solid — matches the Fail/Pass trigger convention this
        // project already uses.
        col.isTrigger = true;
    }

    void Start()
    {
        startPosition = transform.position;
        lastPosition = transform.position;
    }

    void FixedUpdate()
    {
        if (hasHitPlayer)
            return; // no need to keep moving/tracking velocity once its job is done

        // Kinematic Rigidbody movement belongs in FixedUpdate via
        // rb.MovePosition, not Update()+transform.position. MovePosition is
        // what lets PhysX treat this as a real swept motion for collision/
        // trigger purposes instead of just teleporting the collider and
        // re-checking overlap at the new spot.
        float x = Mathf.PingPong(Time.time * speed, distance);
        Vector3 targetPosition = startPosition + Vector3.right * x;
        rb.MovePosition(targetPosition);

        if (Time.fixedDeltaTime > 0f)
            Velocity = (targetPosition - lastPosition) / Time.fixedDeltaTime;

        lastPosition = targetPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHitPlayer)
            return;

        ObstacleRagdollDeath death = other.GetComponent<ObstacleRagdollDeath>();
        if (death == null)
            return;

        hasHitPlayer = true;

        // Stop this trigger from reacting to (or generating) any further
        // overlap events — the cube can keep moving visually if you want,
        // but it's fully out of the collision/ragdoll pipeline from here.
        col.enabled = false;

        CameraFollow cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.SwitchTargetToLookAt();
        }


        death.Die(this);
    }

    /// <summary>
    /// Read by ObstacleRagdollDeath when this cube is the source of death,
    /// to knock the ragdoll in the direction the cube was actually moving.
    /// </summary>
    public Vector3 GetPushForce()
    {
        return (Velocity * pushForceMultiplier) + (Vector3.up * upwardKick);
    }
    public void ResetCube()
    {
        hasHitPlayer = false;

        col.enabled = true;

        lastPosition = transform.position;
        Velocity = Vector3.zero;
    }
}