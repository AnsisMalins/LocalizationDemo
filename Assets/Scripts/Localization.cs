using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml;
using UnityEngine;

public static class Localization
{
    private static Dictionary<LocalizationKey, string> _data = new();
    private static int _isLoaded;
    public static event Action LanguageChanged;

    public static string FilePath => $"{Application.streamingAssetsPath}/Localization/{Language}.xml";
    public static bool IsLoaded => _isLoaded != 0;

    // TODO: In builds, load the chosen language from a config file in the user's home directory.
    public static string Language { get; private set; } = "English";

    public static string Get(long objectId, [CallerMemberName] string propertyName = null)
    {
        // This function must be thread safe. Do not call any Unity APIs from it, directly or indirectly.

        if (!IsLoaded)
            Load();

        var key = new LocalizationKey(objectId, propertyName);
        string value;

#if UNITY_EDITOR
        if (_editorData != null && _editorData.TryGetValue(key, out value))
            return value ?? "LOCALIZATION";
#endif

        return _data.TryGetValue(key, out value) ? value : "LOCALIZATION";
    }

    public static void Load()
    {
        lock (_data)
        {
            _data.Clear();

            try
            {
                using var xml = XmlReader.Create(FilePath, new XmlReaderSettings()
                {
                    IgnoreComments = true,
                    IgnoreWhitespace = true
                });

                xml.ReadToFollowing("Localization");
                xml.ReadStartElement();

                while (!xml.EOF)
                {
                    if (xml.NodeType == XmlNodeType.EndElement)
                        break;
                    if (xml.IsEmptyElement)
                        continue;

                    long objectId = long.Parse(xml.GetAttribute("ID"));

                    xml.ReadStartElement();
                    while (xml.NodeType != XmlNodeType.EndElement)
                        _data.Add(new(objectId, xml.Name), xml.ReadElementContentAsString());
                    xml.ReadEndElement();
                }
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR
                if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                {
                    Debug.LogWarning(
                        $"Localization file {FilePath} does not exist. It will be created next time you save.");
                }
                else
                {
                    // Set localization to read-only to protect the file, if any, from being overwritten with
                    // empty data.
                    IsReadOnly = true;
                    Debug.LogException(ex);
                }
#else
                Debug.LogException(ex);
#endif
            }

            Thread.VolatileWrite(ref _isLoaded, 1);
        }
    }

    public static void SetLanguage(string language, bool calledByEditor = false)
    {
        if (!calledByEditor && !Application.isPlaying)
            throw new InvalidOperationException(
                "Use LocalizationEditor.SetLanguage to change language during edit mode instead");

        Language = language;
#if UNITY_EDITOR
        IsReadOnly = false;
#endif
        Thread.VolatileWrite(ref _isLoaded, 0);
        LanguageChanged?.Invoke();
    }

#if UNITY_EDITOR
    private static IReadOnlyDictionary<LocalizationKey, string> _editorData;
    public static Dictionary<LocalizationKey, string> GetData() => _data;
    public static void SetEditorData(IReadOnlyDictionary<LocalizationKey, string> data) => _editorData = data;
    public static bool IsReadOnly { get; private set; }
#endif
}

[Serializable]
public struct LocalizationKey : IEquatable<LocalizationKey>, IComparable<LocalizationKey>
{
    public long ObjectID;
    public string PropertyName;

    public override int GetHashCode() => (ObjectID, PropertyName).GetHashCode();
    public override string ToString() => $"({ObjectID}, \"{PropertyName}\")";

    public LocalizationKey(long objectId, string propertyName)
    {
        ObjectID = objectId;
        PropertyName = propertyName;
    }

    public int CompareTo(LocalizationKey other)
    {
        return (ObjectID, PropertyName).CompareTo((other.ObjectID, other.PropertyName));
    }

    public bool Equals(LocalizationKey other)
    {
        return ObjectID == other.ObjectID && PropertyName == other.PropertyName;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class LocalizedAttribute : Attribute { }

public sealed class ObjectIDAttribute : PropertyAttribute { }