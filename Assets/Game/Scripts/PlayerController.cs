using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    public float jumpForce = 5f;

    public Transform groundCheck;
    public float groundCheckRadius = 0.25f;
    public LayerMask groundMask;

    public float fallY = -8f;

    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (groundCheck == null)
        {
            // create a simple groundCheck
            GameObject g = new GameObject("GroundCheck");
            g.transform.SetParent(transform);
            g.transform.localPosition = new Vector3(0, -0.5f, 0);
            groundCheck = g.transform;
        }
    }

    void Update()
    {
        Move();

        if (transform.position.y < fallY)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowGameOver();
        }
    }

    void Move()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 desired = new Vector3(h, 0, v).normalized * speed;
        // preserve vertical velocity
        rb.linearVelocity = new Vector3(desired.x, rb.linearVelocity.y, desired.z);

        if (Input.GetButtonDown("Jump") && IsGrounded())
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }
    }

    bool IsGrounded()
    {
        return Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask);
    }

    void OnCollisionEnter(Collision collision)
    {
        // add score when landing on a pulpit (optional)
        if (collision.gameObject.CompareTag("Pulpit"))
        {
            if (UIManager.Instance != null) UIManager.Instance.AddScore(1);
        }
    }
}
