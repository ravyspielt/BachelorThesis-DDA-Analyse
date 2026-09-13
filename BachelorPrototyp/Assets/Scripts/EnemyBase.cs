using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    [Header("General Settings")]
    public float initHealth = 10f;
    public float colorAnimationDuration = 1f;
    public float damage = 1f;

    [Header("Knockback Settings")]
    public float knockbackForce = 8f;
    public float knockbackDuration = 0.15f;
    protected float knockbackTimer = 0f;

    [Header("DDA Scaling")]
    [Tooltip("Wie stark der Schwierigkeits-Multiplikator auf das Tempo durchschlaegt. 1 = voll, 0 = gar nicht.")]
    [SerializeField] private float speedScaleInfluence = 0.6f;

    [Header("Formation")]
    [Tooltip("Radius um den Spieler, innerhalb dessen jeder Gegner seinen eigenen Zielpunkt bekommt. 0 laesst alle direkt auf den Spieler zielen, die Verteilung uebernimmt dann allein die Separation.")]
    [SerializeField] private float spreadRadius = 0f;
    [Tooltip("Ab welchem Abstand ein Nachbar abgestossen wird. 0 schaltet die Separation ab.")]
    [SerializeField] private float separationRadius = 1.2f;
    [Tooltip("Wie stark die Abstossung gegenueber der Zielrichtung gewichtet wird.")]
    [SerializeField] private float separationWeight = 1.5f;

    private static readonly List<Collider2D> neighbourBuffer = new List<Collider2D>(16);

    protected float speedScale = 1f;
    private float baseHealth;
    private Vector2 targetOffset;
    private ContactFilter2D separationFilter;

    protected float health;
    protected Rigidbody2D _rb;
    protected SpriteRenderer spriteRenderer;
    protected GameObject player;
    private bool isDead = false;

    private Coroutine fadeCoroutine;
    protected bool goingToRed = true;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseHealth = initHealth;
        health = initHealth;
        player = GameObject.FindGameObjectWithTag("Player");
        targetOffset = Random.insideUnitCircle * spreadRadius;

        separationFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = 1 << gameObject.layer
        };
    }

   
    protected Vector2 GetDirectionToTarget()
    {
        Vector2 target = (Vector2)player.transform.position + targetOffset;
        return (target - (Vector2)transform.position).normalized;
    }

    protected Vector2 GetSeparation()
    {
        if (separationRadius <= 0f || separationWeight <= 0f)
        {
            return Vector2.zero;
        }

        Vector2 position = transform.position;
        Physics2D.OverlapCircle(position, separationRadius, separationFilter, neighbourBuffer);

        Vector2 push = Vector2.zero;

        for (int i = 0; i < neighbourBuffer.Count; i++)
        {
            Collider2D neighbour = neighbourBuffer[i];

            if (neighbour == null || neighbour.gameObject == gameObject)
            {
                continue;
            }

            Vector2 away = position - (Vector2)neighbour.transform.position;
            float distance = away.magnitude;

            if (distance < 0.001f)
            {
                push += Random.insideUnitCircle.normalized;
                continue;
            }

            push += away / distance * (1f - distance / separationRadius);
        }

        return push;
    }

    protected Vector2 GetSteeringDirection()
    {
        Vector2 toTarget = GetDirectionToTarget();
        Vector2 steering = toTarget + GetSeparation() * separationWeight;

        return steering.sqrMagnitude > 0.0001f ? steering.normalized : toTarget;
    }

    public virtual void ApplyDifficultyScaling(float multiplier)
    {
        speedScale = 1f + (multiplier - 1f) * speedScaleInfluence;
        initHealth = Mathf.Max(1f, Mathf.Round(baseHealth * multiplier));
        health = initHealth;
    }

    protected virtual void FixedUpdate()
    {
        if (player == null) return;
        HandleMovement();
    }

    protected abstract void HandleMovement();

    private IEnumerator ChangeOpacity()
    {
        if (isDead)
        {
            GameManager.Instance.AddKill();
            Destroy(gameObject);
            yield break;
        }

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
            isDead = true;
            Destroy(gameObject);
        }

        spriteRenderer.color = targetColor;
        fadeCoroutine = null;
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Projectile"))
        {
            if (isDead)
            {
                Destroy(collision.gameObject);
                return;
            }

            Projectile projectile = collision.gameObject.GetComponent<Projectile>();

            if (projectile != null)
            {
                health -= projectile.damage;

                Vector2 hitDirection = (transform.position - collision.transform.position).normalized;

                if (_rb != null)
                {
                    _rb.AddForce(hitDirection * knockbackForce, ForceMode2D.Impulse);
                    knockbackTimer = knockbackDuration;
                }
            }

            Destroy(collision.gameObject);

            if (health <= 0)
            {
                damage = 0;
                isDead = true;
            }

            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(ChangeOpacity());
        }
    }


}
