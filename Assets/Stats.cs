using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Records playstyle metrics for one play session and writes them to a readable JSON file.
///
/// - Creates itself automatically when the game starts (no need to put it in a scene) and
///   survives scene changes.
/// - Other scripts report events with e.g. Stats.Instance?.RecordJump().
/// - The file is written when the game quits, and also every time a checkpoint is reached
///   (so you still get data if the game crashes).
/// - Output folder: "PlaytestStats" next to the Assets folder in the editor, and next to the
///   .exe in a build. Falls back to Application.persistentDataPath if that folder isn't writable.
/// </summary>
public class Stats : MonoBehaviour
{
    public static Stats Instance { get; private set; }

    [Tooltip("Write the stats file to disk.")]
    public bool saveToFile = true;

    [Tooltip("How often (seconds) the player's position is sampled for the path/heatmap data. 0 = off.")]
    public float positionSampleInterval = 0.5f;

    private StatsData data = new StatsData();
    private string filePath;
    private float sessionStartTime;

    private Transform player;
    private Rigidbody2D playerRb;
    private Vector2 lastPlayerPos;
    private bool hasLastPlayerPos;
    private float nextPositionSample;

    private float currentSceneEnterTime;
    private float lastCheckpointTime;
    private int deathsSinceCheckpoint;
    private bool wasAirborne;

    // ---------------------------------------------------------------- setup

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null && FindObjectOfType<Stats>() == null)
        {
            new GameObject("[Stats]").AddComponent<Stats>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sessionStartTime = Time.time;
        lastCheckpointTime = 0f;

        DateTime now = DateTime.Now;
        data.sessionId = now.ToString("yyyy-MM-dd_HH-mm-ss");
        data.sessionStart = now.ToString("yyyy-MM-dd HH:mm:ss");
        data.gameVersion = Application.version;
        data.platform = Application.platform.ToString();
        data.isEditor = Application.isEditor;
        data.firstInputTime = -1f;

        filePath = BuildFilePath("Stats_" + data.sessionId + ".json");

        SceneManager.sceneLoaded += OnSceneLoaded;
        EnterScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private static string BuildFilePath(string fileName)
    {
        // Application.dataPath is "<project>/Assets" in the editor and "<Game>_Data" in a build,
        // so its parent is the project folder / the folder holding the .exe.
        string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "PlaytestStats");
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, ".write_test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
        catch (Exception)
        {
            folder = Path.Combine(Application.persistentDataPath, "PlaytestStats");
            Directory.CreateDirectory(folder);
        }
        return Path.Combine(folder, fileName);
    }

    // ---------------------------------------------------------------- per-frame tracking

    private void Update()
    {
        float dt = Time.deltaTime;
        float t = SessionTime;

        // First input of any kind
        if (data.firstInputTime < 0f && (Input.anyKeyDown || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f))
        {
            data.firstInputTime = t;
        }

        // Horizontal input / idle time
        float h = Input.GetAxisRaw("Horizontal");
        if (h > 0.1f) data.movement.timeHoldingRight += dt;
        else if (h < -0.1f) data.movement.timeHoldingLeft += dt;
        else data.movement.timeIdle += dt;

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D)) data.input.adKeyPresses++;

        TrackPlayer(dt, t);
    }

    private void TrackPlayer(float dt, float t)
    {
        if (player == null) return;

        Vector2 pos = player.position;

        if (hasLastPlayerPos)
        {
            float step = Vector2.Distance(pos, lastPlayerPos);
            // Ignore teleports (respawns) so they don't count as distance travelled
            if (step < 5f) data.movement.distanceTravelled += step;
        }
        lastPlayerPos = pos;
        hasLastPlayerPos = true;

        if (pos.y > data.movement.highestY) data.movement.highestY = pos.y;
        if (pos.x > data.movement.furthestX) data.movement.furthestX = pos.x;

        if (playerRb != null)
        {
            bool airborne = Mathf.Abs(playerRb.velocity.y) > 0.05f;
            if (airborne) data.movement.timeAirborne += dt;
            else data.movement.timeGrounded += dt;

            if (wasAirborne && !airborne) data.movement.landings++;
            wasAirborne = airborne;

            float speed = playerRb.velocity.magnitude;
            if (speed > data.movement.topSpeed) data.movement.topSpeed = speed;
        }

        if (positionSampleInterval > 0f && t >= nextPositionSample)
        {
            nextPositionSample = t + positionSampleInterval;
            data.positionSamples.Add(new PositionSample { time = Round(t), scene = CurrentScene, x = Round(pos.x), y = Round(pos.y) });
        }
    }

    // ---------------------------------------------------------------- public API (call from other scripts)

    /// <summary>The player calls this once so Stats can track movement.</summary>
    public void RegisterPlayer(Transform playerTransform)
    {
        player = playerTransform;
        playerRb = playerTransform.GetComponent<Rigidbody2D>();
        hasLastPlayerPos = false;
    }

    public void RecordJump()
    {
        data.input.jumps++;
    }

    public void RecordJumpAttemptWhileAirborne()
    {
        data.input.jumpPressesInAir++;
    }

    /// <summary>A burst was actually fired.</summary>
    public void RecordShotBurst(Vector2 direction, int bullets)
    {
        data.combat.burstsFired++;
        data.combat.bulletsFired += bullets;
        if (direction.y > 0.5f) data.combat.burstsUp++;
        else if (direction.y < -0.5f) data.combat.burstsDown++;
        else if (direction.x > 0.5f) data.combat.burstsRight++;
        else data.combat.burstsLeft++;
    }

    /// <summary>Shoot key pressed while still on cooldown.</summary>
    public void RecordShotOnCooldown()
    {
        data.combat.shootPressesOnCooldown++;
    }

    public void RecordEnemyKilled(string enemyType, Vector2 position)
    {
        data.combat.enemiesKilled++;
        data.combat.kills.Add(new KillEvent { time = Round(SessionTime), scene = CurrentScene, enemyType = enemyType, x = Round(position.x), y = Round(position.y) });
    }

    public void RecordDeath(string cause, Vector2 position)
    {
        data.deaths++;
        deathsSinceCheckpoint++;
        data.deathEvents.Add(new DeathEvent { time = Round(SessionTime), scene = CurrentScene, cause = cause, x = Round(position.x), y = Round(position.y) });
        hasLastPlayerPos = false; // respawn teleport
    }

    public void RecordCollectable(string itemName)
    {
        data.collectablesPicked++;
    }

    public void RecordCheckpoint(int index)
    {
        float t = SessionTime;
        data.checkpoints.Add(new CheckpointEvent
        {
            index = index,
            scene = CurrentScene,
            reachedAt = Round(t),
            timeSincePreviousCheckpoint = Round(t - lastCheckpointTime),
            deathsSincePreviousCheckpoint = deathsSinceCheckpoint,
        });
        lastCheckpointTime = t;
        deathsSinceCheckpoint = 0;
        Save(); // checkpoint = good moment for an autosave
    }

    public void RecordTimerFinished(bool ranOut, float timeLeft)
    {
        data.timerEvents.Add(new TimerEvent { time = Round(SessionTime), scene = CurrentScene, ranOut = ranOut, timeLeft = Round(timeLeft) });
    }

    /// <summary>Writes the current stats to disk. Safe to call any time.</summary>
    public void Save()
    {
        if (!saveToFile) return;

        FinalizeTotals();
        try
        {
            File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
            Debug.Log("[Stats] Saved to " + filePath);
        }
        catch (Exception e)
        {
            Debug.LogError("[Stats] Could not save stats: " + e.Message);
        }
    }

    // ---------------------------------------------------------------- scenes & saving

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) EnterScene(scene.name);
    }

    private void EnterScene(string sceneName)
    {
        CloseCurrentScene();
        currentSceneEnterTime = SessionTime;
        data.scenes.Add(new SceneVisit { scene = sceneName, enteredAt = Round(currentSceneEnterTime), timeSpent = 0f });
        hasLastPlayerPos = false;
    }

    private void CloseCurrentScene()
    {
        if (data.scenes.Count == 0) return;
        SceneVisit last = data.scenes[data.scenes.Count - 1];
        last.timeSpent = Round(SessionTime - currentSceneEnterTime);
    }

    private void FinalizeTotals()
    {
        data.sessionEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.totalPlayTime = Round(SessionTime);
        CloseCurrentScene();

        data.movement.distanceTravelled = Round(data.movement.distanceTravelled);
        data.movement.topSpeed = Round(data.movement.topSpeed);

        float total = Mathf.Max(SessionTime, 0.0001f);
        data.movement.percentTimeIdle = Round(100f * data.movement.timeIdle / total);
        data.movement.percentTimeAirborne = Round(100f * data.movement.timeAirborne / total);
        data.combat.shotsPerMinute = Round(data.combat.burstsFired / (total / 60f));
        data.deathsPerMinute = Round(data.deaths / (total / 60f));
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private float SessionTime => Time.time - sessionStartTime;
    private string CurrentScene => SceneManager.GetActiveScene().name;
    private static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    // ---------------------------------------------------------------- data layout (this is what ends up in the JSON)

    [Serializable]
    public class StatsData
    {
        public string sessionId;
        public string sessionStart;
        public string sessionEnd;
        public string gameVersion;
        public string platform;
        public bool isEditor;

        public float totalPlayTime;
        public float firstInputTime;

        public int deaths;
        public float deathsPerMinute;
        public int collectablesPicked;

        public InputStats input = new InputStats();
        public MovementStats movement = new MovementStats();
        public CombatStats combat = new CombatStats();

        public List<SceneVisit> scenes = new List<SceneVisit>();
        public List<CheckpointEvent> checkpoints = new List<CheckpointEvent>();
        public List<TimerEvent> timerEvents = new List<TimerEvent>();
        public List<DeathEvent> deathEvents = new List<DeathEvent>();
        public List<PositionSample> positionSamples = new List<PositionSample>();
    }

    [Serializable]
    public class InputStats
    {
        public int jumps;
        public int jumpPressesInAir;
        public int adKeyPresses;
    }

    [Serializable]
    public class MovementStats
    {
        public float distanceTravelled;
        public float topSpeed;
        public float highestY = float.MinValue;
        public float furthestX = float.MinValue;
        public float timeHoldingLeft;
        public float timeHoldingRight;
        public float timeIdle;
        public float percentTimeIdle;
        public float timeAirborne;
        public float timeGrounded;
        public float percentTimeAirborne;
        public int landings;
    }

    [Serializable]
    public class CombatStats
    {
        public int burstsFired;
        public int bulletsFired;
        public int burstsUp;
        public int burstsDown;
        public int burstsLeft;
        public int burstsRight;
        public int shootPressesOnCooldown;
        public float shotsPerMinute;
        public int enemiesKilled;
        public List<KillEvent> kills = new List<KillEvent>();
    }

    [Serializable] public class SceneVisit { public string scene; public float enteredAt; public float timeSpent; }
    [Serializable] public class CheckpointEvent { public int index; public string scene; public float reachedAt; public float timeSincePreviousCheckpoint; public int deathsSincePreviousCheckpoint; }
    [Serializable] public class TimerEvent { public float time; public string scene; public bool ranOut; public float timeLeft; }
    [Serializable] public class DeathEvent { public float time; public string scene; public string cause; public float x; public float y; }
    [Serializable] public class KillEvent { public float time; public string scene; public string enemyType; public float x; public float y; }
    [Serializable] public class PositionSample { public float time; public string scene; public float x; public float y; }
}
