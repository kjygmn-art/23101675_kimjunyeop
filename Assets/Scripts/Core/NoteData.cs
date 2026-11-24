using System;
using UnityEngine;

namespace VRRhythmBeat.Core
{
    /// <summary>
    /// 데이터 주도형 채보의 단일 노트 정보.
    /// 노드가 타격 버튼(HitPad)에 도달해야 하는 절대 시간(초)과 레일 번호(0~5)를 저장합니다.
    /// </summary>
    [Serializable]
    public struct NoteData
    {
        [Tooltip("곡 시작(0초) 기준, 이 노드가 타격 버튼(HitPad)에 정확히 도달해야 하는 절대 시간(초)")]
        public double targetTime;

        [Tooltip("6레일 인덱스 (0~1: 바닥 0°, 2~3: 대각선 ±45°, 4~5: 벽면 ±90°)")]
        [Range(0, 5)]
        public int laneIndex;

        public NoteData(double targetTime, int laneIndex)
        {
            this.targetTime = targetTime;
            this.laneIndex = Mathf.Clamp(laneIndex, 0, 5);
        }
    }
}
