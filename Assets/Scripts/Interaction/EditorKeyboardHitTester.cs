using UnityEngine;
using UnityEngine.InputSystem;
using VRRhythmBeat.Core;

namespace VRRhythmBeat.Interaction
{
    /// <summary>
    /// VR 헤드셋 없이 Unity Editor 플레이 모드에서 6개 버튼 타격(키보드 1~6) 및
    /// 곡 재시작(R 키)을 즉시 검증할 수 있는 보조 테스트 컴포넌트입니다.
    /// 빌드된 VR 기기에서는 물리 막대기(StickController) 입력에 아무런 간섭을 하지 않습니다.
    /// </summary>
    public class EditorKeyboardHitTester : MonoBehaviour
    {
        [SerializeField] private HitPadButton[] hitPads = new HitPadButton[6];
        [SerializeField] private RhythmConductor conductor;
        [SerializeField] private bool enableKeyboardTest = true;

        private void Update()
        {
            if (!enableKeyboardTest) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            CheckLaneKey(kb.digit1Key, 0);
            CheckLaneKey(kb.digit2Key, 1);
            CheckLaneKey(kb.digit3Key, 2);
            CheckLaneKey(kb.digit4Key, 3);
            CheckLaneKey(kb.digit5Key, 4);
            CheckLaneKey(kb.digit6Key, 5);

            if (kb.rKey.wasPressedThisFrame && conductor != null)
            {
                conductor.RestartSong();
            }

            if (kb.escapeKey.wasPressedThisFrame)
            {
                GameSessionManager.Instance.GoToSongSelect();
            }
        }

        private void CheckLaneKey(UnityEngine.InputSystem.Controls.KeyControl key, int laneIndex)
        {
            if (key != null && key.wasPressedThisFrame && hitPads != null && laneIndex < hitPads.Length && hitPads[laneIndex] != null)
            {
                hitPads[laneIndex].TriggerDebugHit();
            }
        }

        public void Configure(HitPadButton[] pads, RhythmConductor rhythmConductor)
        {
            hitPads = pads;
            conductor = rhythmConductor;
        }
    }
}
