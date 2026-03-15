namespace Hekiris.Application;

public sealed class OpenCodeHealth
{
    public bool Healthy { get; set; }

    public string Version { get; set; } = string.Empty;
}

public sealed record OpenCodeMessageResponse(string Text, BridgeMessageFormat Format)
{
    public static OpenCodeMessageResponse Empty { get; } = new(string.Empty, BridgeMessageFormat.PlainText);
}

public sealed class OpenCodeException : Exception
{
    public OpenCodeException(string message)
        : base(message)
    {
    }
}

public enum BridgeMessageFormat
{
    PlainText,
    Html,
    Json,
}
