using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Api.V1.DTOs.Response;

namespace UpdateHub.Server.Application.Abstractions.Services;

public interface IUpdateService
{
    Task<CheckResponseDto> CheckUpdatesAsync(CheckRequestDto request);
    Task<CheckResponseDto> UpdateAsync(CheckRequestDto request);
}