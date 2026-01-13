using System.Text.Json.Serialization;

namespace C4Effects;

public class PlayerEffectsData
{
    [JsonPropertyName("PlayerEffects")]
    public Dictionary<ulong, string> PlayerEffects { get; set; } = new();

    public string GetPlayerEffect(ulong steamId)
    {
        if (PlayerEffects.TryGetValue(steamId, out var effect) && !string.IsNullOrEmpty(effect))
        {
            return effect;
        }
        return string.Empty; // Пустая строка означает использование глобального эффекта
    }

    public void SetPlayerEffect(ulong steamId, string effectKey)
    {
        PlayerEffects[steamId] = effectKey;
    }

    public void RemovePlayerEffect(ulong steamId)
    {
        PlayerEffects.Remove(steamId);
    }

    public bool HasPlayerEffect(ulong steamId)
    {
        return PlayerEffects.ContainsKey(steamId) && !string.IsNullOrEmpty(PlayerEffects[steamId]);
    }
}