using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskTracker.Application.Abstractions;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Infrastructure.Security;

public class JwtOptions
{
    public const int MinSecretLength = 32;

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "TaskTracker";
    public string Audience { get; set; } = "TaskTracker";
    public int ExpiresMinutes { get; set; } = 60;
}

/// <summary>Salted BCrypt hashes. The salt is generated per password and stored inside the hash.</summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}

public class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken Create(User user)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(_options.ExpiresMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            [
                new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new System.Security.Claims.Claim(JwtRegisteredClaimNames.Email, user.Email)
            ],
            now,
            expires,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
