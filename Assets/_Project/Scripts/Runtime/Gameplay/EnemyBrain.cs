using Emberfall.Core;
using Emberfall.Data;
using UnityEngine;

namespace Emberfall.Gameplay
{
    /// <summary>
    /// Enemy AI state machine: Idle -> Telegraph -> Attack -> Recover.
    /// Every attack is telegraphed with time + a visible ring + a "!" word
    /// marker (never colour alone). No per-frame allocations.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        private enum State { Idle, Chase, Telegraph, Recover, Staggered, Dead }

        public EnemyDefinition Definition { get; private set; }
        public Health Health { get; private set; }
        public bool IsTelegraphing { get { return _state == State.Telegraph; } }

        private State _state;
        private float _timer;
        private Transform _target;
        private PlayerController _targetPlayer;
        private System.Random _rng;
        private float _aggression = 1f;
        private float _depthScale = 1f;
        private Transform _telegraphRing;
        private TextMesh _marker;
        private Rigidbody _body;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _mpb;
        private Renderer _renderer;

        public event System.Action<EnemyBrain> DiedEvent;

        public void Init(EnemyDefinition def, Transform target, int seed, float depthScale)
        {
            Definition = def;
            _target = target;
            _targetPlayer = target != null ? target.GetComponent<PlayerController>() : null;
            _rng = new System.Random(seed);
            _depthScale = depthScale;
            Health = GetComponent<Health>();
            Health.Init(Mathf.RoundToInt(def.MaxHealth * depthScale));
            Health.Died += OnDied;
            _body = GetComponent<Rigidbody>();
            _renderer = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
            BuildTelegraph();
            _state = State.Idle;
            _timer = 0.5f;
        }

        public void SetAggression(float multiplier) { _aggression = multiplier; }

        public void Stagger(float seconds)
        {
            if (_state == State.Dead) return;
            _state = State.Staggered;
            _timer = seconds;
            HideTelegraph();
        }

        private void Update()
        {
            if (_state == State.Dead || _target == null) return;
            float dt = Time.deltaTime;
            _timer -= dt;

            switch (_state)
            {
                case State.Idle:
                    if (_timer <= 0f) _state = State.Chase;
                    break;

                case State.Chase:
                    ChaseTarget(dt);
                    break;

                case State.Telegraph:
                    if (_timer <= 0f) LandAttack();
                    break;

                case State.Recover:
                case State.Staggered:
                    if (_timer <= 0f) _state = State.Chase;
                    break;
            }
        }

        private void ChaseTarget(float dt)
        {
            Vector3 to = _target.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist <= Definition.AttackRange)
            {
                BeginTelegraph();
                return;
            }
            Vector3 dir = to / Mathf.Max(dist, 0.001f);
            Vector3 step = dir * (Definition.MoveSpeed * _aggression * dt);
            if (_body != null) _body.MovePosition(_body.position + step);
            else transform.position += step;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        private void BeginTelegraph()
        {
            _state = State.Telegraph;
            // Low ember makes enemies telegraph faster, but never below readability floor.
            float t = Definition.TelegraphTime / Mathf.Max(0.5f, _aggression);
            _timer = Mathf.Max(0.45f, t);
            ShowTelegraph();
        }

        private void LandAttack()
        {
            HideTelegraph();
            _state = State.Recover;
            _timer = Definition.AttackCooldown;
            if (_targetPlayer == null) return;
            Vector3 to = _target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > (Definition.AttackRange + 0.6f) * (Definition.AttackRange + 0.6f)) return;
            float power = Definition.BaseDamage * _depthScale;
            _targetPlayer.ReceiveHit(power, this);
        }

        private void BuildTelegraph()
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "TelegraphRing";
            var col = ring.GetComponent<Collider>();
            if (col != null) Destroy(col);
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            ring.transform.localScale = new Vector3(Definition.AttackRange * 2f, 0.02f, Definition.AttackRange * 2f);
            var ringRenderer = ring.GetComponent<Renderer>();
            var ringShader = Shader.Find("Unlit/Color");
            if (ringRenderer != null && ringShader != null)
            {
                ringRenderer.material = new Material(ringShader);
                ringRenderer.material.color = new Color(0.9f, 0.35f, 0.1f); // warning orange; "!" text carries the meaning too
            }
            var markerGo = new GameObject("TelegraphMarker");
            markerGo.transform.SetParent(transform, false);
            markerGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            _marker = markerGo.AddComponent<TextMesh>();
            _marker.text = "!";
            _marker.fontSize = 48;
            _marker.characterSize = 0.12f;
            _marker.anchor = TextAnchor.MiddleCenter;
            _marker.alignment = TextAlignment.Center;
            _marker.color = Color.white;
            // Unity 6: built-in Arial is gone; TextMesh needs an explicit font.
            _marker.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var markerRenderer = markerGo.GetComponent<MeshRenderer>();
            if (markerRenderer != null && _marker.font != null)
                markerRenderer.sharedMaterial = _marker.font.material;
            _telegraphRing = ring.transform;
            HideTelegraph();
        }

        private void ShowTelegraph()
        {
            if (_telegraphRing != null) _telegraphRing.gameObject.SetActive(true);
            if (_marker != null) _marker.gameObject.SetActive(true);
        }

        private void HideTelegraph()
        {
            if (_telegraphRing != null) _telegraphRing.gameObject.SetActive(false);
            if (_marker != null) _marker.gameObject.SetActive(false);
        }

        private void OnDied()
        {
            _state = State.Dead;
            HideTelegraph();
            DiedEvent?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Died -= OnDied;
            DiedEvent = null;
        }
    }
}
