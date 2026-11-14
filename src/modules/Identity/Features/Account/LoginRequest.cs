using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Features.Account;

public sealed record LoginRequest(
    [Required] string UserName,
    [Required] string Password);
