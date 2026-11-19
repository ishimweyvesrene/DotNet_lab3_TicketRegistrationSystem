using System.Text.RegularExpressions;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

/// <summary>Small helpers so every screen is built the same way. Views contain no SQL and no business rules.</summary>
public static class Ui
{
    public const string AppName = "Ticket Registration System";

    public static void Info(string m) => MessageBox.Show(m, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    public static void Warn(string m) => MessageBox.Show(m, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    public static bool Confirm(string m) => MessageBox.Show(m, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    /// <summary>Shows the error of a failed result (or an optional success message) and returns whether it worked.</summary>
    public static bool Report(Result r, string? success = null)
    {
        if (!r.Ok) Warn(r.Error ?? "Something went wrong.");
        else if (success is not null) Info(success);
        return r.Ok;
    }

    public static string Rwf(decimal v) => $"RWF {v:N0}";
    public static string When(DateTime d) => d.ToString("ddd dd MMM yyyy HH:mm");

    public static Label Lbl(string text, bool bold = false) => new()
    {
        Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 6, 3),
        Font = bold ? new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold) : SystemFonts.MessageBoxFont
    };

    public static TextBox Txt(int width = 180, bool password = false) => new() { Width = width, UseSystemPasswordChar = password, Margin = new Padding(3) };

    public static Button Btn(string text, EventHandler click, int width = 120)
    {
        var b = new Button { Text = text, Width = width, Height = 30, Margin = new Padding(3) };
        b.Click += click;
        return b;
    }

    public static NumericUpDown Num(int min, int max, int value, int width = 70) =>
        new() { Minimum = min, Maximum = max, Value = value, Width = width, Margin = new Padding(3) };

    public static ComboBox Combo(int width = 180) =>
        new() { Width = width, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(3) };

    public static DateTimePicker Picker(bool withTime, System.DateTime? value = null, int width = 170) => new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = withTime ? "dd MMM yyyy HH:mm" : "dd MMM yyyy",
        Width = width, Value = value ?? System.DateTime.Now, Margin = new Padding(3)
    };

    /// <summary>Left-to-right row of controls that wraps; used for filter bars and button bars.</summary>
    public static FlowLayoutPanel Bar(DockStyle dock, params Control[] items)
    {
        var f = new FlowLayoutPanel { Dock = dock, AutoSize = true, WrapContents = true, Padding = new Padding(4) };
        f.Controls.AddRange(items);
        return f;
    }

    /// <summary>Label + field form, one row per pair.</summary>
    public static TableLayoutPanel Form(params (string Label, Control Field)[] rows)
    {
        var t = new TableLayoutPanel { ColumnCount = 2, RowCount = rows.Length, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < rows.Length; i++)
        {
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(Lbl(rows[i].Label), 0, i);
            t.Controls.Add(rows[i].Field, 1, i);
        }
        return t;
    }

    public static Form Dialog(string title, int width, int height) => new()
    {
        Text = title, ClientSize = new Size(width, height), StartPosition = FormStartPosition.CenterParent,
        FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false
    };

    public static FlowLayoutPanel ButtonBar(params Button[] buttons)
    {
        var f = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 6, 8, 6) };
        f.Controls.AddRange(buttons);
        return f;
    }

    public static DataGridView Grid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle
        };
        g.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewColumn c in g.Columns)
            {
                c.HeaderText = Regex.Replace(c.Name, "(?<=[a-z])(?=[A-Z])", " ");
                var t = Nullable.GetUnderlyingType(c.ValueType) ?? c.ValueType;
                if (t == typeof(decimal)) { c.DefaultCellStyle.Format = "N0"; c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; }
                else if (t == typeof(System.DateTime)) c.DefaultCellStyle.Format = "dd MMM yyyy HH:mm";
            }
        };
        return g;
    }

    public static void Bind<T>(DataGridView g, List<T> items)
    {
        g.DataSource = null;
        g.DataSource = items;
    }

    public static T? Selected<T>(this DataGridView g) where T : class => g.CurrentRow?.DataBoundItem as T;

    /// <summary>Returns the selected item of a combo or null.</summary>
    public static T? Pick<T>(this ComboBox c) where T : class => c.SelectedItem as T;
}
