using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StaticPropertyTable", menuName = "Undefined/Property/Static Property Table")]
public class StaticPropertyTable : ScriptableObject
{
    [SerializeField] private StaticPropertyDefinition[] properties = System.Array.Empty<StaticPropertyDefinition>();

    private readonly Dictionary<string, StaticPropertyDefinition> _cache = new Dictionary<string, StaticPropertyDefinition>();
    private bool _cacheBuilt;

    public bool TryGetDefinition(string propertyId, out StaticPropertyDefinition definition)
    {
        EnsureCache();
        return _cache.TryGetValue(propertyId ?? string.Empty, out definition);
    }

    public float ResolveInjectedDuration(string propertyId, float fallbackDuration)
    {
        if (TryGetDefinition(propertyId, out StaticPropertyDefinition definition) && definition != null && definition.InjectedDurationOverride > 0f)
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
            StaticPropertyDefinition property = properties[i];
            if (property == null || string.IsNullOrWhiteSpace(property.Id))
            {
                continue;
            }

            _cache[property.Id] = property;
        }

        _cacheBuilt = true;
    }
}
