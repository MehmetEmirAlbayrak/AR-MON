using UnityEngine;
using System.Collections;

public class PokeballCollision : MonoBehaviour
{
    private PokeballController controller;
    private GameObject capturedPokemon;
    private WildPokemon wildPokemonData;
    private bool hasCapturedPokemon = false;
    private Rigidbody rb;
    private float captureYPosition; // Pokemon yakalandığındaki Y pozisyonu
    
    [Header("Capture Settings")]
    private float fallDistance = 2f; // Pokemon yakalandıktan sonra düşmesi gereken mesafe
    private float shakeDelay = 1f; // Sallanma efekti süresi

    public void Initialize(PokeballController pokeballController)
    {
        controller = pokeballController;
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Pokeball collision with: " + collision.gameObject.name + " (Tag: " + collision.gameObject.tag + ")");
        
        // Eğer zaten bir Pokemon yakaladıysa, başka collision'ları yoksay
        if (hasCapturedPokemon)
        {
            return;
        }
        
        if (collision.gameObject.CompareTag("Pokemon"))
        {
            Debug.Log("Pokemon hit detected!");
            CaptureTemporarily(collision.gameObject);
        }
    }

    private void CaptureTemporarily(GameObject pokemon)
    {
        hasCapturedPokemon = true;
        capturedPokemon = pokemon;
        captureYPosition = transform.position.y; // Yakalama anındaki Y pozisyonunu kaydet
        
        // WildPokemon component'ini al
        wildPokemonData = pokemon.GetComponent<WildPokemon>();
        
        // Pokemon'u geçici olarak gizle (pokeball'ın içine girdi efekti)
        pokemon.SetActive(false);
        
        if (wildPokemonData != null)
        {
            float catchRate = wildPokemonData.GetCatchRate() * 100f;
            Debug.Log($"Level {wildPokemonData.level} Pokemon pokeball'a girdi! Yakalama şansı: %{catchRate:F1}");
        }
        else
        {
            Debug.Log("Pokemon pokeball'a girdi! Düşmesi bekleniyor...");
        }
        
        // Düşme kontrolü başlat
        StartCoroutine(WaitForFalling());
    }

    private IEnumerator WaitForFalling()
    {
        // Pokeball belli bir mesafe düşene kadar bekle
        while (rb != null && (captureYPosition - transform.position.y) < fallDistance)
        {
            yield return new WaitForSeconds(0.05f);
        }
        
        // Pokeball'u durdur
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        
        Debug.Log("Pokeball durdu! Sallanıyor...");
        
        // Sallanma efekti süresi bekle
        yield return new WaitForSeconds(shakeDelay);
        
        // Yakalama kararı ver
        DecideCapture();
    }

    private void DecideCapture()
    {
        if (capturedPokemon == null)
        {
            Debug.LogWarning("Captured Pokemon is null!");
            return;
        }

        // WildPokemon varsa level'e göre yakalama şansı, yoksa %50
        bool isCaught;
        int pokemonLevel = 1;
        
        if (wildPokemonData != null)
        {
            isCaught = wildPokemonData.TryCatch();
            pokemonLevel = wildPokemonData.level;
        }
        else
        {
            isCaught = Random.value <= 0.5f; // %50 şansla yakalanma
        }
        
        if (isCaught)
        {
            Debug.Log($"Pokemon yakalandı! (Level {pokemonLevel})");
            
            // Pokemon'un adını ve prefab ID'sini al
            string prefabId = PokemonPrefabRegistry.GetPrefabId(capturedPokemon);
            string pokemonName = wildPokemonData != null ? wildPokemonData.pokemonName : prefabId;
            
            // Çantaya ekle (prefab ID ile birlikte)
            if (PokemonBag.Instance != null)
            {
                PokemonBag.Instance.AddPokemon(pokemonName, pokemonLevel, prefabId);
            }
            
            // Pokemon'u yok et
            Destroy(capturedPokemon);
            
            // Controller'a bildir
            if (controller != null)
            {
                controller.OnPokemonCaught();
            }
        }
        else
        {
            Debug.Log($"Pokemon yakalanamadı! Level {pokemonLevel} Pokemon kaçıyor...");
            
            // Pokemon'u tekrar göster ve kaçır
            if (capturedPokemon != null)
            {
                capturedPokemon.SetActive(true);
                
                // Pokemon'u biraz uzağa taşı (kaçış efekti)
                Vector3 escapeDirection = Random.insideUnitSphere;
                escapeDirection.y = 0;
                capturedPokemon.transform.position += escapeDirection.normalized * 2f;
            }
            
            // Controller'a bildir
            if (controller != null)
            {
                controller.OnPokemonEscaped();
            }
        }
        
        // Durumu sıfırla
        hasCapturedPokemon = false;
        capturedPokemon = null;
        wildPokemonData = null;
    }
}
