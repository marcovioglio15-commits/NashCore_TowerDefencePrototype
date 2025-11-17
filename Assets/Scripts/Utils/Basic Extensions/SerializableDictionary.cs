using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableDictionary<TKey, TValue>
{
    [SerializeField] private List<TKey> keys = new();
    [SerializeField] private List<TValue> values = new();

    private Dictionary<TKey, TValue> dictionary;

    public SerializableDictionary()
    {
        PopulateDictionaryIfEmpty();
    }

    public Dictionary<TKey, TValue> GetDictionary()
    {
        PopulateDictionaryIfEmpty();
        return dictionary;
    }

    private void PopulateDictionaryIfEmpty()
    {
        if (dictionary == null || dictionary.Count == 0)
        {
            dictionary = new Dictionary<TKey, TValue>();
            for (int i = 0; i < Mathf.Min(keys.Count, values.Count); i++)
            {
                dictionary[keys[i]] = values[i];
            }
        }
    }

    public void Clear()
    {
        // Clear serialized data
        keys.Clear(); 
        values.Clear();

        // Clear runtime dictionary
        if (dictionary != null)
            dictionary.Clear();
    }
}
