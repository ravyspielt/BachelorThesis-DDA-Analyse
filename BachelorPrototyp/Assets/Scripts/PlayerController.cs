using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{

    private Rigidbody2D _rb;
    private Vector2 moveInput;

    private Vector2 aimDirection;

    public GameObject projectlie;

    public float speed = 1;

    public float shootCooldown = 0.46f;

    public float initHealth = 10;

    public GameObject directionIndicatior;
    private float shootCooldownTimer = 1;
    private Quaternion targetRotation;
    private Coroutine fadeCoroutine;
    private enum ActiveAimInput { Mouse, Controller };

    [Header("Dash Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private float dashCooldownTimer = 0f;
    private bool isDashing = false;
    private Vector2 dashDirection;
    private float health;
    private SpriteRenderer spriteRenderer;
    public float colorAnimationDuration = 1f;
    [SerializeField] private float respawnInvulnerabilityDuration = 1.5f;
    private Vector3 spawnPosition;
    private bool isInvulnerable;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        shootCooldownTimer = shootCooldown;
        health = initHealth;
        spawnPosition = transform.position;

        if (respawnInvulnerabilityDuration <= 0f)
        {
            respawnInvulnerabilityDuration = 1.5f;
        }
    }
    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.BlocksGameplayInput)
        {
            moveInput = Vector2.zero;
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (!isDashing)
        {
            _rb.linearVelocity = moveInput;
        }

        if (shootCooldownTimer < shootCooldown)
        {
            shootCooldownTimer += Time.deltaTime;
        }

        if (aimDirection.magnitude > 0.1f)
        {
            directionIndicatior.transform.rotation = Quaternion.Slerp(
                directionIndicatior.transform.rotation,
                targetRotation,
                15f * Time.deltaTime
            );
        }
    }

    void FixedUpdate()
    {

    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance != null && GameManager.Instance.BlocksGameplayInput)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = ctx.ReadValue<Vector2>() * speed;
        if (moveInput.magnitude > 0.1f)
        {

        }
    }

    public void OnShoot()
    {
        if (GameManager.Instance != null && GameManager.Instance.BlocksGameplayInput)
        {
            return;
        }

        if (shootCooldownTimer >= shootCooldown)
        {

            GameObject spawnedProjectile = Instantiate(projectlie, transform.position, Quaternion.identity);
            Projectile porjectileScript = spawnedProjectile.GetComponent<Projectile>();

            if (porjectileScript != null)
            {
                porjectileScript.flightDirection = directionIndicatior.transform.up.normalized;
                spawnedProjectile.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(porjectileScript.flightDirection.y, porjectileScript.flightDirection.x) * Mathf.Rad2Deg - 90);
            }
            shootCooldownTimer = 0;
        }
    }

    public void OnMouseInput(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance != null && GameManager.Instance.BlocksGameplayInput)
        {
            return;
        }

        Vector2 mouseScreenPosition = ctx.ReadValue<Vector2>();
        if (Camera.main == null || directionIndicatior == null)
        {
            return;
        }

        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        Vector2 directionMouseToPlayer = mouseWorldPosition - directionIndicatior.transform.position;
        if (directionMouseToPlayer.magnitude > 0.1f)
        {
            aimDirection = directionMouseToPlayer;
        }

        if (aimDirection.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg - 90f;
            targetRotation = Quaternion.Euler(0, 0, angle);
        }
    }

    public void OnJoyStickInput(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance != null && GameManager.Instance.BlocksGameplayInput)
        {
            return;
        }

        Vector2 inputVal = ctx.ReadValue<Vector2>();
        if (inputVal.magnitude > 0.1f)
        {
            aimDirection = inputVal;
        }
        if (aimDirection.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg - 90f;
            targetRotation = Quaternion.Euler(0, 0, angle);
        }
    }
    private IEnumerator PerformDash()
    {
        isDashing = true;
        dashCooldownTimer = dashCooldown;

        if (moveInput.magnitude > 0.1f)
        {
            dashDirection = moveInput.normalized;
        }
        else
        {
            dashDirection = directionIndicatior.transform.up.normalized * -1;
        }

        float elapsedTime = 0f;

        while (elapsedTime < dashDuration)
        {
            _rb.linearVelocity = dashDirection * dashSpeed;
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        isDashing = false;
    }
    public void OnDash(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance != null && GameManager.Instance.BlocksGameplayInput)
        {
            return;
        }

        if (ctx.performed && dashCooldownTimer <= 0 && !isDashing)
        {
            StartCoroutine(PerformDash());
        }
    }

    public void OnPause(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.TogglePause();
    }

    private IEnumerator ChangeOpacity()
    {
        Color startColor = spriteRenderer.color;
        Color targetColor = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, health / initHealth);
        float elapsedTime = 0f;

        while (elapsedTime < colorAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float blend = Mathf.Clamp01(elapsedTime / colorAnimationDuration);
            spriteRenderer.color = Color.Lerp(startColor, targetColor, blend);
            yield return null;
        }
        if (health <= 0)
        {
            Respawn();
        }
        else
        {
            spriteRenderer.color = targetColor;
        }

        fadeCoroutine = null;
    }

    private void Respawn()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddDeath();
        }

        health = initHealth;
        transform.position = spawnPosition;
        _rb.linearVelocity = Vector2.zero;
        isDashing = false;
        spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f);
        StartCoroutine(InvulnerabilityCoroutine());
    }

    private IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(respawnInvulnerabilityDuration);
        isInvulnerable = false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy") && !isDashing && !isInvulnerable && fadeCoroutine == null)
        {
            health -= collision.gameObject.GetComponent<EnemyBase>().damage;

            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(ChangeOpacity());
        }

    }

}
