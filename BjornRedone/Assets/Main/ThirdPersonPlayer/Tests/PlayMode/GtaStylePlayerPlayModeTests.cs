using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Bjorn.ThirdPerson.Tests
{
    public sealed class GtaStylePlayerPlayModeTests
    {
        private const string PrefabPath = "Assets/Main/ThirdPersonPlayer/Prefabs/GTA Style Player.prefab";
        private float previousTimeScale;
        private float previousCaptureDeltaTime;

        [UnitySetUp]
        public IEnumerator SetUpDeterministicFrames()
        {
            previousTimeScale = Time.timeScale;
            previousCaptureDeltaTime = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 60f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreFrameTiming()
        {
            MicrophoneRecorderWindow.CloseActive();
            Time.captureDeltaTime = previousCaptureDeltaTime;
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrefabMovesJumpsOrbitsAndAnimatesWithoutAnimator()
        {
            Vector3 testOrigin = new Vector3(1000f, 0f, 1000f);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Third Person Smoke Ground";
            ground.transform.position = testOrigin + new Vector3(0f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(30f, 0.5f, 30f);
            ApplyPreviewMaterial(ground, new Color(0.19f, 0.24f, 0.2f));

            GameObject lightObject = new GameObject("Third Person Smoke Sun", typeof(Light));
            Light sun = lightObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(46f, -32f, 0f);

            ThirdPersonFeatureCatalog catalog = Resources.Load<ThirdPersonFeatureCatalog>("ThirdPersonFeatureCatalog");
            Assert.That(catalog, Is.Not.Null, "Runtime ThirdPersonFeatureCatalog was not generated.");
            GameObject prefab = catalog.PlayerPrefab;
            Assert.That(prefab, Is.Not.Null, $"Prefab missing at {PrefabPath}");
            GameObject player = Object.Instantiate(prefab, testOrigin, Quaternion.identity);
            player.name = "GTA Style Player Smoke Instance";

            GtaStylePlayerInput input = player.GetComponent<GtaStylePlayerInput>();
            GtaStylePlayerMotor motor = player.GetComponent<GtaStylePlayerMotor>();
            GtaStyleProceduralAnimator proceduralAnimator = player.GetComponent<GtaStyleProceduralAnimator>();
            GtaStylePlayerInteractor interactor = player.GetComponent<GtaStylePlayerInteractor>();
            GtaStyleThirdPersonCamera cameraController = player.GetComponentInChildren<GtaStyleThirdPersonCamera>();

            Assert.That(input, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(proceduralAnimator, Is.Not.Null);
            Assert.That(interactor, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(player.GetComponentInChildren<Animator>(), Is.Null, "The primitive character should be animated entirely by code.");

            input.SetSimulationMode(true);
            input.SetSimulatedState(Vector2.zero, Vector2.zero, false, false);
            yield return null;
            yield return null;

            Vector3 startPosition = player.transform.position;
            input.SetSimulatedState(Vector2.up, Vector2.zero, true, false);
            for (int i = 0; i < 45; i++)
            {
                yield return null;
            }

            float movedDistance = Vector3.ProjectOnPlane(player.transform.position - startPosition, Vector3.up).magnitude;
            Assert.That(movedDistance, Is.GreaterThan(1f), "Simulated forward sprint did not move the controller.");
            Assert.That(proceduralAnimator.LocomotionBlend, Is.GreaterThan(0.25f), "Procedural locomotion did not blend in.");

            float yawBeforeOrbit = cameraController.CurrentYaw;
            input.SetSimulatedState(Vector2.zero, new Vector2(24f, -4f), false, false);
            yield return null;
            yield return null;
            input.SetSimulatedState(Vector2.zero, Vector2.zero, false, false);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yawBeforeOrbit, cameraController.CurrentYaw)), Is.GreaterThan(10f),
                "Orbit look input did not rotate the camera.");

            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            Vector3 cameraPivot = player.transform.position + new Vector3(0f, 1.55f, 0f);
            float clearCameraDistance = Vector3.Distance(cameraPivot, cameraController.transform.position);
            GameObject cameraObstruction = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cameraObstruction.name = "Camera Collision Smoke Obstruction";
            cameraObstruction.transform.position = Vector3.Lerp(cameraPivot, cameraController.transform.position, 0.55f);
            cameraObstruction.transform.localScale = Vector3.one * 1.15f;
            cameraObstruction.GetComponent<MeshRenderer>().enabled = false;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            float obstructedCameraDistance = Vector3.Distance(cameraPivot, cameraController.transform.position);
            Assert.That(obstructedCameraDistance, Is.LessThan(clearCameraDistance - 0.3f),
                "Camera obstruction sphere-cast did not pull the camera inward.");
            Object.Destroy(cameraObstruction);
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }
            cameraPivot = player.transform.position + new Vector3(0f, 1.55f, 0f);
            float recoveredCameraDistance = Vector3.Distance(cameraPivot, cameraController.transform.position);
            Assert.That(recoveredCameraDistance, Is.GreaterThan(clearCameraDistance - 0.3f),
                "Camera did not smoothly return to its normal follow distance after the obstruction cleared.");

            for (int i = 0; i < 4 && !motor.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.That(motor.IsGrounded, Is.True, "Controller was not grounded before the jump smoke check.");
            input.QueueSimulatedJump();
            yield return null;
            Assert.That(motor.VerticalVelocity, Is.GreaterThan(0f), "Jump input did not create upward velocity.");

            int landingWaitFrames = 0;
            do
            {
                yield return null;
                landingWaitFrames++;
            }
            while (!motor.IsGrounded && landingWaitFrames < 180);
            Assert.That(motor.IsGrounded, Is.True, "Controller did not land again after the jump smoke check.");

            GameObject microphonePrefab = catalog.StandardMicrophone;
            Assert.That(microphonePrefab, Is.Not.Null);
            Vector3 viewForward = cameraController.transform.forward;
            viewForward.y = 0f;
            viewForward.Normalize();
            Vector3 microphonePosition = player.transform.position + viewForward * 1.8f
                + Vector3.ProjectOnPlane(cameraController.transform.right, Vector3.up).normalized * 0.9f;
            GameObject microphoneObject = Object.Instantiate(microphonePrefab, microphonePosition, Quaternion.Euler(0f, 180f, 0f));
            RecordableMicrophone microphone = microphoneObject.GetComponent<RecordableMicrophone>();
            Assert.That(microphone, Is.Not.Null);

            yield return CapturePreview(cameraController.ControlledCamera, "GtaStylePlayerPlayMode.png");

            yield return null;
            yield return null;
            Assert.That(interactor.CurrentInteractable, Is.Not.Null,
                "The player's view-weighted proximity interaction did not acquire the microphone.");
            input.QueueSimulatedInteract();
            yield return null;
            yield return null;
            Assert.That(MicrophoneRecorderWindow.IsOpen, Is.True, "Interacting with the microphone did not open the recorder UI.");
            Assert.That(input.GameplayBlocked, Is.True, "Opening recorder UI did not block movement/camera input.");
            MicrophoneRecorderWindow window = Object.FindAnyObjectByType<MicrophoneRecorderWindow>(FindObjectsInactive.Include);
            Assert.That(window, Is.Not.Null);
            Canvas recorderCanvas = window.GetComponent<Canvas>();
            recorderCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            recorderCanvas.worldCamera = cameraController.ControlledCamera;
            recorderCanvas.planeDistance = 0.4f;
            yield return CapturePreview(cameraController.ControlledCamera, "MicrophoneRecorderUI.png");

            MicrophoneRecorderWindow.CloseActive();
            yield return null;
            Assert.That(input.GameplayBlocked, Is.False, "Closing recorder UI did not restore gameplay input.");

            float finalYawDelta = Mathf.Abs(Mathf.DeltaAngle(yawBeforeOrbit, cameraController.CurrentYaw));

            Object.Destroy(player);
            Object.Destroy(ground);
            Object.Destroy(lightObject);
            Object.Destroy(microphoneObject);
            Object.Destroy(window.gameObject);
            yield return null;

            Debug.Log($"[GTA Style Player] PLAY_MODE_SMOKE_PASSED moved={movedDistance:F2}m yawDelta={finalYawDelta:F1}");
        }

        [Test]
        public void MicrophoneQualityPresetsApplyProgressivelyStrongerDamage()
        {
            const int sampleRate = 48000;
            const int frames = 12000;
            const int channels = 2;
            float[] source = new float[frames * channels];
            for (int frame = 0; frame < frames; frame++)
            {
                float time = frame / (float)sampleRate;
                float sample = Mathf.Sin(time * 2f * Mathf.PI * 320f) * 0.42f
                    + Mathf.Sin(time * 2f * Mathf.PI * 7200f) * 0.2f;
                source[frame * channels] = sample;
                source[frame * channels + 1] = sample * 0.92f;
            }

            float[] studio = MicrophoneQualityProcessor.ProcessSamples(source, frames, channels, sampleRate,
                MicrophoneQualityTier.Studio, out int studioChannels);
            float[] standard = MicrophoneQualityProcessor.ProcessSamples(source, frames, channels, sampleRate,
                MicrophoneQualityTier.Standard, out int standardChannels);
            float[] cheap = MicrophoneQualityProcessor.ProcessSamples(source, frames, channels, sampleRate,
                MicrophoneQualityTier.Cheap, out int cheapChannels);
            float[] broken = MicrophoneQualityProcessor.ProcessSamples(source, frames, channels, sampleRate,
                MicrophoneQualityTier.Broken, out int brokenChannels);

            Assert.That(studioChannels, Is.EqualTo(2));
            Assert.That(standardChannels, Is.EqualTo(2));
            Assert.That(cheapChannels, Is.EqualTo(1));
            Assert.That(brokenChannels, Is.EqualTo(1));
            Assert.That(MeanDifference(studio, source), Is.LessThan(MeanDifference(standard, source)));
            Assert.That(MeanDifference(standard, source), Is.LessThan(MeanDifferenceMono(cheap, source, channels)));
            Assert.That(MeanDifferenceMono(cheap, source, channels), Is.LessThan(MeanDifferenceMono(broken, source, channels)));
            Assert.That(MicrophoneQualityProcessor.GetProfile(MicrophoneQualityTier.Broken).BitDepth,
                Is.LessThan(MicrophoneQualityProcessor.GetProfile(MicrophoneQualityTier.Cheap).BitDepth));
            Debug.Log("[Microphone Recorder] QUALITY_PROCESSING_TEST_PASSED");
        }

        private static IEnumerator CapturePreview(Camera camera, string fileName)
        {
            Assert.That(camera, Is.Not.Null);
            const int width = 960;
            const int height = 540;
            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();
            screenshot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            screenshot.Apply();

            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.Destroy(renderTexture);
            Object.Destroy(screenshot);
            yield return null;
        }

        private static float MeanDifference(float[] processed, float[] source)
        {
            int count = Mathf.Min(processed.Length, source.Length);
            double sum = 0d;
            for (int i = 0; i < count; i++)
            {
                sum += Mathf.Abs(processed[i] - source[i]);
            }
            return (float)(sum / count);
        }

        private static float MeanDifferenceMono(float[] processed, float[] source, int sourceChannels)
        {
            double sum = 0d;
            for (int frame = 0; frame < processed.Length; frame++)
            {
                float mono = 0f;
                for (int channel = 0; channel < sourceChannels; channel++)
                {
                    mono += source[frame * sourceChannels + channel];
                }
                mono /= sourceChannels;
                sum += Mathf.Abs(processed[frame] - mono);
            }
            return (float)(sum / processed.Length);
        }

        private static void ApplyPreviewMaterial(GameObject target, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            target.GetComponent<Renderer>().material = material;
        }
    }
}
