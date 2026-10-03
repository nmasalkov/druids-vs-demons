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
    [Tooltip("Optional. Targetables without a bar (e.g. Shield) leave this empty.")]
    public HealthTextController healthText;
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
        RefreshDisplays();
    }

    private void RefreshDisplays()
    {
        if (healthBar != null) healthBar.SetValue(currentHealth, maxHealth);
        if (healthText != null) healthText.SetValue(currentHealth, maxHealth);
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
        RefreshDisplays();
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
    /// <summary>
    /// Restores HP (capped at max) and plays the heal feedback. <paramref name="clearsStatuses"/> false
    /// skips <see cref="onHealed"/> — which StatusesManager uses to clear Shock etc. — so a healing
    /// archer's shot (docs/Battle.md "Healing shots") heals without curing statuses, unlike the
    /// repeat-summon heal.
    /// </summary>
    public void Heal(float amount, bool clearsStatuses = true)
    {
        if (IsDead() || amount <= 0f) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        RefreshDisplays();
        if (clearsStatuses) onHealed?.Invoke();
        if (healFeedback != null) healFeedback.PlayFeedbacks();
    }
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercent => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsDead()
    {
        return currentHealth <= 0;
    }
}