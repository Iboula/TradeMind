using TradeMind.Identity.Domain.Permissions;

namespace TradeMind.Identity.Application.Authorization;

public sealed record AuthorizationRequirement(Permission Permission);
