namespace ArmaExtension;

internal static partial class Enums {
    internal enum ReturnCodes {
        Success = 0,
        Error = 1,
        InvalidMethod = 2,
        InvalidParameters = 3
    }

    internal enum ExtensionResultCode {
        SUCCESS,
        SUCCESS_VOID,
        ERROR,
        ASYNC_RESPONSE,
        ASYNC_SENT,
        ASYNC_SENT_VOID,
        ASYNC_SENT_FAILED,
        ASYNC_CANCEL,
        ASYNC_CANCEL_SUCCESS,
        ASYNC_CANCEL_FAILED,
        ASYNC_SUCCESS,
        ASYNC_STATUS,
        ASYNC_STATUS_NOT_FOUND,
        ASYNC_STATUS_RUNNING,
        ASYNC_STATUS_COMPLETED,
        ASYNC_STATUS_FAULTED,
        ASYNC_STATUS_CANCELLED,
        CALLFUNCTION
    }
}