using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class TutorialStage1SeesawWeightTrigger : MonoBehaviour
{
    [SerializeField] private TutorialStage1Seesaw seesaw;
    [SerializeField] private SeesawSide side = SeesawSide.Left;
    [SerializeField] private string requiredStaticPropertyKeyword = "heavy";
    [SerializeField] private float heavyLoadValue = 8f;
    [SerializeField] private float defaultLoadValue = 3f;
    [SerializeField] private float lightweightLoadValue = 0.5f;

    private readonly HashSet<Collider> _contacts = new HashSet<Collider>();
    private BoxCollider _triggerCollider;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Awake()
    {
        _triggerCollider = GetComponent<BoxCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        _contacts.Add(other);
        Refresh();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
        {
            return;
        }

        _contacts.Remove(other);
        Refresh();
    }

    private void OnTriggerStay(Collider other)
    {
        // Reflect runtime property changes while object stays on trigger.
        if (other != null && _contacts.Contains(other))
        {
            Refresh();
        }
    }

    private void FixedUpdate()
    {
        // Fallback scan so CharacterController / trigger callback edge-cases still register load.
        if (_triggerCollider == null)
        {
            _triggerCollider = GetComponent<BoxCollider>();
            if (_triggerCollider == null)
            {
                return;
            }
        }

        Vector3 worldCenter = transform.TransformPoint(_triggerCollider.center);
        Vector3 halfExtents = Vector3.Scale(_triggerCollider.size * 0.5f, transform.lossyScale);
        Collider[] overlaps = Physics.OverlapBox(worldCenter, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Ignore);

        _contacts.Clear();
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider c = overlaps[i];
            if (c == null || c.transform.IsChildOf(transform))
            {
                continue;
            }

            _contacts.Add(c);
        }

        Refresh();
    }

    private void OnDisable()
    {
        _contacts.Clear();
        if (seesaw != null)
        {
            seesaw.SetSideLoad(side, 0f);
        }
    }

    private float ResolveLoad(Collider other)
    {
        if (other == null)
        {
            return 0f;
        }

        ObjectPropertySet propertySet = other.GetComponentInParent<ObjectPropertySet>();
        if (propertySet != null)
        {
            string effectiveStatic = propertySet.GetEffectiveStaticPropertyId(Time.time);
            if (!string.IsNullOrWhiteSpace(effectiveStatic))
            {
                if (effectiveStatic.IndexOf(requiredStaticPropertyKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return heavyLoadValue;
                }

                if (effectiveStatic.IndexOf("light", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return lightweightLoadValue;
                }
            }
        }

        DebugInteractableCheckTarget debugTarget = other.GetComponentInParent<DebugInteractableCheckTarget>();
        if (debugTarget != null && !string.IsNullOrWhiteSpace(debugTarget.CurrentPropertyVisual))
        {
            if (debugTarget.CurrentPropertyVisual.IndexOf(requiredStaticPropertyKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return heavyLoadValue;
            }

            if (debugTarget.CurrentPropertyVisual.IndexOf("light", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return lightweightLoadValue;
            }
        }

        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            return Mathf.Max(defaultLoadValue, rb.mass);
        }

        return defaultLoadValue;
    }

    private void Refresh()
    {
        if (seesaw == null)
        {
            return;
        }

        float total = 0f;
        _contacts.RemoveWhere(c => c == null);
        foreach (Collider c in _contacts)
        {
            total += ResolveLoad(c);
        }

        seesaw.SetSideLoad(side, total);
    }
}
