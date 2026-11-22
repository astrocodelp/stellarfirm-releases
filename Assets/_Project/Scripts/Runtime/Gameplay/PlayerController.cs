using Emberfall.Core;
using Emberfall.Data;
using UnityEngine;

namespace Emberfall.Gameplay
{
    /// <summary>
    /// Player movement + combat, driven by CombatState (pure, tested).
    /// Graybox input uses the legacy Input API so the loop is playable before
    /// the Input System asset exists; set Active Input Handling to "Both"
    /// until the rebinding UI ships (see README).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class PlayerController : MonoBehaviour
    {
        public Health Health { get; private set; }
        public CombatState Combat { get; private set; }
        public Attributes Attributes;
        public Inventory Inventory { get; private set; }
        public bool IsAlive { get { return Health != null && !Health.IsDead; } }
        public float FacingAngle { get; private set; }

        private float _focus;
        private float _maxFocus;
        private float _attackCooldown;
        private float _heavyPendingMultiplier;
        private float _dodgeTimer;
        private Vector3 _dodgeDir;
        private Rigidbody _body;
        private Transform _attackArc;
        private readonly Collider[] _hitBuffer = new Collider[16];
        private System.Random _rng = new System.Random();
        private float _moveSpeed = 5f;
        private bool _toggleBlockActive;
        private bool _toggleBlockSetting;

        public float Focus { get { return _focus; } }
        public float MaxFocus { get { return _maxFocus; } }

        public event System.Action<int, bool> DealtDamage;   // amount, wasCrit
        public event System.Action Died;

        public void Init(Attributes attributes, Inventory inventory, int seed)
        {
            Attributes = attributes;
            Inventory = inventory;
            _rng = new System.Random(seed);
            Health = GetComponent<Health>();
            RecalculateDerived(true);
            Health.Died += OnDied;
            _body = GetComponent<Rigidbody>();
            BuildAttackArc();
        }

        public void SetToggleBlock(bool enabled) { _toggleBlockSetting = enabled; }

        public void RecalculateDerived(bool refill)
        {
            float flatHp = Inventory != null ? Inventory.AffixTotal(AffixKind.FlatHealth) : 0f;
            float flatStam = Inventory != null ? Inventory.AffixTotal(AffixKind.FlatStamina) : 0f;
            float regenBonus = Inventory != null ? Inventory.AffixTotal(AffixKind.StaminaRegen) : 0f;
            Health.SetMax(DerivedStats.MaxHealth(Attributes, 1) + Mathf.RoundToInt(flatHp), refill);
            _maxFocus = DerivedStats.MaxFocus(Attributes);
            if (refill) _focus = _maxFocus;
            float maxStam = DerivedStats.MaxStamina(Attributes) + flatStam;
            Combat = new CombatState(maxStam, DerivedStats.StaminaRegen(Attributes) + regenBonus);
        }

        private void Update()
        {
            if (!IsAlive) return;
            float dt = Time.deltaTime;
            Combat.Tick(dt);
            _focus = Mathf.Min(_maxFocus, _focus + 2f * dt);
            if (_attackCooldown > 0f) _attackCooldown -= dt;

            HandleMovement(dt);
            HandleCombatInput();
            ResolvePendingHeavy();
        }

        private void HandleMovement(float dt)
        {
            Vector3 input = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) input.z += 1f;
            if (Input.GetKey(KeyCode.S)) input.z -= 1f;
            if (Input.GetKey(KeyCode.A)) input.x -= 1f;
            if (Input.GetKey(KeyCode.D)) input.x += 1f;
            if (input.sqrMagnitude > 1f) input.Normalize();

            if (_dodgeTimer > 0f)
            {
                _dodgeTimer -= dt;
                Move(_dodgeDir * (12f * dt));
                return;
            }
            if (input.sqrMagnitude > 0.0001f)
            {
                FacingAngle = Mathf.Atan2(input.x, input.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, FacingAngle, 0f);
                Move(input * (_moveSpeed * dt));
            }
        }

        private void Move(Vector3 delta)
        {
            if (_body != null) _body.MovePosition(_body.position + delta);
            else transform.position += delta;
        }

        private void HandleCombatInput()
        {
            // Block: hold Shift, or toggle with CapsLock when the setting is on.
            if (_toggleBlockSetting && Input.GetKeyDown(KeyCode.CapsLock))
                _toggleBlockActive = !_toggleBlockActive;
            bool blockHeld = Input.GetKey(KeyCode.LeftShift) || _toggleBlockActive;
            if (blockHeld && !Combat.IsBlocking) Combat.BeginParryWindow();
            Combat.SetBlocking(blockHeld);

            if (Input.GetKeyDown(KeyCode.Space) && Combat.TryDodge())
            {
                Vector3 dir = transform.forward;
                _dodgeDir = dir;
                _dodgeTimer = CombatState.DodgeIFrames;
            }

            if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J)) && _attackCooldown <= 0f)
            {
                float mult = Combat.TryLightAttack();
                if (mult > 0f)
                {
                    _attackCooldown = 0.35f;
                    SwingAttack(mult);
                }
            }
            if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.K)) && _attackCooldown <= 0f)
            {
                float mult = Combat.TryHeavyAttack();
                if (mult > 0f)
                {
                    _heavyPendingMultiplier = mult;
                    _attackCooldown = CombatState.HeavyWindup + 0.1f;
                }
            }
        }

        private void ResolvePendingHeavy()
        {
            if (_heavyPendingMultiplier <= 0f) return;
            if (!Combat.IsWindingUp)
            {
                SwingAttack(_heavyPendingMultiplier);
                _heavyPendingMultiplier = 0f;
            }
        }

        /// <summary>Spends focus to cast a skill. Returns false when unaffordable or on cooldown.</summary>
        public bool TryCastSkill(SkillDefinition skill, float[] cooldowns, int index)
        {
            if (skill == null || !IsAlive) return false;
            float reduction = Inventory != null ? Mathf.Clamp01(Inventory.AffixTotal(AffixKind.FocusCostReduction)) : 0f;
            float cost = skill.FocusCost * (1f - reduction);
            if (cooldowns[index] > 0f || _focus < cost) return false;
            _focus -= cost;
            cooldowns[index] = skill.Cooldown;
            switch (skill.Effect)
            {
                case SkillEffect.EmberSlash:
                    SwingAttack(1.2f * skill.Power, skill.Range);
                    break;
                case SkillEffect.CinderStep:
                    Move(transform.forward * skill.Range);
                    Combat.RestoreStamina(10f);
                    break;
                case SkillEffect.WardOfCinders:
                    Health.Heal(Mathf.RoundToInt(10f * skill.Power));
                    break;
            }
            return true;
        }

        private void SwingAttack(float comboMultiplier)
        {
            SwingAttack(comboMultiplier, 2.2f);
        }

        private void SwingAttack(float comboMultiplier, float range)
        {
            if (_attackArc != null)
            {
                _attackArc.localScale = new Vector3(range, 0.02f, range);
                _attackArc.gameObject.SetActive(true);
                _arcShowTimer = 0.12f;
            }
            float power = CurrentWeaponPower();
            float critChance = DerivedStats.CritChance(Attributes)
                + (Inventory != null ? Inventory.AffixTotal(AffixKind.CritChance) : 0f);
            int hits = Physics.OverlapSphereNonAlloc(
                transform.position + transform.forward * (range * 0.5f),
                range * 0.75f, _hitBuffer);
            for (int i = 0; i < hits; i++)
            {
                var enemy = _hitBuffer[i].GetComponentInParent<EnemyBrain>();
                if (enemy == null || enemy.Health.IsDead) continue;
                var req = new DamageRequest
                {
                    BasePower = power,
                    ComboMultiplier = comboMultiplier,
                    Variance = CombatMath.RollVariance(_rng),
                    IsCritical = CombatMath.RollCrit(_rng, critChance),
                    TargetArmour = enemy.Definition != null ? enemy.Definition.Armour : 0
                };
                var result = CombatMath.Resolve(req);
                enemy.Health.ApplyDamage(result.Amount);
                DealtDamage?.Invoke(result.Amount, result.WasCritical);
            }
        }

        private float _arcShowTimer;

        private void LateUpdate()
        {
            if (_arcShowTimer > 0f)
            {
                _arcShowTimer -= Time.deltaTime;
                if (_arcShowTimer <= 0f && _attackArc != null) _attackArc.gameObject.SetActive(false);
            }
        }

        private float CurrentWeaponPower()
        {
            var weapon = Inventory != null ? Inventory.GetEquipped(EquipSlot.Weapon, false) : null;
            float baseDmg = 10f;
            char str = 'C', dex = 'E';
            if (weapon != null && WeaponLookup != null)
            {
                var def = WeaponLookup(weapon.DefinitionId);
                if (def != null)
                {
                    baseDmg = def.BaseDamage;
                    str = string.IsNullOrEmpty(def.StrScaling) ? 'E' : def.StrScaling[0];
                    dex = string.IsNullOrEmpty(def.DexScaling) ? 'E' : def.DexScaling[0];
                }
            }
            return DerivedStats.WeaponPower(baseDmg, str, dex, Attributes);
        }

        /// <summary>Set by the session so the player can resolve weapon ids without holding the database.</summary>
        public System.Func<string, WeaponDefinition> WeaponLookup;
        /// <summary>Set by the session so armour base values resolve from ids.</summary>
        public System.Func<string, ItemDefinition> ItemLookup;

        private int TotalArmour()
        {
            if (Inventory == null) return 0;
            float total = Inventory.AffixTotal(AffixKind.FlatArmour);
            if (ItemLookup != null)
            {
                foreach (var kv in Inventory.Equipped)
                {
                    var def = ItemLookup(kv.Value.DefinitionId);
                    if (def != null) total += def.BaseArmour;
                }
                if (Inventory.SecondRing != null)
                {
                    var def = ItemLookup(Inventory.SecondRing.DefinitionId);
                    if (def != null) total += def.BaseArmour;
                }
            }
            return Mathf.RoundToInt(total);
        }

        /// <summary>Called by enemies. Dodge i-frames, parry and block reduce the hit.</summary>
        public void ReceiveHit(float basePower, EnemyBrain attacker)
        {
            float fraction = Combat.IncomingHitFraction();
            if (fraction <= 0f)
            {
                if (Combat.IsParrying && attacker != null)
                {
                    attacker.Stagger(2f);
                    _focus = Mathf.Min(_maxFocus, _focus + 8f);
                }
                return;
            }
            int armour = TotalArmour();
            var req = new DamageRequest
            {
                BasePower = basePower * fraction,
                ComboMultiplier = 1f,
                Variance = CombatMath.RollVariance(_rng),
                TargetArmour = armour
            };
            Health.ApplyDamage(CombatMath.Resolve(req).Amount);
        }

        private void BuildAttackArc()
        {
            var arc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arc.name = "AttackArc";
            var col = arc.GetComponent<Collider>();
            if (col != null) Destroy(col);
            arc.transform.SetParent(transform, false);
            arc.transform.localPosition = new Vector3(0f, -0.4f, 1.1f);
            arc.transform.localScale = new Vector3(2.2f, 0.02f, 2.2f);
            var arcRenderer = arc.GetComponent<Renderer>();
            var arcShader = Shader.Find("Unlit/Color");
            if (arcRenderer != null && arcShader != null)
            {
                arcRenderer.material = new Material(arcShader);
                arcRenderer.material.color = new Color(1f, 0.8f, 0.3f, 0.6f);
            }
            arc.gameObject.SetActive(false);
            _attackArc = arc.transform;
        }

        private void OnDied()
        {
            Died?.Invoke();
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Died -= OnDied;
            Died = null;
            DealtDamage = null;
        }
    }
}
