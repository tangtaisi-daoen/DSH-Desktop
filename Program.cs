using System.Runtime.InteropServices;

namespace DSHDesktop;

internal static class Program
{
    /// <summary>自定义消息：唤醒已有实例的窗口（单实例）。</summary>
    public static readonly uint WmDshShow = RegisterWindowMessage("DSHDesktop_ShowWindow");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [STAThread]
    private static void Main()
    {
        // 单实例：第二个实例启动时激活已有窗口后退出
        using var mutex = new Mutex(true, "DSHDesktop_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            IntPtr h = FindWindow(null, "DeepSeek Harness");
            if (h != IntPtr.Zero) PostMessage(h, WmDshShow, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
