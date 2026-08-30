using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Bjorn.ThirdPerson.Editor
{
    public static class ThirdPersonFeatureBuilder
    {
        public const string DemoScenePath = GtaStylePlayerPrefabBuilder.FeatureRoot + "/Scenes/Third Person Playground.unity";
        public const string CatalogPath = GtaStylePlayerPrefabBuilder.FeatureRoot + "/Resources/ThirdPersonFeatureCatalog.asset";

        [MenuItem("Tools/Bjorn/Third Person Player/Rebuild Complete Player + Microphone Demo")]
        public static void RebuildCompleteFeature()
        {
            GtaStylePlayerPrefabBuilder.RebuildPrefab();
            MicrophoneSystemPrefabBuilder.RebuildMicrophoneSystem();
            BuildRuntimeCatalog();
            BuildDemoScene();
            Debug.Log("[Third Person Feature] Complete player, interaction, microphone, and demo rebuild passed.");
        }

        public static void BuildForCommandLine()
        {
            RebuildCompleteFeature();
            Debug.Log("[Third Person Feature] COMMAND_LINE_COMPLETE_BUILD_PASSED");
        }

        [MenuItem("Tools/Bjorn/Third Person Player/Open Third Person Playground")]
        public static void OpenDemoScene()
        {
            if (!System.IO.File.Exists(DemoScenePath))
            {
                BuildDemoScene();
            }
            EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        }

        private static void BuildDemoScene()
        {
            EnsureSceneFolder();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material groundMaterial = CreateOrUpdateMaterial("Demo Ground", new Color(0.16f, 0.2f, 0.18f));
            Material wallMaterial = CreateOrUpdateMaterial("Demo Concrete", new Color(0.28f, 0.3f, 0.31f));

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "3D Ground";
            ground.transform.position = new Vector3(0f, -0.3f, 5f);
            ground.transform.localScale = new Vector3(28f, 0.6f, 28f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            CreateObstacle("Camera Collision Wall", new Vector3(-5f, 1.5f, 5f), new Vector3(0.6f, 3f, 8f), wallMaterial);
            CreateObstacle("Low Vault Block", new Vector3(3.8f, 0.5f, 2.5f), new Vector3(3f, 1f, 1.2f), wallMaterial);
            CreateObstacle("Back Wall", new Vector3(0f, 2f, 12f), new Vector3(14f, 4f, 0.6f), wallMaterial);

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GtaStylePlayerPrefabBuilder.PrefabPath);
            if (playerPrefab == null)
            {
                throw new InvalidOperationException("Build the player prefab before creating the demo scene.");
            }
            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab, scene) as GameObject;
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            PlaceMicrophone(MicrophoneSystemPrefabBuilder.StudioPrefabPath, new Vector3(-4.5f, 0f, 5.5f));
            PlaceMicrophone(MicrophoneSystemPrefabBuilder.StandardPrefabPath, new Vector3(-1.5f, 0f, 5.5f));
            PlaceMicrophone(MicrophoneSystemPrefabBuilder.CheapPrefabPath, new Vector3(1.5f, 0f, 5.5f));
            PlaceMicrophone(MicrophoneSystemPrefabBuilder.BrokenPrefabPath, new Vector3(4.5f, 0f, 5.5f));

            GameObject sunObject = new GameObject("Sun", typeof(Light));
            Light sun = sunObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            CreateInstructions();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, DemoScenePath))
            {
                throw new InvalidOperationException($"Unity failed to save {DemoScenePath}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Third Person Feature] Demo scene saved: {DemoScenePath}");
        }

        private static void BuildRuntimeCatalog()
        {
            string resourcesPath = GtaStylePlayerPrefabBuilder.FeatureRoot + "/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder(GtaStylePlayerPrefabBuilder.FeatureRoot, "Resources");
            }

            ThirdPersonFeatureCatalog catalog = AssetDatabase.LoadAssetAtPath<ThirdPersonFeatureCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ThirdPersonFeatureCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(GtaStylePlayerPrefabBuilder.PrefabPath);
            MicrophoneRecorderWindow ui = AssetDatabase.LoadAssetAtPath<GameObject>(MicrophoneSystemPrefabBuilder.UiPrefabPath)
                .GetComponent<MicrophoneRecorderWindow>();
            GameObject studio = AssetDatabase.LoadAssetAtPath<GameObject>(MicrophoneSystemPrefabBuilder.StudioPrefabPath);
            GameObject standard = AssetDatabase.LoadAssetAtPath<GameObject>(MicrophoneSystemPrefabBuilder.StandardPrefabPath);
            GameObject cheap = AssetDatabase.LoadAssetAtPath<GameObject>(MicrophoneSystemPrefabBuilder.CheapPrefabPath);
            GameObject broken = AssetDatabase.LoadAssetAtPath<GameObject>(MicrophoneSystemPrefabBuilder.BrokenPrefabPath);
            catalog.Configure(player, ui, studio, standard, cheap, broken);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static void PlaceMicrophone(string prefabPath, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Missing microphone prefab: {prefabPath}");
            }
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
        }

        private static void CreateObstacle(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = name;
            obstacle.transform.position = position;
            obstacle.transform.localScale = scale;
            obstacle.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateInstructions()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject canvasObject = new GameObject("Demo Instructions", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject panelObject = new GameObject("Controls", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(24f, -24f);
            panel.sizeDelta = new Vector2(560f, 112f);
            panelObject.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.045f, 0.88f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panelObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 10f);
            textRect.offsetMax = new Vector2(-18f, -10f);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.9f, 0.94f, 0.96f);
            text.text = "WASD  MOVE     SHIFT  SPRINT     SPACE  JUMP\nMOUSE  CAMERA     Q  SWAP SHOULDER     E  USE MICROPHONE";
            text.raycastTarget = false;
        }

        private static Material CreateOrUpdateMaterial(string name, Color color)
        {
            string path = $"{GtaStylePlayerPrefabBuilder.FeatureRoot}/Materials/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("No compatible shader is available for the demo scene.");
            }
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureSceneFolder()
        {
            string path = GtaStylePlayerPrefabBuilder.FeatureRoot + "/Scenes";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(GtaStylePlayerPrefabBuilder.FeatureRoot, "Scenes");
            }
        }
    }
}
