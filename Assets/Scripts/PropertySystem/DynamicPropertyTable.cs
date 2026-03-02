using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DynamicPropertyTable", menuName = "Undefined/Property/Dynamic Property Table")]
public class DynamicPropertyTable : ScriptableObject
{
    [SerializeField] private DynamicPropertyDefinition[] properties = System.Array.Empty<DynamicPropertyDefinition>();

    private readonly Dictionary<string, DynamicPropertyDefinition> _cache = new Dictionary<string, DynamicPropertyDefinition>();
    private bool _cacheBuilt;

    public bool TryGetDefinition(string propertyId, out DynamicPropertyDefinition definition)
    {
        EnsureCache();
        return _cache.TryGetValue(propertyId ?? string.Empty, out definition);
    }

    public float ResolveInjectedDuration(string propertyId, float fallbackDuration)
    {
        if (TryGetDefinition(propertyId, out DynamicPropertyDefinition definition) && definition != null && definition.InjectedDurationOverride > 0f)
        {
            return definition.InjectedDurationOverride;
        }

        return fallbackDuration;
    }

    private void OnEnable()
    {
        _cacheBuilt = false;
    }

    private void OnValidate()
    {
        _cacheBuilt = false;
    }

    private void EnsureCache()
    {
        if (_cacheBuilt)
        {
            return;
        }

        _cache.Clear();
        for (int i = 0; i < properties.Length; i++)
        {
            DynamicPropertyDefinition property = properties[i];
            if (property == null || string.IsNullOrWhiteSpace(property.Id))
            {
                continue;
            }

            _cache[property.Id] = property;
        }

        _cacheBuilt = true;
    }
}
