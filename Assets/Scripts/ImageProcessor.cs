using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ImageProcessor class processes images using compute shaders or materials,
/// normalizing and resizing them according to the specified parameters.
/// </summary>
public class ImageProcessor : MonoBehaviour
{
    [Header("Processing Shaders")]
    [Tooltip("The compute shader for image processing")]
    [SerializeField] private ComputeShader processingShader;
    [Tooltip("The material for image processing")]
    [SerializeField] private Material processingMaterial;

    [Header("Normalization Parameters")]
    [Tooltip("JSON file with the mean and std values for normalization")]
    [SerializeField] private TextAsset normStatsJson = null;

    [Header("Data Processing")]
    [Tooltip("The target dimensions for the processed image")]
    [SerializeField] private int targetDim = 288;

    [System.Serializable]
    private class NormStats
    {
        public float[] mean;
        public float[] std;
    }

    // The mean values for normalization
    private float[] mean = new float[] { 0f, 0f, 0f };
    // The standard deviation values for normalization
    private float[] std = new float[] { 1f, 1f, 1f };

    // Buffer for mean values used in compute shader
    private ComputeBuffer meanBuffer;
    // Buffer for standard deviation values used in compute shader
    private ComputeBuffer stdBuffer;

    /// <summary>
    /// Called when the script is initialized.
    /// </summary>
    private void Start()
    {
        LoadNormStats();
        InitializeProcessingShaders();
    }

    /// <summary>
    /// Load the normalization stats from the provided JSON file.
    /// </summary>
    private void LoadNormStats()
    {
        if (IsNormStatsJsonNullOrEmpty())
        {
            return;
        }

        NormStats normStats = DeserializeNormStats(normStatsJson.text);
        UpdateNormalizationStats(normStats);
    }

    /// <summary>
    /// Check if the provided JSON file is null or empty.
    /// </summary>
    /// <returns>True if the file is null or empty, otherwise false.</returns>
    private bool IsNormStatsJsonNullOrEmpty()
    {
        return normStatsJson == null || string.IsNullOrWhiteSpace(normStatsJson.text);
    }

    /// <summary>
    /// Deserialize the provided JSON string to a NormStats object.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A deserialized NormStats object.</returns>
    private NormStats DeserializeNormStats(string json)
    {
        try
        {
            return JsonUtility.FromJson<NormStats>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to deserialize normalization stats JSON: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Update the mean and standard deviation with the provided NormStats object.
    /// </summary>
    /// <param name="normStats">The NormStats object containing the mean and standard deviation.</param>
    private void UpdateNormalizationStats(NormStats normStats)
    {
        if (normStats == null)
        {
            return;
        }

        mean = normStats.mean;
        std = normStats.std;
    }


    /// <summary>
    /// Initializes the processing shaders by setting the mean and standard deviation values.
    /// </summary>
    private void InitializeProcessingShaders()
    {
        processingMaterial.SetFloatArray("_Mean", mean);
        processingMaterial.SetFloatArray("_Std", std);

        if (SystemInfo.supportsComputeShaders)
        {
            int kernelIndex = processingShader.FindKernel("NormalizeImage");

            meanBuffer = CreateComputeBuffer(mean);
            stdBuffer = CreateComputeBuffer(std);

            processingShader.SetBuffer(kernelIndex, "_Mean", meanBuffer);
            processingShader.SetBuffer(kernelIndex, "_Std", stdBuffer);
        }
    }

    /// <summary>
    /// Creates a compute buffer and sets the provided data.
    /// </summary>
    /// <param name="data">The data to set in the compute buffer.</param>
    /// <returns>A compute buffer with the provided data.</returns>
    private ComputeBuffer CreateComputeBuffer(float[] data)
    {
        ComputeBuffer buffer = new ComputeBuffer(data.Length, sizeof(float));
        buffer.SetData(data);
        return buffer;
    }

    /// <summary>
    /// Processes an image using a compute shader with the specified function name.
    /// </summary>
    /// <param name="image">The image to be processed.</param>
    /// <param name="functionName">The name of the function in the compute shader to use for processing.</param>
    public void ProcessImageComputeShader(RenderTexture image, string functionName)
    {
        int kernelHandle = processingShader.FindKernel(functionName);
        RenderTexture result = GetTemporaryRenderTexture(image);

        BindTextures(kernelHandle, image, result);
        DispatchShader(kernelHandle, result);
        Graphics.Blit(result, image);

        RenderTexture.ReleaseTemporary(result);
    }

    /// <summary>
    /// Processes an image using a material.
    /// </summary>
    /// <param name="image">The image to be processed.</param>
    public void ProcessImageShader(RenderTexture image)
    {
        RenderTexture result = GetTemporaryRenderTexture(image, false);

        RenderTexture.active = result;
        Graphics.Blit(image, result, processingMaterial);
        Graphics.Blit(result, image);

        RenderTexture.ReleaseTemporary(result);
    }

    /// <summary>
    /// Creates a temporary render texture with the same dimensions as the given image.
    /// </summary>
    /// <param name="image">The image to match dimensions with.</param>
    /// <param name="enableRandomWrite">Enable random access write into the RenderTexture.</param>
    /// <returns>A temporary render texture.</returns>
    private RenderTexture GetTemporaryRenderTexture(RenderTexture image, bool enableRandomWrite=true)
    {
        RenderTexture result = RenderTexture.GetTemporary(image.width, image.height, 24, RenderTextureFormat.ARGBHalf);
        result.enableRandomWrite = enableRandomWrite;
        result.Create();
        return result;
    }

    /// <summary>
    /// Binds the source and destination textures to the compute shader.
    /// </summary>
    /// <param name="kernelHandle">The kernel handle of the compute shader.</param>
    /// <param name="source">The source texture to be processed.</param>
    /// <param name="destination">The destination texture for the processed result.</param>
    private void BindTextures(int kernelHandle, RenderTexture source, RenderTexture destination)
    {
        processingShader.SetTexture(kernelHandle, "_Result", destination);
        processingShader.SetTexture(kernelHandle, "_InputImage", source);
    }

    /// <summary>
    /// Dispatches the compute shader based on the dimensions of the result texture.
    /// </summary>
    /// <param name="kernelHandle">The kernel handle of the compute shader.</param>
    /// <param name="result">The result render texture.</param>
    private void DispatchShader(int kernelHandle, RenderTexture result)
    {
        int threadGroupsX = Mathf.CeilToInt((float)result.width / 8);
        int threadGroupsY = Mathf.CeilToInt((float)result.height / 8);
        processingShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);
    }

    /// <summary>
    /// Calculates the input dimensions of the processed image based on the original image dimensions.
    /// </summary>
    /// <param name="imageDims">The dimensions of the original image.</param>
    /// <returns>The calculated input dimensions for the processed image.</returns>
    public Vector2Int CalculateInputDims(Vector2Int imageDims)
    {
        targetDim = Mathf.Max(targetDim, 64);
        float scaleFactor = (float)targetDim / Mathf.Min(imageDims.x, imageDims.y);
        return Vector2Int.RoundToInt(new Vector2(imageDims.x * scaleFactor, imageDims.y * scaleFactor));
    }

    /// <summary>
    /// Called when the script is disabled.
    /// </summary>
    private void OnDisable()
    {
        ReleaseComputeBuffers();
    }

    /// <summary>
    /// Releases the compute buffers if compute shaders are supported.
    /// </summary>
    private void ReleaseComputeBuffers()
    {
        if (SystemInfo.supportsComputeShaders)
        {
            meanBuffer?.Release();
            stdBuffer?.Release();
        }
    }
}
