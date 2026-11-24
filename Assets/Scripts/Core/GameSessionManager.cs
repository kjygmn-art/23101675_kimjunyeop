using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRRhythmBeat.Core
{
    /// <summary>
    /// 로비 -> 노래 선택 -> 게임 -> 결과 씬 간에 선택된 채보(BeatmapData), 노트 속도,
    /// 그리고 최근 플레이 결과(PlayResultData)를 유지·전달하는 영속 세션 매니저.
    /// </summary>
    public class GameSessionManager : MonoBehaviour
    {
        private static GameSessionManager _instance;

        public static GameSessionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameSessionManager");
                    _instance = go.AddComponent<GameSessionManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        [Header("Current Session State")]
        [SerializeField] private BeatmapData selectedBeatmap;
        [SerializeField] private float selectedNoteSpeed = 8.0f;

        public BeatmapData SelectedBeatmap => selectedBeatmap;
        public float SelectedNoteSpeed => selectedNoteSpeed;
        public PlayResultData LastResult { get; private set; }
        public bool HasResult { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SelectSong(BeatmapData beatmap)
        {
            selectedBeatmap = beatmap;
            if (beatmap != null)
            {
                selectedNoteSpeed = beatmap.recommendedNoteSpeed;
            }
        }

        public void SetNoteSpeed(float speed)
        {
            selectedNoteSpeed = Mathf.Clamp(speed, 4.0f, 16.0f);
        }

        public void GoToLobby()
        {
            SceneManager.LoadScene(SceneNames.Lobby);
        }

        public void GoToSongSelect()
        {
            SceneManager.LoadScene(SceneNames.SongSelect);
        }

        public void StartSelectedSong()
        {
            SceneManager.LoadScene(SceneNames.Game);
        }

        public void StartSong(BeatmapData beatmap)
        {
            SelectSong(beatmap);
            SceneManager.LoadScene(SceneNames.Game);
        }

        public void CompleteStageAndShowResult(PlayResultData result)
        {
            LastResult = result;
            HasResult = true;
            SceneManager.LoadScene(SceneNames.Result);
        }

        public void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
