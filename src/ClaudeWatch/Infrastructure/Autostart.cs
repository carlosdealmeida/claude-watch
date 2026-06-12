using Microsoft.Win32;

namespace ClaudeWatch.Infrastructure;

public static class Autostart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "ClaudeWatch";

    public static bool IsEnabled()
    { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue(Name) is not null; }

    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) k.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(Name, throwOnMissingValue: false);
    }
}
