using UnityEngine;
using System.Collections;

public class PokeballCollision : MonoBehaviour
{
    private PokeballController controller;
    private GameObject capturedPokemon;
    private WildPokemon wildPokemonData;
    private bool hasCapturedPokemon = false;
    private Rigidbody rb;
    private float captureYPosition;
    
    [Header("Capture Settings")]
    private float fallDistance = 1.5f;
    private float rollDuration = 2f; // Yuvarlanma süresi
    private float rollSpeed = 360f; // Derece/saniye

    public void Initialize(PokeballController pokeballController)
    {
        controller = pokeballController;
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasCapturedPokemon) return;
        
        if (collision.gameObject.CompareTag("Pokemon"))
        {
            Debug.Log("Pokemon yakalandı!");
            CaptureTemporarily(collision.gameObject);
        }
    }

    private void CaptureTemporarily(GameObject pokemon)
    {
        hasCapturedPokemon = true;
        capturedPokemon = pokemon;
        captureYPosition = transform.position.y;
        
        wildPokemonData = pokemon.GetComponent<WildPokemon>();
        pokemon.SetActive(false);
        
        if (wildPokemonData != null)
        {
            float catchRate = wildPokemonData.GetCatchRate() * 100f;
            Debug.Log($"Level {wildPokemonData.level} Pokemon pokeball'a girdi! Yakalama şansı: %{catchRate:F1}");
        }
        
        StartCoroutine(WaitForFalling());
    }

    private IEnumerator WaitForFalling()
    {
        // Düşmesini bekle
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
        
        // Yerde yuvarlanma animasyonu
        yield return StartCoroutine(RollAnimation());
        
        // Yakalama kararı
        DecideCapture();
    }

    /// <summary>
    /// Pokeball yerde yuvarlanır (kendi etrafında döner)
    /// </summary>
    private IEnumerator RollAnimation()
    {
        float elapsed = 0f;
        Vector3 rollAxis = Vector3.right; // Sağa doğru yuvarlanma
        
        // 3 kez ileri-geri sallan
        int shakeCount = 3;
        float shakeAngle = 30f;
        
        for (int i = 0; i < shakeCount; i++)
        {
            // Sağa döndür
            float shakeDuration = rollDuration / (shakeCount * 2);
            elapsed = 0;
            Quaternion startRot = transform.rotation;
            Quaternion targetRot = startRot * Quaternion.Euler(0, 0, shakeAngle);
            
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shakeDuration;
                transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
                yield return null;
            }
            
            // Sola döndür
            elapsed = 0;
            startRot = transform.rotation;
            targetRot = startRot * Quaternion.Euler(0, 0, -shakeAngle * 2);
            
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shakeDuration;
                transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
                yield return null;
            }
        }
        
        // Düz pozisyona dön
        elapsed = 0;
        Quaternion currentRot = transform.rotation;
        Quaternion uprightRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        float returnDuration = 0.2f;
        
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            transform.rotation = Quaternion.Lerp(currentRot, uprightRot, t);
            yield return null;
        }
    }

    private void DecideCapture()
    {
        if (capturedPokemon == null)
        {
            Debug.LogWarning("Captured Pokemon is null!");
            return;
        }

        bool isCaught;
        int pokemonLevel = 1;
        
        if (wildPokemonData != null)
        {
            isCaught = wildPokemonData.TryCatch();
            pokemonLevel = wildPokemonData.level;
        }
        else
        {
            isCaught = Random.value <= 0.5f;
        }
        
        if (isCaught)
        {
            Debug.Log($"Pokemon yakalandı! (Level {pokemonLevel})");
            StartCoroutine(CatchSuccessAnimation());
        }
        else
        {
            Debug.Log($"Pokemon kaçtı! (Level {pokemonLevel})");
            StartCoroutine(CatchFailAnimation());
        }
    }
    
    /// <summary>
    /// Yakalama başarılı - pokeball zıplar ve kaybolur
    /// </summary>
    private IEnumerator CatchSuccessAnimation()
    {
        // Zıplama animasyonu
        yield return StartCoroutine(JumpAnimation(3)); // 3 kez zıpla
        
        // Parlama efekti
        yield return StartCoroutine(FlashEffect());
        
        // Pokemon'u çantaya ekle
        string prefabId = PokemonPrefabRegistry.GetPrefabId(capturedPokemon);
        string pokemonName = wildPokemonData != null ? wildPokemonData.pokemonName : prefabId;
        int pokemonLevel = wildPokemonData != null ? wildPokemonData.level : 1;
        
        if (PokemonBag.Instance != null)
        {
            PokemonBag.Instance.AddPokemon(pokemonName, pokemonLevel, prefabId);
        }
        
        Destroy(capturedPokemon);
        
        // Pokeball kaybolur
        yield return StartCoroutine(ShrinkAndDisappear());
        
        // Temizle
        hasCapturedPokemon = false;
        capturedPokemon = null;
        wildPokemonData = null;
        
        if (controller != null)
        {
            controller.OnPokemonCaught();
        }
    }
    
    /// <summary>
    /// Zıplama animasyonu
    /// </summary>
    private IEnumerator JumpAnimation(int jumpCount)
    {
        float jumpHeight = 0.3f;
        float jumpDuration = 0.2f;
        
        for (int i = 0; i < jumpCount; i++)
        {
            Vector3 startPos = transform.position;
            Vector3 peakPos = startPos + Vector3.up * jumpHeight;
            
            // Yukarı
            float elapsed = 0;
            while (elapsed < jumpDuration / 2)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (jumpDuration / 2);
                t = 1 - (1 - t) * (1 - t); // Ease out
                transform.position = Vector3.Lerp(startPos, peakPos, t);
                yield return null;
            }
            
            // Aşağı
            elapsed = 0;
            while (elapsed < jumpDuration / 2)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (jumpDuration / 2);
                t = t * t; // Ease in
                transform.position = Vector3.Lerp(peakPos, startPos, t);
                yield return null;
            }
            
            // Her zıplamada biraz küçült
            jumpHeight *= 0.6f;
            
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    /// <summary>
    /// Parlama efekti
    /// </summary>
    private IEnumerator FlashEffect()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend == null) yield break;
        
        Color originalColor = rend.material.color;
        
        // 2 kez parla
        for (int i = 0; i < 2; i++)
        {
            rend.material.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            rend.material.color = originalColor;
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    /// <summary>
    /// Küçülerek kaybol
    /// </summary>
    private IEnumerator ShrinkAndDisappear()
    {
        Vector3 originalScale = transform.localScale;
        float duration = 0.3f;
        float elapsed = 0;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            yield return null;
        }
    }
    
    /// <summary>
    /// Yakalama başarısız - pokeball açılır ve Pokemon çıkar
    /// </summary>
    private IEnumerator CatchFailAnimation()
    {
        // Pokeball sallanır ve açılır efekti
        yield return StartCoroutine(ShakeAndOpen());
        
        // Pokemon'u geri getir
        if (capturedPokemon != null)
        {
            capturedPokemon.SetActive(true);
            
            // Pokemon'u pokeball'ın yanına koy
            Vector3 escapeDirection = Random.insideUnitSphere;
            escapeDirection.y = 0;
            capturedPokemon.transform.position = transform.position + escapeDirection.normalized * 1.5f;
            
            // Rigidbody varsa hızını sıfırla
            Rigidbody pokemonRb = capturedPokemon.GetComponent<Rigidbody>();
            if (pokemonRb != null)
            {
                pokemonRb.linearVelocity = Vector3.zero;
                pokemonRb.angularVelocity = Vector3.zero;
                pokemonRb.isKinematic = true;
            }
            
            // NavMeshAgent varsa durdur
            UnityEngine.AI.NavMeshAgent agent = capturedPokemon.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.velocity = Vector3.zero;
                agent.isStopped = true;
                agent.isStopped = false;
            }
        }
        
        // Temizle
        hasCapturedPokemon = false;
        capturedPokemon = null;
        wildPokemonData = null;
        
        if (controller != null)
        {
            controller.OnPokemonEscaped();
        }
    }
    
    /// <summary>
    /// Sallan ve aç efekti
    /// </summary>
    private IEnumerator ShakeAndOpen()
    {
        // Hızlı sallanma
        float shakeDuration = 0.5f;
        float elapsed = 0;
        float shakeIntensity = 20f;
        
        Quaternion originalRot = transform.rotation;
        
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float shake = Mathf.Sin(elapsed * 50f) * shakeIntensity * (1 - elapsed / shakeDuration);
            transform.rotation = originalRot * Quaternion.Euler(0, 0, shake);
            yield return null;
        }
        
        transform.rotation = originalRot;
        
        // Patlama efekti - büyüyüp küçül
        Vector3 originalScale = transform.localScale;
        
        // Büyü
        elapsed = 0;
        float expandDuration = 0.1f;
        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / expandDuration;
            transform.localScale = Vector3.Lerp(originalScale, originalScale * 1.3f, t);
            yield return null;
        }
        
        // Hızla küçül ve kaybol
        elapsed = 0;
        float shrinkDuration = 0.15f;
        Vector3 expandedScale = transform.localScale;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            transform.localScale = Vector3.Lerp(expandedScale, Vector3.zero, t);
            yield return null;
        }
    }
}
