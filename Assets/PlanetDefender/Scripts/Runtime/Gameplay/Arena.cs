using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlanetDefender
{
    // The playfield: spawning, movement, combat, upgrades and passives for one run. Distance checks only (no
    // physics), one Update over flat lists, everything pooled. UI reads RunState; juice goes through events.
    public sealed class Arena : MonoBehaviour
    {
        public static readonly Vector2 PlanetCenter = new Vector2(0f, -0.55f);
        static readonly int FreezeId = Shader.PropertyToID("_PD_Freeze"), PlanetPosId = Shader.PropertyToID("_PD_PlanetPos");

        GameConfig config;
        GameAssets assets;
        VfxService vfx;
        CameraJuice juice;
        SpawnDirector director;
        public PlanetView Planet { get; private set; }
        public RunState Run { get; private set; }
        public PlayerStats Stats { get; private set; }

        readonly List<Meteor> meteors = new List<Meteor>();
        readonly Stack<Meteor> meteorPool = new Stack<Meteor>();
        readonly List<(Meteor m, float until)> dying = new List<(Meteor, float)>();
        readonly List<GoldPickup> golds = new List<GoldPickup>();
        readonly Stack<GoldPickup> goldPool = new Stack<GoldPickup>();
        readonly List<Bolt> bolts = new List<Bolt>();
        readonly Stack<Bolt> boltPool = new Stack<Bolt>();
        readonly List<Drone> drones = new List<Drone>();
        Transform meteorRoot, goldRoot, boltRoot;
        SpriteRenderer reticle, laser;

        float fireCooldown, freezeTimer, freezeLeft, neptuneTimer, neptuneLeft, shieldTimer, laserCharge, laserLeft, invulnerable;
        bool shieldUp;
        int nextMeteorId;
        float freezeVisual;
        public bool Firing { get; private set; }
        public Vector2 Aim { get; private set; } = Vector2.up;
        int ignoredTouch = -1;
        bool mousePressStartedOnUi;

        public bool InputEnabled { get; set; }
        public int ShotsFired { get; private set; }
        public bool ShieldUp => shieldUp;
        public float LaserReady => Stats == null || Stats.laserCooldown <= 0 ? -1 : Mathf.Clamp01(laserCharge / Stats.laserCooldown);
        public int MeteorCap => PlaygamaBridgeAdapter.IsMobile ? config.spawn.capMobile : config.spawn.capDesktop;
        public int AliveMeteors => meteors.Count;
        public IReadOnlyList<GoldPickup> Golds => golds;

        public event Action<Vector3, int> GoldLanded;
        public event Action PlanetDamaged;
        public event Action PlanetDestroyed;
        public event Action LevelReady;
        public event Action<float> ComboStep;
        public event Action<GoldPickup> GoldSpawned;
        public event Action MeteorKilled;

        public void Init(GameConfig config, GameAssets assets, VfxService vfx, CameraJuice juice)
        {
            this.config = config; this.assets = assets; this.vfx = vfx; this.juice = juice;
            director = new SpawnDirector(config, Environment.TickCount);
            meteorRoot = new GameObject("Meteors").transform; meteorRoot.SetParent(transform, false);
            goldRoot = new GameObject("Gold").transform; goldRoot.SetParent(transform, false);
            boltRoot = new GameObject("Bolts").transform; boltRoot.SetParent(transform, false);
            Planet = PlanetView.Create(assets, transform);
            Planet.transform.position = PlanetCenter;
            reticle = MakeSprite(transform, "Reticle", assets.crosshair, assets.reticle, 30);
            reticle.color = Palette.Plasma;
            reticle.transform.localScale = Vector3.one * 0.55f;
            reticle.gameObject.SetActive(false);
            laser = MakeSprite(transform, "Laser", assets.quad, assets.laser, 14);
            laser.gameObject.SetActive(false);
            Shader.SetGlobalVector(PlanetPosId, PlanetCenter);
            Shader.SetGlobalFloat(FreezeId, 0);
        }

        public void ShowPlanet(PlanetConfig planet, bool turret)
        {
            Planet.Setup(planet, assets.planetMaps[config.PlanetIndex(planet.id)], config.player.planetRadius, turret);
            Planet.UpdateLod(juice.Camera);
        }

        public void BeginRun(RunState run)
        {
            ClearField();
            Run = run;
            director.Reset();
            fireCooldown = 0; freezeTimer = 0; freezeLeft = 0; neptuneTimer = 0; neptuneLeft = 0; laserCharge = 0; laserLeft = 0; invulnerable = 0;
            ShotsFired = 0;
            ShowPlanet(run.planet, true);
            RecomputeStats();
            shieldUp = Stats.hasShield;
            shieldTimer = 0;
            Planet.SetShield(shieldUp, Palette.Gold, false);
        }

        public void RecomputeStats()
        {
            Stats = PlayerStats.Compute(config, Run);
            SyncDrones();
        }

        // Clears everything moving; used at run start, menu and after a revive (meteors near the planet only).
        public void ClearField()
        {
            for (int i = meteors.Count - 1; i >= 0; i--) Release(meteors[i], false);
            for (int i = golds.Count - 1; i >= 0; i--) ReleaseGold(golds[i]);
            for (int i = bolts.Count - 1; i >= 0; i--) ReleaseBolt(bolts[i]);
            foreach (var d in dying) FinishDying(d.m);
            dying.Clear();
            laser.gameObject.SetActive(false);
            laserLeft = 0;
            vfx.ClearAll();
            Shader.SetGlobalFloat(FreezeId, 0);
            freezeVisual = 0;
        }

        public void ReviveClear(float radius)
        {
            for (int i = meteors.Count - 1; i >= 0; i--)
            {
                var m = meteors[i];
                if (Vector2.Distance(m.position, PlanetCenter) > radius) continue;
                vfx.Play(assets.explosionSmall, m.position, m.radius / 0.48f);
                Release(m, true);
            }
            vfx.Play(assets.shockwave, PlanetCenter, radius * 0.9f, Palette.Plasma);
        }

        public void GrantInvulnerability(float seconds)
        {
            invulnerable = seconds;
            Planet.SetShield(true, Palette.Plasma);
        }

        void Update()
        {
            if (juice != null && juice.Camera != null) Planet.UpdateLod(juice.Camera);
            ReadInput();
        }

        // Automated checks drive the blaster without a pointer.
        public Vector2? DebugAim { get; set; }

        void ReadInput()
        {
            Firing = false;
            bool canAim = InputEnabled && Run != null && !TimeController.Frozen;
            reticle.gameObject.SetActive(false);
            if (!canAim) { ignoredTouch = -1; return; }
            if (DebugAim.HasValue && Application.isBatchMode) { Aim = DebugAim.Value.normalized; Planet.Aim(Aim); Firing = true; return; }
            var cam = juice.Camera;
            Vector2 screen;
            bool down = false, touch = false;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                touch = true;
                if (t.phase == TouchPhase.Began && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) ignoredTouch = t.fingerId;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { if (ignoredTouch == t.fingerId) ignoredTouch = -1; }
                screen = t.position;
                down = t.fingerId != ignoredTouch && t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            }
            else
            {
                screen = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) mousePressStartedOnUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                down = Input.GetMouseButton(0) && !mousePressStartedOnUi;
            }
            Vector2 world = cam.ScreenToWorldPoint(screen);
            Vector2 dir = world - PlanetCenter;
            if (dir.sqrMagnitude < 0.04f) dir = Aim;
            Aim = dir.normalized;
            Planet.Aim(Aim);
            Firing = down;
            // Touch: the reticle sits a little further out along the aim so the finger never covers the target.
            if (down || !touch)
            {
                reticle.gameObject.SetActive(true);
                reticle.transform.position = touch ? world + Aim * 0.9f : world;
            }
        }

        public void Tick(float dt)
        {
            if (Run == null || dt <= 0) return;
            Run.elapsed += dt;
            float time = Time.time;
            Rect view = ViewRect();

            // Spawning
            int toSpawn = director.TickMeteors(dt, Run.elapsed, meteors.Count, MeteorCap);
            for (int i = 0; i < toSpawn; i++) SpawnMeteor(director.PickMeteor(Run.elapsed), director.EdgePoint(view, config.spawn.edgeMargin), null);
            if (director.TickGold(dt)) SpawnGold(view);

            TickPassives(dt);
            float speedFactor = MeteorSpeedFactor();
            freezeVisual = Mathf.MoveTowards(freezeVisual, speedFactor < Stats.meteorSpeedMultiplier - 0.01f ? 1f : 0f, dt * 4f);
            Shader.SetGlobalFloat(FreezeId, freezeVisual);

            // Meteors
            float planetRadius = Planet.Radius;
            for (int i = meteors.Count - 1; i >= 0; i--)
            {
                var m = meteors[i];
                m.position += m.direction * (m.speed * speedFactor * dt);
                m.flash = Mathf.MoveTowards(m.flash, 0, dt * 7f);
                m.Apply();
                if ((m.position - PlanetCenter).sqrMagnitude <= Sq(planetRadius + m.radius * 0.65f)) ImpactPlanet(m);
            }

            // Gold
            for (int i = golds.Count - 1; i >= 0; i--)
            {
                var g = golds[i];
                g.position += g.direction * (g.speed * dt);
                g.Apply(time);
                if ((g.position - PlanetCenter).sqrMagnitude <= Sq(planetRadius + g.radius * 0.5f)) LandGold(g);
            }

            TickFiring(dt);
            TickDrones(dt);
            TickLaser(dt);
            TickBolts(dt, view);

            for (int i = dying.Count - 1; i >= 0; i--)
                if (Time.time >= dying[i].until) { FinishDying(dying[i].m); dying.RemoveAt(i); }

            if (Run.xp >= Run.XpNeeded) LevelReady?.Invoke();
        }

        void TickPassives(float dt)
        {
            if (invulnerable > 0)
            {
                invulnerable -= dt;
                if (invulnerable <= 0) Planet.SetShield(shieldUp, Palette.Gold);
            }
            if (Stats.hasShield && !shieldUp)
            {
                shieldTimer += dt;
                if (shieldTimer >= Stats.shieldPeriod)
                {
                    shieldUp = true; shieldTimer = 0;
                    if (invulnerable <= 0) Planet.SetShield(true, Palette.Gold);
                    AudioService.Instance?.Play(Sfx.Revive, 0.6f);
                }
            }
            if (Stats.freezeDuration > 0)
            {
                freezeTimer += dt;
                if (freezeTimer >= config.freezePulsePeriod) { freezeTimer = 0; freezeLeft = Stats.freezeDuration; SlowPulse(Palette.PlasmaSoft); }
            }
            if (Stats.hasSlowPulse)
            {
                neptuneTimer += dt;
                if (neptuneTimer >= Stats.slowPulsePeriod) { neptuneTimer = 0; neptuneLeft = Stats.slowPulseDuration; SlowPulse(Run.planet.AtmosphereColor); }
            }
            freezeLeft = Mathf.Max(0, freezeLeft - dt);
            neptuneLeft = Mathf.Max(0, neptuneLeft - dt);
        }

        void SlowPulse(Color color)
        {
            vfx.Play(assets.freezePulse, PlanetCenter, 1f, color);
            AudioService.Instance?.Play(Sfx.Freeze, 0.8f);
            juice.Chroma(0.35f, 0.4f);
            Planet.Pulse(0.06f);
        }

        // Venus, Freeze Pulse and Neptune stack multiplicatively, never past the GDD's hard slow cap.
        public float MeteorSpeedFactor()
        {
            float factor = Stats.meteorSpeedMultiplier;
            if (freezeLeft > 0) factor *= 1f - Stats.freezeSlow;
            if (neptuneLeft > 0) factor *= 1f - Stats.slowPulseAmount;
            return Mathf.Max(1f - config.caps.maxSlow, factor);
        }

        void TickFiring(float dt)
        {
            fireCooldown = Mathf.Max(-0.05f, fireCooldown - dt);
            if (!Firing) { fireCooldown = Mathf.Max(0, fireCooldown); return; }
            while (fireCooldown <= 0)
            {
                fireCooldown += Stats.fireInterval;
                FireVolley();
            }
        }

        void FireVolley()
        {
            Vector2 muzzle = Planet.Muzzle(Aim);
            if (Stats.shots <= 1) SpawnBolt(muzzle, Aim, Stats.damage, Stats.pierce, false, config.player.bulletSpeed);
            else
            {
                Vector2 side = new Vector2(-Aim.y, Aim.x);
                float half = Stats.doubleSpread * 0.5f;
                SpawnBolt(muzzle + side * Stats.doubleOffset, Rotate(Aim, half), Stats.damage, Stats.pierce, false, config.player.bulletSpeed);
                SpawnBolt(muzzle - side * Stats.doubleOffset, Rotate(Aim, -half), Stats.damage, Stats.pierce, false, config.player.bulletSpeed);
            }
            ShotsFired++;
            Planet.Recoil();
            vfx.Play(assets.muzzle, muzzle, 1f, null, Mathf.Atan2(Aim.y, Aim.x) * Mathf.Rad2Deg);
            AudioService.Instance?.Play(Sfx.Shot, 0.32f);
        }

        void TickBolts(float dt, Rect view)
        {
            Rect bounds = new Rect(view.xMin - 1.5f, view.yMin - 1.5f, view.width + 3f, view.height + 3f);
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                var b = bolts[i];
                b.position += b.velocity * dt;
                b.transform.position = b.position;
                if (!bounds.Contains(b.position)) { ReleaseBolt(b); continue; }
                bool consumed = false;
                for (int j = meteors.Count - 1; j >= 0 && !consumed; j--)
                {
                    if (j >= meteors.Count) continue;   // an explosion chain may have removed several meteors
                    var m = meteors[j];
                    if (m.id == b.lastHitId || m.id == b.lastHitId2) continue;
                    if ((m.position - b.position).sqrMagnitude > Sq(m.radius + b.radius)) continue;
                    b.lastHitId2 = b.lastHitId; b.lastHitId = m.id;
                    vfx.Play(assets.hitSpark, b.position, 0.8f, null, Mathf.Atan2(-b.velocity.y, -b.velocity.x) * Mathf.Rad2Deg);
                    Damage(m, b.damage, 0);
                    if (b.pierceLeft-- <= 0) consumed = true;
                }
                if (!consumed && !b.meteorOnly)
                {
                    for (int j = golds.Count - 1; j >= 0 && !consumed; j--)
                    {
                        var g = golds[j];
                        if ((g.position - b.position).sqrMagnitude > Sq(g.radius + b.radius)) continue;
                        ShatterGold(g);
                        if (b.pierceLeft-- <= 0) consumed = true;
                    }
                }
                if (consumed) ReleaseBolt(b);
            }
        }

        void TickDrones(float dt)
        {
            float orbit = Planet.Radius * 1.95f;
            foreach (var d in drones)
            {
                d.angle += dt * 1.5f;
                Vector2 pos = PlanetCenter + new Vector2(Mathf.Cos(d.angle), Mathf.Sin(d.angle)) * orbit;
                d.transform.position = pos;
                d.transform.localRotation = Quaternion.Euler(0, 0, Time.time * 240f);
                d.cooldown -= dt;
                if (d.cooldown > 0) continue;
                var target = NearestMeteor(config.droneRange);
                if (target == null) { d.cooldown = 0.1f; continue; }
                d.cooldown = Stats.droneInterval;
                Vector2 lead = target.position + target.direction * target.speed * MeteorSpeedFactor() * 0.25f;
                SpawnBolt(pos, (lead - pos).normalized, Stats.damage * 0.75f, 0, true, 12f);
                d.transform.DOKill(true);
                d.transform.DOPunchScale(Vector3.one * 0.25f, 0.15f, 5, 0.5f);
                AudioService.Instance?.Play(Sfx.DroneShot, 0.22f);
            }
        }

        // Charged beam (GDD §8): fires along the aim while the player is shooting, damages every meteor on its line.
        void TickLaser(float dt)
        {
            if (Stats.laserCooldown <= 0) { laser.gameObject.SetActive(false); return; }
            if (laserLeft > 0)
            {
                laserLeft -= dt;
                Vector2 start = Planet.Muzzle(Aim);
                const float length = 30f;
                laser.transform.position = start + Aim * (length * 0.5f);
                laser.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Aim.y, Aim.x) * Mathf.Rad2Deg);
                float fade = Mathf.Clamp01(laserLeft / 0.15f);
                laser.transform.localScale = new Vector3(length, config.laserWidth * 2.2f * fade, 1);
                for (int i = meteors.Count - 1; i >= 0; i--)
                {
                    if (i >= meteors.Count) continue;
                    var m = meteors[i];
                    Vector2 to = m.position - start;
                    float along = Vector2.Dot(to, Aim);
                    if (along < 0) continue;
                    float off = Mathf.Abs(to.x * Aim.y - to.y * Aim.x);
                    if (off > m.radius + config.laserWidth) continue;
                    Damage(m, Stats.laserDps * dt, 0, quiet: true);
                }
                if (laserLeft <= 0) laser.gameObject.SetActive(false);
                return;
            }
            laserCharge += dt;
            if (laserCharge >= Stats.laserCooldown && Firing && meteors.Count > 0)
            {
                laserCharge = 0;
                laserLeft = config.laserDuration;
                laser.gameObject.SetActive(true);
                AudioService.Instance?.Play(Sfx.Laser, 0.8f);
                juice.Shake(0.12f, 0.3f);
                juice.BloomPulse(0.6f, 0.4f);
            }
        }

        Meteor NearestMeteor(float range)
        {
            Meteor best = null;
            float bestD = range * range;
            foreach (var m in meteors)
            {
                float d = (m.position - PlanetCenter).sqrMagnitude;
                if (d < bestD) { bestD = d; best = m; }
            }
            return best;
        }

        void Damage(Meteor m, float amount, int depth, bool quiet = false)
        {
            if (!m.alive) return;
            m.hp -= amount;
            if (m.hp <= 0.0001f) { Kill(m, depth); return; }
            if (quiet) { m.flash = Mathf.Max(m.flash, 0.6f); return; }
            m.Hit();
            AudioService.Instance?.Play(Sfx.MeteorHit, 0.45f);
        }

        void Kill(Meteor m, int depth)
        {
            var cfg = m.config;
            float comboBefore = Run.Combo;
            Run.RegisterKill(config.combo);
            int points = Mathf.RoundToInt(cfg.score * Run.Combo);
            Run.score += points;
            Run.xp += Mathf.RoundToInt(cfg.xp * Stats.xpMultiplier);
            bool big = cfg.id == "big";
            float size = m.radius / 0.48f;
            vfx.Play(big ? assets.explosionBig : assets.explosionSmall, m.position, size);
            vfx.Play(assets.debris, m.position, size);
            vfx.Text(m.position, "+" + points, Run.Combo > 1.01f ? Palette.Plasma : Palette.Text, big ? 5.4f : 4.2f);
            if (Run.Combo > comboBefore + 0.01f) ComboStep?.Invoke(Run.Combo);
            AudioService.Instance?.Play(big ? Sfx.BigExplosion : Sfx.Explosion, big ? 0.9f : 0.55f);
            if (big) { TimeController.HitStop(0.07f); juice.Shake(0.28f, 0.35f); juice.Kick(0.3f); juice.Chroma(0.5f, 0.3f); }
            else if (cfg.id == "armored") { TimeController.HitStop(0.035f); juice.Shake(0.12f, 0.2f); }
            else juice.Shake(0.05f, 0.12f, 14);

            Vector2 pos = m.position, dir = m.direction;
            Release(m, true);
            MeteorKilled?.Invoke();

            if (cfg.splitInto > 0 && !string.IsNullOrEmpty(cfg.splitType))
            {
                var shard = config.Meteor(cfg.splitType);
                Vector2 side = new Vector2(-dir.y, dir.x);
                for (int i = 0; i < cfg.splitInto; i++)
                {
                    float s = i % 2 == 0 ? 1f : -1f;
                    var p = pos + side * (0.38f * s);
                    SpawnMeteor(shard, p, Rotate((PlanetCenter - p).normalized, 10f * s));
                }
            }

            if (Stats.explosionRadius > 0 && depth < 2)
            {
                vfx.Play(assets.shockwave, pos, Stats.explosionRadius, Palette.Meteor);
                for (int i = meteors.Count - 1; i >= 0; i--)
                {
                    if (i >= meteors.Count) continue;
                    var other = meteors[i];
                    if ((other.position - pos).sqrMagnitude <= Sq(Stats.explosionRadius + other.radius)) Damage(other, Stats.explosionDamage, depth + 1);
                }
            }
        }

        void ImpactPlanet(Meteor m)
        {
            Vector2 contact = PlanetCenter + (m.position - PlanetCenter).normalized * Planet.Radius;
            vfx.Play(assets.explosionSmall, contact, m.radius / 0.48f);
            Release(m, true);
            if (invulnerable > 0)
            {
                vfx.Play(assets.shieldBreak, contact, 0.6f, Palette.Plasma);
                return;
            }
            if (shieldUp)
            {
                shieldUp = false; shieldTimer = 0;
                Planet.SetShield(false, Palette.Gold);
                vfx.Play(assets.shieldBreak, PlanetCenter, Planet.Radius, Palette.Gold);
                vfx.Text(contact, Loc.T("shield_block"), Palette.Gold, 4f);
                AudioService.Instance?.Play(Sfx.ShieldBlock);
                juice.Shake(0.15f, 0.25f);
                return;
            }
            Run.hp = Mathf.Max(0, Run.hp - 1);
            Run.BreakCombo();
            Planet.Hit(Palette.Danger.Hdr(1.5f));  // material colour: HDR is kept
            vfx.Play(assets.planetHit, contact, 1f, null, Mathf.Atan2(contact.y - PlanetCenter.y, contact.x - PlanetCenter.x) * Mathf.Rad2Deg);
            AudioService.Instance?.Play(Sfx.PlanetHit);
            juice.Shake(0.38f, 0.4f);
            juice.DamageFlash();
            TimeController.HitStop(0.06f);
            PlanetDamaged?.Invoke();
            if (Run.hp <= 0) PlanetDestroyed?.Invoke();
        }

        void LandGold(GoldPickup g)
        {
            int amount = Mathf.RoundToInt(g.value * Stats.goldMultiplier);
            Run.sessionGold += amount;
            Run.goldEarned += amount;
            Vector3 at = g.position;
            vfx.Play(assets.goldPickup, at, g.radius / 0.42f);
            vfx.Text(at + Vector3.up * 0.3f, "+" + amount, Palette.Gold, 4.6f);
            AudioService.Instance?.Play(Sfx.GoldPickup, 0.9f);
            Planet.Pulse(0.05f);
            ReleaseGold(g);
            GoldLanded?.Invoke(at, amount);
        }

        void ShatterGold(GoldPickup g)
        {
            vfx.Play(assets.goldShatter, g.position, g.radius / 0.42f);
            vfx.Text(g.position, Loc.T("gold_lost"), Palette.GoldLight.WithAlpha(0.85f), 3.6f);
            AudioService.Instance?.Play(Sfx.GoldShatter, 0.8f);
            ReleaseGold(g);
        }

        // ---------------------------------------------------------------- spawning & pools

        void SpawnMeteor(MeteorConfig cfg, Vector2 position, Vector2? direction)
        {
            var m = meteorPool.Count > 0 ? meteorPool.Pop() : CreateMeteor();
            m.gameObject.SetActive(true);
            m.config = cfg;
            m.typeIndex = Array.IndexOf(config.meteors, cfg);
            m.hp = m.maxHp = cfg.hp;
            m.radius = cfg.radius;
            m.speed = config.spawn.baseSpeed * cfg.speedMult * director.SpeedMultiplier(Run.elapsed) * TravelScale(position);
            m.position = position;
            m.direction = direction ?? (PlanetCenter - position).normalized;
            m.seed = director.Range(0.02f, 0.98f);
            m.flash = 0;
            m.alive = true;
            m.id = ++nextMeteorId;
            m.spawnTime = Time.time;
            m.body.enabled = true;
            m.visual.localScale = Vector3.one;
            m.body.transform.localScale = Vector3.one * (m.radius * 2f * 1.25f);
            m.transform.position = position;
            ConfigureTrail(m);
            m.Apply();
            meteors.Add(m);
            if (direction.HasValue)
            {
                m.visual.localScale = Vector3.one * 0.3f;
                m.visual.DOScale(1f, 0.18f).SetEase(Ease.OutBack);
            }
        }

        void ConfigureTrail(Meteor m)
        {
            var t = m.trail;
            t.transform.localScale = Vector3.one * (m.radius / 0.48f);
            var main = t.main;
            bool fast = m.config.id == "fast", armored = m.config.id == "armored";
            main.startLifetime = fast ? 0.55f : 0.38f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                (fast ? new Color(1f, 0.85f, 0.45f) : armored ? new Color(1f, 0.55f, 0.3f) : new Color(1f, 0.62f, 0.25f)),
                new Color(1f, 0.25f, 0.08f));
            t.Clear();
            t.Play();
        }

        Meteor CreateMeteor()
        {
            var go = new GameObject("Meteor");
            go.transform.SetParent(meteorRoot, false);
            var m = go.AddComponent<Meteor>();
            m.visual = new GameObject("Visual").transform;
            m.visual.SetParent(go.transform, false);
            m.body = MakeSprite(m.visual, "Body", assets.quad, assets.meteor, 10);
            m.trail = Instantiate(assets.meteorTrail, go.transform);
            m.trail.transform.localPosition = Vector3.zero;
            return m;
        }

        // Killed meteors keep their trail alive for a moment so the smoke dissipates instead of popping.
        void Release(Meteor m, bool keepTrail)
        {
            m.alive = false;
            meteors.Remove(m);
            m.visual.DOKill();
            if (keepTrail)
            {
                m.body.enabled = false;
                m.trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                dying.Add((m, Time.time + 0.7f));
            }
            else FinishDying(m);
        }

        void FinishDying(Meteor m)
        {
            m.trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            m.gameObject.SetActive(false);
            meteorPool.Push(m);
        }

        void SpawnGold(Rect view)
        {
            var variant = director.PickGold();
            var g = goldPool.Count > 0 ? goldPool.Pop() : CreateGold();
            g.gameObject.SetActive(true);
            g.value = variant.value;
            g.radius = variant.radius;
            g.seed = director.Range(0, 1);
            g.position = director.EdgePoint(view, config.spawn.edgeMargin);
            g.direction = (PlanetCenter - g.position).normalized;
            g.speed = config.gold.speed * TravelScale(g.position);
            g.alive = true;
            g.body.transform.localScale = Vector3.one * (g.radius * 2f * 1.35f);
            g.halo.transform.localScale = Vector3.one * (g.radius * 4.2f);
            g.sparkle.transform.localScale = Vector3.one * (g.radius / 0.42f);
            g.sparkle.Clear(); g.sparkle.Play();
            g.Apply(Time.time);
            golds.Add(g);
            GoldSpawned?.Invoke(g);
        }

        GoldPickup CreateGold()
        {
            var go = new GameObject("Gold");
            go.transform.SetParent(goldRoot, false);
            var g = go.AddComponent<GoldPickup>();
            g.visual = new GameObject("Visual").transform;
            g.visual.SetParent(go.transform, false);
            g.halo = MakeSprite(go.transform, "Halo", assets.softCircle, assets.glow, 4);
            g.halo.color = Palette.Gold.WithAlpha(0.55f);
            g.body = MakeSprite(g.visual, "Body", assets.quad, assets.gold, 5);
            g.sparkle = Instantiate(assets.goldSparkle, go.transform);
            return g;
        }

        void ReleaseGold(GoldPickup g)
        {
            g.alive = false;
            golds.Remove(g);
            g.sparkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            g.gameObject.SetActive(false);
            goldPool.Push(g);
        }

        void SpawnBolt(Vector2 position, Vector2 direction, float damage, int pierce, bool meteorOnly, float speed)
        {
            var b = boltPool.Count > 0 ? boltPool.Pop() : CreateBolt();
            b.transform.gameObject.SetActive(true);
            b.position = position;
            b.velocity = direction * speed;
            b.damage = damage;
            b.pierceLeft = pierce;
            b.meteorOnly = meteorOnly;
            b.radius = meteorOnly ? config.player.bulletRadius * 0.8f : config.player.bulletRadius;
            b.lastHitId = b.lastHitId2 = -1;
            b.alive = true;
            b.transform.position = position;
            b.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            b.transform.localScale = meteorOnly ? new Vector3(1.0f, 0.55f, 1) : new Vector3(1.3f, 0.85f, 1);
            b.renderer.color = meteorOnly ? Palette.Xp : Palette.PlasmaSoft;
            bolts.Add(b);
        }

        Bolt CreateBolt()
        {
            var sr = MakeSprite(boltRoot, "Bolt", assets.bolt2D, assets.bolt, 15);
            return new Bolt { transform = sr.transform, renderer = sr };
        }

        void ReleaseBolt(Bolt b)
        {
            b.alive = false;
            bolts.Remove(b);
            b.transform.gameObject.SetActive(false);
            boltPool.Push(b);
        }

        void SyncDrones()
        {
            int want = Stats.drones;
            while (drones.Count < want)
            {
                var sr = MakeSprite(transform, "Drone", assets.star4, assets.glow, 12);
                sr.color = Palette.Xp;
                sr.transform.localScale = Vector3.one * 0.42f;
                var d = new Drone { transform = sr.transform, angle = drones.Count * Mathf.PI, cooldown = 0.5f };
                drones.Add(d);
                sr.transform.localScale = Vector3.zero;
                sr.transform.DOScale(0.42f, 0.4f).SetEase(Ease.OutBack);
            }
            while (drones.Count > want)
            {
                var d = drones[drones.Count - 1];
                Destroy(d.transform.gameObject);
                drones.RemoveAt(drones.Count - 1);
            }
            for (int i = 0; i < drones.Count; i++) drones[i].angle = i * Mathf.PI * 2f / Mathf.Max(1, drones.Count);
        }

        // Cover/screenshot staging: clear hit flashes so a frozen frame never shows a white-hot meteor.
        public void DebugCalm()
        {
            foreach (var m in meteors) { m.flash = 0; m.visual.DOKill(true); m.visual.localScale = Vector3.one; m.Apply(); }
        }

        public void DebugSpawn(string type, Vector2 position) => SpawnMeteor(config.Meteor(type), position, null);
        public void DebugSpawnGold(Vector2 position)
        {
            SpawnGold(ViewRect());
            var g = golds[golds.Count - 1];
            g.position = position;
            g.direction = (PlanetCenter - position).normalized;
            g.Apply(Time.time);
        }

        public void EndRun()
        {
            Run = null;
            Stats = null;
            Firing = false;
            InputEnabled = false;
            reticle.gameObject.SetActive(false);
            laser.gameObject.SetActive(false);
            while (drones.Count > 0) { Destroy(drones[0].transform.gameObject); drones.RemoveAt(0); }
        }

        public Rect ViewRect()
        {
            var cam = juice.Camera;
            float h = cam.orthographicSize, w = h * cam.aspect;
            return new Rect(-w, -h, w * 2, h * 2);
        }

        static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingOrder = order;
            return sr;
        }

        // Portrait screens put the side edges much closer than the top; blending speed toward equal travel time
        // keeps every approach readable without making the far ones crawl.
        float TravelScale(Vector2 from)
        {
            float distance = Mathf.Max(0.5f, Vector2.Distance(from, PlanetCenter) - config.player.planetRadius);
            return Mathf.Lerp(1f, distance / config.spawn.referenceDistance, config.spawn.travelNormalize);
        }

        static float Sq(float v) => v * v;
        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
