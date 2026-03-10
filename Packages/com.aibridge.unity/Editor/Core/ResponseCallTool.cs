#nullable enable

namespace UnityAiBridge.Editor
{
    /// <summary>
    /// Response from a Bridge tool call.
    /// Used by tools that need to return structured responses (especially deferred ones).
    /// </summary>
    public class ResponseCallTool
    {
        public enum Status
        {
            Success,
            Error,
            Processing
        }

        public Status ResponseStatus { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? RequestID { get; set; }

        public ResponseCallTool SetRequestID(string? requestId)
        {
            RequestID = requestId;
            return this;
        }

        public static ResponseCallTool Success(string message)
        {
            return new ResponseCallTool { ResponseStatus = Status.Success, Message = message };
        }

        public static ResponseCallTool Error(string message)
        {
            return new ResponseCallTool { ResponseStatus = Status.Error, Message = message };
        }

        public static ResponseCallTool Processing(string message)
        {
            return new ResponseCallTool { ResponseStatus = Status.Processing, Message = message };
        }
    }

    /// <summary>
    /// Typed response from a Bridge tool call with structured data support.
    /// Used by tools that return typed results (e.g., TestRunner).
    /// </summary>
    public class ResponseCallValueTool<T> : ResponseCallTool
    {
        public T? Value { get; set; }
        public System.Text.Json.JsonElement? StructuredData { get; set; }

        public new ResponseCallValueTool<T> SetRequestID(string? requestId)
        {
            RequestID = requestId;
            return this;
        }

        public static new ResponseCallValueTool<T> Success(string message)
        {
            return new ResponseCallValueTool<T> { ResponseStatus = Status.Success, Message = message };
        }

        public static ResponseCallValueTool<T> SuccessStructured(System.Text.Json.JsonElement data)
        {
            return new ResponseCallValueTool<T>
            {
                ResponseStatus = Status.Success,
                StructuredData = data,
                Message = data.GetRawText()
            };
        }

        public static new ResponseCallValueTool<T> Error(string message)
        {
            return new ResponseCallValueTool<T> { ResponseStatus = Status.Error, Message = message };
        }

        public static ResponseCallValueTool<T> Processing()
        {
            return new ResponseCallValueTool<T> { ResponseStatus = Status.Processing, Message = "Processing..." };
        }
    }

    /// <summary>
    /// Data for deferred tool completion notification.
    /// Used by Script.UpdateOrCreate, Script.Delete, Package.Add, Package.Remove
    /// to report results after domain reload / package resolution.
    /// </summary>
    public class RequestToolCompletedData
    {
        public string? ToolId { get; set; }
        public string? RequestId { get; set; }
        public ResponseCallTool? Result { get; set; }
    }
}
