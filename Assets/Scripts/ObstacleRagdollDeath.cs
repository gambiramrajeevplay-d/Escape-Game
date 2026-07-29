using UnityEngine;

/// <summary>
/// Attach this to the same GameObject as PlayerControllerRoblox and its
/// CharacterController.
///
/// On collision with an obstacle:
///  - freezes and disables the normal player controller/collider
///  - snaps a ragdoll dummy into the player's position/rotation and turns it on
///  - disables a list of other GameObjects (normal model, weapon, HUD, etc.)
///  - shows the level-fail screen after a short delay
///
/// Detects obstacles two ways so it works whether your obstacle collider is
/// a trigger or solid geometry:
///  - OnTriggerEnter: obstacle collider has "Is Trigger" checked (recommended,
///    consistent with how Fail/Pass triggers already work in this project).
///  - OnControllerColliderHit: obstacle collider is solid and the
///    CharacterController physically bumps into it.
///
/// Counts something as an obstacle if EITHER is true:
///  - it has the obstacleTag tag, OR
///  - it has a MoveCubeX component on it (moving pusher cubes are hazards
///    too, even if nobody remembered to tag them "Obstacle" in the editor).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ObstacleRagdollDeath : MonoBehaviour
{
    [Header("Obstacle Detection")]
    [Tooltip("Tag used on obstacle GameObjects.")]
    public string obstacleTag = "Obstacle";

    [Tooltip("If true, any collider with a MoveCubeX component also counts as an obstacle, even if it isn't tagged. Turn this off if you ever want a pusher cube that DOESN'T kill the player.")]
    public bool treatMoveCubesAsObstacles = true;

    [Header("Ragdoll")]
    [Tooltip("The ragdoll dummy, left disabled in the scene until death.")]
    public GameObject ragdoll;

    [Tooltip("Snap the ragdoll to the player's exact position/rotation the moment it's enabled.")]
    public bool matchPlayerTransformOnRagdoll = true;

    [Tooltip("Optional outward impulse applied to every Rigidbody under the ragdoll on death, for a bit of impact reaction. 0 = no force.")]
    public float impactForce = 0f;

    [Tooltip("When the obstacle that killed the player was a MoveCubeX, use its actual travel velocity (scaled by this) instead of just -transform.forward for the impact push, so the knock direction matches how the cube was actually moving.")]
    public float moveCubeVelocityForceMultiplier = 1.5f;

    [Header("Objects To Disable On Death")]
    [Tooltip("Turned off the moment an obstacle is hit: normal player model/mesh, weapon, HUD, whatever shouldn't be visible during the ragdoll.")]
    public GameObject[] objectsToDisable;

    [Header("Player Control")]
    [Tooltip("Leave empty to auto-grab from this GameObject.")]
    public PlayerControllerRoblox playerController;

    [Header("Camera")]
    [Tooltip("Leave empty to auto-find via FindObjectOfType. Pulled back to a wider cinematic angle the moment the player dies.")]
    public CameraFollow cameraFollow;

    [Header("Fail Screen")]
    public float failDelay = 1f;

    [Header("Death Sound")]
    [Tooltip("One-shot clip played the instant the player dies.")]
    public AudioClip deathSoundClip;
    [Range(0f, 1f)] public float deathSoundVolume = 1f;
    [Tooltip("If true, the death sound is played in 3D space at the death position (spatialBlend = 1). If false, it plays as flat 2D audio.")]
    public bool deathSound3D = false;

    private CharacterController characterController;
    private bool triggered;

    [Header("Respawn")]
    public float respawnDelay = 2f;

    private Transform[] ragdollBones;
    private Vector3[] defaultLocalPos;
    private Quaternion[] defaultLocalRot;
    void Start()
    {

        ragdollBones = ragdoll.GetComponentsInChildren<Transform>(true);

        defaultLocalPos = new Vector3[ragdollBones.Length];
        defaultLocalRot = new Quaternion[ragdollBones.Length];

        for (int i = 0; i < ragdollBones.Length; i++)
        {
            defaultLocalPos[i] = ragdollBones[i].localPosition;
            defaultLocalRot[i] = ragdollBones[i].localRotation;
        }

        if (playerController == null)
            playerController = GetComponent<PlayerControllerRoblox>();

        characterController = GetComponent<CharacterController>();

        if (cameraFollow == null)
            cameraFollow = FindObjectOfType<CameraFollow>();

        if (ragdoll != null)
            ragdoll.SetActive(false);
    }

    private bool IsObstacle(Collider col, out MoveCubeX moveCube)
    {
        moveCube = treatMoveCubesAsObstacles ? col.GetComponent<MoveCubeX>() : null;
        return col.CompareTag(obstacleTag) || moveCube != null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsObstacle(other, out MoveCubeX moveCube))
            Die(moveCube);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (IsObstacle(hit.collider, out MoveCubeX moveCube))
            Die(moveCube);
    }

    public void Die(MoveCubeX sourceMoveCube = null)
    {
        if (triggered)
            return;
        triggered = true;

        // Stop the normal controller from doing anything else this run —
        // full disable, not just canControl = false, so its Update() (and
        // the CharacterController collider) stop entirely and can't fight
        // the ragdoll physics.
        if (playerController != null)
            playerController.enabled = false;

        if (characterController != null)
            characterController.enabled = false;

        // Kill the player's own AudioSource (footstep loop, etc.) immediately
        // on death — otherwise it can keep looping over the death sound and
        // through the whole ragdoll/fail sequence since playerController
        // being disabled stops its Update() but not any sound already playing.
        if (playerController != null && playerController.footstepSource != null)
            playerController.footstepSource.Stop();

        PlayDeathSound();

        if (ragdoll != null)
        {
            if (matchPlayerTransformOnRagdoll)
            {
                ragdoll.transform.position = transform.position;
                ragdoll.transform.rotation = transform.rotation;
            }

            Rigidbody[] bodies = ragdoll.GetComponentsInChildren<Rigidbody>(true);

            foreach (Rigidbody body in bodies)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;

                body.Sleep();
            }

            for (int i = 0; i < ragdollBones.Length; i++)
            {
                ragdollBones[i].localPosition = defaultLocalPos[i];
                ragdollBones[i].localRotation = defaultLocalRot[i];
            }

            ragdoll.SetActive(true);


            foreach (Rigidbody body in bodies)
            {
                body.WakeUp();
            }

            if (cameraFollow != null)
            {
                cameraFollow.SwitchToRagdollTarget();
                cameraFollow.TriggerCinematicPullback(cameraFollow.ragdollTarget);
            }

            if (impactForce > 0f || sourceMoveCube != null)
            {
                // Prefer the cube's real travel direction/speed when a
                // MoveCubeX killed us, so the ragdoll flies the way the cube
                // was actually moving instead of always flying backward from
                // wherever the player happened to be facing.
                Vector3 pushDirection = sourceMoveCube != null
                    ? sourceMoveCube.Velocity * moveCubeVelocityForceMultiplier
                    : -transform.forward * impactForce;

                Vector3 upKick = Vector3.up * (impactForce > 0f ? impactForce * 0.25f : 2f);

                Rigidbody[] ragdollBodies = ragdoll.GetComponentsInChildren<Rigidbody>();
                for (int i = 0; i < ragdollBodies.Length; i++)
                {
                    ragdollBodies[i].AddForce(pushDirection + upKick, ForceMode.Impulse);
                }
            }
        }

        for (int i = 0; i < objectsToDisable.Length; i++)
        {
            if (objectsToDisable[i] != null)
                objectsToDisable[i].SetActive(false);
        }

        if (cameraFollow != null)
            cameraFollow.TriggerCinematicPullback(ragdoll != null ? ragdoll.transform : null);

        if (playerController != null &&
     playerController.respawnCount < playerController.maxRespawns)
        {
            playerController.respawnCount++;
            playerController.UpdateChancesUI();

            if (playerController.respawnCount >= playerController.maxRespawns)
            {
                StartCoroutine(ShowFailScreenAfterDelay());
            }
            else
            {
                StartCoroutine(RespawnAfterDelay());
            }
        }
        else
        {
            StartCoroutine(ShowFailScreenAfterDelay());
        }
    }

    /// <summary>
    /// Spawns a fresh, dedicated AudioSource just for the death one-shot and
    /// destroys it once the clip finishes. Kept separate from the player's
    /// own AudioSource (which we just stopped above) so the death sound
    /// can't be cut off by anything that touches the player's audio again
    /// during the ragdoll/respawn sequence, and so it keeps playing even if
    /// this GameObject or the player model gets deactivated mid-clip.
    /// </summary>
    private void PlayDeathSound()
    {
        if (deathSoundClip == null)
            return;

        GameObject deathSoundObj = new GameObject("DeathSound_OneShot");
        deathSoundObj.transform.position = transform.position;

        AudioSource deathAudioSource = deathSoundObj.AddComponent<AudioSource>();
        deathAudioSource.clip = deathSoundClip;
        deathAudioSource.volume = deathSoundVolume;
        deathAudioSource.spatialBlend = deathSound3D ? 1f : 0f;
        deathAudioSource.playOnAwake = false;
        deathAudioSource.Play();

        Destroy(deathSoundObj, deathSoundClip.length + 0.1f);
    }

    private System.Collections.IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        // Hide ragdoll
        if (ragdoll != null)
            ragdoll.SetActive(false);

        // Show player again
        foreach (GameObject obj in objectsToDisable)
        {
            if (obj != null)
                obj.SetActive(true);
        }

        // Reset camera
        if (cameraFollow != null)
            cameraFollow.ResetCinematicPullback();
        cameraFollow.SwitchToMainPlayer();

        // Re-enable player
        if (playerController != null)
        {
            playerController.enabled = true;
            playerController.Respawn();
        }

        if (characterController != null)
            characterController.enabled = true;

        triggered = false;

        if (characterController != null)
            characterController.enabled = true;

        MoveCubeX[] cubes = FindObjectsByType<MoveCubeX>(FindObjectsSortMode.None);

        foreach (MoveCubeX cube in cubes)
        {
            cube.ResetCube();
        }

        triggered = false;
    }

    private System.Collections.IEnumerator ShowFailScreenAfterDelay()
    {
        yield return new WaitForSeconds(failDelay);
        GameManager.Instance.LevelFailed();
    }
}