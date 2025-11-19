using System;
using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Defines a turret archetype with durability, fire control, placement, projectile linkage and pooling hooks.
    /// </summary>
    [CreateAssetMenu(fileName = "TurretClass", menuName = "Scriptables/Turrets/Turret Class")]
    public class TurretClassDefinition : ScriptableObject
    {
        #region Variables And Properties
        #region Serialized Fields

        [Header("Identity")]

        [SerializeField]
        [Tooltip("Unique identifier used to register this turret archetype across systems.")]
        private string key = "turret_default";

        [SerializeField]
        [Tooltip("Display name shown in UI when the turret is unlocked.")]
        private string displayName = "New Turret";

        [SerializeField]
        [Tooltip("Description visible in selection menus and build previews.")]
        [TextArea]
        private string description;

        [SerializeField]
        [Tooltip("Icon used for build menus and HUD elements.")]
        private Sprite icon;

        [Header("Prefabs And Pools")]

        [SerializeField]
        [Tooltip("Runtime prefab spawned for this turret. Must implement PooledTurret.")]
        private PooledTurret turretPrefab;

        [SerializeField]
        [Tooltip("Pool asset handling the lifecycle of turret instances.")]
        private TurretPoolSO turretPool;

        [SerializeField]
        [Tooltip("Projectile data assigned to this turret's fire modes.")]
        private ProjectileDefinition projectile;

        [SerializeField]
        [Tooltip("Pool asset used to spawn projectiles for this turret.")]
        private ProjectilePoolSO projectilePool;

        [Header("Stats")]

        [SerializeField]
        [Tooltip("Durability settings defining survivability.")]
        private DurabilitySettings durability = new DurabilitySettings(150f, 10f, 8f, 1.5f);

        [SerializeField]
        [Tooltip("Targeting and engagement configuration.")]
        private TargetingSettings targeting = new TargetingSettings(16f, 240f, 0.5f, 0.75f);

        [SerializeField]
        [Tooltip("Fire mode configuration for automatic targeting.")]
        private FireModeSettings automaticFire = new FireModeSettings(0.45f, 3, 0.08f, TurretFirePattern.Cone, 18f);

        [SerializeField]
        [Tooltip("Fire mode configuration for first-person free aim.")]
        private FireModeSettings freeAimFire = new FireModeSettings(0.25f, 1, 0.0f, TurretFirePattern.Consecutive, 0f);

        [SerializeField]
        [Tooltip("Time required by the player to assume manual control of the turret.")]
        private float modeSwitchSeconds = 1.0f;

        [SerializeField]
        [Tooltip("Resource footprint for building and maintaining the turret.")]
        private EconomySettings economy = new EconomySettings(120, 2, 1.5f, 0.85f);

        [SerializeField]
        [Tooltip("Reloading and heat management settings for sustained fire.")]
        private SustainSettings sustain = new SustainSettings(12, 3.5f, 0.45f, 0.65f);

        [SerializeField]
        [Tooltip("Placement rules relative to Grid3D buildable cells.")]
        private PlacementSettings placement = new PlacementSettings(0.45f, 0.08f, 0.15f, true);

        #endregion

        #region Properties

        public string Key
        {
            get { return key; }
        }

        public string DisplayName
        {
            get { return displayName; }
        }

        public string Description
        {
            get { return description; }
        }

        public Sprite Icon
        {
            get { return icon; }
        }

        public PooledTurret TurretPrefab
        {
            get { return turretPrefab; }
        }

        public TurretPoolSO TurretPool
        {
            get { return turretPool; }
        }

        public ProjectileDefinition Projectile
        {
            get { return projectile; }
        }

        public ProjectilePoolSO ProjectilePool
        {
            get { return projectilePool; }
        }

        public DurabilitySettings Durability
        {
            get { return durability; }
        }

        public TargetingSettings Targeting
        {
            get { return targeting; }
        }

        public FireModeSettings AutomaticFire
        {
            get { return automaticFire; }
        }

        public FireModeSettings FreeAimFire
        {
            get { return freeAimFire; }
        }

        public float ModeSwitchSeconds
        {
            get { return modeSwitchSeconds; }
        }

        public EconomySettings Economy
        {
            get { return economy; }
        }

        public SustainSettings Sustain
        {
            get { return sustain; }
        }

        public PlacementSettings Placement
        {
            get { return placement; }
        }

        #endregion
        #endregion

        #region Nested Types

        [Serializable]
        public struct DurabilitySettings
        {
            [SerializeField]
            [Tooltip("Maximum hit points of the turret chassis.")]
            private float health;

            [SerializeField]
            [Tooltip("Flat armor value reducing physical incoming damage.")]
            private float armor;

            [SerializeField]
            [Tooltip("Flat resistance reducing elemental or magical incoming damage.")]
            private float magicResistance;

            [SerializeField]
            [Tooltip("Percentage of health restored per second while idle.")]
            private float passiveRegenPerSecond;

            public float Health { get { return health; } }
            public float Armor { get { return armor; } }
            public float MagicResistance { get { return magicResistance; } }
            public float PassiveRegenPerSecond { get { return passiveRegenPerSecond; } }

            public DurabilitySettings(float health, float armor, float magicResistance, float passiveRegenPerSecond)
            {
                this.health = Mathf.Max(1f, health);
                this.armor = Mathf.Max(0f, armor);
                this.magicResistance = Mathf.Max(0f, magicResistance);
                this.passiveRegenPerSecond = Mathf.Max(0f, passiveRegenPerSecond);
            }
        }

        [Serializable]
        public struct TargetingSettings
        {
            [SerializeField]
            [Tooltip("Effective engagement range in meters.")]
            private float range;

            [SerializeField]
            [Tooltip("Degrees per second rotation speed when tracking targets.")]
            private float turnRate;

            [SerializeField]
            [Tooltip("Dead zone radius around the base where the turret will not fire.")]
            private float deadZoneRadius;

            [SerializeField]
            [Tooltip("Time in seconds between target reevaluations to avoid per-frame checks.")]
            private float retargetInterval;

            public float Range { get { return range; } }
            public float TurnRate { get { return turnRate; } }
            public float DeadZoneRadius { get { return deadZoneRadius; } }
            public float RetargetInterval { get { return retargetInterval; } }

            public TargetingSettings(float range, float turnRate, float deadZoneRadius, float retargetInterval)
            {
                this.range = Mathf.Max(0.5f, range);
                this.turnRate = Mathf.Max(0f, turnRate);
                this.deadZoneRadius = Mathf.Max(0f, deadZoneRadius);
                this.retargetInterval = Mathf.Max(0.05f, retargetInterval);
            }
        }

        [Serializable]
        public struct FireModeSettings
        {
            [SerializeField]
            [Tooltip("Seconds between shots for this fire mode.")]
            private float cadenceSeconds;

            [SerializeField]
            [Tooltip("Number of projectiles released per trigger event.")]
            private int projectilesPerShot;

            [SerializeField]
            [Tooltip("Delay between consecutive projectiles when the pattern is Consecutive.")]
            private float interProjectileDelay;

            [SerializeField]
            [Tooltip("Pattern used to position multiple projectiles.")]
            private TurretFirePattern pattern;

            [SerializeField]
            [Tooltip("Cone width in degrees when the pattern uses cone distribution.")]
            private float coneAngleDegrees;

            public float CadenceSeconds { get { return cadenceSeconds; } }
            public int ProjectilesPerShot { get { return projectilesPerShot; } }
            public float InterProjectileDelay { get { return interProjectileDelay; } }
            public TurretFirePattern Pattern { get { return pattern; } }
            public float ConeAngleDegrees { get { return coneAngleDegrees; } }

            public FireModeSettings(float cadenceSeconds, int projectilesPerShot, float interProjectileDelay, TurretFirePattern pattern, float coneAngleDegrees)
            {
                this.cadenceSeconds = Mathf.Max(0.02f, cadenceSeconds);
                this.projectilesPerShot = Mathf.Max(1, projectilesPerShot);
                this.interProjectileDelay = Mathf.Max(0f, interProjectileDelay);
                this.pattern = pattern;
                this.coneAngleDegrees = Mathf.Max(0f, coneAngleDegrees);
            }
        }

        [Serializable]
        public struct EconomySettings
        {
            [SerializeField]
            [Tooltip("Cost required to place this turret on the grid.")]
            private int buildCost;

            [SerializeField]
            [Tooltip("Maintenance cost per wave or per minute depending on economic rules.")]
            private int upkeepCost;

            [SerializeField]
            [Tooltip("Time in seconds required to refund the turret once sold.")]
            private float salvageDelay;

            [SerializeField]
            [Tooltip("Percentage of resources refunded when selling the turret.")]
            [Range(0f, 1f)]
            private float refundRatio;

            public int BuildCost { get { return buildCost; } }
            public int UpkeepCost { get { return upkeepCost; } }
            public float SalvageDelay { get { return salvageDelay; } }
            public float RefundRatio { get { return refundRatio; } }

            public EconomySettings(int buildCost, int upkeepCost, float salvageDelay, float refundRatio)
            {
                this.buildCost = Mathf.Max(0, buildCost);
                this.upkeepCost = Mathf.Max(0, upkeepCost);
                this.salvageDelay = Mathf.Max(0f, salvageDelay);
                this.refundRatio = Mathf.Clamp01(refundRatio);
            }
        }

        [Serializable]
        public struct SustainSettings
        {
            [SerializeField]
            [Tooltip("Number of shots before the turret must reload.")]
            private int magazineSize;

            [SerializeField]
            [Tooltip("Seconds needed to reload the magazine.")]
            private float reloadSeconds;

            [SerializeField]
            [Tooltip("Maximum heat accumulated during sustained fire.")]
            private float maxHeat;

            [SerializeField]
            [Tooltip("Seconds to dissipate heat back to zero once firing stops.")]
            private float heatDissipationSeconds;

            public int MagazineSize { get { return magazineSize; } }
            public float ReloadSeconds { get { return reloadSeconds; } }
            public float MaxHeat { get { return maxHeat; } }
            public float HeatDissipationSeconds { get { return heatDissipationSeconds; } }

            public SustainSettings(int magazineSize, float reloadSeconds, float maxHeat, float heatDissipationSeconds)
            {
                this.magazineSize = Mathf.Max(1, magazineSize);
                this.reloadSeconds = Mathf.Max(0f, reloadSeconds);
                this.maxHeat = Mathf.Max(0f, maxHeat);
                this.heatDissipationSeconds = Mathf.Max(0.01f, heatDissipationSeconds);
            }
        }

        [Serializable]
        public struct PlacementSettings
        {
            [SerializeField]
            [Tooltip("Radius in meters occupied by the turret footprint.")]
            private float footprintRadius;

            [SerializeField]
            [Tooltip("Clearance in meters around the turret base required to avoid overlap.")]
            private float clearance;

            [SerializeField]
            [Tooltip("Height offset applied when spawning the turret to avoid clipping with the ground.")]
            private float heightOffset;

            [SerializeField]
            [Tooltip("If true, the turret rotates to align with Grid3D cell axes when placed.")]
            private bool alignWithGrid;

            public float FootprintRadius { get { return footprintRadius; } }
            public float Clearance { get { return clearance; } }
            public float HeightOffset { get { return heightOffset; } }
            public bool AlignWithGrid { get { return alignWithGrid; } }

            public PlacementSettings(float footprintRadius, float clearance, float heightOffset, bool alignWithGrid)
            {
                this.footprintRadius = Mathf.Max(0.05f, footprintRadius);
                this.clearance = Mathf.Max(0f, clearance);
                this.heightOffset = heightOffset;
                this.alignWithGrid = alignWithGrid;
            }
        }

        #endregion
    }
}
