using System.Collections;
using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace UmlautAdaptarr.Utilities;

public class ApiKeyMaskingEnricher : ILogEventEnricher
{
    private readonly List<string> apiKeys = new();

    public ApiKeyMaskingEnricher(string appsetting)
    {
        ExtractApiKeysFromAppSettings(appsetting);
        ExtractApiKeysFromEnvironmentVariables();
        apiKeys = new List<string>(apiKeys.Distinct());
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        //if (logEvent.Properties.TryGetValue("apikey", out var value) && value is ScalarValue scalarValue)
        //{
            var maskedValue = new ScalarValue("**Hidden Api Key**");
            foreach (var apikey in apiKeys) logEvent.AddOrUpdateProperty(new LogEventProperty(apikey, maskedValue));
       // }
    }


    /// <summary>
    ///     Scan all Env Variabels for known Apikeys
    /// </summary>
    /// <returns>List of all Apikeys</returns>
    public List<string> ExtractApiKeysFromEnvironmentVariables()
    {
        var envVariables = Environment.GetEnvironmentVariables();

        foreach (DictionaryEntry envVariable in envVariables)
            if (envVariable.Key.ToString()!.Contains("ApiKey"))
                apiKeys.Add(envVariable.Value.ToString());

        return apiKeys;
    }


    public List<string> ExtractApiKeysFromAppSettings(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var fileContent = File.ReadAllText(filePath);

                var pattern = "\"ApiKey\": \"(.*?)\"";
                var regex = new Regex(pattern);
                var matches = regex.Matches(fileContent);

                foreach (Match match in matches) apiKeys.Add(match.Groups[1].Value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return apiKeys;
    }
}


