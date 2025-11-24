using System;
using UnityEngine;
using UnityEngine.Serialization;

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
        [Tooltip("Unique identifier used to register this turret archetype across systems.")]
        [SerializeField]private string key = "turret_default";
        [Tooltip("Display name shown in UI when the turret is unlocked.")]
        [SerializeField] private string displayName = "New Turret";
        [Tooltip("Description visible in selection menus and build previews.")]
        [SerializeField,TextArea] private string description;
        [Tooltip("Icon used for build menus and HUD elements.")]
        [SerializeField]private Sprite icon;

        [Header("Prefabs And Pools")]
        [Tooltip("Runtime prefab spawned for this turret. Must implement PooledTurret.")]
        [SerializeField] private PooledTurret turretPrefab;
        [Tooltip("Pool asset handling the lifecycle of turret instances.")]
        [SerializeField]private TurretPoolSO turretPool;
        [Tooltip("Projectile data assigned to this turret's fire modes.")]
        [SerializeField] private ProjectileDefinition projectile;
        [Tooltip("Pool asset used to spawn projectiles for this turret.")]
        [SerializeField]private ProjectilePoolSO projectilePool;

        [Header("Stats")]
        [Tooltip("Durability settings defining survivability.")]
        [SerializeField] private DurabilitySettings durability = new DurabilitySettings(150f, 10f, 8f, 1.5f);
        [Tooltip("Targeting and engagement configuration.")]
        [SerializeField] private TargetingSettings targeting = new TargetingSettings(16f, 240f, 130f, 0.5f, 0.75f);
        [Tooltip("Fire mode configuration for automatic targeting.")]
        [SerializeField]private FireModeSettings automaticFire = new FireModeSettings(0.45f, 3, 0.08f, TurretFirePattern.Bazooka, 0f);
        [Tooltip("Fire mode configuration for first-person free aim.")]
        [SerializeField]private FireModeSettings freeAimFire = new FireModeSettings(0.25f, 1, 0.0f, TurretFirePattern.Consecutive, 0f);
        [SerializeField]
        [Tooltip("Time required by the player to assume manual control of the turret.")]
        private float modeSwitchSeconds = 1.0f;
        [Tooltip("Resource footprint for building and maintaining the turret.")]
        [SerializeField]private EconomySettings economy = new EconomySettings(120, 2, 1.5f, 0.85f);
        [Tooltip("Reloading and heat management settings for sustained fire.")]
        [SerializeField] private SustainSettings sustain = new SustainSettings(12, 3.5f, 0.45f, 0.65f);
        [Tooltip("Placement rules relative to Grid3D buildable cells.")]
        [SerializeField]private PlacementSettings placement = new PlacementSettings(0.45f, 0.08f, 0.15f, true, Vector3.zero);

        [Header("Free Aim Multipliers")]
        [Tooltip("Multiplicative modifiers applied to each turret statistic while manual control is active.")]
        [SerializeField] private FreeAimMultipliers freeAimMultipliers = FreeAimMultipliers.Identity;
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

        public FreeAimMultipliers FreeAimMultiplierSettings
        {
            get { return freeAimMultipliers; }
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
            [Tooltip("Maximum yaw swing allowed relative to the spawn orientation. Set to zero to disable clamping.")]
            private float yawClampDegrees;

            [SerializeField]
            [Tooltip("Dead zone radius around the base where the turret will not fire.")]
            private float deadZoneRadius;

            [SerializeField]
            [Tooltip("Time in seconds between target reevaluations to avoid per-frame checks.")]
            private float retargetInterval;

            public float Range { get { return range; } }
            public float TurnRate { get { return turnRate; } }
            public float YawClampDegrees { get { return yawClampDegrees; } }
            public float DeadZoneRadius { get { return deadZoneRadius; } }
            public float RetargetInterval { get { return retargetInterval; } }

            public TargetingSettings(float range, float turnRate, float yawClampDegrees, float deadZoneRadius, float retargetInterval)
            {
                this.range = Mathf.Max(0.5f, range);
                this.turnRate = Mathf.Max(0f, turnRate);
                this.yawClampDegrees = Mathf.Max(0f, yawClampDegrees);
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
            [Tooltip("Pattern used to position multiple projectiles or trigger splash impacts.")]
            private TurretFirePattern pattern;

            public float CadenceSeconds { get { return cadenceSeconds; } }
            public int ProjectilesPerShot { get { return projectilesPerShot; } }
            public float InterProjectileDelay { get { return interProjectileDelay; } }
            public TurretFirePattern Pattern { get { return pattern; } }
            public FireModeSettings(float cadenceSeconds, int projectilesPerShot, float interProjectileDelay, TurretFirePattern pattern, float legacyConeAngle)
            {
                this.cadenceSeconds = Mathf.Max(0.02f, cadenceSeconds);
                this.projectilesPerShot = Mathf.Max(1, projectilesPerShot);
                this.interProjectileDelay = Mathf.Max(0f, interProjectileDelay);
                this.pattern = pattern;
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

            [SerializeField]
            [Tooltip("Positional offset relative to the grid cell center applied at spawn.")]
            private Vector3 spawnOffset;

            public float FootprintRadius { get { return footprintRadius; } }
            public float Clearance { get { return clearance; } }
            public float HeightOffset { get { return heightOffset; } }
            public bool AlignWithGrid { get { return alignWithGrid; } }
            public Vector3 SpawnOffset { get { return spawnOffset; } }

            public PlacementSettings(float footprintRadius, float clearance, float heightOffset, bool alignWithGrid, Vector3 spawnOffset)
            {
                this.footprintRadius = Mathf.Max(0.05f, footprintRadius);
                this.clearance = Mathf.Max(0f, clearance);
                this.heightOffset = heightOffset;
                this.alignWithGrid = alignWithGrid;
                this.spawnOffset = spawnOffset;
            }
        }

        #endregion

        [Serializable]
        public struct FreeAimMultipliers
        {
            [SerializeField]
            [Tooltip("Multiplier applied to chassis health while in free aim.")]
            private float health;

            [SerializeField]
            [Tooltip("Multiplier applied to armor while in free aim.")]
            private float armor;

            [SerializeField]
            [Tooltip("Multiplier applied to magic resistance while in free aim.")]
            private float magicResistance;

            [SerializeField]
            [Tooltip("Multiplier applied to passive regeneration per second while in free aim.")]
            private float passiveRegenPerSecond;

            [SerializeField]
            [Tooltip("Multiplier applied to targeting range while in free aim.")]
            private float range;

            [SerializeField]
            [Tooltip("Multiplier applied to rotation speed while in free aim.")]
            private float turnRate;

            [SerializeField]
            [Tooltip("Multiplier applied to dead zone radius while in free aim.")]
            private float deadZoneRadius;

            [SerializeField]
            [Tooltip("Multiplier applied to retarget interval while in free aim.")]
            private float retargetInterval;

            [SerializeField]
            [Tooltip("Multiplier applied to automatic fire cadence while in free aim.")]
            private float automaticCadenceSeconds;

            [SerializeField]
            [Tooltip("Multiplier applied to projectiles per automatic shot while in free aim.")]
            private float automaticProjectilesPerShot;

            [SerializeField]
            [Tooltip("Multiplier applied to inter-projectile delay for automatic fire while in free aim.")]
            private float automaticInterProjectileDelay;

            [SerializeField]
            [Tooltip("Multiplier applied to free aim fire cadence while in free aim.")]
            private float freeAimCadenceSeconds;

            [SerializeField]
            [Tooltip("Multiplier applied to projectiles per free aim shot while in free aim.")]
            private float freeAimProjectilesPerShot;

            [SerializeField]
            [Tooltip("Multiplier applied to inter-projectile delay for free aim fire while in free aim.")]
            private float freeAimInterProjectileDelay;

            [SerializeField]
            [Tooltip("Multiplier applied to magazine size while in free aim.")]
            private float magazineSize;

            [SerializeField]
            [Tooltip("Multiplier applied to reload duration while in free aim.")]
            private float reloadSeconds;

            [SerializeField]
            [Tooltip("Multiplier applied to maximum heat while in free aim.")]
            private float maxHeat;

            [SerializeField]
            [Tooltip("Multiplier applied to heat dissipation time while in free aim.")]
            private float heatDissipationSeconds;

            [SerializeField]
            [Tooltip("Multiplier applied to the time needed to assume manual control of the turret.")]
            private float modeSwitchSeconds;

            [SerializeField]
            [Tooltip("Multiplier applied to build cost while in free aim.")]
            private float buildCost;

            [SerializeField]
            [Tooltip("Multiplier applied to upkeep cost while in free aim.")]
            private float upkeepCost;

            [SerializeField]
            [Tooltip("Multiplier applied to salvage delay while in free aim.")]
            private float salvageDelay;

            [SerializeField]
            [Tooltip("Multiplier applied to refund ratio while in free aim.")]
            private float refundRatio;

            [SerializeField]
            [Tooltip("Multiplier applied to placement footprint radius while in free aim.")]
            private float footprintRadius;

            [SerializeField]
            [Tooltip("Multiplier applied to placement clearance while in free aim.")]
            private float clearance;

            [SerializeField]
            [Tooltip("Multiplier applied to placement height offset while in free aim.")]
            private float placementHeightOffset;

            public float Health { get { return health; } }
            public float Armor { get { return armor; } }
            public float MagicResistance { get { return magicResistance; } }
            public float PassiveRegenPerSecond { get { return passiveRegenPerSecond; } }
            public float Range { get { return range; } }
            public float TurnRate { get { return turnRate; } }
            public float DeadZoneRadius { get { return deadZoneRadius; } }
            public float RetargetInterval { get { return retargetInterval; } }
            public float AutomaticCadenceSeconds { get { return automaticCadenceSeconds; } }
            public float AutomaticProjectilesPerShot { get { return automaticProjectilesPerShot; } }
            public float AutomaticInterProjectileDelay { get { return automaticInterProjectileDelay; } }
            public float FreeAimCadenceSeconds { get { return freeAimCadenceSeconds; } }
            public float FreeAimProjectilesPerShot { get { return freeAimProjectilesPerShot; } }
            public float FreeAimInterProjectileDelay { get { return freeAimInterProjectileDelay; } }
            public float MagazineSize { get { return magazineSize; } }
            public float ReloadSeconds { get { return reloadSeconds; } }
            public float MaxHeat { get { return maxHeat; } }
            public float HeatDissipationSeconds { get { return heatDissipationSeconds; } }
            public float ModeSwitchSeconds { get { return modeSwitchSeconds; } }
            public float BuildCost { get { return buildCost; } }
            public float UpkeepCost { get { return upkeepCost; } }
            public float SalvageDelay { get { return salvageDelay; } }
            public float RefundRatio { get { return refundRatio; } }
            public float FootprintRadius { get { return footprintRadius; } }
            public float Clearance { get { return clearance; } }
            public float PlacementHeightOffset { get { return placementHeightOffset; } }

            public FreeAimMultipliers(float health, float armor, float magicResistance, float passiveRegenPerSecond, float range, float turnRate, float deadZoneRadius, float retargetInterval, float automaticCadenceSeconds, float automaticProjectilesPerShot, float automaticInterProjectileDelay, float freeAimCadenceSeconds, float freeAimProjectilesPerShot, float freeAimInterProjectileDelay, float magazineSize, float reloadSeconds, float maxHeat, float heatDissipationSeconds, float modeSwitchSeconds, float buildCost, float upkeepCost, float salvageDelay, float refundRatio, float footprintRadius, float clearance, float placementHeightOffset)
            {
                this.health = Mathf.Max(0.01f, health);
                this.armor = Mathf.Max(0f, armor);
                this.magicResistance = Mathf.Max(0f, magicResistance);
                this.passiveRegenPerSecond = Mathf.Max(0f, passiveRegenPerSecond);
                this.range = Mathf.Max(0f, range);
                this.turnRate = Mathf.Max(0f, turnRate);
                this.deadZoneRadius = Mathf.Max(0f, deadZoneRadius);
                this.retargetInterval = Mathf.Max(0f, retargetInterval);
                this.automaticCadenceSeconds = Mathf.Max(0.01f, automaticCadenceSeconds);
                this.automaticProjectilesPerShot = Mathf.Max(0f, automaticProjectilesPerShot);
                this.automaticInterProjectileDelay = Mathf.Max(0f, automaticInterProjectileDelay);
                this.freeAimCadenceSeconds = Mathf.Max(0.01f, freeAimCadenceSeconds);
                this.freeAimProjectilesPerShot = Mathf.Max(0f, freeAimProjectilesPerShot);
                this.freeAimInterProjectileDelay = Mathf.Max(0f, freeAimInterProjectileDelay);
                this.magazineSize = Mathf.Max(0f, magazineSize);
                this.reloadSeconds = Mathf.Max(0f, reloadSeconds);
                this.maxHeat = Mathf.Max(0f, maxHeat);
                this.heatDissipationSeconds = Mathf.Max(0.01f, heatDissipationSeconds);
                this.modeSwitchSeconds = Mathf.Max(0.01f, modeSwitchSeconds);
                this.buildCost = Mathf.Max(0f, buildCost);
                this.upkeepCost = Mathf.Max(0f, upkeepCost);
                this.salvageDelay = Mathf.Max(0f, salvageDelay);
                this.refundRatio = Mathf.Max(0f, refundRatio);
                this.footprintRadius = Mathf.Max(0.01f, footprintRadius);
                this.clearance = Mathf.Max(0f, clearance);
                this.placementHeightOffset = placementHeightOffset;
            }

            public static FreeAimMultipliers Identity
            {
                get
                {
                    FreeAimMultipliers identity = new FreeAimMultipliers(
                        health:1f,
                        armor:1f,
                        magicResistance:1f,
                        passiveRegenPerSecond:1f,
                        range:1f, 
                        turnRate:1f, 
                        deadZoneRadius:1f, 
                        retargetInterval:1f,
                        automaticCadenceSeconds: 1f,
                        automaticProjectilesPerShot:1f,
                        automaticInterProjectileDelay: 1f,
                        freeAimCadenceSeconds: 1f,
                        freeAimProjectilesPerShot: 1f,
                        freeAimInterProjectileDelay: 1f,
                        magazineSize: 1f,
                        reloadSeconds: 1f,
                        maxHeat: 1f,
                        heatDissipationSeconds: 1f,
                        modeSwitchSeconds: 1f,
                        buildCost: 1f,
                        upkeepCost: 1f,
                        salvageDelay: 1f, 
                        refundRatio: 1f,
                        footprintRadius: 1f, 
                        clearance: 1f, 
                        placementHeightOffset: 1f); 
                    return identity;
                }
            }
        }
    }
}
