using System.Net;

namespace Common.Exceptions;

[Serializable]
public class DomainException : Exception
{
    public DomainException(ErrorMessage error)
        : base(error?.ToString())
    {
        this.Reason = error ?? new ErrorMessage("UnknownError", "Unknown error");
    }

    public DomainException(ErrorMessage error, string message, Exception inner)
        : base(message, inner)
    {
        this.Reason = error ?? new ErrorMessage("UnknownError", message);
    }

    [Newtonsoft.Json.JsonProperty]
    public ErrorMessage Reason { get; private set; }

    [Newtonsoft.Json.JsonProperty]
    public HttpStatusCode Code { get; private set; } = HttpStatusCode.InternalServerError;
}
