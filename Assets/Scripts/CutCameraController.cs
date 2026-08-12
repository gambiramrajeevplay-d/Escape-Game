using UnityEngine;
using System.Collections;

public class CutCameraController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Player tag used to find the camera target.")]
    public string playerTag = "Player";

    [Tooltip("Player target. Automatically found using the Player Tag if empty.")]
    public Transform target;

    [Header("Camera Position")]
    [Tooltip("Starting distance from the player.")]
    public float distance = 7f;

    [Tooltip("Height of the camera above the player.")]
    public float height = 3.5f;

    [Header("Curved Movement")]
    [Tooltip("How far around the player the camera rotates.")]
    public float curveAngle = 100f;

    [Tooltip("How long the curved camera movement takes.")]
    public float curveDuration = 2.5f;

    [Tooltip("Controls how smooth the camera movement is.")]
    public AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Look At")]
    [Tooltip("Height offset used when looking at the player.")]
    public float lookAtHeight = 1.2f;

    [Tooltip("How smoothly the camera rotates toward the player.")]
    public float lookSmoothSpeed = 8f;

    [Header("Orbit")]
    [Tooltip("Direction of the camera orbit.")]
    public bool clockwise = true;

    [Tooltip("Automatically start cinematic when the camera is enabled.")]
    public bool playOnEnable = false;

    private bool cinematicPlaying;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        // CutCam should be disabled by default.
        if (!playOnEnable)
        {
            gameObject.SetActive(false);
        }
    }

    // =========================================================
    // ENABLE
    // =========================================================

    private void OnEnable()
    {
        FindPlayer();

        if (playOnEnable && target != null)
        {
            StartCinematic(target);
        }
    }

    // =========================================================
    // LATE UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (!cinematicPlaying || target == null)
            return;

        LookAtTarget();
    }

    // =========================================================
    // START CINEMATIC
    // =========================================================

    public void StartCinematic(Transform player)
    {
        if (player != null)
        {
            target = player;
        }

        // If no target was supplied, find it using the Player tag.
        if (target == null)
        {
            FindPlayer();
        }

        if (target == null)
        {
            Debug.LogWarning(
                "CutCameraController: No GameObject with tag '" +
                playerTag +
                "' was found."
            );

            return;
        }

        StopAllCoroutines();

        StartCoroutine(
            CurvedCameraMovement()
        );
    }

    // =========================================================
    // FIND PLAYER BY TAG
    // =========================================================

    private void FindPlayer()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag(playerTag);

        if (player != null)
        {
            target = player.transform;

            Debug.Log(
                "CutCameraController: Target found: " +
                player.name
            );
        }
        else
        {
            Debug.LogWarning(
                "CutCameraController: Could not find player with tag '" +
                playerTag +
                "'."
            );
        }
    }

    // =========================================================
    // CURVED CAMERA MOVEMENT
    // =========================================================

    private IEnumerator CurvedCameraMovement()
    {
        cinematicPlaying = true;

        Vector3 targetPosition =
            target.position;

        Vector3 startDirection =
            transform.position - targetPosition;

        // Remove vertical component so the orbit
        // happens horizontally around the player.
        Vector3 horizontalDirection =
            new Vector3(
                startDirection.x,
                0f,
                startDirection.z
            );

        // If camera is directly above the player,
        // use the opposite of player's forward direction.
        if (horizontalDirection.sqrMagnitude < 0.001f)
        {
            horizontalDirection =
                -target.forward;
        }

        horizontalDirection.Normalize();

        // -----------------------------------------------------
        // START ANGLE
        // -----------------------------------------------------

        float startAngle =
            Mathf.Atan2(
                horizontalDirection.x,
                horizontalDirection.z
            ) * Mathf.Rad2Deg;

        float direction =
            clockwise ? 1f : -1f;

        float endAngle =
            startAngle +
            curveAngle * direction;

        Vector3 originalPosition =
            transform.position;

        float elapsed = 0f;

        // -----------------------------------------------------
        // CURVED ORBIT
        // -----------------------------------------------------

        while (elapsed < curveDuration)
        {
            if (target == null)
            {
                cinematicPlaying = false;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsed / curveDuration
                );

            float curvedTime =
                movementCurve != null
                    ? movementCurve.Evaluate(
                        normalizedTime)
                    : normalizedTime;

            float currentAngle =
                Mathf.Lerp(
                    startAngle,
                    endAngle,
                    curvedTime
                );

            float angleRadians =
                currentAngle * Mathf.Deg2Rad;

            Vector3 orbitDirection =
                new Vector3(
                    Mathf.Sin(angleRadians),
                    0f,
                    Mathf.Cos(angleRadians)
                );

            Vector3 desiredPosition =
                target.position +
                orbitDirection * distance;

            desiredPosition.y =
                target.position.y + height;

            transform.position =
                Vector3.Lerp(
                    originalPosition,
                    desiredPosition,
                    curvedTime
                );

            LookAtTarget();

            yield return null;
        }

        // -----------------------------------------------------
        // FINAL POSITION
        // -----------------------------------------------------

        float finalAngle =
            endAngle * Mathf.Deg2Rad;

        Vector3 finalDirection =
            new Vector3(
                Mathf.Sin(finalAngle),
                0f,
                Mathf.Cos(finalAngle)
            );

        transform.position =
            target.position +
            finalDirection * distance +
            Vector3.up * height;

        LookAtTarget();

        cinematicPlaying = false;
    }

    // =========================================================
    // LOOK AT PLAYER
    // =========================================================

    private void LookAtTarget()
    {
        if (target == null)
            return;

        Vector3 lookPosition =
            target.position +
            Vector3.up * lookAtHeight;

        Vector3 direction =
            lookPosition - transform.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                lookSmoothSpeed *
                Time.unscaledDeltaTime
            );
    }
}