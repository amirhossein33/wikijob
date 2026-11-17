using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InterviewHub.Business.Auth;
using InterviewHub.Data.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InterviewHub.Api.Auth;

// Options are validated at startup so a missing signing key cannot issue weak tokens.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
    public int LifetimeMinutes { get; init; } = 60;
}

public interface ITokenService
{
    TokenResponse Create(AppUser user, IReadOnlyCollection<string> roles);
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public TokenResponse Create(AppUser user, IReadOnlyCollection<string> roles)
    {
        var settings = options.Value;
        var expires = DateTimeOffset.UtcNow.AddMinutes(settings.LifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            expires: expires.UtcDateTime, signingCredentials: credentials);

        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expires, roles.ToArray());
    }
}
