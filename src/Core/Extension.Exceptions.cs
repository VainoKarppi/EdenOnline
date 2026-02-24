using System;

namespace ArmaExtension;


[Serializable]
public class ArmaException : Exception {
    public ArmaException () {}
    public ArmaException (string message) : base(message) {}
    public ArmaException (string message, Exception innerException) : base (message, innerException) {}    
}

[Serializable]
public class ArmaParseException : ArmaException {
    public ArmaParseException () {}
    public ArmaParseException (string message) : base(message) {}
    public ArmaParseException (string message, ArmaException innerException) : base (message, innerException) {}    
}

[Serializable]
public class ArmaAsyncException : ArmaException {
    public int TaskId { get; }
    public ArmaAsyncException() {}
    public ArmaAsyncException(int taskId, string message) : base($"Task {taskId} Failed: {message}") { TaskId = taskId; }
    public ArmaAsyncException(int taskId, string message, Exception innerException) : base($"Task {taskId} Failed: {message}", innerException) {
        TaskId = taskId;
    }
}