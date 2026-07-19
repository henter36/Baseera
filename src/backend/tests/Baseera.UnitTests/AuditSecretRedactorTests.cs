using Baseera.Infrastructure.Audit;

namespace Baseera.UnitTests;

public sealed class AuditSecretRedactorTests
{
    [Fact]
    public void Redacts_password_payload()
    {
        var protectedJson = AuditSecretRedactor.Protect("""{"password":"super-secret"}""");
        Assert.Equal("""{"redacted":true}""", protectedJson);
    }

    [Fact]
    public void Leaves_benign_payload()
    {
        var json = """{"action":"Create","entity":"Region"}""";
        Assert.Equal(json, AuditSecretRedactor.Protect(json));
    }

    [Fact]
    public void Large_adversarial_payload_with_secret_word_is_redacted()
    {
        var noise = new string('a', 50_000);
        var json = $$"""{"note":"{{noise}}","token":"leak-me"}""";
        var protectedJson = AuditSecretRedactor.Protect(json);
        Assert.Equal("""{"redacted":true}""", protectedJson);
        Assert.DoesNotContain("leak-me", protectedJson);
    }

    [Fact]
    public void ConnectionString_keyword_is_redacted()
    {
        var protectedJson = AuditSecretRedactor.Protect("""{"connectionString":"Server=.;Password=x"}""");
        Assert.Equal("""{"redacted":true}""", protectedJson);
    }
}
