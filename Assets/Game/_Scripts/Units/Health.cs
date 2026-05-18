using System;
using _Scripts.Creatures;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private UnitAnimator animator;
    [SerializeField] private float maxHealth;
    [SerializeField] private float currentHealth;
    public SliderController healthBar;

    public event Action onDamageTaken;
    public event Action onDeath;
    public event Action onHealed;

    [SerializeField] private MoreMountains.Feedbacks.MMF_Player healFeedback;

    /// <summary>
    /// When true, death animation is deferred until ExecutePostponedDeath() is called.
    /// </summary>
    public bool PostponeDeath { get; set; }
    private bool deathPostponed;

    public void Init(float maxHp)
    {
        maxHealth = maxHp;
        currentHealth = maxHp;
        healthBar.SetValue(currentHealth, maxHealth);
    }

    void Start()
    {
        onDamageTaken += () =>
        {
            if (!IsDead())
                healthBar.gameObject.SetActive(true);
        };

        onDeath += () =>
        {
            animator.PlayDead();
            healthBar.gameObject.SetActive(false);
        };
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        healthBar.SetValue(currentHealth, maxHealth);

        if (IsDead())
        {
            if (PostponeDeath)
            {
                deathPostponed = true;
                healthBar.gameObject.SetActive(false);
            }
            else
            {
                onDeath?.Invoke();
            }
        }
        else
        {
            onDamageTaken?.Invoke();
        }
    }

    /// <summary>
    /// Call this to trigger the deferred death (after tank finishes its attack).
    /// </summary>
    public void ExecutePostponedDeath()
    {
        if (!deathPostponed) return;
        deathPostponed = false;
        PostponeDeath = false;
        onDeath?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead() || amount <= 0f) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        healthBar.SetValue(currentHealth, maxHealth);
        onHealed?.Invoke();
        if (healFeedback != null) healFeedback.PlayFeedbacks();
    }

    public float CurrentHealth => currentHealth;

    public bool IsDead()
    {
        return currentHealth <= 0;
    }
}