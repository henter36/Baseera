namespace Baseera.UnitTests;

using System.Text.Json;

public sealed class AppSettingsExampleTests
{
    [Fact]
    public async Task Example_settings_do_not_include_connection_string_credentials()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../Baseera.Api/appsettings.example.json"));

        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);

        var connectionString = document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("Baseera")
            .GetString();

        Assert.Equal(string.Empty, connectionString);
    }
}
