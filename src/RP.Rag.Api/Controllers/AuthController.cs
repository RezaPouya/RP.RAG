using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RP.Rag.Application.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RP.RAG.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IOptions<List<AppUser>> users, IOptions<JwtSettings> jwtSettings) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var user = users.Value.FirstOrDefault(u =>
            u.Username == request.Username && u.Password == request.Password);

        if (user is null) return Unauthorized(new { error = "Invalid credentials" });

        var jwt = jwtSettings.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId),
            new Claim(ClaimTypes.Name, user.Username)
        };

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(jwt.ExpirationInMinutes),
            signingCredentials: creds);

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }
}

public sealed class LoginRequest
{
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
}