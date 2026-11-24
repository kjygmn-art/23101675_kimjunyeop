using UnityEngine;
using VRRhythmBeat.Core;
using VRRhythmBeat.Gameplay;

namespace VRRhythmBeat.Interaction
{
    /// <summary>
    /// Step 2 기믹 스위치: 6개 레일의 끝자락에 배치된 주황색 물리 타격 버튼(Hit Pad).
    /// BoxCollider(isTrigger = true)에 막대기(Stick)가 진입하는 순간(OnTriggerEnter):
    ///   1) 1차 피드백 상시 실행 (Z축 눌림 애니메이션 + 둔탁한 버튼음 + 컨트롤러 햅틱)
    ///   2) 연결된 레일(Lane 0~5)의 JudgmentManager 판정 호출
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(AudioSource))]
    public class HitPadButton : MonoBehaviour
    {
        [Header("Lane Binding")]
        [SerializeField, Range(0, 5)] private int laneIndex;
        [SerializeField] private JudgmentManager judgmentManager;

        [Header("Visual Press Animation (Z-Axis, Zero GC)")]
        [Tooltip("실제로 Z축으로 눌렸다 복귀할 주황색 버튼 메시 Transform")]
        [SerializeField] private Transform buttonVisual;
        [Tooltip("버튼이 눌릴 때 Local Z축으로 들어가는 깊이(m)")]
        [SerializeField] private float pressDepthZ = 0.08f;
        [Tooltip("눌린 후 원위치로 복귀하는 총 애니메이션 시간(초)")]
        [SerializeField] private float pressAnimDuration = 0.14f;

        [Header("1st-Tier Feedback (Always on Button Touch)")]
        [Tooltip("비워두면 내장된 둔탁한 버튼 타격음이 자동 생성됩니다.")]
        [SerializeField] private AudioClip buttonThudClip;
        [SerializeField, Range(0f, 1f)] private float buttonSoundVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.65f;
        [SerializeField, Range(0.01f, 0.2f)] private float hapticDuration = 0.055f;

        [Header("Trigger Stability")]
        [Tooltip("콜라이더 경계 미세 떨림으로 인한 중복 타격 방지 쿨다운(초)")]
        [SerializeField] private float hitCooldownSeconds = 0.055f;

        public int LaneIndex => laneIndex;

        private BoxCollider _boxCollider;
        private AudioSource _audioSource;
        private Vector3 _initialVisualLocalPos;
        private float _animElapsed = 1f;
        private bool _isAnimating;
        private double _lastHitDspTime = -10.0;

        private Renderer _visualRenderer;
        private MaterialPropertyBlock _mpb;
        private Color _baseEmissionColor;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Reset()
        {
            ConfigureTrigger();
        }

        private void Awake()
        {
            ConfigureTrigger();

            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.65f;

            if (buttonThudClip == null)
            {
                buttonThudClip = ProceduralAudioFactory.CreateButtonThudSfx();
            }

            if (buttonVisual == null)
            {
                buttonVisual = transform;
            }
            _initialVisualLocalPos = buttonVisual.localPosition;

            _visualRenderer = buttonVisual.GetComponentInChildren<Renderer>();
            if (_visualRenderer != null)
            {
                _mpb = new MaterialPropertyBlock();
                if (_visualRenderer.sharedMaterial != null && _visualRenderer.sharedMaterial.HasProperty(EmissionColorId))
                {
                    _baseEmissionColor = _visualRenderer.sharedMaterial.GetColor(EmissionColorId);
                }
                else
                {
                    _baseEmissionColor = new Color(1f, 0.45f, 0.05f) * 0.4f;
                }
            }
        }

        private void ConfigureTrigger()
        {
            _boxCollider = GetComponent<BoxCollider>();
            if (_boxCollider != null)
            {
                _boxCollider.isTrigger = true;
            }

            int hitPadLayer = LayerMask.NameToLayer("HitPad");
            if (hitPadLayer >= 0)
            {
                gameObject.layer = hitPadLayer;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 막대기(StickController)와의 충돌만 처리
            StickController stick = other.GetComponentInParent<StickController>();
            if (stick == null) return;

            ProcessHit(stick);
        }

        /// <summary>
        /// 에디터/데스크톱 테스트용(키보드 1~6)으로 버튼 눌림을 시뮬레이션할 때 호출합니다.
        /// </summary>
        public void TriggerDebugHit()
        {
            ProcessHit(null);
        }

        private void ProcessHit(StickController stick)
        {
            double now = AudioSettings.dspTime;
            if (now - _lastHitDspTime < hitCooldownSeconds)
            {
                return;
            }
            _lastHitDspTime = now;

            // --- 1차 피드백 (노드 유무와 관계없이 상시 발동) ---
            StartPressAnimation();

            if (stick != null)
            {
                stick.SendHaptic(hapticAmplitude, hapticDuration);
            }

            if (_audioSource != null && buttonThudClip != null)
            {
                _audioSource.PlayOneShot(buttonThudClip, buttonSoundVolume);
            }

            // --- Step 3: 판정 매니저 호출 ---
            if (judgmentManager != null)
            {
                judgmentManager.EvaluateLaneHit(laneIndex, this, stick);
            }
        }

        private void StartPressAnimation()
        {
            _animElapsed = 0f;
            _isAnimating = true;
            ApplyPressVisual(1f);
        }

        private void Update()
        {
            if (!_isAnimating) return;

            _animElapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(_animElapsed / Mathf.Max(0.01f, pressAnimDuration));

            // 빠르게 푹 들어갔다(전반 25%) 부드럽게 복귀(후반 75%)
            float depthFactor = normalized < 0.25f
                ? Mathf.Lerp(0.6f, 1f, normalized / 0.25f)
                : Mathf.SmoothStep(1f, 0f, (normalized - 0.25f) / 0.75f);

            ApplyPressVisual(depthFactor);

            if (normalized >= 1f)
            {
                _isAnimating = false;
                ApplyPressVisual(0f);
            }
        }

        private void ApplyPressVisual(float depthFactor)
        {
            if (buttonVisual != null)
            {
                // Local +Z(전방 타격 방향)으로 푹 들어갔다 나오는 피드백
                buttonVisual.localPosition = _initialVisualLocalPos + new Vector3(0f, 0f, pressDepthZ * depthFactor);
            }

            if (_visualRenderer != null && _mpb != null)
            {
                _visualRenderer.GetPropertyBlock(_mpb);
                Color flashColor = Color.Lerp(_baseEmissionColor, new Color(1f, 0.75f, 0.2f) * 2.5f, depthFactor);
                _mpb.SetColor(EmissionColorId, flashColor);
                _visualRenderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// 에디터 셋업 스크립트에서 레일 번호, 판정 매니저, 비주얼 메시를 연결합니다.
        /// </summary>
        public void ConfigurePad(int index, JudgmentManager manager, Transform visual)
        {
            laneIndex = Mathf.Clamp(index, 0, 5);
            judgmentManager = manager;
            buttonVisual = visual;
            ConfigureTrigger();
        }
    }
}
