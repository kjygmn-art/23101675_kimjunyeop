using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VRRhythmBeat.Core;

namespace VRRhythmBeat.UI
{
    /// <summary>
    /// 01_Lobby 씬의 VR 로비 UI 컨트롤러.
    /// XRI Ray/NearFar 포인터 클릭 및 에디터 키보드(Enter/Space) 입력을 모두 지원합니다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LobbyUIController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button songSelectButton;
        [SerializeField] private Button quickPlayButton;
        [SerializeField] private Button quitButton;

        [Header("Optional Quick Play Default")]
        [SerializeField] private BeatmapData quickPlayBeatmap;

        private AudioSource _sfxSource;
        private AudioClip _clickClip;

        private void Awake()
        {
            _sfxSource = GetComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _clickClip = ProceduralAudioFactory.CreateNoteHitChimeSfx(true);

            // 세션 매니저 사전 보장
            _ = GameSessionManager.Instance;

            if (songSelectButton != null)
            {
                songSelectButton.onClick.AddListener(OnClickSongSelect);
            }
            if (quickPlayButton != null)
            {
                quickPlayButton.onClick.AddListener(OnClickQuickPlay);
            }
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnClickQuit);
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                OnClickSongSelect();
            }
        }

        public void OnClickSongSelect()
        {
            PlayClickSfx();
            GameSessionManager.Instance.GoToSongSelect();
        }

        public void OnClickQuickPlay()
        {
            PlayClickSfx();
            if (quickPlayBeatmap != null)
            {
                GameSessionManager.Instance.StartSong(quickPlayBeatmap);
            }
            else
            {
                GameSessionManager.Instance.StartSelectedSong();
            }
        }

        public void OnClickQuit()
        {
            PlayClickSfx();
            GameSessionManager.Instance.QuitApplication();
        }

        private void PlayClickSfx()
        {
            if (_sfxSource != null && _clickClip != null)
            {
                _sfxSource.PlayOneShot(_clickClip, 0.7f);
            }
        }

        public void Configure(Button selectBtn, Button quickBtn, Button quitBtn, BeatmapData defaultBeatmap)
        {
            songSelectButton = selectBtn;
            quickPlayButton = quickBtn;
            quitButton = quitBtn;
            quickPlayBeatmap = defaultBeatmap;
        }
    }
}
