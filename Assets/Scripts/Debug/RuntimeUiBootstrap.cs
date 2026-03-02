using UnityEngine;
using UnityEngine.UI;

public static class RuntimeUiBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureCrosshairCanvas()
    {
        if (GameObject.Find("RuntimeCrosshairCanvas") != null)
        {
            return;
        }

        GameObject canvasGo = new GameObject("RuntimeCrosshairCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();
        Object.DontDestroyOnLoad(canvasGo);

        RectTransform root = canvasGo.GetComponent<RectTransform>();
        CreateCrosshairPart(root, new Vector2(-11f, 0f), new Vector2(8f, 2f));
        CreateCrosshairPart(root, new Vector2(11f, 0f), new Vector2(8f, 2f));
        CreateCrosshairPart(root, new Vector2(0f, -11f), new Vector2(2f, 8f));
        CreateCrosshairPart(root, new Vector2(0f, 11f), new Vector2(2f, 8f));
        CreateCrosshairPart(root, Vector2.zero, new Vector2(2f, 2f));
    }

    private static void CreateCrosshairPart(RectTransform parent, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject("CrosshairPart");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = Color.white;
    }
}
