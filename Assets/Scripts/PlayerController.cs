using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float rotationSpeed = 15f;

    private Rigidbody rb;
    private Animator animator;
    private Camera cam;

    private Vector3 moveDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        cam = Camera.main;

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;

        camForward.y = 0;
        camRight.y = 0;

        camForward.Normalize();
        camRight.Normalize();

        moveDirection = (camForward * v + camRight * h).normalized;

        animator.SetBool("isRunning", moveDirection.sqrMagnitude > 0.01f);
    }

    void FixedUpdate()
    {
        rb.velocity = new Vector3(
            moveDirection.x * moveSpeed,
            rb.velocity.y,
            moveDirection.z * moveSpeed);

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(moveDirection);

            rb.MoveRotation(Quaternion.Slerp(
                rb.rotation,
                rot,
                rotationSpeed * Time.fixedDeltaTime));
        }
    }
}