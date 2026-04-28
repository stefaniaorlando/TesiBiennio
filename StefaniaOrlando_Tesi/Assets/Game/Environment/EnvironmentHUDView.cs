using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * Read-only on-screen HUD that mirrors the four EnvironmentManager values.
 * Each row is a Slider (range pulled from EnvironmentConfig) plus a numeric label.
 *
 * Setup: drop on a Canvas, assign the EnvironmentManager and the four
 * slider/label pairs. Set each Slider's Interactable to false in the
 * Inspector if you only want display.
 */

[DisallowMultipleComponent]
public class EnvironmentHUDView : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public Slider slider;
        public TMP_Text valueLabel;
    }

    [SerializeField] private EnvironmentManager environment;

    [SerializeField] private Row temperature;
    [SerializeField] private Row humidity;
    [SerializeField] private Row toxicity;
    [SerializeField] private Row light;

    [SerializeField, Range(0, 3)] private int decimals = 1;

    private void OnEnable()
    {
        if (environment == null)
        {
            Debug.LogError($"{nameof(EnvironmentHUDView)} has no {nameof(EnvironmentManager)} assigned.", this);
            enabled = false;
            return;
        }

        var c = environment.Config;
        ConfigureRange(temperature.slider, c.temperature);
        ConfigureRange(humidity.slider,    c.humidity);
        ConfigureRange(toxicity.slider,    c.toxicity);
        ConfigureRange(light.slider,       c.light);
    }

    private void Update()
    {
        Apply(temperature, environment.Temperature);
        Apply(humidity,    environment.Humidity);
        Apply(toxicity,    environment.Toxicity);
        Apply(light,       environment.Light);
    }

    private void Apply(Row row, float value)
    {
        if (row.slider != null) row.slider.SetValueWithoutNotify(value);
        if (row.valueLabel != null) row.valueLabel.text = value.ToString($"F{decimals}");
    }

    private static void ConfigureRange(Slider slider, EnvironmentVariableConfig v)
    {
        if (slider == null) return;
        slider.minValue = v.minValue;
        slider.maxValue = v.maxValue;
        slider.wholeNumbers = false;
    }
}
