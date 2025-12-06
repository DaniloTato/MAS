using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class RobotCollisionAvoidance : MonoBehaviour
{
    public Transform targetObject;
    public float returnSpeed = 0.3f;
    public float rotationSpeed = 5f;

    private Rigidbody rb;
    private Collider col;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void FixedUpdate()
    {
        if (!targetObject) return;

        Vector3 curr = rb.position;
        Vector3 targetPos = targetObject.position;
        targetPos.y = curr.y;

        Vector3 moveDir = targetPos - curr;
        Vector3 desiredMove = moveDir * returnSpeed;

        Vector3 lookDir = moveDir;
        lookDir.y = 0;

        if (lookDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            rb.MoveRotation(Quaternion.Slerp(
                rb.rotation,
                targetRot,
                rotationSpeed * Time.deltaTime
            ));
        }

        if (!IsBlocked(desiredMove))
        {
            rb.MovePosition(curr + desiredMove);
        }
    }

    private bool IsBlocked(Vector3 movement)
    {
        if (movement.sqrMagnitude < 0.000001f) return false;

        Vector3 dir = movement.normalized;
        float dist = movement.magnitude;

        return Physics.CapsuleCast(
            col.bounds.center + Vector3.up * 0.1f,
            col.bounds.center + Vector3.down * 0.1f,
            Mathf.Min(col.bounds.extents.x, col.bounds.extents.z),
            dir,
            out _,
            dist
        );
    }
}