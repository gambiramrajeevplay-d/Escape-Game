using UnityEngine;
using TMPro;
/// <summary>
/// Roblox-style third person player controller for Unity 2021.
/// Movement is relative to the camera (WASD), Space to jump, hold Shift to sprint.
/// Also reads on-screen touch input from TouchControlsUI when active (mobile/tablet).
/// Requires a CharacterController component on the same GameObject.
/// Drives an Animator with "isRunning" and "isJumping" bools.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerControllerRoblox : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your main/follow camera here. If left empty, Camera.main is used.")]
    public Transform cameraTransform;

    [Tooltip("Drag the Animator here. If left empty, GetComponent<Animator>() is used.")]
    public Animator animator;

    [Header("Movement")]
    public float walkSpeed = 8f;
    public float sprintSpeed = 14f;
    public float acceleration = 12f;      // how fast we ramp up/down to target speed
    public float rotationSpeed = 12f;     // how fast the character turns to face movement direction

    [Header("Jumping / Gravity")]
    public float jumpHeight = 3.2f;       // roughly matches Roblox's floaty jump feel
    public float gravity = -25f;
    public float fallMultiplier = 1.6f;   // makes falling feel snappier than rising
    public int maxJumps = 1;              // set to 2 for a double jump like some Roblox games

    [Header("Ground Check")]
    public float groundedStickForce = -2f;

    [Header("Ground Layer Snap")]
    [Tooltip("Layer(s) that count as walkable ground/path. Set this to a dedicated 'Ground' layer containing your floor/path tiles, rather than leaving it as Everything, so this check can't accidentally snap to the player's own colliders, obstacles, or the ragdoll.")]
    public LayerMask groundLayer = ~0;
    [Tooltip("Extra raycast-based snap that runs whenever the CharacterController reports grounded, closing any small visual gap left between the capsule's bottom and the actual floor mesh (a common source of a character looking like it's floating/hovering just above the path). Turn off if you don't want this and only Slope Limit/Skin Width/Step Offset tuning is desired.")]
    public bool useGroundSnap = true;
    [Tooltip("How far below the capsule's bottom to look for ground when snapping.")]
    public float groundSnapCheckDistance = 0.3f;
    [Tooltip("Small gap intentionally left between the capsule's bottom and the ground after snapping, so the CharacterController's own next-frame grounded check still reads true (should roughly match Skin Width).")]
    public float groundSnapBuffer = 0.02f;

    [Header("Animator Parameter Names")]
    public string isRunningParam = "isRunning";
    public string isJumpingParam = "isJumping";

    [Header("Footsteps")]
    [Tooltip("AudioSource used to play the footstep loop. If left empty, one is added to this GameObject automatically.")]
    public AudioSource footstepSource;
    [Tooltip("Single footstep clip. It will be looped for as long as the player is walking/running on the ground.")]
    public AudioClip footstepClip;
    [Tooltip("Pitch used for the footstep loop while walking.")]
    [Range(0.5f, 2f)] public float walkFootstepPitch = 1f;
    [Tooltip("Pitch used for the footstep loop while sprinting — higher pitch reads as a faster step cadence without needing a second clip.")]
    [Range(0.5f, 2f)] public float sprintFootstepPitch = 1.4f;
    [Tooltip("Volume of the footstep loop.")]
    [Range(0f, 1f)] public float footstepVolume = 0.8f;

    [Tooltip("One-shot clip played each time the player jumps.")]
    public AudioClip jumpClip;
    [Range(0f, 1f)] public float jumpSoundVolume = 1f;

    [Header("Jump Forward Movement")]
    [Tooltip("How far forward (in world units) the player is pushed, in the direction they're facing at the moment they jump, over the full arc of the jump. This is added on top of normal WASD air control, not a replacement for it. 0 = no extra push (player only moves via input while airborne, same as before).")]
    public float jumpForwardDistance = 0f;

    // Speed derived from jumpForwardDistance and the jump's estimated total
    // air time, computed once when the jump starts. Applied every frame for
    // the duration of the jump so it isn't affected by HandleMovement's
    // acceleration/deceleration smoothing (which would otherwise fight it
    // or wash it out if the player isn't also holding a direction).
    private float jumpForwardSpeed = 0f;
    private Vector3 jumpForwardDirection = Vector3.forward;

    private CharacterController controller;
    private Vector3 velocity;             // current vertical velocity (and used for horizontal smoothing)
    private Vector3 currentMoveVelocity;  // smoothed horizontal velocity
    private int jumpsUsed = 0;
    private bool wasGroundedLastFrame;

    // Cached Animator.StringToHash values. The string overload of SetBool
    // re-hashes the parameter name on every single call, which is wasted
    // CPU work every frame on weak hardware. Hash once, reuse forever.
    private int isRunningHash;
    private int isJumpingHash;

    // Only warn about a missing camera once instead of every frame — log
    // spam itself costs real time on low-end devices.
    private bool loggedMissingCameraWarning = false;

    [Header("Ground Check Stability")]
    [Tooltip("How long (seconds) isGrounded must read false before we count the player as actually airborne/jumping. Filters out the single-frame false reading Unity gives before the first Move() call.")]
    public float groundedGraceTime = 0.15f;

    private float ungroundedTimer = 0f;

    // True only while the player is airborne *because they jumped*, as
    // opposed to airborne because they walked off a ledge or are falling
    // after a respawn. UpdateAnimator uses this so the jump animation only
    // plays on an actual jump, not on every ground-leaving moment.
    private bool jumpStarted = false;

    // Set each frame in HandleMovement/HandleMovementFallback so the
    // footstep pitch (and any other sprint-dependent logic) can read the
    // current sprint state without recomputing the input check again.
    private bool isSprintingCached = false;

    [Header("Control")]
    public bool canControl = true;

    [Header("Trigger Tunneling Safety")]
    [Tooltip("Sphere-casts along the player's movement path each frame so a big Move() step — e.g. from a slow/dropped frame on weak TV hardware — can't skip clean over a thin Level Trigger volume without ever firing OnTriggerEnter.")]
    public bool sweepForMissedTriggers = true;

    // Reused every frame to avoid a GC allocation from SphereCastAll on
    // low-end hardware.
    private readonly RaycastHit[] sweepHitsBuffer = new RaycastHit[8];

    [Header("Respawn")]
    public Vector3 lastSafePosition;
    public float savePositionDistance = 0.5f;

    [Tooltip("Tag used on the parent GameObject that holds all respawn points as children (e.g. your 'Respawn Points' object).")]
    public string respawnPointsTag = "Respawn Points";

    private Transform respawnParent;
    private Transform[] respawnPoints;

    public int respawnCount = 0;
    public int maxRespawns = 5;

    private Quaternion lastSafeRotation;

    [Header("UI")]
    public TMP_Text chancesText;
    void Start()
    {
        lastSafeRotation = transform.rotation;

        GameObject respawnObj = GameObject.FindGameObjectWithTag(respawnPointsTag);

        if (respawnObj != null)
        {
            respawnParent = respawnObj.transform;

            respawnPoints = new Transform[respawnParent.childCount];

            for (int i = 0; i < respawnParent.childCount; i++)
            {
                respawnPoints[i] = respawnParent.GetChild(i);
            }
        }
        else
        {
            Debug.LogWarning("PlayerControllerRoblox: No GameObject found with tag '" + respawnPointsTag + "'. Respawning will fail.", this);
        }

        lastSafePosition = transform.position;

        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (animator == null)
            animator = GetComponent<Animator>();

        isRunningHash = Animator.StringToHash(isRunningParam);
        isJumpingHash = Animator.StringToHash(isJumpingParam);

        // CharacterController.isGrounded is not valid until Move() has been
        // called at least once. Without this, isGrounded reads false on the
        // very first frame(s), which made UpdateAnimator think the player
        // was airborne and play the jump animation right at game start.
        controller.Move(Vector3.down * 0.01f);

        if (chancesText == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("ChancesText");

            if (obj != null)
                chancesText = obj.GetComponent<TMP_Text>();
        }

        // Set up the footstep AudioSource once. Using a single looping
        // clip whose pitch shifts for sprint, rather than Play()-ing a
        // one-shot every frame, since a one-shot would either overlap
        // itself constantly or need its own timer/cadence logic.
        if (footstepSource == null)
            footstepSource = gameObject.AddComponent<AudioSource>();

        footstepSource.clip = footstepClip;
        footstepSource.loop = true;
        footstepSource.playOnAwake = false;
        footstepSource.volume = footstepVolume;

        UpdateChancesUI();
    }

    void Update()
    {
        // Save last safe position while grounded
        if (controller.isGrounded &&
      Vector3.Distance(lastSafePosition, transform.position) >= savePositionDistance)
        {
            lastSafePosition = transform.position;
            lastSafeRotation = transform.rotation;
        }

        if (!canControl)
        {
            // Stop horizontal player movement.
            currentMoveVelocity = Vector3.zero;

            // Continue gravity so the player can finish falling
            // if the trigger was entered while jumping.
            if (!controller.isGrounded)
            {
                float appliedGravity = gravity *
                    (velocity.y < 0f ? fallMultiplier : 1f);

                velocity.y += appliedGravity * Time.deltaTime;
            }
            else
            {
                velocity.y = groundedStickForce;
            }

            // Keep the CharacterController moving vertically even
            // though player input is disabled.
            controller.Move(Vector3.up * velocity.y * Time.deltaTime);

            // Update animation based on the actual grounded state.
            UpdateAnimator(controller.isGrounded);

            return;
        }
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = groundedStickForce;
            jumpsUsed = 0;
            jumpStarted = false;
            jumpForwardSpeed = 0f;
        }

        HandleMovement(isGrounded);
        HandleJump();
        ApplyGravity();

        // A single Move() call per frame instead of three. CharacterController.Move
        // does a full collision sweep/depenetration pass every time it's called, so
        // calling it 2-3x per frame roughly multiplies that cost for no benefit —
        // this matters a lot more on weak TV-box CPUs than on a dev machine.
        Vector3 positionBeforeMove = transform.position;

        // Jump-forward push only applies while airborne from an actual jump
        // (jumpStarted), so it never affects normal ground movement.
        Vector3 jumpForwardVelocity = jumpStarted ? jumpForwardDirection * jumpForwardSpeed : Vector3.zero;
        controller.Move((currentMoveVelocity + jumpForwardVelocity + Vector3.up * velocity.y) * Time.deltaTime);

        if (sweepForMissedTriggers)
            CheckForMissedTriggers(positionBeforeMove, transform.position);

        // Re-read isGrounded after Move() — it can flip true this same frame
        // (e.g. landing). Snapping only while grounded and only when the
        // player isn't actively rising (velocity.y small/negative) means this
        // never fights a jump in progress.
        if (useGroundSnap && controller.isGrounded && velocity.y <= 0f)
            SnapToGround();

        UpdateAnimator(isGrounded);

        wasGroundedLastFrame = isGrounded;
    }

    /// <summary>
    /// Raycasts straight down from the capsule's center to the ground layer
    /// and closes any small leftover gap between the capsule's bottom and
    /// the actual floor mesh. This exists because Skin Width/Step Offset
    /// tuning on the CharacterController can still leave a persistent
    /// fraction-of-a-unit gap that reads visually as the character hovering
    /// above the path instead of standing flush on it — this is a direct,
    /// explicit fix for that rather than relying purely on CharacterController
    /// internals to get it exactly right.
    /// </summary>
    private void SnapToGround()
    {
        Vector3 origin = transform.position + controller.center;
        float halfHeight = controller.height * 0.5f;
        float castDistance = halfHeight + groundSnapCheckDistance;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, castDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            float capsuleBottomY = origin.y - halfHeight;
            float gap = hit.point.y - capsuleBottomY;

            // Only close small gaps — this is meant to fix a hovering
            // artifact, not to teleport the player down a ledge or through
            // a legitimate step it hasn't actually descended yet.
            if (gap > groundSnapBuffer && gap < groundSnapCheckDistance)
            {
                controller.Move(Vector3.down * (gap - groundSnapBuffer));
            }
        }
    }

    // NOTE: no OnControllerColliderHit here anymore. Cube-touch (and other
    // obstacle) detection now lives entirely in ObstacleRagdollDeath, which
    // sits on this same GameObject and has its own OnControllerColliderHit.
    // Unity calls every MonoBehaviour's OnControllerColliderHit independently,
    // so having it here too would just fire a second, conflicting reaction
    // to the same hit — ObstacleRagdollDeath.Die() already fully disables
    // this component, so it doesn't need this script's cooperation.

    /// <summary>
    /// Sphere-casts along the path actually traveled this frame (not just a
    /// point check at the new position) so any LevelTrigger collider the
    /// player passed through gets a chance to fire even if Unity's normal
    /// once-per-frame overlap check never caught it — which can happen when
    /// a slow frame produces a large Move() step that jumps clean over a
    /// thin trigger volume. This matters most on TV hardware where frame
    /// pacing is inconsistent, making that tunneling case far more common
    /// than on a stable-framerate dev machine.
    /// </summary>
    private void CheckForMissedTriggers(Vector3 fromPosition, Vector3 toPosition)
    {
        Vector3 delta = toPosition - fromPosition;
        float distance = delta.magnitude;
        if (distance < 0.0001f)
            return;

        Vector3 direction = delta / distance;
        // Slightly smaller than the controller's own radius so the sweep
        // doesn't pick up unrelated colliders the capsule is merely grazing.
        float radius = controller.radius * 0.1f;
        Vector3 castOrigin = fromPosition + controller.center;

        int hitCount = Physics.SphereCastNonAlloc(
            castOrigin,
            radius,
            direction,
            sweepHitsBuffer,
            distance,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            LevelTrigger levelTrigger = sweepHitsBuffer[i].collider.GetComponent<LevelTrigger>();
            if (levelTrigger != null)
                levelTrigger.TryTrigger(controller);
        }
    }

    private void HandleMovement(bool isGrounded)
    {
        float inputX = Input.GetAxisRaw("Horizontal"); // A/D
        float inputZ = Mathf.Max(0f, Input.GetAxisRaw("Vertical")); // Only allow forward

        // Merge on-screen touch input (mobile/tablet) with keyboard/gamepad
        // input. TouchControlsUI.ControlsActive is only true on devices
        // where the buttons are actually shown, so this is a no-op on TV
        // builds. Clamped so holding a keyboard key and a touch button at
        // the same time can't push the value past +/-1.
        if (TouchControlsUI.ControlsActive)
        {
            inputX = Mathf.Clamp(inputX + TouchControlsUI.Horizontal, -1f, 1f);
            inputZ = Mathf.Clamp(inputZ + Mathf.Max(0f, TouchControlsUI.Vertical), 0f, 1f);
        }

        Vector3 inputDir = new Vector3(inputX, 0f, inputZ).normalized;

        bool isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        isSprintingCached = isSprinting;

        // Guard against a missing camera reference. Previously this threw a
        // NullReferenceException on cameraTransform.forward, which silently
        // froze the rest of Update() every frame — no movement, no gravity,
        // no animation — if no camera was assigned and none was tagged "MainCamera".
        if (cameraTransform == null)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                if (!loggedMissingCameraWarning)
                {
                    Debug.LogWarning("PlayerControllerRoblox: No camera assigned and no camera tagged 'MainCamera' found. " +
                        "Falling back to world-space movement. Assign a Camera Transform in the Inspector or tag your camera 'MainCamera'.", this);
                    loggedMissingCameraWarning = true;
                }
                HandleMovementFallback(inputDir, isGrounded);
                return;
            }
        }

        // Build camera-relative movement axes (ignore camera's pitch, like Roblox's default camera)
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 targetDir = (camForward * inputDir.z + camRight * inputDir.x);

        float targetSpeed = (isSprinting ? sprintSpeed : walkSpeed) * inputDir.magnitude;

        // Normalize once and reuse for both speed and rotation, instead of
        // calling .normalized (a sqrt) twice on the same vector.
        bool hasDir = targetDir.sqrMagnitude > 0.001f;
        Vector3 targetDirNormalized = hasDir ? targetDir.normalized : Vector3.zero;
        Vector3 targetVelocity = targetDirNormalized * targetSpeed;

        // Smoothly accelerate/decelerate toward the target velocity
        currentMoveVelocity = Vector3.MoveTowards(currentMoveVelocity, targetVelocity, acceleration * Time.deltaTime * Mathf.Max(targetSpeed, walkSpeed));

        // Rotate the character to face the direction it's moving (Roblox-style facing)
        if (hasDir)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirNormalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Used only if no camera is assigned/found. Moves in raw world-space
    /// axes instead of camera-relative axes, so the character still moves
    /// and animates instead of freezing entirely.
    /// </summary>
    private void HandleMovementFallback(Vector3 inputDir, bool isGrounded)
    {
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        isSprintingCached = isSprinting;

        float targetSpeed = (isSprinting ? sprintSpeed : walkSpeed) * inputDir.magnitude;
        Vector3 targetVelocity = inputDir.normalized * targetSpeed;

        currentMoveVelocity = Vector3.MoveTowards(currentMoveVelocity, targetVelocity, acceleration * Time.deltaTime * Mathf.Max(targetSpeed, walkSpeed));

        if (inputDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleJump()
    {
        bool jumpInput = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.JoystickButton0);

        // Touch jump button. Consumed here (whether or not the jump actually
        // fires below) so a single tap can't keep re-triggering every frame
        // until the player happens to be grounded again with jumps available.
        if (TouchControlsUI.ControlsActive && TouchControlsUI.JumpPressed)
        {
            jumpInput = true;
            TouchControlsUI.ConsumeJump();
        }

        if (jumpInput && jumpsUsed < maxJumps)
        {
            // v = sqrt(h * -2 * g)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpsUsed++;
            jumpStarted = true;

            SetupJumpForwardMovement();

            PlayJumpSound();
        }
    }

    /// <summary>
    /// Works out how fast to push the player forward so that, over the
    /// jump's full estimated air time, they cover exactly jumpForwardDistance
    /// world units. Air time is split into rise (using gravity) and fall
    /// (using gravity * fallMultiplier) phases since ApplyGravity treats
    /// them differently. Direction is locked to wherever the player is
    /// facing at the instant they leave the ground, so strafing/turning
    /// input mid-air doesn't change the push direction.
    /// </summary>
    private void SetupJumpForwardMovement()
    {
        if (jumpForwardDistance <= 0f)
        {
            jumpForwardSpeed = 0f;
            return;
        }

        float timeToApex = velocity.y / -gravity;
        float timeToFall = velocity.y / (-gravity * fallMultiplier);
        float estimatedAirTime = timeToApex + timeToFall;

        jumpForwardSpeed = estimatedAirTime > 0f ? jumpForwardDistance / estimatedAirTime : 0f;
        jumpForwardDirection = transform.forward;
    }

    /// <summary>
    /// Plays the jump clip on a brand-new, dedicated AudioSource rather than
    /// footstepSource. Jumping flips isGrounded false almost immediately,
    /// which makes UpdateFootsteps call footstepSource.Stop() that same
    /// frame — and AudioSource.Stop() kills every sound on that source,
    /// including a PlayOneShot that was just started, cutting the jump clip
    /// off before it could be heard. A separate source sidesteps that
    /// entirely. Destroys itself once the clip finishes playing.
    /// </summary>
    private void PlayJumpSound()
    {
        if (jumpClip == null)
            return;

        GameObject jumpSoundObj = new GameObject("JumpSound_OneShot");
        jumpSoundObj.transform.position = transform.position;

        AudioSource jumpAudioSource = jumpSoundObj.AddComponent<AudioSource>();
        jumpAudioSource.clip = jumpClip;
        jumpAudioSource.volume = jumpSoundVolume;
        jumpAudioSource.playOnAwake = false;
        jumpAudioSource.Play();

        Destroy(jumpSoundObj, jumpClip.length + 0.1f);
    }

    private void ApplyGravity()
    {
        // Fall faster than we rise, for a snappier, less floaty feel
        float appliedGravity = gravity * (velocity.y < 0f ? fallMultiplier : 1f);
        velocity.y += appliedGravity * Time.deltaTime;
    }

    private void UpdateAnimator(bool isGrounded)
    {
        if (animator == null) return;

        // isJumping: true only once we've been continuously ungrounded for
        // groundedGraceTime AND that airborne time was actually kicked off by
        // a jump input (jumpStarted). Without the jumpStarted check, simply
        // walking off a ledge or platform edge — with no jump button pressed
        // at all — would satisfy "ungrounded for groundedGraceTime" and play
        // the jump animation, which looked wrong.
        if (isGrounded)
            ungroundedTimer = 0f;
        else
            ungroundedTimer += Time.deltaTime;

        bool isJumping = jumpStarted && ungroundedTimer >= groundedGraceTime;

        // Falling: airborne past the grace window, but not because of a real
        // jump (walked off a ledge, pushed off, respawned mid-air, etc).
        // Force idle here instead of letting isRunning keep blending in a run
        // animation just because there happens to be horizontal drift.
        bool isFalling = !isGrounded && !jumpStarted && ungroundedTimer >= groundedGraceTime;

        // isRunning: true whenever there's meaningful horizontal input/movement,
        // but never while falling — falling should read as idle only.
        bool isRunning = !isFalling &&
            new Vector3(currentMoveVelocity.x, 0f, currentMoveVelocity.z).sqrMagnitude > 0.01f;

        animator.SetBool(isRunningHash, isRunning);
        animator.SetBool(isJumpingHash, isJumping);

        // canControl is included here because UpdateAnimator (and this call)
        // keep running every single frame even while canControl is false —
        // only HandleMovement/HandleJump are skipped. Without this check,
        // isRunning can stay frozen at whatever currentMoveVelocity was the
        // instant control was cut (e.g. by LevelTrigger on Pass/Fail), which
        // made UpdateFootsteps call Play() again the very next frame right
        // after something else had just called Stop() on this same source.
        UpdateFootsteps(isRunning && isGrounded && canControl, isSprintingCached);
    }

    /// <summary>
    /// Starts/stops the looping footstep clip and adjusts its pitch for
    /// walk vs sprint. A single clip is reused rather than one-shotting it
    /// repeatedly, since looping avoids needing a step-cadence timer.
    /// </summary>
    private void UpdateFootsteps(bool shouldPlay, bool isSprinting)
    {
        if (footstepSource == null || footstepClip == null)
            return;

        if (shouldPlay)
        {
            footstepSource.pitch = isSprinting ? sprintFootstepPitch : walkFootstepPitch;

            if (!footstepSource.isPlaying)
                footstepSource.Play();
        }
        else if (footstepSource.isPlaying)
        {
            footstepSource.Stop();
        }
    }
    public void ResetMovementAfterPause()
    {
        // Stop the previous jump.
        velocity = Vector3.zero;

        // Stop horizontal movement.
        currentMoveVelocity = Vector3.zero;

        // Reset jump state.
        jumpStarted = false;
        jumpsUsed = 0;
        jumpForwardSpeed = 0f;

        // Reset grounded animation state.
        ungroundedTimer = 0f;

        // Stop footsteps.
        if (footstepSource != null &&
            footstepSource.isPlaying)
        {
            footstepSource.Stop();
        }

        // Make sure animator doesn't remain in jump/run state.
        if (animator != null)
        {
            animator.SetBool(isRunningHash, false);
            animator.SetBool(isJumpingHash, false);
        }
    }
    public void Respawn()
    {
        controller.enabled = false;

        // Was "lastSafePosition - transform.forward", which teleported the
        // player to an arbitrary spot offset by whatever direction they
        // happened to be facing when they died — sometimes back into the
        // same fail zone, into a wall, or off the edge of the platform
        // entirely. Always respawn at an actual respawn point instead —
        // never fall back to lastSafePosition, which could be mid-air,
        // mid-obstacle, or otherwise unsafe depending on where the player
        // died.
        Transform point = GetClosestRespawnPoint();

        if (point != null)
        {
            transform.position = point.position;
            transform.rotation = lastSafeRotation;
        }
        else
        {
            Debug.LogError("PlayerControllerRoblox: No respawn point available — " +
                "check that a GameObject tagged '" + respawnPointsTag + "' exists in the scene and has child respawn points.", this);
            controller.enabled = true;
            canControl = true;
            return;
        }

        velocity = Vector3.zero;
        currentMoveVelocity = Vector3.zero;

        // Clear airborne/jump state too, so a player who respawns mid-fall
        // doesn't land with a jump animation already "in progress" or with
        // jumpsUsed left over from before they died.
        ungroundedTimer = 0f;
        jumpStarted = false;
        jumpsUsed = 0;
        jumpForwardSpeed = 0f;

        // Stop the footstep loop too — otherwise it can keep playing through
        // the teleport if the player was mid-step when they died.
        if (footstepSource != null && footstepSource.isPlaying)
            footstepSource.Stop();

        controller.enabled = true;

        // Give control back after respawning
        canControl = true;
    }
    public void UpdateChancesUI()
    {
        if (chancesText == null)
            return;

        int remaining = maxRespawns - respawnCount;

        if (remaining < 0)
            remaining = 0;

        chancesText.text = "Chances Remaining : " + remaining + "/" + maxRespawns;
    }

    /// <summary>
    /// Picks whichever respawn point (among all children of the tagged
    /// Respawn Points object) is closest to where the player currently is.
    /// </summary>
    private Transform GetClosestRespawnPoint()
    {
        if (respawnPoints == null || respawnPoints.Length == 0)
            return null;

        Transform closest = respawnPoints[0];
        float closestDistance = Vector3.Distance(transform.position, closest.position);

        for (int i = 1; i < respawnPoints.Length; i++)
        {
            float distance = Vector3.Distance(transform.position, respawnPoints[i].position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = respawnPoints[i];
            }
        }

        return closest;
    }
   

}