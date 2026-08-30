using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Bjorn.ThirdPerson.Editor
{
    public static class MicrophoneSystemPrefabBuilder
    {
        public const string UiPrefabPath = GtaStylePlayerPrefabBuilder.FeatureRoot + "/Prefabs/Interaction/Microphone Recorder UI.prefab";
        public const string StandardPrefabPath = GtaStylePlayerPrefabBuilder.FeatureRoot + "/Prefabs/Interaction/Recordable Microphone.prefab";
        public const string StudioPrefabPath = GtaStylePlayerPrefabBuilder.FeatureRoot + "/Prefabs/Interaction/Studio Microphone.prefab";
        public const string CheapPrefabPath = GtaStylePlayerPrefabBuilder.FeatureRoot + "/Prefabs/Interaction/Cheap Microphone.prefab";
        public const string BrokenPrefabPath = GtaStylePlayerPrefabBuilder.FeatureRoot + "/Prefabs/Interaction/Broken Microphone.prefab";
        private const string MaterialsPath = GtaStylePlayerPrefabBuilder.FeatureRoot + "/Materials";

        [MenuItem("Tools/Bjorn/Third Person Player/Rebuild Microphone Recording System")]
        public static void RebuildMicrophoneSystem()
        {
            EnsureFolders();
            MicrophoneRecorderWindow windowPrefab = BuildRecorderUi();
            Dictionary<string, Material> materials = CreateMaterials();

            BuildMicrophonePrefab(StandardPrefabPath, "Recordable Microphone", MicrophoneQualityTier.Standard, windowPrefab, materials);
            BuildMicrophonePrefab(StudioPrefabPath, "Studio Microphone", MicrophoneQualityTier.Studio, windowPrefab, materials);
            BuildMicrophonePrefab(CheapPrefabPath, "Cheap Microphone", MicrophoneQualityTier.Cheap, windowPrefab, materials);
            BuildMicrophonePrefab(BrokenPrefabPath, "Broken Microphone", MicrophoneQualityTier.Broken, windowPrefab, materials);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateOrThrow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(StandardPrefabPath);
            Debug.Log("[Microphone Recorder] Rebuilt UI and all four quality prefabs.");
        }

        public static void BuildForCommandLine()
        {
            RebuildMicrophoneSystem();
            Debug.Log("[Microphone Recorder] COMMAND_LINE_BUILD_PASSED");
        }

        [MenuItem("Tools/Bjorn/Third Person Player/Validate Microphone Recording System")]
        public static void ValidateMenu()
        {
            ValidateOrThrow();
            Debug.Log("[Microphone Recorder] Validation passed.");
        }

        public static void ValidateOrThrow()
        {
            GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath);
            if (uiPrefab == null || uiPrefab.GetComponent<MicrophoneRecorderWindow>() == null
                || uiPrefab.GetComponent<Canvas>() == null || uiPrefab.GetComponentInChildren<WaveformGraphic>(true) == null)
            {
                throw new InvalidOperationException($"Recorder UI prefab is missing or incomplete: {UiPrefabPath}");
            }

            ValidateMicrophone(StandardPrefabPath, MicrophoneQualityTier.Standard);
            ValidateMicrophone(StudioPrefabPath, MicrophoneQualityTier.Studio);
            ValidateMicrophone(CheapPrefabPath, MicrophoneQualityTier.Cheap);
            ValidateMicrophone(BrokenPrefabPath, MicrophoneQualityTier.Broken);
        }

        private static void ValidateMicrophone(string path, MicrophoneQualityTier expectedQuality)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            RecordableMicrophone microphone = prefab != null ? prefab.GetComponent<RecordableMicrophone>() : null;
            if (prefab == null || microphone == null || microphone.Quality != expectedQuality
                || microphone.RecorderWindowPrefab == null || prefab.GetComponent<Collider>() == null
                || prefab.GetComponent<AudioSource>() == null || prefab.GetComponentsInChildren<MeshRenderer>(true).Length < 5)
            {
                throw new InvalidOperationException($"Microphone prefab is missing or incomplete: {path}");
            }
        }

        private static MicrophoneRecorderWindow BuildRecorderUi()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject root = new GameObject("Microphone Recorder UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MicrophoneRecorderWindow));

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                Image dimmer = CreateImage("Dimmer", root.transform, new Color(0.01f, 0.015f, 0.02f, 0.78f));
                Stretch(dimmer.rectTransform, Vector2.zero, Vector2.zero);

                Image window = CreateImage("Recorder Window", root.transform, new Color(0.075f, 0.085f, 0.095f, 0.99f));
                SetCenteredRect(window.rectTransform, Vector2.zero, new Vector2(940f, 560f));

                Image titleBar = CreateImage("Title Bar", window.transform, new Color(0.14f, 0.17f, 0.19f, 1f));
                SetCenteredRect(titleBar.rectTransform, new Vector2(0f, 256f), new Vector2(940f, 48f));
                Text title = CreateText("Title", titleBar.transform, font, "MICROPHONE RECORDER", 24, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Color(0.91f, 0.94f, 0.95f));
                Stretch(title.rectTransform, new Vector2(22f, 0f), new Vector2(-22f, 0f));

                Image toolbar = CreateImage("Transport Toolbar", window.transform, new Color(0.105f, 0.12f, 0.13f, 1f));
                SetCenteredRect(toolbar.rectTransform, new Vector2(0f, 191f), new Vector2(880f, 58f));
                Button record = CreateButton("Record", toolbar.transform, font, "●  RECORD", new Vector2(-355f, 0f),
                    new Vector2(120f, 38f), new Color(0.63f, 0.12f, 0.12f));
                Button stop = CreateButton("Stop", toolbar.transform, font, "■  STOP", new Vector2(-215f, 0f),
                    new Vector2(120f, 38f), new Color(0.23f, 0.27f, 0.29f));
                Button play = CreateButton("Play", toolbar.transform, font, "▶  PLAY", new Vector2(-75f, 0f),
                    new Vector2(120f, 38f), new Color(0.12f, 0.42f, 0.25f));
                Button save = CreateButton("Save WAV", toolbar.transform, font, "SAVE WAV", new Vector2(75f, 0f),
                    new Vector2(140f, 38f), new Color(0.14f, 0.32f, 0.47f));
                Button nextDevice = CreateButton("Next Device", toolbar.transform, font, "NEXT INPUT", new Vector2(235f, 0f),
                    new Vector2(140f, 38f), new Color(0.25f, 0.28f, 0.31f));
                Button close = CreateButton("Close", toolbar.transform, font, "CLOSE", new Vector2(365f, 0f),
                    new Vector2(92f, 38f), new Color(0.31f, 0.22f, 0.22f));

                Text device = CreateText("Input Device", window.transform, font, "INPUT: NO DEVICE", 17, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Color(0.72f, 0.78f, 0.8f));
                SetCenteredRect(device.rectTransform, new Vector2(-80f, 145f), new Vector2(700f, 30f));
                Text timer = CreateText("Timer", window.transform, font, "00:00.0", 25, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Color(0.55f, 0.92f, 0.67f));
                SetCenteredRect(timer.rectTransform, new Vector2(362f, 145f), new Vector2(150f, 34f));

                Image waveformBackground = CreateImage("Waveform Background", window.transform, new Color(0.025f, 0.035f, 0.04f, 1f));
                SetCenteredRect(waveformBackground.rectTransform, new Vector2(0f, 0f), new Vector2(860f, 252f));
                GameObject waveformObject = new GameObject("Waveform", typeof(RectTransform), typeof(WaveformGraphic));
                waveformObject.transform.SetParent(waveformBackground.transform, false);
                WaveformGraphic waveform = waveformObject.GetComponent<WaveformGraphic>();
                waveform.color = new Color(0.34f, 0.88f, 0.48f, 1f);
                waveform.raycastTarget = false;
                Stretch(waveform.rectTransform, new Vector2(12f, 12f), new Vector2(-12f, -12f));

                Image meterBackground = CreateImage("Level Meter", window.transform, new Color(0.03f, 0.04f, 0.045f, 1f));
                SetCenteredRect(meterBackground.rectTransform, new Vector2(0f, -145f), new Vector2(860f, 14f));
                Image meterFill = CreateImage("Fill", meterBackground.transform, new Color(0.33f, 0.88f, 0.44f, 1f));
                Stretch(meterFill.rectTransform, Vector2.zero, Vector2.zero);
                meterFill.type = Image.Type.Filled;
                meterFill.fillMethod = Image.FillMethod.Horizontal;
                meterFill.fillOrigin = 0;
                meterFill.fillAmount = 0f;

                Text effects = CreateText("Quality Effects", window.transform, font,
                    "QUALITY EFFECTS", 17, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(0.71f, 0.75f, 0.77f));
                SetCenteredRect(effects.rectTransform, new Vector2(0f, -181f), new Vector2(860f, 34f));
                Text status = CreateText("Status", window.transform, font, "Ready.", 16, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Color(0.57f, 0.9f, 0.66f));
                SetCenteredRect(status.rectTransform, new Vector2(0f, -229f), new Vector2(860f, 52f));

                MicrophoneRecorderWindow controller = root.GetComponent<MicrophoneRecorderWindow>();
                controller.Configure(record, stop, play, save, nextDevice, close, title, device, timer, status, effects,
                    meterFill, waveform);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, UiPrefabPath);
                if (saved == null)
                {
                    throw new InvalidOperationException("Unity failed to save the recorder UI prefab.");
                }
                return saved.GetComponent<MicrophoneRecorderWindow>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildMicrophonePrefab(string path, string microphoneName, MicrophoneQualityTier quality,
            MicrophoneRecorderWindow windowPrefab, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new GameObject(microphoneName, typeof(BoxCollider), typeof(AudioSource), typeof(RecordableMicrophone));
            try
            {
                BoxCollider interactionCollider = root.GetComponent<BoxCollider>();
                interactionCollider.center = new Vector3(0f, 0.92f, 0.08f);
                interactionCollider.size = new Vector3(1.45f, 2.05f, 1.65f);
                interactionCollider.isTrigger = false;

                AudioSource source = root.GetComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;

                Transform visualRoot = CreateEmpty("Microphone Visuals", root.transform, Vector3.zero);
                Material bodyMaterial = materials[quality.ToString()];
                CreatePrimitive("Weighted Base", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 0.1f, 0f),
                    new Vector3(0.72f, 0.09f, 0.72f), materials["Dark"]);
                CreatePrimitive("Stand", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 0.82f, 0f),
                    new Vector3(0.09f, 0.65f, 0.09f), materials["Metal"]);
                CreatePrimitive("Shock Mount", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 1.45f, 0f),
                    new Vector3(0.24f, 0.1f, 0.24f), materials["Metal"]);
                Transform body = CreatePrimitive("Microphone Body", PrimitiveType.Capsule, visualRoot,
                    new Vector3(0f, 1.68f, 0.12f), new Vector3(0.28f, 0.52f, 0.28f), bodyMaterial,
                    Quaternion.Euler(90f, 0f, 0f));
                CreatePrimitive("Grille", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 1.68f, 0.62f),
                    new Vector3(0.34f, 0.34f, 0.3f), materials["Grille"]);
                CreatePrimitive("Quality Band", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 1.68f, 0.39f),
                    new Vector3(0.3f, 0.055f, 0.3f), bodyMaterial, Quaternion.Euler(90f, 0f, 0f));

                if (quality == MicrophoneQualityTier.Studio)
                {
                    CreatePrimitive("Pop Filter", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 1.68f, 0.96f),
                        new Vector3(0.43f, 0.035f, 0.43f), materials["PopFilter"], Quaternion.Euler(90f, 0f, 0f));
                    CreatePrimitive("Pop Filter Arm", PrimitiveType.Cylinder, visualRoot, new Vector3(0.36f, 1.24f, 0.55f),
                        new Vector3(0.025f, 0.5f, 0.025f), materials["Metal"], Quaternion.Euler(-22f, 0f, 0f));
                }
                else if (quality == MicrophoneQualityTier.Cheap)
                {
                    CreatePrimitive("Plastic Switch", PrimitiveType.Cube, body, new Vector3(0f, -0.38f, -0.7f),
                        new Vector3(0.26f, 0.12f, 0.08f), materials["CheapAccent"]);
                }
                else if (quality == MicrophoneQualityTier.Broken)
                {
                    visualRoot.localRotation = Quaternion.Euler(0f, 0f, 8f);
                    CreatePrimitive("Repair Tape", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.55f, 0.12f),
                        new Vector3(0.45f, 0.2f, 0.38f), materials["Tape"], Quaternion.Euler(0f, 0f, -12f));
                    CreatePrimitive("Loose Wire", PrimitiveType.Cylinder, visualRoot, new Vector3(0.28f, 0.43f, 0.04f),
                        new Vector3(0.025f, 0.34f, 0.025f), materials["Broken"], Quaternion.Euler(0f, 0f, 24f));
                }

                root.GetComponent<RecordableMicrophone>().Configure(microphoneName, quality, 120, windowPrefab);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null)
                {
                    throw new InvalidOperationException($"Unity failed to save {path}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            return new Dictionary<string, Material>
            {
                ["Studio"] = CreateOrUpdateMaterial("Mic Studio", new Color(0.12f, 0.33f, 0.38f)),
                ["Standard"] = CreateOrUpdateMaterial("Mic Standard", new Color(0.24f, 0.28f, 0.31f)),
                ["Cheap"] = CreateOrUpdateMaterial("Mic Cheap", new Color(0.62f, 0.52f, 0.16f)),
                ["Broken"] = CreateOrUpdateMaterial("Mic Broken", new Color(0.42f, 0.08f, 0.075f)),
                ["Dark"] = CreateOrUpdateMaterial("Mic Base", new Color(0.025f, 0.03f, 0.035f)),
                ["Metal"] = CreateOrUpdateMaterial("Mic Metal", new Color(0.18f, 0.2f, 0.21f)),
                ["Grille"] = CreateOrUpdateMaterial("Mic Grille", new Color(0.08f, 0.09f, 0.095f)),
                ["PopFilter"] = CreateOrUpdateMaterial("Mic Pop Filter", new Color(0.015f, 0.02f, 0.022f)),
                ["CheapAccent"] = CreateOrUpdateMaterial("Mic Cheap Switch", new Color(0.12f, 0.1f, 0.035f)),
                ["Tape"] = CreateOrUpdateMaterial("Mic Repair Tape", new Color(0.42f, 0.38f, 0.2f))
            };
        }

        private static Material CreateOrUpdateMaterial(string name, Color color)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("No supported URP or standard shader is available.");
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
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform CreateEmpty(string name, Transform parent, Vector3 localPosition)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            return gameObject.transform;
        }

        private static Transform CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, Quaternion? localRotation = null)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localRotation = localRotation ?? Quaternion.identity;
            gameObject.transform.localScale = localScale;
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return gameObject.transform;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Font font, string value, int size,
            FontStyle style, TextAnchor alignment, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, Vector2 position,
            Vector2 size, Color color)
        {
            Image background = CreateImage(name, parent, color);
            SetCenteredRect(background.rectTransform, position, size);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.65f);
            button.colors = colors;

            Text text = CreateText("Label", button.transform, font, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            return button;
        }

        private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(GtaStylePlayerPrefabBuilder.FeatureRoot, "Prefabs");
            EnsureFolder(GtaStylePlayerPrefabBuilder.FeatureRoot + "/Prefabs", "Interaction");
            EnsureFolder(GtaStylePlayerPrefabBuilder.FeatureRoot, "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
