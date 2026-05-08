using System.ComponentModel;

namespace MicrosoftAgentFrameworkAPI.Agents;

/// <summary>
/// Phase 2 — Agent Tools
/// Static methods decorated with [Description] that the agent can call automatically.
/// Registered with the agent via AIFunctionFactory.Create().
/// </summary>
public static class AgentTools
{
    private static readonly string[] Conditions = ["sunny", "cloudy", "rainy", "partly cloudy", "stormy"];

    /// <summary>
    /// Gets simulated weather data for a given location.
    /// The agent calls this automatically when the user asks about weather.
    /// </summary>
    [Description("Get the current weather for a given location.")]
    public static string GetWeather(
        [Description("The city or location to get the weather for.")] string location)
    {
        var condition = Conditions[Random.Shared.Next(Conditions.Length)];
        var tempC = Random.Shared.Next(-5, 35);
        var tempF = 32 + (int)(tempC / 0.5556);
        return $"The weather in {location} is {condition} with a temperature of {tempC}°C ({tempF}°F).";
    }

    /// <summary>
    /// Gets a 5-day weather forecast for a given location.
    /// </summary>
    [Description("Get a 5-day weather forecast for a given location.")]
    public static string GetWeatherForecast(
        [Description("The city or location to get the forecast for.")] string location)
    {
        var days = Enumerable.Range(1, 5).Select(i =>
        {
            var date = DateTime.Today.AddDays(i).ToString("ddd, MMM d");
            var condition = Conditions[Random.Shared.Next(Conditions.Length)];
            var tempC = Random.Shared.Next(-5, 35);
            return $"  {date}: {condition}, {tempC}°C";
        });

        return $"5-day forecast for {location}:\n{string.Join("\n", days)}";
    }
}
