using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backlog.Application;
using Backlog.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Backlog.Infrastructure;

public sealed class JwtSettings
{
    public string Key { get; init; } = "";

    public string Issuer { get; init; } = "backlog-api";

    public string Audience { get; init; } = "backlog-client";

    public int AccessTokenMinutes { get; init; } = 15;

    public void Validate()
    {
        if (Encoding.UTF8.GetByteCount(Key) < 32 || string.IsNullOrWhiteSpace(Issuer)
            || string.IsNullOrWhiteSpace(Audience) || AccessTokenMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException("Configuração JWT inválida. Forneça chave segura, emissor, audiência e duração.");
        }
    }
}

public sealed class SecurePasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<string> hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 210000 }));

    public string Hash(string password)
    {
        return hasher.HashPassword("", password);
    }

    public bool Verify(string hash, string password)
    {
        try
        {
            return hasher.VerifyHashedPassword("", hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class JwtTokenIssuer(JwtSettings settings) : ITokenIssuer
{
    public SignedToken Issue(User user, DateTimeOffset now)
    {
        var id = Guid.NewGuid();

        var expires = now.AddMinutes(settings.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, id.ToString()),
            new Claim("role", user.Role),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now.UtcDateTime, expires.UtcDateTime, credentials);

        return new SignedToken(new JwtSecurityTokenHandler().WriteToken(token), expires, id);
    }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
