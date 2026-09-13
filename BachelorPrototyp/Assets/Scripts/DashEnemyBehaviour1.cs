using UnityEngine;

public class DashEnemyBehaviour : EnemyBase
{
    [Header("Dash Settings")]
    public float normalSpeed = 1.5f;
    public float dashSpeed = 10f;
    public float dashDuration = 0.3f; 
    public float dashCooldown = 3f;

    private bool isDashing = false;
    private float dashTimer = 0f;
    private float cooldownTimer = 0f;
    private Vector2 dashDirection;

    protected override void HandleMovement()
    {
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            return; 
        }
        
        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            _rb.linearVelocity = dashDirection * dashSpeed * speedScale;

            if (dashTimer <= 0f)
            {
                isDashing = false;
                cooldownTimer = dashCooldown;
            }
        }
        else
        {
            cooldownTimer -= Time.fixedDeltaTime;

            _rb.linearVelocity = GetSteeringDirection() * normalSpeed * speedScale;

            if (cooldownTimer <= 0f)
            {
                StartDash(GetDirectionToTarget());
            }
        }
    }

    private void StartDash(Vector2 targetDirection)
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashDirection = targetDirection;
    }
}
