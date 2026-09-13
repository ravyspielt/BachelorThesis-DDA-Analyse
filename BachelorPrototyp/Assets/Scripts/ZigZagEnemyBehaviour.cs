using UnityEngine;

public class ZigZagEnemyBehaviour : EnemyBase
{
    [Header("Zigzag Settings")]
    public float moveSpeed = 3f;          
    public float zigzagFrequency = 4f;    
    public float zigzagAmplitude = 2f;    

    protected override void HandleMovement()
    {
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            return; 
        }
        
        Vector2 directionToPlayer = GetSteeringDirection();

        Vector2 perpendicularDir = new Vector2(-directionToPlayer.y, directionToPlayer.x);

        float zigzagOffset = Mathf.Sin(Time.time * zigzagFrequency) * zigzagAmplitude;

        Vector2 finalVelocity = (directionToPlayer * moveSpeed * speedScale) + (perpendicularDir * zigzagOffset);
        _rb.linearVelocity = finalVelocity;
    }
}