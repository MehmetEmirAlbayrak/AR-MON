using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Pot türleri
public enum PotionType
{
    SmallPotion,    // 20 HP iyileştirir
    SuperPotion,    // 50 HP iyileştirir
    HyperPotion,    // 100 HP iyileştirir (Full heal)
    Revive          // Bayılmış Pokemon'u %50 HP ile diriltir
}

[System.Serializable]
public class PotionInventory
{
    public int smallPotions;   // Başlangıçta 3 küçük pot
    public int superPotions;   // Başlangıçta 1 süper pot
    public int hyperPotions;
    public int revives;        // Başlangıçta 1 revive
}

public class PokemonBag : MonoBehaviour
{

    public int startSmallPotions = 3;
    public int startSuperPotions = 1;
    public int startHyperPotions = 0;
    public int startRevives = 100;

    public static PokemonBag Instance { get; private set; }

    private PokemonInventory inventory;
    private PotionInventory potions;
    private const string SAVE_KEY = "PokemonBag";
    private const string POTION_SAVE_KEY = "PotionBag";

    public List<PokemonData> CaughtPokemon => inventory.caughtPokemon;
    public PotionInventory Potions => potions;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadInventory();
            LoadPotions();
        }
        else
        {
            Destroy(gameObject);
        }

        // Oyun başladığında otomatik iyileştirme yapma - potlarla manuel iyileştirsin
        // HealAllPokemon();
        Debug.Log($"Çanta yüklendi! Potlar: {potions.smallPotions} küçük, {potions.superPotions} süper, {potions.hyperPotions} hyper, {potions.revives} revive");
        potions.smallPotions = startSmallPotions;
        potions.superPotions = startSuperPotions;
        potions.hyperPotions = startHyperPotions;
        potions.revives = startRevives;
        SavePotions();
    }

    public PokemonData AddPokemon(string pokemonName, int level = 1, string prefabId = "")
    {
        PokemonData newPokemon = new PokemonData(pokemonName, level, prefabId);
        inventory.caughtPokemon.Add(newPokemon);
        SaveInventory();
        
        Debug.Log($"=== YENİ POKEMON YAKALANDI! ===\n{newPokemon.GetStatsSummary()}\nPrefab: {newPokemon.prefabId}\nToplam Pokemon: {inventory.caughtPokemon.Count}");
        
        return newPokemon;
    }

    // İsme göre Pokemon bul
    public PokemonData GetPokemon(string pokemonName)
    {
        return inventory.caughtPokemon.FirstOrDefault(p => p.pokemonName == pokemonName);
    }

    // Index'e göre Pokemon bul
    public PokemonData GetPokemonAt(int index)
    {
        if (index >= 0 && index < inventory.caughtPokemon.Count)
        {
            return inventory.caughtPokemon[index];
        }
        return null;
    }

    // Pokemon'a XP ekle ve kaydet
    public bool AddXPToPokemon(int index, int xpAmount)
    {
        PokemonData pokemon = GetPokemonAt(index);
        if (pokemon != null)
        {
            bool leveledUp = pokemon.AddXP(xpAmount);
            SaveInventory();
            return leveledUp;
        }
        return false;
    }

    // Pokemon'u iyileştir
    public void HealPokemon(int index)
    {
        PokemonData pokemon = GetPokemonAt(index);
        if (pokemon != null)
        {
            pokemon.FullHeal();
            SaveInventory();
            Debug.Log($"{pokemon.pokemonName} tamamen iyileştirildi!");
        }
    }

    // Tüm Pokemonları iyileştir
    public void HealAllPokemon()
    {
        foreach (var pokemon in inventory.caughtPokemon)
        {
            pokemon.FullHeal();
        }
        SaveInventory();
        Debug.Log("Tüm Pokemonlar iyileştirildi!");
    }

    // Pokemon'u serbest bırak
    public void ReleasePokemon(int index)
    {
        if (index >= 0 && index < inventory.caughtPokemon.Count)
        {
            string name = inventory.caughtPokemon[index].pokemonName;
            inventory.caughtPokemon.RemoveAt(index);
            SaveInventory();
            Debug.Log($"{name} serbest bırakıldı!");
        }
    }

    // En güçlü Pokemon'u bul (Attack'a göre)
    public PokemonData GetStrongestPokemon()
    {
        return inventory.caughtPokemon.OrderByDescending(p => p.Attack).FirstOrDefault();
    }

    // En yüksek levelli Pokemon'u bul
    public PokemonData GetHighestLevelPokemon()
    {
        return inventory.caughtPokemon.OrderByDescending(p => p.level).FirstOrDefault();
    }

    // Tüm Pokemonların stat özetini göster
    public void PrintAllPokemonStats()
    {
        if (inventory.caughtPokemon.Count == 0)
        {
            Debug.Log("Çantada Pokemon yok!");
            return;
        }

        Debug.Log("=== ÇANTADAKI POKEMONLAR ===");
        for (int i = 0; i < inventory.caughtPokemon.Count; i++)
        {
            Debug.Log($"[{i}] {inventory.caughtPokemon[i].GetStatsSummary()}\n---");
        }
    }

    public void SaveInventory()
    {
        string json = JsonUtility.ToJson(inventory);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    public void LoadInventory()
    {
        if (PlayerPrefs.HasKey(SAVE_KEY))
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            inventory = JsonUtility.FromJson<PokemonInventory>(json);
            Debug.Log($"Çanta yüklendi! {inventory.caughtPokemon.Count} Pokemon bulundu.");
            
            // Yüklenen Pokemonların statlarını kontrol et ve düzelt
            foreach (var pokemon in inventory.caughtPokemon)
            {
                // Eski kayıtlarda base stat'lar 0 olabilir - yeniden oluştur
                if (pokemon.baseHealth <= 0)
                {
                    pokemon.baseHealth = Random.Range(15, 26);
                    Debug.Log($"{pokemon.pokemonName} baseHealth düzeltildi: {pokemon.baseHealth}");
                }
                if (pokemon.baseAttack <= 0)
                {
                    pokemon.baseAttack = Random.Range(3, 8);
                }
                if (pokemon.baseDefense <= 0)
                {
                    pokemon.baseDefense = Random.Range(2, 6);
                }
                if (pokemon.baseSpeed <= 0)
                {
                    pokemon.baseSpeed = Random.Range(5, 11);
                }
                
                // Level 0 veya negatifse düzelt
                if (pokemon.level <= 0)
                {
                    pokemon.level = 1;
                }
                
                // currentHealth, Health'ten büyükse düzelt
                if (pokemon.currentHealth > pokemon.Health)
                {
                    pokemon.currentHealth = pokemon.Health;
                }
            }
            
            // Düzeltmeleri kaydet
            SaveInventory();
        }
        else
        {
            inventory = new PokemonInventory();
            Debug.Log("Yeni çanta oluşturuldu.");
        }
    }

    public void ClearBag()
    {
        inventory = new PokemonInventory();
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
        Debug.Log("Çanta temizlendi!");
    }

    public int GetPokemonCount()
    {
        return inventory.caughtPokemon.Count;
    }
    
    // ========== POT SİSTEMİ ==========
    
    /// <summary>
    /// Pot ekle (vahşi Pokemon yenildiğinde veya satın alındığında)
    /// </summary>
    public void AddPotion(PotionType type, int amount = 1)
    {
        switch (type)
        {
            case PotionType.SmallPotion:
                potions.smallPotions += amount;
                break;
            case PotionType.SuperPotion:
                potions.superPotions += amount;
                break;
            case PotionType.HyperPotion:
                potions.hyperPotions += amount;
                break;
            case PotionType.Revive:
                potions.revives += amount;
                break;
        }
        SavePotions();
        Debug.Log($"{type} x{amount} eklendi!");
    }
    
    /// <summary>
    /// Pot sayısını al
    /// </summary>
    public int GetPotionCount(PotionType type)
    {
        switch (type)
        {
            case PotionType.SmallPotion: return potions.smallPotions;
            case PotionType.SuperPotion: return potions.superPotions;
            case PotionType.HyperPotion: return potions.hyperPotions;
            case PotionType.Revive: return potions.revives;
            default: return 0;
        }
    }
    
    /// <summary>
    /// Pokemon'a pot kullan
    /// </summary>
    public bool UsePotion(PotionType type, int pokemonIndex)
    {
        PokemonData pokemon = GetPokemonAt(pokemonIndex);
        if (pokemon == null) return false;
        
        // Pot sayısı kontrolü
        int potCount = GetPotionCount(type);
        if (potCount <= 0)
        {
            Debug.Log($"{type} kalmadı!");
            return false;
        }
        
        // Revive sadece bayılmış Pokemon'a kullanılabilir
        if (type == PotionType.Revive)
        {
            if (!pokemon.IsFainted)
            {
                Debug.Log($"{pokemon.pokemonName} bayılmamış, Revive kullanılamaz!");
                return false;
            }
            
            // Revive: %50 HP ile dirilt (minimum 1 HP)
            int reviveHP = Mathf.Max(1, pokemon.Health / 2);
            pokemon.currentHealth = reviveHP;
            potions.revives--;
            SavePotions();
            SaveInventory();
            Debug.Log($"{pokemon.pokemonName} dirildi! HP: {pokemon.currentHealth}/{pokemon.Health}");
            return true;
        }
        
        // Diğer potlar sadece bayılmamış Pokemon'a kullanılabilir
        if (pokemon.IsFainted)
        {
            Debug.Log($"{pokemon.pokemonName} bayılmış, önce Revive kullanmalısın!");
            return false;
        }
        
        // Zaten full HP ise kullanma
        if (pokemon.currentHealth >= pokemon.Health)
        {
            Debug.Log($"{pokemon.pokemonName} zaten tam sağlıklı!");
            return false;
        }
        
        // İyileştirme miktarı
        int healAmount = 0;
        switch (type)
        {
            case PotionType.SmallPotion:
                healAmount = 20;
                potions.smallPotions--;
                break;
            case PotionType.SuperPotion:
                healAmount = 50;
                potions.superPotions--;
                break;
            case PotionType.HyperPotion:
                healAmount = 9999; // Full heal
                potions.hyperPotions--;
                break;
        }
        
        int oldHealth = pokemon.currentHealth;
        pokemon.Heal(healAmount);
        int actualHeal = pokemon.currentHealth - oldHealth;
        
        SavePotions();
        SaveInventory();
        Debug.Log($"{pokemon.pokemonName} +{actualHeal} HP iyileşti! HP: {pokemon.currentHealth}/{pokemon.Health}");
        return true;
    }
    
    /// <summary>
    /// Potları kaydet
    /// </summary>
    public void SavePotions()
    {
        string json = JsonUtility.ToJson(potions);
        PlayerPrefs.SetString(POTION_SAVE_KEY, json);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Potları yükle
    /// </summary>
    public void LoadPotions()
    {
        if (PlayerPrefs.HasKey(POTION_SAVE_KEY))
        {
            string json = PlayerPrefs.GetString(POTION_SAVE_KEY);
            potions = JsonUtility.FromJson<PotionInventory>(json);
        }
        else
        {
            potions = new PotionInventory();
        }
    }
    
    /// <summary>
    /// Pot envanterini sıfırla (test için)
    /// </summary>
    public void ResetPotions()
    {
        potions = new PotionInventory();
        SavePotions();
        Debug.Log("Potlar sıfırlandı!");
    }
}
