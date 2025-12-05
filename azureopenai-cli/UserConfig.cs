using System.Text.Json;

namespace AzureOpenAI_CLI;

/// <summary>
/// Manages persistent user configuration for the CLI, including model selection.
/// Stores configuration in the user's home directory.
/// </summary>
public class UserConfig
{
    private const string ConfigFileName = ".azureopenai-cli.json";
    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ConfigFileName
    );

    public string? ActiveModel { get; set; }
    public List<string> AvailableModels { get; set; } = new();

    /// <summary>
    /// Loads the user configuration from the config file, or creates a new one if it doesn't exist.
    /// </summary>
    public static UserConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize<UserConfig>(json) ?? new UserConfig();
            }
        }
        catch (Exception)
        {
            // If there's any error reading the config, return a new instance
        }
        return new UserConfig();
    }

    /// <summary>
    /// Saves the current configuration to the config file.
    /// </summary>
    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARNING] Could not save config: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes the available models from the environment variable if not already set.
    /// </summary>
    public void InitializeFromEnvironment(string? modelsEnvVar)
    {
        if (string.IsNullOrEmpty(modelsEnvVar))
            return;

        // Parse comma-separated list of models
        var models = modelsEnvVar.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        if (models.Count > 0)
        {
            AvailableModels = models;

            // If no active model is set, use the first one
            if (string.IsNullOrEmpty(ActiveModel) || !AvailableModels.Contains(ActiveModel))
            {
                ActiveModel = AvailableModels[0];
            }
        }
    }

    /// <summary>
    /// Sets the active model if it's in the available models list.
    /// </summary>
    public bool SetActiveModel(string modelName)
    {
        if (AvailableModels.Contains(modelName, StringComparer.OrdinalIgnoreCase))
        {
            ActiveModel = AvailableModels.First(m => m.Equals(modelName, StringComparison.OrdinalIgnoreCase));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the path to the config file for display purposes.
    /// </summary>
    public static string GetConfigPath() => ConfigFilePath;
}
