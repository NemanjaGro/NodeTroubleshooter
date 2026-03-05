using System.Text.Json;
using NodeTroubleshooter.Model;

namespace NodeTroubleshooter.Core;

public static class SpecLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static SpecDatabase Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Knowledge base not found at: {path}\n" +
                $"Use --data <path> to specify the location of nodespecs.json");
        }

        var json = File.ReadAllText(path);
        var db = JsonSerializer.Deserialize<SpecDatabase>(json, JsonOptions);

        if (db == null)
        {
            throw new InvalidOperationException("Failed to deserialize knowledge base.");
        }

        return db;
    }
}
