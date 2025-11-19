using UnityEditor;
using UnityEngine;

namespace Scriptables.Turrets.Editor
{
    /// <summary>
    /// Editor utility that streamlines authoring turret classes, projectile archetypes and their pools.
    /// </summary>
    public class TurretLibraryWindow : EditorWindow
    {
        #region Variables And Properties
        #region Serialized Fields

        [Tooltip("Target folder where turret assets will be created.")]
        private string turretFolder = "Assets/ScriptableObjects/Turrets";

        [Tooltip("Target folder where projectile assets will be created.")]
        private string projectileFolder = "Assets/ScriptableObjects/Projectiles";

        [Tooltip("Base name used when generating new assets.")]
        private string assetBaseName = "NewTurret";

        [Tooltip("Currently selected turret definition.")]
        private TurretClassDefinition selectedTurret;

        [Tooltip("Currently selected projectile definition.")]
        private ProjectileDefinition selectedProjectile;

        [Tooltip("Scroll position used to keep the layout navigable.")]
        private Vector2 scrollPosition;

        [Tooltip("Active panel index for toolbar switching.")]
        private int toolbarIndex;

        #endregion
        #endregion

        #region Methods
        #region Menu

        [MenuItem("Tools/Turrets/Library")]
        private static void Open()
        {
            TurretLibraryWindow window = GetWindow<TurretLibraryWindow>("Turret Library");
            window.Show();
        }

        #endregion

        #region GUI

        /// <summary>
        /// Draws the main editor window content including toolbar, creation and inline editing.
        /// </summary>
        private void OnGUI()
        {
            DrawFolderConfig();
            EditorGUILayout.Space(6f);

            toolbarIndex = GUILayout.Toolbar(toolbarIndex, new string[] { "Turrets", "Projectiles" });
            EditorGUILayout.Space(4f);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (toolbarIndex == 0)
                DrawTurretTab();
            else
                DrawProjectileTab();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Renders folder and naming configuration.
        /// </summary>
        private void DrawFolderConfig()
        {
            EditorGUILayout.LabelField("Asset Targets", EditorStyles.boldLabel);
            turretFolder = EditorGUILayout.TextField("Turret Folder", turretFolder);
            projectileFolder = EditorGUILayout.TextField("Projectile Folder", projectileFolder);
            assetBaseName = EditorGUILayout.TextField("Base Name", assetBaseName);
        }

        /// <summary>
        /// Renders the turret-specific UI.
        /// </summary>
        private void DrawTurretTab()
        {
            EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
            selectedTurret = (TurretClassDefinition)EditorGUILayout.ObjectField("Turret", selectedTurret, typeof(TurretClassDefinition), false);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            if (GUILayout.Button("Create Turret Bundle", GUILayout.Height(28f)))
                CreateTurretBundle();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Inline Editing", EditorStyles.boldLabel);
            if (selectedTurret != null)
                DrawTurretInspector(selectedTurret);
        }

        /// <summary>
        /// Renders the projectile-specific UI.
        /// </summary>
        private void DrawProjectileTab()
        {
            EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
            selectedProjectile = (ProjectileDefinition)EditorGUILayout.ObjectField("Projectile", selectedProjectile, typeof(ProjectileDefinition), false);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            if (GUILayout.Button("Create Projectile", GUILayout.Height(28f)))
                CreateProjectileOnly();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Inline Editing", EditorStyles.boldLabel);
            if (selectedProjectile != null)
                DrawProjectileInspector(selectedProjectile);
        }

        /// <summary>
        /// Renders inline inspector for a turret definition.
        /// </summary>
        private void DrawTurretInspector(TurretClassDefinition turret)
        {
            SerializedObject serialized = new SerializedObject(turret);
            serialized.Update();

            EditorGUILayout.LabelField("Turret Definition", EditorStyles.largeLabel);
            DrawProperty(serialized, "key");
            DrawProperty(serialized, "displayName");
            DrawProperty(serialized, "description");
            DrawProperty(serialized, "icon");
            DrawProperty(serialized, "turretPrefab");
            DrawProperty(serialized, "turretPool");
            DrawProperty(serialized, "projectile");
            DrawProperty(serialized, "projectilePool");
            DrawProperty(serialized, "durability", true);
            DrawProperty(serialized, "targeting", true);
            DrawProperty(serialized, "automaticFire", true);
            DrawProperty(serialized, "freeAimFire", true);
            DrawProperty(serialized, "modeSwitchSeconds");
            DrawProperty(serialized, "economy", true);
            DrawProperty(serialized, "sustain", true);
            DrawProperty(serialized, "placement", true);

            serialized.ApplyModifiedProperties();
        }

        /// <summary>
        /// Renders inline inspector for a projectile definition.
        /// </summary>
        private void DrawProjectileInspector(ProjectileDefinition projectile)
        {
            SerializedObject serialized = new SerializedObject(projectile);
            serialized.Update();

            EditorGUILayout.LabelField("Projectile Definition", EditorStyles.largeLabel);
            DrawProperty(serialized, "key");
            DrawProperty(serialized, "icon");
            DrawProperty(serialized, "projectilePrefab");
            DrawProperty(serialized, "pool");
            DrawProperty(serialized, "damage");
            DrawProperty(serialized, "criticalChance");
            DrawProperty(serialized, "criticalMultiplier");
            DrawProperty(serialized, "pierceFalloffRatio");
            DrawProperty(serialized, "speed");
            DrawProperty(serialized, "maxPiercedTargets");
            DrawProperty(serialized, "lifetimeSeconds");
            DrawProperty(serialized, "maxDistance");
            DrawProperty(serialized, "splashRadius");
            DrawProperty(serialized, "statusChance");
            DrawProperty(serialized, "statusDurationSeconds");

            serialized.ApplyModifiedProperties();
        }

        /// <summary>
        /// Helper that wraps EditorGUILayout.PropertyField with null safety.
        /// </summary>
        private void DrawProperty(SerializedObject serializedObject, string name, bool includeChildren = false)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property == null)
                return;

            EditorGUILayout.PropertyField(property, includeChildren);
        }

        #endregion

        #region Creation

        /// <summary>
        /// Creates a turret, projectile and their pools, wiring references automatically.
        /// </summary>
        private void CreateTurretBundle()
        {
            EnsureFolder(turretFolder);
            EnsureFolder(projectileFolder);

            TurretClassDefinition turret = CreateInstance<TurretClassDefinition>();
            ProjectileDefinition projectile = CreateInstance<ProjectileDefinition>();
            TurretPoolSO turretPool = CreateInstance<TurretPoolSO>();
            ProjectilePoolSO projectilePool = CreateInstance<ProjectilePoolSO>();

            turretPool.SetFallbackDefinition(turret);
            projectilePool.SetFallbackDefinition(projectile);

            SerializedObject turretSO = new SerializedObject(turret);
            turretSO.FindProperty("projectile").objectReferenceValue = projectile;
            turretSO.FindProperty("projectilePool").objectReferenceValue = projectilePool;
            turretSO.FindProperty("turretPool").objectReferenceValue = turretPool;
            turretSO.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject projectileSO = new SerializedObject(projectile);
            projectileSO.FindProperty("pool").objectReferenceValue = projectilePool;
            projectileSO.ApplyModifiedPropertiesWithoutUndo();

            SaveAsset(turret, string.Format("{0}/{1}_Class.asset", turretFolder, assetBaseName));
            SaveAsset(projectile, string.Format("{0}/{1}_Projectile.asset", projectileFolder, assetBaseName));
            SaveAsset(turretPool, string.Format("{0}/{1}_TurretPool.asset", turretFolder, assetBaseName));
            SaveAsset(projectilePool, string.Format("{0}/{1}_ProjectilePool.asset", projectileFolder, assetBaseName));

            selectedTurret = turret;
            selectedProjectile = projectile;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Creates a projectile and its pool asset.
        /// </summary>
        private void CreateProjectileOnly()
        {
            EnsureFolder(projectileFolder);

            ProjectileDefinition projectile = CreateInstance<ProjectileDefinition>();
            ProjectilePoolSO projectilePool = CreateInstance<ProjectilePoolSO>();
            projectilePool.SetFallbackDefinition(projectile);

            SerializedObject projectileSO = new SerializedObject(projectile);
            projectileSO.FindProperty("pool").objectReferenceValue = projectilePool;
            projectileSO.ApplyModifiedPropertiesWithoutUndo();

            SaveAsset(projectile, string.Format("{0}/{1}_Projectile.asset", projectileFolder, assetBaseName));
            SaveAsset(projectilePool, string.Format("{0}/{1}_ProjectilePool.asset", projectileFolder, assetBaseName));

            selectedProjectile = projectile;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Saves a newly created asset to a unique path.
        /// </summary>
        private void SaveAsset(Object asset, string path)
        {
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(asset, uniquePath);
        }

        /// <summary>
        /// Ensures the target folder exists by creating missing segments.
        /// </summary>
        private void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = string.Format("{0}/{1}", current, segments[i]);
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);

                current = next;
            }
        }

        #endregion
        #endregion
    }
}
