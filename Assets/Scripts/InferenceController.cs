using System;
using System.Linq;
using UnityEngine;
using CJM.BarracudaInference.ImageClassification;
using CJM.DeepLearningImageProcessor;

/// <summary>
/// The InferenceController class manages the process of running the inference on the input image
/// and updating the UI with the results.
/// </summary>
public class InferenceController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private ImageProcessor imageProcessor;
    [SerializeField] private MultiClassImageClassifier modelRunner;
    [SerializeField] private InferenceUI inferenceUI;

    [Header("Settings")]
    [SerializeField] private MeshRenderer screenRenderer;
    [SerializeField] private bool printDebugMessages = false;

    [Header("Data Processing")]
    [Tooltip("The target dimensions for the processed image")]
    [SerializeField] private int targetDim = 288;
    [Tooltip("Flag to use compute shaders for processing input images.")]
    [SerializeField] private bool useComputeShaders = false;

    // Output processing settings
    [Header("Output Processing")]
    [SerializeField, Tooltip("Flag to enable/disable async GPU readback for model output")]
    private bool useAsyncGPUReadback = false;

    private void Update()
    {
        if (!AreComponentsValid()) return;

        var imageTexture = screenRenderer.material.mainTexture;
        var screenDims = new Vector2Int(imageTexture.width, imageTexture.height);
        var inputDims = imageProcessor.CalculateInputDims(screenDims, targetDim);

        // Prepare and process the input texture
        RenderTexture inputTexture = PrepareInputTexture(inputDims);
        Graphics.Blit(imageTexture, inputTexture);
        ProcessInputImage(inputTexture);

        // Get the model output and process the detected objects
        float[] outputArray = GetModelOutput(inputTexture, useAsyncGPUReadback);
        UpdateUI(outputArray);
    }

    /// <summary>
    /// Checks if all required components are assigned and valid.
    /// </summary>
    /// <returns>True if all components are valid, false otherwise.</returns>
    private bool AreComponentsValid()
    {
        if (imageProcessor == null || modelRunner == null || inferenceUI == null)
        {
            Debug.LogError("InferenceController requires ImageProcessor, ModelRunner, and InferenceUI components.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Prepare a temporary RenderTexture with the given input dimensions.
    /// </summary>
    /// <param name="inputDims">The input dimensions for the RenderTexture</param>
    /// <returns>A temporary RenderTexture with the specified input dimensions</returns>
    private RenderTexture PrepareInputTexture(Vector2Int inputDims)
    {
        return RenderTexture.GetTemporary(inputDims.x, inputDims.y, 0, RenderTextureFormat.ARGBHalf);
    }

    /// <summary>
    /// Processes the input image using the image processor.
    /// </summary>
    /// <param name="inputTexture">The input texture to be processed.</param>
    private void ProcessInputImage(RenderTexture inputTexture)
    {
        if (SystemInfo.supportsComputeShaders && useComputeShaders)
        {
            imageProcessor.ProcessImageComputeShader(inputTexture, "NormalizeImage");
        }
        else
        {
            imageProcessor.ProcessImageShader(inputTexture);
        }
    }

    /// <summary>
    /// Get the model output either using async GPU readback or by copying the output to an array.
    /// </summary>
    /// <param name="inputTexture">The processed input RenderTexture</param>
    /// <param name="useAsyncReadback">Flag to indicate if async GPU readback should be used</param>
    /// <returns>An array of float values representing the model output</returns>
    private float[] GetModelOutput(RenderTexture inputTexture, bool useAsyncReadback)
    {
        // Run the model with the processed input texture
        modelRunner.ExecuteModel(inputTexture);
        RenderTexture.ReleaseTemporary(inputTexture);

        // Get the model output using async GPU readback or by copying the output to an array
        if (useAsyncReadback)
        {
            return modelRunner.CopyOutputWithAsyncReadback();
        }
        else
        {
            return modelRunner.CopyOutputToArray();
        }
    }

    /// <summary>
    /// Updates the UI with the results from the output array.
    /// </summary>
    /// <param name="outputArray">The output array from the model execution.</param>
    private void UpdateUI(float[] outputArray)
    {
        if (outputArray.Length <= 0) outputArray = new float[] { 0f };

        float confidenceScore = outputArray.Max();
        int classIndex = Array.IndexOf(outputArray, confidenceScore);
        bool modelLoaded = outputArray.Min() >= 0f && confidenceScore <= 1f;

        string className = modelRunner.GetClassName(classIndex);
        inferenceUI.UpdateUI(className, confidenceScore, modelLoaded);

        if (printDebugMessages) Debug.Log($"Output Array: {string.Join(", ", outputArray)}");
    }
}
