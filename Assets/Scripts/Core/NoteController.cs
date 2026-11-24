using UnityEngine;

namespace VRRhythmBeat.Core
{
    /// <summary>
    /// 6레일(곡선 아치 레일 또는 직선 레일)을 따라 플레이어 앞으로 다가오는 파란색 큐브 노드.
    /// Time.deltaTime에 의존하지 않고, 오직 AudioSettings.dspTime 기반 songPosition으로
    /// 호 길이(Arc-Length) 위치와 회전을 결정론적으로 동기화합니다.
    /// </summary>
    public class NoteController : MonoBehaviour
    {
        public NoteData Data { get; private set; }
        public int LaneIndex => Data.laneIndex;
        public double TargetTime => Data.targetTime;
        public bool IsActiveInLane { get; private set; }

        /// <summary>
        /// 타격 버튼(HitPad) 중심까지의 부호 있는 레일 진행 거리(m).
        /// 양수: 버튼 접근 중, 0: 정확히 버튼 중심, 음수: 버튼을 지나쳐 Miss 구간 진입.
        /// </summary>
        public float SignedDistanceToHitPad { get; private set; }

        private CurvedLaneTrack _curvedTrack;
        private float _hitButtonLocalZ;
        private float _noteHeightOffset;
        private float _noteSpeed;

        /// <summary>
        /// 풀에서 꺼낸 노드를 지정된 레일에 배치하고 초기 위치를 동기화합니다.
        /// </summary>
        public void Initialize(
            NoteData data,
            Transform laneParent,
            float hitButtonLocalZ,
            float noteHeightOffset,
            float noteSpeed,
            double currentSongPosition)
        {
            Data = data;
            _hitButtonLocalZ = hitButtonLocalZ;
            _noteHeightOffset = noteHeightOffset;
            _noteSpeed = noteSpeed;
            IsActiveInLane = true;

            _curvedTrack = laneParent != null ? laneParent.GetComponent<CurvedLaneTrack>() : null;

            transform.SetParent(laneParent, false);
            transform.localRotation = Quaternion.identity;

            SyncPosition(currentSongPosition);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// AudioSettings.dspTime 기반 songPosition으로 매 프레임 노드의 정확한 궤도 위치를 갱신합니다.
        /// </summary>
        public void SyncPosition(double songPosition)
        {
            if (!IsActiveInLane) return;

            double timeRemaining = Data.targetTime - songPosition;
            float dist = (float)(timeRemaining * _noteSpeed);
            SignedDistanceToHitPad = dist;

            if (_curvedTrack != null)
            {
                _curvedTrack.EvaluateAtDistance(dist, _noteHeightOffset, out Vector3 worldPos, out Quaternion worldRot);
                transform.SetPositionAndRotation(worldPos, worldRot);
            }
            else
            {
                float localZ = _hitButtonLocalZ + dist;
                transform.localPosition = new Vector3(0f, _noteHeightOffset, localZ);
            }
        }

        /// <summary>
        /// 판정 완료(Perfect/Good/Miss) 후 오브젝트 풀로 반환하기 전 상태를 초기화합니다.
        /// </summary>
        public void Deactivate()
        {
            IsActiveInLane = false;
            _curvedTrack = null;
            gameObject.SetActive(false);
        }
    }
}
