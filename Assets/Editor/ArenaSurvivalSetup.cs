using System.IO;
using System.Linq;
using ArenaSurvival;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArenaSurvivalEditor
{
    /// <summary>
    /// Автосборка проекта. Меню Arena Survival > Build All (или автоматически при первом открытии проекта):
    /// слои, материалы, префаб монстра, игровая сцена Arena, сцена MainMenu, Build Settings.
    /// Это инструмент разработки, в самой игре он не участвует.
    /// </summary>
    [InitializeOnLoad]
    public static class ArenaSurvivalSetup
    {
        private const string SceneDir = "Assets/Scenes";
        private const string MatDir = "Assets/Materials";
        private const string PrefabDir = "Assets/Prefabs";
        private const string GameScenePath = SceneDir + "/Arena.unity";
        private const string MenuScenePath = SceneDir + "/MainMenu.unity";

        private const float ArenaHalf = 15f;

        static ArenaSurvivalSetup()
        {
            EditorApplication.delayCall += AutoBuildIfMissing;
        }

        private static void AutoBuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (File.Exists(GameScenePath) && File.Exists(MenuScenePath)) return;

            // Не трогаем несохранённую работу пользователя: тогда нужен ручной запуск из меню.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return;

            Debug.Log("Arena Survival: сцены не найдены, выполняется автоматическая сборка проекта...");
            BuildAll(false);
        }

        [MenuItem("Arena Survival/Build All (сцены, префаб, материалы)")]
        public static void BuildAllFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Arena Survival", "Остановите режим Play и повторите.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll(true);
        }

        private static void BuildAll(bool interactive)
        {
            foreach (string folder in new[] { "Scenes", "Materials", "Prefabs", "Models", "Animations", "Audio", "UI" })
                EnsureFolder("Assets/" + folder);

            EnsureLayer("Player");
            EnsureLayer("Enemy");
            EnsureLayer("Obstacle");
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (playerLayer < 0 || enemyLayer < 0 || obstacleLayer < 0)
            {
                Debug.LogError("Arena Survival: не удалось создать слои Player / Enemy / Obstacle. Создайте их вручную (Project Settings > Tags and Layers).");
                return;
            }

            Mats mats = CreateMaterials();

            // Префаб монстра создаётся во временной сцене.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnemyAI monsterPrefab = CreateMonsterPrefab(mats, enemyLayer, 1 << obstacleLayer);

            BuildGameScene(mats, monsterPrefab, playerLayer, enemyLayer, obstacleLayer);
            BuildMenuScene(mats);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (interactive)
                EditorUtility.DisplayDialog("Arena Survival",
                    "Готово: сцены MainMenu и Arena созданы и добавлены в Build Settings.\nОткрыта сцена MainMenu — нажмите Play.", "OK");
            else
                Debug.Log("Arena Survival: сборка завершена. Открыта сцена MainMenu — нажмите Play.");
        }

        // ------------------------------------------------------------------ папки / слои / serialized
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void EnsureLayer(string layerName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null) return;

            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError("Поле '" + field + "' не найдено в " + target.GetType().Name); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError("Поле '" + field + "' не найдено в " + target.GetType().Name); return; }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError("Поле '" + field + "' не найдено в " + target.GetType().Name); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetWaves(WaveManager manager, int[] counts)
        {
            var so = new SerializedObject(manager);
            SerializedProperty p = so.FindProperty("waves");
            p.arraySize = counts.Length;
            for (int i = 0; i < counts.Length; i++)
                p.GetArrayElementAtIndex(i).FindPropertyRelative("enemyCount").intValue = counts[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBuildScenes(params string[] first)
        {
            var list = EditorBuildSettings.scenes.ToList();
            foreach (string path in first)
                if (!list.Any(s => s.path == path)) list.Add(new EditorBuildSettingsScene(path, true));

            var ordered = first.Select(p => list.First(s => s.path == p)).ToList();
            ordered.AddRange(list.Where(s => !first.Contains(s.path)));
            EditorBuildSettings.scenes = ordered.ToArray();
        }

        // ------------------------------------------------------------------ материалы
        private class Mats
        {
            public Material floor, wall, pillar, steel, darkSteel, skin, brown, gold, blade, black;
            public Material monsterBody, monsterDark, monsterHead, eye, bone;
        }

        private static Mats CreateMaterials()
        {
            return new Mats
            {
                floor = MakeMat("Floor_Stone", new Color(0.36f, 0.36f, 0.4f), 0.1f),
                wall = MakeMat("Wall_Stone", new Color(0.24f, 0.23f, 0.27f), 0.1f),
                pillar = MakeMat("Pillar_Stone", new Color(0.45f, 0.43f, 0.47f), 0.15f),
                steel = MakeMat("Knight_Steel", new Color(0.62f, 0.66f, 0.72f), 0.5f),
                darkSteel = MakeMat("Knight_DarkSteel", new Color(0.32f, 0.35f, 0.42f), 0.4f),
                skin = MakeMat("Knight_Skin", new Color(0.93f, 0.74f, 0.58f), 0.2f),
                brown = MakeMat("Knight_Leather", new Color(0.4f, 0.26f, 0.14f), 0.1f),
                gold = MakeMat("Knight_Gold", new Color(0.9f, 0.72f, 0.2f), 0.6f),
                blade = MakeMat("Sword_Blade", new Color(0.85f, 0.88f, 0.95f), 0.8f),
                black = MakeMat("Dark_Visor", new Color(0.05f, 0.05f, 0.07f), 0.1f),
                monsterBody = MakeMat("Monster_Body", new Color(0.45f, 0.2f, 0.6f), 0.2f),
                monsterDark = MakeMat("Monster_Dark", new Color(0.25f, 0.1f, 0.35f), 0.2f),
                monsterHead = MakeMat("Monster_Head", new Color(0.35f, 0.65f, 0.3f), 0.2f),
                eye = MakeMat("Monster_Eye", new Color(1f, 0.15f, 0.1f), 0.3f),
                bone = MakeMat("Monster_Bone", new Color(0.9f, 0.88f, 0.75f), 0.2f)
            };
        }

        private static Material MakeMat(string name, Color color, float smoothness)
        {
            string path = MatDir + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) Debug.LogError("Arena Survival: не найден шейдер URP/Lit — материалы будут розовыми.");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(shader) { name = name };

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ модели из примитивов
        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            return go;
        }

        private static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        private static void ConfigureRig(ProceduralRig rig, Transform model, Transform body, Transform la, Transform ra, Transform ll, Transform rl)
        {
            SetRef(rig, "modelRoot", model);
            SetRef(rig, "body", body);
            SetRef(rig, "leftArm", la);
            SetRef(rig, "rightArm", ra);
            SetRef(rig, "leftLeg", ll);
            SetRef(rig, "rightLeg", rl);
        }

        private static ProceduralRig BuildKnight(Transform root, Mats m)
        {
            Transform model = Pivot("Model", root, Vector3.zero);
            Transform leftLeg = Pivot("LeftLeg", model, new Vector3(-0.17f, 0.8f, 0f));
            Transform rightLeg = Pivot("RightLeg", model, new Vector3(0.17f, 0.8f, 0f));
            Prim(PrimitiveType.Cube, "Leg", leftLeg, new Vector3(0f, -0.4f, 0f), new Vector3(0.22f, 0.8f, 0.26f), m.darkSteel);
            Prim(PrimitiveType.Cube, "Leg", rightLeg, new Vector3(0f, -0.4f, 0f), new Vector3(0.22f, 0.8f, 0.26f), m.darkSteel);

            Transform body = Pivot("Body", model, new Vector3(0f, 0.8f, 0f));
            Prim(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0.4f, 0f), new Vector3(0.7f, 0.8f, 0.4f), m.steel);
            Prim(PrimitiveType.Cube, "Belt", body, new Vector3(0f, 0.04f, 0f), new Vector3(0.72f, 0.1f, 0.42f), m.brown);
            Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.95f, 0f), new Vector3(0.42f, 0.42f, 0.42f), m.skin);
            Prim(PrimitiveType.Sphere, "Helmet", body, new Vector3(0f, 1.04f, 0f), new Vector3(0.48f, 0.32f, 0.48f), m.steel);
            Prim(PrimitiveType.Cube, "Visor", body, new Vector3(0f, 0.95f, 0.2f), new Vector3(0.3f, 0.07f, 0.05f), m.black);

            Transform leftArm = Pivot("LeftArm", body, new Vector3(-0.46f, 0.75f, 0f));
            Prim(PrimitiveType.Cube, "Arm", leftArm, new Vector3(0f, -0.3f, 0f), new Vector3(0.18f, 0.6f, 0.18f), m.steel);
            Prim(PrimitiveType.Cube, "Shield", leftArm, new Vector3(-0.12f, -0.35f, 0.08f), new Vector3(0.06f, 0.5f, 0.4f), m.gold);

            Transform rightArm = Pivot("RightArm", body, new Vector3(0.46f, 0.75f, 0f));
            Prim(PrimitiveType.Cube, "Arm", rightArm, new Vector3(0f, -0.3f, 0f), new Vector3(0.18f, 0.6f, 0.18f), m.steel);
            Prim(PrimitiveType.Cube, "Hand", rightArm, new Vector3(0f, -0.62f, 0f), new Vector3(0.14f, 0.14f, 0.14f), m.skin);
            Prim(PrimitiveType.Cube, "SwordGuard", rightArm, new Vector3(0f, -0.62f, 0.18f), new Vector3(0.32f, 0.06f, 0.08f), m.gold);
            Prim(PrimitiveType.Cube, "SwordBlade", rightArm, new Vector3(0f, -0.62f, 0.75f), new Vector3(0.07f, 0.1f, 1.0f), m.blade);

            ProceduralRig rig = root.gameObject.AddComponent<ProceduralRig>();
            ConfigureRig(rig, model, body, leftArm, rightArm, leftLeg, rightLeg);
            return rig;
        }

        private static ProceduralRig BuildMonster(Transform root, Mats m)
        {
            Transform model = Pivot("Model", root, Vector3.zero);
            Transform leftLeg = Pivot("LeftLeg", model, new Vector3(-0.22f, 0.5f, 0f));
            Transform rightLeg = Pivot("RightLeg", model, new Vector3(0.22f, 0.5f, 0f));
            Prim(PrimitiveType.Cube, "Leg", leftLeg, new Vector3(0f, -0.25f, 0f), new Vector3(0.3f, 0.5f, 0.3f), m.monsterDark);
            Prim(PrimitiveType.Cube, "Leg", rightLeg, new Vector3(0f, -0.25f, 0f), new Vector3(0.3f, 0.5f, 0.3f), m.monsterDark);

            Transform body = Pivot("Body", model, new Vector3(0f, 0.5f, 0f));
            Prim(PrimitiveType.Sphere, "Torso", body, new Vector3(0f, 0.5f, 0f), new Vector3(1.0f, 1.0f, 0.85f), m.monsterBody);
            Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 1.15f, 0.12f), new Vector3(0.6f, 0.55f, 0.6f), m.monsterHead);
            Prim(PrimitiveType.Sphere, "EyeL", body, new Vector3(-0.14f, 1.2f, 0.4f), new Vector3(0.13f, 0.13f, 0.13f), m.eye);
            Prim(PrimitiveType.Sphere, "EyeR", body, new Vector3(0.14f, 1.2f, 0.4f), new Vector3(0.13f, 0.13f, 0.13f), m.eye);
            Prim(PrimitiveType.Cylinder, "HornL", body, new Vector3(-0.2f, 1.5f, 0.1f), new Vector3(0.08f, 0.16f, 0.08f), m.bone, new Vector3(0f, 0f, 20f));
            Prim(PrimitiveType.Cylinder, "HornR", body, new Vector3(0.2f, 1.5f, 0.1f), new Vector3(0.08f, 0.16f, 0.08f), m.bone, new Vector3(0f, 0f, -20f));

            Transform leftArm = Pivot("LeftArm", body, new Vector3(-0.58f, 0.8f, 0f));
            Prim(PrimitiveType.Cube, "Arm", leftArm, new Vector3(0f, -0.35f, 0f), new Vector3(0.2f, 0.7f, 0.2f), m.monsterHead);
            Prim(PrimitiveType.Cube, "Claws", leftArm, new Vector3(0f, -0.75f, 0.05f), new Vector3(0.2f, 0.1f, 0.22f), m.bone);

            Transform rightArm = Pivot("RightArm", body, new Vector3(0.58f, 0.8f, 0f));
            Prim(PrimitiveType.Cube, "Arm", rightArm, new Vector3(0f, -0.35f, 0f), new Vector3(0.2f, 0.7f, 0.2f), m.monsterHead);
            Prim(PrimitiveType.Cube, "Claws", rightArm, new Vector3(0f, -0.75f, 0.05f), new Vector3(0.2f, 0.1f, 0.22f), m.bone);

            ProceduralRig rig = root.gameObject.AddComponent<ProceduralRig>();
            ConfigureRig(rig, model, body, leftArm, rightArm, leftLeg, rightLeg);
            return rig;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        private static EnemyAI CreateMonsterPrefab(Mats mats, int enemyLayer, int obstacleMask)
        {
            var go = new GameObject("Monster");

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 0.85f, 0f);
            cc.stepOffset = 0.3f;

            ProceduralRig rig = BuildMonster(go.transform, mats);
            var health = go.AddComponent<EnemyHealth>();
            var ai = go.AddComponent<EnemyAI>();

            SetRef(health, "rig", rig);
            SetRef(ai, "rig", rig);
            SetInt(ai, "obstacleMask", obstacleMask);
            SetLayerRecursive(go, enemyLayer);

            string path = PrefabDir + "/Monster.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved.GetComponent<EnemyAI>();
        }

        // ------------------------------------------------------------------ игровая сцена
        private static void BuildGameScene(Mats mats, EnemyAI monsterPrefab, int playerLayer, int enemyLayer, int obstacleLayer)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int obstacleMask = 1 << obstacleLayer;
            int enemyMask = 1 << enemyLayer;
            int spawnBlockMask = obstacleMask | enemyMask | (1 << playerLayer);

            CreateLighting();
            CreateArena(mats, obstacleLayer);
            Transform[] spawnPoints = CreateSpawnPoints();

            GameObject playerGo = CreatePlayer(mats, playerLayer, enemyMask, obstacleMask);
            PlayerHealth playerHealth = playerGo.GetComponent<PlayerHealth>();

            Camera cam = CreateCamera(playerGo.transform);
            SetRef(playerGo.GetComponent<PlayerController>(), "aimCamera", cam);

            GameManager gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            SetRef(gameManager, "player", playerHealth);

            new GameObject("AudioManager").AddComponent<AudioManager>();

            Transform enemiesRoot = new GameObject("Enemies").transform;
            WaveManager waveManager = new GameObject("WaveManager").AddComponent<WaveManager>();
            SetRef(waveManager, "enemyPrefab", monsterPrefab);
            SetRef(waveManager, "player", playerHealth);
            SetRef(waveManager, "gameManager", gameManager);
            SetRef(waveManager, "enemiesRoot", enemiesRoot);
            SetInt(waveManager, "spawnBlockMask", spawnBlockMask);
            SetRefArray(waveManager, "spawnPoints", spawnPoints);
            SetWaves(waveManager, new[] { 3, 5, 7 });

            CreateGameUi(gameManager, playerHealth, waveManager);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.52f);

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
        }

        private static Camera CreateCamera(Transform target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 14f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            go.AddComponent<AudioListener>();
            var follow = go.AddComponent<TopDownCamera>();
            SetRef(follow, "target", target);
            return cam;
        }

        private static void CreateArena(Mats mats, int obstacleLayer)
        {
            var arena = new GameObject("Arena").transform;

            float size = ArenaHalf * 2f;
            GameObject floor = Prim(PrimitiveType.Cube, "Floor", arena, new Vector3(0f, -0.5f, 0f), new Vector3(size + 2f, 1f, size + 2f), mats.floor, null, true);
            floor.layer = 0;

            float wallPos = ArenaHalf + 0.5f;
            float wallLength = size + 2f;
            Wall("Wall_North", arena, new Vector3(0f, 2f, wallPos), new Vector3(wallLength, 4f, 1f), mats.wall, obstacleLayer);
            Wall("Wall_South", arena, new Vector3(0f, 2f, -wallPos), new Vector3(wallLength, 4f, 1f), mats.wall, obstacleLayer);
            Wall("Wall_East", arena, new Vector3(wallPos, 2f, 0f), new Vector3(1f, 4f, wallLength), mats.wall, obstacleLayer);
            Wall("Wall_West", arena, new Vector3(-wallPos, 2f, 0f), new Vector3(1f, 4f, wallLength), mats.wall, obstacleLayer);

            Vector2[] pillars = { new Vector2(-8, -8), new Vector2(8, -8), new Vector2(-8, 8), new Vector2(8, 8), new Vector2(0, 9), new Vector2(0, -9) };
            for (int i = 0; i < pillars.Length; i++)
            {
                GameObject p = Prim(PrimitiveType.Cylinder, "Pillar_" + (i + 1), arena, new Vector3(pillars[i].x, 2f, pillars[i].y), new Vector3(2f, 2f, 2f), mats.pillar, null, true);
                p.layer = obstacleLayer;
            }
        }

        private static void Wall(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, int layer)
        {
            GameObject w = Prim(PrimitiveType.Cube, name, parent, pos, scale, mat, null, true);
            w.layer = layer;
        }

        private static Transform[] CreateSpawnPoints()
        {
            var root = new GameObject("SpawnPoints").transform;
            Vector2[] points = { new Vector2(-12, 12), new Vector2(12, 12), new Vector2(-13, 0), new Vector2(13, 0), new Vector2(-12, -12), new Vector2(12, -12) };
            var result = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var p = new GameObject("SpawnPoint_" + (i + 1)).transform;
                p.SetParent(root, false);
                p.position = new Vector3(points[i].x, 0.05f, points[i].y);
                result[i] = p;
            }
            return result;
        }

        private static GameObject CreatePlayer(Mats mats, int playerLayer, int enemyMask, int obstacleMask)
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(0f, 0.1f, 0f);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.stepOffset = 0.3f;

            ProceduralRig rig = BuildKnight(go.transform, mats);
            var health = go.AddComponent<PlayerHealth>();
            var controller = go.AddComponent<PlayerController>();
            var combat = go.AddComponent<PlayerCombat>();

            SetRef(health, "rig", rig);
            SetRef(controller, "rig", rig);
            SetRef(combat, "rig", rig);
            SetInt(combat, "enemyMask", enemyMask);
            SetInt(combat, "obstacleMask", obstacleMask);

            SetLayerRecursive(go, playerLayer);
            return go;
        }

        // ------------------------------------------------------------------ UI
        private static Font UiFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Place(RectTransform r, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot;
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text NewText(string name, Transform parent, string content, int size, TextAnchor align, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiFont();
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
            var o = rt.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        private static Button NewButton(Transform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Image img = NewImage(name, parent, Color.white);
            Place(img.rectTransform, mid, mid, mid, pos, size);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.22f, 0.22f, 0.28f, 1f);
            cb.highlightedColor = new Color(0.38f, 0.38f, 0.5f, 1f);
            cb.pressedColor = new Color(0.14f, 0.14f, 0.18f, 1f);
            cb.selectedColor = cb.normalColor;
            cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            btn.colors = cb;
            Text t = NewText("Текст", img.rectTransform, label, 34, TextAnchor.MiddleCenter, Color.white);
            Stretch(t.rectTransform, 0f);
            return btn;
        }

        private static Transform CreateCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            ObjectFactory.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(es);
#else
            es.AddComponent<StandaloneInputModule>();
#endif
            return canvasGo.transform;
        }

        private static GameObject NewPanel(Transform canvas, string name, Color tint, string title, Color titleColor, int titleSize, out RectTransform rect)
        {
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Image img = NewImage(name, canvas, tint);
            Stretch(img.rectTransform, 0f);
            rect = img.rectTransform;
            Text t = NewText("Заголовок", rect, title, titleSize, TextAnchor.MiddleCenter, titleColor);
            Place(t.rectTransform, mid, mid, mid, new Vector2(0f, 230f), new Vector2(1400f, 170f));
            img.gameObject.SetActive(false);
            return img.gameObject;
        }

        private static void CreateGameUi(GameManager gameManager, PlayerHealth player, WaveManager waveManager)
        {
            Transform root = CreateCanvas();
            Vector2 topLeft = new Vector2(0, 1), topRight = new Vector2(1, 1), topCenter = new Vector2(0.5f, 1f), mid = new Vector2(0.5f, 0.5f);

            // Полоса здоровья
            Image healthBack = NewImage("ПолосаЗдоровья", root, new Color(0f, 0f, 0f, 0.7f));
            Place(healthBack.rectTransform, topLeft, topLeft, topLeft, new Vector2(30f, -30f), new Vector2(460f, 48f));
            RectTransform fillArea = NewRect("ОбластьЗаливки", healthBack.rectTransform);
            Stretch(fillArea, 5f);
            Image fill = NewImage("Заливка", fillArea, new Color(0.25f, 0.8f, 0.3f, 1f));
            Stretch(fill.rectTransform, 0f);
            Text hpText = NewText("ТекстЗдоровья", healthBack.rectTransform, "HP: 100 / 100", 28, TextAnchor.MiddleCenter, Color.white);
            Stretch(hpText.rectTransform, 0f);

            // Волна и убийства
            Text waveText = NewText("ТекстВолны", root, "Волна: 0 / 3", 44, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f, 1f));
            Place(waveText.rectTransform, topCenter, topCenter, topCenter, new Vector2(0f, -30f), new Vector2(500f, 60f));
            Text killsText = NewText("ТекстУбийств", root, "Убито: 0", 40, TextAnchor.MiddleRight, Color.white);
            Place(killsText.rectTransform, topRight, topRight, topRight, new Vector2(-30f, -35f), new Vector2(400f, 56f));

            // Обратный отсчёт и баннер волны
            Text countdown = NewText("Отсчёт", root, "Начало через: 3", 80, TextAnchor.MiddleCenter, Color.white);
            Place(countdown.rectTransform, mid, mid, mid, new Vector2(0f, 150f), new Vector2(1400f, 130f));
            Text banner = NewText("БаннерВолны", root, "ВОЛНА 1", 96, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.6f, 1f));
            Place(banner.rectTransform, mid, mid, mid, new Vector2(0f, 200f), new Vector2(1400f, 140f));
            banner.gameObject.SetActive(false);

            // Пауза
            RectTransform pauseRect;
            GameObject pausePanel = NewPanel(root, "ПанельПаузы", new Color(0f, 0f, 0f, 0.7f), "ПАУЗА", Color.white, 110, out pauseRect);
            Button resume = NewButton(pauseRect, "КнопкаПродолжить", "Продолжить", new Vector2(0f, 40f), new Vector2(520f, 90f));
            Button pauseMenu = NewButton(pauseRect, "КнопкаВМеню", "В главное меню", new Vector2(0f, -70f), new Vector2(520f, 90f));

            // Победа
            RectTransform victoryRect;
            GameObject victoryPanel = NewPanel(root, "ПанельПобеды", new Color(0f, 0.12f, 0f, 0.82f), "ПОБЕДА", new Color(1f, 0.85f, 0.2f, 1f), 130, out victoryRect);
            Text victoryStats = NewText("Статистика", victoryRect, "Убито монстров: 0", 54, TextAnchor.MiddleCenter, Color.white);
            Place(victoryStats.rectTransform, mid, mid, mid, new Vector2(0f, 100f), new Vector2(1200f, 80f));
            Button victoryRestart = NewButton(victoryRect, "КнопкаСнова", "Играть снова", new Vector2(0f, -20f), new Vector2(520f, 90f));
            Button victoryMenu = NewButton(victoryRect, "КнопкаВМеню", "В главное меню", new Vector2(0f, -130f), new Vector2(520f, 90f));

            // Поражение
            RectTransform defeatRect;
            GameObject defeatPanel = NewPanel(root, "ПанельПоражения", new Color(0.16f, 0f, 0f, 0.82f), "ПОРАЖЕНИЕ", new Color(0.95f, 0.25f, 0.25f, 1f), 130, out defeatRect);
            Button defeatRestart = NewButton(defeatRect, "КнопкаСнова", "Попробовать снова", new Vector2(0f, 40f), new Vector2(560f, 90f));
            Button defeatMenu = NewButton(defeatRect, "КнопкаВМеню", "В главное меню", new Vector2(0f, -70f), new Vector2(560f, 90f));

            var ui = root.gameObject.AddComponent<UIManager>();
            SetRef(ui, "gameManager", gameManager);
            SetRef(ui, "player", player);
            SetRef(ui, "waveManager", waveManager);
            SetRef(ui, "healthFill", fill.rectTransform);
            SetRef(ui, "healthFillImage", fill);
            SetRef(ui, "healthText", hpText);
            SetRef(ui, "waveText", waveText);
            SetRef(ui, "killsText", killsText);
            SetRef(ui, "countdownText", countdown);
            SetRef(ui, "waveBanner", banner);
            SetRef(ui, "pausePanel", pausePanel);
            SetRef(ui, "victoryPanel", victoryPanel);
            SetRef(ui, "defeatPanel", defeatPanel);
            SetRef(ui, "victoryStatsText", victoryStats);
            SetRef(ui, "resumeButton", resume);
            SetRef(ui, "pauseMenuButton", pauseMenu);
            SetRef(ui, "victoryRestartButton", victoryRestart);
            SetRef(ui, "victoryMenuButton", victoryMenu);
            SetRef(ui, "defeatRestartButton", defeatRestart);
            SetRef(ui, "defeatMenuButton", defeatMenu);
        }

        // ------------------------------------------------------------------ главное меню
        private static void BuildMenuScene(Mats mats)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.09f);
            camGo.AddComponent<AudioListener>();

            new GameObject("AudioManager").AddComponent<AudioManager>();

            Transform root = CreateCanvas();
            Vector2 mid = new Vector2(0.5f, 0.5f);

            Text title = NewText("Название", root, "Arena Survival", 150, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f, 1f));
            Place(title.rectTransform, mid, mid, mid, new Vector2(0f, 230f), new Vector2(1600f, 200f));
            Button play = NewButton(root, "КнопкаИграть", "Играть", new Vector2(0f, 0f), new Vector2(480f, 100f));
            Button quit = NewButton(root, "КнопкаВыход", "Выход", new Vector2(0f, -130f), new Vector2(480f, 100f));
            Text hint = NewText("Подсказка", root, "WASD — движение     Shift — бег     ЛКМ — удар мечом     ESC — пауза", 32, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f, 1f));
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1800f, 50f));

            var menu = root.gameObject.AddComponent<MainMenu>();
            SetRef(menu, "playButton", play);
            SetRef(menu, "quitButton", quit);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            SetBuildScenes(MenuScenePath, GameScenePath);
        }
    }
}
