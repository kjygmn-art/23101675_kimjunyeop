using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRRhythmBeat.Core;
using VRRhythmBeat.Gameplay;
using VRRhythmBeat.Interaction;
using VRRhythmBeat.UI;

namespace VRRhythmBeat.EditorTools
{
    /// <summary>
    /// [VR Rhythm Beat - Canon] 4개 정식 씬(로비 / 노래 선택 / 게임 / 결과),
    /// 3개 난이도 채보 에셋, URP 머티리얼, 오브젝트 풀 프리팹, Build Settings를
    /// 자동으로 생성·연결하는 에디터 빌더.
    ///
    /// [InitializeOnLoad]를 통해 스크립트 컴파일 완료 시 4개 씬이 아직 없으면
    /// 별도 메뉴 클릭 없이 자동으로 생성하며, 01_Lobby 씬을 열어 즉시 실행 가능한 상태로 만듭니다.
    /// </summary>
    [InitializeOnLoad]
    public static class VRRhythmSceneBuilder
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string MaterialsDir = "Assets/Materials";
        private const string PrefabsDir = "Assets/Prefabs";
        private const string BeatmapsDir = "Assets/Beatmaps";
        private const string MeshesDir = "Assets/Meshes";

        private const string LobbyScenePath = ScenesDir + "/01_Lobby.unity";
        private const string SongSelectScenePath = ScenesDir + "/02_SongSelect.unity";
        private const string GameScenePath = ScenesDir + "/03_Game.unity";
        private const string ResultScenePath = ScenesDir + "/04_Result.unity";

        private const string XROriginPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        private const string LayoutVersionPrefKey = "VRRhythmBeat_LayoutVersion";
        private const string CurrentLayoutVersion = "v5_curved_cockpit_arch";
        private const string FadeAudioAssetPath = "Assets/Audio/NCS_Fade.wav";

        private const float SpawnLocalZ = 16.0f;
        private const float HitButtonRadius = 0.82f;
        private const float NoteHeightOffset = 0.11f;
        private const float DefaultNoteSpeed = 8.0f;
        private const float RailWidth = 0.27f;

        private struct CurvedLaneSpec
        {
            public string Name;
            public float YawDeg;   // 플레이어 정면(+Z) 기준 수평 반원 감싸기 각도 (-55° ~ +55°)
            public float HitY;     // 타격 버튼 높이 (허리~가슴 편안한 드럼 높이, 외곽 살짝 상승)
            public float RollDeg;  // 외곽 날개 살짝 안쪽 기울기
            public float SpawnX;   // 먼 정면 지평선에서 6레일이 한눈에 모이는 X 좌표
        }

        // 스케치 기반: 플레이어의 몸을 수평 반원형 아치(파노라마 콕핏)로 감싸고,
        // 먼 정면 지평선에서 부드럽게 곡선으로 휘어져 양손 앞으로 흘러들어오는 6레일 (좌 -> 우: 0~5)
        private static readonly CurvedLaneSpec[] LaneSpecs =
        {
            new CurvedLaneSpec { Name = "Lane_0_FarLeft",     YawDeg = -55f, HitY = 1.04f, RollDeg = -14f, SpawnX = -1.55f },
            new CurvedLaneSpec { Name = "Lane_1_MidLeft",     YawDeg = -33f, HitY = 1.00f, RollDeg =  -8f, SpawnX = -0.93f },
            new CurvedLaneSpec { Name = "Lane_2_CenterLeft",  YawDeg = -11f, HitY = 0.97f, RollDeg =  -3f, SpawnX = -0.31f },
            new CurvedLaneSpec { Name = "Lane_3_CenterRight", YawDeg =  11f, HitY = 0.97f, RollDeg =   3f, SpawnX =  0.31f },
            new CurvedLaneSpec { Name = "Lane_4_MidRight",    YawDeg =  33f, HitY = 1.00f, RollDeg =   8f, SpawnX =  0.93f },
            new CurvedLaneSpec { Name = "Lane_5_FarRight",    YawDeg =  55f, HitY = 1.04f, RollDeg =  14f, SpawnX =  1.55f },
        };

        private static bool _autoBuildScheduled;

        static VRRhythmSceneBuilder()
        {
            if (_autoBuildScheduled) return;
            _autoBuildScheduled = true;
            EditorApplication.delayCall += AutoBuildOrUpgradeScenes;
        }

        private static void AutoBuildOrUpgradeScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
            {
                return;
            }

            bool allScenesExist =
                File.Exists(LobbyScenePath) &&
                File.Exists(SongSelectScenePath) &&
                File.Exists(GameScenePath) &&
                File.Exists(ResultScenePath);

            if (!allScenesExist)
            {
                BuildAllScenesAndAssets();
                EditorPrefs.SetString(LayoutVersionPrefKey, CurrentLayoutVersion);
                return;
            }

            // 기존에 생성된 씬들이 구버전이면(레일 각도 변경 또는 NCS_Fade 실음원 채보 반영) 자동으로 전체 업그레이드
            if (EditorPrefs.GetString(LayoutVersionPrefKey, "") != CurrentLayoutVersion)
            {
                string previousScenePath = EditorSceneManager.GetActiveScene().path;
                BuildAllScenesAndAssets();
                EditorPrefs.SetString(LayoutVersionPrefKey, CurrentLayoutVersion);

                if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousScenePath))
                {
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                }

                Debug.Log("[VR Rhythm Beat] NCS_Fade.wav 실음원(90 BPM) 연동 및 Easy/Normal/Hard 채보·4개 씬 자동 업데이트 완료!");
            }
        }

        /// <summary>
        /// 03_Game 씬과 NoteCube 프리팹만 최신 레일 스펙으로 깔끔하게 재생성합니다.
        /// </summary>
        public static void RebuildGameSceneOnly()
        {
            EnsureDirectories();
            EnsurePhysicsLayersAndMatrix();

            Material floorMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_ArenaFloor.mat", new Color(0.05f, 0.06f, 0.11f), new Color(0.01f, 0.02f, 0.05f), 0.8f, 0.25f);
            Material trackMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_LaneTrack.mat", new Color(0.08f, 0.10f, 0.16f), new Color(0.02f, 0.05f, 0.12f), 0.75f, 0.4f);
            Material edgeMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_LaneEdge.mat", new Color(0.10f, 0.65f, 1.0f), new Color(0.15f, 0.80f, 1.0f) * 1.8f, 0.5f, 0.2f);
            Material hitPadMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_HitPadOrange.mat", new Color(1.0f, 0.45f, 0.05f), new Color(1.0f, 0.42f, 0.04f) * 1.2f, 0.65f, 0.15f);
            Material noteBlueMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_NoteBlue.mat", new Color(0.10f, 0.55f, 1.0f), new Color(0.15f, 0.70f, 1.0f) * 1.9f, 0.8f, 0.2f);
            Material stickHandleMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_StickHandle.mat", new Color(0.12f, 0.13f, 0.16f), Color.black, 0.4f, 0.5f);
            Material stickRodMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_StickRod.mat", new Color(0.90f, 0.96f, 1.0f), new Color(0.35f, 0.85f, 1.0f) * 1.6f, 0.85f, 0.1f);
            Material vfxPerfectMat = CreateOrUpdateParticleMaterial($"{MaterialsDir}/Mat_VfxPerfect.mat", new Color(0.18f, 0.82f, 1.0f));
            Material vfxGoodMat = CreateOrUpdateParticleMaterial($"{MaterialsDir}/Mat_VfxGood.mat", new Color(0.25f, 1.0f, 0.45f));

            NoteController notePrefab = CreateNoteCubePrefab(noteBlueMat);
            StickController stickPrefab = CreateStickPrefab(stickHandleMat, stickRodMat);
            ParticleSystem perfectVfxPrefab = CreateHitVfxPrefab($"{PrefabsDir}/HitVfxPerfect.prefab", vfxPerfectMat, new Color(0.18f, 0.82f, 1.0f));
            ParticleSystem goodVfxPrefab = CreateHitVfxPrefab($"{PrefabsDir}/HitVfxGood.prefab", vfxGoodMat, new Color(0.25f, 1.0f, 0.45f));
            AudioClip fadeClip = LoadFadeAudioClip();
            BeatmapData fadeNormal = CreateOrUpdateBeatmap($"{BeatmapsDir}/Fade_Normal.asset", b => b.PopulateFadeNormal(fadeClip));

            BuildGameScene(floorMat, trackMat, edgeMat, hitPadMat, notePrefab, stickPrefab, perfectVfxPrefab, goodVfxPrefab, fadeNormal);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 4개 신규 씬(01_Lobby, 02_SongSelect, 03_Game, 04_Result) 및 공용 에셋 일괄 생성.
        /// (추후 재생성이 필요할 경우를 위한 보조 메뉴도 유지합니다.)
        /// </summary>
        [MenuItem("VR Rhythm Beat/Rebuild All 4 Scenes (Lobby, SongSelect, Game, Result)", false, 10)]
        public static void BuildAllScenesAndAssets()
        {
            EnsureDirectories();
            EnsurePhysicsLayersAndMatrix();

            // 1. 공용 URP 머티리얼 생성
            Material floorMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_ArenaFloor.mat", new Color(0.05f, 0.06f, 0.11f), new Color(0.01f, 0.02f, 0.05f), 0.8f, 0.25f);
            Material trackMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_LaneTrack.mat", new Color(0.08f, 0.10f, 0.16f), new Color(0.02f, 0.05f, 0.12f), 0.75f, 0.4f);
            Material edgeMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_LaneEdge.mat", new Color(0.10f, 0.65f, 1.0f), new Color(0.15f, 0.80f, 1.0f) * 1.8f, 0.5f, 0.2f);
            Material accentOrangeMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_AccentOrange.mat", new Color(1.0f, 0.48f, 0.08f), new Color(1.0f, 0.45f, 0.05f) * 1.5f, 0.65f, 0.15f);
            Material hitPadMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_HitPadOrange.mat", new Color(1.0f, 0.45f, 0.05f), new Color(1.0f, 0.42f, 0.04f) * 1.2f, 0.65f, 0.15f);
            Material noteBlueMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_NoteBlue.mat", new Color(0.10f, 0.55f, 1.0f), new Color(0.15f, 0.70f, 1.0f) * 1.9f, 0.8f, 0.2f);
            Material stickHandleMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_StickHandle.mat", new Color(0.12f, 0.13f, 0.16f), Color.black, 0.4f, 0.5f);
            Material stickRodMat = CreateOrUpdateLitMaterial($"{MaterialsDir}/Mat_StickRod.mat", new Color(0.90f, 0.96f, 1.0f), new Color(0.35f, 0.85f, 1.0f) * 1.6f, 0.85f, 0.1f);
            Material vfxPerfectMat = CreateOrUpdateParticleMaterial($"{MaterialsDir}/Mat_VfxPerfect.mat", new Color(0.18f, 0.82f, 1.0f));
            Material vfxGoodMat = CreateOrUpdateParticleMaterial($"{MaterialsDir}/Mat_VfxGood.mat", new Color(0.25f, 1.0f, 0.45f));

            // 2. 공용 프리팹 생성
            NoteController notePrefab = CreateNoteCubePrefab(noteBlueMat);
            StickController stickPrefab = CreateStickPrefab(stickHandleMat, stickRodMat);
            ParticleSystem perfectVfxPrefab = CreateHitVfxPrefab($"{PrefabsDir}/HitVfxPerfect.prefab", vfxPerfectMat, new Color(0.18f, 0.82f, 1.0f));
            ParticleSystem goodVfxPrefab = CreateHitVfxPrefab($"{PrefabsDir}/HitVfxGood.prefab", vfxGoodMat, new Color(0.25f, 1.0f, 0.45f));

            // 3. 실제 추가된 NCS_Fade.wav 오디오 로드 및 3단계 난이도 채보 에셋 생성/갱신
            AudioClip fadeClip = LoadFadeAudioClip();
            BeatmapData fadeEasy = CreateOrUpdateBeatmap($"{BeatmapsDir}/Fade_Easy.asset", b => b.PopulateFadeEasy(fadeClip));
            BeatmapData fadeNormal = CreateOrUpdateBeatmap($"{BeatmapsDir}/Fade_Normal.asset", b => b.PopulateFadeNormal(fadeClip));
            BeatmapData fadeHard = CreateOrUpdateBeatmap($"{BeatmapsDir}/Fade_Hard.asset", b => b.PopulateFadeHard(fadeClip));
            BeatmapData[] allCharts = { fadeEasy, fadeNormal, fadeHard };

            AssetDatabase.SaveAssets();

            // 4. 4개 정식 씬 순차 생성 및 저장
            BuildLobbyScene(floorMat, edgeMat, accentOrangeMat, fadeNormal);
            BuildSongSelectScene(floorMat, edgeMat, accentOrangeMat, allCharts);
            BuildGameScene(floorMat, trackMat, edgeMat, hitPadMat, notePrefab, stickPrefab, perfectVfxPrefab, goodVfxPrefab, fadeNormal);
            BuildResultScene(floorMat, edgeMat, accentOrangeMat);

            // 5. Build Settings에 4개 씬 순서대로 등록 (0: Lobby, 1: SongSelect, 2: Game, 3: Result)
            RegisterBuildSettings();
            EditorPrefs.SetString(LayoutVersionPrefKey, CurrentLayoutVersion);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 6. 로비 씬을 열어 즉시 Play 가능하도록 준비
            EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            Debug.Log("[VR Rhythm Beat] NCS_Fade.wav 기반 4개 씬(01_Lobby, 02_SongSelect, 03_Game, 04_Result) 및 3난이도 채보 구축 완료!");
        }

        // =========================================================================
        // Scene 1: 01_Lobby.unity
        // =========================================================================
        private static void BuildLobbyScene(Material floorMat, Material edgeMat, Material accentOrangeMat, BeatmapData quickPlayBeatmap)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupCommonArenaEnvironment(floorMat, edgeMat, accentOrangeMat, includeDecorativeArch: true);

            Camera xrCam = SetupXRRigAndUIInfra(attachSticks: false, stickPrefab: null);

            // 영속 세션 매니저 배치
            GameObject sessionObj = new GameObject("GameSessionManager");
            sessionObj.AddComponent<GameSessionManager>();

            // World-Space VR 로비 캔버스
            Canvas canvas = CreateVRWorldCanvas("LobbyCanvas", new Vector3(0f, 1.55f, 2.55f), new Vector2(1060f, 680f), 0.0022f, xrCam);
            CreatePanelBackground(canvas.transform, new Color(0.04f, 0.07f, 0.15f, 0.88f));

            CreateTmpText(canvas.transform, "SubHeader", "6-LANE CURVED ARC VR RHYTHM", new Vector2(0, 255), new Vector2(940, 50), 26, new Color(0.30f, 0.88f, 1.0f));
            CreateTmpText(canvas.transform, "MainTitle", "VR RHYTHM BEAT", new Vector2(0, 180), new Vector2(960, 100), 74, Color.white);
            CreateTmpText(canvas.transform, "EditionTag", "— ALAN WALKER : FADE [NCS] —", new Vector2(0, 110), new Vector2(780, 50), 32, new Color(1.0f, 0.58f, 0.15f));

            Button songSelectBtn = CreateVRButton(canvas.transform, "SongSelectButton", "SONG SELECT", new Vector2(0, -10), new Vector2(460, 84), new Color(0.10f, 0.52f, 0.88f, 0.96f), 34);
            Button quickPlayBtn = CreateVRButton(canvas.transform, "QuickPlayButton", "QUICK PLAY  (FADE NORMAL)", new Vector2(0, -115), new Vector2(460, 76), new Color(0.92f, 0.42f, 0.06f, 0.96f), 28);
            Button quitBtn = CreateVRButton(canvas.transform, "QuitButton", "QUIT", new Vector2(0, -215), new Vector2(300, 62), new Color(0.24f, 0.27f, 0.35f, 0.92f), 26);

            CreateTmpText(canvas.transform, "FooterHint", "VR 컨트롤러 레이저 포인터로 버튼을 선택하세요  |  PC 테스트: Enter", new Vector2(0, -295), new Vector2(960, 42), 20, new Color(0.72f, 0.80f, 0.90f));

            LobbyUIController controller = canvas.gameObject.AddComponent<LobbyUIController>();
            controller.Configure(songSelectBtn, quickPlayBtn, quitBtn, quickPlayBeatmap);

            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        // =========================================================================
        // Scene 2: 02_SongSelect.unity
        // =========================================================================
        private static void BuildSongSelectScene(Material floorMat, Material edgeMat, Material accentOrangeMat, BeatmapData[] charts)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupCommonArenaEnvironment(floorMat, edgeMat, accentOrangeMat, includeDecorativeArch: true);

            Camera xrCam = SetupXRRigAndUIInfra(attachSticks: false, stickPrefab: null);

            Canvas canvas = CreateVRWorldCanvas("SongSelectCanvas", new Vector3(0f, 1.55f, 2.55f), new Vector2(1280f, 760f), 0.0020f, xrCam);
            CreatePanelBackground(canvas.transform, new Color(0.04f, 0.07f, 0.15f, 0.90f));

            CreateTmpText(canvas.transform, "HeaderTitle", "SELECT SONG & DIFFICULTY", new Vector2(0, 315), new Vector2(1100, 70), 46, Color.white);

            // 좌측: 3개 난이도 곡 카드 버튼
            Button[] cardButtons = new Button[charts.Length];
            Image[] cardBgs = new Image[charts.Length];

            for (int i = 0; i < charts.Length; i++)
            {
                BeatmapData c = charts[i];
                float yPos = 155f - i * 145f;
                string label = $"{c.songTitle}\n<size=22>[{c.difficultyLabel}]   {c.bpm:0} BPM   |   {c.notes.Count} Notes</size>";

                Button cardBtn = CreateVRButton(
                    canvas.transform,
                    $"SongCard_{i}_{c.difficultyLabel}",
                    label,
                    new Vector2(-300f, yPos),
                    new Vector2(560f, 120f),
                    new Color(0.10f, 0.15f, 0.26f, 0.92f),
                    27);

                cardButtons[i] = cardBtn;
                cardBgs[i] = cardBtn.GetComponent<Image>();
            }

            // 우측: 선택 곡 상세 패널
            GameObject detailBg = new GameObject("DetailPanelBG");
            detailBg.transform.SetParent(canvas.transform, false);
            Image detailImg = detailBg.AddComponent<Image>();
            detailImg.color = new Color(0.07f, 0.11f, 0.21f, 0.92f);
            RectTransform detailRt = detailBg.GetComponent<RectTransform>();
            detailRt.anchoredPosition = new Vector2(300f, 15f);
            detailRt.sizeDelta = new Vector2(580f, 415f);

            TMP_Text detailTitle = CreateTmpText(canvas.transform, "SelectedTitle", "Fade - VR Arcade", new Vector2(300f, 165f), new Vector2(540f, 68f), 34, Color.white);
            TMP_Text detailDiff = CreateTmpText(canvas.transform, "SelectedDiff", "DIFFICULTY : NORMAL", new Vector2(300f, 108f), new Vector2(540f, 46f), 26, new Color(0.25f, 0.85f, 1f));
            TMP_Text detailMeta = CreateTmpText(canvas.transform, "SelectedMeta", "Alan Walker [NCS Release] | 90 BPM | 618 NOTES", new Vector2(300f, 60f), new Vector2(540f, 44f), 22, new Color(0.82f, 0.88f, 0.95f));
            TMP_Text detailDesc = CreateTmpText(canvas.transform, "SelectedDesc", "NCS_Fade.wav 원곡 파형에 동기화된 6레일 아크 채보", new Vector2(300f, -15f), new Vector2(520f, 95f), 22, new Color(0.75f, 0.82f, 0.90f));

            // 노트 속도 조절 (- / +)
            Button speedDownBtn = CreateVRButton(canvas.transform, "SpeedDownButton", "-", new Vector2(105f, -125f), new Vector2(72f, 58f), new Color(0.20f, 0.26f, 0.38f, 0.95f), 36);
            TMP_Text speedValText = CreateTmpText(canvas.transform, "SpeedValueText", "NOTE SPEED : 8.0 m/s", new Vector2(300f, -125f), new Vector2(300f, 58f), 24, new Color(1.0f, 0.85f, 0.35f));
            Button speedUpBtn = CreateVRButton(canvas.transform, "SpeedUpButton", "+", new Vector2(495f, -125f), new Vector2(72f, 58f), new Color(0.20f, 0.26f, 0.38f, 0.95f), 36);

            // 하단 액션 버튼
            Button backBtn = CreateVRButton(canvas.transform, "BackButton", "← BACK TO LOBBY", new Vector2(-300f, -290f), new Vector2(360f, 76f), new Color(0.24f, 0.27f, 0.35f, 0.95f), 26);
            Button playBtn = CreateVRButton(canvas.transform, "PlayStageButton", "PLAY STAGE  ▶", new Vector2(300f, -290f), new Vector2(440f, 82f), new Color(0.95f, 0.45f, 0.05f, 0.98f), 34);

            SongSelectUIController controller = canvas.gameObject.AddComponent<SongSelectUIController>();
            controller.Configure(
                charts,
                cardButtons,
                cardBgs,
                speedDownBtn,
                speedUpBtn,
                playBtn,
                backBtn,
                detailTitle,
                detailDiff,
                detailMeta,
                detailDesc,
                speedValText);

            EditorSceneManager.SaveScene(scene, SongSelectScenePath);
        }

        // =========================================================================
        // Scene 3: 03_Game.unity (6레일 U자형 VR 리듬 게임 본편)
        // =========================================================================
        private static void BuildGameScene(
            Material floorMat,
            Material trackMat,
            Material edgeMat,
            Material hitPadMat,
            NoteController notePrefab,
            StickController stickPrefab,
            ParticleSystem perfectVfxPrefab,
            ParticleSystem goodVfxPrefab,
            BeatmapData defaultBeatmap)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupCommonArenaEnvironment(floorMat, edgeMat, hitPadMat, includeDecorativeArch: false);

            // 양손 컨트롤러에 VR 막대기(방안 A) 자동 부착
            Camera xrCam = SetupXRRigAndUIInfra(attachSticks: true, stickPrefab: stickPrefab);

            // 핵심 매니저 계층
            GameObject managersRoot = new GameObject("RhythmGameManagers");

            GameObject poolerObj = new GameObject("NotePooler");
            poolerObj.transform.SetParent(managersRoot.transform, false);
            NotePooler notePooler = poolerObj.AddComponent<NotePooler>();
            notePooler.ConfigurePrefab(notePrefab, 150);

            GameObject vfxObj = new GameObject("VfxPooler");
            vfxObj.transform.SetParent(managersRoot.transform, false);
            VfxPooler vfxPooler = vfxObj.AddComponent<VfxPooler>();
            vfxPooler.ConfigurePrefabs(perfectVfxPrefab, goodVfxPrefab, 20);

            GameObject judgeObj = new GameObject("JudgmentManager");
            judgeObj.transform.SetParent(managersRoot.transform, false);
            JudgmentManager judgmentManager = judgeObj.AddComponent<JudgmentManager>();

            GameObject conductorObj = new GameObject("RhythmConductor");
            conductorObj.transform.SetParent(managersRoot.transform, false);
            RhythmConductor conductor = conductorObj.AddComponent<RhythmConductor>();

            // 6레일 수평 반원형 아치(파노라마 콕핏) 베지어 곡선 궤도 + 6개 주황색 타격 버튼(HitPad)
            GameObject trackRoot = new GameObject("CurvedCockpitArchRoot");
            Transform[] laneParents = new Transform[6];
            HitPadButton[] hitPads = new HitPadButton[6];

            for (int i = 0; i < 6; i++)
            {
                CurvedLaneSpec spec = LaneSpecs[i];
                GameObject laneObj = new GameObject(spec.Name);
                laneObj.transform.SetParent(trackRoot.transform, false);
                laneObj.transform.position = Vector3.zero;
                laneObj.transform.rotation = Quaternion.identity;
                laneParents[i] = laneObj.transform;

                float rad = spec.YawDeg * Mathf.Deg2Rad;
                Vector3 radialOut = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

                // 4개의 3차 베지어 제어점: 플레이어 앞 반원 아치(P0) <-> 먼 정면 지평선(P3)
                Vector3 p0 = new Vector3(radialOut.x * HitButtonRadius, spec.HitY, radialOut.z * HitButtonRadius);
                Vector3 p3 = new Vector3(spec.SpawnX, 1.78f, SpawnLocalZ);
                Vector3 tangentOut = (radialOut + new Vector3(0f, 0.08f, 0f)).normalized;
                Vector3 p1 = p0 + tangentOut * 3.4f + Vector3.forward * 1.5f;
                Vector3 p2 = p3 - Vector3.forward * 5.2f;

                CurvedLaneTrack curvedTrack = laneObj.AddComponent<CurvedLaneTrack>();
                curvedTrack.Configure(p0, p1, p2, p3, spec.RollDeg);

                // 부드러운 3D 곡선 레일 본체 메시 생성 및 저장
                Mesh surfaceMesh = BuildAndSaveCurvedRibbonMesh(
                    $"{MeshesDir}/CurvedLaneSurface_{i}.asset",
                    curvedTrack,
                    RailWidth,
                    0f,
                    0f,
                    48);

                GameObject railObj = new GameObject("CurvedRailSurface");
                railObj.transform.SetParent(laneObj.transform, false);
                railObj.AddComponent<MeshFilter>().sharedMesh = surfaceMesh;
                railObj.AddComponent<MeshRenderer>().sharedMaterial = trackMat;

                // 좌·우 네온 가이드 라인 곡선 리본
                float halfW = RailWidth * 0.5f;
                CreateCurvedEdgeMesh(laneObj.transform, $"CurvedEdgeLeft_{i}", $"{MeshesDir}/CurvedEdgeL_{i}.asset", curvedTrack, -halfW, edgeMat);
                CreateCurvedEdgeMesh(laneObj.transform, $"CurvedEdgeRight_{i}", $"{MeshesDir}/CurvedEdgeR_{i}.asset", curvedTrack, halfW, edgeMat);

                // 주황색 물리 타격 버튼 (Hit Pad) — 아치 반원 선상(P0)에 플레이어를 정면으로 마주보게 배치
                curvedTrack.EvaluateAtDistance(0f, 0.05f, out Vector3 padPos, out Quaternion padRot);

                GameObject padObj = new GameObject($"HitPad_{i}");
                padObj.transform.SetParent(laneObj.transform, false);
                padObj.transform.SetPositionAndRotation(padPos, padRot);

                BoxCollider triggerBox = padObj.AddComponent<BoxCollider>();
                triggerBox.isTrigger = true;
                triggerBox.size = new Vector3(RailWidth + 0.03f, 0.22f, 0.30f);
                triggerBox.center = new Vector3(0f, 0.04f, 0f);

                GameObject buttonVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                buttonVisual.name = "ButtonVisual_Orange";
                buttonVisual.transform.SetParent(padObj.transform, false);
                buttonVisual.transform.localPosition = Vector3.zero;
                buttonVisual.transform.localScale = new Vector3(RailWidth - 0.025f, 0.09f, 0.19f);
                Object.DestroyImmediate(buttonVisual.GetComponent<Collider>());
                buttonVisual.GetComponent<Renderer>().sharedMaterial = hitPadMat;

                HitPadButton hitPad = padObj.AddComponent<HitPadButton>();
                hitPad.ConfigurePad(i, judgmentManager, buttonVisual.transform);
                hitPads[i] = hitPad;
            }

            // 플레이어 앞 6개 타격 버튼을 부드러운 수평 반원형 아치로 이어주는 콕핏 네온 바
            BuildHorizontalCockpitArchBar(trackRoot.transform, edgeMat);

            judgmentManager.ConfigureReferences(notePooler, vfxPooler, hitPads);
            conductor.ConfigureReferences(defaultBeatmap, notePooler, judgmentManager, laneParents, SpawnLocalZ, HitButtonRadius, NoteHeightOffset, DefaultNoteSpeed);

            // AudioSource에 오디오 클립 및 2D 스테레오 설정을 씬에 직접 직렬화
            AudioSource bgmSource = conductorObj.GetComponent<AudioSource>();
            if (bgmSource != null)
            {
                bgmSource.playOnAwake = false;
                bgmSource.loop = false;
                bgmSource.spatialBlend = 0f;
                bgmSource.volume = 0.95f;
                bgmSource.clip = defaultBeatmap != null ? defaultBeatmap.audioClip : null;
            }

            // 런타임 막대기 표시·각도 보정 보장기 부착
            VRStickRigBootstrap stickBootstrap = managersRoot.AddComponent<VRStickRigBootstrap>();
            stickBootstrap.Configure(stickPrefab);

            // PC 에디터 보조 키보드 테스터 (1~6 타격, R 재시작, Esc 곡 선택 복귀)
            EditorKeyboardHitTester keyTester = managersRoot.AddComponent<EditorKeyboardHitTester>();
            keyTester.Configure(hitPads, conductor);

            // World-Space 스코어·콤보·판정 HUD — 6개 곡선 레일이 모이는 정면 지평선 위쪽에 시야 방해 없이 배치
            Canvas hudCanvas = CreateVRWorldCanvas("WorldSpaceHUD", new Vector3(0f, 2.55f, 6.0f), new Vector2(1040f, 520f), 0.0040f, xrCam);
            CreatePanelBackground(hudCanvas.transform, new Color(0.04f, 0.07f, 0.14f, 0.76f));

            TMP_Text titleText = CreateTmpText(hudCanvas.transform, "SongTitleText", "Fade - VR Arcade", new Vector2(0, 200), new Vector2(960, 56), 34, new Color(0.75f, 0.90f, 1f));
            TMP_Text scoreText = CreateTmpText(hudCanvas.transform, "ScoreText", "SCORE  0", new Vector2(0, 125), new Vector2(960, 80), 58, Color.white);
            TMP_Text comboText = CreateTmpText(hudCanvas.transform, "ComboText", "", new Vector2(0, 42), new Vector2(960, 70), 48, new Color(1f, 0.85f, 0.3f));
            TMP_Text judgeText = CreateTmpText(hudCanvas.transform, "JudgmentText", "READY", new Vector2(0, -45), new Vector2(960, 90), 68, new Color(0.2f, 0.85f, 1f));
            TMP_Text statsText = CreateTmpText(hudCanvas.transform, "StatsText", "PERFECT 0   |   GOOD 0   |   MISS 0   |   MAX 0", new Vector2(0, -135), new Vector2(960, 48), 26, new Color(0.8f, 0.85f, 0.9f));

            Button retryBtn = CreateVRButton(hudCanvas.transform, "HudRetryButton", "RETRY", new Vector2(-175f, -205f), new Vector2(260f, 58f), new Color(0.15f, 0.45f, 0.75f, 0.90f), 24);
            Button exitBtn = CreateVRButton(hudCanvas.transform, "HudExitButton", "SONG SELECT", new Vector2(175f, -205f), new Vector2(280f, 58f), new Color(0.28f, 0.30f, 0.38f, 0.90f), 24);

            RhythmHudUI hud = hudCanvas.gameObject.AddComponent<RhythmHudUI>();
            hud.Configure(judgmentManager, conductor, titleText, scoreText, comboText, judgeText, statsText, retryBtn, exitBtn);

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        // =========================================================================
        // Scene 4: 04_Result.unity
        // =========================================================================
        private static void BuildResultScene(Material floorMat, Material edgeMat, Material accentOrangeMat)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupCommonArenaEnvironment(floorMat, edgeMat, accentOrangeMat, includeDecorativeArch: true);

            Camera xrCam = SetupXRRigAndUIInfra(attachSticks: false, stickPrefab: null);

            Canvas canvas = CreateVRWorldCanvas("ResultCanvas", new Vector3(0f, 1.55f, 2.55f), new Vector2(1140f, 720f), 0.0021f, xrCam);
            CreatePanelBackground(canvas.transform, new Color(0.04f, 0.07f, 0.15f, 0.90f));

            CreateTmpText(canvas.transform, "StageClearTitle", "STAGE RESULT", new Vector2(0, 290), new Vector2(960, 64), 46, new Color(0.30f, 0.88f, 1f));
            TMP_Text headerText = CreateTmpText(canvas.transform, "SongHeaderText", "Fade - VR Arcade  [NORMAL]", new Vector2(0, 225), new Vector2(1020, 52), 30, Color.white);
            TMP_Text gradeText = CreateTmpText(canvas.transform, "GradeText", "S", new Vector2(0, 105), new Vector2(320, 160), 130, new Color(1.0f, 0.88f, 0.22f));
            TMP_Text scoreText = CreateTmpText(canvas.transform, "FinalScoreText", "FINAL SCORE   0", new Vector2(0, -15), new Vector2(960, 68), 46, Color.white);
            TMP_Text accText = CreateTmpText(canvas.transform, "AccuracyText", "ACCURACY  100.0%     |     MAX COMBO  0", new Vector2(0, -82), new Vector2(960, 48), 28, new Color(0.85f, 0.92f, 1f));
            TMP_Text breakdownText = CreateTmpText(canvas.transform, "BreakdownText", "PERFECT  0      |      GOOD  0      |      MISS  0", new Vector2(0, -138), new Vector2(960, 46), 26, new Color(0.75f, 0.84f, 0.92f));

            Button retryBtn = CreateVRButton(canvas.transform, "RetryButton", "RETRY", new Vector2(-330f, -255f), new Vector2(280f, 76f), new Color(0.95f, 0.45f, 0.05f, 0.96f), 30);
            Button songSelectBtn = CreateVRButton(canvas.transform, "SongSelectButton", "SONG SELECT", new Vector2(0f, -255f), new Vector2(310f, 76f), new Color(0.10f, 0.52f, 0.88f, 0.96f), 28);
            Button lobbyBtn = CreateVRButton(canvas.transform, "LobbyButton", "LOBBY", new Vector2(330f, -255f), new Vector2(280f, 76f), new Color(0.24f, 0.27f, 0.35f, 0.94f), 28);

            ResultUIController controller = canvas.gameObject.AddComponent<ResultUIController>();
            controller.Configure(headerText, gradeText, scoreText, accText, breakdownText, retryBtn, songSelectBtn, lobbyBtn);

            EditorSceneManager.SaveScene(scene, ResultScenePath);
        }

        // =========================================================================
        // Shared Scene & Asset Builders
        // =========================================================================
        private static void SetupCommonArenaEnvironment(Material floorMat, Material edgeMat, Material accentOrangeMat, bool includeDecorativeArch)
        {
            // 조명
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.color = new Color(0.92f, 0.95f, 1.0f);
            lightObj.transform.rotation = Quaternion.Euler(48f, -25f, 0f);

            // 어두운 사이버 아케이드 분위기
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.20f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.03f, 0.05f, 0.10f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.025f;

            // 바닥 플랫폼
            GameObject arenaRoot = new GameObject("ArenaEnvironment");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ArenaFloor";
            floor.transform.SetParent(arenaRoot.transform, false);
            floor.transform.position = new Vector3(0f, -0.05f, 8f);
            floor.transform.localScale = new Vector3(18f, 0.1f, 34f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // 로비/곡선택/결과 씬용 장식 U자 아치 프레임
            if (includeDecorativeArch)
            {
                GameObject arch = new GameObject("DecorativeUArch");
                arch.transform.SetParent(arenaRoot.transform, false);
                arch.transform.position = new Vector3(0f, 0f, 3.2f);

                CreateDecoBar(arch.transform, "BottomGlow", new Vector3(0f, 0.04f, 0f), new Vector3(2.8f, 0.06f, 0.08f), Vector3.zero, edgeMat);
                CreateDecoBar(arch.transform, "LeftWallGlow", new Vector3(-1.85f, 1.5f, 0f), new Vector3(0.07f, 2.8f, 0.08f), Vector3.zero, accentOrangeMat);
                CreateDecoBar(arch.transform, "RightWallGlow", new Vector3(1.85f, 1.5f, 0f), new Vector3(0.07f, 2.8f, 0.08f), Vector3.zero, accentOrangeMat);
            }
        }

        private static void CreateDecoBar(Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = pos;
            bar.transform.localRotation = Quaternion.Euler(euler);
            bar.transform.localScale = scale;
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Camera SetupXRRigAndUIInfra(bool attachSticks, StickController stickPrefab)
        {
            // XR Interaction Manager
            GameObject xriManagerObj = new GameObject("XR Interaction Manager");
            xriManagerObj.AddComponent<XRInteractionManager>();

            // EventSystem + XRUIInputModule (VR 레이저 포인터 + 에디터 마우스 모두 지원)
            GameObject eventSysObj = new GameObject("EventSystem");
            eventSysObj.AddComponent<EventSystem>();
            XRUIInputModule uiModule = eventSysObj.AddComponent<XRUIInputModule>();
            uiModule.enableXRInput = true;
            uiModule.enableMouseInput = true;
            uiModule.enableTouchInput = true;
            uiModule.enableBuiltinActionsAsFallback = true;

            // XR Origin (XR Rig) 프리팹 배치
            GameObject xrOriginPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
            Camera xrCam = null;

            if (xrOriginPrefab != null)
            {
                GameObject xrOrigin = (GameObject)PrefabUtility.InstantiatePrefab(xrOriginPrefab);
                xrOrigin.name = "XR Origin (XR Rig)";
                xrOrigin.transform.position = Vector3.zero;
                xrOrigin.transform.rotation = Quaternion.identity;

                xrCam = xrOrigin.GetComponentInChildren<Camera>(true);

                if (attachSticks && stickPrefab != null)
                {
                    // 중첩 프리팹 저장 누락(m_AddedGameObjects 비어짐)을 원천 방지하기 위해
                    // 게임 씬의 XR Origin 루트를 언팩한 뒤 양손 막대기를 확실히 직렬화합니다.
                    PrefabUtility.UnpackPrefabInstance(xrOrigin, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

                    Transform leftCtrl = FindDeepChild(xrOrigin.transform, "Left Controller");
                    Transform rightCtrl = FindDeepChild(xrOrigin.transform, "Right Controller");
                    AttachStickToController(leftCtrl, stickPrefab, StickController.HandSide.Left, "LeftVRStick");
                    AttachStickToController(rightCtrl, stickPrefab, StickController.HandSide.Right, "RightVRStick");
                }
            }
            else
            {
                Debug.LogWarning($"[VR Rhythm Beat] '{XROriginPrefabPath}'를 찾지 못해 임시 Main Camera를 생성합니다.");
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                camObj.transform.position = new Vector3(0f, 1.55f, 0f);
                xrCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            // 스카이박스 대신 어두운 아케이드 단색 배경으로 VR 가독성 확보
            if (xrCam != null)
            {
                xrCam.clearFlags = CameraClearFlags.SolidColor;
                xrCam.backgroundColor = new Color(0.03f, 0.05f, 0.11f, 1f);
            }

            return xrCam;
        }

        private static Canvas CreateVRWorldCanvas(string name, Vector3 worldPos, Vector2 sizeDelta, float scale, Camera worldCamera)
        {
            GameObject canvasObj = new GameObject(name);
            canvasObj.transform.position = worldPos;
            canvasObj.transform.rotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * scale;

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = worldCamera;

            RectTransform rt = canvasObj.GetComponent<RectTransform>();
            rt.sizeDelta = sizeDelta;

            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();
            return canvas;
        }

        private static void CreatePanelBackground(Transform parent, Color color)
        {
            GameObject bgObj = new GameObject("PanelBG");
            bgObj.transform.SetParent(parent, false);
            Image bg = bgObj.AddComponent<Image>();
            bg.color = color;
            RectTransform rt = bgObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TMP_Text CreateTmpText(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return tmp;
        }

        private static Button CreateVRButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor, float fontSize)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
            cb.selectedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            btn.colors = cb;

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            CreateTmpText(btnObj.transform, "Label", label, Vector2.zero, size - new Vector2(24f, 12f), fontSize, Color.white);
            return btn;
        }

        private static void RegisterBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(SongSelectScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
                new EditorBuildSettingsScene(ResultScenePath, true)
            };
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
            if (!Directory.Exists(MaterialsDir)) Directory.CreateDirectory(MaterialsDir);
            if (!Directory.Exists(PrefabsDir)) Directory.CreateDirectory(PrefabsDir);
            if (!Directory.Exists(BeatmapsDir)) Directory.CreateDirectory(BeatmapsDir);
            if (!Directory.Exists(MeshesDir)) Directory.CreateDirectory(MeshesDir);
        }

        private static void EnsurePhysicsLayersAndMatrix()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            SetLayerIfEmpty(layersProp, 6, "Stick");
            SetLayerIfEmpty(layersProp, 7, "HitPad");
            SetLayerIfEmpty(layersProp, 8, "Note");
            tagManager.ApplyModifiedProperties();

            int stickLayer = LayerMask.NameToLayer("Stick");
            int hitPadLayer = LayerMask.NameToLayer("HitPad");
            int noteLayer = LayerMask.NameToLayer("Note");

            if (stickLayer >= 0 && hitPadLayer >= 0)
            {
                for (int l = 0; l < 32; l++)
                {
                    Physics.IgnoreLayerCollision(stickLayer, l, l != hitPadLayer);
                }
            }

            if (noteLayer >= 0)
            {
                for (int l = 0; l < 32; l++)
                {
                    Physics.IgnoreLayerCollision(noteLayer, l, true);
                }
            }
        }

        private static void SetLayerIfEmpty(SerializedProperty layersProp, int index, string layerName)
        {
            SerializedProperty sp = layersProp.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
            }
        }

        private static Material CreateOrUpdateLitMaterial(string assetPath, Color baseColor, Color emissionColor, float smoothness, float metallic)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);

            if (emissionColor.maxColorComponent > 0.001f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateParticleMaterial(string assetPath, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", color);
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static NoteController CreateNoteCubePrefab(Material noteMat)
        {
            string prefabPath = $"{PrefabsDir}/NoteCube.prefab";
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temp.name = "NoteCube";
            temp.transform.localScale = new Vector3(0.23f, 0.11f, 0.28f);

            Object.DestroyImmediate(temp.GetComponent<Collider>());
            temp.GetComponent<Renderer>().sharedMaterial = noteMat;

            int noteLayer = LayerMask.NameToLayer("Note");
            if (noteLayer >= 0) temp.layer = noteLayer;

            temp.AddComponent<NoteController>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);
            return saved.GetComponent<NoteController>();
        }

        private static StickController CreateStickPrefab(Material handleMat, Material rodMat)
        {
            string prefabPath = $"{PrefabsDir}/VRStick.prefab";
            GameObject root = new GameObject("VRStick");

            int stickLayer = LayerMask.NameToLayer("Stick");
            if (stickLayer >= 0) root.layer = stickLayer;

            CapsuleCollider cap = root.AddComponent<CapsuleCollider>();
            cap.isTrigger = false;
            cap.direction = 2;
            cap.radius = 0.038f;
            cap.height = 0.54f;
            cap.center = new Vector3(0f, 0f, 0.25f);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            root.AddComponent<StickController>();

            // 손바닥 안에 딱 잡히는 손잡이
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "HandleMesh";
            handle.transform.SetParent(root.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handle.transform.localScale = new Vector3(0.038f, 0.09f, 0.038f);
            Object.DestroyImmediate(handle.GetComponent<Collider>());
            handle.GetComponent<Renderer>().sharedMaterial = handleMat;

            // 손잡이와 타격봉 경계 가드 링
            GameObject guard = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            guard.name = "GuardRing";
            guard.transform.SetParent(root.transform, false);
            guard.transform.localPosition = new Vector3(0f, 0f, 0.11f);
            guard.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            guard.transform.localScale = new Vector3(0.062f, 0.012f, 0.062f);
            Object.DestroyImmediate(guard.GetComponent<Collider>());
            guard.GetComponent<Renderer>().sharedMaterial = rodMat;

            // 선명하게 빛나는 전방 타격봉
            GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = "RodMesh";
            rod.transform.SetParent(root.transform, false);
            rod.transform.localPosition = new Vector3(0f, 0f, 0.32f);
            rod.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rod.transform.localScale = new Vector3(0.030f, 0.21f, 0.030f);
            Object.DestroyImmediate(rod.GetComponent<Collider>());
            rod.GetComponent<Renderer>().sharedMaterial = rodMat;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<StickController>();
        }

        private static ParticleSystem CreateHitVfxPrefab(string prefabPath, Material particleMat, Color burstColor)
        {
            GameObject go = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.45f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
            main.startColor = burstColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)));

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = particleMat;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return saved.GetComponent<ParticleSystem>();
        }

        private static AudioClip LoadFadeAudioClip()
        {
            if (File.Exists(FadeAudioAssetPath))
            {
                // PlayScheduled 무음 버그의 원인이 되는 preloadAudioData=false를 true로 강제 보정
                AudioImporter importer = AssetImporter.GetAtPath(FadeAudioAssetPath) as AudioImporter;
                if (importer != null)
                {
                    AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                    bool changed = false;
                    if (!settings.preloadAudioData)
                    {
                        settings.preloadAudioData = true;
                        changed = true;
                    }
                    if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
                    {
                        settings.loadType = AudioClipLoadType.DecompressOnLoad;
                        changed = true;
                    }
                    if (importer.loadInBackground)
                    {
                        importer.loadInBackground = false;
                        changed = true;
                    }
                    if (changed)
                    {
                        importer.defaultSampleSettings = settings;
                        importer.SaveAndReimport();
                    }
                }
                else
                {
                    AssetDatabase.ImportAsset(FadeAudioAssetPath, ImportAssetOptions.ForceSynchronousImport);
                }

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(FadeAudioAssetPath);
                if (clip != null) return clip;
            }
            Debug.LogWarning($"[VR Rhythm Beat] '{FadeAudioAssetPath}' 오디오 클립을 찾지 못했습니다.");
            return null;
        }

        private static void CreateCurvedEdgeMesh(Transform laneParent, string objName, string meshPath, CurvedLaneTrack track, float lateralOffset, Material edgeMat)
        {
            Mesh edgeMesh = BuildAndSaveCurvedRibbonMesh(meshPath, track, 0.016f, lateralOffset, 0.015f, 48);
            GameObject edgeObj = new GameObject(objName);
            edgeObj.transform.SetParent(laneParent, false);
            edgeObj.AddComponent<MeshFilter>().sharedMesh = edgeMesh;
            edgeObj.AddComponent<MeshRenderer>().sharedMaterial = edgeMat;
        }

        /// <summary>
        /// CurvedLaneTrack의 3차 베지어 곡선을 따라 부드러운 3D 곡선 리본 메시를 생성하고 에셋으로 저장합니다.
        /// </summary>
        private static Mesh BuildAndSaveCurvedRibbonMesh(
            string assetPath,
            CurvedLaneTrack track,
            float width,
            float lateralOffset,
            float heightOffset,
            int segments)
        {
            int vertCount = (segments + 1) * 2;
            Vector3[] vertices = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] triangles = new int[segments * 6];

            float halfW = width * 0.5f;
            float startDist = -0.35f; // 타격 버튼 뒤쪽까지 살짝 여유 연장
            float endDist = track.TrackLength;

            for (int i = 0; i <= segments; i++)
            {
                float frac = i / (float)segments;
                float dist = Mathf.Lerp(startDist, endDist, frac);
                track.EvaluateAtDistance(dist, heightOffset, out Vector3 center, out Quaternion rot);

                Vector3 right = rot * Vector3.right;
                Vector3 up = rot * Vector3.up;

                int vi = i * 2;
                vertices[vi]     = center + right * (lateralOffset - halfW);
                vertices[vi + 1] = center + right * (lateralOffset + halfW);
                normals[vi]      = up;
                normals[vi + 1]  = up;
                uvs[vi]          = new Vector2(0f, frac);
                uvs[vi + 1]      = new Vector2(1f, frac);

                if (i < segments)
                {
                    int ti = i * 6;
                    triangles[ti]     = vi;
                    triangles[ti + 1] = vi + 2;
                    triangles[ti + 2] = vi + 1;
                    triangles[ti + 3] = vi + 1;
                    triangles[ti + 4] = vi + 2;
                    triangles[ti + 5] = vi + 3;
                }
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = Path.GetFileNameWithoutExtension(assetPath);
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, assetPath);
            }
            else
            {
                mesh.Clear();
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        /// <summary>
        /// 플레이어 몸 앞을 수평 반원형(-62° ~ +62°)으로 감싸며 6개 타격 버튼을 하나로 연결하는 네온 아치 바.
        /// </summary>
        private static void BuildHorizontalCockpitArchBar(Transform parent, Material glowMat)
        {
            GameObject ringRoot = new GameObject("CockpitArchBar");
            ringRoot.transform.SetParent(parent, false);

            int segments = 16;
            float startDeg = -62f;
            float endDeg = 62f;
            float r = HitButtonRadius - 0.12f; // 버튼 바로 앞쪽 플레이어 쪽 테두리

            for (int i = 0; i < segments; i++)
            {
                float d0 = Mathf.Lerp(startDeg, endDeg, i / (float)segments);
                float d1 = Mathf.Lerp(startDeg, endDeg, (i + 1) / (float)segments);
                float a0 = d0 * Mathf.Deg2Rad;
                float a1 = d1 * Mathf.Deg2Rad;

                float y0 = 0.94f + 0.08f * Mathf.Abs(d0 / 62f);
                float y1 = 0.94f + 0.08f * Mathf.Abs(d1 / 62f);

                Vector3 p0 = new Vector3(Mathf.Sin(a0) * r, y0, Mathf.Cos(a0) * r);
                Vector3 p1 = new Vector3(Mathf.Sin(a1) * r, y1, Mathf.Cos(a1) * r);

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"CockpitSeg_{i:00}";
                seg.transform.SetParent(ringRoot.transform, false);
                seg.transform.position = (p0 + p1) * 0.5f;
                Vector3 dir = p1 - p0;
                seg.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                seg.transform.localScale = new Vector3(0.022f, 0.024f, dir.magnitude + 0.012f);
                Object.DestroyImmediate(seg.GetComponent<Collider>());
                seg.GetComponent<Renderer>().sharedMaterial = glowMat;
            }
        }

        private static BeatmapData CreateOrUpdateBeatmap(string assetPath, System.Action<BeatmapData> populator)
        {
            BeatmapData data = AssetDatabase.LoadAssetAtPath<BeatmapData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BeatmapData>();
                populator?.Invoke(data);
                AssetDatabase.CreateAsset(data, assetPath);
            }
            else
            {
                populator?.Invoke(data);
                EditorUtility.SetDirty(data);
            }
            return data;
        }

        private static void CreateRailEdge(Transform laneParent, string edgeName, float localX, float centerZ, float length, Material edgeMat)
        {
            GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = edgeName;
            edge.transform.SetParent(laneParent, false);
            edge.transform.localPosition = new Vector3(localX, 0.02f, centerZ);
            edge.transform.localScale = new Vector3(0.018f, 0.022f, length);
            Object.DestroyImmediate(edge.GetComponent<Collider>());
            edge.GetComponent<Renderer>().sharedMaterial = edgeMat;
        }

        private static void AttachStickToController(Transform controller, StickController stickPrefab, StickController.HandSide side, string stickName)
        {
            if (controller == null) return;

            Transform existing = controller.Find(stickName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            // 게임 씬에서는 손-컨트롤러 각도 이질감이 생길 수 있는 기본 Quest형 컨트롤러 모델 숨김
            Transform defaultVisual = FindDeepChild(controller, side == StickController.HandSide.Left ? "Left Controller Visual" : "Right Controller Visual");
            if (defaultVisual != null)
            {
                defaultVisual.gameObject.SetActive(false);
            }

            GameObject stickInstance = (GameObject)PrefabUtility.InstantiatePrefab(stickPrefab.gameObject, controller);
            stickInstance.name = stickName;
            stickInstance.transform.localPosition = new Vector3(0f, -0.015f, 0f);
            stickInstance.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

            StickController sc = stickInstance.GetComponent<StickController>();
            HapticImpulsePlayer haptic = controller.GetComponentInChildren<HapticImpulsePlayer>(true);
            sc.ConfigureHand(side, haptic);
            EditorUtility.SetDirty(sc);
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == targetName) return child;
                Transform found = FindDeepChild(child, targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
