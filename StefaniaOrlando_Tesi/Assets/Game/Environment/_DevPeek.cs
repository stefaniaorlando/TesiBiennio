using UnityEngine;

// Throwaway: shows current env values in the Inspector at runtime.
// Delete (or just remove the component) once a real view exists.
[RequireComponent(typeof(EnvironmentManager))]
public class _DevPeek : MonoBehaviour
{
    public float temperature, humidity, toxicity, light;
    private EnvironmentManager env;

    private void Awake() => env = GetComponent<EnvironmentManager>();

    private void Update()
    {
        temperature = env.Temperature;
        humidity    = env.Humidity;
        toxicity    = env.Toxicity;
        light       = env.Light;
    }
}
