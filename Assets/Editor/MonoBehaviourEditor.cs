using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonoBehaviour), true, isFallback = true), CanEditMultipleObjects]
public class MonoBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        DrawLocalizationInspector();
    }

    protected void DrawLocalizationInspector()
    {
        if (Localization.IsReadOnly)
        {
            EditorGUILayout.HelpBox(
                "Localization data is broken. Check the console for errors and fix them.",
                MessageType.Error);
            return;
        }
        else if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Cannot edit localization during play.", MessageType.Info);
            return;
        }
        else if (serializedObject.isEditingMultipleObjects)
        {
            EditorGUILayout.HelpBox("Localization does not support editing multiple objects.",
                MessageType.Info);
            return;
        }

        long objectId = 0L;
        var targetType = target.GetType();

        foreach (var property in targetType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(i => i.GetCustomAttribute<LocalizedAttribute>() != null))
        {
            if (objectId == 0L)
            {
                var objectIdField = targetType
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(i => i.GetCustomAttribute<ObjectIDAttribute>() != null)
                    .FirstOrDefault();

                if (objectIdField != null)
                {
                    objectId = (long)objectIdField.GetValue(target);
                }
                else
                {
                    EditorGUILayout.HelpBox("Localized properties require an Object ID field.",
                        MessageType.Error);
                    break;
                }

                if (objectId == 0L)
                {
                    EditorGUILayout.HelpBox("Object ID must be non-zero.", MessageType.Error);
                    break;
                }
            }

            string value = Localization.Get(objectId, property.Name);
            string newValue = EditorGUILayout.TextField(ObjectNames.NicifyVariableName(property.Name), value);
            if (newValue != value)
                LocalizationEditor.Set(objectId, property.Name, newValue, target.name);
        }
    }
}

[CustomPropertyDrawer(typeof(ObjectIDAttribute))]
internal sealed class ObjectIDDrawer : PropertyDrawer
{
    private static float _buttonWidth;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        label = EditorGUI.BeginProperty(position, label, property);

        if (_buttonWidth == 0f)
            _buttonWidth = EditorStyles.miniButton.CalcSize(new GUIContent("RNG")).x;

        position.width -= _buttonWidth + 2f;
        property.longValue = EditorGUI.LongField(position, label, property.longValue);

        position.x += position.width + 2f;
        position.width = _buttonWidth;
        if (GUI.Button(position, "RNG"))
        {
            property.longValue = ((long)Random.Range(int.MinValue, int.MaxValue) << 32)
                + Random.Range(int.MinValue, int.MaxValue);
        }

        EditorGUI.EndProperty();
    }
}