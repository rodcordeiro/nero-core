using System.Text;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Writing;

/// <summary>
/// Shared UTF-8 payload ceiling for long free-text fields (Marco 23). Oversize is InvalidInput, not Compliance.
/// </summary>
public static class KnowledgeFieldLimits
{
    public const int MaxLongFieldUtf8Bytes = 64 * 1024;

    public static void EnsureUtf8WithinLimit(string? value, string parameterName, int maxBytes = MaxLongFieldUtf8Bytes)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var sizeBytes = Encoding.UTF8.GetByteCount(value);
        if (sizeBytes > maxBytes)
        {
            throw new ArgumentException(
                $"Field must not exceed {maxBytes} UTF-8 bytes (64 KiB); received {sizeBytes} bytes.",
                parameterName);
        }
    }

    public static void EnsureUtf8WithinLimit(params (string? Value, string ParameterName)[] fields)
    {
        foreach (var (value, parameterName) in fields)
        {
            EnsureUtf8WithinLimit(value, parameterName);
        }
    }
}
