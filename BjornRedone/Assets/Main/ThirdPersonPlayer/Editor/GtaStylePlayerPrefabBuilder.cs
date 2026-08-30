using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Bjorn.ThirdPerson.Editor
{
    public static class GtaStylePlayerPrefabBuilder
    {
        public const string FeatureRoot = "Assets/Main/ThirdPersonPlayer";
        public const string PrefabPath = FeatureRoot + "/Prefabs/GTA Style Player.prefab";
        private const string MaterialsPath = FeatureRoot + "/Materials";

        [MenuItem("Tools/Bjorn/Third Person Player/Rebuild GTA Style Player Prefab")]
        public static void RebuildPrefab()
        {
            EnsureFolders();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject root = BuildPlayerHierarchy(materials);

            try
            {
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException("Unity did not return a saved prefab asset.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidatePrefabOrThrow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log($"[GTA Style Player] Rebuilt and validated {PrefabPath}");
        }

        [MenuItem("Tools/Bjorn/Third Person Player/Validate GTA Style Player Prefab")]
        public static void ValidatePrefabMenu()
        {
            ValidatePrefabOrThrow();
            Debug.Log($"[GTA Style Player] Validation passed for {PrefabPath}");
        }

        public static void BuildForCommandLine()
        {
            RebuildPrefab();
            Debug.Log("[GTA Style Player] COMMAND_LINE_BUILD_PASSED");
        }

        public static void ValidateForCommandLine()
        {
            ValidatePrefabOrThrow();
            Debug.Log("[GTA Style Player] COMMAND_LINE_VALIDATION_PASSED");
        }

        public static void ValidatePrefabOrThrow()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Missing prefab: {PrefabPath}");
            }

            List<string> errors = new List<string>();
            CharacterController controller = prefab.GetComponent<CharacterController>();
            GtaStylePlayerInput input = prefab.GetComponent<GtaStylePlayerInput>();
            GtaStylePlayerMotor motor = prefab.GetComponent<GtaStylePlayerMotor>();
            GtaStyleProceduralAnimator proceduralAnimator = prefab.GetComponent<GtaStyleProceduralAnimator>();
            GtaStylePlayerInteractor interactor = prefab.GetComponent<GtaStylePlayerInteractor>();
            GtaStyleThirdPersonCamera cameraController = prefab.GetComponentInChildren<GtaStyleThirdPersonCamera>(true);
            Camera playerCamera = prefab.GetComponentInChildren<Camera>(true);

            Require(controller != null, "Root is missing CharacterController.", errors);
            Require(input != null, "Root is missing GtaStylePlayerInput.", errors);
            Require(motor != null, "Root is missing GtaStylePlayerMotor.", errors);
            Require(proceduralAnimator != null, "Root is missing GtaStyleProceduralAnimator.", errors);
            Require(interactor != null, "Root is missing GtaStylePlayerInteractor.", errors);
            Require(cameraController != null, "Prefab is missing GtaStyleThirdPersonCamera.", errors);
            Require(playerCamera != null, "Prefab is missing a Camera.", errors);
            Require(playerCamera != null && playerCamera.CompareTag("MainCamera"), "Player camera must use the MainCamera tag.", errors);
            Require(playerCamera != null && playerCamera.GetComponent<AudioListener>() != null, "Player camera is missing AudioListener.", errors);
            Require(prefab.GetComponentInChildren<Animator>(true) == null, "Prefab must remain code-animated and should not contain Animator.", errors);
            Require(prefab.GetComponentsInChildren<MeshRenderer>(true).Length >= 16, "Primitive character hierarchy is incomplete.", errors);
            Require(motor != null && motor.PlayerInput == input, "Motor input reference is not wired.", errors);
            Require(motor != null && playerCamera != null && motor.MovementCamera == playerCamera.transform, "Motor camera reference is not wired.", errors);
            Require(cameraController != null && cameraController.Target == prefab.transform, "Camera target reference is not wired.", errors);
            Require(proceduralAnimator != null && proceduralAnimator.Motor == motor, "Procedural animator motor reference is not wired.", errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("GTA Style Player prefab validation failed:\n- " + string.Join("\n- ", errors));
            }
        }

        private static GameObject BuildPlayerHierarchy(IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new GameObject("GTA Style Player");
            root.tag = "Player";

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1.06f, 0f);
            controller.height = 2.12f;
            controller.radius = 0.38f;
            controller.slopeLimit = 48f;
            controller.stepOffset = 0.32f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;

            GtaStylePlayerInput input = root.AddComponent<GtaStylePlayerInput>();
            GtaStylePlayerMotor motor = root.AddComponent<GtaStylePlayerMotor>();
            GtaStyleProceduralAnimator proceduralAnimator = root.AddComponent<GtaStyleProceduralAnimator>();
            GtaStylePlayerInteractor interactor = root.AddComponent<GtaStylePlayerInteractor>();

            Transform visuals = CreateEmpty("Code Animated Visuals", root.transform, Vector3.zero);
            Transform pelvis = CreatePrimitive("Pelvis", PrimitiveType.Cube, visuals, new Vector3(0f, 0.88f, 0f),
                new Vector3(0.54f, 0.24f, 0.34f), materials["Jeans"]);
            Transform torso = CreatePrimitive("Torso", PrimitiveType.Cube, visuals, new Vector3(0f, 1.28f, 0f),
                new Vector3(0.68f, 0.72f, 0.38f), materials["Jacket"]);
            CreatePrimitive("Shirt", PrimitiveType.Cube, torso, new Vector3(0f, 0f, 0.51f),
                new Vector3(0.34f, 0.8f, 0.035f), materials["Shirt"]);
            CreatePrimitive("Neck", PrimitiveType.Cylinder, visuals, new Vector3(0f, 1.63f, 0f),
                new Vector3(0.15f, 0.08f, 0.15f), materials["Skin"]);
            Transform head = CreatePrimitive("Head", PrimitiveType.Sphere, visuals, new Vector3(0f, 1.87f, 0f),
                new Vector3(0.52f, 0.56f, 0.52f), materials["Skin"]);
            CreatePrimitive("Hair", PrimitiveType.Sphere, head, new Vector3(0f, 0.36f, -0.03f),
                new Vector3(1.03f, 0.35f, 1.03f), materials["Hair"]);
            CreatePrimitive("Left Eye", PrimitiveType.Sphere, head, new Vector3(-0.21f, 0.08f, 0.46f),
                new Vector3(0.22f, 0.22f, 0.12f), materials["Eye"]);
            CreatePrimitive("Right Eye", PrimitiveType.Sphere, head, new Vector3(0.21f, 0.08f, 0.46f),
                new Vector3(0.22f, 0.22f, 0.12f), materials["Eye"]);
            CreatePrimitive("Left Pupil", PrimitiveType.Sphere, head, new Vector3(-0.21f, 0.08f, 0.525f),
                new Vector3(0.09f, 0.11f, 0.055f), materials["Dark"]);
            CreatePrimitive("Right Pupil", PrimitiveType.Sphere, head, new Vector3(0.21f, 0.08f, 0.525f),
                new Vector3(0.09f, 0.11f, 0.055f), materials["Dark"]);
            CreatePrimitive("Nose", PrimitiveType.Cube, head, new Vector3(0f, -0.09f, 0.52f),
                new Vector3(0.16f, 0.11f, 0.14f), materials["SkinShade"]);

            Transform leftArm = CreateEmpty("Left Arm Pivot", visuals, new Vector3(-0.43f, 1.48f, 0f));
            Transform rightArm = CreateEmpty("Right Arm Pivot", visuals, new Vector3(0.43f, 1.48f, 0f));
            CreatePrimitive("Left Sleeve", PrimitiveType.Capsule, leftArm, new Vector3(0f, -0.29f, 0f),
                new Vector3(0.17f, 0.3f, 0.17f), materials["Jacket"]);
            CreatePrimitive("Right Sleeve", PrimitiveType.Capsule, rightArm, new Vector3(0f, -0.29f, 0f),
                new Vector3(0.17f, 0.3f, 0.17f), materials["Jacket"]);
            CreatePrimitive("Left Hand", PrimitiveType.Sphere, leftArm, new Vector3(0f, -0.59f, 0f),
                new Vector3(0.18f, 0.18f, 0.18f), materials["Skin"]);
            CreatePrimitive("Right Hand", PrimitiveType.Sphere, rightArm, new Vector3(0f, -0.59f, 0f),
                new Vector3(0.18f, 0.18f, 0.18f), materials["Skin"]);

            Transform leftLeg = CreateEmpty("Left Leg Pivot", visuals, new Vector3(-0.18f, 0.82f, 0f));
            Transform rightLeg = CreateEmpty("Right Leg Pivot", visuals, new Vector3(0.18f, 0.82f, 0f));
            CreatePrimitive("Left Leg", PrimitiveType.Capsule, leftLeg, new Vector3(0f, -0.31f, 0f),
                new Vector3(0.21f, 0.33f, 0.21f), materials["Jeans"]);
            CreatePrimitive("Right Leg", PrimitiveType.Capsule, rightLeg, new Vector3(0f, -0.31f, 0f),
                new Vector3(0.21f, 0.33f, 0.21f), materials["Jeans"]);
            Transform leftFoot = CreatePrimitive("Left Shoe", PrimitiveType.Cube, leftLeg, new Vector3(0f, -0.65f, 0.09f),
                new Vector3(0.27f, 0.16f, 0.43f), materials["Dark"]);
            Transform rightFoot = CreatePrimitive("Right Shoe", PrimitiveType.Cube, rightLeg, new Vector3(0f, -0.65f, 0.09f),
                new Vector3(0.27f, 0.16f, 0.43f), materials["Dark"]);

            GameObject cameraObject = new GameObject("GTA Third Person Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0.5f, 2.25f, -4.6f);
            cameraObject.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            Camera playerCamera = cameraObject.GetComponent<Camera>();
            playerCamera.fieldOfView = 65f;
            playerCamera.nearClipPlane = 0.08f;
            playerCamera.farClipPlane = 1000f;
            playerCamera.allowHDR = true;
            playerCamera.allowMSAA = true;
            GtaStyleThirdPersonCamera cameraController = cameraObject.AddComponent<GtaStyleThirdPersonCamera>();

            CreateInteractionHud(root.transform, out GameObject promptPanel, out Text promptText);

            motor.Configure(input, cameraObject.transform);
            cameraController.Configure(root.transform, input, motor);
            proceduralAnimator.Configure(motor, visuals, pelvis, torso, head, leftArm, rightArm, leftLeg, rightLeg, leftFoot, rightFoot);
            interactor.Configure(playerCamera, promptPanel, promptText);
            return root;
        }

        private static void CreateInteractionHud(Transform playerRoot, out GameObject promptPanel, out Text promptText)
        {
            GameObject canvasObject = new GameObject("Interaction HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(playerRoot, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            promptPanel = new GameObject("Interaction Prompt", typeof(RectTransform), typeof(Image));
            promptPanel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = promptPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = new Vector2(440f, 52f);
            panelRect.anchoredPosition = new Vector2(0f, 72f);
            promptPanel.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.045f, 0.9f);

            GameObject textObject = new GameObject("Prompt Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(promptPanel.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 4f);
            textRect.offsetMax = new Vector2(-14f, -4f);
            promptText = textObject.GetComponent<Text>();
            promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptText.fontSize = 23;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color = new Color(0.92f, 0.95f, 0.98f);
            promptText.text = "[E]  Interact";
            promptText.raycastTarget = false;
            promptPanel.SetActive(false);
        }

        private static Transform CreateEmpty(string name, Transform parent, Vector3 localPosition)
        {
            GameObject gameObject = new GameObject(name);
            Transform child = gameObject.transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private static Transform CreatePrimitive(string name, PrimitiveType primitiveType, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = name;
            Transform child = gameObject.transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = localScale;

            Collider primitiveCollider = gameObject.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(primitiveCollider);
            }

            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return child;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            return new Dictionary<string, Material>
            {
                ["Jacket"] = CreateOrUpdateMaterial("Jacket Blue", new Color(0.075f, 0.21f, 0.31f), 0.05f, 0.28f),
                ["Shirt"] = CreateOrUpdateMaterial("Shirt Red", new Color(0.58f, 0.12f, 0.1f), 0f, 0.18f),
                ["Jeans"] = CreateOrUpdateMaterial("Faded Jeans", new Color(0.09f, 0.13f, 0.2f), 0f, 0.2f),
                ["Skin"] = CreateOrUpdateMaterial("Skin", new Color(0.72f, 0.42f, 0.25f), 0f, 0.32f),
                ["SkinShade"] = CreateOrUpdateMaterial("Skin Shade", new Color(0.48f, 0.22f, 0.13f), 0f, 0.28f),
                ["Hair"] = CreateOrUpdateMaterial("Hair", new Color(0.075f, 0.045f, 0.03f), 0f, 0.3f),
                ["Eye"] = CreateOrUpdateMaterial("Eye White", new Color(0.92f, 0.94f, 0.9f), 0f, 0.22f),
                ["Dark"] = CreateOrUpdateMaterial("Shoes and Pupils", new Color(0.025f, 0.03f, 0.035f), 0.15f, 0.42f)
            };
        }

        private static Material CreateOrUpdateMaterial(string name, Color color, float metallic, float smoothness)
        {
            string assetPath = $"{MaterialsPath}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("Could not find a supported 3D shader.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            material.color = color;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(FeatureRoot, "Prefabs");
            EnsureFolder(FeatureRoot, "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void Require(bool condition, string message, ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(message);
            }
        }
    }
}
