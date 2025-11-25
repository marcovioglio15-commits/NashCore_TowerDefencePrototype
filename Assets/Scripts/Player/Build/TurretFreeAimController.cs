using System;
using System.Collections;
using System.Collections.Generic;
using Scriptables.Turrets;
using UnityEngine;

namespace Player.Build
{
    /// <summary>
    /// Manages manual turret possession, camera interpolation, and gesture reinterpretation for free-aim mode.
    /// </summary>
    public class TurretFreeAimController : MonoBehaviour
    {
        #region Nested Types
        /// <summary>
        /// Axis processing mode applied to drag and swipe gestures during possession.
        /// </summary>
        private enum FreeAimAxisMode
        {
            HorizontalOnly,
            VerticalOnly,
            HorizontalAndVertical
        }
        #endregion

        #region Variables And Properties
        #region Serialized Fields
        [Tooltip("Camera interpolated into turret perspective during free-aim control.")]
        [Header("Camera")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("Local offset from the turret anchor applied to the camera.")]
        [SerializeField] private Vector3 cameraLocalOffset = new Vector3(0f, 0.1f, -0.85f);
        [Tooltip("Seconds used for linear interpolation when entering free-aim.")]
        [SerializeField] private float enterLerpSeconds = 0.4f;
        [Tooltip("Seconds used for linear interpolation when exiting free-aim.")]
        [SerializeField] private float exitLerpSeconds = 0.35f;

        [Tooltip("Axes interpreted from drag and swipe while controlling a turret.")]
        [Header("Rotation")]
        [SerializeField] private FreeAimAxisMode axisMode = FreeAimAxisMode.HorizontalAndVertical;
        [Tooltip("Degrees of yaw applied per pixel of horizontal drag or swipe.")]
        [SerializeField] private float yawSensitivity = 0.35f;
        [Tooltip("Degrees of pitch applied per pixel of vertical drag or swipe.")]
        [SerializeField] private float pitchSensitivity = 0.35f;
        [Tooltip("Vertical drag magnitude in pixels required before pitch input is processed.")]
        [SerializeField] private float pitchInputDeadZone = 1.5f;
        [Tooltip("Maximum upward pitch offset allowed relative to the starting orientation; zero disables the clamp.")]
        [SerializeField] private float pitchUpClampDegrees = 55f;
        [Tooltip("Maximum downward pitch offset allowed relative to the starting orientation; zero disables the clamp.")]
        [SerializeField] private float pitchDownClampDegrees = 35f;
        [Tooltip("Maximum yaw offset allowed while possessed, relative to the starting orientation; zero disables the clamp.")]
        [SerializeField] private float fallbackYawClampDegrees = 110f;
        [Tooltip("When true, gesture deltas respect turret turn rate; when false they are applied immediately using queued overflow if needed.")]
        [SerializeField] private bool clampTurnRate = true;
        [Tooltip("Seconds used to ease out rotation when the input is released to avoid abrupt stops.")]
        [Header("Input Damping")]
        [SerializeField, Range(0.01f, 0.5f)] private float releaseDampSeconds = 0.15f;
        [Tooltip("Maximum degrees per second while smoothing release; zero disables this cap.")]
        [SerializeField] private float releaseMaxDegreesPerSecond = 0f;

        [Tooltip("Smallest cadence allowed to process manual tap firing.")]
        [Header("Firing")]
        [SerializeField] private float FireCD = 0.05f;
        [Tooltip("Local offset from the possessed camera used as projectile spawn origin.")]
        [SerializeField] private Vector3 freeAimProjectileOffset = new Vector3(0f, -0.05f, 0.1f);

        [Tooltip("Distance at which the controlled turret is hidden to avoid camera clipping.")]
        [Header("Visibility")]
        [SerializeField] private float hideDistance = 0.45f;
        [Tooltip("Normalized camera lerp progress at which turret renderers are hidden.")]
        [Range(0f,1f)]
        [SerializeField] private float hideLerpThreshold = 0.65f;
        [Tooltip("Normalized camera lerp progress at which reticle and exit UI arm.")]
        [Range(0f,1f)]
        [SerializeField] private float uiRevealLerpThreshold = 0.85f;
        [Tooltip("Layer mask used to auto-populate auxiliary renderers shown only during free-aim.")]
        [Header("Auxiliary Visibility")]
        [SerializeField] private LayerMask auxiliaryRendererLayerMask;
        [Tooltip("Renderers auto-collected through the layer mask and shown exclusively during free-aim.")]
        [SerializeField] private Renderer[] auxiliaryFreeAimRenderers;
        #endregion

        #region Runtime State
        private PooledTurret activeTurret;
        private TurretAutoController cachedAutoController;
        private bool cachedAutoEnabled;
        private Transform cachedCameraParent;
        private Vector3 cachedCameraPosition;
        private Quaternion cachedCameraRotation;
        private bool hasCameraCache;
        private Coroutine cameraRoutine;
        private float fireCooldownTimer;
        private bool freeAimActive;
        private Quaternion anchorBaseRotation;
        private float currentYawOffset;
        private float currentPitchOffset;
        private float pendingYawInput;
        private float pendingPitchInput;
        private float targetYawOffset;
        private float targetPitchOffset;
        private float yawDampVelocity;
        private float pitchDampVelocity;
        private bool cameraOffsetsDirty;
        private bool renderersHiddenDuringFreeAim;
        private bool auxiliaryShownDuringFreeAim;
        private Renderer[] cachedTurretRenderers;
        private bool[] cachedRendererStates;
        private Renderer[] activeAuxiliaryRenderers;
        private bool uiArmed;
        private bool reticleHoldActive;
        private WaitForSeconds cachedFreeAimInterDelay;
        private float cachedFreeAimDelaySeconds;
        private FreeAimRotationFollower[] rotationFollowers;
        private Quaternion[] rotationFollowerBaseRotations;
        private bool phaseAllowsFreeAim = true;
        private readonly Dictionary<PooledTurret, Renderer[]> auxiliaryRendererMap = new Dictionary<PooledTurret, Renderer[]>();
        private readonly List<Renderer> auxiliaryRendererBuffer = new List<Renderer>();
        private bool auxiliaryRuntimeInitialized;
        #endregion
        #endregion

        #region Methods
        #region Unity
        /// <summary>
        /// Ensures the controlled camera reference is available.
        /// </summary>
        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        /// <summary>
        /// Wires up event handlers for perspective requests and gesture streams.
        /// </summary>
        private void OnEnable()
        {
            EventsManager.TurretPerspectiveRequested += HandlePerspectiveRequested;
            EventsManager.Drag += HandleAngularDrag;
            EventsManager.Swipe += HandleAngularSwipe;
            EventsManager.HoldBegan += HandleHoldBegan;
            EventsManager.HoldEnded += HandleHoldEnded;
            EventsManager.Tap += HandleTap;
            EventsManager.TurretFreeAimExitRequested += HandleExitRequested;
            EventsManager.GamePhaseChanged += HandleGamePhaseChanged;
            RefreshAuxiliaryRendererListIfNeeded();
            if (!freeAimActive)
                HideAllAuxiliaryRenderers();
            SyncPhasePermissions();
        }

        /// <summary>
        /// Cleans up listeners and restores any hijacked camera or turret state.
        /// </summary>
        private void OnDisable()
        {
            EventsManager.TurretPerspectiveRequested -= HandlePerspectiveRequested;
            EventsManager.Drag -= HandleAngularDrag;
            EventsManager.Swipe -= HandleAngularSwipe;
            EventsManager.HoldBegan -= HandleHoldBegan;
            EventsManager.HoldEnded -= HandleHoldEnded;
            EventsManager.Tap -= HandleTap;
            EventsManager.TurretFreeAimExitRequested -= HandleExitRequested;
            EventsManager.GamePhaseChanged -= HandleGamePhaseChanged;
            if (freeAimActive)
                EventsManager.InvokeTurretFreeAimEnded(activeTurret);
            StopActiveCameraRoutine();
            RestoreCameraTransform();
            ReleaseTurret();
            HideAllAuxiliaryRenderers();
        }

        /// <summary>
        /// Keeps auxiliary renderer bindings synchronized when values change in the editor.
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            RefreshAuxiliaryRendererList();
        }

        /// <summary>
        /// Maintains cooldowns and safety exits without per-frame heavy work.
        /// </summary>
        private void Update()
        {
            if (fireCooldownTimer > 0f)
                fireCooldownTimer = Mathf.Max(0f, fireCooldownTimer - Time.deltaTime);

            if (!freeAimActive)
                return;

            if (activeTurret == null || !activeTurret.gameObject.activeInHierarchy)
            {
                ExitFreeAim();
                return;
            }

            if (reticleHoldActive)
                TryFire();

            if (activeTurret.HasDefinition)
                activeTurret.CooldownHeat(Time.deltaTime);

            ProcessPendingAngularInput();
            EvaluateCameraClipping();
            UpdateSmoothedRotation();
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Starts possession flow when the player holds a turret.
        /// </summary>
        private void HandlePerspectiveRequested(PooledTurret turret)
        {
            if (turret == null)
                return;

            if (freeAimActive)
                return;

            if (!phaseAllowsFreeAim)
                return;

            BeginFreeAim(turret);
        }

        /// <summary>
        /// Applies yaw and pitch input from drag gestures.
        /// </summary>
        private void HandleAngularDrag(Vector2 delta)
        {
            if (!freeAimActive)
                return;

            QueueAngularInput(delta);
        }

        /// <summary>
        /// Applies yaw and pitch input from swipe gestures.
        /// </summary>
        private void HandleAngularSwipe(Vector2 delta)
        {
            if (!freeAimActive)
                return;

            QueueAngularInput(delta);
        }

        /// <summary>
        /// Starts continuous fire attempts while the reticle is held.
        /// </summary>
        private void HandleHoldBegan(Vector2 screenPosition)
        {
            if (!freeAimActive)
                return;

            if (!IsTapWithinReticle(screenPosition))
                return;

            reticleHoldActive = true;
            TryFire();
        }

        /// <summary>
        /// Stops continuous fire attempts when the reticle hold ends.
        /// </summary>
        private void HandleHoldEnded(Vector2 screenPosition)
        {
            if (!reticleHoldActive)
                return;

            reticleHoldActive = false;
        }

        /// <summary>
        /// Issues a manual fire attempt when tapping in free-aim.
        /// </summary>
        private void HandleTap(Vector2 screenPosition)
        {
            if (!freeAimActive)
                return;

            if (!IsTapWithinReticle(screenPosition))
                return;

            TryFire();
        }

        /// <summary>
        /// Leaves free-aim when the UI exit hold completes.
        /// </summary>
        private void HandleExitRequested()
        {
            if (!freeAimActive)
                return;

            ExitFreeAim();
        }

        /// <summary>
        /// Exits free-aim when the game enters the build phase.
        /// </summary>
        private void HandleGamePhaseChanged(GamePhase phase)
        {
            phaseAllowsFreeAim = phase == GamePhase.Combat;
            if (!phaseAllowsFreeAim && freeAimActive)
                ExitFreeAim();
        }
        #endregion

        #region Free-Aim Flow
        /// <summary>
        /// Performs state setup and camera interpolation for manual control.
        /// </summary>
        private void BeginFreeAim(PooledTurret targetTurret)
        {
            if (targetTurret == null || !targetTurret.HasDefinition)
                return;

            activeTurret = targetTurret;
            cachedAutoController = activeTurret.GetComponent<TurretAutoController>();
            cachedAutoEnabled = cachedAutoController != null && cachedAutoController.enabled;
            if (cachedAutoController != null)
                cachedAutoController.enabled = false;

            activeTurret.SetFreeAimState(true);
            freeAimActive = true;
            fireCooldownTimer = 0f;
            cachedTurretRenderers = activeTurret.GetFreeAimRendererSet();
            cachedRendererStates = null;
            activeAuxiliaryRenderers = ResolveAuxiliaryRenderers(activeTurret);
            renderersHiddenDuringFreeAim = false;
            auxiliaryShownDuringFreeAim = false;
            uiArmed = false;
            reticleHoldActive = false;
            currentYawOffset = 0f;
            currentPitchOffset = 0f;
            targetYawOffset = 0f;
            targetPitchOffset = 0f;
            yawDampVelocity = 0f;
            pitchDampVelocity = 0f;
            cameraOffsetsDirty = true;
            pendingYawInput = 0f;
            pendingPitchInput = 0f;
            CacheCameraState();
            CacheAnchorOrientation();
            CacheRotationFollowers();
            HideAllAuxiliaryRenderers();
            StartCameraLerpToTurret();
            EventsManager.InvokeTurretFreeAimStarted(activeTurret);
        }

        /// <summary>
        /// Restores previous state and camera placement.
        /// </summary>
        private void ExitFreeAim()
        {
            PooledTurret turretToRelease = activeTurret;
            if (freeAimActive && activeTurret != null)
                activeTurret.SetFreeAimState(false);

            RestoreAutoController();
            freeAimActive = false;
            activeTurret = null;
            fireCooldownTimer = 0f;
            currentYawOffset = 0f;
            currentPitchOffset = 0f;
            targetYawOffset = 0f;
            targetPitchOffset = 0f;
            yawDampVelocity = 0f;
            pitchDampVelocity = 0f;
            cameraOffsetsDirty = false;
            pendingYawInput = 0f;
            pendingPitchInput = 0f;
            uiArmed = false;
            reticleHoldActive = false;
            StartCameraReturn();
            EventsManager.InvokeTurretFreeAimEnded(turretToRelease);
            HideAllAuxiliaryRenderers();
            ShowPossessionRenderers();
            cachedTurretRenderers = null;
            cachedRendererStates = null;
            activeAuxiliaryRenderers = null;
            rotationFollowers = null;
            rotationFollowerBaseRotations = null;
        }

        /// <summary>
        /// Restores turret and camera immediately when disabling the controller.
        /// </summary>
        private void ReleaseTurret()
        {
            if (activeTurret != null)
                activeTurret.SetFreeAimState(false);

            RestoreAutoController();
            activeTurret = null;
            freeAimActive = false;
            fireCooldownTimer = 0f;
            reticleHoldActive = false;
            pendingYawInput = 0f;
            pendingPitchInput = 0f;
            targetYawOffset = 0f;
            targetPitchOffset = 0f;
            yawDampVelocity = 0f;
            pitchDampVelocity = 0f;
            cameraOffsetsDirty = false;
            HideAllAuxiliaryRenderers();
            ShowPossessionRenderers();
            cachedTurretRenderers = null;
            cachedRendererStates = null;
            activeAuxiliaryRenderers = null;
            rotationFollowers = null;
            rotationFollowerBaseRotations = null;
            auxiliaryShownDuringFreeAim = false;
        }

        /// <summary>
        /// Registers additional renderers to hide while possessing the specified turret.
        /// </summary>
        public void RegisterAuxiliaryRenderers(PooledTurret turret, Renderer[] renderers)
        {
            if (turret == null)
                return;

            bool hasRenderers = renderers != null && renderers.Length > 0;
            if (!hasRenderers)
            {
                if (auxiliaryRendererMap.ContainsKey(turret))
                    auxiliaryRendererMap.Remove(turret);
                return;
            }

            auxiliaryRendererMap[turret] = renderers;
            if (freeAimActive && turret == activeTurret && auxiliaryShownDuringFreeAim)
            {
                activeAuxiliaryRenderers = ResolveAuxiliaryRenderers(activeTurret);
                ShowRendererCollection(activeAuxiliaryRenderers, null);
            }
            else
                SetRendererCollectionEnabled(renderers, false);
        }

        /// <summary>
        /// Removes auxiliary renderer bindings associated with the specified turret.
        /// </summary>
        public void UnregisterAuxiliaryRenderers(PooledTurret turret)
        {
            if (turret == null)
                return;

            Renderer[] removedRenderers = null;
            if (auxiliaryRendererMap.ContainsKey(turret))
            {
                removedRenderers = auxiliaryRendererMap[turret];
                auxiliaryRendererMap.Remove(turret);
            }

            if (freeAimActive && turret == activeTurret)
            {
                SetRendererCollectionEnabled(removedRenderers, false);
                activeAuxiliaryRenderers = ResolveAuxiliaryRenderers(activeTurret);
                if (auxiliaryShownDuringFreeAim)
                    ShowRendererCollection(activeAuxiliaryRenderers, null);
            }
            else if (!freeAimActive)
                HideAllAuxiliaryRenderers();
        }
        #endregion

        #region Rotation And Fire
        /// <summary>
        /// Queues gesture deltas and processes yaw and pitch using the configured clamp strategy.
        /// </summary>
        private void QueueAngularInput(Vector2 delta)
        {
            if (activeTurret == null)
                return;

            bool processYaw = ShouldProcessYaw();
            bool processPitch = ShouldProcessPitch();
            if (!processYaw)
                pendingYawInput = 0f;
            if (!processPitch)
                pendingPitchInput = 0f;

            float pitchThreshold = pitchInputDeadZone <= 0f ? 0f : pitchInputDeadZone;
            bool pitchEngaged = processPitch && Mathf.Abs(delta.y) >= pitchThreshold;
            if (processYaw)
                pendingYawInput += delta.x * yawSensitivity;
            if (pitchEngaged)
                pendingPitchInput += -delta.y * pitchSensitivity;

            ProcessPendingAngularInput();
        }

        /// <summary>
        /// Applies queued yaw and pitch respecting clamp preferences and range limits.
        /// </summary>
        private void ProcessPendingAngularInput()
        {
            if (!freeAimActive || activeTurret == null)
                return;

            bool processYaw = ShouldProcessYaw();
            bool processPitch = ShouldProcessPitch();
            if (!processYaw)
                pendingYawInput = 0f;
            if (!processPitch)
                pendingPitchInput = 0f;

            bool hasYaw = processYaw && !Mathf.Approximately(pendingYawInput, 0f);
            bool hasPitch = processPitch && !Mathf.Approximately(pendingPitchInput, 0f);
            if (!hasYaw && !hasPitch)
                return;

            float yawStep = hasYaw ? pendingYawInput : 0f;
            float pitchStep = hasPitch ? pendingPitchInput : 0f;
            float maxStep = ResolveMaxAngularStep();

            if (float.IsPositiveInfinity(maxStep))
            {
                pendingYawInput = 0f;
                pendingPitchInput = 0f;
            }
            else if (maxStep > 0f)
            {
                if (hasYaw)
                {
                    float clampedYaw = Mathf.Clamp(yawStep, -maxStep, maxStep);
                    pendingYawInput -= clampedYaw;
                    yawStep = clampedYaw;
                }

                if (hasPitch)
                {
                    float clampedPitch = Mathf.Clamp(pitchStep, -maxStep, maxStep);
                    pendingPitchInput -= clampedPitch;
                    pitchStep = clampedPitch;
                }
            }
            else
            {
                pendingYawInput = 0f;
                pendingPitchInput = 0f;
                return;
            }

            bool targetChanged = false;

            if (hasYaw)
            {
                float clampHalf = ResolveYawClampHalf();
                if (float.IsPositiveInfinity(clampHalf))
                {
                    targetYawOffset = NormalizeSignedAngle(targetYawOffset + yawStep);
                    targetChanged = true;
                }
                else
                {
                    float clampedTarget = Mathf.Clamp(targetYawOffset + yawStep, -clampHalf, clampHalf);
                    float appliedYaw = clampedTarget - targetYawOffset;
                    if (!Mathf.Approximately(appliedYaw, 0f))
                    {
                        targetYawOffset = clampedTarget;
                        targetChanged = true;
                    }

                    if (!Mathf.Approximately(appliedYaw, yawStep))
                        pendingYawInput = 0f;
                }
            }

            if (hasPitch)
            {
                Vector2 pitchClamp = ResolvePitchClamp();
                float minPitch = -pitchClamp.x;
                float maxPitch = pitchClamp.y;
                float clampedPitchOffset = Mathf.Clamp(targetPitchOffset + pitchStep, minPitch, maxPitch);
                float appliedPitch = clampedPitchOffset - targetPitchOffset;
                if (!Mathf.Approximately(appliedPitch, 0f))
                {
                    targetPitchOffset = clampedPitchOffset;
                    targetChanged = true;
                }

                if (!Mathf.Approximately(appliedPitch, pitchStep))
                    pendingPitchInput = 0f;
            }

            if (targetChanged)
                cameraOffsetsDirty = true;
        }

        /// <summary>
        /// Smooths yaw and pitch offsets toward their targets to avoid abrupt release stops.
        /// </summary>
        private void UpdateSmoothedRotation()
        {
            if (!freeAimActive)
                return;

            if (targetCamera == null && activeTurret == null)
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            float dampTime = Mathf.Max(0.0001f, releaseDampSeconds);
            float maxSpeed = releaseMaxDegreesPerSecond > 0f ? releaseMaxDegreesPerSecond : float.MaxValue;

            float nextYaw = Mathf.SmoothDampAngle(currentYawOffset, targetYawOffset, ref yawDampVelocity, dampTime, maxSpeed, deltaTime);
            float nextPitch = Mathf.SmoothDamp(currentPitchOffset, targetPitchOffset, ref pitchDampVelocity, dampTime, maxSpeed, deltaTime);
            bool changed = !Mathf.Approximately(nextYaw, currentYawOffset) || !Mathf.Approximately(nextPitch, currentPitchOffset);
            currentYawOffset = nextYaw;
            currentPitchOffset = nextPitch;

            if (changed || cameraOffsetsDirty)
            {
                cameraOffsetsDirty = false;
                ApplyCameraOffsets();
            }
        }

        /// <summary>
        /// Fires the turret once if cadence allows it.
        /// </summary>
        private void TryFire()
        {
            if (activeTurret == null || !activeTurret.HasDefinition)
                return;

            if (fireCooldownTimer > 0f)
                return;

            TurretStatSnapshot stats = activeTurret.ActiveStats;
            float cadence = Mathf.Max(FireCD, stats.FreeAimCadenceSeconds);
            fireCooldownTimer = cadence;
            Vector3 forward = ResolveFireForward();
            Transform spawnOrigin = ResolveFreeAimSpawnOrigin();
            Vector3 upAxis = ResolveFireUpAxis();

            bool needsCoroutine = stats.FreeAimProjectilesPerShot > 1 && (stats.FreeAimPattern == TurretFirePattern.Consecutive || stats.FreeAimPattern == TurretFirePattern.Bazooka) && stats.FreeAimInterProjectileDelay > 0f;
            if (needsCoroutine)
            {
                StartCoroutine(FireBurstRoutine(stats, forward, spawnOrigin, upAxis));
                return;
            }

            FireProjectiles(stats, forward, spawnOrigin, upAxis);
        }

        /// <summary>
        /// Executes manual burst spawning using free-aim fire settings.
        /// </summary>
        private IEnumerator FireBurstRoutine(TurretStatSnapshot stats, Vector3 forward, Transform spawnOrigin, Vector3 upAxis)
        {
            if (activeTurret == null || !activeTurret.HasDefinition)
                yield break;

            ProjectileDefinition projectileDefinition = activeTurret.Definition != null ? activeTurret.Definition.Projectile : null;
            float splashRadius = projectileDefinition != null ? Mathf.Max(0f, projectileDefinition.SplashRadius) : 0f;
            int projectiles = Mathf.Max(1, stats.FreeAimProjectilesPerShot);
            TurretFirePattern pattern = stats.FreeAimPattern;
            WaitForSeconds delay = ResolveInterProjectileDelay(stats.FreeAimInterProjectileDelay);

            for (int i = 0; i < projectiles; i++)
            {
                Vector3 direction = TurretFireUtility.ResolveProjectileDirection(forward, pattern, splashRadius, i, projectiles, upAxis);
                float patternSplash = pattern == TurretFirePattern.Bazooka ? splashRadius : 0f;
                TurretFireUtility.SpawnProjectile(activeTurret, direction, spawnOrigin, freeAimProjectileOffset, patternSplash);

                bool shouldDelay = delay != null && i < projectiles - 1;
                if (shouldDelay)
                    yield return delay;
            }
        }

        /// <summary>
        /// Fires projectiles immediately when no stagger is required.
        /// </summary>
        private void FireProjectiles(TurretStatSnapshot stats, Vector3 forward, Transform spawnOrigin, Vector3 upAxis)
        {
            ProjectileDefinition projectileDefinition = activeTurret != null && activeTurret.Definition != null ? activeTurret.Definition.Projectile : null;
            float splashRadius = projectileDefinition != null ? Mathf.Max(0f, projectileDefinition.SplashRadius) : 0f;
            int projectiles = Mathf.Max(1, stats.FreeAimProjectilesPerShot);
            TurretFirePattern pattern = stats.FreeAimPattern;

            for (int i = 0; i < projectiles; i++)
            {
                Vector3 direction = TurretFireUtility.ResolveProjectileDirection(forward, pattern, splashRadius, i, projectiles, upAxis);
                float patternSplash = pattern == TurretFirePattern.Bazooka ? splashRadius : 0f;
                TurretFireUtility.SpawnProjectile(activeTurret, direction, spawnOrigin, freeAimProjectileOffset, patternSplash);
            }
        }
        #endregion

        #region Camera
        /// <summary>
        /// Caches the camera transform before possession.
        /// </summary>
        private void CacheCameraState()
        {
            if (targetCamera == null)
                return;

            Transform cameraTransform = targetCamera.transform;
            cachedCameraParent = cameraTransform.parent;
            cachedCameraPosition = cameraTransform.position;
            cachedCameraRotation = cameraTransform.rotation;
            hasCameraCache = true;
        }

        /// <summary>
        /// Stops any active camera interpolation routines.
        /// </summary>
        private void StopActiveCameraRoutine()
        {
            if (cameraRoutine != null)
            {
                StopCoroutine(cameraRoutine);
                cameraRoutine = null;
            }
        }

        /// <summary>
        /// Starts interpolation into the turret viewpoint.
        /// </summary>
        private void StartCameraLerpToTurret()
        {
            if (targetCamera == null)
                return;

            StopActiveCameraRoutine();
            cameraRoutine = StartCoroutine(LerpCameraToTurret());
        }

        /// <summary>
        /// Starts interpolation back to the cached camera pose.
        /// </summary>
        private void StartCameraReturn()
        {
            if (targetCamera == null)
                return;

            if (!hasCameraCache)
                return;

            StopActiveCameraRoutine();
            cameraRoutine = StartCoroutine(LerpCameraToOriginal());
        }

        /// <summary>
        /// Interpolates the camera into the turret anchor and parents it.
        /// </summary>
        private IEnumerator LerpCameraToTurret()
        {
            if (activeTurret == null || targetCamera == null)
                yield break;

            Transform cameraTransform = targetCamera.transform;
            Transform anchor = ResolveCameraAnchor();
            if (anchor == null)
                yield break;

            Vector3 startPosition = cameraTransform.position;
            Quaternion startRotation = cameraTransform.rotation;
            float duration = Mathf.Max(0f, Mathf.Max(enterLerpSeconds, activeTurret.ActiveStats.ModeSwitchSeconds));
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float normalized = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                Vector3 targetPosition = anchor.position + anchor.TransformVector(cameraLocalOffset);
                Quaternion targetRotation = anchor.rotation;
                cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, normalized);
                cameraTransform.rotation = Quaternion.Lerp(startRotation, targetRotation, normalized);
                HandleLerpProgress(normalized);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Vector3 finalPosition = anchor.position + anchor.TransformVector(cameraLocalOffset);
            cameraTransform.SetPositionAndRotation(finalPosition, anchor.rotation);
            cameraTransform.SetParent(anchor, true);
            ApplyCameraOffsets();
            cameraRoutine = null;
        }

        /// <summary>
        /// Interpolates the camera back to the cached pose and parent.
        /// </summary>
        private IEnumerator LerpCameraToOriginal()
        {
            Transform cameraTransform = targetCamera.transform;
            cameraTransform.SetParent(null, true);

            Vector3 startPosition = cameraTransform.position;
            Quaternion startRotation = cameraTransform.rotation;
            Vector3 targetPosition = cachedCameraPosition;
            Quaternion targetRotation = cachedCameraRotation;
            float duration = Mathf.Max(0f, exitLerpSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float normalized = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, normalized);
                cameraTransform.rotation = Quaternion.Lerp(startRotation, targetRotation, normalized);
                elapsed += Time.deltaTime;
                yield return null;
            }

            cameraTransform.SetPositionAndRotation(targetPosition, targetRotation);
            cameraTransform.SetParent(cachedCameraParent, true);
            hasCameraCache = false;
            cameraRoutine = null;
        }

        /// <summary>
        /// Restores the cached camera transform immediately.
        /// </summary>
        private void RestoreCameraTransform()
        {
            if (!hasCameraCache || targetCamera == null)
                return;

            Transform cameraTransform = targetCamera.transform;
            cameraTransform.SetParent(null, true);
            cameraTransform.SetPositionAndRotation(cachedCameraPosition, cachedCameraRotation);
            cameraTransform.SetParent(cachedCameraParent, true);
            hasCameraCache = false;
        }

        /// <summary>
        /// Resolves the transform used as camera anchor.
        /// </summary>
        private Transform ResolveCameraAnchor()
        {
            if (activeTurret == null)
                return null;

            if (activeTurret.Muzzle != null)
                return activeTurret.Muzzle;

            return activeTurret.transform;
        }

        /// <summary>
        /// Stores the base orientation used for yaw clamping during free-aim.
        /// </summary>
        private void CacheAnchorOrientation()
        {
            Transform anchor = ResolveCameraAnchor();
            if (anchor == null)
            {
                anchorBaseRotation = Quaternion.identity;
                return;
            }

            anchorBaseRotation = anchor.rotation;
            currentYawOffset = 0f;
            currentPitchOffset = 0f;
            targetYawOffset = 0f;
            targetPitchOffset = 0f;
            yawDampVelocity = 0f;
            pitchDampVelocity = 0f;
            cameraOffsetsDirty = true;
        }

        /// <summary>
        /// Applies yaw and pitch offsets to the camera and aligns visible turret parts horizontally.
        /// </summary>
        private void ApplyCameraOffsets()
        {
            if (targetCamera == null)
                return;

            Transform cameraTransform = targetCamera.transform;
            Transform anchor = ResolveCameraAnchor();
            Quaternion offsetRotation = BuildOffsetRotation();
            if (anchor != null && cameraTransform.parent == anchor)
                cameraTransform.localRotation = offsetRotation;
            else if (anchor != null)
                cameraTransform.rotation = anchorBaseRotation * offsetRotation;
            else
                cameraTransform.rotation = offsetRotation;

            ApplyRotationFollowers(anchor);
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Returns true when yaw should respond to drag and swipe input.
        /// </summary>
        private bool ShouldProcessYaw()
        {
            return axisMode == FreeAimAxisMode.HorizontalOnly || axisMode == FreeAimAxisMode.HorizontalAndVertical;
        }

        /// <summary>
        /// Returns true when pitch should respond to drag and swipe input.
        /// </summary>
        private bool ShouldProcessPitch()
        {
            return axisMode == FreeAimAxisMode.VerticalOnly || axisMode == FreeAimAxisMode.HorizontalAndVertical;
        }

        /// <summary>
        /// Determines the maximum angular step allowed for this frame based on clamp preference and turret stats.
        /// </summary>
        private float ResolveMaxAngularStep()
        {
            if (!clampTurnRate)
                return float.PositiveInfinity;

            if (activeTurret == null || !activeTurret.HasDefinition)
                return float.PositiveInfinity;

            float turnRate = activeTurret.ActiveStats.TurnRate;
            if (turnRate <= 0f)
                return 0f;

            return turnRate * Time.deltaTime;
        }

        /// <summary>
        /// Resolves pitch clamp values to protect from excessive vertical rotation.
        /// </summary>
        private Vector2 ResolvePitchClamp()
        {
            float up = pitchUpClampDegrees <= 0f ? float.PositiveInfinity : Mathf.Max(0f, pitchUpClampDegrees);
            float down = pitchDownClampDegrees <= 0f ? float.PositiveInfinity : Mathf.Max(0f, pitchDownClampDegrees);
            return new Vector2(up, down);
        }

        /// <summary>
        /// Builds the combined pitch and yaw rotation from the current offsets.
        /// </summary>
        private Quaternion BuildOffsetRotation()
        {
            return Quaternion.Euler(currentPitchOffset, currentYawOffset, 0f);
        }

        /// <summary>
        /// Normalizes an angle to the [-180, 180] range.
        /// </summary>
        private float NormalizeSignedAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }

        /// <summary>
        /// Rotates designated turret transforms to mirror the camera rotation while visible.
        /// </summary>
        private void ApplyRotationFollowers(Transform anchor)
        {
            if (rotationFollowers == null || rotationFollowers.Length == 0)
                return;

            Vector3 upAxis = anchor != null ? anchor.up : Vector3.up;
            Vector3 rightAxis = anchor != null ? anchor.right : Vector3.right;
            Quaternion yawRotation = Quaternion.AngleAxis(currentYawOffset, upAxis);
            Quaternion pitchRotation = Quaternion.AngleAxis(currentPitchOffset, rightAxis);
            for (int i = 0; i < rotationFollowers.Length; i++)
            {
                Transform target = rotationFollowers[i].Target;
                if (target == null)
                    continue;

                Quaternion baseRotation = rotationFollowerBaseRotations != null && rotationFollowerBaseRotations.Length > i ? rotationFollowerBaseRotations[i] : target.rotation;
                bool applyYaw = rotationFollowers[i].Axis == FreeAimFollowAxis.HorizontalOnly || rotationFollowers[i].Axis == FreeAimFollowAxis.HorizontalAndVertical;
                bool applyPitch = rotationFollowers[i].Axis == FreeAimFollowAxis.VerticalOnly || rotationFollowers[i].Axis == FreeAimFollowAxis.HorizontalAndVertical;
                Quaternion followerRotation = baseRotation;
                if (applyYaw && applyPitch)
                    followerRotation = yawRotation * pitchRotation * baseRotation;
                else if (applyYaw)
                    followerRotation = yawRotation * baseRotation;
                else if (applyPitch)
                    followerRotation = pitchRotation * baseRotation;

                target.rotation = followerRotation;
            }
        }

        /// <summary>
        /// Captures rotation followers and their initial rotation for free-aim alignment.
        /// </summary>
        private void CacheRotationFollowers()
        {
            if (activeTurret == null)
            {
                rotationFollowers = null;
                rotationFollowerBaseRotations = null;
                return;
            }

            rotationFollowers = activeTurret.GetFreeAimRotationFollowers();
            if (rotationFollowers == null || rotationFollowers.Length == 0)
            {
                rotationFollowerBaseRotations = null;
                return;
            }

            if (rotationFollowerBaseRotations == null || rotationFollowerBaseRotations.Length != rotationFollowers.Length)
                rotationFollowerBaseRotations = new Quaternion[rotationFollowers.Length];

            for (int i = 0; i < rotationFollowers.Length; i++)
            {
                Transform target = rotationFollowers[i].Target;
                rotationFollowerBaseRotations[i] = target != null ? target.rotation : Quaternion.identity;
            }
        }

        /// <summary>
        /// Maps the current muzzle forward as firing direction fallback.
        /// </summary>
        private Vector3 ResolveFireForward()
        {
            if (targetCamera != null)
                return targetCamera.transform.forward;

            if (activeTurret == null)
                return Vector3.forward;

            Quaternion baseRotation = anchorBaseRotation;
            Quaternion offsetRotation = BuildOffsetRotation();
            Vector3 forward = baseRotation * offsetRotation * Vector3.forward;
            return forward.normalized;
        }

        /// <summary>
        /// Caches inter-projectile delay instances to reduce allocations during sustained fire.
        /// </summary>
        private WaitForSeconds ResolveInterProjectileDelay(float delaySeconds)
        {
            if (delaySeconds <= 0f)
                return null;

            if (cachedFreeAimInterDelay == null || !Mathf.Approximately(cachedFreeAimDelaySeconds, delaySeconds))
            {
                cachedFreeAimDelaySeconds = delaySeconds;
                cachedFreeAimInterDelay = new WaitForSeconds(delaySeconds);
            }

            return cachedFreeAimInterDelay;
        }

        /// <summary>
        /// Provides the up axis used to distribute projectile offsets from the current perspective.
        /// </summary>
        private Vector3 ResolveFireUpAxis()
        {
            if (targetCamera != null)
                return targetCamera.transform.up;

            if (activeTurret != null)
                return activeTurret.transform.up;

            return Vector3.up;
        }

        /// <summary>
        /// Reacts to camera lerp progress to hide renderers and arm UI at configured thresholds.
        /// </summary>
        private void HandleLerpProgress(float normalized)
        {
            if (!renderersHiddenDuringFreeAim && normalized >= hideLerpThreshold)
            {
                HidePossessionRenderers();
                renderersHiddenDuringFreeAim = true;
                ShowRendererCollection(activeAuxiliaryRenderers, null);
                auxiliaryShownDuringFreeAim = true;
            }

            if (!uiArmed && normalized >= uiRevealLerpThreshold)
            {
                uiArmed = true;
                UIManager_MainScene manager = UIManager_MainScene.Instance;
                if (manager != null)
                    manager.ArmFreeAimControls();
            }
        }

        /// <summary>
        /// Restores the autonomous controller if it was previously active.
        /// </summary>
        private void RestoreAutoController()
        {
            if (cachedAutoController == null)
                return;

            cachedAutoController.enabled = cachedAutoEnabled;
            cachedAutoController = null;
            cachedAutoEnabled = false;
        }

        /// <summary>
        /// Determines whether the incoming tap lies within the free-aim reticle.
        /// </summary>
        private bool IsTapWithinReticle(Vector2 screenPoint)
        {
            UIManager_MainScene manager = UIManager_MainScene.Instance;
            if (manager == null)
                return false;

            return manager.IsWithinReticle(screenPoint);
        }

        /// <summary>
        /// Resolves the transform used as projectile origin during free-aim.
        /// </summary>
        private Transform ResolveFreeAimSpawnOrigin()
        {
            if (targetCamera != null)
                return targetCamera.transform;

            if (activeTurret != null && activeTurret.Muzzle != null)
                return activeTurret.Muzzle;

            return activeTurret != null ? activeTurret.transform : null;
        }

        /// <summary>
        /// Returns half of the allowed yaw clamp in degrees.
        /// </summary>
        private float ResolveYawClampHalf()
        {
            if (freeAimActive && fallbackYawClampDegrees <= 0f)
                return float.PositiveInfinity;

            float clampDegrees = fallbackYawClampDegrees;
            if (activeTurret != null && activeTurret.HasDefinition && activeTurret.ActiveStats.YawClampDegrees > 0f)
                clampDegrees = activeTurret.ActiveStats.YawClampDegrees;

            clampDegrees = Mathf.Max(0f, clampDegrees);
            return clampDegrees > 0f ? clampDegrees * 0.5f : float.PositiveInfinity;
        }

        /// <summary>
        /// Provides the renderer list to toggle, preferring authored references over full hierarchy.
        /// </summary>
        private Renderer[] ResolveRendererCache()
        {
            if (cachedTurretRenderers != null && cachedTurretRenderers.Length > 0)
                return cachedTurretRenderers;

            if (activeTurret == null)
                return null;

            cachedTurretRenderers = activeTurret.GetFreeAimRendererSet();
            return cachedTurretRenderers;
        }

        #region Auxiliary Visibility
        /// <summary>
        /// Resolves auxiliary renderers registered for the active turret merged with defaults.
        /// </summary>
        private Renderer[] ResolveAuxiliaryRenderers(PooledTurret turret)
        {
            Renderer[] registered = null;
            if (turret != null && auxiliaryRendererMap.ContainsKey(turret))
                registered = auxiliaryRendererMap[turret];

            bool hasDefault = auxiliaryFreeAimRenderers != null && auxiliaryFreeAimRenderers.Length > 0;
            bool hasRegistered = registered != null && registered.Length > 0;
            if (!hasDefault && !hasRegistered)
                return null;

            if (!hasDefault)
                return registered;

            if (!hasRegistered)
                return auxiliaryFreeAimRenderers;

            int defaultCount = auxiliaryFreeAimRenderers.Length;
            int registeredCount = registered.Length;
            int total = defaultCount + registeredCount;
            Renderer[] merged = new Renderer[total];
            for (int i = 0; i < defaultCount; i++)
                merged[i] = auxiliaryFreeAimRenderers[i];
            for (int j = 0; j < registeredCount; j++)
                merged[defaultCount + j] = registered[j];
            return merged;
        }

        /// <summary>
        /// Refreshes the auxiliary renderer cache using the configured layer mask.
        /// </summary>
        private void RefreshAuxiliaryRendererList()
        {
            if (auxiliaryRendererLayerMask == 0)
            {
                auxiliaryFreeAimRenderers = Array.Empty<Renderer>();
                return;
            }

            Renderer[] sceneRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            auxiliaryRendererBuffer.Clear();
            int maskValue = auxiliaryRendererLayerMask.value;

            for (int i = 0; i < sceneRenderers.Length; i++)
            {
                Renderer renderer = sceneRenderers[i];
                if (renderer == null)
                    continue;

                GameObject rendererObject = renderer.gameObject;
                int rendererLayerMask = 1 << rendererObject.layer;
                bool layerIncluded = (maskValue & rendererLayerMask) != 0;
                if (!layerIncluded)
                    continue;

                auxiliaryRendererBuffer.Add(renderer);
            }

            auxiliaryFreeAimRenderers = auxiliaryRendererBuffer.Count > 0 ? auxiliaryRendererBuffer.ToArray() : Array.Empty<Renderer>();
        }

        /// <summary>
        /// Builds auxiliary renderer cache once per runtime session to avoid repeated allocations.
        /// </summary>
        private void RefreshAuxiliaryRendererListIfNeeded()
        {
            if (!Application.isPlaying)
            {
                RefreshAuxiliaryRendererList();
                auxiliaryRuntimeInitialized = false;
                return;
            }

            if (auxiliaryRuntimeInitialized)
                return;

            RefreshAuxiliaryRendererList();
            auxiliaryRuntimeInitialized = true;
        }

        /// <summary>
        /// Forces every auxiliary renderer to remain hidden while not in free-aim.
        /// </summary>
        private void HideAllAuxiliaryRenderers()
        {
            auxiliaryShownDuringFreeAim = false;
            SetRendererCollectionEnabled(auxiliaryFreeAimRenderers, false);
            if (auxiliaryRendererMap.Count == 0)
                return;

            foreach (KeyValuePair<PooledTurret, Renderer[]> binding in auxiliaryRendererMap)
                SetRendererCollectionEnabled(binding.Value, false);
        }

        /// <summary>
        /// Enables or disables all renderers contained in the provided collection.
        /// </summary>
        private void SetRendererCollectionEnabled(Renderer[] renderers, bool enabled)
        {
            if (renderers == null || renderers.Length == 0)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (renderer.enabled != enabled)
                    renderer.enabled = enabled;
            }
        }
        #endregion

        /// <summary>
        /// Hides turret renderers if the camera approaches the chassis to avoid clipping.
        /// </summary>
        private void EvaluateCameraClipping()
        {
            if (targetCamera == null || activeTurret == null || renderersHiddenDuringFreeAim)
                return;

            float sqrThreshold = hideDistance * hideDistance;
            float sqrDistance = (targetCamera.transform.position - activeTurret.transform.position).sqrMagnitude;
            if (sqrDistance <= sqrThreshold)
            {
                HidePossessionRenderers();
                renderersHiddenDuringFreeAim = true;
            }
        }

        /// <summary>
        /// Deactivates turret renderers cached on possession.
        /// </summary>
        private void HidePossessionRenderers()
        {
            Renderer[] renderers = ResolveRendererCache();
            HideRendererCollection(renderers, ref cachedRendererStates);
        }

        /// <summary>
        /// Restores turret renderers visibility after free-aim concludes.
        /// </summary>
        private void ShowPossessionRenderers()
        {
            renderersHiddenDuringFreeAim = false;
            Renderer[] renderers = ResolveRendererCache();
            ShowRendererCollection(renderers, cachedRendererStates);
            cachedRendererStates = null;
        }

        /// <summary>
        /// Hides the provided renderer collection while caching previous states.
        /// </summary>
        private void HideRendererCollection(Renderer[] renderers, ref bool[] stateCache)
        {
            if (renderers == null || renderers.Length == 0)
                return;

            if (stateCache == null || stateCache.Length != renderers.Length)
                stateCache = new bool[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                stateCache[i] = renderer.enabled;
                if (renderer.enabled)
                    renderer.enabled = false;
            }
        }

        /// <summary>
        /// Restores renderer enablement based on the provided cache.
        /// </summary>
        private void ShowRendererCollection(Renderer[] renderers, bool[] stateCache)
        {
            if (renderers == null || renderers.Length == 0)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                bool shouldEnable = stateCache != null && stateCache.Length > i ? stateCache[i] : true;
                if (shouldEnable && !renderer.enabled)
                    renderer.enabled = true;
            }
        }
        #endregion

        #region Gizmos
        /// <summary>
        /// Shows the target camera offset relative to the active turret anchor.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (activeTurret == null || targetCamera == null)
                return;

            Transform anchor = ResolveCameraAnchor();
            if (anchor == null)
                return;

            Vector3 offsetPosition = anchor.position + anchor.TransformVector(cameraLocalOffset);
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.35f);
            Gizmos.DrawWireSphere(offsetPosition, 0.15f);
            Gizmos.DrawLine(anchor.position, offsetPosition);

            float clampHalf = ResolveYawClampHalf();
            if (!float.IsPositiveInfinity(clampHalf))
            {
                Vector3 forward = anchor.forward;
                Vector3 upAxis = anchor.up;
                float radius = 0.6f;
                Gizmos.color = new Color(0.95f, 0.65f, 0.2f, 0.75f);
                Gizmos.DrawLine(anchor.position, anchor.position + Quaternion.AngleAxis(-clampHalf, upAxis) * forward * radius);
                Gizmos.DrawLine(anchor.position, anchor.position + Quaternion.AngleAxis(clampHalf, upAxis) * forward * radius);
            }

            Renderer[] gizmoRenderers = auxiliaryFreeAimRenderers;
            if (gizmoRenderers != null && gizmoRenderers.Length > 0)
            {
                Gizmos.color = new Color(0.25f, 0.95f, 0.45f, 0.4f);
                for (int i = 0; i < gizmoRenderers.Length; i++)
                {
                    Renderer renderer = gizmoRenderers[i];
                    if (renderer == null)
                        continue;

                    Bounds bounds = renderer.bounds;
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                }
            }
        }

        /// <summary>
        /// Aligns free-aim permissions with the current phase when enabling the controller.
        /// </summary>
        private void SyncPhasePermissions()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
                return;

            phaseAllowsFreeAim = manager.CurrentPhase == GamePhase.Combat;
            if (!phaseAllowsFreeAim && freeAimActive)
                ExitFreeAim();
        }
        #endregion
        #endregion
    }
}
