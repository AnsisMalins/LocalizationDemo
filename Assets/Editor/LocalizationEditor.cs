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
    private static void SelectUndoTarget()
    {
        Selection.activeObject = _editorData;
    }

    private sealed class EditorData : ScriptableObject, IReadOnlyDictionary<LocalizationKey, string>
    {
        public string Language;
        [SerializeField]
        private List<Item> _data = new();

        public void Clear() => _data.Clear();
        public bool ContainsKey(LocalizationKey key) => _data.BinarySearch(new(key)) >= 0;
        public int Count => _data.Count;
        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
        public IEnumerable<LocalizationKey> Keys => _data.Select(i => i.key);
        public IEnumerable<string> Values => _data.Select(i => i.value);

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
                int index = _data.BinarySearch(new(key));
                var item = new Item(key, value);
                if (index >= 0)
                    _data[index] = item;
                else
                    _data.Insert(~index, item);
            }
        }

        public IEnumerator<KeyValuePair<LocalizationKey, string>> GetEnumerator()
        {
            for (int i = 0; i < _data.Count; i++)
            {
                var item = _data[i];
                yield return new(item.key, item.value);
            }
        }

        public bool TryGetValue(LocalizationKey key, out string value)
        {
            int index = _data.BinarySearch(new(key));

            if (index >= 0)
            {
                value = _data[index].value;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
    }

    [Serializable]
    private struct Item : IComparable<Item>
    {
        public LocalizationKey key;
        public string value;

        public Item(LocalizationKey key)
        {
            this.key = key;
            value = null;
        }

        public Item(LocalizationKey key, string value)
        {
            this.key = key;
            this.value = value;
        }

        public int CompareTo(Item other)
        {
            return key.CompareTo(other.key);
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