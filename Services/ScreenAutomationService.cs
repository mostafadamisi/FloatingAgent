using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FloatingAgent.Services;

public class ScreenAutomationService
{
    public Window? OwnerWindow { get; set; }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private double _savedLeft, _savedTop, _savedOpacity;
    private bool _isHidden;

    public void HideFromScreen()
    {
        if (OwnerWindow == null || _isHidden) return;
        OwnerWindow.Dispatcher.Invoke(() =>
        {
            _savedLeft = OwnerWindow.Left;
            _savedTop = OwnerWindow.Top;
            _savedOpacity = OwnerWindow.Opacity;
            OwnerWindow.Opacity = 0;
            OwnerWindow.Left = -32000;
            OwnerWindow.Top = -32000;
            _isHidden = true;
        });
    }

    public void ShowOnScreen()
    {
        if (OwnerWindow == null || !_isHidden) return;
        OwnerWindow.Dispatcher.Invoke(() =>
        {
            OwnerWindow.Left = _savedLeft;
            OwnerWindow.Top = _savedTop;
            OwnerWindow.Opacity = _savedOpacity;
            OwnerWindow.Topmost = true;
            _isHidden = false;
        });
    }

    public void FadeOut()
    {
        if (OwnerWindow == null) return;
        OwnerWindow.Dispatcher.Invoke(() =>
        {
            var fade = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = OwnerWindow.Opacity, To = 0,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            OwnerWindow.BeginAnimation(UIElement.OpacityProperty, fade);
        });
    }

    public void FadeIn()
    {
        if (OwnerWindow == null) return;
        OwnerWindow.Dispatcher.Invoke(() =>
        {
            var fade = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0, To = 1,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            OwnerWindow.BeginAnimation(UIElement.OpacityProperty, fade);
        });
    }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    public string CaptureScreenAsBase64()
    {
        HideFromScreen();
        try
        {
            var width = GetSystemMetrics(SM_CXSCREEN);
            var height = GetSystemMetrics(SM_CYSCREEN);
            using var bitmap = new Bitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(width, height));
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
        finally { ShowOnScreen(); }
    }

    public void MoveMouse(int x, int y)
    {
        SetCursorPos(x, y);
    }

    public void LeftClick(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    public void RightClick(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
    }

    public void DoubleClick(int x, int y)
    {
        LeftClick(x, y);
        Thread.Sleep(100);
        LeftClick(x, y);
    }

    public void TypeText(string text)
    {
        foreach (char c in text)
        {
            short vk = VkKeyScan(c);
            if (vk == -1) continue;
            byte vkCode = (byte)(vk & 0xFF);
            byte shiftState = (byte)((vk >> 8) & 0xFF);

            if ((shiftState & 1) != 0)
                keybd_event(0x10, 0, 0, UIntPtr.Zero);

            keybd_event(vkCode, 0, 0, UIntPtr.Zero);
            Thread.Sleep(10);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            if ((shiftState & 1) != 0)
                keybd_event(0x10, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            Thread.Sleep(20);
        }
    }

    public void PressKey(byte vkCode)
    {
        keybd_event(vkCode, 0, 0, UIntPtr.Zero);
        Thread.Sleep(30);
        keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static readonly Dictionary<string, byte> ModifierKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CTRL"] = 0x11, ["CONTROL"] = 0x11,
        ["ALT"] = 0x12,
        ["SHIFT"] = 0x10,
        ["WIN"] = 0x5B, ["WINDOWS"] = 0x5B
    };

    private static readonly Dictionary<string, byte> KeyCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = 0x41, ["B"] = 0x42, ["C"] = 0x43, ["D"] = 0x44,
        ["E"] = 0x45, ["F"] = 0x46, ["G"] = 0x47, ["H"] = 0x48,
        ["I"] = 0x49, ["J"] = 0x4A, ["K"] = 0x4B, ["L"] = 0x4C,
        ["M"] = 0x4D, ["N"] = 0x4E, ["O"] = 0x4F, ["P"] = 0x50,
        ["Q"] = 0x51, ["R"] = 0x52, ["S"] = 0x53, ["T"] = 0x54,
        ["U"] = 0x55, ["V"] = 0x56, ["W"] = 0x57, ["X"] = 0x58,
        ["Y"] = 0x59, ["Z"] = 0x5A,
        ["0"] = 0x30, ["1"] = 0x31, ["2"] = 0x32, ["3"] = 0x33,
        ["4"] = 0x34, ["5"] = 0x35, ["6"] = 0x36, ["7"] = 0x37,
        ["8"] = 0x38, ["9"] = 0x39,
        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73,
        ["F5"] = 0x74, ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77,
        ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,
        ["ENTER"] = 0x0D, ["RETURN"] = 0x0D,
        ["TAB"] = 0x09, ["ESC"] = 0x1B, ["ESCAPE"] = 0x1B,
        ["SPACE"] = 0x20,
        ["UP"] = 0x26, ["DOWN"] = 0x28, ["LEFT"] = 0x25, ["RIGHT"] = 0x27,
        ["DELETE"] = 0x2E, ["DEL"] = 0x2E,
        ["BACK"] = 0x08, ["BACKSPACE"] = 0x08,
        ["HOME"] = 0x24, ["END"] = 0x23,
        ["PAGEUP"] = 0x21, ["PAGEDOWN"] = 0x22,
        ["CAPSLOCK"] = 0x14, ["NUMLOCK"] = 0x90
    };

    public void PressHotKey(string keyCombination)
    {
        // Format: "CTRL+A" or "CTRL+SHIFT+ESC" or "ALT+F4"
        var parts = keyCombination.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return;

        var modifiers = new List<byte>();
        byte? mainKey = null;

        foreach (var part in parts)
        {
            if (ModifierKeys.TryGetValue(part, out var mod))
                modifiers.Add(mod);
            else if (KeyCodes.TryGetValue(part, out var key))
                mainKey = key;
        }

        if (mainKey == null) return;

        foreach (var mod in modifiers)
            keybd_event(mod, 0, 0, UIntPtr.Zero);
        Thread.Sleep(20);

        keybd_event(mainKey.Value, 0, 0, UIntPtr.Zero);
        Thread.Sleep(30);
        keybd_event(mainKey.Value, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        Thread.Sleep(20);

        foreach (var mod in modifiers.Reverse<byte>())
            keybd_event(mod, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public void PressEnter() => PressKey(0x0D);
    public void PressTab() => PressKey(0x09);

    public void LaunchApp(string appName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = appName,
            UseShellExecute = true
        };
        Process.Start(startInfo);
    }

    public void OpenUrl(string url)
    {
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            url = "https://" + url;
        var startInfo = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        Process.Start(startInfo);
    }

    public int ScreenWidth => GetSystemMetrics(SM_CXSCREEN);
    public int ScreenHeight => GetSystemMetrics(SM_CYSCREEN);
}
