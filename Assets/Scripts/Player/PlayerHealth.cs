using UnityEngine;
using Utils.Combat;

namespace Player
{
    /// <summary>
    /// Maintains player health, processes incoming damage, and triggers defeat flow for the ending screen.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamagable
    {
        #region Variables And Properties
        #region Serialized Fields
        [Tooltip("Maximum health value available to the player.")]
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [Tooltip("Health applied on spawn; falls back to max health when not specified.")]
        [SerializeField] private float startingHealth = 100f;
        [Tooltip("Disables incoming damage for debugging or invulnerability sequences.")]
        [SerializeField] private bool damageEnabled = true;
        #endregion

        #region Runtime State
        private float currentHealth;
        private bool defeated;
        private int defeatedHordes;
        #endregion
        #endregion

        #region Properties
        /// <summary>
        /// Returns the current health value.
        /// </summary>
        public float CurrentHealth
        {
            get { return currentHealth; }
        }

        /// <summary>
        /// Returns the configured maximum health.
        /// </summary>
        public float MaxHealth
        {
            get { return maxHealth; }
        }

        /// <summary>
        /// Returns the number of hordes defeated during the run.
        /// </summary>
        public int DefeatedHordes
        {
            get { return defeatedHordes; }
        }

        /// <summary>
        /// True after the player has been defeated.
        /// </summary>
        public bool IsDefeated
        {
            get { return defeated; }
        }
        #endregion

        #region Methods
        #region Unity
        /// <summary>
        /// Initializes health values and broadcasts the initial state.
        /// </summary>
        private void Awake()
        {
            ClampConfiguration();
            ResetHealth(false);
        }

        /// <summary>
        /// Re-synchronizes listeners when the component is enabled.
        /// </summary>
        private void OnEnable()
        {
            BroadcastHealth();
        }

        /// <summary>
        /// Validates serialized configuration whenever values change in the inspector.
        /// </summary>
        private void OnValidate()
        {
            ClampConfiguration();
        }
        #endregion

        #region Public
        /// <summary>
        /// Applies incoming damage and triggers defeat when health is depleted.
        /// </summary>
        public void ApplyDamage(IDamage damageSource, Vector3 hitPoint)
        {
            if (!damageEnabled || defeated)
                return;

            float damageAmount = damageSource != null ? Mathf.Max(0f, damageSource.DamageAmount) : 0f;
            if (damageAmount <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - damageAmount);
            EventsManager.InvokePlayerDamaged(damageSource, hitPoint, currentHealth);
            BroadcastHealth();

            if (currentHealth <= 0f)
                HandleDefeat();
        }

        /// <summary>
        /// Restores the player to full health and clears defeat flags.
        /// </summary>
        public void ResetHealth(bool broadcast = true)
        {
            defeated = false;
            currentHealth = startingHealth > 0f ? Mathf.Min(startingHealth, maxHealth) : maxHealth;
            if (broadcast)
                BroadcastHealth();
        }

        /// <summary>
        /// Registers a defeated horde to keep the ending panel accurate.
        /// </summary>
        public void RegisterHordeDefeat()
        {
            if (defeated)
                return;

            defeatedHordes++;
            EventsManager.InvokeIncreaseCompletedHordesCounter();
        }

        /// <summary>
        /// Enables or disables damage intake at runtime.
        /// </summary>
        public void SetDamageEnabled(bool enabled)
        {
            damageEnabled = enabled;
        }
        #endregion

        #region Internal
        /// <summary>
        /// Enforces valid ranges on serialized fields.
        /// </summary>
        private void ClampConfiguration()
        {
            if (maxHealth < 1f)
                maxHealth = 1f;

            if (startingHealth <= 0f)
                startingHealth = maxHealth;
        }

        /// <summary>
        /// Broadcasts current health through the global event pipeline.
        /// </summary>
        private void BroadcastHealth()
        {
            EventsManager.InvokePlayerHealthChanged(currentHealth, maxHealth);
        }

        /// <summary>
        /// Finalizes defeat flow and triggers the ending panel.
        /// </summary>
        private void HandleDefeat()
        {
            if (defeated)
                return;

            defeated = true;
            currentHealth = 0f;
            BroadcastHealth();
            EventsManager.InvokePlayerDeath();
            EventsManager.InvokeGameDefeatTriggered();
        }
        #endregion
        #endregion
    }
}
