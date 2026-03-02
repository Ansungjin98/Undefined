using UnityEngine;

public class DebugInteractableCheckTarget : MonoBehaviour
{
    [Header("Interaction Metadata (reflection hook names)")]
    [SerializeField] public bool holdenable = true;
    [SerializeField] public int holdableState = 1;
    [SerializeField] public string PromptText = "E - Check interaction";

    [Header("Transfer Metadata (reflection hook names)")]
    [SerializeField] public bool CanReceiveInjectedProperty = true;
    [SerializeField] public bool IsDestructible = true;
    [SerializeField] public bool IsInjectedProperty = false;
    [SerializeField] public string CurrentPropertyVisual = "Default";
    [SerializeField] public string ExtractStaticPropertyId = "static:default";
    [SerializeField] public string ExtractDynamicPropertyId = "dynamic:default";
    [SerializeField] public string ExtractStaticDisplayName = "Default Static Property";
    [SerializeField] public string ExtractDynamicDisplayName = "Default Dynamic Property";
    [SerializeField] public string UnlockHoldableOnInjectKeyword = "lightweight";
    [SerializeField] public bool requireKeywordForHoldableUnlock = false;
    [SerializeField] public bool revertHoldableOnExpire = true;
    [SerializeField] public string injectedPromptOverride = "E - Pick up (lightweight applied)";

    [Header("Visual Check")]
    [SerializeField] private Color baseColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color staticPropertyColor = new Color(1f, 0.9f, 0.15f, 1f);
    [SerializeField] private Color dynamicPropertyColor = new Color(0.2f, 1f, 0.25f, 1f);
    [SerializeField] private Color transparentPropertyTint = new Color(1f, 0.95f, 0.25f, 0.22f);
    [SerializeField] private bool applyColorToRenderer = true;
    [SerializeField] private float fallbackVisualRevertDuration = 30f;
    [SerializeField] private float heavyMass = 8f;
    [SerializeField] private float lightweightMass = 0.5f;

    private Renderer[] _cachedRenderers;
    private bool _initialHoldenable;
    private string _initialPromptText;
    private string _initialPropertyVisual;
    private Rigidbody _cachedRigidbody;
    private float _initialMass;
    private bool _pendingTimedRevert;
    private float _pendingTimedRevertAt;

    private void Awake()
    {
        SyncHoldableStateFromBool();
        _initialHoldenable = holdenable;
        _initialPromptText = PromptText;
        _initialPropertyVisual = CurrentPropertyVisual;
        _cachedRigidbody = GetComponent<Rigidbody>();
        _initialMass = _cachedRigidbody != null ? _cachedRigidbody.mass : 0f;
        CacheRenderer();
        ApplyVisualState();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            SyncHoldableStateFromBool();
            CacheRenderer();
        }
    }

    private void Update()
    {
        if (!_pendingTimedRevert || Time.time < _pendingTimedRevertAt)
        {
            return;
        }

        _pendingTimedRevert = false;
        OnInjectedPropertyExpired();
        OnExtractedPropertyVisualExpired();
    }

    public string GetInteractionPrompt()
    {
        return PromptText;
    }

    public void HandlePrimaryInteraction()
    {
        Debug.Log($"[CHECK][Target:{name}] Primary interaction");
    }

    public void OpenDoor() { }
    public void ClimbLadder() { }

    public void HandleShakeInteraction()
    {
        Debug.Log($"[CHECK][Target:{name}] Shake");
    }

    public void HandleVerticalDragDummy()
    {
        Debug.Log($"[CHECK][Target:{name}] Vertical dummy drag");
    }

    public void HandleThrown()
    {
        Debug.Log($"[CHECK][Target:{name}] Thrown");
    }

    public void OnPropertyExtracted(string propertyId)
    {
        CurrentPropertyVisual = propertyId;
        ApplyColorForProperty(propertyId);
        Debug.Log($"[CHECK][Target:{name}] Property extracted: {propertyId}");
    }

    public void OnPropertyInjected(string propertyId)
    {
        IsInjectedProperty = true;
        CurrentPropertyVisual = propertyId;
        ScheduleFallbackRevert();
        ApplyPropertyGameplayState(propertyId);

        ApplyVisualState();
        Debug.Log($"[CHECK][Target:{name}] Property injected: {propertyId}");
    }

    public void OnExtractedPropertyLinkedToInjection(string propertyId)
    {
        CurrentPropertyVisual = propertyId;
        ScheduleFallbackRevert();
        ApplyColorForProperty(propertyId);
    }

    public void OnExtractedPropertyVisualExpired()
    {
        if (!IsInjectedProperty)
        {
            CurrentPropertyVisual = string.IsNullOrWhiteSpace(_initialPropertyVisual) ? "Default" : _initialPropertyVisual;
            ApplyVisualState();
        }
    }

    public void OnInjectedPropertyExpired()
    {
        IsInjectedProperty = false;
        CurrentPropertyVisual = string.IsNullOrWhiteSpace(_initialPropertyVisual) ? "Default" : _initialPropertyVisual;

        if (revertHoldableOnExpire)
        {
            holdenable = _initialHoldenable;
            holdableState = holdenable ? 1 : 0;
        }

        PromptText = _initialPromptText;
        RestoreMass();
        ApplyVisualState();
        Debug.Log($"[CHECK][Target:{name}] Injected property expired");
    }

    private void ScheduleFallbackRevert()
    {
        _pendingTimedRevert = true;
        _pendingTimedRevertAt = Time.time + Mathf.Max(0.1f, fallbackVisualRevertDuration);
    }

    private void ApplyPropertyGameplayState(string propertyId)
    {
        if (string.IsNullOrWhiteSpace(propertyId))
        {
            return;
        }

        bool isHeavy = propertyId.IndexOf("heavy", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool isLight = propertyId.IndexOf("light", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (isHeavy)
        {
            holdenable = false;
            holdableState = 0;
            PromptText = "E - Too heavy to carry";
            SetMassIfRigidbody(heavyMass);
            return;
        }

        if (isLight || CanUnlockHoldable(propertyId))
        {
            holdenable = true;
            holdableState = 1;
            if (!string.IsNullOrWhiteSpace(injectedPromptOverride))
            {
                PromptText = injectedPromptOverride;
            }
            SetMassIfRigidbody(lightweightMass);
        }
    }

    private void SetMassIfRigidbody(float mass)
    {
        if (_cachedRigidbody == null)
        {
            _cachedRigidbody = GetComponent<Rigidbody>();
            if (_cachedRigidbody != null && _initialMass <= 0f)
            {
                _initialMass = _cachedRigidbody.mass;
            }
        }

        if (_cachedRigidbody != null)
        {
            _cachedRigidbody.mass = Mathf.Max(0.01f, mass);
        }
    }

    private void RestoreMass()
    {
        if (_cachedRigidbody == null)
        {
            return;
        }

        float restore = _initialMass > 0f ? _initialMass : lightweightMass;
        _cachedRigidbody.mass = Mathf.Max(0.01f, restore);
    }

    private bool CanUnlockHoldable(string propertyId)
    {
        if (!requireKeywordForHoldableUnlock)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(UnlockHoldableOnInjectKeyword))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(propertyId)
            && propertyId.IndexOf(UnlockHoldableOnInjectKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SyncHoldableStateFromBool()
    {
        holdableState = holdenable ? 1 : 0;
    }

    private void CacheRenderer()
    {
        if (!applyColorToRenderer)
        {
            return;
        }

        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
        {
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private void ApplyVisualState()
    {
        if (IsInjectedProperty)
        {
            ApplyColorForProperty(CurrentPropertyVisual);
        }
        else
        {
            ApplyColor(baseColor);
        }
    }

    private void ApplyColorForProperty(string propertyId)
    {
        if (string.IsNullOrWhiteSpace(propertyId))
        {
            ApplyColor(baseColor);
            return;
        }

        if (propertyId.IndexOf("transparent", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            ApplyColor(transparentPropertyTint);
            return;
        }

        if (propertyId.StartsWith("static:", System.StringComparison.OrdinalIgnoreCase))
        {
            ApplyColor(staticPropertyColor);
            return;
        }

        if (propertyId.StartsWith("dynamic:", System.StringComparison.OrdinalIgnoreCase))
        {
            ApplyColor(dynamicPropertyColor);
            return;
        }

        ApplyColor(baseColor);
    }

    private void ApplyColor(Color color)
    {
        if (!Application.isPlaying)
        {
            // Avoid creating material instances in edit mode (Unity warning/leak).
            return;
        }

        if (!applyColorToRenderer)
        {
            return;
        }

        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
        {
            CacheRenderer();
        }

        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer renderer = _cachedRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] mats = renderer.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null)
                {
                    continue;
                }

                if (mats[m].HasProperty("_Color"))
                {
                    mats[m].SetColor("_Color", color);
                }

                if (mats[m].HasProperty("_BaseColor"))
                {
                    mats[m].SetColor("_BaseColor", color);
                }
            }
        }
    }
}
