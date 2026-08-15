using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Domain.ValueObjects;

namespace UpdateHub.Server.Application.Validators;

public static class CheckRequestValidator
{
    public static bool IsValid(CheckRequestDto request, out List<string> errors)
    {
        errors = [];

        if (request == null)
        {
            errors.Add("Request cannot be null");
            return false;
        }

        // Проверка ClientInfo
        if (request.ClientInfo == null)
        {
            errors.Add("Client information is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.ClientInfo.ClientId))
        {
            errors.Add("Client ID (UUID) is required");
        }
        else
        {
            // Проверка формата UUID
            if (!Guid.TryParse(request.ClientInfo.ClientId, out _))
            {
                errors.Add($"Invalid UUID format: {request.ClientInfo.ClientId}");
            }
        }

        if (string.IsNullOrWhiteSpace(request.ClientInfo.Hostname))
        {
            errors.Add("Hostname is required");
        }

        if (string.IsNullOrWhiteSpace(request.ClientInfo.IpAddress))
        {
            errors.Add("IP address is required");
        }

        // Проверка манифеста (опционально, но если передан — должен быть валидным)
        if (request.Manifest != null)
        {
            foreach (var entry in request.Manifest)
            {
                var path = entry.Key;
                var md5 = entry.Value;

                // Проверка пути
                try
                {
                    var relativePath = new RelativePath(path);
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"Invalid path in manifest: {path} - {ex.Message}");
                }

                // Проверка MD5 (должен быть 32 символа hex)
                if (!string.IsNullOrEmpty(md5) && !IsValidMd5(md5))
                {
                    errors.Add($"Invalid MD5 hash for {path}: {md5}");
                }
            }
        }

        return errors.Count == 0;
    }

    private static bool IsValidMd5(string md5)
    {
        if (string.IsNullOrEmpty(md5)) return false;
        if (md5.Length != 32) return false;
        return md5.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }
}