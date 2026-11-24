using System;
using UnityEngine;
using VRRhythmBeat.Gameplay;

namespace VRRhythmBeat.Core
{
    /// <summary>
    /// AudioSettings.dspTime 기반 절대 시간 동기화 리듬 엔진(Conductor).
    /// 프레임 드랍 시에도 음악과 노드의 씽크가 밀리지 않도록 dspTime으로 스폰 및 위치를 통제합니다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RhythmConductor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private BeatmapData beatmap;
        [SerializeField] private NotePooler notePooler;
        [SerializeField] private JudgmentManager judgmentManager;
        [SerializeField] private Transform[] laneParents = new Transform[6];

        [Header("Track Geometry (Local Z of Each Lane)")]
        [Tooltip("노드가 소실점(먼 끝)에서 나타나는 Local Z 좌표")]
        [SerializeField] private float spawnLocalZ = 18.0f;

        [Tooltip("주황색 타격 버튼(HitPad)이 위치한 Local Z 좌표")]
        [SerializeField] private float hitButtonLocalZ = 0.85f;

        [Tooltip("레일 표면 위로 노드가 떠서 달리는 Local Y 오프셋")]
        [SerializeField] private float noteLocalY = 0.14f;

        [Tooltip("노드가 플레이어를 향해 다가오는 속도 (m/s)")]
        [SerializeField] private float noteSpeed = 8.0f;

        [Header("Playback Settings")]
        [SerializeField] private bool autoStartOnPlay = true;
        [Tooltip("PlayScheduled 호출 전 오디오 스레드 준비 및 선행 스폰 여유 시간(초)")]
        [SerializeField] private double startDelaySeconds = 1.5;
        [Tooltip("곡이 끝난 뒤 결과 씬(04_Result)으로 자동 전환되기 전 대기 시간(초)")]
        [SerializeField] private float resultTransitionDelay = 1.8f;
        [SerializeField] private bool autoLoadResultSceneOnFinish = true;

        /// <summary>현재 곡의 절대 재생 위치(초, dspTime 기준).</summary>
        public double SongPosition { get; private set; }

        /// <summary>노드가 지평선(스폰 끝)에서 타격 버튼까지 도달하는 데 걸리는 시간(초).</summary>
        public double ApproachTime => GetTrackTravelDistance() / Mathf.Max(0.1f, noteSpeed);

        private float GetTrackTravelDistance()
        {
            if (laneParents != null)
            {
                for (int i = 0; i < laneParents.Length; i++)
                {
                    if (laneParents[i] == null) continue;
                    CurvedLaneTrack curved = laneParents[i].GetComponent<CurvedLaneTrack>();
                    if (curved != null && curved.TrackLength > 0.5f)
                    {
                        return curved.TrackLength;
                    }
                }
            }
            return Mathf.Max(1f, spawnLocalZ - hitButtonLocalZ);
        }

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public BeatmapData Beatmap => beatmap;

        public event Action OnSongStarted;
        public event Action OnSongFinished;

        private double _dspSongStartTime;
        private double _pauseStartDspTime;
        private double _totalPausedDuration;
        private int _nextSpawnIndex;
        private double _songDuration;
        private bool _waitingForResultTransition;
        private float _resultTransitionTimer;

        private void Awake()
        {
            if (bgmSource == null)
            {
                bgmSource = GetComponent<AudioSource>();
            }

            bgmSource.playOnAwake = false;
            bgmSource.loop = false;
            bgmSource.spatialBlend = 0f; // BGM은 거리 감쇠 없는 선명한 2D 스테레오로 출력
            bgmSource.volume = 0.95f;
            bgmSource.mute = false;
            bgmSource.bypassEffects = true;
            bgmSource.bypassListenerEffects = true;
            bgmSource.bypassReverbZones = true;

            // 씬에 AudioListener가 없으면 메인 카메라에 자동 추가
            if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }
        }

        private void Start()
        {
            if (autoStartOnPlay)
            {
                StartSong();
            }
        }

        /// <summary>
        /// AudioSettings.dspTime + PlayScheduled로 음악과 노트 스폰을 밀리초 단위로 동기화 시작합니다.
        /// </summary>
        public void StartSong()
        {
            EnsureBeatmapAndAudioReady();

            if (beatmap == null || beatmap.notes == null || beatmap.notes.Count == 0)
            {
                Debug.LogError("[RhythmConductor] 재생할 채보(BeatmapData)가 비어 있습니다.");
                return;
            }

            beatmap.SortNotesByTime();

            // 기존 진행 중이던 노트/점수 초기화
            if (judgmentManager != null)
            {
                judgmentManager.ResetState();
            }

            _nextSpawnIndex = 0;
            _totalPausedDuration = 0.0;
            IsPaused = false;
            _waitingForResultTransition = false;
            _resultTransitionTimer = 0f;

            // 노드가 소실점에서 버튼까지 이동할 시간(ApproachTime)이 첫 노트보다 길어도 자연스럽게 스폰되도록 여유 확보
            double firstNoteTime = beatmap.notes[0].targetTime;
            double requiredLeadIn = Math.Max(startDelaySeconds, ApproachTime - firstNoteTime + 0.25);

            _dspSongStartTime = AudioSettings.dspTime + requiredLeadIn;
            _songDuration = bgmSource.clip != null ? bgmSource.clip.length : (beatmap.notes[beatmap.notes.Count - 1].targetTime + 3.0);

            // preloadAudioData가 꺼져 있어 PlayScheduled가 무음으로 실패하는 현상을 원천 차단
            if (bgmSource.clip != null && bgmSource.clip.loadState != AudioDataLoadState.Loaded)
            {
                bgmSource.clip.LoadAudioData();
            }

            bgmSource.Stop();
            bgmSource.spatialBlend = 0f;
            bgmSource.volume = 0.95f;
            bgmSource.mute = false;
            bgmSource.PlayScheduled(_dspSongStartTime);

            SongPosition = AudioSettings.dspTime - _dspSongStartTime - beatmap.audioOffsetSeconds;
            IsPlaying = true;
            OnSongStarted?.Invoke();
        }

        public void PauseSong()
        {
            if (!IsPlaying || IsPaused) return;
            IsPaused = true;
            _pauseStartDspTime = AudioSettings.dspTime;
            bgmSource.Pause();
        }

        public void ResumeSong()
        {
            if (!IsPlaying || !IsPaused) return;
            _totalPausedDuration += AudioSettings.dspTime - _pauseStartDspTime;
            IsPaused = false;
            bgmSource.UnPause();
        }

        public void RestartSong()
        {
            StartSong();
        }

        private void Update()
        {
            if (_waitingForResultTransition)
            {
                _resultTransitionTimer -= Time.deltaTime;
                if (_resultTransitionTimer <= 0f)
                {
                    _waitingForResultTransition = false;
                    TransitionToResultScene();
                }
                return;
            }

            if (!IsPlaying || IsPaused) return;

            // 핵심: Time.deltaTime 대신 AudioSettings.dspTime으로 절대 위치 산출
            SongPosition = AudioSettings.dspTime - _dspSongStartTime - _totalPausedDuration - beatmap.audioOffsetSeconds;

            // AudioClip 비동기 로딩 지연으로 PlayScheduled 시점을 놓쳤을 때의 자동 복구 안전장치
            if (bgmSource != null && bgmSource.clip != null && !bgmSource.isPlaying &&
                SongPosition >= 0.05 && SongPosition < _songDuration - 0.5)
            {
                if (bgmSource.clip.loadState == AudioDataLoadState.Unloaded)
                {
                    bgmSource.clip.LoadAudioData();
                }
                if (bgmSource.clip.loadState == AudioDataLoadState.Loaded)
                {
                    bgmSource.time = Mathf.Clamp((float)SongPosition, 0f, bgmSource.clip.length - 0.05f);
                    bgmSource.Play();
                }
            }

            SpawnDueNotes();

            if (judgmentManager != null)
            {
                judgmentManager.UpdateActiveNotes(SongPosition);
            }

            // 모든 노트가 처리되고 곡 길이가 지나면 종료 이벤트 발생 및 결과 씬 전환 예약
            bool allSpawned = _nextSpawnIndex >= beatmap.notes.Count;
            bool noActiveNotes = judgmentManager == null || judgmentManager.ActiveNoteCount == 0;
            if (allSpawned && noActiveNotes && SongPosition >= _songDuration + 0.5)
            {
                IsPlaying = false;
                OnSongFinished?.Invoke();

                if (autoLoadResultSceneOnFinish)
                {
                    _waitingForResultTransition = true;
                    _resultTransitionTimer = resultTransitionDelay;
                }
            }
        }

        private void TransitionToResultScene()
        {
            PlayResultData result = new PlayResultData
            {
                songTitle = beatmap != null ? beatmap.songTitle : "Canon in D",
                difficultyLabel = beatmap != null ? beatmap.difficultyLabel : "NORMAL",
                score = judgmentManager != null ? judgmentManager.Score : 0,
                maxCombo = judgmentManager != null ? judgmentManager.MaxCombo : 0,
                perfectCount = judgmentManager != null ? judgmentManager.PerfectCount : 0,
                goodCount = judgmentManager != null ? judgmentManager.GoodCount : 0,
                missCount = judgmentManager != null ? judgmentManager.MissCount : 0
            };

            GameSessionManager.Instance.CompleteStageAndShowResult(result);
        }

        private void SpawnDueNotes()
        {
            if (notePooler == null || judgmentManager == null) return;

            double approach = ApproachTime;

            while (_nextSpawnIndex < beatmap.notes.Count)
            {
                NoteData noteData = beatmap.notes[_nextSpawnIndex];
                double spawnTriggerTime = noteData.targetTime - approach;

                if (SongPosition < spawnTriggerTime)
                {
                    break;
                }

                SpawnSingleNote(noteData);
                _nextSpawnIndex++;
            }
        }

        private void SpawnSingleNote(NoteData noteData)
        {
            int lane = noteData.laneIndex;
            if (lane < 0 || lane >= laneParents.Length || laneParents[lane] == null)
            {
                Debug.LogWarning($"[RhythmConductor] 유효하지 않은 레일 인덱스({lane})입니다.");
                return;
            }

            NoteController note = notePooler.GetNote();
            if (note == null) return;

            note.Initialize(
                noteData,
                laneParents[lane],
                hitButtonLocalZ,
                noteLocalY,
                noteSpeed,
                SongPosition);

            judgmentManager.RegisterActiveNote(lane, note);
        }

        private void EnsureBeatmapAndAudioReady()
        {
            // 노래 선택 씬(02_SongSelect)에서 고른 채보와 속도가 있으면 우선 적용
            if (GameSessionManager.HasInstance && GameSessionManager.Instance.SelectedBeatmap != null)
            {
                beatmap = GameSessionManager.Instance.SelectedBeatmap;
                noteSpeed = GameSessionManager.Instance.SelectedNoteSpeed;
                bgmSource.clip = null;
            }

            if (beatmap == null)
            {
                beatmap = ScriptableObject.CreateInstance<BeatmapData>();
                beatmap.PopulateCanonInDSample();
            }
            else if (beatmap.notes == null || beatmap.notes.Count == 0)
            {
                beatmap.PopulateCanonInDSample();
            }

            if (beatmap.audioClip != null)
            {
                bgmSource.clip = beatmap.audioClip;
            }
            else if (bgmSource.clip == null)
            {
                bgmSource.clip = ProceduralAudioFactory.CreateCanonInDBgm(beatmap.bpm);
            }
        }

        /// <summary>
        /// 에디터 셋업 도구가 씬의 레일/매니저 참조를 일괄 연결할 때 사용합니다.
        /// </summary>
        public void ConfigureReferences(
            BeatmapData beatmapAsset,
            NotePooler pooler,
            JudgmentManager manager,
            Transform[] lanes,
            float spawnZ,
            float hitZ,
            float localY,
            float speed)
        {
            beatmap = beatmapAsset;
            notePooler = pooler;
            judgmentManager = manager;
            laneParents = lanes;
            spawnLocalZ = spawnZ;
            hitButtonLocalZ = hitZ;
            noteLocalY = localY;
            noteSpeed = speed;
        }
    }
}
