using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VRRhythmBeat.Core;

namespace VRRhythmBeat.UI
{
    /// <summary>
    /// 02_SongSelect 씬의 노래·난이도·노트 속도 선택 VR UI 컨트롤러.
    /// 선택한 BeatmapData와 속도를 GameSessionManager에 저장한 뒤 03_Game 씬으로 전환합니다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SongSelectUIController : MonoBehaviour
    {
        [Header("Available Charts")]
        [SerializeField] private BeatmapData[] beatmaps;

        [Header("UI Buttons")]
        [SerializeField] private Button[] songCardButtons;
        [SerializeField] private Image[] songCardBackgrounds;
        [SerializeField] private Button speedDownButton;
        [SerializeField] private Button speedUpButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button backButton;

        [Header("Detail Panel Texts")]
        [SerializeField] private TMP_Text selectedTitleText;
        [SerializeField] private TMP_Text selectedDifficultyText;
        [SerializeField] private TMP_Text selectedMetaText;
        [SerializeField] private TMP_Text selectedDescText;
        [SerializeField] private TMP_Text speedValueText;

        [Header("Card Highlight Colors")]
        [SerializeField] private Color normalCardColor = new Color(0.10f, 0.15f, 0.26f, 0.92f);
        [SerializeField] private Color selectedCardColor = new Color(0.12f, 0.45f, 0.78f, 0.98f);

        private int _selectedIndex;
        private float _currentSpeed = 8.0f;
        private AudioSource _sfxSource;
        private AudioClip _selectClip;
        private AudioClip _startClip;

        private void Awake()
        {
            _sfxSource = GetComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _selectClip = ProceduralAudioFactory.CreateNoteHitChimeSfx(false);
            _startClip = ProceduralAudioFactory.CreateNoteHitChimeSfx(true);

            if (songCardButtons != null)
            {
                for (int i = 0; i < songCardButtons.Length; i++)
                {
                    int idx = i;
                    if (songCardButtons[i] != null)
                    {
                        songCardButtons[i].onClick.AddListener(() => SelectIndex(idx, true));
                    }
                }
            }

            if (speedDownButton != null) speedDownButton.onClick.AddListener(() => AdjustSpeed(-0.5f));
            if (speedUpButton != null) speedUpButton.onClick.AddListener(() => AdjustSpeed(+0.5f));
            if (playButton != null) playButton.onClick.AddListener(OnClickPlay);
            if (backButton != null) backButton.onClick.AddListener(OnClickBack);
        }

        private void Start()
        {
            // 직전 선택한 곡이 있으면 그 인덱스를 기본 선택, 없으면 NORMAL(인덱스 1) 선택
            int initialIdx = (beatmaps != null && beatmaps.Length > 1) ? 1 : 0;
            if (GameSessionManager.HasInstance && GameSessionManager.Instance.SelectedBeatmap != null && beatmaps != null)
            {
                for (int i = 0; i < beatmaps.Length; i++)
                {
                    if (beatmaps[i] == GameSessionManager.Instance.SelectedBeatmap)
                    {
                        initialIdx = i;
                        break;
                    }
                }
            }

            SelectIndex(initialIdx, false);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) SelectIndex(0, true);
            else if (kb.digit2Key.wasPressedThisFrame) SelectIndex(1, true);
            else if (kb.digit3Key.wasPressedThisFrame) SelectIndex(2, true);
            else if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) OnClickPlay();
            else if (kb.escapeKey.wasPressedThisFrame) OnClickBack();
        }

        public void SelectIndex(int index, bool playSound)
        {
            if (beatmaps == null || beatmaps.Length == 0) return;
            _selectedIndex = Mathf.Clamp(index, 0, beatmaps.Length - 1);

            BeatmapData data = beatmaps[_selectedIndex];
            if (data != null)
            {
                _currentSpeed = data.recommendedNoteSpeed;
                GameSessionManager.Instance.SelectSong(data);
                RefreshDetailPanel(data);
            }

            RefreshCardHighlights();

            if (playSound && _sfxSource != null && _selectClip != null)
            {
                _sfxSource.PlayOneShot(_selectClip, 0.65f);
            }
        }

        private void AdjustSpeed(float delta)
        {
            _currentSpeed = Mathf.Clamp(_currentSpeed + delta, 5.0f, 14.0f);
            GameSessionManager.Instance.SetNoteSpeed(_currentSpeed);
            RefreshSpeedLabel();

            if (_sfxSource != null && _selectClip != null)
            {
                _sfxSource.PlayOneShot(_selectClip, 0.45f);
            }
        }

        private void RefreshDetailPanel(BeatmapData data)
        {
            if (selectedTitleText != null)
            {
                selectedTitleText.text = data.songTitle;
            }

            if (selectedDifficultyText != null)
            {
                selectedDifficultyText.text = $"DIFFICULTY : {data.difficultyLabel}";
                selectedDifficultyText.color = GetDifficultyColor(data.difficultyLabel);
            }

            if (selectedMetaText != null)
            {
                int noteCount = data.notes != null ? data.notes.Count : 0;
                selectedMetaText.text = $"{data.artist}   |   {data.bpm:0} BPM   |   {noteCount} NOTES";
            }

            if (selectedDescText != null)
            {
                selectedDescText.text = data.description;
            }

            RefreshSpeedLabel();
        }

        private void RefreshSpeedLabel()
        {
            if (speedValueText != null)
            {
                speedValueText.text = $"NOTE SPEED : {_currentSpeed:0.0} m/s";
            }
        }

        private void RefreshCardHighlights()
        {
            if (songCardBackgrounds == null) return;
            for (int i = 0; i < songCardBackgrounds.Length; i++)
            {
                if (songCardBackgrounds[i] != null)
                {
                    songCardBackgrounds[i].color = (i == _selectedIndex) ? selectedCardColor : normalCardColor;
                }
            }
        }

        private static Color GetDifficultyColor(string diff)
        {
            if (string.IsNullOrEmpty(diff)) return Color.white;
            string upper = diff.ToUpperInvariant();
            if (upper.Contains("EASY")) return new Color(0.30f, 1.0f, 0.55f);
            if (upper.Contains("HARD")) return new Color(1.0f, 0.35f, 0.30f);
            return new Color(0.25f, 0.85f, 1.0f);
        }

        public void OnClickPlay()
        {
            if (_sfxSource != null && _startClip != null)
            {
                _sfxSource.PlayOneShot(_startClip, 0.75f);
            }

            if (beatmaps != null && _selectedIndex >= 0 && _selectedIndex < beatmaps.Length && beatmaps[_selectedIndex] != null)
            {
                GameSessionManager.Instance.SelectSong(beatmaps[_selectedIndex]);
                GameSessionManager.Instance.SetNoteSpeed(_currentSpeed);
            }

            GameSessionManager.Instance.StartSelectedSong();
        }

        public void OnClickBack()
        {
            GameSessionManager.Instance.GoToLobby();
        }

        public void Configure(
            BeatmapData[] charts,
            Button[] cardButtons,
            Image[] cardBgs,
            Button speedDown,
            Button speedUp,
            Button playBtn,
            Button backBtn,
            TMP_Text titleText,
            TMP_Text diffText,
            TMP_Text metaText,
            TMP_Text descText,
            TMP_Text speedText)
        {
            beatmaps = charts;
            songCardButtons = cardButtons;
            songCardBackgrounds = cardBgs;
            speedDownButton = speedDown;
            speedUpButton = speedUp;
            playButton = playBtn;
            backButton = backBtn;
            selectedTitleText = titleText;
            selectedDifficultyText = diffText;
            selectedMetaText = metaText;
            selectedDescText = descText;
            speedValueText = speedText;
        }
    }
}
