using System.Collections.Generic;
using Emberfall.Core;
using Emberfall.Data;
using Emberfall.Gameplay;
using UnityEngine;

namespace Emberfall.Gameplay
{
    /// <summary>
    /// One component makes the game playable: builds a graybox arena, spawns the
    /// player and a seeded wave of enemies, and wires the HUD, inventory and
    /// pause screens. No scene authoring required for the vertical slice.
    /// Add to an empty GameObject in an empty scene and press Play.
    /// </summary>
    public sealed class GrayboxBootstrap : MonoBehaviour
    {
        public ItemDatabase Database;
        public int RunSeed = 20260214;
        public int SaveSlot = 0;

        private GameSession _session;
        private PlayerController _player;
        private GrayboxHud _hud;
        private Camera _camera;
        private Transform _enemyRoot;
        private bool _built;
        private int _waveIndex;
        private float _waveDelay;

        private void Start()
        {
            if (Database == null) Database = StarterContent.CreateRuntimeDatabase();
            BuildWorld();
            BuildPlayer();
            BuildSession();
            SpawnWave(0);
            _waveIndex = 0;
            BuildHud();
            _built = true;
        }

        private void BuildWorld()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            Paint(ground, new Color(0.16f, 0.15f, 0.18f));

            // Low walls so the arena reads as a room and keeps bodies inside.
            CreateWall(new Vector3(0f, 1f, 20f), new Vector3(40f, 2f, 1f));
            CreateWall(new Vector3(0f, 1f, -20f), new Vector3(40f, 2f, 1f));
            CreateWall(new Vector3(20f, 1f, 0f), new Vector3(1f, 2f, 40f));
            CreateWall(new Vector3(-20f, 1f, 0f), new Vector3(1f, 2f, 40f));

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            _camera = camGo.AddComponent<Camera>();
            _camera.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            camGo.AddComponent<AudioListener>();
            _camera.transform.position = new Vector3(0f, 16f, -10f);
            _camera.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.color = new Color(1f, 0.85f, 0.7f);
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var emberGlow = new GameObject("Ember Glow");
            var glow = emberGlow.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 14f;
            glow.intensity = 1.4f;
            glow.color = new Color(1f, 0.55f, 0.2f);
            emberGlow.transform.position = new Vector3(0f, 2f, 0f);

            _enemyRoot = new GameObject("Enemies").transform;
        }

        private static void CreateWall(Vector3 pos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            Paint(wall, new Color(0.22f, 0.2f, 0.24f));
        }

        private void BuildPlayer()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.transform.position = new Vector3(0f, 1f, 0f);
            Paint(go, new Color(0.9f, 0.6f, 0.25f));
            var body = go.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            _player = go.AddComponent<PlayerController>();
            _player.Attributes = Attributes.Default();
        }

        private void BuildSession()
        {
            _session = gameObject.AddComponent<GameSession>();
            _session.Begin(_player, Database, RunSeed);
            _session.SaveSlot = SaveSlot;
        }

        /// <summary>Spawns a wave from the seeded floor's encounter list.</summary>
        public void SpawnWave(int waveIndex)
        {
            if (_enemyRoot == null || Database == null || Database.Enemies.Count == 0) return;
            var rng = new System.Random(unchecked(RunSeed * 7919 + waveIndex * 104729));
            int count = 3 + Mathf.Min(4, waveIndex);
            float depth = CombatMath.DepthScaling(_session.FloorIndex + waveIndex);
            for (int i = 0; i < count; i++)
            {
                var def = Database.Enemies[rng.Next(Database.Enemies.Count)];
                SpawnEnemy(def, rng, depth);
            }
            var eliteRoll = waveIndex > 0 && waveIndex % 3 == 0;
            if (eliteRoll)
            {
                for (int i = 0; i < Database.Enemies.Count; i++)
                {
                    if (Database.Enemies[i] != null && Database.Enemies[i].IsElite)
                    {
                        SpawnEnemy(Database.Enemies[i], rng, depth);
                        break;
                    }
                }
            }
        }

        private void SpawnEnemy(EnemyDefinition def, System.Random rng, float depth)
        {
            float angle = (float)(rng.NextDouble() * 360.0);
            float radius = 8f + (float)(rng.NextDouble() * 8.0);
            Vector3 pos = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius, 1f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = def.DisplayName;
            go.transform.position = pos;
            go.transform.SetParent(_enemyRoot, true);
            Paint(go, def.IsElite ? new Color(0.75f, 0.2f, 0.5f) : new Color(0.45f, 0.5f, 0.6f));
            var body = go.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            var brain = go.AddComponent<EnemyBrain>();
            brain.Init(def, _player.transform, rng.Next(), depth);
            _session.RegisterEnemy(brain);
        }

        private void BuildHud()
        {
            _hud = gameObject.AddComponent<GrayboxHud>();
            _hud.Bind(_session, this);
        }

        private void Update()
        {
            if (!_built || _session == null || !_player.IsAlive || _hud != null && _hud.IsPaused) return;
            if (_session.LivingEnemies == 0)
            {
                _waveDelay += Time.deltaTime;
                if (_waveDelay >= 3f)
                {
                    _waveDelay = 0f;
                    _waveIndex++;
                    SpawnWave(_waveIndex);
                }
            }
            else _waveDelay = 0f;
        }

        private void LateUpdate()
        {
            if (!_built || _player == null || _camera == null) return;
            Vector3 target = _player.transform.position + new Vector3(0f, 16f, -10f);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        private static void Paint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            // new Material per object at spawn time only (never per frame).
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            renderer.material.color = color;
        }
    }
}
