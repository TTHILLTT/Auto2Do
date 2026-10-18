using System.Drawing;

namespace Auto2Do;

internal static class UiTheme
{
    private static readonly Color WindowBackground = Color.Black;
    private static readonly Color SurfaceBackground = Color.FromArgb(18, 18, 18);
    private static readonly Color ControlBackground = Color.FromArgb(28, 28, 28);
    private static readonly Color PrimaryText = Color.FromArgb(245, 245, 245);
    private static readonly Color SecondaryText = Color.FromArgb(176, 176, 176);
    private static readonly Color Border = Color.FromArgb(58, 58, 58);
    private static readonly Color Selection = Color.FromArgb(42, 42, 42);

    public static void Apply(Control root, bool amoled)
    {
        if (!amoled)
        {
            root.BackColor = SystemColors.Control;
            root.ForeColor = SystemColors.ControlText;
            ApplySystemColors(root);
            return;
        }

        ApplyDarkColors(root);
    }

    private static void ApplySystemColors(Control control)
    {
        foreach (Control child in control.Controls)
        {
            child.BackColor = child switch
            {
                DataGridView => SystemColors.Window,
                TextBox or ComboBox or DateTimePicker => SystemColors.Window,
                _ => SystemColors.Control
            };
            child.ForeColor = child is Label { Name: "ThemeWarning" }
                ? Color.Firebrick
                : child is Label { Name: "ThemeDescription" }
                    ? Color.DimGray
                    : SystemColors.ControlText;
            if (child is Button button)
            {
                button.FlatStyle = FlatStyle.Standard;
            }

            if (child is ToolStrip toolStrip)
            {
                toolStrip.BackColor = SystemColors.Control;
                toolStrip.ForeColor = SystemColors.ControlText;
                SetToolStripColors(toolStrip.Items, SystemColors.Control, SystemColors.ControlText);
            }

            if (child is DataGridView grid)
            {
                grid.BackgroundColor = SystemColors.Window;
                grid.GridColor = SystemColors.ControlDark;
                grid.DefaultCellStyle.BackColor = SystemColors.Window;
                grid.DefaultCellStyle.ForeColor = SystemColors.WindowText;
                grid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
                grid.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
                grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
                grid.RowHeadersDefaultCellStyle.BackColor = SystemColors.Control;
                grid.RowHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
                grid.EnableHeadersVisualStyles = true;
            }
            else if (child is StatusStrip statusStrip)
            {
                foreach (ToolStripItem item in statusStrip.Items)
                {
                    item.BackColor = SystemColors.Control;
                    item.ForeColor = SystemColors.ControlText;
                }
            }
            else
            {
                ApplySystemColors(child);
            }
        }
    }

    private static void ApplyDarkColors(Control control)
    {
        control.BackColor = WindowBackground;
        control.ForeColor = PrimaryText;

        if (control is DataGridView grid)
        {
            grid.BackgroundColor = WindowBackground;
            grid.GridColor = Border;
            grid.EnableHeadersVisualStyles = false;
            grid.DefaultCellStyle.BackColor = SurfaceBackground;
            grid.DefaultCellStyle.ForeColor = PrimaryText;
            grid.DefaultCellStyle.SelectionBackColor = Selection;
            grid.DefaultCellStyle.SelectionForeColor = PrimaryText;
            grid.ColumnHeadersDefaultCellStyle.BackColor = ControlBackground;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = PrimaryText;
            grid.RowHeadersDefaultCellStyle.BackColor = ControlBackground;
            grid.RowHeadersDefaultCellStyle.ForeColor = PrimaryText;
            return;
        }

        if (control is TableLayoutPanel or FlowLayoutPanel)
        {
            control.BackColor = WindowBackground;
        }
        else if (control is TextBox or ComboBox or DateTimePicker)
        {
            control.BackColor = SurfaceBackground;
        }
        else if (control is Button button)
        {
            button.BackColor = ControlBackground;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Border;
        }
        else if (control is CheckBox)
        {
            control.BackColor = WindowBackground;
        }
        else if (control is StatusStrip)
        {
            control.BackColor = ControlBackground;
            foreach (ToolStripItem item in ((StatusStrip)control).Items)
            {
                item.BackColor = ControlBackground;
                item.ForeColor = PrimaryText;
            }
        }

        if (control is ToolStrip toolStrip)
        {
            toolStrip.BackColor = ControlBackground;
            toolStrip.ForeColor = PrimaryText;
            SetToolStripColors(toolStrip.Items, ControlBackground, PrimaryText);
        }

        if (control is Label { Name: "ThemeWarning" })
        {
            control.ForeColor = Color.FromArgb(255, 125, 125);
        }
        else if (control is Label { Name: "ThemeDescription" })
        {
            control.ForeColor = SecondaryText;
        }

        foreach (Control child in control.Controls)
        {
            ApplyDarkColors(child);
        }
    }

    private static void SetToolStripColors(
        ToolStripItemCollection items,
        Color background,
        Color foreground)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = background;
            item.ForeColor = foreground;
            if (item is ToolStripDropDownItem dropDownItem)
            {
                SetToolStripColors(dropDownItem.DropDownItems, background, foreground);
            }
        }
    }
}
