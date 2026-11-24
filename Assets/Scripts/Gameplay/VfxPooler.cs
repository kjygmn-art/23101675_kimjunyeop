using UnityEngine;

namespace VRRhythmBeat.Gameplay
{
    /// <summary>
    /// 2차 피드백(노드 파괴 시 파란색/초록색 파티클 폭발)을 런타임 GC 없이 재생하는 VFX 풀러.
    /// 링 버퍼(Ring Buffer) 방식으로 Instantiate/Destroy 없이 파티클을 순환 재사용합니다.
    /// </summary>
    public class VfxPooler : MonoBehaviour
    {
        [Header("Particle Prefabs")]
        [SerializeField] private ParticleSystem perfectBurstPrefab;
        [SerializeField] private ParticleSystem goodBurstPrefab;
        [SerializeField, Range(8, 48)] private int poolSizePerType = 20;

        private ParticleSystem[] _perfectPool;
        private ParticleSystem[] _goodPool;
        private int _perfectCursor;
        private int _goodCursor;

        private void Awake()
        {
            _perfectPool = CreatePool(perfectBurstPrefab, "PerfectVfx", new Color(0.15f, 0.75f, 1f));
            _goodPool = CreatePool(goodBurstPrefab, "GoodVfx", new Color(0.2f, 1f, 0.45f));
        }

        private ParticleSystem[] CreatePool(ParticleSystem prefab, string label, Color fallbackColor)
        {
            ParticleSystem[] pool = new ParticleSystem[poolSizePerType];

            for (int i = 0; i < poolSizePerType; i++)
            {
                ParticleSystem ps;
                if (prefab != null)
                {
                    ps = Instantiate(prefab, transform);
                    ps.name = $"{label}_{i:00}";
                }
                else
                {
                    ps = CreateFallbackParticleSystem($"{label}_Fallback_{i:00}", fallbackColor);
                }

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                pool[i] = ps;
            }

            return pool;
        }

        /// <summary>
        /// 프리팹이 아직 연결되지 않은 경우에도 즉시 시각적 폭발 이펙트를 볼 수 있도록 기본 파티클을 구성합니다.
        /// </summary>
        private ParticleSystem CreateFallbackParticleSystem(string objName, Color burstColor)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.45f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 5.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = burstColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve shrinkCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, shrinkCurve);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    mat.color = burstColor;
                    renderer.sharedMaterial = mat;
                }
            }

            return ps;
        }

        /// <summary>
        /// 지정된 월드 위치에서 Perfect(파란색) 또는 Good(초록색) 폭발 파티클을 재생합니다.
        /// </summary>
        public void PlayHitBurst(Vector3 worldPosition, bool isPerfect)
        {
            ParticleSystem[] pool = isPerfect ? _perfectPool : _goodPool;
            if (pool == null || pool.Length == 0) return;

            int index = isPerfect
                ? (_perfectCursor++ % pool.Length)
                : (_goodCursor++ % pool.Length);

            ParticleSystem ps = pool[index];
            if (ps == null) return;

            ps.transform.position = worldPosition;
            ps.Clear(true);
            ps.Play(true);
        }

        public void ConfigurePrefabs(ParticleSystem perfectPrefab, ParticleSystem goodPrefab, int sizePerType = 20)
        {
            perfectBurstPrefab = perfectPrefab;
            goodBurstPrefab = goodPrefab;
            poolSizePerType = Mathf.Clamp(sizePerType, 8, 48);
        }
    }
}
