using System.Drawing;
using System.Windows.Forms;

namespace PAYROLL.UI
{
    // A small "LABEL" caption above a rounded, borderless text box.
    // Shared by LoginForm and RegisterForm so every field looks identical.
    public static class InputField
    {
        public static (Panel container, TextBox box) Create(string labelText, bool isPassword, Size size, Point location)
        {
            var container = new Panel { Location = location, Size = new Size(size.Width, 70), BackColor = Color.White };

            var label = new Label
            {
                Text = labelText,
                Font = Theme.SmallBold,
                ForeColor = Theme.TextGray,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.White
            };

            var box = new RoundedPanel
            {
                Location = new Point(0, 22),
                Size = new Size(size.Width, size.Height),
                CornerRadius = 10,
                SurroundColor = Color.White
            };

            var textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Theme.Body,
                ForeColor = Theme.TextDark,
                BackColor = Color.White,
                UseSystemPasswordChar = isPassword,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = size.Width - 28
            };
            textBox.Location = new Point(14, (size.Height - textBox.PreferredHeight) / 2);
            box.Controls.Add(textBox);

            container.Controls.Add(box);
            container.Controls.Add(label);
            return (container, textBox);
        }
    }
}