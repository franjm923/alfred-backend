using System.Security.Claims;
using Alfred2.DBContext;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Alfred2.Services;

/// <summary>
/// Agrega el claim de rol (leído de la BD) al principal autenticado.
/// Google no envía el rol de la app, así que sin esto [Authorize(Roles=...)] nunca matchea.
/// </summary>
public class RoleClaimsTransformation : IClaimsTransformation
{
    private readonly AppDbContext _db;

    public RoleClaimsTransformation(AppDbContext db) => _db = db;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return principal;
        if (principal.FindFirst(ClaimTypes.Role) is not null) return principal;

        var email = principal.FindFirst("email")?.Value
                    ?? principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email)) return principal;

        var role = await _db.Users
            .Where(u => u.Email == email)
            .Select(u => u.Role)
            .FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(role)) return principal;

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        principal.AddIdentity(identity);
        return principal;
    }
}
