using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ecosphere.Infrastructure.Data.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Ecosphere.Infrastructure.Infrastructure.Auth.JWT;

public class JwtHandler : IJwtHandler
{
    private readonly IConfiguration _configuration;
    private readonly byte[] _key;
    private readonly string? _issuer;
    private readonly string? _audience;
    private readonly int _expiryMinutes;

    public JwtHandler(IConfiguration configuration)
    {
        _configuration = configuration;
        var secret = _configuration["JwtSettings:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        _key = Encoding.UTF8.GetBytes(secret);
        _issuer = _configuration["JwtSettings:Issuer"];
        _audience = _configuration["JwtSettings:Audience"];
        _expiryMinutes = int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "15");
    }

    public LoginResponse Create(JwtRequest request)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, request.UserId.ToString()),  // Use standard claim type like SoundMind
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("unique_name", request.EmailAddress),
            new Claim("username", request.UserName),
            new Claim("user_type", ((int)request.UserType).ToString()),
            new Claim(JwtRegisteredClaimNames.Iss, _issuer!),
            new Claim(JwtRegisteredClaimNames.Aud, _audience!),
        };

        foreach (var role in request.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_expiryMinutes),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return new LoginResponse
        {
            Token = tokenHandler.WriteToken(token),
            RefreshToken = GenerateRefreshToken(),
            Expires = new DateTimeOffset(token.ValidTo).ToUnixTimeSeconds()
        };
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
