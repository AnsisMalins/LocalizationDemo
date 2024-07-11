using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;

public static class LocalizationEditor
{
    private static EditorData _editorData;

    internal static bool IsDirty { get; private set; }

    public static void Set(long objectId, string propertyName, string value, string objectName = null)
    {
        // isPlaying also serves as a check to make sure only the main thread edits localisation.
        if (Application.isPlaying)
            throw new InvalidOperationException("Cannot edit localization during play");

        if (!Localization.IsLoaded)
            Localization.Load();
        if (Localization.IsReadOnly)
            return;

        if (_editorData == null)
            _editorData = ScriptableObject.CreateInstance<EditorData>();
        Localization.SetEditorData(_editorData);

        if (objectName == null)
            objectName = $"object {objectId}";

        Undo.RecordObject(_editorData,
            $"Modified {ObjectNames.NicifyVariableName(propertyName)} in {objectName}");

        _editorData[new(objectId, propertyName)] = value;
        IsDirty = true;
    }

    [MenuItem("Tools/Localization/Load")]
    public static void Load()
    {
        ClearUndoData();
        Localization.Load();
    }

    [MenuItem("Tools/Localization/Save")]
    public static void Save()
    {
        if (Localization.IsReadOnly)
            throw new InvalidOperationException("Localization data is read only");

        Directory.CreateDirectory(Path.GetDirectoryName(Localization.FilePath));

        using var xml = XmlWriter.Create(Localization.FilePath, new XmlWriterSettings()
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "\t",
            OmitXmlDeclaration = true
        });

        var data = Localization.GetData();

        foreach (var item in _editorData)
        {
            if (item.Value != null)
                data[item.Key] = item.Value;
            else
                data.Remove(item.Key);
        }

        xml.WriteStartElement("Localization");
        foreach (var item in data
            .GroupBy(i => i.Key.ObjectID)
            .OrderBy(i => i.Key))
        {
            xml.WriteStartElement("Object");
            xml.WriteAttributeString("ID", item.Key.ToString());

            foreach (var property in item.OrderBy(i => i.Key.PropertyName))
                xml.WriteElementString(property.Key.PropertyName, property.Value);

            xml.WriteEndElement();
        }
        xml.WriteEndElement();

        IsDirty = false;
    }

    private static void SetLanguage(string language)
    {
        if (Application.isPlaying)
            throw new InvalidOperationException(
                "Use Localization.SetLanguage to change language during play instead");

        if (IsDirty)
            Save();

        if (_editorData == null)
            _editorData = ScriptableObject.CreateInstance<EditorData>();
        _editorData.Language = language;
        Localization.SetLanguage(language, true);

        foreach (var editor in Resources.FindObjectsOfTypeAll<MonoBehaviourEditor>())
            editor.Repaint();
    }

    [MenuItem("Tools/Localization/Set Language/English")]
    private static void SetLangaugeToEnglish() => SetLanguage("English");

    [MenuItem("Tools/Localization/Set Language/German")]
    private static void SetLanguageToGerman() => SetLanguage("German");

    [MenuItem("Tools/Localization/Debug/Clear Undo Data")]
    private static void ClearUndoData()
    {
        if (_editorData == null)
            return;

        _editorData.Clear();
        Undo.ClearUndo(_editorData);
        IsDirty = false;
    }

    [MenuItem("Tools/Localization/Debug/Select Editor Data Object")]
    private static void SelectEditorDataObject()
    {
        Selection.activeObject = _editorData;
    }

    private sealed class EditorData : ScriptableObject, IReadOnlyDictionary<LocalizationKey, string>
    {
        public string Language;
        [SerializeField]
        private List<LocalizationKey> _keys = new();
        [SerializeField]
        private List<string> _values = new();

        public bool ContainsKey(LocalizationKey key) => _keys.BinarySearch(key) >= 0;
        public int Count => _keys.Count;
        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
        public IEnumerable<LocalizationKey> Keys => _keys;
        public IEnumerable<string> Values => _values;

        public EditorData()
        {
            if (_editorData != null)
                Debug.LogError("Localization undo target exists already!", _editorData);

            _editorData = this;
            Localization.LanguageChanged += ClearUndoData;
        }

        private void OnEnable()
        {
            hideFlags = HideFlags.DontSave;

            // Restore the language set in editor through domain reloads and play mode changes.
            if (Language != null && Language != Localization.Language)
                SetLanguage(Language);
        }

        public void Clear()
        {
            _keys.Clear();
            _values.Clear();
        }

        public IEnumerator<KeyValuePair<LocalizationKey, string>> GetEnumerator()
        {
            for (int i = 0; i < _keys.Count; i++)
                yield return new(_keys[i], _values[i]);
        }

        public string this[LocalizationKey key]
        {
            get
            {
                if (!TryGetValue(key, out var value))
                    throw new KeyNotFoundException($"Localization key {key} not found");
                return value;
            }
            set
            {
                int index = _keys.BinarySearch(key);
                if (index >= 0)
                {
                    _values[index] = value;
                }
                else
                {
                    index = ~index;
                    _keys.Insert(index, key);
                    _values.Insert(index, value);
                }
            }
        }

        public bool TryGetValue(LocalizationKey key, out string value)
        {
            int index = _keys.BinarySearch(key);

            if (index >= 0)
            {
                value = _values[index];
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
    }
}

internal class LocalizationAssetModificationProcesor : UnityEditor.AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        try
        {
            if (LocalizationEditor.IsDirty)
                LocalizationEditor.Save();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        return paths;
    }
}