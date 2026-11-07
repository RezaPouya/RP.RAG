namespace RP.Rag.Application.Models;

public class AppUser
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class JwtSettings
{
    public string SecretKey { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int ExpirationInMinutes { get; set; }
}
