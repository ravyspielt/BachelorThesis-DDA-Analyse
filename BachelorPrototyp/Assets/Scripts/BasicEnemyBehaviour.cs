using UnityEngine;

public class BasicEnemyBehaviour : EnemyBase
{
    public float moveSpeed = 3f;      
    protected override void HandleMovement()
    {
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            return; 
        }
        _rb.linearVelocity = GetSteeringDirection() * moveSpeed * speedScale;
    }
}