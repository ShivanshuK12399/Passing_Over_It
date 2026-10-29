using UnityEngine;

/// <summary>
/// Sets platform-specific framerate, VSync, and graphics quality on startup.
/// </summary>
public static class FrameRateManager
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // Select the highest quality level available on this device
        if (QualitySettings.names.Length > 0)
        {
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        }

        // Disable VSync for true uncapped max FPS on PC/Editor and optimal timing on Mobile
        QualitySettings.vSyncCount = 0;

        if (Application.isMobilePlatform)
        {
            Application.targetFrameRate = 60;
        }
        else
        {
            Application.targetFrameRate = -1; // Max uncapped FPS on PC & Editor

#if UNITY_EDITOR
            // Force high-fidelity PC_RPAsset in Editor even if active build target is Android
            var pcAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pcAsset != null)
            {
                QualitySettings.renderPipeline = pcAsset;
            }
#endif
        }

        string currentQuality = QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "Default";
        Debug.Log($"[FrameRateManager] Platform: {(Application.isMobilePlatform ? "Mobile" : "PC/Editor")} | Target FPS: {Application.targetFrameRate} | VSync: {QualitySettings.vSyncCount} | Quality: {currentQuality}");
    }
}
