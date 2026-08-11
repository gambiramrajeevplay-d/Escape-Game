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

    // No Start()-time lookup here on purpose. If the player is spawned at
    // runtime (e.g. by a spawner script) after this trigger's Start() runs,
    // GameObject.FindGameObjectWithTag("MainPlayer") returns null and these
    // references would stay null forever, silently skipping all pass/fail
    // logic. Instead, everything is resolved lazily the first time a trigger
    // actually fires, straight from the collider involved — see TryTrigger.

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

        // Resolve everything off the collider that actually entered, using
        // GetComponentInParent rather than GetComponent — if the player's
        // collider lives on a child object (a feet/capsule collider under a
        // root rig, say) while PlayerControllerRoblox sits on the parent,
        // a plain GetComponent would come back null and silently no-op the
        // whole trigger. Falls back to the cached fields first so repeated
        // triggers with the same already-known player don't redo the lookup.
        PlayerControllerRoblox controllerToUse = playerController != null
            ? playerController
            : other.GetComponentInParent<PlayerControllerRoblox>();

        Animator animatorToUse = playerAnimator != null
            ? playerAnimator
            : other.GetComponentInParent<Animator>();

        ObstacleRagdollDeath ragdollToUse = ragdollDeath != null
            ? ragdollDeath
            : other.GetComponentInParent<ObstacleRagdollDeath>();

        // Cache whatever we found so future triggers (this one resetting via
        // RespawnAfterDelay, or a different LevelTrigger later in the level)
        // skip the GetComponentInParent calls entirely.
        if (playerController == null) playerController = controllerToUse;
        if (playerAnimator == null) playerAnimator = animatorToUse;
        if (ragdollDeath == null) ragdollDeath = ragdollToUse;

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
            if (controllerToUse.footstepSource != null)
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
                        ragdollDeath = other.GetComponentInParent<ObstacleRagdollDeath>();
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