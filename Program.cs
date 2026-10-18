namespace Auto2Do;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\Auto2Do.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Auto2Do 已经在运行。请从系统托盘打开它。", "Auto2Do",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
#if NETFRAMEWORK
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
#else
            ApplicationConfiguration.Initialize();
#endif
            Application.Run(new MainForm(args.Contains("--minimized", StringComparer.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Auto2Do 无法启动。\n\n{ex.Message}", "Auto2Do - 错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
