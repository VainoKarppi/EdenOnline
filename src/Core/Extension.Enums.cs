namespace ArmaExtension;

public static partial class Extension {
    public enum ReturnCodes {
        Success = 0,
        Error = 1,
        InvalidMethod = 2,
        InvalidParameters = 3
    }

    public enum ResultCodes {
        SUCCESS,
        SUCCESS_VOID,
        ERROR,
        ASYNC_RESPONSE,
        ASYNC_SENT,
        ASYNC_SENT_VOID,
        ASYNC_FAILED,
        ASYNC_CANCEL,
        ASYNC_CANCEL_SUCCESS,
        ASYNC_CANCEL_FAILED,
        ASYNC_SUCCESS,
        CALLFUNCTION
    }
}