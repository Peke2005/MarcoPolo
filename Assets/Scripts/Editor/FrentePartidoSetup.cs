#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.IO;

namespace FrentePartido.Editor
{
    public static class FrentePartidoSetup
    {
        private const string ASSETS = "Assets";
        private const string SCENES = "Assets/Scenes";
        private const string PREFABS = "Assets/Prefabs";
        private const string SO_PATH = "Assets/ScriptableObjects";

        [MenuItem("FrentePartido/>> SETUP COMPLETO <<", priority = 0)]
        public static void FullSetup()
        {
            if (!EditorUtility.DisplayDialog("Setup Frente Partido",
                "Esto creará todas las escenas, prefabs, ScriptableObjects y configuración del juego.\n\n¿Continuar?",
                "Sí, montar todo", "Cancelar")) return;

            ImportTMPEssentials();
            CreateScriptableObjects();
            CreatePrefabs();
            CreateAllScenes();
            SetupBuildSettings();

            // Open Boot scene so Play goes straight to the game
            EditorSceneManager.OpenScene($"{SCENES}/00_Boot.unity");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("¡Listo!", "Proyecto Frente Partido montado.\n\nDale Play para empezar.", "OK");
        }

        private static void ImportTMPEssentials()
        {
            // Check if TMP resources already imported
            if (AssetDatabase.FindAssets("LiberationSans SDF", new[] { "Assets" }).Length > 0)
                return;

            // Import from package
            string packagePath = "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage";
            if (!File.Exists(Path.GetFullPath(packagePath)))
            {
                // Try Library cache path
                var guids = AssetDatabase.FindAssets("TMP Essential Resources");
                foreach (var guid in guids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.EndsWith(".unitypackage"))
                    {
                        AssetDatabase.ImportPackage(p, false);
                        AssetDatabase.Refresh();
                        return;
                    }
                }
                Debug.LogWarning("[Setup] TMP Essential Resources not found. Import manually via Window > TextMeshPro > Import TMP Essential Resources");
                return;
            }
            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh();
        }

        [MenuItem("FrentePartido/1. Crear ScriptableObjects")]
        public static void CreateScriptableObjects()
        {
            EnsureFolder(SO_PATH);
            EnsureFolder($"{SO_PATH}/Weapons");
            EnsureFolder($"{SO_PATH}/Abilities");
            EnsureFolder($"{SO_PATH}/Balance");
            EnsureFolder($"{SO_PATH}/Maps");

            // Balance
            var balance = CreateOrLoad<Data.BalanceTuningData>($"{SO_PATH}/Balance/MainBalance.asset");
            EditorUtility.SetDirty(balance);

            // Weapon
            var rifle = CreateOrLoad<Data.WeaponData>($"{SO_PATH}/Weapons/Rifle_Standard.asset");
            rifle.weaponName = "Fusil Estándar";
            rifle.weaponId = "rifle_standard";
            rifle.damage = 20;
            rifle.fireRate = 4f;
            rifle.magazineSize = 8;
            rifle.reloadTime = 1.4f;
            rifle.spreadAngle = 2f;
            rifle.range = 30f;
            EditorUtility.SetDirty(rifle);

            // Abilities
            var dash = CreateOrLoad<Data.AbilityDefinition>($"{SO_PATH}/Abilities/Ability_Dash.asset");
            dash.abilityName = "Carrera Táctica";
            dash.abilityId = "dash";
            dash.type = Data.AbilityType.Dash;
            dash.cooldown = 7f;
            dash.duration = 0.3f;
            dash.value1 = 4f; // distance
            dash.value2 = 15f; // speed
            EditorUtility.SetDirty(dash);

            var shield = CreateOrLoad<Data.AbilityDefinition>($"{SO_PATH}/Abilities/Ability_Shield.asset");
            shield.abilityName = "Escudo Frontal";
            shield.abilityId = "shield";
            shield.type = Data.AbilityType.Shield;
            shield.cooldown = 12f;
            shield.duration = 2.5f;
            shield.value1 = 60f; // shield HP
            shield.value2 = 90f; // arc angle
            EditorUtility.SetDirty(shield);

            var mine = CreateOrLoad<Data.AbilityDefinition>($"{SO_PATH}/Abilities/Ability_Mine.asset");
            mine.abilityName = "Mina de Proximidad";
            mine.abilityId = "mine";
            mine.type = Data.AbilityType.Mine;
            mine.cooldown = 14f;
            mine.duration = 0f;
            mine.value1 = 35f; // damage
            mine.value2 = 1.5f; // detection radius
            EditorUtility.SetDirty(mine);

            // Map
            var map1 = CreateOrLoad<Data.MapDefinition>($"{SO_PATH}/Maps/Map_TrincheraPartida.asset");
            map1.mapName = "Trinchera Partida";
            map1.mapId = "trinchera";
            map1.spawnPointA = new Vector2(-7f, 0f);
            map1.spawnPointB = new Vector2(7f, 0f);
            map1.beaconPosition = Vector2.zero;
            map1.pickupSpawnPoints = new Vector2[] {
                new Vector2(-3.5f, 2.5f),
                new Vector2(3.5f, -2.5f),
                new Vector2(0f, 3.5f)
            };
            map1.boundsMin = new Vector2(-10f, -6f);
            map1.boundsMax = new Vector2(10f, 6f);
            EditorUtility.SetDirty(map1);

            Debug.Log("[Setup] ScriptableObjects creados.");
        }

        [MenuItem("FrentePartido/2. Crear Prefabs")]
        public static void CreatePrefabs()
        {
            EnsureFolder(PREFABS);
            EnsureFolder($"{PREFABS}/Characters");
            EnsureFolder($"{PREFABS}/Weapons");
            EnsureFolder($"{PREFABS}/Pickups");
            EnsureFolder($"{PREFABS}/Network");
            EnsureFolder($"{PREFABS}/Environment");

            var balance = AssetDatabase.LoadAssetAtPath<Data.BalanceTuningData>($"{SO_PATH}/Balance/MainBalance.asset");
            var rifle = AssetDatabase.LoadAssetAtPath<Data.WeaponData>($"{SO_PATH}/Weapons/Rifle_Standard.asset");
            var map1 = AssetDatabase.LoadAssetAtPath<Data.MapDefinition>($"{SO_PATH}/Maps/Map_TrincheraPartida.asset");
            var dashDef = AssetDatabase.LoadAssetAtPath<Data.AbilityDefinition>($"{SO_PATH}/Abilities/Ability_Dash.asset");
            var shieldDef = AssetDatabase.LoadAssetAtPath<Data.AbilityDefinition>($"{SO_PATH}/Abilities/Ability_Shield.asset");
            var mineDef = AssetDatabase.LoadAssetAtPath<Data.AbilityDefinition>($"{SO_PATH}/Abilities/Ability_Mine.asset");

            // === PLAYER PREFAB ===
            CreatePlayerPrefab(balance, rifle, map1, dashDef, shieldDef, mineDef);

            // === PROJECTILE (visual tracer) ===
            CreateProjectilePrefab();

            // === GRENADE ===
            CreateGrenadePrefab(balance);

            // === BEACON ===
            CreateBeaconPrefab(balance);

            // === PICKUPS ===
            CreatePickupPrefab("Pickup_Health", Data.PickupType.Health, Color.green, balance);
            CreatePickupPrefab("Pickup_Ammo", Data.PickupType.Ammo, Color.yellow, balance);
            CreatePickupPrefab("Pickup_Armor", Data.PickupType.Armor, Color.cyan, balance);

            // === MINE ===
            CreateMinePrefab();

            Debug.Log("[Setup] Prefabs creados.");
        }

        [MenuItem("FrentePartido/3. Crear Escenas")]
        public static void CreateAllScenes()
        {
            EnsureFolder(SCENES);

            var balance = AssetDatabase.LoadAssetAtPath<Data.BalanceTuningData>($"{SO_PATH}/Balance/MainBalance.asset");
            var map1 = AssetDatabase.LoadAssetAtPath<Data.MapDefinition>($"{SO_PATH}/Maps/Map_TrincheraPartida.asset");

            CreateBootScene();
            CreateAuthScene();
            CreateMainMenuScene();
            CreateLobbyScene();
            CreateGameScene(balance, map1);
            CreatePostMatchScene();

            Debug.Log("[Setup] Escenas creadas.");
        }

        [MenuItem("FrentePartido/4. Configurar Build Settings")]
        public static void SetupBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{SCENES}/00_Boot.unity", true),
                new EditorBuildSettingsScene($"{SCENES}/01_Auth.unity", true),
                new EditorBuildSettingsScene($"{SCENES}/02_MainMenu.unity", true),
                new EditorBuildSettingsScene($"{SCENES}/03_Lobby.unity", true),
                new EditorBuildSettingsScene($"{SCENES}/04_Game.unity", true),
                new EditorBuildSettingsScene($"{SCENES}/05_PostMatch.unity", true),
            };
            Debug.Log("[Setup] Build settings configurados con 6 escenas.");
        }

        // =====================================================================
        // PREFAB CREATORS
        // =====================================================================

        private static void CreatePlayerPrefab(Data.BalanceTuningData balance, Data.WeaponData rifle,
            Data.MapDefinition map, Data.AbilityDefinition dash, Data.AbilityDefinition shield, Data.AbilityDefinition mine)
        {
            var go = new GameObject("Player");

            // Visual
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform);
            var bodySr = body.AddComponent<SpriteRenderer>();
            bodySr.color = Color.white;
            bodySr.sortingOrder = 10;
            CreatePlaceholderSprite(bodySr, 0.5f, Color.white);

            // Weapon pivot
            var weaponPivot = new GameObject("WeaponPivot");
            weaponPivot.transform.SetParent(go.transform);
            var weaponVisual = new GameObject("WeaponSprite");
            weaponVisual.transform.SetParent(weaponPivot.transform);
            weaponVisual.transform.localPosition = new Vector3(0.4f, 0, 0);
            var weaponSr = weaponVisual.AddComponent<SpriteRenderer>();
            weaponSr.sortingOrder = 11;
            CreatePlaceholderSprite(weaponSr, 0.2f, Color.gray);

            // Fire point
            var firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(weaponPivot.transform);
            firePoint.transform.localPosition = new Vector3(0.7f, 0, 0);

            // Physics
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;

            // Netcode
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<Unity.Netcode.Components.NetworkTransform>();

            // Player scripts
            var input = go.AddComponent<Player.PlayerInputReader>();

            var motor = go.AddComponent<Player.PlayerMotor2D>();
            SetPrivateField(motor, "balanceData", balance);
            SetPrivateField(motor, "mapDefinition", map);

            var aim = go.AddComponent<Player.PlayerAimController>();
            SetPrivateField(aim, "weaponPivot", weaponPivot.transform);

            var state = go.AddComponent<Player.PlayerStateController>();

            var health = go.AddComponent<Player.PlayerHealth>();
            SetPrivateField(health, "balanceData", balance);

            var presentation = go.AddComponent<Player.PlayerPresentation>();
            SetPrivateField(presentation, "mainSprite", bodySr);
            SetPrivateField(presentation, "weaponSprite", weaponSr);

            // Combat
            var weapon = go.AddComponent<Combat.WeaponController>();
            SetPrivateField(weapon, "weaponData", rifle);
            SetPrivateField(weapon, "firePoint", firePoint.transform);

            // Abilities
            var abilityCtrl = go.AddComponent<Abilities.AbilityController>();
            SetPrivateField(abilityCtrl, "availableAbilities", new Data.AbilityDefinition[] { dash, shield, mine });
            go.AddComponent<Abilities.DashAbility>();
            go.AddComponent<Abilities.ShieldAbility>();
            go.AddComponent<Abilities.MineAbility>();

            // HitFlash
            var hitFlash = go.AddComponent<Combat.HitFlashController>();
            SetPrivateField(hitFlash, "targetRenderer", bodySr);

            // Set layer
            go.layer = LayerMask.NameToLayer("Default");

            SavePrefab(go, $"{PREFABS}/Characters/Player.prefab");
        }

        private static void CreateProjectilePrefab()
        {
            var go = new GameObject("Projectile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 15;
            CreatePlaceholderSprite(sr, 0.08f, Color.yellow);

            go.AddComponent<Rigidbody2D>();
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.04f;
            col.isTrigger = true;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.1f;
            trail.startWidth = 0.05f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = Color.yellow;
            trail.endColor = new Color(1, 1, 0, 0);

            go.AddComponent<Combat.Projectile>();

            SavePrefab(go, $"{PREFABS}/Weapons/Projectile.prefab");
        }

        private static void CreateGrenadePrefab(Data.BalanceTuningData balance)
        {
            var go = new GameObject("Grenade");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 12;
            CreatePlaceholderSprite(sr, 0.15f, Color.gray);

            go.AddComponent<Rigidbody2D>();
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.12f;

            go.AddComponent<NetworkObject>();
            go.AddComponent<Unity.Netcode.Components.NetworkTransform>();

            var grenade = go.AddComponent<Combat.GrenadeController>();
            SetPrivateField(grenade, "balanceData", balance);
            SetPrivateField(grenade, "spriteRenderer", sr);

            SavePrefab(go, $"{PREFABS}/Weapons/Grenade.prefab");
        }

        private static void CreateBeaconPrefab(Data.BalanceTuningData balance)
        {
            var go = new GameObject("Beacon");

            // Zone visual (circle)
            var zoneVisual = new GameObject("ZoneVisual");
            zoneVisual.transform.SetParent(go.transform);
            var zoneSr = zoneVisual.AddComponent<SpriteRenderer>();
            zoneSr.sortingOrder = 1;
            CreatePlaceholderSprite(zoneSr, balance != null ? balance.beaconRadius : 2f, new Color(1, 1, 1, 0.2f));

            // Center indicator
            var center = new GameObject("CenterPoint");
            center.transform.SetParent(go.transform);
            var centerSr = center.AddComponent<SpriteRenderer>();
            centerSr.sortingOrder = 2;
            CreatePlaceholderSprite(centerSr, 0.3f, Color.white);

            // Collider for detection
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = balance != null ? balance.beaconRadius : 2f;
            col.isTrigger = true;

            var beacon = go.AddComponent<Match.BeaconCaptureController>();
            SetPrivateField(beacon, "_balance", balance);
            SetPrivateField(beacon, "_zoneVisual", zoneSr);

            SavePrefab(go, $"{PREFABS}/Environment/Beacon.prefab");
        }

        private static void CreatePickupPrefab(string name, Data.PickupType type, Color color, Data.BalanceTuningData balance)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;
            CreatePlaceholderSprite(sr, 0.25f, color);

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;
            col.isTrigger = true;

            go.AddComponent<NetworkObject>();

            Pickups.PickupBase pickup = null;
            switch (type)
            {
                case Data.PickupType.Health:
                    pickup = go.AddComponent<Pickups.HealthPickup>();
                    SetPrivateField(pickup, "_balance", balance);
                    break;
                case Data.PickupType.Ammo:
                    pickup = go.AddComponent<Pickups.AmmoPickup>();
                    break;
                case Data.PickupType.Armor:
                    pickup = go.AddComponent<Pickups.ArmorPickup>();
                    SetPrivateField(pickup, "_balance", balance);
                    break;
            }

            if (pickup != null)
            {
                SetPrivateField(pickup, "_spriteRenderer", sr);
            }

            SavePrefab(go, $"{PREFABS}/Pickups/{name}.prefab");
        }

        private static void CreateMinePrefab()
        {
            var go = new GameObject("Mine");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;
            CreatePlaceholderSprite(sr, 0.2f, Color.red);

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.15f;
            col.isTrigger = true;

            go.AddComponent<NetworkObject>();

            SavePrefab(go, $"{PREFABS}/Weapons/Mine.prefab");
        }

        // =====================================================================
        // SCENE CREATORS
        // =====================================================================

        private static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var cam = new GameObject("Main Camera");
            var camera = cam.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            cam.AddComponent<AudioListener>();
            cam.tag = "MainCamera";

            // NetworkManager
            var netMgr = new GameObject("NetworkManager");
            var nm = netMgr.AddComponent<NetworkManager>();
            var transport = netMgr.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig();
            // Player prefab will be assigned after scenes save
            netMgr.AddComponent<Networking.NetworkSessionManager>();

            // Bootstrap
            var bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<Core.GameBootstrap>();

            EditorSceneManager.SaveScene(scene, $"{SCENES}/00_Boot.unity");
        }

        private static void CreateAuthScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateSceneCamera(new Color(0.05f, 0.05f, 0.1f));

            var canvas = CreateUICanvas("AuthCanvas");
            var canvasT = canvas.GetComponent<RectTransform>();

            // Title
            var title = CreateUIText(canvasT, "TitleText", "FRENTE PARTIDO", 48,
                new Vector2(0, 280), new Vector2(600, 80));
            title.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
            title.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

            // ── Login Panel ──
            var loginPanel = new GameObject("LoginPanel");
            loginPanel.transform.SetParent(canvasT, false);
            var loginRT = loginPanel.AddComponent<RectTransform>();
            loginRT.sizeDelta = new Vector2(400, 350);
            var loginImg = loginPanel.AddComponent<Image>();
            loginImg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

            var loginTitle = CreateUIText(loginRT, "LoginTitle", "INICIAR SESIÓN", 28,
                new Vector2(0, 130), new Vector2(350, 40));
            loginTitle.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

            var loginUser = CreateUIInputField(loginRT, "LoginUsername",
                new Vector2(0, 60), new Vector2(300, 40), "Usuario o email");
            var loginPass = CreateUIInputField(loginRT, "LoginPassword",
                new Vector2(0, 5), new Vector2(300, 40), "Contraseña");
            loginPass.GetComponent<TMP_InputField>().contentType = TMP_InputField.ContentType.Password;

            var loginBtn = CreateUIButton(loginRT, "LoginButton", "ENTRAR",
                new Vector2(0, -55), new Vector2(250, 50), new Color(0.2f, 0.6f, 0.3f));
            var goToRegBtn = CreateUIButton(loginRT, "GoToRegisterButton", "¿No tienes cuenta? Regístrate",
                new Vector2(0, -115), new Vector2(300, 40), new Color(0.3f, 0.3f, 0.5f));

            // ── Register Panel ──
            var regPanel = new GameObject("RegisterPanel");
            regPanel.transform.SetParent(canvasT, false);
            var regRT = regPanel.AddComponent<RectTransform>();
            regRT.sizeDelta = new Vector2(400, 450);
            var regImg = regPanel.AddComponent<Image>();
            regImg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

            var regTitle = CreateUIText(regRT, "RegisterTitle", "REGISTRO", 28,
                new Vector2(0, 180), new Vector2(350, 40));
            regTitle.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

            var regUser = CreateUIInputField(regRT, "RegUsername",
                new Vector2(0, 110), new Vector2(300, 40), "Usuario");
            var regEmail = CreateUIInputField(regRT, "RegEmail",
                new Vector2(0, 55), new Vector2(300, 40), "Email");
            var regPass = CreateUIInputField(regRT, "RegPassword",
                new Vector2(0, 0), new Vector2(300, 40), "Contraseña (mín. 6)");
            regPass.GetComponent<TMP_InputField>().contentType = TMP_InputField.ContentType.Password;
            var regDisplay = CreateUIInputField(regRT, "RegDisplayName",
                new Vector2(0, -55), new Vector2(300, 40), "Nombre para mostrar (opcional)");

            var regBtn = CreateUIButton(regRT, "RegisterButton", "REGISTRARSE",
                new Vector2(0, -120), new Vector2(250, 50), new Color(0.2f, 0.4f, 0.7f));
            var goToLoginBtn = CreateUIButton(regRT, "GoToLoginButton", "¿Ya tienes cuenta? Inicia sesión",
                new Vector2(0, -180), new Vector2(300, 40), new Color(0.3f, 0.3f, 0.5f));
            regPanel.SetActive(false);

            // ── Status / Loading ──
            var statusText = CreateUIText(canvasT, "StatusText", "", 16,
                new Vector2(0, -220), new Vector2(400, 30));
            statusText.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

            var loading = new GameObject("LoadingIndicator");
            loading.transform.SetParent(canvasT, false);
            var loadingRT = loading.AddComponent<RectTransform>();
            loadingRT.anchoredPosition = new Vector2(0, -250);
            loadingRT.sizeDelta = new Vector2(40, 40);
            var loadingImg = loading.AddComponent<Image>();
            loadingImg.color = Color.white;
            loading.SetActive(false);

            // ── Wire AuthUI ──
            var authUI = canvas.AddComponent<Auth.AuthUI>();
            SetPrivateField(authUI, "_loginPanel", loginPanel);
            SetPrivateField(authUI, "_registerPanel", regPanel);
            SetPrivateField(authUI, "_loginUsername", loginUser.GetComponent<TMP_InputField>());
            SetPrivateField(authUI, "_loginPassword", loginPass.GetComponent<TMP_InputField>());
            SetPrivateField(authUI, "_loginButton", loginBtn.GetComponent<Button>());
            SetPrivateField(authUI, "_goToRegisterButton", goToRegBtn.GetComponent<Button>());
            SetPrivateField(authUI, "_regUsername", regUser.GetComponent<TMP_InputField>());
            SetPrivateField(authUI, "_regEmail", regEmail.GetComponent<TMP_InputField>());
            SetPrivateField(authUI, "_regPassword", regPass.GetComponent<TMP_InputField>());
            SetPrivateField(authUI, "_regDisplayName", regDisplay.GetComponent<TMP_InputField>());
            SetPrivateField(authUI, "_registerButton", regBtn.GetComponent<Button>());
            SetPrivateField(authUI, "_goToLoginButton", goToLoginBtn.GetComponent<Button>());
            SetPrivateField(authUI, "_statusText", statusText.GetComponent<TMP_Text>());
            SetPrivateField(authUI, "_loadingIndicator", loading);

            EditorSceneManager.SaveScene(scene, $"{SCENES}/01_Auth.unity");
        }

        private static void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateSceneCamera(new Color(0.08f, 0.08f, 0.12f));

            // Canvas
            var canvas = CreateUICanvas("MainMenuCanvas");
            var canvasT = canvas.GetComponent<RectTransform>();

            // Title
            var title = CreateUIText(canvasT, "TitleText", "FRENTE PARTIDO", 48,
                new Vector2(0, 200), new Vector2(600, 80));
            title.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
            title.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

            // Player name input
            var nameLabel = CreateUIText(canvasT, "NameLabel", "Tu nombre:", 18,
                new Vector2(0, 100), new Vector2(300, 30));
            var nameInput = CreateUIInputField(canvasT, "PlayerNameInput",
                new Vector2(0, 65), new Vector2(300, 40), "Soldado");

            // Buttons
            var createBtn = CreateUIButton(canvasT, "CreateGameButton", "CREAR PARTIDA",
                new Vector2(0, 0), new Vector2(250, 50), new Color(0.2f, 0.6f, 0.3f));
            var joinBtn = CreateUIButton(canvasT, "JoinGameButton", "UNIRSE A PARTIDA",
                new Vector2(0, -65), new Vector2(250, 50), new Color(0.2f, 0.4f, 0.7f));
            var optionsBtn = CreateUIButton(canvasT, "OptionsButton", "OPCIONES",
                new Vector2(0, -130), new Vector2(250, 50), new Color(0.4f, 0.4f, 0.4f));
            var quitBtn = CreateUIButton(canvasT, "QuitButton", "SALIR",
                new Vector2(0, -195), new Vector2(250, 50), new Color(0.6f, 0.2f, 0.2f));

            // Join Code Panel (hidden)
            var joinPanel = new GameObject("JoinCodePanel");
            joinPanel.transform.SetParent(canvasT, false);
            var joinPanelRT = joinPanel.AddComponent<RectTransform>();
            joinPanelRT.sizeDelta = new Vector2(350, 150);
            var joinPanelImg = joinPanel.AddComponent<Image>();
            joinPanelImg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            var codeInput = CreateUIInputField(joinPanelRT, "JoinCodeInput",
                new Vector2(0, 20), new Vector2(250, 40), "Código...");
            var confirmJoinBtn = CreateUIButton(joinPanelRT, "ConfirmJoinButton", "UNIRSE",
                new Vector2(-60, -35), new Vector2(120, 40), new Color(0.2f, 0.6f, 0.3f));
            var cancelJoinBtn = CreateUIButton(joinPanelRT, "CancelJoinButton", "CANCELAR",
                new Vector2(60, -35), new Vector2(120, 40), new Color(0.5f, 0.3f, 0.3f));
            joinPanel.SetActive(false);

            // Options Panel (hidden)
            var optPanel = new GameObject("OptionsPanel");
            optPanel.transform.SetParent(canvasT, false);
            var optPanelRT = optPanel.AddComponent<RectTransform>();
            optPanelRT.sizeDelta = new Vector2(400, 250);
            optPanel.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            CreateUIText(optPanelRT, "MusicLabel", "Música", 16, new Vector2(-100, 60), new Vector2(100, 30));
            var musicSlider = CreateUISlider(optPanelRT, "MusicVolumeSlider", new Vector2(50, 60), new Vector2(200, 20));
            CreateUIText(optPanelRT, "SFXLabel", "SFX", 16, new Vector2(-100, 20), new Vector2(100, 30));
            var sfxSlider = CreateUISlider(optPanelRT, "SFXVolumeSlider", new Vector2(50, 20), new Vector2(200, 20));
            var closeOptBtn = CreateUIButton(optPanelRT, "CloseOptionsButton", "CERRAR",
                new Vector2(0, -60), new Vector2(120, 40), new Color(0.4f, 0.4f, 0.4f));
            optPanel.SetActive(false);

            // Status text
            var statusText = CreateUIText(canvasT, "StatusText", "", 16,
                new Vector2(0, -260), new Vector2(400, 30));

            // Loading indicator
            var loading = CreateUIText(canvasT, "LoadingIndicator", "Conectando...", 20,
                new Vector2(0, 0), new Vector2(300, 50));
            loading.SetActive(false);

            // Wire MainMenuUI
            var menuUI = canvas.AddComponent<UI.MainMenuUI>();
            SetPrivateField(menuUI, "_createGameButton", createBtn.GetComponent<Button>());
            SetPrivateField(menuUI, "_joinGameButton", joinBtn.GetComponent<Button>());
            SetPrivateField(menuUI, "_optionsButton", optionsBtn.GetComponent<Button>());
            SetPrivateField(menuUI, "_quitButton", quitBtn.GetComponent<Button>());
            SetPrivateField(menuUI, "_playerNameInput", nameInput.GetComponent<TMP_InputField>());
            SetPrivateField(menuUI, "_joinCodePanel", joinPanel);
            SetPrivateField(menuUI, "_joinCodeInput", codeInput.GetComponent<TMP_InputField>());
            SetPrivateField(menuUI, "_confirmJoinButton", confirmJoinBtn.GetComponent<Button>());
            SetPrivateField(menuUI, "_cancelJoinButton", cancelJoinBtn.GetComponent<Button>());
            SetPrivateField(menuUI, "_optionsPanel", optPanel);
            SetPrivateField(menuUI, "_musicVolumeSlider", musicSlider.GetComponent<Slider>());
            SetPrivateField(menuUI, "_sfxVolumeSlider", sfxSlider.GetComponent<Slider>());
            SetPrivateField(menuUI, "_closeOptionsButton", closeOptBtn.GetComponent<Button>());
            SetPrivateField(menuUI, "_statusText", statusText.GetComponent<TMP_Text>());
            SetPrivateField(menuUI, "_loadingIndicator", loading);

            EditorSceneManager.SaveScene(scene, $"{SCENES}/02_MainMenu.unity");
        }

        private static void CreateLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSceneCamera(new Color(0.08f, 0.1f, 0.15f));

            var canvas = CreateUICanvas("LobbyCanvas");
            var ct = canvas.GetComponent<RectTransform>();

            // Title
            CreateUIText(ct, "LobbyTitle", "SALA DE ESPERA", 36, new Vector2(0, 250), new Vector2(500, 60));

            // Join code
            var codeText = CreateUIText(ct, "JoinCodeText", "CÓDIGO: ------", 28,
                new Vector2(0, 190), new Vector2(400, 50));
            var copyBtn = CreateUIButton(ct, "CopyCodeButton", "COPIAR",
                new Vector2(220, 190), new Vector2(100, 40), new Color(0.3f, 0.5f, 0.7f));

            // Players
            var p1Name = CreateUIText(ct, "Player1NameText", "Jugador 1", 22, new Vector2(-120, 110), new Vector2(200, 40));
            var p2Name = CreateUIText(ct, "Player2NameText", "Esperando...", 22, new Vector2(120, 110), new Vector2(200, 40));
            var p1Status = CreateUIText(ct, "Player1StatusText", "Conectado", 16, new Vector2(-120, 80), new Vector2(200, 30));
            var p2Status = CreateUIText(ct, "Player2StatusText", "---", 16, new Vector2(120, 80), new Vector2(200, 30));

            // Ability selection
            CreateUIText(ct, "AbilityLabel", "HABILIDAD:", 18, new Vector2(0, 30), new Vector2(200, 30));
            var dashBtn = CreateUIButton(ct, "DashButton", "DASH", new Vector2(-120, -10), new Vector2(100, 40), new Color(0.3f, 0.6f, 0.3f));
            var shieldBtn = CreateUIButton(ct, "ShieldButton", "ESCUDO", new Vector2(0, -10), new Vector2(100, 40), new Color(0.3f, 0.4f, 0.7f));
            var mineBtn = CreateUIButton(ct, "MineButton", "MINA", new Vector2(120, -10), new Vector2(100, 40), new Color(0.7f, 0.3f, 0.3f));
            var selectedAbText = CreateUIText(ct, "SelectedAbilityText", "Carrera Táctica", 16, new Vector2(0, -50), new Vector2(250, 30));

            // Ability highlight borders
            Image dashHighlight = CreateHighlightBorder(dashBtn);
            Image shieldHighlight = CreateHighlightBorder(shieldBtn);
            Image mineHighlight = CreateHighlightBorder(mineBtn);

            // Faction
            var blueBtn = CreateUIButton(ct, "BlueButton", "AZUL", new Vector2(-60, -90), new Vector2(100, 40), new Color(0.2f, 0.3f, 0.8f));
            var redBtn = CreateUIButton(ct, "RedButton", "ROJO", new Vector2(60, -90), new Vector2(100, 40), new Color(0.8f, 0.2f, 0.2f));

            // Actions
            var readyBtn = CreateUIButton(ct, "ReadyButton", "LISTO", new Vector2(-80, -160), new Vector2(140, 50), new Color(0.2f, 0.7f, 0.3f));
            var startBtn = CreateUIButton(ct, "StartGameButton", "INICIAR", new Vector2(80, -160), new Vector2(140, 50), new Color(0.8f, 0.6f, 0.1f));
            var leaveBtn = CreateUIButton(ct, "LeaveButton", "SALIR", new Vector2(0, -230), new Vector2(140, 40), new Color(0.5f, 0.2f, 0.2f));
            var readyBtnText = readyBtn.GetComponentInChildren<TMP_Text>();

            // Wire LobbyUI
            var lobbyUI = canvas.AddComponent<UI.LobbyUI>();
            SetPrivateField(lobbyUI, "_joinCodeText", codeText.GetComponent<TMP_Text>());
            SetPrivateField(lobbyUI, "_copyCodeButton", copyBtn.GetComponent<Button>());
            SetPrivateField(lobbyUI, "_player1NameText", p1Name.GetComponent<TMP_Text>());
            SetPrivateField(lobbyUI, "_player2NameText", p2Name.GetComponent<TMP_Text>());
            SetPrivateField(lobbyUI, "_player1StatusText", p1Status.GetComponent<TMP_Text>());
            SetPrivateField(lobbyUI, "_player2StatusText", p2Status.GetComponent<TMP_Text>());
            SetPrivateField(lobbyUI, "_dashButton", dashBtn.GetComponent<Button>());
            SetPrivateField(lobbyUI, "_shieldButton", shieldBtn.GetComponent<Button>());
            SetPrivateField(lobbyUI, "_mineButton", mineBtn.GetComponent<Button>());
            SetPrivateField(lobbyUI, "_selectedAbilityText", selectedAbText.GetComponent<TMP_Text>());
            SetPrivateField(lobbyUI, "_blueButton", blueBtn.GetComponent<Button>());
            SetPrivateField(lobbyUI, "_redButton", redBtn.GetComponent<Button>());
            SetPrivateField(lobbyUI, "_readyButton", readyBtn.GetComponent<Button>());
            SetPrivateField(lobbyUI, "_startGameButton", startBtn.GetComponent<Button>());
            SetPrivateField(lobbyUI, "_leaveButton", leaveBtn.GetComponent<Button>());
            SetPrivateField(lobbyUI, "_readyButtonText", readyBtnText);
            SetPrivateField(lobbyUI, "_abilityHighlights", new Image[] { dashHighlight, shieldHighlight, mineHighlight });

            EditorSceneManager.SaveScene(scene, $"{SCENES}/03_Lobby.unity");
        }

        private static void CreateGameScene(Data.BalanceTuningData balance, Data.MapDefinition map)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var cam = CreateSceneCamera(new Color(0.15f, 0.12f, 0.1f));
            cam.GetComponent<Camera>().orthographicSize = 7;

            // --- MAP ---
            // Floor
            var floor = new GameObject("Floor");
            var floorSr = floor.AddComponent<SpriteRenderer>();
            floorSr.sortingOrder = -10;
            CreatePlaceholderSprite(floorSr, 10f, new Color(0.25f, 0.22f, 0.18f));
            floor.transform.localScale = new Vector3(2f, 1.2f, 1f);

            // Walls (map bounds)
            CreateWall("Wall_Top", new Vector2(0, 6.2f), new Vector2(21, 0.5f));
            CreateWall("Wall_Bottom", new Vector2(0, -6.2f), new Vector2(21, 0.5f));
            CreateWall("Wall_Left", new Vector2(-10.2f, 0), new Vector2(0.5f, 13f));
            CreateWall("Wall_Right", new Vector2(10.2f, 0), new Vector2(0.5f, 13f));

            // Cover objects (symmetric)
            CreateCover("Cover_TopLeft", new Vector2(-4f, 3f), new Vector2(1.5f, 0.4f));
            CreateCover("Cover_TopRight", new Vector2(4f, 3f), new Vector2(1.5f, 0.4f));
            CreateCover("Cover_BotLeft", new Vector2(-4f, -3f), new Vector2(1.5f, 0.4f));
            CreateCover("Cover_BotRight", new Vector2(4f, -3f), new Vector2(1.5f, 0.4f));
            CreateCover("Cover_MidLeft", new Vector2(-6f, 0), new Vector2(0.4f, 2f));
            CreateCover("Cover_MidRight", new Vector2(6f, 0), new Vector2(0.4f, 2f));
            CreateCover("Cover_CenterTop", new Vector2(0, 2f), new Vector2(1f, 0.4f));
            CreateCover("Cover_CenterBot", new Vector2(0, -2f), new Vector2(1f, 0.4f));

            // Spawn markers
            CreateSpawnMarker("SpawnA", map != null ? map.spawnPointA : new Vector2(-7, 0), Color.blue);
            CreateSpawnMarker("SpawnB", map != null ? map.spawnPointB : new Vector2(7, 0), Color.red);

            // --- BEACON ---
            var beaconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Environment/Beacon.prefab");
            if (beaconPrefab != null)
            {
                var beaconInst = (GameObject)PrefabUtility.InstantiatePrefab(beaconPrefab);
                beaconInst.transform.position = Vector3.zero;
            }

            // --- GAME MANAGERS ---
            var managers = new GameObject("GameManagers");

            var matchMgr = managers.AddComponent<Match.MatchManager>();
            SetPrivateField(matchMgr, "_balance", balance);

            var roundMgr = managers.AddComponent<Match.RoundManager>();
            SetPrivateField(roundMgr, "_balance", balance);

            var suddenDeath = managers.AddComponent<Match.SuddenDeathController>();
            SetPrivateField(suddenDeath, "_balance", balance);

            var netGameState = managers.AddComponent<NetworkObject>();
            managers.AddComponent<Networking.NetworkGameState>();

            var spawnMgr = managers.AddComponent<Networking.PlayerSpawnManager>();
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Characters/Player.prefab");
            SetPrivateField(spawnMgr, "_playerPrefab", playerPrefab);
            SetPrivateField(spawnMgr, "_mapDefinition", map);

            var pickupSpawner = managers.AddComponent<Pickups.PickupSpawner>();
            SetPrivateField(pickupSpawner, "_mapDefinition", map);
            SetPrivateField(pickupSpawner, "_balance", balance);
            SetPrivateField(pickupSpawner, "_healthPickupPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Pickups/Pickup_Health.prefab"));
            SetPrivateField(pickupSpawner, "_ammoPickupPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Pickups/Pickup_Ammo.prefab"));
            SetPrivateField(pickupSpawner, "_armorPickupPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFABS}/Pickups/Pickup_Armor.prefab"));

            // --- HUD ---
            var hudCanvas = CreateUICanvas("HUDCanvas");
            var hct = hudCanvas.GetComponent<RectTransform>();

            // Health
            var healthBar = CreateUIBar(hct, "HealthBar", new Vector2(-350, -250), new Vector2(200, 20), Color.red);
            var healthText = CreateUIText(hct, "HealthText", "100", 18, new Vector2(-350, -225), new Vector2(60, 25));
            var armorBar = CreateUIBar(hct, "ArmorBar", new Vector2(-350, -275), new Vector2(200, 12), Color.cyan);
            var armorText = CreateUIText(hct, "ArmorText", "", 14, new Vector2(-350, -275), new Vector2(60, 20));

            // Ammo
            var ammoText = CreateUIText(hct, "AmmoText", "8/8", 22, new Vector2(350, -250), new Vector2(100, 30));
            var reloadBar = CreateUIBar(hct, "ReloadBar", new Vector2(350, -275), new Vector2(100, 8), new Color(0.9f, 0.9f, 0.2f));

            // Round timer
            var timerGo = CreateUIText(hct, "RoundTimerText", "1:30", 32, new Vector2(0, 270), new Vector2(120, 50));
            var timerUI = timerGo.AddComponent<UI.RoundTimerUI>();
            SetPrivateField(timerUI, "_timerText", timerGo.GetComponent<TMP_Text>());

            // Score
            var scoreText = CreateUIText(hct, "RoundScoreText", "0 - 0", 28, new Vector2(0, 230), new Vector2(150, 40));

            // Announcement
            var announcement = CreateUIText(hct, "AnnouncementText", "", 40, new Vector2(0, 50), new Vector2(500, 60));

            // Grenade icon
            var grenadeIcon = new GameObject("GrenadeIcon");
            grenadeIcon.transform.SetParent(hct, false);
            var grenadeRT = grenadeIcon.AddComponent<RectTransform>();
            grenadeRT.anchoredPosition = new Vector2(300, -270);
            grenadeRT.sizeDelta = new Vector2(30, 30);
            var grenadeImg = grenadeIcon.AddComponent<Image>();
            grenadeImg.color = Color.white;

            // Ability cooldown
            var abilityCd = new GameObject("AbilityCooldown");
            abilityCd.transform.SetParent(hct, false);
            var abCdRT = abilityCd.AddComponent<RectTransform>();
            abCdRT.anchoredPosition = new Vector2(250, -270);
            abCdRT.sizeDelta = new Vector2(40, 40);
            var iconImg = abilityCd.AddComponent<Image>();
            var cdOverlay = new GameObject("CooldownOverlay");
            cdOverlay.transform.SetParent(abilityCd.transform, false);
            var cdOvRT = cdOverlay.AddComponent<RectTransform>();
            cdOvRT.anchorMin = Vector2.zero; cdOvRT.anchorMax = Vector2.one;
            cdOvRT.sizeDelta = Vector2.zero;
            var cdOvImg = cdOverlay.AddComponent<Image>();
            cdOvImg.type = Image.Type.Filled;
            cdOvImg.fillMethod = Image.FillMethod.Radial360;
            cdOvImg.color = new Color(0, 0, 0, 0.7f);
            var cdText = CreateUIText(abilityCd.GetComponent<RectTransform>(), "CooldownText", "", 14, Vector2.zero, new Vector2(40, 40));

            var cooldownWidget = abilityCd.AddComponent<UI.CooldownWidget>();
            SetPrivateField(cooldownWidget, "_iconImage", iconImg);
            SetPrivateField(cooldownWidget, "_cooldownOverlay", cdOvImg);
            SetPrivateField(cooldownWidget, "_cooldownText", cdText.GetComponent<TMP_Text>());

            // Beacon UI
            var beaconPanel = new GameObject("BeaconPanel");
            beaconPanel.transform.SetParent(hct, false);
            var bpRT = beaconPanel.AddComponent<RectTransform>();
            bpRT.anchoredPosition = new Vector2(0, 180);
            bpRT.sizeDelta = new Vector2(250, 50);
            var beaconStatus = CreateUIText(bpRT, "BeaconStatusText", "FARO ACTIVO", 16, new Vector2(0, 10), new Vector2(200, 25));
            var beaconCapBar = CreateUIBar(bpRT, "BeaconCaptureBar", new Vector2(0, -10), new Vector2(180, 12), new Color(1, 0.8f, 0));

            // Wire HUDController
            var hudCtrl = hudCanvas.AddComponent<UI.HUDController>();
            SetPrivateField(hudCtrl, "_healthText", healthText.GetComponent<TMP_Text>());
            SetPrivateField(hudCtrl, "_healthBar", healthBar.GetComponent<Image>());
            SetPrivateField(hudCtrl, "_armorText", armorText.GetComponent<TMP_Text>());
            SetPrivateField(hudCtrl, "_armorBar", armorBar.GetComponent<Image>());
            SetPrivateField(hudCtrl, "_ammoText", ammoText.GetComponent<TMP_Text>());
            SetPrivateField(hudCtrl, "_reloadBar", reloadBar.GetComponent<Image>());
            SetPrivateField(hudCtrl, "_roundTimer", timerUI);
            SetPrivateField(hudCtrl, "_roundScoreText", scoreText.GetComponent<TMP_Text>());
            SetPrivateField(hudCtrl, "_announcementText", announcement.GetComponent<TMP_Text>());
            SetPrivateField(hudCtrl, "_grenadeIcon", grenadeImg);
            SetPrivateField(hudCtrl, "_abilityCooldown", cooldownWidget);
            SetPrivateField(hudCtrl, "_beaconPanel", beaconPanel);
            SetPrivateField(hudCtrl, "_beaconCaptureBar", beaconCapBar.GetComponent<Image>());
            SetPrivateField(hudCtrl, "_beaconStatusText", beaconStatus.GetComponent<TMP_Text>());

            EditorSceneManager.SaveScene(scene, $"{SCENES}/04_Game.unity");
        }

        private static void CreatePostMatchScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSceneCamera(new Color(0.05f, 0.05f, 0.08f));

            var canvas = CreateUICanvas("ResultsCanvas");
            var ct = canvas.GetComponent<RectTransform>();
            var cg = canvas.AddComponent<CanvasGroup>();

            var winnerText = CreateUIText(ct, "WinnerText", "¡VICTORIA!", 48, new Vector2(0, 180), new Vector2(500, 80));
            winnerText.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

            var p1Name = CreateUIText(ct, "Player1NameText", "Jugador 1", 24, new Vector2(-120, 80), new Vector2(200, 40));
            var p2Name = CreateUIText(ct, "Player2NameText", "Jugador 2", 24, new Vector2(120, 80), new Vector2(200, 40));
            var p1Score = CreateUIText(ct, "Player1ScoreText", "3", 60, new Vector2(-120, 20), new Vector2(100, 80));
            var p2Score = CreateUIText(ct, "Player2ScoreText", "1", 60, new Vector2(120, 20), new Vector2(100, 80));
            CreateUIText(ct, "VsText", "vs", 24, new Vector2(0, 30), new Vector2(60, 40));

            var rematchBtn = CreateUIButton(ct, "RematchButton", "REVANCHA", new Vector2(-80, -100), new Vector2(160, 50), new Color(0.2f, 0.7f, 0.3f));
            var menuBtn = CreateUIButton(ct, "MainMenuButton", "MENÚ", new Vector2(80, -100), new Vector2(160, 50), new Color(0.4f, 0.4f, 0.4f));

            var resultsUI = canvas.AddComponent<UI.ResultsUI>();
            SetPrivateField(resultsUI, "_winnerText", winnerText.GetComponent<TMP_Text>());
            SetPrivateField(resultsUI, "_player1NameText", p1Name.GetComponent<TMP_Text>());
            SetPrivateField(resultsUI, "_player2NameText", p2Name.GetComponent<TMP_Text>());
            SetPrivateField(resultsUI, "_player1ScoreText", p1Score.GetComponent<TMP_Text>());
            SetPrivateField(resultsUI, "_player2ScoreText", p2Score.GetComponent<TMP_Text>());
            SetPrivateField(resultsUI, "_rematchButton", rematchBtn.GetComponent<Button>());
            SetPrivateField(resultsUI, "_mainMenuButton", menuBtn.GetComponent<Button>());
            SetPrivateField(resultsUI, "_canvasGroup", cg);

            EditorSceneManager.SaveScene(scene, $"{SCENES}/05_PostMatch.unity");
        }

        // =====================================================================
        // UI HELPERS
        // =====================================================================

        private static GameObject CreateUICanvas(string name)
        {
            var canvasGo = new GameObject(name);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvasGo;
        }

        private static TMP_FontAsset _cachedFont;
        private static TMP_FontAsset GetDefaultTMPFont()
        {
            if (_cachedFont != null) return _cachedFont;

            // Try to find LiberationSans SDF in project
            var guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            if (guids.Length > 0)
            {
                _cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (_cachedFont != null) return _cachedFont;
            }

            // Fallback: search any TMP font asset
            guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            if (guids.Length > 0)
                _cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));

            return _cachedFont;
        }

        private static GameObject CreateUIText(RectTransform parent, string name, string text, int fontSize,
            Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            var font = GetDefaultTMPFont();
            if (font != null) tmp.font = font;

            return go;
        }

        private static Image CreateHighlightBorder(GameObject buttonGo)
        {
            var highlight = new GameObject("Highlight");
            highlight.transform.SetParent(buttonGo.transform, false);
            var rt = highlight.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = new Vector2(6, 6);
            rt.anchoredPosition = Vector2.zero;

            var img = highlight.AddComponent<Image>();
            img.color = Color.yellow;
            img.type = Image.Type.Sliced;
            img.enabled = false; // Hidden by default, LobbyUI enables the selected one
            return img;
        }

        private static GameObject CreateUIButton(RectTransform parent, string name, string label,
            Vector2 position, Vector2 size, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRT = textGo.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            var font = GetDefaultTMPFont();
            if (font != null) tmp.font = font;

            return go;
        }

        private static GameObject CreateUIInputField(RectTransform parent, string name,
            Vector2 position, Vector2 size, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.25f);

            // Text area
            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(go.transform, false);
            var taRT = textArea.AddComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
            taRT.offsetMin = new Vector2(10, 5); taRT.offsetMax = new Vector2(-10, -5);
            textArea.AddComponent<RectMask2D>();

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textArea.transform, false);
            var phRT = phGo.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.sizeDelta = Vector2.zero;
            var phTMP = phGo.AddComponent<TextMeshProUGUI>();
            phTMP.text = placeholder;
            phTMP.fontSize = 16;
            phTMP.fontStyle = FontStyles.Italic;
            phTMP.color = new Color(0.6f, 0.6f, 0.6f);
            var inputFont = GetDefaultTMPFont();
            if (inputFont != null) phTMP.font = inputFont;

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(textArea.transform, false);
            var txtRT = txtGo.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;
            var txtTMP = txtGo.AddComponent<TextMeshProUGUI>();
            txtTMP.fontSize = 16;
            txtTMP.color = Color.white;
            if (inputFont != null) txtTMP.font = inputFont;

            var inputField = go.AddComponent<TMP_InputField>();
            inputField.textViewport = taRT;
            inputField.textComponent = txtTMP;
            inputField.placeholder = phTMP;
            inputField.fontAsset = txtTMP.font;

            return go;
        }

        private static GameObject CreateUISlider(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.25f); bgRT.anchorMax = new Vector2(1, 0.75f);
            bgRT.sizeDelta = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);

            // Fill Area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var faRT = fillArea.AddComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0, 0.25f); faRT.anchorMax = new Vector2(1, 0.75f);
            faRT.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.AddComponent<RectTransform>();
            fillRT.sizeDelta = Vector2.zero;
            fill.AddComponent<Image>().color = new Color(0.3f, 0.7f, 0.4f);

            // Handle
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var haRT = handleArea.AddComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
            haRT.sizeDelta = Vector2.zero;

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var hRT = handle.AddComponent<RectTransform>();
            hRT.sizeDelta = new Vector2(20, 0);
            handle.AddComponent<Image>().color = Color.white;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = hRT;
            slider.value = 0.7f;

            return go;
        }

        private static GameObject CreateUIBar(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var bg = new GameObject(name + "_BG");
            bg.transform.SetParent(parent, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchoredPosition = pos;
            bgRT.sizeDelta = size;
            bg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);

            var fill = new GameObject(name);
            fill.transform.SetParent(bg.transform, false);
            var fillRT = fill.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = color;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;

            return fill;
        }

        // =====================================================================
        // MAP HELPERS
        // =====================================================================

        private static void CreateWall(string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0;
            CreatePlaceholderSprite(sr, 0.5f, new Color(0.35f, 0.3f, 0.25f));
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
        }

        private static void CreateCover(string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 4;
            CreatePlaceholderSprite(sr, 0.5f, new Color(0.4f, 0.35f, 0.28f));
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
        }

        private static void CreateSpawnMarker(string name, Vector2 pos, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -5;
            CreatePlaceholderSprite(sr, 0.5f, new Color(color.r, color.g, color.b, 0.3f));
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(0.8f, 0.8f);
        }

        // =====================================================================
        // UTILITIES
        // =====================================================================

        private static GameObject CreateSceneCamera(Color bgColor)
        {
            var cam = new GameObject("Main Camera");
            var camera = cam.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6;
            camera.backgroundColor = bgColor;
            camera.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.AddComponent<AudioListener>();
            cam.tag = "MainCamera";
            return cam;
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static void CreatePlaceholderSprite(SpriteRenderer sr, float size, Color color)
        {
            sr.color = color;
            // Use built-in white square sprite
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sr.sprite == null)
            {
                // Fallback: create texture
                var tex = new Texture2D(4, 4);
                var pixels = new Color[16];
                for (int i = 0; i < 16; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }

            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(size, size);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;

            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }

            Debug.LogWarning($"[Setup] Field '{fieldName}' not found on {target.GetType().Name}");
        }
    }
}
#endif
