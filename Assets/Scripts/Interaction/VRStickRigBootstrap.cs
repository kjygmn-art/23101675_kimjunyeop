using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace VRRhythmBeat.Interaction
{
    /// <summary>
    /// 03_Game 씬에서 양손 VR 컨트롤러에 막대기(LeftVRStick / RightVRStick)가
    /// 반드시 표시·작동하도록 보장하고, PICO 4 Ultra 손목/그립 각도에 자연스럽게 정렬하는 런타임 보정기.
    /// </summary>
    public class VRStickRigBootstrap : MonoBehaviour
    {
        [Header("Stick Prefab & Pose Alignment")]
        [SerializeField] private StickController stickPrefab;

        [Tooltip("컨트롤러(Aim Pose 기준)에서 막대기 손잡이 위치 오프셋")]
        [SerializeField] private Vector3 stickLocalPosition = new Vector3(0f, -0.015f, 0.0f);

        [Tooltip("PICO 4 Ultra 컨트롤러를 드럼스틱처럼 쥐었을 때 자연스럽게 앞을 향하도록 하는 각도 오프셋(도)")]
        [SerializeField] private Vector3 stickLocalEuler = new Vector3(15f, 0f, 0f);

        [Header("Controller Visual Cleanup")]
        [Tooltip("게임 씬에서는 각도가 어긋나 보일 수 있는 기본 Quest형 컨트롤러 모델 대신 VR 막대기만 깔끔하게 표시합니다.")]
        [SerializeField] private bool hideDefaultControllerModels = true;

        [Header("Stick Colors")]
        [SerializeField] private Color leftStickGlow = new Color(0.15f, 0.85f, 1.0f);
        [SerializeField] private Color rightStickGlow = new Color(1.0f, 0.55f, 0.12f);

        private Transform _leftController;
        private Transform _rightController;

        private void Start()
        {
            BindBothControllers();
        }

        public void BindBothControllers()
        {
            GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)");
            Transform searchRoot = xrOrigin != null ? xrOrigin.transform : transform.root;

            _leftController = FindDeepChild(searchRoot, "Left Controller");
            _rightController = FindDeepChild(searchRoot, "Right Controller");

            EnsureControllerStick(_leftController, StickController.HandSide.Left, "LeftVRStick", leftStickGlow);
            EnsureControllerStick(_rightController, StickController.HandSide.Right, "RightVRStick", rightStickGlow);
        }

        private void EnsureControllerStick(Transform controller, StickController.HandSide side, string stickName, Color glowColor)
        {
            if (controller == null)
            {
                Debug.LogWarning($"[VRStickRigBootstrap] {side} Controller를 찾지 못했습니다.");
                return;
            }

            if (!controller.gameObject.activeSelf)
            {
                controller.gameObject.SetActive(true);
            }

            // 기본 XRI Quest형 컨트롤러 비주얼은 게임 중 숨겨서 손-컨트롤러 각도 이질감 제거
            if (hideDefaultControllerModels)
            {
                HideControllerVisualChild(controller, side == StickController.HandSide.Left ? "Left Controller Visual" : "Right Controller Visual");
                HideControllerVisualChild(controller, side == StickController.HandSide.Left ? "XR Controller Left" : "XR Controller Right");
            }

            StickController existing = controller.GetComponentInChildren<StickController>(true);
            GameObject stickObj;

            if (existing != null)
            {
                stickObj = existing.gameObject;
                stickObj.SetActive(true);
            }
            else if (stickPrefab != null)
            {
                StickController spawned = Instantiate(stickPrefab, controller);
                spawned.name = stickName;
                stickObj = spawned.gameObject;
            }
            else
            {
                stickObj = BuildFallbackStickRuntime(controller, stickName);
            }

            stickObj.transform.localPosition = stickLocalPosition;
            stickObj.transform.localRotation = Quaternion.Euler(stickLocalEuler);
            stickObj.transform.localScale = Vector3.one;

            StickController sc = stickObj.GetComponent<StickController>();
            HapticImpulsePlayer haptic = controller.GetComponentInChildren<HapticImpulsePlayer>(true);
            if (sc != null)
            {
                sc.ConfigureHand(side, haptic);
            }

            TintStickRod(stickObj.transform, glowColor);
        }

        private static void HideControllerVisualChild(Transform controller, string childName)
        {
            Transform visual = FindDeepChild(controller, childName);
            if (visual != null)
            {
                visual.gameObject.SetActive(false);
            }
        }

        private static void TintStickRod(Transform stickRoot, Color glowColor)
        {
            Transform rod = stickRoot.Find("RodMesh");
            if (rod == null) return;
            Renderer r = rod.GetComponent<Renderer>();
            if (r == null) return;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", Color.Lerp(Color.white, glowColor, 0.55f));
            mpb.SetColor("_EmissionColor", glowColor * 1.8f);
            r.SetPropertyBlock(mpb);
        }

        private static GameObject BuildFallbackStickRuntime(Transform parent, string name)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);

            CapsuleCollider cap = root.AddComponent<CapsuleCollider>();
            cap.isTrigger = false;
            cap.direction = 2;
            cap.radius = 0.035f;
            cap.height = 0.52f;
            cap.center = new Vector3(0f, 0f, 0.24f);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            root.AddComponent<StickController>();

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "HandleMesh";
            handle.transform.SetParent(root.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handle.transform.localScale = new Vector3(0.036f, 0.09f, 0.036f);
            Destroy(handle.GetComponent<Collider>());

            GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = "RodMesh";
            rod.transform.SetParent(root.transform, false);
            rod.transform.localPosition = new Vector3(0f, 0f, 0.30f);
            rod.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rod.transform.localScale = new Vector3(0.028f, 0.20f, 0.028f);
            Destroy(rod.GetComponent<Collider>());

            return root;
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent == null) return null;
            foreach (Transform child in parent)
            {
                if (child.name == targetName) return child;
                Transform found = FindDeepChild(child, targetName);
                if (found != null) return found;
            }
            return null;
        }

        public void Configure(StickController prefab)
        {
            stickPrefab = prefab;
        }
    }
}
