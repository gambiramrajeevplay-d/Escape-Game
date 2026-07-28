using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerJump : MonoBehaviour
{
    [Header("Jump")]
    public float jumpForce = 4f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody rb;
    private Animator animator;

    private bool isGrounded;
    private bool canJump = true;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundRadius,
            groundLayer,
            QueryTriggerInteraction.Ignore);

        // Landed
        if (isGrounded && rb.velocity.y <= 0.1f)
        {
            canJump = true;
            animator.SetBool("isJumping", false);
        }

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && canJump)
        {
            canJump = false;

            animator.SetBool("isJumping", true);

            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}