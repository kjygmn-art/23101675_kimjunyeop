using UnityEngine;

namespace VRRhythmBeat.Core
{
    /// <summary>
    /// 외부 오디오 에셋(.mp3/.wav)이 아직 연결되지 않았을 때도 즉시 테스트·시연할 수 있도록
    /// 파헬벨의 캐논(Canon in D) BGM과 1차/2차 효과음을 프로시저럴 AudioClip으로 생성합니다.
    /// Inspector에 실제 음원 파일을 넣으면 해당 음원이 최우선 사용됩니다.
    /// </summary>
    public static class ProceduralAudioFactory
    {
        private const int SampleRate = 44100;

        /// <summary>
        /// 90 BPM 캐논(Canon in D) 화성(D-A-Bm-F#m-G-D-G-A)에 맞춘 신스 BGM을 생성합니다.
        /// BeatmapData.PopulateCanonInDSample()의 박자와 1:1 정확히 동기화됩니다.
        /// </summary>
        public static AudioClip CreateCanonInDBgm(float bpm = 90f)
        {
            double beatSec = 60.0 / bpm;
            int totalBeats = 72; // 4박 예비박 + 64박 본곡 + 4박 여운
            int totalSamples = Mathf.CeilToInt((float)(totalBeats * beatSec * SampleRate));
            float[] data = new float[totalSamples];

            // 캐논 8코드 베이스 주파수 (D3, A2, B2, F#2, G2, D2, G2, A2)
            float[] bassFreqs = { 146.83f, 110.00f, 123.47f, 92.50f, 98.00f, 73.42f, 98.00f, 110.00f };
            // 캐논 대표 멜로디 주파수 (F#5, E5, D5, C#5, B4, A4, B4, C#5)
            float[] leadFreqs = { 739.99f, 659.25f, 587.33f, 554.37f, 493.88f, 440.00f, 493.88f, 554.37f };
            // 아르페지오 화음 (각 코드의 3음/5음)
            float[] arpFreqs = { 587.33f, 440.00f, 493.88f, 369.99f, 392.00f, 293.66f, 392.00f, 440.00f };

            double leadInSec = 4.0 * beatSec;

            // 1) 예비박(4박) 부드러운 메트로놈 카운트인
            for (int b = 0; b < 4; b++)
            {
                AddPluckTone(data, b * beatSec, 0.08, b == 0 ? 880f : 660f, 0.22f);
            }

            // 2) 본곡 4개 섹션 (총 64박 = 16마디 = 캐논 8코드 x 4사이클)
            for (int cycle = 0; cycle < 4; cycle++)
            {
                double cycleStart = leadInSec + cycle * 16.0 * beatSec;

                for (int chord = 0; chord < 8; chord++)
                {
                    double chordStart = cycleStart + chord * 2.0 * beatSec;
                    float bass = bassFreqs[chord];
                    float lead = leadFreqs[chord];
                    float arp = arpFreqs[chord];

                    // 모든 사이클: 따뜻한 첼로풍 베이스 지속음 (2박)
                    AddWarmPad(data, chordStart, 2.0 * beatSec, bass, 0.22f);
                    // 매 박자마다 은은한 리듬 펄스
                    AddPluckTone(data, chordStart, 0.25, bass * 2f, 0.14f);
                    AddPluckTone(data, chordStart + beatSec, 0.20, arp, 0.10f);

                    // 사이클 1 이상: 메인 캐논 멜로디
                    if (cycle >= 1)
                    {
                        AddPluckTone(data, chordStart, 1.8 * beatSec, lead, 0.20f);
                    }

                    // 사이클 2 이상: 8분음표 캐논 아르페지오
                    if (cycle >= 2)
                    {
                        for (int step = 0; step < 4; step++)
                        {
                            double t = chordStart + step * 0.5 * beatSec;
                            float freq = (step % 2 == 0) ? lead : arp * 1.5f;
                            AddPluckTone(data, t, 0.35 * beatSec, freq, 0.12f);
                        }
                    }

                    // 사이클 3 (클라이맥스): 옥타브 하모니 추가
                    if (cycle >= 3)
                    {
                        AddPluckTone(data, chordStart, 0.9 * beatSec, lead * 2f, 0.10f);
                        AddPluckTone(data, chordStart + beatSec, 0.9 * beatSec, arp * 2f, 0.10f);
                    }
                }
            }

            // 3) 피날레 D 메이저 화음 (D4 + F#4 + A4 + D5)
            double finaleStart = leadInSec + 64.0 * beatSec;
            AddWarmPad(data, finaleStart, 3.5 * beatSec, 293.66f, 0.18f);
            AddWarmPad(data, finaleStart, 3.5 * beatSec, 369.99f, 0.14f);
            AddWarmPad(data, finaleStart, 3.5 * beatSec, 440.00f, 0.14f);
            AddPluckTone(data, finaleStart, 3.5 * beatSec, 587.33f, 0.20f);

            // 소프트 클리핑 방지 정규화
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Mathf.Clamp(data[i], -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("Procedural_CanonInD_90BPM", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 1차 피드백용: 주황색 버튼을 막대기로 쳤을 때 나는 짧고 둔탁한 물리 타격음.
        /// </summary>
        public static AudioClip CreateButtonThudSfx()
        {
            int samples = Mathf.CeilToInt(0.085f * SampleRate);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 48f);
                float freq = Mathf.Lerp(175f, 68f, t / 0.085f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                float click = Mathf.Sin(2f * Mathf.PI * 520f * t) * Mathf.Exp(-t * 140f) * 0.35f;
                data[i] = (tone * 0.75f + click) * env * 0.7f;
            }

            AudioClip clip = AudioClip.Create("Procedural_ButtonThud", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 2차 피드백용: Perfect/Good 판정 성공 시 나는 맑고 경쾌한 전자음.
        /// </summary>
        public static AudioClip CreateNoteHitChimeSfx(bool isPerfect)
        {
            float duration = isPerfect ? 0.22f : 0.16f;
            int samples = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[samples];

            float f1 = isPerfect ? 1046.50f : 783.99f; // C6 (Perfect) vs G5 (Good)
            float f2 = isPerfect ? 1567.98f : 1174.66f; // G6 vs D6

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float attack = Mathf.Clamp01(t / 0.005f);
                float env = attack * Mathf.Exp(-t * (isPerfect ? 14f : 19f));
                float wave = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.65f
                           + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.35f;
                data[i] = wave * env * 0.65f;
            }

            AudioClip clip = AudioClip.Create(isPerfect ? "Procedural_PerfectChime" : "Procedural_GoodChime", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void AddPluckTone(float[] buffer, double startSec, double durationSec, float freq, float amplitude)
        {
            int startIdx = Mathf.Max(0, Mathf.FloorToInt((float)(startSec * SampleRate)));
            int count = Mathf.FloorToInt((float)(durationSec * SampleRate));
            int endIdx = Mathf.Min(buffer.Length, startIdx + count);

            for (int i = startIdx; i < endIdx; i++)
            {
                float t = (i - startIdx) / (float)SampleRate;
                float attack = Mathf.Clamp01(t / 0.006f);
                float decay = Mathf.Exp(-t * 5.5f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.72f
                           + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.20f
                           + Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.08f;
                buffer[i] += wave * attack * decay * amplitude;
            }
        }

        private static void AddWarmPad(float[] buffer, double startSec, double durationSec, float freq, float amplitude)
        {
            int startIdx = Mathf.Max(0, Mathf.FloorToInt((float)(startSec * SampleRate)));
            int count = Mathf.FloorToInt((float)(durationSec * SampleRate));
            int endIdx = Mathf.Min(buffer.Length, startIdx + count);
            float dur = (float)durationSec;

            for (int i = startIdx; i < endIdx; i++)
            {
                float t = (i - startIdx) / (float)SampleRate;
                float attack = Mathf.Clamp01(t / 0.04f);
                float release = Mathf.Clamp01((dur - t) / 0.12f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.8f
                           + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f;
                buffer[i] += wave * attack * release * amplitude;
            }
        }
    }
}
