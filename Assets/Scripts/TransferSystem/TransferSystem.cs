using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public enum HandSide
{
    Left,
    Right
}

public enum TransferActionKind
{
    Extract,
    Inject
}

public interface ITransferInteractionGate
{
    bool CanTransferInteraction(out string reason);
}

[Serializable]
public class HandSlotState
{
    [SerializeField] private string propertyId;
    [SerializeField] private string displayName;
    [SerializeField] private bool isDynamicProperty;
    [SerializeField] private bool hasProperty;
    [SerializeField] private float storedAtTime;
    [SerializeField] private float expiresAtTime;

    public string PropertyId => propertyId;
    public string DisplayName => displayName;
    public bool IsDynamicProperty => isDynamicProperty;
    public bool HasProperty => hasProperty;
    public float StoredAtTime => storedAtTime;
    public float ExpiresAtTime => expiresAtTime;

    public void Set(string newPropertyId, string newDisplayName, bool dynamicProperty, float now, float holdDuration)
    {
        propertyId = newPropertyId;
        displayName = newDisplayName;
        isDynamicProperty = dynamicProperty;
        hasProperty = true;
        storedAtTime = now;
        expiresAtTime = now + holdDuration;
    }

    public void Clear()
    {
        propertyId = string.Empty;
        displayName = string.Empty;
        isDynamicProperty = false;
        hasProperty = false;
        storedAtTime = 0f;
        expiresAtTime = 0f;
    }
}

public class TransferSystem : MonoBehaviour
{
    [Header("Transfer Tuning")]
    [SerializeField] private float defaultExtractTime = 1.5f;
    [SerializeField] private float defaultInjectTime = 1.5f;
    [SerializeField] private float maxPropertyHoldTime = 600f;
    [SerializeField] private float defaultInjectedDuration = 30f;
    [SerializeField] private float channelBreakDistanceTolerance = 0.25f;

    [Header("Data / Managers")]
    [SerializeField] private UnityEngine.Object propertyDatabase;
    [SerializeField] private StaticPropertyTable staticPropertyTable;
    [SerializeField] private DynamicPropertyTable dynamicPropertyTable;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private InteractionManager interactionManager;

    [Header("Runtime")]
    [SerializeField] private HandSlotState leftHandState = new HandSlotState();
    [SerializeField] private HandSlotState rightHandState = new HandSlotState();

    public bool IsChanneling => _activeChannelRoutine != null;
    public HandSlotState LeftHandState => leftHandState;
    public HandSlotState RightHandState => rightHandState;
    public float CurrentChannelProgress01
    {
        get
        {
            if (!IsChanneling || _activeRequest.Duration <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01((Time.time - _activeRequest.StartedAtTime) / _activeRequest.Duration);
        }
    }
    public HandSide CurrentChannelHandSide => _activeRequest.HandSide;
    public TransferActionKind CurrentChannelActionKind => _activeRequest.ActionKind;

    public event Action<HandSide, bool> OnChannelingStateChanged;
    public event Action<HandSide, string> OnTransferFeedbackRequested;
    public event Action<HandSide> OnHandStateChanged;

    private Coroutine _activeChannelRoutine;
    private ChannelRequest _activeRequest;
    private readonly Dictionary<int, InjectedTargetState> _injectedTargetStates = new Dictionary<int, InjectedTargetState>();
    private readonly Dictionary<int, ObjectPropertySet> _trackedObjectPropertySets = new Dictionary<int, ObjectPropertySet>();
    private readonly Dictionary<HandSide, ExtractVisualLink> _extractVisualLinks = new Dictionary<HandSide, ExtractVisualLink>();

    private void Awake()
    {
        leftHandState ??= new HandSlotState();
        rightHandState ??= new HandSlotState();
        leftHandState.Clear();
        rightHandState.Clear();
    }

    private void OnEnable()
    {
        SubscribeInputEvents();
    }

    private void Update()
    {
        HandleHandExpiration(leftHandState, HandSide.Left);
        HandleHandExpiration(rightHandState, HandSide.Right);
        TickTrackedInjectedObjects();
    }

    private void OnDisable()
    {
        UnsubscribeInputEvents();
        CancelCurrentChanneling("disabled");
    }

    public void HandleTransferClick(MouseButtonType buttonType)
    {
        HandSide handSide = buttonType == MouseButtonType.Left ? HandSide.Left : HandSide.Right;
        if (IsChanneling)
        {
            EmitFeedback(handSide, "Transfer is already channeling.");
            return;
        }

        if (interactionManager == null)
        {
            interactionManager = FindFirstObjectByType<InteractionManager>();
        }

        if (interactionManager == null || !interactionManager.TryGetTransferTarget(out GameObject target))
        {
            EmitFeedback(handSide, "No transfer target found.");
            return;
        }

        if (TryHandleClickOnlyTarget(target, buttonType))
        {
            return;
        }

        if (!TryCheckTransferGate(target, out string gateReason))
        {
            EmitFeedback(handSide, string.IsNullOrWhiteSpace(gateReason) ? "Cannot transfer from current position." : gateReason);
            return;
        }

        bool isDynamic = handSide == HandSide.Right;
        HandSlotState handState = GetHandState(handSide);
        TransferActionKind kind = handState.HasProperty ? TransferActionKind.Inject : TransferActionKind.Extract;

        _activeRequest = new ChannelRequest
        {
            HandSide = handSide,
            ActionKind = kind,
            Target = target,
            TargetTransform = target.transform,
            TargetPositionAtStart = target.transform.position,
            IsDynamicProperty = isDynamic,
            Duration = kind == TransferActionKind.Extract ? defaultExtractTime : defaultInjectTime,
            StartedAtTime = Time.time
        };

        _activeChannelRoutine = StartCoroutine(ChannelRoutine(_activeRequest));
    }

    public void CancelCurrentChanneling(string reason)
    {
        if (_activeChannelRoutine == null)
        {
            return;
        }

        StopCoroutine(_activeChannelRoutine);
        _activeChannelRoutine = null;
        EmitFeedback(_activeRequest.HandSide, $"Channel canceled: {reason}");
        OnChannelingStateChanged?.Invoke(_activeRequest.HandSide, false);
        _activeRequest = default;
    }

    private IEnumerator ChannelRoutine(ChannelRequest request)
    {
        OnChannelingStateChanged?.Invoke(request.HandSide, true);
        EmitFeedback(request.HandSide, request.ActionKind == TransferActionKind.Extract ? "Extract start" : "Inject start");

        float started = Time.time;
        while (Time.time - started < request.Duration)
        {
            if (ShouldCancelChanneling(request, out string reason))
            {
                _activeChannelRoutine = null;
                OnChannelingStateChanged?.Invoke(request.HandSide, false);
                EmitFeedback(request.HandSide, $"Transfer failed: {reason}");
                _activeRequest = default;
                yield break;
            }

            yield return null;
        }

        bool success = request.ActionKind == TransferActionKind.Extract
            ? CompleteExtract(request)
            : CompleteInject(request);

        _activeChannelRoutine = null;
        OnChannelingStateChanged?.Invoke(request.HandSide, false);
        _activeRequest = default;
        if (!success)
        {
            yield break;
        }
    }

    private bool ShouldCancelChanneling(ChannelRequest request, out string reason)
    {
        reason = string.Empty;

        if (request.Target == null || request.TargetTransform == null)
        {
            reason = "Target missing";
            return true;
        }

        if (interactionManager == null || interactionManager.CurrentTargetObject == null)
        {
            reason = "Target lost";
            return true;
        }

        if (interactionManager.CurrentTargetObject != request.Target)
        {
            reason = "Target changed";
            return true;
        }

        float moved = Vector3.Distance(request.TargetTransform.position, request.TargetPositionAtStart);
        if (moved > channelBreakDistanceTolerance)
        {
            reason = "Target moved";
            return true;
        }

        if (!TryCheckTransferGate(request.Target, out string gateReason))
        {
            reason = string.IsNullOrWhiteSpace(gateReason) ? "Transfer condition failed" : gateReason;
            return true;
        }

        return false;
    }

    private bool CompleteExtract(ChannelRequest request)
    {
        if (propertyDatabase == null)
        {
            Debug.LogWarning("[TransferSystem] propertyDatabase is null. Prototype mode continues.");
        }

        if (IsInjectedPropertyLocked(request.Target))
        {
            EmitFeedback(request.HandSide, "Injected property cannot be extracted.");
            return false;
        }

        string propertyId = ResolveExtractPropertyId(request.Target, request.IsDynamicProperty);
        if (string.IsNullOrWhiteSpace(propertyId))
        {
            EmitFeedback(request.HandSide, "No extractable property.");
            return false;
        }

        string displayName = ResolveExtractDisplayName(request.Target, request.IsDynamicProperty, propertyId);
        HandSlotState hand = GetHandState(request.HandSide);
        hand.Set(propertyId, displayName, request.IsDynamicProperty, Time.time, maxPropertyHoldTime);
        OnHandStateChanged?.Invoke(request.HandSide);
        EmitFeedback(request.HandSide, "Extract success: " + displayName);

        _extractVisualLinks[request.HandSide] = new ExtractVisualLink { Source = request.Target, PropertyId = propertyId };
        request.Target.SendMessage("OnPropertyExtracted", propertyId, SendMessageOptions.DontRequireReceiver);
        return true;
    }

    private bool CompleteInject(ChannelRequest request)
    {
        HandSlotState hand = GetHandState(request.HandSide);
        if (!hand.HasProperty)
        {
            EmitFeedback(request.HandSide, "No property in hand.");
            return false;
        }

        if (!IsTargetInjectionAllowed(request.Target, out string denyReason))
        {
            EmitFeedback(request.HandSide, denyReason);
            return false;
        }

        if (hand.IsDynamicProperty != request.IsDynamicProperty)
        {
            EmitFeedback(request.HandSide, "Property type mismatch with hand.");
            return false;
        }

        ObjectPropertySet propertySet = request.Target.GetComponent<ObjectPropertySet>();
        if (propertySet != null)
        {
            return CompleteInjectWithObjectPropertySet(request, hand, propertySet);
        }

        int id = request.Target.GetInstanceID();
        if (_injectedTargetStates.TryGetValue(id, out InjectedTargetState existing) && existing.RestoreCoroutine != null)
        {
            StopCoroutine(existing.RestoreCoroutine);
        }

        InjectedTargetState state = existing ?? new InjectedTargetState();
        state.Target = request.Target;
        state.PreviousVisualToken = TryReadStringMetadata(request.Target, "CurrentPropertyVisual", "PropertyVisualId");
        state.InjectedPropertyId = hand.PropertyId;
        state.RestoreAtTime = Time.time + defaultInjectedDuration;
        state.RestoreCoroutine = StartCoroutine(RestoreInjectedPropertyAfterDuration(id, defaultInjectedDuration));
        _injectedTargetStates[id] = state;

        TrySetBoolMetadata(request.Target, true, "IsInjectedProperty", "isInjectedProperty");
        TrySetStringMetadata(request.Target, hand.PropertyId, "CurrentPropertyVisual", "PropertyVisualId");
        request.Target.SendMessage("OnPropertyInjected", hand.PropertyId, SendMessageOptions.DontRequireReceiver);
        NotifySourceVisualLinkedToInjection(request.HandSide, hand.PropertyId, defaultInjectedDuration);

        hand.Clear();
        OnHandStateChanged?.Invoke(request.HandSide);
        EmitFeedback(request.HandSide, "Inject success");
        return true;
    }

    private bool CompleteInjectWithObjectPropertySet(ChannelRequest request, HandSlotState hand, ObjectPropertySet propertySet)
    {
        float duration = ResolveInjectedDuration(hand.PropertyId, hand.IsDynamicProperty);
        PropertyChannelType type = hand.IsDynamicProperty ? PropertyChannelType.Dynamic : PropertyChannelType.Static;
        if (!propertySet.TryInjectProperty(type, hand.PropertyId, duration))
        {
            EmitFeedback(request.HandSide, "Target cannot receive injected property.");
            return false;
        }

        propertySet.RecomputeRuntimeState(staticPropertyTable, dynamicPropertyTable, Time.time);
        _trackedObjectPropertySets[propertySet.GetInstanceID()] = propertySet;

        request.Target.SendMessage("OnPropertyInjected", hand.PropertyId, SendMessageOptions.DontRequireReceiver);
        NotifySourceVisualLinkedToInjection(request.HandSide, hand.PropertyId, duration);

        hand.Clear();
        OnHandStateChanged?.Invoke(request.HandSide);
        EmitFeedback(request.HandSide, "Inject success");
        return true;
    }

    private IEnumerator RestoreInjectedPropertyAfterDuration(int targetInstanceId, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (!_injectedTargetStates.TryGetValue(targetInstanceId, out InjectedTargetState state))
        {
            yield break;
        }

        if (state.Target == null)
        {
            _injectedTargetStates.Remove(targetInstanceId);
            yield break;
        }

        TrySetBoolMetadata(state.Target, false, "IsInjectedProperty", "isInjectedProperty");
        if (!string.IsNullOrWhiteSpace(state.PreviousVisualToken))
        {
            TrySetStringMetadata(state.Target, state.PreviousVisualToken, "CurrentPropertyVisual", "PropertyVisualId");
        }

        state.Target.SendMessage("OnInjectedPropertyExpired", null, SendMessageOptions.DontRequireReceiver);
        _injectedTargetStates.Remove(targetInstanceId);
    }

    private void TickTrackedInjectedObjects()
    {
        if (_trackedObjectPropertySets.Count == 0)
        {
            return;
        }

        float now = Time.time;
        List<int> removes = null;
        foreach (KeyValuePair<int, ObjectPropertySet> pair in _trackedObjectPropertySets)
        {
            ObjectPropertySet set = pair.Value;
            if (set == null)
            {
                removes ??= new List<int>();
                removes.Add(pair.Key);
                continue;
            }

            bool changed = set.TickExpireInjectedProperties(now);
            if (changed)
            {
                set.RecomputeRuntimeState(staticPropertyTable, dynamicPropertyTable, now);
                set.gameObject.SendMessage("OnInjectedPropertyExpired", null, SendMessageOptions.DontRequireReceiver);
            }

            if (!set.HasInjectedStatic(now) && !set.HasInjectedDynamic(now))
            {
                removes ??= new List<int>();
                removes.Add(pair.Key);
            }
        }

        if (removes == null)
        {
            return;
        }

        for (int i = 0; i < removes.Count; i++)
        {
            _trackedObjectPropertySets.Remove(removes[i]);
        }
    }

    private void HandleHandExpiration(HandSlotState hand, HandSide handSide)
    {
        if (!hand.HasProperty || Time.time < hand.ExpiresAtTime)
        {
            return;
        }

        hand.Clear();
        NotifySourceVisualExpired(handSide);
        OnHandStateChanged?.Invoke(handSide);
        EmitFeedback(handSide, "Stored property expired.");
    }

    private void NotifySourceVisualLinkedToInjection(HandSide handSide, string propertyId, float duration)
    {
        if (!_extractVisualLinks.TryGetValue(handSide, out ExtractVisualLink link))
        {
            return;
        }

        if (link.Source == null)
        {
            _extractVisualLinks.Remove(handSide);
            return;
        }

        link.Source.SendMessage("OnExtractedPropertyLinkedToInjection", propertyId, SendMessageOptions.DontRequireReceiver);
        StartCoroutine(RevertSourceExtractVisualAfterDuration(handSide, duration));
    }

    private IEnumerator RevertSourceExtractVisualAfterDuration(HandSide handSide, float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        NotifySourceVisualExpired(handSide);
    }

    private void NotifySourceVisualExpired(HandSide handSide)
    {
        if (!_extractVisualLinks.TryGetValue(handSide, out ExtractVisualLink link))
        {
            return;
        }

        _extractVisualLinks.Remove(handSide);
        if (link.Source == null)
        {
            return;
        }

        link.Source.SendMessage("OnExtractedPropertyVisualExpired", null, SendMessageOptions.DontRequireReceiver);
    }

    private float ResolveInjectedDuration(string propertyId, bool isDynamic)
    {
        if (isDynamic && dynamicPropertyTable != null)
        {
            return dynamicPropertyTable.ResolveInjectedDuration(propertyId, defaultInjectedDuration);
        }

        if (!isDynamic && staticPropertyTable != null)
        {
            return staticPropertyTable.ResolveInjectedDuration(propertyId, defaultInjectedDuration);
        }

        return defaultInjectedDuration;
    }

    private string ResolveExtractPropertyId(GameObject target, bool isDynamic)
    {
        ObjectPropertySet propertySet = target != null ? target.GetComponent<ObjectPropertySet>() : null;
        if (propertySet != null)
        {
            string bySet = isDynamic
                ? propertySet.GetEffectiveDynamicPropertyId(Time.time)
                : propertySet.GetEffectiveStaticPropertyId(Time.time);
            if (!string.IsNullOrWhiteSpace(bySet))
            {
                return bySet;
            }
        }

        string byMeta = isDynamic
            ? TryReadStringMetadata(target, "ExtractDynamicPropertyId", "TransferDynamicPropertyId", "DynamicPropertyId")
            : TryReadStringMetadata(target, "ExtractStaticPropertyId", "TransferStaticPropertyId", "StaticPropertyId");
        if (!string.IsNullOrWhiteSpace(byMeta))
        {
            return byMeta;
        }

        return $"{(isDynamic ? "dynamic" : "static")}:{target.name}";
    }

    private string ResolveExtractDisplayName(GameObject target, bool isDynamic, string propertyId)
    {
        if (isDynamic && dynamicPropertyTable != null && dynamicPropertyTable.TryGetDefinition(propertyId, out DynamicPropertyDefinition dynamicDef) && !string.IsNullOrWhiteSpace(dynamicDef.DisplayName))
        {
            return dynamicDef.DisplayName;
        }

        if (!isDynamic && staticPropertyTable != null && staticPropertyTable.TryGetDefinition(propertyId, out StaticPropertyDefinition staticDef) && !string.IsNullOrWhiteSpace(staticDef.DisplayName))
        {
            return staticDef.DisplayName;
        }

        string byMeta = isDynamic
            ? TryReadStringMetadata(target, "ExtractDynamicDisplayName", "DynamicPropertyDisplayName")
            : TryReadStringMetadata(target, "ExtractStaticDisplayName", "StaticPropertyDisplayName");
        if (!string.IsNullOrWhiteSpace(byMeta))
        {
            return byMeta;
        }

        return $"{target.name} ({(isDynamic ? "dynamic" : "static")}:{propertyId})";
    }

    private bool TryHandleClickOnlyTarget(GameObject target, MouseButtonType buttonType)
    {
        if (!TryReadBoolMetadata(target, out bool blockTransferClick, "BlockTransferClick", "blockTransferClick") || !blockTransferClick)
        {
            return false;
        }

        target.SendMessage("HandleClick", buttonType, SendMessageOptions.DontRequireReceiver);
        return true;
    }

    private HandSlotState GetHandState(HandSide side)
    {
        return side == HandSide.Left ? leftHandState : rightHandState;
    }

    private void SubscribeInputEvents()
    {
        if (inputManager == null)
        {
            inputManager = InputManager.Instance;
        }

        if (interactionManager == null)
        {
            interactionManager = FindFirstObjectByType<InteractionManager>();
        }

        if (inputManager == null)
        {
            Debug.LogWarning("[TransferSystem] InputManager reference missing.");
            return;
        }

        inputManager.OnTransferClickPressed += HandleTransferClick;
    }

    private void UnsubscribeInputEvents()
    {
        if (inputManager != null)
        {
            inputManager.OnTransferClickPressed -= HandleTransferClick;
        }
    }

    private void EmitFeedback(HandSide side, string message)
    {
        OnTransferFeedbackRequested?.Invoke(side, message);
    }

    private static bool IsInjectedPropertyLocked(GameObject target)
    {
        return TryReadBoolMetadata(target, out bool value, "IsInjectedProperty", "isInjectedProperty", "InjectedPropertyLocked", "isInjectedLocked") && value;
    }

    private static bool IsTargetInjectionAllowed(GameObject target, out string denyReason)
    {
        denyReason = string.Empty;

        if (TryReadBoolMetadata(target, out bool canInject, "CanReceiveInjectedProperty", "canReceiveInjectedProperty", "AllowPropertyInjection") && !canInject)
        {
            denyReason = "Target cannot receive injected property.";
            return false;
        }

        if (TryReadBoolMetadata(target, out bool destructible, "IsDestructible", "isDestructible", "Destructible") && !destructible)
        {
            denyReason = "Injection is blocked for non-destructible target.";
            return false;
        }

        return true;
    }

    private static bool TryCheckTransferGate(GameObject target, out string reason)
    {
        reason = string.Empty;
        if (target == null)
        {
            return false;
        }

        MonoBehaviour[] components = target.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is ITransferInteractionGate gate && !gate.CanTransferInteraction(out reason))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadBoolMetadata(GameObject target, out bool value, params string[] members)
    {
        value = false;
        if (target == null)
        {
            return false;
        }

        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            Type type = component.GetType();
            for (int m = 0; m < members.Length; m++)
            {
                FieldInfo field = type.GetField(members[m], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(bool))
                {
                    value = (bool)field.GetValue(component);
                    return true;
                }

                PropertyInfo property = type.GetProperty(members[m], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(bool) && property.CanRead)
                {
                    value = (bool)property.GetValue(component, null);
                    return true;
                }
            }
        }

        return false;
    }

    private static string TryReadStringMetadata(GameObject target, params string[] members)
    {
        if (target == null)
        {
            return null;
        }

        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            Type type = component.GetType();
            for (int m = 0; m < members.Length; m++)
            {
                FieldInfo field = type.GetField(members[m], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(string))
                {
                    return (string)field.GetValue(component);
                }

                PropertyInfo property = type.GetProperty(members[m], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(string) && property.CanRead)
                {
                    return (string)property.GetValue(component, null);
                }
            }
        }

        return null;
    }

    private static void TrySetBoolMetadata(GameObject target, bool value, params string[] members)
    {
        if (target == null)
        {
            return;
        }

        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            Type type = component.GetType();
            for (int m = 0; m < members.Length; m++)
            {
                FieldInfo field = type.GetField(members[m], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(bool))
                {
                    field.SetValue(component, value);
                    return;
                }

                PropertyInfo property = type.GetProperty(members[m], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
                {
                    property.SetValue(component, value, null);
                    return;
                }
            }
        }
    }

    private static void TrySetStringMetadata(GameObject target, string value, params string[] members)
    {
        if (target == null)
        {
            return;
        }

        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            Type type = component.GetType();
            for (int m = 0; m < members.Length; m++)
            {
                FieldInfo field = type.GetField(members[m], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(string))
                {
                    field.SetValue(component, value);
                    return;
                }

                PropertyInfo property = type.GetProperty(members[m], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(string) && property.CanWrite)
                {
                    property.SetValue(component, value, null);
                    return;
                }
            }
        }
    }

    private struct ChannelRequest
    {
        public HandSide HandSide;
        public TransferActionKind ActionKind;
        public GameObject Target;
        public Transform TargetTransform;
        public Vector3 TargetPositionAtStart;
        public bool IsDynamicProperty;
        public float Duration;
        public float StartedAtTime;
    }

    private struct ExtractVisualLink
    {
        public GameObject Source;
        public string PropertyId;
    }

    private sealed class InjectedTargetState
    {
        public GameObject Target;
        public string PreviousVisualToken;
        public string InjectedPropertyId;
        public float RestoreAtTime;
        public Coroutine RestoreCoroutine;
    }
}
