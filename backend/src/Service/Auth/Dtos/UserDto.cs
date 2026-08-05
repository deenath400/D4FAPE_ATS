namespace Ats.Service.Auth.Dtos;

using System;
using System.Collections.Generic;

public record UserDto(Guid Id, string Email, string FirstName, string LastName, IReadOnlyList<string> Roles);
