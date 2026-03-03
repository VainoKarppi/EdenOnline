using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using static ArmaExtension.Logger;

namespace ArmaExtension;

// TODO REEQUIRES TESTING AND VALIDATION (no idea if this works...)

public static unsafe partial class Extension {
    [UnmanagedCallersOnly(EntryPoint = "RVExtensionRequestUI", CallConvs = new[] { typeof(CallConvStdcall) })]
    public static bool RVExtensionRequestUI(sbyte* uiClass, void* interfaceStruct)
    {
        string className = Marshal.PtrToStringAnsi((IntPtr)uiClass) ?? string.Empty;

        try
        {
            UIEventStruct* data = (UIEventStruct*)interfaceStruct;

            switch (className)
            {
                // Mouse / Keyboard / Control Events
                case "LButtonDown": UIEvents.RaiseLButtonDown((nint)data->Sender, data->X, data->Y); break;
                case "LButtonUp": UIEvents.RaiseLButtonUp((nint)data->Sender, data->X, data->Y); break;
                case "LButtonClick": UIEvents.RaiseLButtonClick((nint)data->Sender, data->X, data->Y); break;
                case "LButtonDblClick": UIEvents.RaiseLButtonDblClick((nint)data->Sender, data->X, data->Y); break;

                case "RButtonDown": UIEvents.RaiseRButtonDown((nint)data->Sender, data->X, data->Y); break;
                case "RButtonUp": UIEvents.RaiseRButtonUp((nint)data->Sender, data->X, data->Y); break;
                case "RButtonClick": UIEvents.RaiseRButtonClick((nint)data->Sender, data->X, data->Y); break;

                case "MouseMove": UIEvents.RaiseMouseMove((nint)data->Sender, data->X, data->Y); break;
                case "MouseZChanged": return UIEvents.RaiseMouseZChanged((nint)data->Sender, data->Delta);

                case "MouseEnter": UIEvents.RaiseMouseEnter((nint)data->Sender, data->X, data->Y); break;
                case "MouseExit": UIEvents.RaiseMouseExit((nint)data->Sender, data->X, data->Y); break;

                case "KeyDown": return UIEvents.RaiseKeyDown((nint)data->Sender, data->Key);
                case "KeyUp": return UIEvents.RaiseKeyUp((nint)data->Sender, data->Key);
                case "Char": return UIEvents.RaiseChar((nint)data->Sender, data->CharCode, data->RepCount, data->Flags);

                default:
                    Error($"Unknown UI class: {className}");
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            Error($"RVExtensionRequestUI exception: {ex}");
            return false;
        }
    }

    // Helper to read args array
    private static string[] GetArgs(UIEventStruct* data)
    {
        if (data->ArgsPtr == IntPtr.Zero || data->ArgsCount <= 0) return [];

        var args = new string[data->ArgsCount];
        var ptrArray = (IntPtr*)data->ArgsPtr;

        for (int i = 0; i < data->ArgsCount; i++)
            args[i] = Marshal.PtrToStringAnsi(ptrArray[i]) ?? string.Empty;

        return args;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIEventStruct
    {
        public void* Sender;
        public float X;
        public float Y;
        public int Key;
        public uint CharCode;
        public uint RepCount;
        public uint Flags;
        public float Delta;

        public void* Context;
        public IntPtr MethodName;
        public int AsyncKey;
        public bool Success;

        public IntPtr ArgsPtr;
        public int ArgsCount;
    }
}

public static class UIEvents
{
    // Mouse / Keyboard / Control Events
    public static event Action<object?, float, float>? OnLButtonDown;
    public static event Action<object?, float, float>? OnLButtonUp;
    public static event Action<object?, float, float>? OnLButtonClick;
    public static event Action<object?, float, float>? OnLButtonDblClick;

    public static event Action<object?, float, float>? OnRButtonDown;
    public static event Action<object?, float, float>? OnRButtonUp;
    public static event Action<object?, float, float>? OnRButtonClick;
    public static event Action<object?, float, float>? OnMouseMove;
    public static event Func<object?, float, bool>? OnMouseZChanged;
    public static event Action<object?, float, float>? OnMouseEnter;
    public static event Action<object?, float, float>? OnMouseExit;

    public static event Func<object?, int, bool>? OnKeyDown;
    public static event Func<object?, int, bool>? OnKeyUp;
    public static event Func<object?, uint, uint, uint, bool>? OnChar;


    // Helper: Fire event asynchronously on thread pool
    private static void FireAndForget(this MulticastDelegate? eventDelegate, params object?[] args)
    {
        if (eventDelegate == null) return;

        foreach (var handler in eventDelegate.GetInvocationList())
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    handler.DynamicInvoke(args);
                }
                catch (TargetParameterCountException)
                {
                    // Skip handlers with wrong number of parameters
                }
                catch (Exception ex)
                {
                    Error($"UIEvents exception in handler: {ex}");
                }
            });
        }
    }

    // Trigger Methods
    public static void RaiseLButtonDown(object? obj, float x, float y) => OnLButtonDown.FireAndForget(obj, x, y);
    public static void RaiseLButtonUp(object? obj, float x, float y) => OnLButtonUp.FireAndForget(obj, x, y);
    public static void RaiseLButtonClick(object? obj, float x, float y) => OnLButtonClick.FireAndForget(obj, x, y);
    public static void RaiseLButtonDblClick(object? obj, float x, float y) => OnLButtonDblClick.FireAndForget(obj, x, y);

    public static void RaiseRButtonDown(object? obj, float x, float y) => OnRButtonDown.FireAndForget(obj, x, y);
    public static void RaiseRButtonUp(object? obj, float x, float y) => OnRButtonUp.FireAndForget(obj, x, y);
    public static void RaiseRButtonClick(object? obj, float x, float y) => OnRButtonClick.FireAndForget(obj, x, y);
    public static void RaiseMouseMove(object? obj, float x, float y) => OnMouseMove.FireAndForget(obj, x, y);
    public static bool RaiseMouseZChanged(object? obj, float dz)
    {
        if (OnMouseZChanged == null) return false;

        bool handled = false;
        foreach (var handler in OnMouseZChanged.GetInvocationList())
        {
            try { handled |= (bool)handler.DynamicInvoke(obj, dz)!; }
            catch { }
        }
        return handled;
    }

    public static void RaiseMouseEnter(object? obj, float x, float y) => OnMouseEnter.FireAndForget(obj, x, y);
    public static void RaiseMouseExit(object? obj, float x, float y) => OnMouseExit.FireAndForget(obj, x, y);

    public static bool RaiseKeyDown(object? obj, int key) => RaiseKeyEvent(OnKeyDown, obj, key);
    public static bool RaiseKeyUp(object? obj, int key) => RaiseKeyEvent(OnKeyUp, obj, key);
    public static bool RaiseChar(object? obj, uint charCode, uint repCnt, uint flags)
    {
        if (OnChar == null) return false;

        bool handled = false;
        foreach (var handler in OnChar.GetInvocationList())
        {
            try { handled |= (bool)handler.DynamicInvoke(obj, charCode, repCnt, flags)!; }
            catch { }
        }
        return handled;
    }

    private static bool RaiseKeyEvent(MulticastDelegate? del, object? obj, int key)
    {
        if (del == null) return false;
        bool handled = false;
        foreach (var handler in del.GetInvocationList())
        {
            try { handled |= (bool)handler.DynamicInvoke(obj, key)!; }
            catch { }
        }
        return handled;
    }

}