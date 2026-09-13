using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Rigidbody2D _rb;

    public Vector2 flightDirection;
    public float projectleSpeed = 1;
    public float damage = 1;

    public float lifeTime = 7f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        _rb.linearVelocity = flightDirection * projectleSpeed;
    }
}
