using System.Text.Json;

sealed class ReactionSettings
{
    public required int MinDelayMs { get; init; }
    public required int MaxDelayMs { get; init; }

    public static ReactionSettings Load(string path)
    {
        ReactionSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<ReactionSettings>(File.ReadAllText(path));
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Configuración inválida en {path}. {error.Message}", error);
        }

        if (settings is null || settings.MinDelayMs < 0 || settings.MaxDelayMs < settings.MinDelayMs)
            throw new InvalidDataException(
                $"Configuración inválida en {path}. Se requiere 0 <= MinDelayMs <= MaxDelayMs.");

        return settings;
    }
}
