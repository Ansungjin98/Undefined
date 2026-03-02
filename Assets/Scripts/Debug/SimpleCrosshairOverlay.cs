using UnityEngine;

public class SimpleCrosshairOverlay : MonoBehaviour
{
    [SerializeField] private bool showCrosshair = true;
    [SerializeField] private int crosshairSize = 40;
    [SerializeField] private int lineThickness = 4;
    [SerializeField] private int gap = 6;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.95f);
    [SerializeField] private bool drawCenterDot = true;
    private Texture2D _pixel;

    private void OnGUI()
    {
        if (!showCrosshair)
        {
            return;
        }

        EnsurePixel();

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        int arm = Mathf.Max(4, crosshairSize / 2);

        DrawRect(new Rect(cx - lineThickness * 0.5f + 1f, cy - gap - arm + 1f, lineThickness, arm), shadowColor);
        DrawRect(new Rect(cx - lineThickness * 0.5f + 1f, cy + gap + 1f, lineThickness, arm), shadowColor);
        DrawRect(new Rect(cx - gap - arm + 1f, cy - lineThickness * 0.5f + 1f, arm, lineThickness), shadowColor);
        DrawRect(new Rect(cx + gap + 1f, cy - lineThickness * 0.5f + 1f, arm, lineThickness), shadowColor);

        DrawRect(new Rect(cx - lineThickness * 0.5f, cy - gap - arm, lineThickness, arm), crosshairColor);
        DrawRect(new Rect(cx - lineThickness * 0.5f, cy + gap, lineThickness, arm), crosshairColor);
        DrawRect(new Rect(cx - gap - arm, cy - lineThickness * 0.5f, arm, lineThickness), crosshairColor);
        DrawRect(new Rect(cx + gap, cy - lineThickness * 0.5f, arm, lineThickness), crosshairColor);

        if (drawCenterDot)
        {
            DrawRect(new Rect(cx - 2f, cy - 2f, 4f, 4f), shadowColor);
            DrawRect(new Rect(cx - 1f, cy - 1f, 2f, 2f), crosshairColor);
        }
    }

    private void EnsurePixel()
    {
        if (_pixel != null)
        {
            return;
        }

        _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _pixel.SetPixel(0, 0, Color.white);
        _pixel.Apply();
    }

    private void DrawRect(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, _pixel);
        GUI.color = prev;
    }
}
