using System.Text.Json;
using System.Text.Json.Serialization;

namespace UmlautAdaptarr.Utilities;

public static class Helper
{
    public static void ShowLogo()
    {
        Console.WriteLine(
            "\r\n _   _           _             _    ___      _             _                  \r\n| | | |         | |           | |  / _ \\    | |           | |                 \r\n| | | |_ __ ___ | | __ _ _   _| |_/ /_\\ \\ __| | __ _ _ __ | |_ __ _ _ __ _ __ \r\n| | | | '_ ` _ \\| |/ _` | | | | __|  _  |/ _` |/ _` | '_ \\| __/ _` | '__| '__|\r\n| |_| | | | | | | | (_| | |_| | |_| | | | (_| | (_| | |_) | || (_| | |  | |   \r\n \\___/|_| |_| |_|_|\\__,_|\\__,_|\\__\\_| |_/\\__,_|\\__,_| .__/ \\__\\__,_|_|  |_|   \r\n                                                    | |                       \r\n                                                    |_|                       \r\n");
    }

    public static void ShowInformation()
    {
        Console.WriteLine("--------------------------[IP Leak Test]-----------------------------");
        var ipInfo = GetPublicIpAddressInfoAsync().GetAwaiter().GetResult();

        if (ipInfo != null)
        {
            Console.WriteLine($"Your Public IP Address is '{ipInfo.Ip}'");
            Console.WriteLine($"Hostname: {ipInfo.Hostname}");
            Console.WriteLine($"City: {ipInfo.City}");
            Console.WriteLine($"Region: {ipInfo.Region}");
            Console.WriteLine($"Country: {ipInfo.Country}");
            Console.WriteLine($"Provider: {ipInfo.Org}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Could not retrieve public IP information.");
            Console.ResetColor();
        }

        Console.WriteLine("--------------------------------------------------------------------");
    }


    private static async Task<IpInfo?> GetPublicIpAddressInfoAsync()
    {
        using (var client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                var response = await client.GetAsync("https://ipinfo.io/json");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<IpInfo>(content);
            }
            catch
            {
                return null;
            }
        }
    }
}
