using System.Collections.Generic;
using UnityEngine;

namespace VRRhythmBeat.Core
{
    /// <summary>
    /// 곡의 오디오 클립, BPM, 난이도, 권장 노트 속도 및 정렬된 채보(NoteData) 리스트를 보관하는 ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "CanonInD_Beatmap", menuName = "VR Rhythm Beat/Beatmap Data")]
    public class BeatmapData : ScriptableObject
    {
        [Header("Song Metadata")]
        public string songTitle = "Canon in D";
        public string artist = "Johann Pachelbel";
        public string difficultyLabel = "NORMAL";
        [TextArea(2, 3)]
        public string description = "6레일 U자형 입체 궤도에서 캐논의 선율을 온몸으로 타격하는 스탠다드 채보";
        public float bpm = 90f;
        public float recommendedNoteSpeed = 8.0f;

        [Tooltip("비워두면 런타임에 내장된 캐논(Canon in D) 신스 오디오가 자동 생성됩니다.")]
        public AudioClip audioClip;

        [Tooltip("오디오 출력 지연 보정값(초). 노트가 음악보다 늦게 느껴지면 음수, 빠르면 양수로 미세 조정")]
        public double audioOffsetSeconds = 0.0;

        [Header("Chart Notes (Sorted by TargetTime)")]
        public List<NoteData> notes = new List<NoteData>();

        public void SortNotesByTime()
        {
            if (notes == null) return;
            notes.Sort((a, b) => a.targetTime.CompareTo(b.targetTime));
        }

        /// <summary>
        /// NCS_Fade.wav (Alan Walker - Fade, 90 BPM) 실음원 분석 기반 EASY 채보.
        /// </summary>
        [ContextMenu("Populate Fade (EASY)")]
        public void PopulateFadeEasy(AudioClip clip = null)
        {
            songTitle = "Fade - Gentle Flow";
            artist = "Alan Walker [NCS Release]";
            difficultyLabel = "EASY";
            description = "Fade의 90 BPM 메인 멜로디와 킥 비트에 맞춰 6레일 완만 아크를 편안히 즐기는 입문 채보";
            bpm = FadeBeatmapLibrary.FadeBpm;
            recommendedNoteSpeed = 6.5f;
            audioOffsetSeconds = 0.0;
            if (clip != null) audioClip = clip;
            notes = FadeBeatmapLibrary.BuildEasyNotes();
            SortNotesByTime();
        }

        /// <summary>
        /// NCS_Fade.wav (Alan Walker - Fade, 90 BPM) 실음원 분석 기반 NORMAL 채보.
        /// </summary>
        [ContextMenu("Populate Fade (NORMAL)")]
        public void PopulateFadeNormal(AudioClip clip = null)
        {
            songTitle = "Fade - VR Arcade";
            artist = "Alan Walker [NCS Release]";
            difficultyLabel = "NORMAL";
            description = "인트로 플럭(3.67s), 드롭1(80s), 드롭2 클라이맥스(176s)까지 원곡 파형에 밀착 동기화된 스탠다드 채보";
            bpm = FadeBeatmapLibrary.FadeBpm;
            recommendedNoteSpeed = 8.0f;
            audioOffsetSeconds = 0.0;
            if (clip != null) audioClip = clip;
            notes = FadeBeatmapLibrary.BuildNormalNotes();
            SortNotesByTime();
        }

        /// <summary>
        /// NCS_Fade.wav (Alan Walker - Fade, 90 BPM) 실음원 분석 기반 HARD 채보.
        /// </summary>
        [ContextMenu("Populate Fade (HARD)")]
        public void PopulateFadeHard(AudioClip clip = null)
        {
            songTitle = "Fade - Overdrive";
            artist = "Alan Walker [NCS Release]";
            difficultyLabel = "HARD";
            description = "드롭 직전 16분음표 스네어 롤, 고속 8분음표 스트림과 양손 동시타가 몰아치는 상급자용 채보";
            bpm = FadeBeatmapLibrary.FadeBpm;
            recommendedNoteSpeed = 10.0f;
            audioOffsetSeconds = 0.0;
            if (clip != null) audioClip = clip;
            notes = FadeBeatmapLibrary.BuildHardNotes();
            SortNotesByTime();
        }

        /// <summary>
        /// EASY (78 BPM): 바닥(0,1) + 대각선(2,3) 중심, 느긋한 4분/2분음표 입문 패턴.
        /// </summary>
        [ContextMenu("Generate Canon in D (EASY)")]
        public void PopulateCanonEasy()
        {
            songTitle = "Canon in D - Gentle Prelude";
            artist = "Johann Pachelbel";
            difficultyLabel = "EASY";
            description = "6레일 U자 공간의 거리감과 물리 타격감을 익히기 좋은 입문 난이도";
            bpm = 78f;
            recommendedNoteSpeed = 6.5f;
            audioOffsetSeconds = 0.0;
            notes = new List<NoteData>();

            double beat = 60.0 / bpm;
            double leadIn = beat * 4.0;

            int[] pattern = { 0, 1, 2, 3, 0, 1, 4, 5, 2, 3, 0, 1, 4, 2, 5, 3 };
            for (int cycle = 0; cycle < 2; cycle++)
            {
                double start = leadIn + cycle * 32.0 * beat;
                for (int i = 0; i < pattern.Length; i++)
                {
                    notes.Add(new NoteData(start + i * 2.0 * beat, pattern[i]));
                }
            }

            // 마지막 마무리 양손 바닥 동시타
            double endT = leadIn + 64.0 * beat;
            notes.Add(new NoteData(endT, 0));
            notes.Add(new NoteData(endT, 1));
            SortNotesByTime();
        }

        /// <summary>
        /// NORMAL (90 BPM): 파헬벨 캐논 기본 6레일 입체 채보 (바닥 -> 대각선 -> 수직 벽면 확장).
        /// </summary>
        [ContextMenu("Generate Canon in D (NORMAL)")]
        public void PopulateCanonInDSample()
        {
            songTitle = "Canon in D - VR Arcade";
            artist = "Johann Pachelbel";
            difficultyLabel = "NORMAL";
            description = "바닥(0°), 대각선(±45°), 벽면(±90°) 전 레일을 순환하는 스탠다드 캐논 채보";
            bpm = 90f;
            recommendedNoteSpeed = 8.0f;
            audioOffsetSeconds = 0.0;
            notes = new List<NoteData>();

            double beat = 60.0 / bpm;
            double leadIn = beat * 4.0;

            // Section 1: 바닥 & 대각선 교차
            int[] introLanes = { 0, 1, 2, 3, 0, 1, 2, 3 };
            for (int i = 0; i < introLanes.Length; i++)
            {
                double t = leadIn + i * 2.0 * beat;
                notes.Add(new NoteData(t, introLanes[i]));
                notes.Add(new NoteData(t + beat, introLanes[i] ^ 1));
            }

            // Section 2: U자 공간 전체(0~5) 순환
            double sec2Start = leadIn + 16.0 * beat;
            int[] melodyPattern = { 4, 2, 0, 1, 3, 5, 3, 1, 0, 2, 4, 2, 1, 3, 5, 3 };
            for (int i = 0; i < melodyPattern.Length; i++)
            {
                notes.Add(new NoteData(sec2Start + i * beat, melodyPattern[i]));
            }

            // Section 3: 8분음표 캐논 변주
            double sec3Start = leadIn + 32.0 * beat;
            int[] variationA = { 0, 2, 4, 2, 1, 3, 5, 3 };
            for (int bar = 0; bar < 4; bar++)
            {
                double barStart = sec3Start + bar * 4.0 * beat;
                for (int step = 0; step < 8; step++)
                {
                    int lane = variationA[(step + bar * 2) % variationA.Length];
                    notes.Add(new NoteData(barStart + step * 0.5 * beat, lane));
                }
            }

            // Section 4: 클라이맥스 양손 동시타 + U자 스윕
            double sec4Start = leadIn + 48.0 * beat;
            for (int bar = 0; bar < 4; bar++)
            {
                double barStart = sec4Start + bar * 4.0 * beat;
                notes.Add(new NoteData(barStart, 0));
                notes.Add(new NoteData(barStart, 1));
                notes.Add(new NoteData(barStart + 0.5 * beat, 2));
                notes.Add(new NoteData(barStart + 1.0 * beat, 3));
                notes.Add(new NoteData(barStart + 1.5 * beat, 4));
                notes.Add(new NoteData(barStart + 1.5 * beat, 5));
                notes.Add(new NoteData(barStart + 2.0 * beat, 2));
                notes.Add(new NoteData(barStart + 2.5 * beat, 0));
                notes.Add(new NoteData(barStart + 3.0 * beat, 3));
                notes.Add(new NoteData(barStart + 3.5 * beat, 1));
            }

            double finaleTime = leadIn + 64.0 * beat;
            notes.Add(new NoteData(finaleTime, 4));
            notes.Add(new NoteData(finaleTime, 5));

            SortNotesByTime();
        }

        /// <summary>
        /// HARD (112 BPM): 고속 8분음표 연타와 양손 교차·벽면 동시타가 몰아치는 록/아케이드 캐논 채보.
        /// </summary>
        [ContextMenu("Generate Canon in D (HARD)")]
        public void PopulateCanonHard()
        {
            songTitle = "Canon Rock - Overdrive";
            artist = "Johann Pachelbel (Arr. VR)";
            difficultyLabel = "HARD";
            description = "112 BPM 고속 U자 스윙과 양손 동시타가 몰아치는 상급자용 아케이드 채보";
            bpm = 112f;
            recommendedNoteSpeed = 10.5f;
            audioOffsetSeconds = 0.0;
            notes = new List<NoteData>();

            double beat = 60.0 / bpm;
            double leadIn = beat * 4.0;

            int[] wave = { 0, 2, 4, 5, 3, 1, 2, 3 };
            for (int bar = 0; bar < 16; bar++)
            {
                double barStart = leadIn + bar * 4.0 * beat;
                for (int step = 0; step < 8; step++)
                {
                    double t = barStart + step * 0.5 * beat;
                    int primary = wave[(step + bar) % wave.Length];
                    notes.Add(new NoteData(t, primary));

                    // 매 마디 첫 박과 3박에 대칭 양손 동시타 삽입
                    if (step == 0 || (bar >= 8 && step == 4))
                    {
                        int mirror = primary ^ 1;
                        notes.Add(new NoteData(t, mirror));
                    }
                }
            }

            double finaleTime = leadIn + 64.0 * beat;
            notes.Add(new NoteData(finaleTime, 0));
            notes.Add(new NoteData(finaleTime, 1));
            notes.Add(new NoteData(finaleTime + 0.5 * beat, 2));
            notes.Add(new NoteData(finaleTime + 0.5 * beat, 3));
            notes.Add(new NoteData(finaleTime + 1.0 * beat, 4));
            notes.Add(new NoteData(finaleTime + 1.0 * beat, 5));

            SortNotesByTime();
        }
    }
}
