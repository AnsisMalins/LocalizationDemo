using UnityEngine;
using UnityEngine.Serialization;

public sealed class GameBehaviour : MonoBehaviour
{
    [SerializeField, ObjectID, FormerlySerializedAs("_targetId")]
    private long _objectId;

    [Localized]
    public string Name => Localization.Get(_objectId);

    [Localized]
    public string Description => Localization.Get(_objectId);
}