using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ArmaExtension;

public static partial class Extension {

    // --- Read-only context values that never change
    public static string? MissionNameStatic { get; private set; }
    public static string? ServerNameStatic { get; private set; }

    public static ExtensionCallContext? ExtensionContext { get; internal set; }

    public sealed record ExtensionCallContext {
        public string MethodName { get; set; } = string.Empty;
        public int? AsyncKey { get; set; }
        public ulong SteamId { get; init; }
        public string FileSource { get; init; } = string.Empty;
        public string? MissionName { get; init; } = MissionNameStatic;
        public string? ServerName { get; init; } = ServerNameStatic;
        public short RemoteExecutedOwner { get; init; }
        public List<StackTraceLine> StackTrace { get; init; } = new();

        public sealed record StackTraceLine {
            public uint LineNumber { get; init; }
            public uint FileOffset { get; init; }
            public string SourceFile { get; init; } = string.Empty;
            public string ScopeName { get; init; } = string.Empty;
            public string FileContent { get; init; } = string.Empty;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "RVExtensionContext")]
    private static unsafe void RVExtensionContext(IntPtr* args, uint argsCnt) {
        string GetArg(int index) => index < argsCnt && args[index] != IntPtr.Zero
            ? Marshal.PtrToStringAnsi(args[index]) ?? string.Empty
            : string.Empty;

        int i = 0;
        _ = GetArg(i++); // CaptionText (ignored)

        ulong steamId = ulong.TryParse(GetArg(i++), out var sid) ? sid : 0;
        string fileSource = GetArg(i++);
        string missionName = GetArg(i++);
        string serverName = GetArg(i++);
        short remoteOwner = short.TryParse(GetArg(i++), out var owner) ? owner : (short)0;

        // Assign static values once if not already assigned
        if (missionName != null) MissionNameStatic = missionName;
        if (serverName != null) ServerNameStatic = serverName;

        var context = new ExtensionCallContext {
            SteamId = steamId,
            FileSource = fileSource,
            RemoteExecutedOwner = remoteOwner
            // MissionName and ServerName automatically use the static readonly values
        };

        // Stack trace parsing
        if (i < argsCnt && int.TryParse(GetArg(i++), out int stackCount)) {
            for (int s = 0; s < stackCount; s++) {
                if (i + 4 >= argsCnt) break;

                uint lineNumber = uint.TryParse(GetArg(i++), out var ln) ? ln : 0;
                uint fileOffset = uint.TryParse(GetArg(i++), out var fo) ? fo : 0;
                string sourceFile = GetArg(i++);
                string scopeName = GetArg(i++);
                string fileContent = GetArg(i++);

                context.StackTrace.Add(new ExtensionCallContext.StackTraceLine {
                    LineNumber = lineNumber,
                    FileOffset = fileOffset,
                    SourceFile = sourceFile,
                    ScopeName = scopeName,
                    FileContent = fileContent
                });
            }
        }

        ExtensionContext = context;
    }
}