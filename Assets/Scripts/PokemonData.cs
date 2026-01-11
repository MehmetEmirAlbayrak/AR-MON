using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class PokemonData
{
    public string pokemonName;
    public string prefabId; // Prefab'ı yüklemek için kullanılacak ID
    public string catchDate;
    
    // Level sistemi
    public int level = 1;
    public int currentXP = 0;
    public int xpToNextLevel = 100;
    
    // Base statlar (level 1'deki değerler)
    public int baseAttack;
    public int baseHealth;
    public int baseDefense;
    public int baseSpeed;
    
    // Hesaplanmış statlar (level'e bağlı)
    public int Attack => CalculateStat(baseAttack);
    public int Health => CalculateStat(baseHealth);
    public int Defense => CalculateStat(baseDefense);
    public int Speed => CalculateStat(baseSpeed);
    
    // Mevcut can (savaşta kullanılır)
    public int currentHealth;

    public PokemonData(string name, int startLevel = 1, string prefab = "")
    {
        pokemonName = name;
        prefabId = string.IsNullOrEmpty(prefab) ? name : prefab;
        catchDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        level = Mathf.Max(1, startLevel);
        currentXP = 0;
        
        // Level'e göre gereken XP hesapla
        xpToNextLevel = 100;
        for (int i = 1; i < level; i++)
        {
            xpToNextLevel = Mathf.RoundToInt(xpToNextLevel * 1.2f);
        }
        
        // Rastgele base statlar oluştur (düşük değerler - level ile artacak)
        // Level 1: ATK ~3-7, HP ~15-25, DEF ~2-5, SPD ~5-10
        baseAttack = UnityEngine.Random.Range(3, 8);
        baseHealth = UnityEngine.Random.Range(15, 26);
        baseDefense = UnityEngine.Random.Range(2, 6);
        baseSpeed = UnityEngine.Random.Range(5, 11);
        
        // Başlangıçta can full
        currentHealth = Health;
    }

    // Level'e göre stat hesaplama
    // Her level %15 artış sağlar
    private int CalculateStat(int baseStat)
    {
        return Mathf.RoundToInt(baseStat * (1 + (level - 1) * 0.15f));
    }

    // XP kazanma ve level atlama
    public bool AddXP(int amount)
    {
        currentXP += amount;
        bool leveledUp = false;
        
        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            LevelUp();
            leveledUp = true;
        }
        
        return leveledUp;
    }

    private void LevelUp()
    {
        level++;
        // Her level için gereken XP %20 artar
        xpToNextLevel = Mathf.RoundToInt(xpToNextLevel * 1.2f);
        // Level atladığında can full olsun
        currentHealth = Health;
        
        Debug.Log($"{pokemonName} level atladı! Yeni level: {level}");
    }

    // Canı iyileştir
    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, Health);
    }

    // Full iyileştir
    public void FullHeal()
    {
        currentHealth = Health;
    }

    // Hasar al
    public void TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(1, damage - Defense / 2);
        currentHealth = Mathf.Max(0, currentHealth - actualDamage);
    }

    // Bayıldı mı?
    public bool IsFainted => currentHealth <= 0;

    // Stat özeti
    public string GetStatsSummary()
    {
        return $"{pokemonName} (Lv.{level})\n" +
               $"HP: {currentHealth}/{Health}\n" +
               $"Attack: {Attack}\n" +
               $"Defense: {Defense}\n" +
               $"Speed: {Speed}\n" +
               $"XP: {currentXP}/{xpToNextLevel}";
    }
}

[Serializable]
public class PokemonInventory
{
    public List<PokemonData> caughtPokemon = new List<PokemonData>();
}
