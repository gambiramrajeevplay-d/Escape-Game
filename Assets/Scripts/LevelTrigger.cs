using UnityEngine;
public class LevelTrigger : MonoBehaviour
{
    [Header("Player Animation")]
    public Animator playerAnimator;
    [Tooltip("Animation Trigger/Bool name to play on level complete.")]
    public string winAnimation = "Win";
    private bool triggered;
    private PlayerControllerRoblox playerController;
    [Header("Respawn")]
    public float respawnDelay = 0.5f;
    private ObstacleRagdollDeath ragdollDeath;
    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("MainPlayer");
        if (player != null)
        {
            if (playerAnimator == null)
                playerAnimator = player.GetComponent<Animator>();
            playerController = player.GetComponent<PlayerControllerRoblox>();
            ragdollDeath = player.GetComponent<ObstacleRagdollDeath>();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        TryTrigger(other);
    }
    /// <summary>
    /// The actual trigger-fire logic, pulled out of OnTriggerEnter so it can
    /// also be called manually by PlayerControllerRoblox's tunneling-safety
    /// sweep. On low-end/TV hardware a slow frame means a big Move() step,
    /// which can occasionally skip clean over a thin trigger volume within
    /// one frame — Unity's normal trigger check is discrete (once per frame)
    /// and never sees an overlap, so OnTriggerEnter silently doesn't fire.
    /// The sweep check in PlayerControllerRoblox catches that case and calls
    /// this directly instead.
    /// </summary>
    public void TryTrigger(Collider other)
    {
        if (triggered)
            return;
        if (!other.CompareTag("MainPlayer"))
            return;
        triggered = true;
        // Pull the controller straight off the collider that entered the trigger
        // instead of relying only on the Start() cache. If this LevelTrigger's
        // Start() ran before the player existed (spawn order, player instantiated
        // late, trigger object enabled later, etc.) playerController stayed null
        // forever, which silently skipped canControl/respawn logic entirely —
        // the player kept moving normally and nothing about "failing" ever
        // actually happened, which is exactly the symptom of respawn "not working".
        PlayerControllerRoblox controllerToUse = playerController != null
            ? playerController
            : other.GetComponent<PlayerControllerRoblox>();
        Animator animatorToUse = playerAnimator != null
            ? playerAnimator
            : other.GetComponent<Animator>();
        if (controllerToUse != null)
        {
            controllerToUse.canControl = false;

            // canControl = false stops HandleMovement from running, but
            // UpdateAnimator (and the UpdateFootsteps call inside it) still
            // run every frame off whatever currentMoveVelocity was last set
            // to — so without this, the footstep loop just keeps looping
            // forever after the player wins/fails instead of cutting off.
            if (controllerToUse.footstepSource != null)
                controllerToUse.footstepSource.Stop();
            if(controllerToUse.footstepSource != null)
                controllerToUse.footstepSource.loop = false;
        }
        if (CompareTag("Pass Trigger"))
        {
            if (animatorToUse != null)
                animatorToUse.SetTrigger(winAnimation);
            GameManager.Instance.LevelPassed();
        }
        else if (CompareTag("Fail Trigger"))
        {
            if (controllerToUse != null)
            {
                controllerToUse.respawnCount++;
                if (controllerToUse.respawnCount >= controllerToUse.maxRespawns)
                {
                    if (ragdollDeath == null)
                        ragdollDeath = other.GetComponent<ObstacleRagdollDeath>();
                    if (ragdollDeath != null)
                        ragdollDeath.Die();
                    else
                        GameManager.Instance.LevelFailed();
                }
                else
                {
                    StartCoroutine(RespawnAfterDelay(controllerToUse));
                }
            }
        }
    }
    private System.Collections.IEnumerator RespawnAfterDelay(PlayerControllerRoblox controllerToUse)
    {
        yield return new WaitForSeconds(respawnDelay);
        if (controllerToUse != null)
        {
            controllerToUse.Respawn();
        }
        triggered = false;
    }
}