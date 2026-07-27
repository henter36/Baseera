namespace Baseera.Application.Abstractions;

public interface ISensitiveValueProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}
