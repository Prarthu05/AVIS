namespace Avis.IFactory;

public class IFactoryApiException : Exception
{
    public int? StatusCode { get; }
    public string? Payload { get; }

    public IFactoryApiException(string message, int? statusCode = null, string? payload = null)
        : base(message)
    {
        StatusCode = statusCode;
        Payload = payload;
    }
}
