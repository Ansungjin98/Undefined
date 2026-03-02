using System;
using UnityEngine;
using UnityEngine.Rendering;

public class TutorialStage1Safe : MonoBehaviour
{
    [SerializeField] private string hiddenCode = "1234";
    [SerializeField] private bool revealed;
    [SerializeField] private string promptWhenHidden = "Safe";
    [SerializeField] private string promptWhenRevealed = "Safe (Code visible)";
    [SerializeField] private GameObject hiddenNoteObject;
    [SerializeField, Range(0.05f, 0.8f)] private float transparencyAlpha = 0.2f;

    private MaterialRuntimeSnapshot[] _snapshots;

    public string GetInteractionPrompt()
    {
        return revealed ? promptWhenRevealed : promptWhenHidden;
    }

    private void Awake()
    {
        CacheMaterialSnapshots();
        ApplyVisualState();
    }

    public void OnPropertyInjected(string propertyId)
    {
        if (string.IsNullOrWhiteSpace(propertyId) ||
            propertyId.IndexOf("transparent", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return;
        }

        revealed = true;
        if (hiddenNoteObject != null)
        {
            hiddenNoteObject.SetActive(true);
        }

        ApplyVisualState();
        Debug.Log($"[TUTORIAL][Safe] Transparent property injected. Code revealed: {hiddenCode}");
    }

    public void OnInjectedPropertyExpired()
    {
        revealed = false;
        if (hiddenNoteObject != null)
        {
            hiddenNoteObject.SetActive(false);
        }

        ApplyVisualState();
    }

    private void CacheMaterialSnapshots()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        int count = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            count += renderers[i].materials.Length;
        }

        _snapshots = new MaterialRuntimeSnapshot[count];
        int write = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null)
                {
                    continue;
                }

                _snapshots[write++] = new MaterialRuntimeSnapshot(mat);
            }
        }

        if (write != _snapshots.Length)
        {
            Array.Resize(ref _snapshots, write);
        }
    }

    private void ApplyVisualState()
    {
        if (_snapshots == null || _snapshots.Length == 0)
        {
            return;
        }

        if (!revealed)
        {
            for (int i = 0; i < _snapshots.Length; i++)
            {
                _snapshots[i].Restore();
            }
            return;
        }

        for (int i = 0; i < _snapshots.Length; i++)
        {
            _snapshots[i].ApplyTransparent(transparencyAlpha);
        }
    }

    private struct MaterialRuntimeSnapshot
    {
        private readonly Material _material;
        private readonly bool _hasColor;
        private readonly bool _hasBaseColor;
        private readonly Color _color;
        private readonly Color _baseColor;
        private readonly int _renderQueue;
        private readonly float _srcBlend;
        private readonly float _dstBlend;
        private readonly float _zWrite;
        private readonly float _surface;

        public MaterialRuntimeSnapshot(Material material)
        {
            _material = material;
            _hasColor = material.HasProperty("_Color");
            _hasBaseColor = material.HasProperty("_BaseColor");
            _color = _hasColor ? material.GetColor("_Color") : Color.white;
            _baseColor = _hasBaseColor ? material.GetColor("_BaseColor") : Color.white;
            _renderQueue = material.renderQueue;
            _srcBlend = material.HasProperty("_SrcBlend") ? material.GetFloat("_SrcBlend") : 1f;
            _dstBlend = material.HasProperty("_DstBlend") ? material.GetFloat("_DstBlend") : 0f;
            _zWrite = material.HasProperty("_ZWrite") ? material.GetFloat("_ZWrite") : 1f;
            _surface = material.HasProperty("_Surface") ? material.GetFloat("_Surface") : 0f;
        }

        public void ApplyTransparent(float alpha)
        {
            if (_material == null)
            {
                return;
            }

            if (_hasColor)
            {
                Color c = _material.GetColor("_Color");
                c.a = alpha;
                _material.SetColor("_Color", c);
            }

            if (_hasBaseColor)
            {
                Color c = _material.GetColor("_BaseColor");
                c.a = alpha;
                _material.SetColor("_BaseColor", c);
            }

            _material.SetOverrideTag("RenderType", "Transparent");
            if (_material.HasProperty("_Surface"))
            {
                _material.SetFloat("_Surface", 1f);
            }
            if (_material.HasProperty("_SrcBlend"))
            {
                _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }
            if (_material.HasProperty("_DstBlend"))
            {
                _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            if (_material.HasProperty("_ZWrite"))
            {
                _material.SetFloat("_ZWrite", 0f);
            }
            _material.EnableKeyword("_ALPHABLEND_ON");
            _material.DisableKeyword("_ALPHATEST_ON");
            _material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _material.renderQueue = (int)RenderQueue.Transparent;
        }

        public void Restore()
        {
            if (_material == null)
            {
                return;
            }

            if (_hasColor)
            {
                _material.SetColor("_Color", _color);
            }
            if (_hasBaseColor)
            {
                _material.SetColor("_BaseColor", _baseColor);
            }

            _material.SetOverrideTag("RenderType", "Opaque");
            if (_material.HasProperty("_Surface"))
            {
                _material.SetFloat("_Surface", _surface);
            }
            if (_material.HasProperty("_SrcBlend"))
            {
                _material.SetFloat("_SrcBlend", _srcBlend);
            }
            if (_material.HasProperty("_DstBlend"))
            {
                _material.SetFloat("_DstBlend", _dstBlend);
            }
            if (_material.HasProperty("_ZWrite"))
            {
                _material.SetFloat("_ZWrite", _zWrite);
            }
            _material.DisableKeyword("_ALPHABLEND_ON");
            _material.DisableKeyword("_ALPHATEST_ON");
            _material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _material.renderQueue = _renderQueue;
        }
    }
}
