using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// InferenceUI is responsible for displaying the predicted class and FPS on the screen.
/// </summary>
public class InferenceUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI predictedClassText;
    [SerializeField] private TextMeshProUGUI fpsText;
    [SerializeField] private Slider confidenceThresholdSlider;

    [Header("Settings")]
    [SerializeField, Tooltip("Time in seconds between refreshing fps value"), Range(0.01f, 1.0f)]
    private float fpsRefreshRate = 0.1f;
    [SerializeField, Tooltip("Option to display fps")]
    private bool displayFPS = true;

    private float minConfidence;
    private string className;
    private float confidenceScore;
    private bool modelLoaded;
    private float fpsTimer;

    /// <summary>
    /// Initializes the UI components and sets the confidence threshold.
    /// </summary>
    private void Start()
    {
        confidenceThresholdSlider.onValueChanged.AddListener(UpdateConfidenceThreshold);
        minConfidence = confidenceThresholdSlider.value;
    }

    /// <summary>
    /// Updates the UI with the provided class name, confidence score, and model load status.
    /// </summary>
    /// <param name="className">The predicted class name.</param>
    /// <param name="confidenceScore">The confidence score of the predicted class.</param>
    /// <param name="modelLoaded">Indicates whether the model is loaded.</param>
    public void UpdateUI(string className, float confidenceScore, bool modelLoaded)
    {
        this.className = className;
        this.confidenceScore = confidenceScore;
        this.modelLoaded = modelLoaded;

        UpdatePredictedClass();
    }

    /// <summary>
    /// Updates the FPS display if the displayFPS option is enabled.
    /// </summary>
    private void Update()
    {
        if (displayFPS)
        {
            UpdateFPS();
        }
    }

    /// <summary>
    /// Updates the displayed predicted class and its confidence score.
    /// </summary>
    private void UpdatePredictedClass()
    {
        string labelText = $"{className} {(confidenceScore * 100).ToString("0.##")}%";
        if (confidenceScore < minConfidence) labelText = "None";

        string content = modelLoaded ? $"Predicted Class: {labelText}" : "Loading Model...";
        predictedClassText.text = content;
    }

    /// <summary>
    /// Updates the displayed FPS value.
    /// </summary>
    private void UpdateFPS()
    {
        if (Time.unscaledTime > fpsTimer)
        {
            int fps = (int)(1f / Time.unscaledDeltaTime);
            fpsText.text = $"FPS: {fps}";

            fpsTimer = Time.unscaledTime + fpsRefreshRate;
        }
    }

    /// <summary>
    /// Updates the minimum confidence threshold for displaying the predicted class.
    /// </summary>
    /// <param name="value">The new minimum confidence threshold value.</param>
    private void UpdateConfidenceThreshold(float value)
    {
        minConfidence = value;
    }
}

