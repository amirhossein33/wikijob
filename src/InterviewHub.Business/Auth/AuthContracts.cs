using System.ComponentModel.DataAnnotations;

namespace InterviewHub.Business.Auth;

// Public account types map to Identity roles; one account may have both member roles.
public static class AppRoles
{
    public const string Candidate = "Candidate";
    public const string Interviewer = "Interviewer";
    public const string Admin = "Admin";
    public static readonly string[] All = [Candidate, Interviewer, Admin];
}

// DTOs limit the fields accepted and returned by authentication endpoints.
public sealed record RegisterRequest(
    [Required, MinLength(3), MaxLength(100)] string UserName,
    [Required, EmailAddress] string Email,
    [Required] string Password,
    [Required] string Role);

public sealed record LoginRequest(
    [Required] string UserNameOrEmail,
    [Required] string Password);

public sealed record TokenResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, string[] Roles);
public sealed record AccountResponse(Guid Id, string UserName, string Email, string[] Roles);
