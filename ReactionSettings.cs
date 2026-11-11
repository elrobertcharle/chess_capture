using System.Text.Json;

sealed class ReactionSettings
{
    public required int MinDelayMs { get; init; }
    public required int MaxDelayMs { get; init; }

    public static ReactionSettings Defaults { get; } = new() { MinDelayMs = 600, MaxDelayMs = 2900 };

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

        if (settings is null)
            throw new InvalidDataException($"Configuración inválida en {path}.");

        settings.Validate();
        return settings;
    }

    public void Save(string path)
    {
        Validate();
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporaryPath, json + Environment.NewLine);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private void Validate()
    {
        if (MinDelayMs < 0 || MaxDelayMs < MinDelayMs)
            throw new InvalidDataException("El mínimo debe ser mayor o igual a 0 y el máximo debe ser mayor o igual al mínimo.");
    }
}
