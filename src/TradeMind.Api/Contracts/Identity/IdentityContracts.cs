using TradeMind.Api.Contracts.Common;
using TradeMind.Identity.Application.DTOs;

namespace TradeMind.Api.Contracts.Identity;

public sealed record IdentityMeResponse(CurrentIdentityDto Data, ApiResponseMetadata Metadata);
public sealed record IdentityApiKeyResponse(ApiKeySecretResponse Data, ApiResponseMetadata Metadata);
public sealed record IdentityApiKeyListResponse(IReadOnlyList<ApiKeyDto> Items, ApiResponseMetadata Metadata);
