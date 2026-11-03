// 3D Space Shooter - self-building Unity game.
// Everything (ship, enemies, stars, HUD) is created from code at runtime.
// Just open the project in Unity and press Play.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SpaceShooter
{
    /// <summary>Works with both the old Input Manager and the new Input System.</summary>
    public static class GameInput
    {
        public static Vector2 Move()
        {
            float x = 0f, y = 0f;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;
            }
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
#endif
            return new Vector2(x, y);
        }

        public static bool Fire()
        {
#if ENABLE_INPUT_SYSTEM
            bool key = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            bool mouse = Mouse.current != null && Mouse.current.leftButton.isPressed;
            return key || mouse;
#else
            return Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
#endif
        }

        public static bool Restart()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        public static bool Pause()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.P);
#endif
        }
    }

    public class SpaceGame : MonoBehaviour
    {
        static SpaceGame instance;

        // Runs automatically when you press Play in ANY scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            new GameObject("Space Shooter Game").AddComponent<SpaceGame>();
        }

        // ---------- data ----------
        class Bullet
        {
            public Transform t;
            public Vector3 vel;
            public Vector3 prev;
            public float life;
            public bool hostile;
        }

        class Enemy
        {
            public Transform t;
            public Vector3 vel;
            public Vector3 spin;
            public float radius;
            public int hp;
            public bool ship;
            public float fireTimer;
            public int points;
            public Color explodeColor;
            public Renderer[] rends;
            public Color[] baseColors;
            public float flash;
        }

        class Particle
        {
            public Transform t;
            public Vector3 vel;
            public float life;
            public float maxLife;
            public Vector3 startScale;
        }

        // ---------- tuning ----------
        const float BoundX = 12f;
        const float BoundY = 7f;
        const float SpawnZ = 140f;
        const float DespawnZ = -25f;
        const float PlayerSpeed = 15f;
        const float FireRate = 0.13f;
        const float BulletSpeed = 110f;
        const int StarCount = 300;
        const int StartLives = 3;

        // ---------- state ----------
        Camera cam;
        Transform player;
        Transform engineGlow;
        Renderer[] playerRends;
        Transform[] stars;

        readonly List<Bullet> bullets = new List<Bullet>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Particle> particles = new List<Particle>();

        int score, lives, highScore;
        float fireCooldown, spawnTimer, invuln, elapsed, shake;
        float bank, pitch;
        bool gameOver, paused;
        Vector3 camSmooth;
        GUIStyle hud, big;

        float Difficulty { get { return 1f + elapsed / 45f; } }

        // ======================================================
        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;

            SetupScene();
            BuildPlayer();
            BuildStars();
            highScore = PlayerPrefs.GetInt("SS_HighScore", 0);
            ResetGame();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            Time.timeScale = 1f;
        }

        void SetupScene()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.04f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            cam.transform.position = new Vector3(0f, 2.5f, -13f);
            camSmooth = cam.transform.position;

            Light sun = FindFirstObjectByType<Light>();
            if (sun == null)
            {
                sun = new GameObject("Directional Light").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.96f, 0.9f);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.35f);
        }

        // ---------- primitive helper ----------
        static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool unlit = false)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // we do our own collision checks

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            var r = go.GetComponent<Renderer>();
            var m = r.material;
            if (unlit && GraphicsSettings.currentRenderPipeline == null)
            {
                var s = Shader.Find("Unlit/Color");
                if (s != null) m.shader = s;
            }
            m.color = color;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // ---------- builders ----------
        void BuildPlayer()
        {
            player = new GameObject("Player Ship").transform;

            var body = Prim(PrimitiveType.Capsule, player, Vector3.zero, new Vector3(0.8f, 1.4f, 0.8f), new Color(0.8f, 0.82f, 0.88f));
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, player, new Vector3(0f, 0.35f, 0.5f), new Vector3(0.5f, 0.4f, 0.9f), new Color(0.2f, 0.9f, 1f), true);
            Prim(PrimitiveType.Cube, player, new Vector3(0f, -0.1f, -0.2f), new Vector3(4f, 0.12f, 1.2f), new Color(0.3f, 0.4f, 0.6f));
            Prim(PrimitiveType.Cube, player, new Vector3(2f, 0.1f, -0.2f), new Vector3(0.25f, 0.5f, 1.4f), new Color(0.9f, 0.2f, 0.2f));
            Prim(PrimitiveType.Cube, player, new Vector3(-2f, 0.1f, -0.2f), new Vector3(0.25f, 0.5f, 1.4f), new Color(0.9f, 0.2f, 0.2f));
            Prim(PrimitiveType.Cube, player, new Vector3(0f, 0.5f, -1f), new Vector3(0.1f, 0.7f, 0.8f), new Color(0.3f, 0.4f, 0.6f));
            engineGlow = Prim(PrimitiveType.Sphere, player, new Vector3(0f, 0f, -1.5f), new Vector3(0.55f, 0.55f, 0.4f), new Color(1f, 0.6f, 0.1f), true).transform;

            playerRends = player.GetComponentsInChildren<Renderer>();
        }

        void BuildStars()
        {
            var root = new GameObject("Starfield").transform;
            stars = new Transform[StarCount];
            for (int i = 0; i < StarCount; i++)
            {
                float b = Random.Range(0.5f, 1f);
                var s = Prim(PrimitiveType.Cube, root,
                    new Vector3(Random.Range(-60f, 60f), Random.Range(-40f, 40f), Random.Range(-20f, 200f)),
                    new Vector3(0.07f, 0.07f, Random.Range(0.4f, 1.2f)),
                    new Color(b, b, Mathf.Min(1f, b + 0.1f)), true);
                stars[i] = s.transform;
            }
        }

        // ---------- game flow ----------
        void ResetGame()
        {
            foreach (var b in bullets) Destroy(b.t.gameObject);
            foreach (var e in enemies) Destroy(e.t.gameObject);
            bullets.Clear();
            enemies.Clear();

            score = 0;
            lives = StartLives;
            elapsed = 0f;
            spawnTimer = 1.5f;
            fireCooldown = 0f;
            invuln = 2f;
            shake = 0f;
            gameOver = false;
            paused = false;
            Time.timeScale = 1f;

            player.gameObject.SetActive(true);
            player.position = Vector3.zero;
            player.rotation = Quaternion.identity;
        }

        void Update()
        {
            if (!gameOver && GameInput.Pause())
            {
                paused = !paused;
                Time.timeScale = paused ? 0f : 1f;
            }
            if (paused) return;

            float dt = Time.deltaTime;

            UpdateStars(dt);
            UpdateParticles(dt);

            if (gameOver)
            {
                UpdateEnemies(dt, false);
                UpdateBullets(dt);
                UpdateCamera(dt);
                if (GameInput.Restart()) ResetGame();
                return;
            }

            elapsed += dt;
            UpdatePlayer(dt);
            UpdateSpawning(dt);
            UpdateEnemies(dt, true);
            UpdateBullets(dt);
            CheckCollisions();
            UpdateCamera(dt);
        }

        // ---------- player ----------
        void UpdatePlayer(float dt)
        {
            Vector2 mv = GameInput.Move();
            if (mv.sqrMagnitude > 1f) mv.Normalize();

            Vector3 p = player.position + new Vector3(mv.x, mv.y, 0f) * PlayerSpeed * dt;
            p.x = Mathf.Clamp(p.x, -BoundX, BoundX);
            p.y = Mathf.Clamp(p.y, -BoundY, BoundY);
            p.z = 0f;
            player.position = p;

            bank = Mathf.Lerp(bank, -mv.x * 35f, 8f * dt);
            pitch = Mathf.Lerp(pitch, -mv.y * 15f, 8f * dt);
            player.rotation = Quaternion.Euler(pitch, mv.x * 8f, bank);

            float flicker = 1f + Mathf.Sin(Time.time * 40f) * 0.15f;
            engineGlow.localScale = new Vector3(0.55f, 0.55f, 0.4f) * flicker;

            fireCooldown -= dt;
            if (GameInput.Fire() && fireCooldown <= 0f)
            {
                fireCooldown = FireRate;
                Vector3 fwd = Vector3.forward * BulletSpeed;
                SpawnBullet(player.TransformPoint(new Vector3(2f, 0.1f, 0.6f)), fwd, false);
                SpawnBullet(player.TransformPoint(new Vector3(-2f, 0.1f, 0.6f)), fwd, false);
            }

            if (invuln > 0f) invuln -= dt;
            bool visible = invuln <= 0f || Mathf.Repeat(invuln, 0.2f) > 0.1f;
            foreach (var r in playerRends) r.enabled = visible;
        }

        void SpawnBullet(Vector3 pos, Vector3 vel, bool hostile)
        {
            GameObject go;
            if (hostile)
            {
                go = Prim(PrimitiveType.Sphere, null, pos, Vector3.one * 0.6f, new Color(1f, 0.25f, 0.15f), true);
            }
            else
            {
                go = Prim(PrimitiveType.Cube, null, pos, new Vector3(0.12f, 0.12f, 1.8f), new Color(0.3f, 1f, 0.6f), true);
                go.transform.rotation = Quaternion.LookRotation(vel);
            }
            go.name = hostile ? "Enemy Bullet" : "Laser";
            bullets.Add(new Bullet { t = go.transform, vel = vel, prev = pos, life = hostile ? 5f : 1.6f, hostile = hostile });
        }

        // ---------- enemies ----------
        void UpdateSpawning(float dt)
        {
            spawnTimer -= dt;
            if (spawnTimer > 0f) return;

            spawnTimer = Mathf.Max(0.22f, 1.1f / Difficulty) * Random.Range(0.7f, 1.3f);
            float shipChance = Mathf.Min(0.15f + elapsed / 200f, 0.45f);
            if (Random.value < shipChance) SpawnShip();
            else SpawnAsteroid();
        }

        void SpawnAsteroid()
        {
            float size = Random.Range(1.2f, 3.5f);
            var root = new GameObject("Asteroid").transform;
            root.position = new Vector3(Random.Range(-BoundX - 3f, BoundX + 3f), Random.Range(-BoundY - 2f, BoundY + 2f), SpawnZ);

            float g = Random.Range(0.35f, 0.55f);
            Color c = new Color(g + 0.08f, g, g - 0.05f);
            Prim(PrimitiveType.Sphere, root, Vector3.zero,
                new Vector3(Random.Range(0.85f, 1.15f), Random.Range(0.85f, 1.15f), Random.Range(0.85f, 1.15f)) * size * 2f, c);
            for (int i = 0; i < 3; i++)
            {
                Prim(PrimitiveType.Sphere, root, Random.onUnitSphere * size * 0.6f, Vector3.one * size * Random.Range(0.7f, 1.1f), c * 0.9f);
            }

            Vector3 target = new Vector3(Random.Range(-BoundX, BoundX), Random.Range(-BoundY, BoundY), 0f);
            Vector3 vel = (target - root.position).normalized * Random.Range(18f, 28f) * Mathf.Sqrt(Difficulty);

            int hp = Mathf.CeilToInt(size * 1.5f);
            AddEnemy(root, vel, Random.insideUnitSphere * 90f, size, hp, false, 10 * Mathf.CeilToInt(size), new Color(1f, 0.6f, 0.2f));
        }

        void SpawnShip()
        {
            var root = new GameObject("Enemy Ship").transform;
            root.position = new Vector3(Random.Range(-BoundX, BoundX), Random.Range(-BoundY, BoundY), SpawnZ);

            Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(1.6f, 0.6f, 2.2f), new Color(0.6f, 0.1f, 0.15f));
            Prim(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.3f), new Vector3(4f, 0.15f, 1f), new Color(0.35f, 0.35f, 0.4f));
            Prim(PrimitiveType.Cube, root, new Vector3(2f, 0f, 0f), new Vector3(0.3f, 0.9f, 1.6f), new Color(0.6f, 0.1f, 0.15f));
            Prim(PrimitiveType.Cube, root, new Vector3(-2f, 0f, 0f), new Vector3(0.3f, 0.9f, 1.6f), new Color(0.6f, 0.1f, 0.15f));
            Prim(PrimitiveType.Sphere, root, new Vector3(0f, 0.35f, -0.6f), Vector3.one * 0.6f, new Color(1f, 0.9f, 0.2f), true);

            Vector3 vel = new Vector3(0f, 0f, -Random.Range(14f, 20f) * Mathf.Sqrt(Difficulty));
            var e = AddEnemy(root, vel, Vector3.zero, 1.8f, 3, true, 50, new Color(1f, 0.3f, 0.1f));
            e.fireTimer = Random.Range(0.8f, 1.8f);
        }

        Enemy AddEnemy(Transform t, Vector3 vel, Vector3 spin, float radius, int hp, bool ship, int points, Color explodeColor)
        {
            var rends = t.GetComponentsInChildren<Renderer>();
            var cols = new Color[rends.Length];
            for (int i = 0; i < rends.Length; i++) cols[i] = rends[i].material.color;

            var e = new Enemy
            {
                t = t, vel = vel, spin = spin, radius = radius, hp = hp, ship = ship,
                points = points, explodeColor = explodeColor, rends = rends, baseColors = cols
            };
            enemies.Add(e);
            return e;
        }

        void UpdateEnemies(float dt, bool active)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];

                if (e.ship)
                {
                    if (active)
                    {
                        Vector3 to = player.position - e.t.position;
                        e.vel.x = Mathf.MoveTowards(e.vel.x, Mathf.Clamp(to.x, -1f, 1f) * 6f, 6f * dt);
                        e.vel.y = Mathf.MoveTowards(e.vel.y, Mathf.Clamp(to.y, -1f, 1f) * 4f, 6f * dt);

                        e.fireTimer -= dt;
                        float z = e.t.position.z;
                        if (e.fireTimer <= 0f && z > 12f && z < 100f)
                        {
                            e.fireTimer = Random.Range(1.2f, 2.4f) / Mathf.Sqrt(Difficulty);
                            Vector3 aim = (player.position - e.t.position).normalized;
                            SpawnBullet(e.t.position + aim * 2f, aim * 35f, true);
                        }
                    }
                    e.t.rotation = Quaternion.Euler(0f, 0f, -e.vel.x * 5f);
                }
                else
                {
                    e.t.Rotate(e.spin * dt, Space.Self);
                }

                e.t.position += e.vel * dt;

                if (e.flash > 0f)
                {
                    e.flash -= dt;
                    bool on = e.flash > 0f;
                    for (int r = 0; r < e.rends.Length; r++)
                        e.rends[r].material.color = on ? Color.white : e.baseColors[r];
                }

                if (e.t.position.z < DespawnZ) RemoveEnemyAt(i);
            }
        }

        void RemoveEnemyAt(int i)
        {
            Destroy(enemies[i].t.gameObject);
            enemies.RemoveAt(i);
        }

        // ---------- bullets ----------
        void UpdateBullets(float dt)
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                b.prev = b.t.position;
                b.t.position += b.vel * dt;
                b.life -= dt;
                float z = b.t.position.z;
                if (b.life <= 0f || z > SpawnZ + 20f || z < DespawnZ - 10f) RemoveBulletAt(i);
            }
        }

        void RemoveBulletAt(int i)
        {
            Destroy(bullets[i].t.gameObject);
            bullets.RemoveAt(i);
        }

        // ---------- collisions ----------
        static float SqrDistPointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
            return (a + ab * t - p).sqrMagnitude;
        }

        void CheckCollisions()
        {
            // player lasers vs enemies (segment test so fast lasers never tunnel)
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                if (b.hostile) continue;

                for (int j = enemies.Count - 1; j >= 0; j--)
                {
                    var e = enemies[j];
                    float r = e.radius + 0.3f;
                    if (SqrDistPointSegment(e.t.position, b.prev, b.t.position) < r * r)
                    {
                        DamageEnemy(j);
                        RemoveBulletAt(i);
                        break;
                    }
                }
            }

            if (invuln > 0f) return;

            // enemies ramming the player
            Vector3 pp = player.position;
            for (int j = enemies.Count - 1; j >= 0; j--)
            {
                var e = enemies[j];
                float r = e.radius + 1f;
                if ((e.t.position - pp).sqrMagnitude < r * r)
                {
                    Explode(e.t.position, e.radius, e.explodeColor);
                    RemoveEnemyAt(j);
                    HitPlayer();
                    return;
                }
            }

            // enemy bullets vs player
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                if (!b.hostile) continue;
                if (SqrDistPointSegment(pp, b.prev, b.t.position) < 1.2f * 1.2f)
                {
                    RemoveBulletAt(i);
                    HitPlayer();
                    return;
                }
            }
        }

        void DamageEnemy(int j)
        {
            var e = enemies[j];
            e.hp--;
            e.flash = 0.06f;
            if (e.hp <= 0)
            {
                score += e.points;
                Explode(e.t.position, e.radius, e.explodeColor);
                shake = Mathf.Max(shake, 0.08f * e.radius);
                RemoveEnemyAt(j);
            }
        }

        void HitPlayer()
        {
            if (invuln > 0f || gameOver) return;

            lives--;
            shake = 0.6f;
            Explode(player.position, 1.5f, new Color(1f, 0.5f, 0.1f));

            if (lives <= 0)
            {
                gameOver = true;
                Explode(player.position, 3.5f, new Color(1f, 0.8f, 0.3f));
                player.gameObject.SetActive(false);
                if (score > highScore)
                {
                    highScore = score;
                    PlayerPrefs.SetInt("SS_HighScore", highScore);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                invuln = 2f;
            }
        }

        // ---------- effects ----------
        void Explode(Vector3 pos, float size, Color color)
        {
            int count = Mathf.RoundToInt(10 + size * 6f);
            for (int i = 0; i < count; i++)
            {
                Color c = Color.Lerp(color, new Color(1f, 1f, 0.6f), Random.value);
                float s = Random.Range(0.2f, 0.5f) * (0.5f + size * 0.3f);
                var go = Prim(PrimitiveType.Cube, null, pos + Random.insideUnitSphere * size * 0.5f, Vector3.one * s, c, true);
                go.transform.rotation = Random.rotation;
                float life = Random.Range(0.5f, 1.1f);
                particles.Add(new Particle
                {
                    t = go.transform,
                    vel = Random.onUnitSphere * Random.Range(4f, 14f) * (0.6f + size * 0.3f),
                    life = life,
                    maxLife = life,
                    startScale = go.transform.localScale
                });
            }
        }

        void UpdateParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.life -= dt;
                if (p.life <= 0f)
                {
                    Destroy(p.t.gameObject);
                    particles.RemoveAt(i);
                    continue;
                }
                p.t.position += p.vel * dt;
                p.vel *= Mathf.Max(0f, 1f - 2f * dt);
                p.t.Rotate(Vector3.one * 200f * dt);
                p.t.localScale = p.startScale * (p.life / p.maxLife);
            }
        }

        void UpdateStars(float dt)
        {
            float speed = 60f + Mathf.Min(elapsed, 120f) * 0.4f;
            for (int i = 0; i < stars.Length; i++)
            {
                var s = stars[i];
                Vector3 p = s.position;
                p.z -= speed * dt;
                if (p.z < -20f)
                {
                    p.z += 220f;
                    p.x = Random.Range(-60f, 60f);
                    p.y = Random.Range(-40f, 40f);
                }
                s.position = p;
            }
        }

        void UpdateCamera(float dt)
        {
            Vector3 pp = player.position;
            Vector3 target = new Vector3(pp.x * 0.6f, pp.y * 0.6f + 2.5f, -13f);
            camSmooth = Vector3.Lerp(camSmooth, target, 5f * dt);

            shake = Mathf.Max(0f, shake - dt);
            cam.transform.position = camSmooth + Random.insideUnitSphere * shake * 0.6f;
            cam.transform.LookAt(new Vector3(pp.x * 0.4f, pp.y * 0.4f, 20f));
        }

        // ---------- HUD ----------
        void OnGUI()
        {
            float s = Screen.height / 720f;
            if (hud == null)
            {
                hud = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                hud.normal.textColor = Color.white;
                big = new GUIStyle(hud) { alignment = TextAnchor.MiddleCenter };
            }
            hud.fontSize = Mathf.RoundToInt(24 * s);

            hud.alignment = TextAnchor.UpperLeft;
            GUI.Label(new Rect(20 * s, 15 * s, 500 * s, 40 * s), "SCORE  " + score, hud);
            GUI.Label(new Rect(20 * s, 45 * s, 500 * s, 40 * s), "BEST   " + highScore, hud);

            hud.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(Screen.width - 520 * s, 15 * s, 500 * s, 40 * s), "LIVES  " + Mathf.Max(lives, 0), hud);
            GUI.Label(new Rect(Screen.width - 520 * s, 45 * s, 500 * s, 40 * s), "TIME   " + Mathf.FloorToInt(elapsed) + "s", hud);

            if (!gameOver && elapsed < 6f)
            {
                hud.alignment = TextAnchor.LowerCenter;
                GUI.Label(new Rect(0, Screen.height - 60 * s, Screen.width, 40 * s),
                    "WASD / Arrows: move     Space / Left-click: shoot     P: pause", hud);
            }

            if (paused)
            {
                big.fontSize = Mathf.RoundToInt(60 * s);
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "PAUSED", big);
            }

            if (gameOver)
            {
                big.fontSize = Mathf.RoundToInt(72 * s);
                GUI.Label(new Rect(0, Screen.height * 0.5f - 120 * s, Screen.width, 100 * s), "GAME OVER", big);
                big.fontSize = Mathf.RoundToInt(30 * s);
                GUI.Label(new Rect(0, Screen.height * 0.5f - 20 * s, Screen.width, 50 * s), "Score: " + score + "     Best: " + highScore, big);
                GUI.Label(new Rect(0, Screen.height * 0.5f + 30 * s, Screen.width, 50 * s), "Press R to restart", big);
            }
        }
    }
}
