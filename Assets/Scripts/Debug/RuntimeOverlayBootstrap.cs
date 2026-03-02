using UnityEngine;
using UnityEngine.SceneManagement;

public static class RuntimeOverlayBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureOverlay()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        ManagerSpecRuntimeHUD existingHud = Object.FindFirstObjectByType<ManagerSpecRuntimeHUD>();
        SimpleCrosshairOverlay existingCrosshair = Object.FindFirstObjectByType<SimpleCrosshairOverlay>();
        if (existingHud != null && existingCrosshair != null)
        {
            return;
        }

        GameObject root = GameObject.Find("RuntimeOverlays");
        if (root == null)
        {
            root = new GameObject("RuntimeOverlays");
        }

        if (existingHud == null)
        {
            root.AddComponent<ManagerSpecRuntimeHUD>();
        }

        if (existingCrosshair == null)
        {
            root.AddComponent<SimpleCrosshairOverlay>();
        }
    }
}
