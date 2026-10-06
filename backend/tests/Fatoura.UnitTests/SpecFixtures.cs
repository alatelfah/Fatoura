using System.Text.Json;

namespace Fatoura.UnitTests;

internal static class SpecFixtures
{
    public static JsonElement Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "spec", fileName);
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    public static decimal Dec(this JsonElement e) =>
        decimal.Parse(e.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
}
