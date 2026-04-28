using UnityEngine;

/*
 * Central, pausable, scalable game-time clock.
 * Single source of time for all gameplay simulation systems
 * (environment drift, events, holobiont metabolism, etc.).
 *
 * Drop one in the scene. Other systems read GameClock.Instance.DeltaTime
 * (or .Time for accumulated seconds).
 *
 * DeltaTime is a computed property, so callers in Update get a fresh value
 * regardless of script execution order. Time is accumulated in this script's
 * own Update — fine for curve evaluation and similar low-precision needs.
 */

[DefaultExecutionOrder(-100)]
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    [Header("Runtime")]
    [SerializeField, Min(0f)] private float timeScale = 1f;
    [SerializeField] private bool paused;

    /// <summary>Accumulated game-time seconds. Stops when paused, scales with TimeScale.</summary>
    public float Time { get; private set; }

    /// <summary>Scaled delta for this frame. Zero while paused.</summary>
    public float DeltaTime => paused ? 0f : UnityEngine.Time.deltaTime * timeScale;

    public bool  IsPaused  { get => paused;    set => paused = value; }
    public float TimeScale { get => timeScale; set => timeScale = Mathf.Max(0f, value); }

    public void Pause()  => paused = true;
    public void Resume() => paused = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple {nameof(GameClock)} instances. Destroying duplicate on {name}.", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        Time += DeltaTime;
    }
}
