using UnityEngine;

/// <summary>
/// Roblox-style third person orbit camera for Unity 2021.
/// Right-click (or hold, depending on settings) and drag the mouse to orbit around the player.
/// Scroll wheel zooms in/out. Pair with PlayerController.cs.
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;              // the player to follow
    public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f); // roughly head height

    [Header("Orbit")]
    public float mouseSensitivity = 3f;
    public float minPitch = -35f;
    public float maxPitch = 70f;
    public bool requireRightMouseToLook = false; // false = always free-look, like default Roblox camera

    [Header("Zoom")]
    public float distance = 6f;
    public float minDistance = 1.5f;
    public float maxDistance = 12f;
    public float zoomSpeed = 4f;

    [Header("Collision")]
    public LayerMask collisionMask = ~0;
    public float collisionBuffer = 0.3f;

    private float yaw;
    private float pitch = 15f;

    void Start()
    {
        if (target != null)
            yaw = target.eulerAngles.y;

        Cursor.lockState = requireRightMouseToLook ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = requireRightMouseToLook;
    }

    void LateUpdate()
    {
        if (target == null) return;

        bool canLook = !requireRightMouseToLook || Input.GetMouseButton(1);

        if (canLook)
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + targetOffset;
        Vector3 desiredPosition = pivot - (rotation * Vector3.forward * distance);

        // Simple collision handling so the camera doesn't clip through walls
        float finalDistance = distance;
        if (Physics.Linecast(pivot, desiredPosition, out RaycastHit hit, collisionMask))
        {
            finalDistance = Mathf.Clamp(hit.distance - collisionBuffer, minDistance, maxDistance);
        }

        transform.position = pivot - (rotation * Vector3.forward * finalDistance);
        transform.rotation = rotation;
    }
}