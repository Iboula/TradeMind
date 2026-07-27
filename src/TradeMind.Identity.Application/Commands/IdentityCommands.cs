using TradeMind.Identity.Application.DTOs;

namespace TradeMind.Identity.Application.Commands;

public sealed record CreateApiKeyCommand(CreateApiKeyRequest Request, string? EnvironmentName = null);
public sealed record RotateApiKeyCommand(Guid ApiKeyId, RotateApiKeyRequest Request, string? EnvironmentName = null);
public sealed record RevokeApiKeyCommand(Guid ApiKeyId, RevokeApiKeyRequest Request);
public sealed record DisableApiKeyCommand(Guid ApiKeyId, ApiKeyMutationRequest Request);
public sealed record EnableApiKeyCommand(Guid ApiKeyId, ApiKeyMutationRequest Request);
