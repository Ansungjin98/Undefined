using UnityEngine;

[CreateAssetMenu(fileName = "PropertyDatabaseCheckPlaceholder", menuName = "Undefined/Debug/Property Database Placeholder")]
public class PropertyDatabaseCheckPlaceholder : ScriptableObject
{
    [TextArea]
    [SerializeField] private string note = "TransferSystem 체크용 placeholder. 실제 PropertyDatabase 구현 전 임시 할당용.";
}
