using Microsoft.AspNetCore.DataProtection;

namespace Alfred2.Services;

/// <summary>
/// Cifra/descifra los tokens de calendario antes de guardarlos en la BD.
/// Usa DataProtection de ASP.NET, así no hay que manejar claves a mano.
/// </summary>
public class TokenProtector
{
    private readonly IDataProtector _protector;

    public TokenProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Alfred.CalendarTokens.v1");

    public string? Protect(string? plaintext)
        => string.IsNullOrEmpty(plaintext) ? plaintext : _protector.Protect(plaintext);

    /// <summary>
    /// Devuelve el texto plano, o null si no se puede descifrar (token viejo en
    /// texto plano o clave rotada) → el llamador lo trata como "hay que reconectar".
    /// </summary>
    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;
        try { return _protector.Unprotect(ciphertext); }
        catch { return null; }
    }
}
