using UnityEngine;

public class ObjectPropertySet : MonoBehaviour
{
    [Header("Object Identity")]
    [SerializeField] private string objectId;

    [Header("Base Properties")]
    [SerializeField] private string baseStaticPropertyId;
    [SerializeField] private string baseDynamicPropertyId;

    [Header("Interaction State")]
    [SerializeField] private int defaultHoldableState = 0;
    [SerializeField] public int holdableState = 0;
    [SerializeField] public string PromptText = "E - 상호작용";
    [SerializeField] public bool CanReceiveInjectedProperty = true;
    [SerializeField] public bool IsDestructible = true;

    [Header("Runtime Injected State (Debug)")]
    [SerializeField] public bool IsInjectedProperty;
    [SerializeField] public string CurrentPropertyVisual = "Default";
    [SerializeField] private string injectedStaticPropertyId;
    [SerializeField] private string injectedDynamicPropertyId;
    [SerializeField] private float injectedStaticExpireAt;
    [SerializeField] private float injectedDynamicExpireAt;

    [Header("Compatibility Reflection Hooks")]
    [SerializeField] public string ExtractStaticPropertyId = string.Empty;
    [SerializeField] public string ExtractDynamicPropertyId = string.Empty;
    [SerializeField] public string ExtractStaticDisplayName = string.Empty;
    [SerializeField] public string ExtractDynamicDisplayName = string.Empty;

    public string ObjectId => objectId;
    public string BaseStaticPropertyId => baseStaticPropertyId;
    public string BaseDynamicPropertyId => baseDynamicPropertyId;
    public string InjectedStaticPropertyId => injectedStaticPropertyId;
    public string InjectedDynamicPropertyId => injectedDynamicPropertyId;

    private void Awake()
    {
        ResetRuntimeState();
        SyncCompatibilityExtractIds();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            holdableState = Mathf.Clamp(defaultHoldableState, 0, 1);
            SyncCompatibilityExtractIds();
        }
    }

    public string GetInteractionPrompt()
    {
        return PromptText;
    }

    public string GetEffectiveStaticPropertyId(float now)
    {
        if (HasInjectedStatic(now))
        {
            return injectedStaticPropertyId;
        }

        return baseStaticPropertyId;
    }

    public string GetEffectiveDynamicPropertyId(float now)
    {
        if (HasInjectedDynamic(now))
        {
            return injectedDynamicPropertyId;
        }

        return baseDynamicPropertyId;
    }

    public bool HasInjectedStatic(float now)
    {
        return !string.IsNullOrWhiteSpace(injectedStaticPropertyId) && now < injectedStaticExpireAt;
    }

    public bool HasInjectedDynamic(float now)
    {
        return !string.IsNullOrWhiteSpace(injectedDynamicPropertyId) && now < injectedDynamicExpireAt;
    }

    public bool TryInjectProperty(PropertyChannelType channelType, string propertyId, float duration)
    {
        if (!CanReceiveInjectedProperty)
        {
            return false;
        }

        float expireAt = Time.time + Mathf.Max(0f, duration);
        if (channelType == PropertyChannelType.Static)
        {
            injectedStaticPropertyId = propertyId;
            injectedStaticExpireAt = expireAt;
        }
        else
        {
            injectedDynamicPropertyId = propertyId;
            injectedDynamicExpireAt = expireAt;
        }

        IsInjectedProperty = HasInjectedStatic(Time.time) || HasInjectedDynamic(Time.time);
        CurrentPropertyVisual = propertyId;
        return true;
    }

    public bool TickExpireInjectedProperties(float now)
    {
        bool changed = false;

        if (!string.IsNullOrWhiteSpace(injectedStaticPropertyId) && now >= injectedStaticExpireAt)
        {
            injectedStaticPropertyId = string.Empty;
            injectedStaticExpireAt = 0f;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(injectedDynamicPropertyId) && now >= injectedDynamicExpireAt)
        {
            injectedDynamicPropertyId = string.Empty;
            injectedDynamicExpireAt = 0f;
            changed = true;
        }

        if (changed)
        {
            IsInjectedProperty = HasInjectedStatic(now) || HasInjectedDynamic(now);
            if (!IsInjectedProperty)
            {
                CurrentPropertyVisual = "Default";
            }
        }

        return changed;
    }

    public void RecomputeRuntimeState(StaticPropertyTable staticTable, DynamicPropertyTable dynamicTable, float now)
    {
        holdableState = Mathf.Clamp(defaultHoldableState, 0, 1);

        ApplyEffects(staticTable, GetEffectiveStaticPropertyId(now));
        ApplyEffects(dynamicTable, GetEffectiveDynamicPropertyId(now));
    }

    public void SetHoldableState(int value)
    {
        holdableState = value != 0 ? 1 : 0;
    }

    public void SetDefaultHoldableState(int value)
    {
        defaultHoldableState = value != 0 ? 1 : 0;
    }

    public void ResetRuntimeState()
    {
        holdableState = Mathf.Clamp(defaultHoldableState, 0, 1);
        injectedStaticPropertyId = string.Empty;
        injectedDynamicPropertyId = string.Empty;
        injectedStaticExpireAt = 0f;
        injectedDynamicExpireAt = 0f;
        IsInjectedProperty = false;
        CurrentPropertyVisual = "Default";
    }

    private void ApplyEffects(StaticPropertyTable table, string propertyId)
    {
        if (table == null || string.IsNullOrWhiteSpace(propertyId))
        {
            return;
        }

        if (!table.TryGetDefinition(propertyId, out StaticPropertyDefinition definition) || definition == null)
        {
            return;
        }

        PropertyEffectDefinition[] effects = definition.InjectEffects;
        for (int i = 0; i < effects.Length; i++)
        {
            PropertyEffectDefinition effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            effect.TryApply(this);
        }
    }

    private void ApplyEffects(DynamicPropertyTable table, string propertyId)
    {
        if (table == null || string.IsNullOrWhiteSpace(propertyId))
        {
            return;
        }

        if (!table.TryGetDefinition(propertyId, out DynamicPropertyDefinition definition) || definition == null)
        {
            return;
        }

        PropertyEffectDefinition[] effects = definition.InjectEffects;
        for (int i = 0; i < effects.Length; i++)
        {
            PropertyEffectDefinition effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            effect.TryApply(this);
        }
    }

    private void SyncCompatibilityExtractIds()
    {
        if (string.IsNullOrWhiteSpace(ExtractStaticPropertyId))
        {
            ExtractStaticPropertyId = baseStaticPropertyId;
        }

        if (string.IsNullOrWhiteSpace(ExtractDynamicPropertyId))
        {
            ExtractDynamicPropertyId = baseDynamicPropertyId;
        }
    }
}
