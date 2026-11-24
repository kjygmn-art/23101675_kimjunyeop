using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRRhythmBeat.Core;
using VRRhythmBeat.Gameplay;

namespace VRRhythmBeat.UI
{
    /// <summary>
    /// VR U자형 터널 전방에 띄워 점수, 콤보, 실시간 판정(PERFECT / GOOD / MISS)과
    /// 재시작/곡 선택 복귀 버튼을 제공하는 World-Space HUD.
    /// </summary>
    public class RhythmHudUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private JudgmentManager judgmentManager;
        [SerializeField] private RhythmConductor conductor;

        [Header("Text Elements")]
        [SerializeField] private TMP_Text songTitleText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private TMP_Text judgmentText;
        [SerializeField] private TMP_Text statsText;

        [Header("Optional Action Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button exitToSongSelectButton;

        [Header("Judgment Popup Animation")]
        [SerializeField] private float popupDuration = 0.45f;
        [SerializeField] private Color perfectColor = new Color(0.2f, 0.85f, 1f);
        [SerializeField] private Color goodColor = new Color(0.3f, 1f, 0.45f);
        [SerializeField] private Color missColor = new Color(1f, 0.3f, 0.25f);

        private float _popupTimer;

        private void OnEnable()
        {
            if (judgmentManager != null)
            {
                judgmentManager.OnJudgment += HandleJudgment;
                judgmentManager.OnScoreUpdated += HandleScoreUpdated;
            }

            if (conductor != null)
            {
                conductor.OnSongStarted += HandleSongStarted;
                conductor.OnSongFinished += HandleSongFinished;
            }
        }

        private void OnDisable()
        {
            if (judgmentManager != null)
            {
                judgmentManager.OnJudgment -= HandleJudgment;
                judgmentManager.OnScoreUpdated -= HandleScoreUpdated;
            }

            if (conductor != null)
            {
                conductor.OnSongStarted -= HandleSongStarted;
                conductor.OnSongFinished -= HandleSongFinished;
            }
        }

        private void Awake()
        {
            if (retryButton != null)
            {
                retryButton.onClick.AddListener(() =>
                {
                    if (conductor != null) conductor.RestartSong();
                });
            }

            if (exitToSongSelectButton != null)
            {
                exitToSongSelectButton.onClick.AddListener(() =>
                {
                    GameSessionManager.Instance.GoToSongSelect();
                });
            }
        }

        private void Start()
        {
            RefreshStaticLabels();
            HandleScoreUpdated(0, 0, 0);
            if (judgmentText != null)
            {
                judgmentText.text = "READY";
                judgmentText.color = Color.white;
            }
        }

        private void Update()
        {
            if (_popupTimer > 0f && judgmentText != null)
            {
                _popupTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_popupTimer / Mathf.Max(0.01f, popupDuration));
                float scale = Mathf.Lerp(1.0f, 1.25f, t);
                judgmentText.transform.localScale = Vector3.one * scale;
            }
        }

        private void HandleSongStarted()
        {
            RefreshStaticLabels();
            if (judgmentText != null)
            {
                judgmentText.text = "GO!";
                judgmentText.color = perfectColor;
                _popupTimer = popupDuration;
            }
        }

        private void HandleSongFinished()
        {
            if (judgmentText != null)
            {
                judgmentText.text = "STAGE CLEAR!";
                judgmentText.color = new Color(1f, 0.85f, 0.25f);
                judgmentText.transform.localScale = Vector3.one * 1.15f;
                _popupTimer = 0f;
            }
        }

        private void HandleJudgment(JudgmentType type, int laneIndex, float error)
        {
            if (judgmentText == null) return;

            switch (type)
            {
                case JudgmentType.Perfect:
                    judgmentText.text = "PERFECT";
                    judgmentText.color = perfectColor;
                    break;
                case JudgmentType.Good:
                    judgmentText.text = "GOOD";
                    judgmentText.color = goodColor;
                    break;
                case JudgmentType.Miss:
                    judgmentText.text = "MISS";
                    judgmentText.color = missColor;
                    break;
            }

            _popupTimer = popupDuration;
            UpdateStatsText();
        }

        private void HandleScoreUpdated(int score, int combo, int maxCombo)
        {
            if (scoreText != null)
            {
                scoreText.text = $"SCORE  {score:N0}";
            }

            if (comboText != null)
            {
                comboText.text = combo > 0 ? $"{combo} COMBO" : "";
            }

            UpdateStatsText();
        }

        private void UpdateStatsText()
        {
            if (statsText == null || judgmentManager == null) return;
            statsText.text =
                $"PERFECT {judgmentManager.PerfectCount}   |   GOOD {judgmentManager.GoodCount}   |   MISS {judgmentManager.MissCount}   |   MAX {judgmentManager.MaxCombo}";
        }

        private void RefreshStaticLabels()
        {
            if (songTitleText != null && conductor != null && conductor.Beatmap != null)
            {
                songTitleText.text = conductor.Beatmap.songTitle;
            }
        }

        public void Configure(
            JudgmentManager manager,
            RhythmConductor rhythmConductor,
            TMP_Text title,
            TMP_Text score,
            TMP_Text combo,
            TMP_Text judgment,
            TMP_Text stats,
            Button retryBtn = null,
            Button exitBtn = null)
        {
            judgmentManager = manager;
            conductor = rhythmConductor;
            songTitleText = title;
            scoreText = score;
            comboText = combo;
            judgmentText = judgment;
            statsText = stats;
            retryButton = retryBtn;
            exitToSongSelectButton = exitBtn;
        }
    }
}
