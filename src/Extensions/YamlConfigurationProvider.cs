using Microsoft.Extensions.Configuration;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

public class YamlConfigurationProvider : ConfigurationProvider
{
    private readonly string _filePath;

    public YamlConfigurationProvider(string filePath)
    {
        _filePath = filePath;
    }

    public override void Load()
    {
        if (!File.Exists(_filePath))
            throw new FileNotFoundException($"YAML configuration file '{_filePath}' not found.");

        // Read the file content
        var yamlContent = File.ReadAllText(_filePath);

        // Deserialize YAML to a dictionary
        var deserializer = new DeserializerBuilder().Build();
        var yamlData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        // Pass the parsed YAML data into the base `Data` dictionary
        Data = Flatten(yamlData);
    }

    private IDictionary<string, string> Flatten(Dictionary<string, object> source, string parentKey = null)
    {
        var result = new Dictionary<string, string>();

        foreach (var kvp in source)
        {
            var currentKey = parentKey == null ? kvp.Key : $"{parentKey}:{kvp.Key}";

            if (kvp.Value is IDictionary<string, object> nestedDict)
            {
                // Recursively flatten nested dictionaries
                foreach (var nested in Flatten(nestedDict.ToDictionary(k => k.Key, k => (object)k.Value), currentKey))
                {
                    result[nested.Key] = nested.Value;
                }
            }
            else if (kvp.Value is IList<object> list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var listKey = $"{currentKey}:{i}";
                    var value = list[i];

                    if (value is IDictionary<string, object> listNestedDict)
                    {
                        foreach (var nested in Flatten(listNestedDict.ToDictionary(k => k.Key, k => (object)k.Value), listKey))
                        {
                            result[nested.Key] = nested.Value;
                        }
                    }
                    else
                    {
                        result[listKey] = value.ToString();
                    }
                }
            }
            else
            {
                result[currentKey] = kvp.Value?.ToString();
            }
        }

        return result;
    }
}

public class YamlConfigurationSource : IConfigurationSource
{
    private readonly string _filePath;

    public YamlConfigurationSource(string filePath)
    {
        _filePath = filePath;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new YamlConfigurationProvider(_filePath);
}

public static class YamlConfigurationExtensions
{
    public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, string filePath)
    {
        return builder.Add(new YamlConfigurationSource(filePath));
    }
}