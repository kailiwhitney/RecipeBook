using System.Text.Json;

namespace RecipeBook.Services;

// Thin client over USDA FoodData Central. The key comes from configuration, never
// source: https://fdc.nal.usda.gov/api-key-signup
public class UsdaFoodClient
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly ILogger<UsdaFoodClient> _logger;

    public UsdaFoodClient(HttpClient http, IConfiguration config, ILogger<UsdaFoodClient> logger)
    {
        _http = http;
        _logger = logger;

        var baseUrl = config["Usda:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _http.BaseAddress = new Uri(baseUrl);
        }

        _apiKey = config["Usda:FdcApiKey"];
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            // Ingredient lookup is optional; the app still works with free text.
            _logger.LogWarning("Usda:FdcApiKey is not configured; ingredient lookup disabled.");
        }
    }

    public async Task<List<FoodSearchHit>> SearchAsync(string query, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(_apiKey)) return [];

        // FDC takes the key as a query parameter, not a header.
        var url = $"foods/search?api_key={_apiKey}&dataType=SR,Legacy&limit={limit}" +
                  $"&query={Uri.EscapeDataString(query)}";

        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var results = new List<FoodSearchHit>();

            foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                if (!item.TryGetProperty("fdcId", out var id)) continue;
                if (!item.TryGetProperty("description", out var name)) continue;
                results.Add(new FoodSearchHit(id.GetInt32(), name.GetString() ?? ""));
            }
            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "USDA lookup failed for {Query}", query);
            return [];
        }
    }
}

public record FoodSearchHit(int FdcId, string Name);
