using System.Drawing;
using System.Windows.Forms;

namespace PAYROLL.UI;

internal static class OwnerPasscodeDialog
{
    public static bool Configure(
        IWin32Window owner, string employeeName, string currentUser, Func<string, bool> savePasscode)
    {
        using var dialog = new Form
        {
            Text = "Set owner passcode",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(470, 256),
            BackColor = Color.White,
            Font = Theme.Body
        };

        var heading = new Label
        {
            Text = "Set the database owner passcode",
            Font = Theme.H2,
            ForeColor = Theme.TextDark,
            AutoSize = true,
            Location = new Point(22, 16),
            BackColor = Color.White
        };
        var explanation = new Label
        {
            Text = $"One-time setup. Signed in as {currentUser}. This also approves promoting {employeeName} to Admin. The first admin to set it should be the owner.",
            Font = Theme.Small,
            ForeColor = Theme.TextGray,
            AutoEllipsis = true,
            Size = new Size(426, 42),
            Location = new Point(22, 45),
            BackColor = Color.White
        };
        var passcodeLabel = new Label
        {
            Text = "PASSCODE:",
            Font = Theme.BodyBold,
            ForeColor = Theme.TextDark,
            AutoSize = true,
            Location = new Point(22, 101),
            BackColor = Color.White
        };
        var passcodeBox = new TextBox
        {
            Location = new Point(132, 97),
            Size = new Size(296, 30),
            UseSystemPasswordChar = true,
            MaxLength = 256,
            Font = Theme.Body
        };
        var confirmLabel = new Label
        {
            Text = "CONFIRM:",
            Font = Theme.BodyBold,
            ForeColor = Theme.TextDark,
            AutoSize = true,
            Location = new Point(22, 137),
            BackColor = Color.White
        };
        var confirmBox = new TextBox
        {
            Location = new Point(132, 133),
            Size = new Size(296, 30),
            UseSystemPasswordChar = true,
            MaxLength = 256,
            Font = Theme.Body
        };
        var error = new Label
        {
            Text = "",
            Location = new Point(132, 166),
            Size = new Size(296, 34),
            ForeColor = Theme.RedText,
            Font = Theme.Small,
            BackColor = Color.White,
            Visible = false
        };
        var saveButton = CreateButton("Save and promote", new Point(213, 214), Theme.Accent, Color.White, Theme.BodyBold);
        var cancelButton = CreateButton("Cancel", new Point(338, 214), Color.FromArgb(238, 241, 245), Theme.TextDark, Theme.Body);
        cancelButton.DialogResult = DialogResult.Cancel;

        saveButton.Click += (_, _) =>
        {
            string passcode = passcodeBox.Text;
            if (passcode.Length < 12)
            {
                error.Text = "Use at least 12 characters.";
                error.Visible = true;
                passcodeBox.Focus();
                return;
            }
            if (!string.Equals(passcode, confirmBox.Text, StringComparison.Ordinal))
            {
                error.Text = "The passcodes do not match.";
                error.Visible = true;
                confirmBox.Clear();
                confirmBox.Focus();
                return;
            }

            try
            {
                if (!savePasscode(passcode))
                {
                    error.Text = "A passcode was already set. Cancel and verify it instead.";
                    error.Visible = true;
                    passcodeBox.Clear();
                    confirmBox.Clear();
                    passcodeBox.Focus();
                    return;
                }

                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
            catch (Exception ex)
            {
                error.Text = "Could not save the passcode: " + ex.Message;
                error.Visible = true;
            }
        };

        dialog.Controls.AddRange(new Control[]
        {
            heading, explanation, passcodeLabel, passcodeBox, confirmLabel, confirmBox,
            error, saveButton, cancelButton
        });
        dialog.AcceptButton = saveButton;
        dialog.CancelButton = cancelButton;
        dialog.Shown += (_, _) => passcodeBox.Focus();
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    public static bool Verify(IWin32Window owner, string employeeName, Func<string, bool> verifyPasscode)
    {
        using var dialog = new Form
        {
            Text = "Owner verification",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(430, 176),
            BackColor = Color.White,
            Font = Theme.Body
        };

        var heading = new Label
        {
            Text = "Confirm admin promotion",
            Font = Theme.H2,
            ForeColor = Theme.TextDark,
            AutoSize = true,
            Location = new Point(22, 15),
            BackColor = Color.White
        };
        var explanation = new Label
        {
            Text = $"Promoting {employeeName} to Admin. Owner passcode required.",
            Font = Theme.Small,
            ForeColor = Theme.TextGray,
            AutoEllipsis = true,
            Size = new Size(386, 22),
            Location = new Point(22, 43),
            BackColor = Color.White
        };
        var passcodeLabel = new Label
        {
            Text = "PASSCODE:",
            Font = Theme.BodyBold,
            ForeColor = Theme.TextDark,
            AutoSize = true,
            Location = new Point(22, 84),
            BackColor = Color.White
        };
        var passcodeBox = new TextBox
        {
            Location = new Point(112, 80),
            Size = new Size(296, 30),
            UseSystemPasswordChar = true,
            MaxLength = 256,
            Font = Theme.Body
        };
        var error = new Label
        {
            Text = "",
            Location = new Point(112, 111),
            Size = new Size(296, 20),
            ForeColor = Theme.RedText,
            Font = Theme.Small,
            BackColor = Color.White,
            Visible = false
        };
        var verifyButton = CreateButton("Verify", new Point(218, 136), Theme.Accent, Color.White, Theme.BodyBold);
        var cancelButton = CreateButton("Cancel", new Point(316, 136), Color.FromArgb(238, 241, 245), Theme.TextDark, Theme.Body);
        cancelButton.DialogResult = DialogResult.Cancel;

        int attemptsRemaining = 5;
        verifyButton.Click += (_, _) =>
        {
            if (verifyPasscode(passcodeBox.Text))
            {
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
                return;
            }

            attemptsRemaining--;
            passcodeBox.Clear();
            passcodeBox.Focus();
            error.Text = attemptsRemaining > 0
                ? $"Incorrect passcode. {attemptsRemaining} attempt(s) remaining."
                : "Too many failed attempts. Owner verification is temporarily locked.";
            error.Visible = true;
            if (attemptsRemaining == 0)
            {
                verifyButton.Enabled = false;
                cancelButton.Text = "Close";
            }
        };

        dialog.Controls.AddRange(new Control[] { heading, explanation, passcodeLabel, passcodeBox, error, verifyButton, cancelButton });
        dialog.AcceptButton = verifyButton;
        dialog.CancelButton = cancelButton;
        dialog.Shown += (_, _) => passcodeBox.Focus();
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    private static Button CreateButton(string text, Point location, Color background, Color foreground, Font font)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(112, 32),
            Location = location,
            BackColor = background,
            ForeColor = foreground,
            FlatStyle = FlatStyle.Flat,
            Font = font
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
