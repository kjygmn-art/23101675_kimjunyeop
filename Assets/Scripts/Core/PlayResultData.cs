using System;
using UnityEngine;

namespace VRRhythmBeat.Core
{
    /// <summary>
    /// 게임 씬 종료 시 결과 씬(04_Result)으로 전달되는 플레이 결과 스냅샷.
    /// </summary>
    [Serializable]
    public struct PlayResultData
    {
        public string songTitle;
        public string difficultyLabel;
        public int score;
        public int maxCombo;
        public int perfectCount;
        public int goodCount;
        public int missCount;

        public int TotalNotes => perfectCount + goodCount + missCount;

        public float AccuracyPercent
        {
            get
            {
                int total = TotalNotes;
                if (total <= 0) return 0f;
                float weighted = perfectCount + goodCount * 0.5f;
                return Mathf.Clamp((weighted / total) * 100f, 0f, 100f);
            }
        }

        public string Grade
        {
            get
            {
                float acc = AccuracyPercent;
                if (acc >= 95f && missCount == 0) return "S+";
                if (acc >= 90f) return "S";
                if (acc >= 80f) return "A";
                if (acc >= 65f) return "B";
                return "C";
            }
        }
    }
}
