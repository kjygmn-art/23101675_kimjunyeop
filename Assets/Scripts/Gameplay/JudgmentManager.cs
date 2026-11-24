using System;
using System.Collections.Generic;
using UnityEngine;
using VRRhythmBeat.Core;
using VRRhythmBeat.Interaction;

namespace VRRhythmBeat.Gameplay
{
    /// <summary>
    /// Step 3 판정 매니저 & 2차 피드백 오케스트레이터.
    /// - 6개 레일별 활성 노드 큐(Queue)를 관리하여 O(1) 속도로 선두 노드의 Z축 거리를 판정합니다.
    /// - 오차 <= 0.3f: Perfect (100점)
    /// - 0.3f < 오차 <= 0.6f: Good (50점)
    /// - 오차 > 0.6f: 허공 타격 (무시, 콤보 유지)
    /// - 타격선 뒤(hitPadZ - 0.6f 미만) 통과 시: 강제 Miss (0점, 콤보 초기화) 후 풀 반환
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class JudgmentManager : MonoBehaviour
    {
        public const int LaneCount = 6;

        [Header("Core References")]
        [SerializeField] private NotePooler notePooler;
        [SerializeField] private VfxPooler vfxPooler;
        [SerializeField] private HitPadButton[] hitPads = new HitPadButton[LaneCount];

        [Header("Z-Axis Distance Judgment Thresholds (Meters)")]
        [SerializeField] private float perfectThresholdZ = 0.3f;
        [SerializeField] private float goodThresholdZ = 0.6f;

        [Header("Scoring")]
        [SerializeField] private int perfectScore = 100;
        [SerializeField] private int goodScore = 50;

        [Header("2nd-Tier Feedback (On Perfect / Good Hit)")]
        [SerializeField] private AudioSource hitSfxSource;
        [Tooltip("비워두면 내장된 맑은 Perfect 전자음이 자동 생성됩니다.")]
        [SerializeField] private AudioClip perfectChimeClip;
        [Tooltip("비워두면 내장된 경쾌한 Good 전자음이 자동 생성됩니다.")]
        [SerializeField] private AudioClip goodChimeClip;
        [SerializeField, Range(0f, 1f)] private float hitChimeVolume = 0.85f;

        // 레일 0~5별 현재 주행 중인 활성 노드 리스트 (시간순 정렬 유지)
        private readonly List<NoteController>[] _laneActiveNotes = new List<NoteController>[LaneCount];

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int PerfectCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }
        public int ActiveNoteCount { get; private set; }

        public event Action<JudgmentType, int, float> OnJudgment; // (판정, 레일번호, Z오차)
        public event Action<int, int, int> OnScoreUpdated;        // (점수, 현재콤보, 최대콤보)

        private void Awake()
        {
            for (int i = 0; i < LaneCount; i++)
            {
                _laneActiveNotes[i] = new List<NoteController>(32);
            }

            if (hitSfxSource == null)
            {
                hitSfxSource = GetComponent<AudioSource>();
            }
            hitSfxSource.playOnAwake = false;
            hitSfxSource.spatialBlend = 0f; // 2차 판정 전자음은 명료한 2D 스테레오로 출력

            if (perfectChimeClip == null)
            {
                perfectChimeClip = ProceduralAudioFactory.CreateNoteHitChimeSfx(true);
            }
            if (goodChimeClip == null)
            {
                goodChimeClip = ProceduralAudioFactory.CreateNoteHitChimeSfx(false);
            }

            ConfigurePhysicsLayerIsolation();
        }

        /// <summary>
        /// Physics Layer 충돌 매트릭스 안전장치:
        /// Stick 레이어는 오직 HitPad 레이어와만 상호작용하도록 런타임에도 보장합니다.
        /// </summary>
        private static void ConfigurePhysicsLayerIsolation()
        {
            int stickLayer = LayerMask.NameToLayer("Stick");
            int hitPadLayer = LayerMask.NameToLayer("HitPad");
            int noteLayer = LayerMask.NameToLayer("Note");

            if (stickLayer < 0 || hitPadLayer < 0) return;

            for (int layer = 0; layer < 32; layer++)
            {
                if (layer == hitPadLayer)
                {
                    Physics.IgnoreLayerCollision(stickLayer, layer, false);
                }
                else
                {
                    Physics.IgnoreLayerCollision(stickLayer, layer, true);
                }
            }

            if (noteLayer >= 0)
            {
                for (int layer = 0; layer < 32; layer++)
                {
                    Physics.IgnoreLayerCollision(noteLayer, layer, true);
                }
            }
        }

        public void ResetState()
        {
            for (int lane = 0; lane < LaneCount; lane++)
            {
                List<NoteController> list = _laneActiveNotes[lane];
                for (int i = 0; i < list.Count; i++)
                {
                    if (notePooler != null)
                    {
                        notePooler.ReturnNote(list[i]);
                    }
                }
                list.Clear();
            }

            ActiveNoteCount = 0;
            Score = 0;
            Combo = 0;
            MaxCombo = 0;
            PerfectCount = 0;
            GoodCount = 0;
            MissCount = 0;

            OnScoreUpdated?.Invoke(Score, Combo, MaxCombo);
        }

        /// <summary>
        /// RhythmConductor가 노드를 스폰할 때 해당 레일의 활성 큐에 등록합니다.
        /// </summary>
        public void RegisterActiveNote(int laneIndex, NoteController note)
        {
            if (laneIndex < 0 || laneIndex >= LaneCount || note == null) return;

            _laneActiveNotes[laneIndex].Add(note);
            ActiveNoteCount++;
        }

        /// <summary>
        /// 매 프레임 RhythmConductor에서 호출:
        /// 1) 모든 활성 노드의 위치를 dspTime 기반 songPosition으로 동기화
        /// 2) 타격선을 지나 플레이어 등 뒤로 넘어간 노드를 강제 Miss 처리 후 풀 반환
        /// </summary>
        public void UpdateActiveNotes(double songPosition)
        {
            for (int lane = 0; lane < LaneCount; lane++)
            {
                List<NoteController> list = _laneActiveNotes[lane];
                if (list.Count == 0) continue;

                // 1) 모든 노드 위치 동기화
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].SyncPosition(songPosition);
                }

                // 2) 타격선 뒤쪽(-goodThresholdZ 미만)으로 지나간 선두 노드 강제 Miss 검사
                while (list.Count > 0)
                {
                    NoteController frontNote = list[0];
                    if (frontNote.SignedDistanceToHitPad < -goodThresholdZ)
                    {
                        list.RemoveAt(0);
                        ActiveNoteCount = Mathf.Max(0, ActiveNoteCount - 1);

                        if (notePooler != null)
                        {
                            notePooler.ReturnNote(frontNote);
                        }

                        ApplyMiss(lane, Mathf.Abs(frontNote.SignedDistanceToHitPad));
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Step 3: 주황색 버튼(HitPadButton)이 눌린 정확한 프레임에 호출되는 레일 진행 거리 판정 메서드.
        /// 곡선 아치 레일에서도 6개 레일 모두 동일한 0.3m(Perfect) / 0.6m(Good) 판정 폭을 보장합니다.
        /// </summary>
        public void EvaluateLaneHit(int laneIndex, HitPadButton hitButton, StickController stick)
        {
            if (laneIndex < 0 || laneIndex >= LaneCount || hitButton == null) return;

            List<NoteController> list = _laneActiveNotes[laneIndex];
            if (list.Count == 0)
            {
                // 노드가 없는 허공 타격: 콤보 유지, 무시
                return;
            }

            NoteController frontNote = list[0];
            float error = Mathf.Abs(frontNote.SignedDistanceToHitPad);

            if (error <= perfectThresholdZ)
            {
                ConsumeNoteAndReward(laneIndex, frontNote, JudgmentType.Perfect, error, hitButton.transform.position);
            }
            else if (error <= goodThresholdZ)
            {
                ConsumeNoteAndReward(laneIndex, frontNote, JudgmentType.Good, error, hitButton.transform.position);
            }
            else
            {
                // 오차 > 0.6f: 허공 타격 (무시, 콤보는 끊기지 않음, 노드 유지)
            }
        }

        private void ConsumeNoteAndReward(
            int laneIndex,
            NoteController note,
            JudgmentType judgment,
            float error,
            Vector3 hitWorldPos)
        {
            _laneActiveNotes[laneIndex].RemoveAt(0);
            ActiveNoteCount = Mathf.Max(0, ActiveNoteCount - 1);

            bool isPerfect = judgment == JudgmentType.Perfect;
            Score += isPerfect ? perfectScore : goodScore;
            Combo++;
            if (Combo > MaxCombo) MaxCombo = Combo;

            if (isPerfect) PerfectCount++;
            else GoodCount++;

            // 2차 피드백: 파란색(Perfect) / 초록색(Good) 파티클 폭발 + 맑고 경쾌한 전자음
            if (vfxPooler != null)
            {
                vfxPooler.PlayHitBurst(note.transform.position, isPerfect);
            }

            if (hitSfxSource != null)
            {
                AudioClip chime = isPerfect ? perfectChimeClip : goodChimeClip;
                if (chime != null)
                {
                    hitSfxSource.PlayOneShot(chime, hitChimeVolume);
                }
            }

            // 노드 회수 (오브젝트 풀 반환)
            if (notePooler != null)
            {
                notePooler.ReturnNote(note);
            }

            OnJudgment?.Invoke(judgment, laneIndex, error);
            OnScoreUpdated?.Invoke(Score, Combo, MaxCombo);
        }

        private void ApplyMiss(int laneIndex, float error)
        {
            MissCount++;
            Combo = 0;

            OnJudgment?.Invoke(JudgmentType.Miss, laneIndex, error);
            OnScoreUpdated?.Invoke(Score, Combo, MaxCombo);
        }

        public void ConfigureReferences(NotePooler pooler, VfxPooler vfx, HitPadButton[] pads)
        {
            notePooler = pooler;
            vfxPooler = vfx;
            hitPads = pads;
        }
    }
}
