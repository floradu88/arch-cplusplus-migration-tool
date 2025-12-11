namespace SolutionDependencyMapper.Models;

public sealed class ConfigurationPlatform
{
    public string Configuration { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;

    public string Key => string.IsNullOrWhiteSpace(Platform) ? Configuration : $"{Configuration}|{Platform}";

    public static ConfigurationPlatform FromKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new ConfigurationPlatform();

        var parts = key.Split('|', 2, StringSplitOptions.TrimEntries);
        return new ConfigurationPlatform
        {
            Configuration = parts.Length > 0 ? parts[0] : key,
            Platform = parts.Length > 1 ? parts[1] : string.Empty
        };
    }
}


