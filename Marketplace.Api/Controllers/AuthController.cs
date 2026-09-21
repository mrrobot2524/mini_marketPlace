using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login(int userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("super-secret-key-for-marketplace-12345"));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler()
            .WriteToken(token);

        return Ok(new
        {
            token = tokenString
        });
    }
}


/*{
"token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjEiLCJleHAiOjE3ODk4NDk3NzR9.JNhQKVg1iLSqUePzrAtqqU20gUuJYwjGgpRRVCx3RFI"
}*/