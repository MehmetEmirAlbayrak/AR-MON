using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pokemon prefablarını yöneten kayıt sistemi.
/// Yakalanan Pokemon'un modelini daha sonra yüklemek için kullanılır.
/// </summary>
public class PokemonPrefabRegistry : MonoBehaviour
{
    public static PokemonPrefabRegistry Instance { get; private set; }
    
    [System.Serializable]
    public class PokemonPrefabEntry
    {
        public string pokemonId; // Benzersiz ID (prefab adı)
        public GameObject prefab;
    }
    
    [Header("Pokemon Prefabları")]
    [Tooltip("Tüm Pokemon prefablarını buraya ekleyin")]
    public List<PokemonPrefabEntry> pokemonPrefabs = new List<PokemonPrefabEntry>();
    
    // Hızlı erişim için dictionary
    private Dictionary<string, GameObject> prefabDictionary = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildDictionary();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void BuildDictionary()
    {
        prefabDictionary.Clear();
        foreach (var entry in pokemonPrefabs)
        {
            if (entry.prefab != null && !string.IsNullOrEmpty(entry.pokemonId))
            {
                string key = NormalizeId(entry.pokemonId);
                if (!prefabDictionary.ContainsKey(key))
                {
                    prefabDictionary[key] = entry.prefab;
                }
            }
        }
        Debug.Log($"PokemonPrefabRegistry: {prefabDictionary.Count} prefab yüklendi.");
    }

    /// <summary>
    /// ID'ye göre prefab bul
    /// </summary>
    public GameObject GetPrefab(string pokemonId)
    {
        if (string.IsNullOrEmpty(pokemonId)) return null;
        
        string key = NormalizeId(pokemonId);
        
        if (prefabDictionary.TryGetValue(key, out GameObject prefab))
        {
            return prefab;
        }
        
        // Tam eşleşme bulunamazsa, içeren aramayı dene
        foreach (var entry in pokemonPrefabs)
        {
            string entryKey = NormalizeId(entry.pokemonId);
            if (entryKey.Contains(key) || key.Contains(entryKey))
            {
                return entry.prefab;
            }
        }
        
        Debug.LogWarning($"Pokemon prefabı bulunamadı: {pokemonId}");
        return null;
    }

    /// <summary>
    /// GameObject'ten prefab ID'sini çıkar
    /// </summary>
    public static string GetPrefabId(GameObject pokemon)
    {
        if (pokemon == null) return "";
        
        string name = pokemon.name;
        // (Clone) ve benzeri ekleri temizle
        name = name.Replace("(Clone)", "").Trim();
        return name;
    }

    /// <summary>
    /// ID'yi normalize et (küçük harf, boşluk/alt çizgi temizle)
    /// </summary>
    string NormalizeId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        return id.ToLower().Replace(" ", "").Replace("_", "").Replace("-", "").Trim();
    }

    /// <summary>
    /// Yeni prefab ekle (runtime'da)
    /// </summary>
    public void RegisterPrefab(string pokemonId, GameObject prefab)
    {
        if (prefab == null || string.IsNullOrEmpty(pokemonId)) return;
        
        string key = NormalizeId(pokemonId);
        prefabDictionary[key] = prefab;
        
        // Listeye de ekle
        pokemonPrefabs.Add(new PokemonPrefabEntry
        {
            pokemonId = pokemonId,
            prefab = prefab
        });
    }

    /// <summary>
    /// Tüm kayıtlı prefab ID'lerini döndür
    /// </summary>
    public List<string> GetAllPrefabIds()
    {
        return new List<string>(prefabDictionary.Keys);
    }
}
