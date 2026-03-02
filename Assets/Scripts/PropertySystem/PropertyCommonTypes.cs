using System;
using UnityEngine;

public enum PropertyChannelType
{
    Static,
    Dynamic
}

public enum PropertyEffectKind
{
    None,
    SetHoldableState
}

[Serializable]
public class PropertyEffectDefinition
{
    [SerializeField] private PropertyEffectKind effectKind = PropertyEffectKind.None;
    [SerializeField] private int intValue;

    public PropertyEffectKind EffectKind => effectKind;
    public int IntValue => intValue;

    public bool TryApply(ObjectPropertySet target)
    {
        if (target == null)
        {
            return false;
        }

        switch (effectKind)
        {
            case PropertyEffectKind.SetHoldableState:
                target.SetHoldableState(intValue);
                return true;
            default:
                return false;
        }
    }
}

[Serializable]
public class StaticPropertyDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private bool extractable = true;
    [SerializeField] private float injectedDurationOverride = -1f;
    [SerializeField] private PropertyEffectDefinition[] injectEffects = Array.Empty<PropertyEffectDefinition>();

    public string Id => id;
    public string DisplayName => displayName;
    public bool Extractable => extractable;
    public float InjectedDurationOverride => injectedDurationOverride;
    public PropertyEffectDefinition[] InjectEffects => injectEffects;
}

[Serializable]
public class DynamicPropertyDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private bool extractable = true;
    [SerializeField] private float injectedDurationOverride = -1f;
    [SerializeField] private PropertyEffectDefinition[] injectEffects = Array.Empty<PropertyEffectDefinition>();

    public string Id => id;
    public string DisplayName => displayName;
    public bool Extractable => extractable;
    public float InjectedDurationOverride => injectedDurationOverride;
    public PropertyEffectDefinition[] InjectEffects => injectEffects;
}
