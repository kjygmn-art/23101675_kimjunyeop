using UnityEngine;

namespace VRRhythmBeat.Core
{
    /// <summary>
    /// 플레이어의 몸을 수평 반원형 아치(파노라마 콕핏) 형태로 감싸며
    /// 먼 정면 지평선(P3)에서 플레이어 앞 타격 버튼(P0)까지 부드럽게 휘어져 들어오는
    /// 3차 베지어 곡선(Cubic Bezier) 레일 컴포넌트입니다.
    ///
    /// - distanceFromHitPad == 0 : 주황색 타격 버튼(HitPad) 중심
    /// - distanceFromHitPad > 0  : 지평선 방향(스폰 위치)에서 다가오는 구간
    /// - distanceFromHitPad < 0  : 타격 버튼을 지나 플레이어 쪽으로 넘어간 Miss 구간
    /// </summary>
    public class CurvedLaneTrack : MonoBehaviour
    {
        [Header("Cubic Bezier Control Points (World Space)")]
        [Tooltip("u = 0: 주황색 타격 버튼(HitPad) 위치")]
        [SerializeField] private Vector3 p0;
        [Tooltip("타격 버튼 부근 곡률 제어점 (플레이어를 감싸는 아치 방향)")]
        [SerializeField] private Vector3 p1;
        [Tooltip("지평선 부근 곡률 제어점 (정면 시야에 6레일이 나란히 모이는 방향)")]
        [SerializeField] private Vector3 p2;
        [Tooltip("u = 1: 노드가 스폰되는 먼 지평선 끝")]
        [SerializeField] private Vector3 p3;

        [Header("Track Metrics")]
        [SerializeField] private float trackLength = 16.5f;
        [SerializeField] private float laneRollDeg;

        private const int LutSamples = 128;
        private float[] _cumulativeLengths;
        private Vector3 _hitTangent;

        public Vector3 HitPoint => p0;
        public Vector3 SpawnPoint => p3;
        public float TrackLength => trackLength;
        public float LaneRollDeg => laneRollDeg;

        private void Awake()
        {
            BuildArcLengthTable();
        }

        public void Configure(Vector3 point0, Vector3 point1, Vector3 point2, Vector3 point3, float rollDegrees)
        {
            p0 = point0;
            p1 = point1;
            p2 = point2;
            p3 = point3;
            laneRollDeg = rollDegrees;
            BuildArcLengthTable();
        }

        public void BuildArcLengthTable()
        {
            _cumulativeLengths = new float[LutSamples + 1];
            _cumulativeLengths[0] = 0f;

            Vector3 prev = EvaluateBezierRaw(0f);
            float total = 0f;

            for (int i = 1; i <= LutSamples; i++)
            {
                float t = i / (float)LutSamples;
                Vector3 cur = EvaluateBezierRaw(t);
                total += Vector3.Distance(prev, cur);
                _cumulativeLengths[i] = total;
                prev = cur;
            }

            trackLength = Mathf.Max(0.5f, total);

            Vector3 d0 = EvaluateBezierDerivativeRaw(0f);
            _hitTangent = d0.sqrMagnitude > 1e-6f ? d0.normalized : Vector3.forward;
        }

        /// <summary>
        /// 타격 버튼으로부터의 호 길이 거리(m)에 해당하는 정확한 월드 위치와 진행 방향 회전을 계산합니다.
        /// 등속 아크길이 보간(Arc-Length Parameterization)을 사용해 곡선 구간에서도 노트 속도가 완벽히 균일합니다.
        /// </summary>
        public void EvaluateAtDistance(float distanceFromHitPad, float heightOffset, out Vector3 worldPos, out Quaternion worldRot)
        {
            if (_cumulativeLengths == null || _cumulativeLengths.Length != LutSamples + 1)
            {
                BuildArcLengthTable();
            }

            Vector3 basePos;
            Vector3 forwardToHorizon;
            float rollWeight;

            if (distanceFromHitPad <= 0f)
            {
                // 타격선을 지나 플레이어 쪽으로 넘어간 구간: 버튼 접선 방향 그대로 직진 연장
                basePos = p0 + _hitTangent * distanceFromHitPad;
                forwardToHorizon = _hitTangent;
                rollWeight = 1f;
            }
            else if (distanceFromHitPad >= trackLength)
            {
                Vector3 dEnd = EvaluateBezierDerivativeRaw(1f);
                Vector3 endTangent = dEnd.sqrMagnitude > 1e-6f ? dEnd.normalized : Vector3.forward;
                basePos = p3 + endTangent * (distanceFromHitPad - trackLength);
                forwardToHorizon = endTangent;
                rollWeight = 0f;
            }
            else
            {
                float t = DistanceToParam(distanceFromHitPad);
                basePos = EvaluateBezierRaw(t);
                Vector3 deriv = EvaluateBezierDerivativeRaw(t);
                forwardToHorizon = deriv.sqrMagnitude > 1e-6f ? deriv.normalized : Vector3.forward;
                // 지평선에서는 수평(0°), 플레이어 앞 아치로 다가올수록 부드럽게 외곽 기울기(laneRollDeg) 적용
                rollWeight = 1f - Mathf.SmoothStep(0f, 1f, t);
            }

            Quaternion baseRot = Quaternion.LookRotation(forwardToHorizon, Vector3.up);
            worldRot = baseRot * Quaternion.Euler(0f, 0f, laneRollDeg * rollWeight);
            worldPos = basePos + (worldRot * Vector3.up) * heightOffset;
        }

        private float DistanceToParam(float dist)
        {
            // 이진 탐색으로 호 길이 -> 베지어 파라미터 t 변환 (O(log N), GC 0)
            int lo = 0;
            int hi = LutSamples;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if (_cumulativeLengths[mid] < dist) lo = mid;
                else hi = mid;
            }

            float d0 = _cumulativeLengths[lo];
            float d1 = _cumulativeLengths[hi];
            float segFrac = (d1 > d0) ? (dist - d0) / (d1 - d0) : 0f;
            return (lo + segFrac) / LutSamples;
        }

        public Vector3 EvaluateBezierRaw(float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;
            return (uu * u) * p0
                 + (3f * uu * t) * p1
                 + (3f * u * tt) * p2
                 + (tt * t) * p3;
        }

        public Vector3 EvaluateBezierDerivativeRaw(float t)
        {
            float u = 1f - t;
            return (3f * u * u) * (p1 - p0)
                 + (6f * u * t) * (p2 - p1)
                 + (3f * t * t) * (p3 - p2);
        }
    }
}
