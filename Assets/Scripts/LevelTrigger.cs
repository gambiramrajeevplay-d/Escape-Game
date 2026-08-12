using UnityEngine;
using System.Collections;

public class LevelTrigger : MonoBehaviour
{
    [Header("Player Animation")]
    public Animator playerAnimator;

    [Tooltip("Animation Trigger/Bool name to play on level complete.")]
    public string winAnimation = "Win";

    [Header("Cut Camera")]
    [Tooltip("Automatically finds the CutCameraController, even if CutCam is disabled.")]
    public Camera cutCamera;

    [Tooltip("Delay before starting the cinematic camera.")]
    public float cutCameraDelay = 0f;

    [Tooltip("Automatically disable CutCam when the scene starts.")]
    public bool disableCutCameraOnStart = true;

    private CutCameraController cutCameraController;

    private bool triggered;
    private PlayerControllerRoblox playerController;

    [Header("Respawn")]
    public float respawnDelay = 0.5f;

    private ObstacleRagdollDeath ragdollDeath;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        // Find the CutCameraController even if the CutCam
        // GameObject is already disabled.
        FindCutCameraController();

        if (disableCutCameraOnStart)
        {
            DisableCutCamera();
        }
    }

    // =========================================================
    // FIND CUT CAMERA
    // =========================================================

    private void FindCutCameraController()
    {
        // Find inactive CutCameraController too.
        CutCameraController[] cameras =
            Resources.FindObjectsOfTypeAll<CutCameraController>();

        foreach (CutCameraController controller in cameras)
        {
            if (controller == null)
                continue;

            // Ignore assets/prefabs that are not actually
            // part of the current scene.
            if (!controller.gameObject.scene.IsValid())
                continue;

            cutCameraController = controller;

            cutCamera =
                controller.GetComponent<Camera>();

            Debug.Log(
                "LevelTrigger: CutCam found through " +
                "CutCameraController: " +
                controller.gameObject.name,
                this
            );

            return;
        }

        Debug.LogWarning(
            "LevelTrigger: Could not find a " +
            "CutCameraController in the scene.",
            this
        );
    }

    // =========================================================
    // TRIGGER
    // =========================================================

    private void OnTriggerEnter(Collider other)
    {
        TryTrigger(other);
    }

    public void TryTrigger(Collider other)
    {
        if (triggered)
            return;

        if (!other.CompareTag("MainPlayer"))
            return;

        triggered = true;

        // ---------------------------------------------------------
        // RESOLVE PLAYER REFERENCES
        // ---------------------------------------------------------

        PlayerControllerRoblox controllerToUse =
            playerController != null
                ? playerController
                : other.GetComponentInParent<PlayerControllerRoblox>();

        Animator animatorToUse =
            playerAnimator != null
                ? playerAnimator
                : other.GetComponentInParent<Animator>();

        ObstacleRagdollDeath ragdollToUse =
            ragdollDeath != null
                ? ragdollDeath
                : other.GetComponentInParent<ObstacleRagdollDeath>();

        if (playerController == null)
            playerController = controllerToUse;

        if (playerAnimator == null)
            playerAnimator = animatorToUse;

        if (ragdollDeath == null)
            ragdollDeath = ragdollToUse;

        // ---------------------------------------------------------
        // STOP PLAYER CONTROL
        // ---------------------------------------------------------

        if (controllerToUse != null)
        {
            controllerToUse.canControl = false;

            if (controllerToUse.footstepSource != null)
            {
                controllerToUse.footstepSource.Stop();
                controllerToUse.footstepSource.loop = false;
            }
        }

        // ---------------------------------------------------------
        // PASS
        // ---------------------------------------------------------

        if (CompareTag("Pass Trigger"))
        {
            StartCoroutine(
                PassSequence(animatorToUse)
            );
        }

        // ---------------------------------------------------------
        // FAIL
        // ---------------------------------------------------------

        else if (CompareTag("Fail Trigger"))
        {
            HandleFail(
                controllerToUse,
                other
            );
        }
    }

    // =========================================================
    // PASS SEQUENCE
    // =========================================================

    private IEnumerator PassSequence(
        Animator animatorToUse)
    {
        // Optional delay.
        if (cutCameraDelay > 0f)
        {
            yield return new WaitForSeconds(
                cutCameraDelay
            );
        }

        // ---------------------------------------------------------
        // FIND CUT CAMERA AGAIN IF NEEDED
        // ---------------------------------------------------------

        if (cutCameraController == null)
        {
            FindCutCameraController();
        }

        // ---------------------------------------------------------
        // ENABLE CUT CAMERA
        // ---------------------------------------------------------

        EnableCutCamera();

        // ---------------------------------------------------------
        // START CAMERA CINEMATIC
        // ---------------------------------------------------------

        if (cutCameraController != null)
        {
            cutCameraController.StartCinematic(
                playerController != null
                    ? playerController.transform
                    : null
            );
        }
        else
        {
            Debug.LogWarning(
                "LevelTrigger: CutCameraController not found.",
                this
            );
        }

        // ---------------------------------------------------------
        // PLAY WIN ANIMATION
        // ---------------------------------------------------------

        if (animatorToUse != null)
        {
            animatorToUse.SetTrigger(
                winAnimation
            );
        }

        // ---------------------------------------------------------
        // LEVEL PASSED
        // ---------------------------------------------------------

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LevelPassed();
        }
    }

    // =========================================================
    // FAIL
    // =========================================================

    private void HandleFail(
        PlayerControllerRoblox controllerToUse,
        Collider other)
    {
        if (controllerToUse == null)
            return;

        controllerToUse.respawnCount++;

        if (controllerToUse.respawnCount >=
            controllerToUse.maxRespawns)
        {
            if (ragdollDeath == null)
            {
                ragdollDeath =
                    other.GetComponentInParent<
                        ObstacleRagdollDeath>();
            }

            if (ragdollDeath != null)
            {
                ragdollDeath.Die();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.LevelFailed();
            }
        }
        else
        {
            StartCoroutine(
                RespawnAfterDelay(
                    controllerToUse
                )
            );
        }
    }

    // =========================================================
    // ENABLE CUT CAMERA
    // =========================================================

    private void EnableCutCamera()
    {
        // If reference was lost, find it again.
        if (cutCameraController == null)
        {
            FindCutCameraController();
        }

        if (cutCameraController == null)
        {
            Debug.LogWarning(
                "LevelTrigger: CutCameraController not found.",
                this
            );

            return;
        }

        // Get camera from the controller's GameObject.
        if (cutCamera == null)
        {
            cutCamera =
                cutCameraController.GetComponent<Camera>();
        }

        // IMPORTANT:
        // The GameObject is currently inactive.
        // Enable it FIRST.
        cutCameraController.gameObject.SetActive(true);

        // Then enable the Camera component.
        if (cutCamera != null)
        {
            cutCamera.enabled = true;
        }

        Debug.Log(
            "LevelTrigger: CutCam ENABLED.",
            this
        );
    }

    // =========================================================
    // DISABLE CUT CAMERA
    // =========================================================

    private void DisableCutCamera()
    {
        if (cutCameraController != null)
        {
            cutCameraController.gameObject.SetActive(false);
        }
        else if (cutCamera != null)
        {
            cutCamera.enabled = false;
            cutCamera.gameObject.SetActive(false);
        }
    }

    // =========================================================
    // RESPAWN
    // =========================================================

    private IEnumerator RespawnAfterDelay(
        PlayerControllerRoblox controllerToUse)
    {
        yield return new WaitForSeconds(
            respawnDelay
        );

        if (controllerToUse != null)
        {
            controllerToUse.Respawn();
        }

        triggered = false;
    }
}