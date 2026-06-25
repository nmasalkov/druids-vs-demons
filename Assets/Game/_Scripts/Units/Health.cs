using System;
using _Scripts.Creatures;
using UnityEngine;
public class Health : MonoBehaviour
{
    [SerializeField] private UnitAnimator animator;
    [SerializeField] private float maxHealth;
    [SerializeField] private float currentHealth;
    [Tooltip("Optional. Targetables without a bar (e.g. Shield) leave this empty.")]
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
        if (healthBar != null) healthBar.SetValue(currentHealth, maxHealth);
    }
    void Start()
    {
        onDamageTaken += () =>
        {
            if (healthBar != null && !IsDead())
                healthBar.gameObject.SetActive(true);
        };
        onDeath += () =>
        {
            // animator is genuinely optional: Shield (and other non-Unit Targetables) drive
            // their own death visuals via their HandleDeath override.
            if (animator != null) animator.PlayDead();
            if (healthBar != null) healthBar.gameObject.SetActive(false);
        };
    }
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (healthBar != null) healthBar.SetValue(currentHealth, maxHealth);
        if (IsDead())
        {
            if (PostponeDeath)
            {
                deathPostponed = true;
                if (healthBar != null) healthBar.gameObject.SetActive(false);
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
        if (healthBar != null) healthBar.SetValue(currentHealth, maxHealth);
        onHealed?.Invoke();
        if (healFeedback != null) healFeedback.PlayFeedbacks();
    }
    public float CurrentHealth => currentHealth;
    public bool IsDead()
    {
        return currentHealth <= 0;
    }
}