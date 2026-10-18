using System.Diagnostics;
using System.Drawing;

namespace Auto2Do;

internal sealed class AboutForm : Form
{
    public AboutForm(bool isAmoled)
    {
        Text = "关于 Auto2Do";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(390, 190);
        Font = new Font("Microsoft YaHei UI", 9F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(22)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = "Auto2Do",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            Dock = DockStyle.Fill
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = AboutInfo.Attribution,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = AboutInfo.Model,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 2);

        var repositoryLink = new LinkLabel
        {
            Text = AboutInfo.RepositoryUrl,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        repositoryLink.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(AboutInfo.RepositoryUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"无法打开项目链接。\n\n{ex.Message}", "Auto2Do",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        layout.Controls.Add(repositoryLink, 0, 3);

        var close = new Button
        {
            Text = "关闭",
            AutoSize = true,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            DialogResult = DialogResult.OK
        };
        layout.Controls.Add(close, 0, 4);
        Controls.Add(layout);
        AcceptButton = close;
        UiTheme.Apply(this, isAmoled);
    }
}
