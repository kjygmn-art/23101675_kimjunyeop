using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace VRRhythmBeat.Interaction
{
    /// <summary>
    /// Step 1 타격 매개체: VR 컨트롤러 하위에 부착되는 막대기(Stick).
    /// 방안 A(XR 표준)에 따라 CapsuleCollider(isTrigger = false)와
    /// Kinematic Rigidbody(isKinematic = true, useGravity = false)를 사용하며,
    /// XRI 3.6.1의 HapticImpulsePlayer를 통해 해당 손 컨트롤러에 햅틱을 전달합니다.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class StickController : MonoBehaviour
    {
        public enum HandSide
        {
            Left,
            Right
        }

        [Header("Hand & Haptics (XRI 3.x)")]
        [SerializeField] private HandSide handSide = HandSide.Right;

        [Tooltip("비워두면 부모(Left/Right Controller)에서 HapticImpulsePlayer를 자동 탐색합니다.")]
        [SerializeField] private HapticImpulsePlayer hapticImpulsePlayer;

        public HandSide Side => handSide;

        private CapsuleCollider _capsuleCollider;
        private Rigidbody _rigidbody;

        private void Reset()
        {
            ConfigurePhysics();
        }

        private void Awake()
        {
            ConfigurePhysics();

            if (hapticImpulsePlayer == null)
            {
                hapticImpulsePlayer = GetComponentInParent<HapticImpulsePlayer>(true);
            }
        }

        private void ConfigurePhysics()
        {
            _capsuleCollider = GetComponent<CapsuleCollider>();
            if (_capsuleCollider != null)
            {
                _capsuleCollider.isTrigger = false;
            }

            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                // 방안 A: 컨트롤러 Transform에 1:1 밀착 + 고속 스윙 관통(Tunneling) 방지
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            int stickLayer = LayerMask.NameToLayer("Stick");
            if (stickLayer >= 0)
            {
                gameObject.layer = stickLayer;
            }
        }

        /// <summary>
        /// XRI 3.6.1 HapticImpulsePlayer를 통해 이 막대기를 쥔 컨트롤러에 진동 피드백을 보냅니다.
        /// </summary>
        public void SendHaptic(float amplitude, float duration)
        {
            if (hapticImpulsePlayer == null)
            {
                hapticImpulsePlayer = GetComponentInParent<HapticImpulsePlayer>(true);
            }

            if (hapticImpulsePlayer != null)
            {
                hapticImpulsePlayer.SendHapticImpulse(
                    Mathf.Clamp01(amplitude),
                    Mathf.Max(0.01f, duration));
            }
        }

        /// <summary>
        /// 에디터 셋업 스크립트에서 좌/우 손과 햅틱 플레이어를 설정합니다.
        /// </summary>
        public void ConfigureHand(HandSide side, HapticImpulsePlayer player)
        {
            handSide = side;
            hapticImpulsePlayer = player;
            ConfigurePhysics();
        }
    }
}
