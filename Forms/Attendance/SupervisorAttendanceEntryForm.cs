using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PAYROLL.UI;

namespace PAYROLL;

// Used only from the Team Attendance screen. Employees do not get this form.
public sealed class SupervisorAttendanceEntryForm : Form
{
    private readonly int employeeId;
    private readonly Action attendanceChanged;
    private readonly DateTimePicker timeValue = new();
    private readonly Label status = new();
    private readonly ModernButton timeInButton = new();
    private readonly ModernButton breakOutButton = new();
    private readonly ModernButton breakInButton = new();
    private readonly ModernButton timeOutButton = new();

    public SupervisorAttendanceEntryForm(string employeeName, int employeeId, Action attendanceChanged)
    {
        this.employeeId = employeeId;
        this.attendanceChanged = attendanceChanged;

        Text = "Record Team Attendance";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(450, 350);
        BackColor = Theme.Bg;
        Font = Theme.Body;

        var title = new Label
        {
            Text = "Record attendance", Location = new Point(20, 18), AutoSize = true,
            Font = Theme.H2, ForeColor = Theme.TextDark, BackColor = Theme.Bg
        };
        var employee = new Label
        {
            Text = $"Employee: {employeeName}", Location = new Point(20, 53), Size = new Size(410, 22),
            Font = Theme.BodyBold, ForeColor = Theme.TextDark, BackColor = Theme.Bg, AutoEllipsis = true
        };
        var date = new Label
        {
            Text = $"Today: {DateTime.Today:dddd, MMMM d, yyyy}", Location = new Point(20, 77),
            Size = new Size(410, 22), Font = Theme.Small, ForeColor = Theme.TextGray, BackColor = Theme.Bg
        };
        status.Location = new Point(20, 106);
        status.Size = new Size(410, 46);
        status.Font = Theme.Small;
        status.ForeColor = Theme.TextGray;
        status.BackColor = Theme.Bg;

        var timeLabel = new Label
        {
            Text = "TIME TO RECORD", Location = new Point(20, 166), AutoSize = true,
            Font = Theme.SmallBold, ForeColor = Theme.TextGray, BackColor = Theme.Bg
        };
        timeValue.Location = new Point(20, 188);
        timeValue.Size = new Size(170, 28);
        timeValue.Format = DateTimePickerFormat.Time;
        timeValue.ShowUpDown = true;
        timeValue.Value = DateTime.Now;

        ConfigureButton(timeInButton, "Time In", 20);
        ConfigureButton(breakOutButton, "Break Out", 120);
        ConfigureButton(breakInButton, "Break In", 220);
        ConfigureButton(timeOutButton, "Time Out", 320);
        timeInButton.Click += (_, _) => Record("time-in", () => AttendanceService.RecordTimeIn(employeeId, timeValue.Value.TimeOfDay));
        breakOutButton.Click += (_, _) => Record("break-out", () => AttendanceService.RecordBreakOut(employeeId, timeValue.Value.TimeOfDay));
        breakInButton.Click += (_, _) => RecordBreakIn();
        timeOutButton.Click += (_, _) => RecordTimeOut();

        var hint = new Label
        {
            Text = "Follow the order: time-in, break-out, break-in, then time-out.",
            Location = new Point(20, 274), Size = new Size(410, 24),
            Font = Theme.Small, ForeColor = Theme.TextGray, BackColor = Theme.Bg
        };
        var close = new ModernButton
        {
            Text = "Close", Location = new Point(330, 304), Size = new Size(100, 34),
            SurroundColor = Theme.Bg, FillColor = Color.FromArgb(229, 231, 235)
        };
        close.ForeColor = Theme.TextDark;
        close.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            title, employee, date, status, timeLabel, timeValue,
            timeInButton, breakOutButton, breakInButton, timeOutButton, hint, close
        });
        RefreshState();
    }

    private static void ConfigureButton(ModernButton button, string text, int left)
    {
        button.Text = text;
        button.Location = new Point(left, 230);
        button.Size = new Size(90, 36);
        button.SurroundColor = Theme.Bg;
    }

    private void Record(string actionName, Action action)
    {
        try
        {
            action();
            attendanceChanged();
            RefreshState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not record {actionName}: {ex.Message}",
                "Team Attendance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RecordBreakIn()
    {
        int unfinished = AttendanceService.GetUnfinishedBreakMinutes(employeeId, timeValue.Value.TimeOfDay);
        if (unfinished > 0 && MessageBox.Show(this,
                $"The one-hour break is not finished yet ({unfinished} minute(s) left).\n\n" +
                "Record break-in anyway? The remaining break time is still unpaid.",
                "Break not finished", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        Record("break-in", () => AttendanceService.RecordBreakIn(employeeId, timeValue.Value.TimeOfDay));
    }

    private void RecordTimeOut()
    {
        int undertime = AttendanceService.GetUndertimeMinutes(employeeId, timeValue.Value.TimeOfDay);
        if (undertime > 0 && MessageBox.Show(this,
                $"This employee is {AttendanceService.FormatDuration(undertime)} short of the regular duty.\n\n" +
                "Record time-out and create an undertime follow-up notice?",
                "Undertime", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        Record("time-out", () => AttendanceService.RecordTimeOut(employeeId, timeValue.Value.TimeOfDay));
    }

    private void RefreshState()
    {
        AttendanceRecord? record = AttendanceService.ListForDate(DateTime.Today, employeeId)
            .FirstOrDefault(item => item.EmployeeId == employeeId);
        status.Text = record?.TimeIn == null
            ? "No time-in has been recorded today."
            : $"In: {Clock(record.TimeIn)}   Break: {Clock(record.BreakOut)} - {Clock(record.BreakIn)}   Out: {Clock(record.TimeOut)}";

        bool canTimeIn = record?.TimeIn == null;
        bool canBreakOut = record?.TimeIn != null && record.TimeOut == null && record.BreakOut == null;
        bool canBreakIn = record?.BreakOut != null && record.TimeOut == null && record.BreakIn == null;
        bool canTimeOut = record?.BreakIn != null && record.TimeOut == null;
        timeInButton.Enabled = canTimeIn;
        breakOutButton.Enabled = canBreakOut;
        breakInButton.Enabled = canBreakIn;
        timeOutButton.Enabled = canTimeOut;
    }

    private static string Clock(TimeSpan? time)
        => time.HasValue ? DateTime.Today.Add(time.Value).ToString("h:mm tt") : "not recorded";
}
