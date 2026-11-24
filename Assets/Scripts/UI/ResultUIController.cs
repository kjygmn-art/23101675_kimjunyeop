using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VRRhythmBeat.Core;

namespace VRRhythmBeat.UI
{
    /// <summary>
    /// 04_Result 씬의 스테이지 결과 표시 및 재도전/곡 선택/로비 복귀 VR UI 컨트롤러.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ResultUIController : MonoBehaviour
    {
        [Header("Result Texts")]
        [SerializeField] private TMP_Text songHeaderText;
        [SerializeField] private TMP_Text gradeText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text accuracyText;
        [SerializeField] private TMP_Text breakdownText;

        [Header("Navigation Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button songSelectButton;
        [SerializeField] private Button lobbyButton;

        private AudioSource _sfxSource;
        private AudioClip _fanfareClip;

        private void Awake()
        {
            _sfxSource = GetComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _fanfareClip = ProceduralAudioFactory.CreateNoteHitChimeSfx(true);

            if (retryButton != null) retryButton.onClick.AddListener(OnClickRetry);
            if (songSelectButton != null) songSelectButton.onClick.AddListener(OnClickSongSelect);
            if (lobbyButton != null) lobbyButton.onClick.AddListener(OnClickLobby);
        }

        private void Start()
        {
            PlayResultData result;
            if (GameSessionManager.HasInstance && GameSessionManager.Instance.HasResult)
            {
                result = GameSessionManager.Instance.LastResult;
            }
            else
            {
                // 결과 씬을 단독 실행해 레이아웃을 확인할 때의 기본 표시값
                result = new PlayResultData
                {
                    songTitle = "Canon in D - VR Arcade",
                    difficultyLabel = "NORMAL",
                    score = 0,
                    maxCombo = 0,
                    perfectCount = 0,
                    goodCount = 0,
                    missCount = 0
                };
            }

            DisplayResult(result);

            if (_sfxSource != null && _fanfareClip != null)
            {
                _sfxSource.PlayOneShot(_fanfareClip, 0.7f);
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.rKey.wasPressedThisFrame) OnClickRetry();
            else if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) OnClickSongSelect();
            else if (kb.escapeKey.wasPressedThisFrame) OnClickLobby();
        }

        private void DisplayResult(PlayResultData result)
        {
            if (songHeaderText != null)
            {
                songHeaderText.text = $"{result.songTitle}  [{result.difficultyLabel}]";
            }

            string grade = result.Grade;
            if (gradeText != null)
            {
                gradeText.text = grade;
                gradeText.color = GetGradeColor(grade);
            }

            if (scoreText != null)
            {
                scoreText.text = $"FINAL SCORE   {result.score:N0}";
            }

            if (accuracyText != null)
            {
                accuracyText.text = $"ACCURACY  {result.AccuracyPercent:0.0}%     |     MAX COMBO  {result.maxCombo}";
            }

            if (breakdownText != null)
            {
                breakdownText.text =
                    $"PERFECT  {result.perfectCount}      |      GOOD  {result.goodCount}      |      MISS  {result.missCount}";
            }
        }

        private static Color GetGradeColor(string grade)
        {
            switch (grade)
            {
                case "S+": return new Color(1.0f, 0.90f, 0.20f);
                case "S":  return new Color(0.25f, 0.92f, 1.0f);
                case "A":  return new Color(0.30f, 1.0f, 0.50f);
                case "B":  return new Color(1.0f, 0.65f, 0.20f);
                default:   return new Color(0.85f, 0.45f, 0.45f);
            }
        }

        public void OnClickRetry()
        {
            GameSessionManager.Instance.StartSelectedSong();
        }

        public void OnClickSongSelect()
        {
            GameSessionManager.Instance.GoToSongSelect();
        }

        public void OnClickLobby()
        {
            GameSessionManager.Instance.GoToLobby();
        }

        public void Configure(
            TMP_Text header,
            TMP_Text grade,
            TMP_Text score,
            TMP_Text accuracy,
            TMP_Text breakdown,
            Button retry,
            Button select,
            Button lobby)
        {
            songHeaderText = header;
            gradeText = grade;
            scoreText = score;
            accuracyText = accuracy;
            breakdownText = breakdown;
            retryButton = retry;
            songSelectButton = select;
            lobbyButton = lobby;
        }
    }
}
