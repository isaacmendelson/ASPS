using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Common.Enums;
using Newtonsoft.Json;

namespace Common.Exceptions;

[DataContract]
[JsonObject]
public sealed class ErrorMessage : Models.LocalizableMessage
{
    private readonly Dictionary<string, object> data = new();

    private ErrorMessage()
    {
        this.Key = "UnknownError";
        this.Message = "Unknown error.";
    }

    public ErrorMessage(string key, string message, ResultStatusCode? resultCode = null)
    {
        this.Key = key;
        this.Message = message;
        this.StatusCode = resultCode;
    }

    public ErrorMessage(string key, string message, string fromFile, string fromMethod, ResultStatusCode? resultCode = null)
    {
        this.Key = key;
        this.Message = message;
        this.StatusCode = resultCode;
    }

    [DataMember]
    public string Message { get; private set; }

    [DataMember]
    public ResultStatusCode? StatusCode { get; private set; }

    [NotMapped] 
    public string? OriginalRaisedErrorKey { get; set; }

    public object this[string key]
    {
        get { return this.data[key]; }
        set { this.data[key] = value; }
    }

    public List<string> GetDataKeys()
    {
        return this.data.Keys.ToList();
    }

    [DataMember(Name = "Data")]
    private KeyValuePair<string, object>[] SerializedData
    {
        get { return this.data.ToArray(); }
        set
        {
            if (value is not null)
            {
                foreach (var i in value)
                {
                    this.data[i.Key] = i.Value;
                }
            }
        }
    }

    public ErrorMessage AddParam(string key, bool value)
    {
        this.data[key] = value;
        return this;
    }

    public ErrorMessage AddParam(string key, int quantity)
    {
        this.data[key] = quantity;
        return this;
    }

    public ErrorMessage AddParam(string key, string value)
    {
        this.data[key] = value;
        return this;
    }

    public ErrorMessage AddParamIfNotNull(string key, Models.Key? value)
    {
        if (value is not null)
        {
            this.data[key] = value;
        }
        return this;
    }

    public ErrorMessage AddParamIfNotNull(string key, string? value)
    {
        if (value is not null)
        {
            this.data[key] = value;
        }
        return this;
    }

    public ErrorMessage AddParam(string key, IEnumerable<string> value)
    {
        this.data[key] = value.ToArray();
        return this;
    }

    public ErrorMessage AddParam(string key, Models.Tag value)
    {
        this.data[key] = value;
        return this;
    }

    public ErrorMessage AddParamIfNotNull(string key, Models.Tag? value)
    {
        if (value is not null)
        {
            this.data[key] = value;
        }
        return this;
    }

    public ErrorMessage AddParam(string key, IEnumerable<Models.Tag> value)
    {
        this.data[key] = value.ToArray();
        return this;
    }

    public ErrorMessage AddParam(string key, IEnumerable<Models.Key> value)
    {
        this.data[key] = value.ToArray();
        return this;
    }

    public ErrorMessage AddParam(string key, Models.Key value)
    {
        this.data[key] = value.ToString();
        return this;
    }

    public override string ToString()
    {
        return this.Key;
    }

    // Helper method to create common error messages
    public static ErrorMessage Create(string key, string message, ResultStatusCode? statusCode = null)
    {
        return new ErrorMessage(key, message, statusCode);
    }
}

// Common error message factory
public static class ParameterValueInvalid
{
    public static ErrorMessage Create(string message, string parameterName)
    {
        return new ErrorMessage("ParameterValueInvalid", message, ResultStatusCode.ValidationError)
            .AddParam("ParameterName", parameterName);
    }
}
