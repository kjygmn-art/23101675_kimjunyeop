using System.Collections.Generic;
using UnityEngine;

namespace VRRhythmBeat.Core
{
    /// <summary>
    /// 런타임 Instantiate/Destroy를 원천 금지하기 위한 노트 오브젝트 풀러.
    /// Awake 시점에 파란색 큐브 노드를 100~150개 미리 생성해 Queue에 대기시킵니다.
    /// </summary>
    public class NotePooler : MonoBehaviour
    {
        [Header("Pool Configuration")]
        [SerializeField] private NoteController notePrefab;
        [SerializeField, Range(100, 300)] private int initialPoolSize = 150;

        private readonly Queue<NoteController> _pool = new Queue<NoteController>(160);
        private Transform _poolContainer;

        public int AvailableCount => _pool.Count;

        private void Awake()
        {
            _poolContainer = transform;
            PrewarmPool();
        }

        private void PrewarmPool()
        {
            if (notePrefab == null)
            {
                Debug.LogError("[NotePooler] notePrefab이 할당되지 않았습니다! Inspector에서 NoteCube 프리팹을 연결하세요.");
                return;
            }

            for (int i = 0; i < initialPoolSize; i++)
            {
                NoteController instance = Instantiate(notePrefab, _poolContainer);
                instance.name = $"PooledNote_{i:000}";
                instance.Deactivate();
                _pool.Enqueue(instance);
            }
        }

        /// <summary>
        /// 풀에서 비활성화된 노트 1개를 꺼냅니다. 런타임 생성(Instantiate)을 수행하지 않습니다.
        /// </summary>
        public NoteController GetNote()
        {
            if (_pool.Count == 0)
            {
                Debug.LogWarning("[NotePooler] 풀이 비었습니다! initialPoolSize를 늘려주세요.");
                return null;
            }

            return _pool.Dequeue();
        }

        /// <summary>
        /// 사용이 끝난 노드를 비활성화하고 풀 Queue로 반환합니다.
        /// </summary>
        public void ReturnNote(NoteController note)
        {
            if (note == null) return;

            note.Deactivate();
            note.transform.SetParent(_poolContainer, false);
            _pool.Enqueue(note);
        }

        /// <summary>
        /// 에디터 셋업 스크립트에서 프리팹을 주입하기 위한 설정 메서드.
        /// </summary>
        public void ConfigurePrefab(NoteController prefab, int poolSize = 150)
        {
            notePrefab = prefab;
            initialPoolSize = Mathf.Clamp(poolSize, 100, 300);
        }
    }
}
