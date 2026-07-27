using TradeMind.Identity.Application.DTOs;

namespace TradeMind.Identity.Application.Queries;

public sealed record GetCurrentIdentityQuery;
public sealed record GetApiKeyQuery(Guid ApiKeyId);
public sealed record SearchApiKeysQuery;
