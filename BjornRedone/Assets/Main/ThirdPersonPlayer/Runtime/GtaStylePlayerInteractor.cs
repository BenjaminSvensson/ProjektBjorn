using UnityEngine;
using UnityEngine.UI;

namespace Bjorn.ThirdPerson
{
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GtaStylePlayerInput))]
    public sealed class GtaStylePlayerInteractor : MonoBehaviour
    {
        [Header("Interaction Cast")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField, Min(0.5f)] private float interactionDistance = 3.2f;
        [SerializeField, Range(-0.5f, 0.95f)] private float minimumViewDot = 0.35f;
        [SerializeField] private LayerMask interactionMask = ~0;

        [Header("Prompt UI")]
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private Text promptText;

        private readonly Collider[] nearbyColliders = new Collider[32];
        private GtaStylePlayerInput playerInput;
        private IGtaStyleInteractable currentInteractable;
        private bool modalUiOpen;

        public bool ModalUiOpen => modalUiOpen;
        public IGtaStyleInteractable CurrentInteractable => currentInteractable;
        public GtaStylePlayerInput PlayerInput => playerInput;

        private void Awake()
        {
            playerInput = GetComponent<GtaStylePlayerInput>();
            if (interactionCamera == null)
            {
                interactionCamera = GetComponentInChildren<Camera>(true);
            }
            SetPromptVisible(false);
        }

        private void Update()
        {
            if (modalUiOpen)
            {
                currentInteractable = null;
                SetPromptVisible(false);
                return;
            }

            currentInteractable = FindInteractable();
            bool canInteract = currentInteractable != null && currentInteractable.CanInteract(this);
            SetPromptVisible(canInteract);

            if (canInteract)
            {
                if (promptText != null)
                {
                    promptText.text = $"[E]  {currentInteractable.InteractionPrompt}";
                }

                if (playerInput != null && playerInput.InteractPressed)
                {
                    currentInteractable.Interact(this);
                }
            }
        }

        private IGtaStyleInteractable FindInteractable()
        {
            if (interactionCamera == null)
            {
                return null;
            }

            Transform cameraTransform = interactionCamera.transform;
            Vector3 interactionOrigin = transform.position + Vector3.up * 0.9f;
            int hitCount = Physics.OverlapSphereNonAlloc(interactionOrigin, interactionDistance,
                nearbyColliders, interactionMask, QueryTriggerInteraction.Collide);

            float bestScore = float.PositiveInfinity;
            IGtaStyleInteractable closest = null;
            for (int i = 0; i < hitCount; i++)
            {
                Collider candidateCollider = nearbyColliders[i];
                if (candidateCollider == null || candidateCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                Vector3 focusPoint = candidateCollider.bounds.center;
                Vector3 cameraToTarget = focusPoint - cameraTransform.position;
                if (cameraToTarget.sqrMagnitude <= 0.001f)
                {
                    continue;
                }
                float viewDot = Vector3.Dot(cameraTransform.forward, cameraToTarget.normalized);
                if (viewDot < minimumViewDot)
                {
                    continue;
                }

                float playerDistance = Vector3.Distance(interactionOrigin, candidateCollider.ClosestPoint(interactionOrigin));
                float score = playerDistance + (1f - viewDot) * 1.75f;
                MonoBehaviour[] behaviours = candidateCollider.GetComponentsInParent<MonoBehaviour>(true);
                for (int j = 0; j < behaviours.Length; j++)
                {
                    if (behaviours[j] is IGtaStyleInteractable interactable && interactable.CanInteract(this)
                        && score < bestScore)
                    {
                        bestScore = score;
                        closest = interactable;
                    }
                }
            }

            return closest;
        }

        private void SetPromptVisible(bool visible)
        {
            if (promptRoot != null && promptRoot.activeSelf != visible)
            {
                promptRoot.SetActive(visible);
            }
        }

        public void SetModalUiOpen(bool open)
        {
            modalUiOpen = open;
            SetPromptVisible(false);
            if (playerInput == null)
            {
                playerInput = GetComponent<GtaStylePlayerInput>();
            }
            playerInput?.SetGameplayBlocked(open);
        }

        public void Configure(Camera sourceCamera, GameObject promptPanel, Text promptLabel)
        {
            interactionCamera = sourceCamera;
            promptRoot = promptPanel;
            promptText = promptLabel;
            playerInput = GetComponent<GtaStylePlayerInput>();
            SetPromptVisible(false);
        }
    }
}
