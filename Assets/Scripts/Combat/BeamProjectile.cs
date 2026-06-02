using UnityEngine;
using ARMON.Data;

public class BeamProjectile : MonoBehaviour
{
    [SerializeField] float travelSeconds = 0.3f;
    Vector3 start, end;
    float t;
    PokemonSkill skill;
    int damage;
    WildPokemon target;
    Renderer rend;

    public void Init(Vector3 fromWorld, WildPokemon t, PokemonSkill s, int attackerAttack)
    {
        start = fromWorld;
        end   = t != null ? t.transform.position : fromWorld + Vector3.forward;
        skill = s;
        target = t;
        damage = s.DamageAgainst(attackerAttack);
        transform.position = start;
        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            var mat = rend.material;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", s.beamColor);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", s.beamColor);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", s.beamColor * 2f);
        }
    }

    void Update()
    {
        if (travelSeconds <= 0f) return;
        t += Time.deltaTime / travelSeconds;
        transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
        if (target != null && t >= 1f)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
        }
        else if (t >= 1f) Destroy(gameObject);
    }
}
