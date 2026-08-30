using UnityEngine;

namespace Bjorn.ThirdPerson
{
    [CreateAssetMenu(menuName = "Bjorn/Third Person Feature Catalog", fileName = "ThirdPersonFeatureCatalog")]
    public sealed class ThirdPersonFeatureCatalog : ScriptableObject
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private MicrophoneRecorderWindow recorderUiPrefab;
        [SerializeField] private GameObject studioMicrophone;
        [SerializeField] private GameObject standardMicrophone;
        [SerializeField] private GameObject cheapMicrophone;
        [SerializeField] private GameObject brokenMicrophone;

        public GameObject PlayerPrefab => playerPrefab;
        public MicrophoneRecorderWindow RecorderUiPrefab => recorderUiPrefab;
        public GameObject StudioMicrophone => studioMicrophone;
        public GameObject StandardMicrophone => standardMicrophone;
        public GameObject CheapMicrophone => cheapMicrophone;
        public GameObject BrokenMicrophone => brokenMicrophone;

        public void Configure(GameObject player, MicrophoneRecorderWindow recorderUi, GameObject studio,
            GameObject standard, GameObject cheap, GameObject broken)
        {
            playerPrefab = player;
            recorderUiPrefab = recorderUi;
            studioMicrophone = studio;
            standardMicrophone = standard;
            cheapMicrophone = cheap;
            brokenMicrophone = broken;
        }
    }
}
