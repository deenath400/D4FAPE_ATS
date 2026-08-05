namespace Ats.Service.Auth.Dtos;

public record RegisterRequestDto(string Email, string Password, string FirstName, string LastName);
