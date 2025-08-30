using GHelper.Mode;
using System.Drawing;
using System.Windows.Forms;

namespace GHelper.UI
{
    public class MiniOverlay : Form
    {
        private readonly Label _label;
        private Point _dragStart;

        public MiniOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = 0.6; // mostly transparent
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(10);

            _label = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Text = Modes.GetCurrentName()
            };

            Controls.Add(_label);

            // Restore last position
            int left = AppConfig.Get("mini_left", 50);
            int top = AppConfig.Get("mini_top", 50);
            Location = new Point(Math.Max(0, left), Math.Max(0, top));

            // Enable move by dragging anywhere
            MouseDown += MiniOverlay_MouseDown;
            MouseMove += MiniOverlay_MouseMove;
            _label.MouseDown += MiniOverlay_MouseDown;
            _label.MouseMove += MiniOverlay_MouseMove;

            // Right-click to close (toggle will reopen)
            ContextMenuStrip = new ContextMenuStrip();
            var closeItem = new ToolStripMenuItem("Hide Mini Overlay");
            closeItem.Click += (s, e) => { Hide(); AppConfig.Set("mini_overlay", 0); };
            ContextMenuStrip.Items.Add(closeItem);
        }

        private void MiniOverlay_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragStart = e.Location;
            }
        }

        private void MiniOverlay_MouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Left += e.X - _dragStart.X;
                Top += e.Y - _dragStart.Y;
                AppConfig.Set("mini_left", Left);
                AppConfig.Set("mini_top", Top);
            }
        }

        public void SetText(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => _label.Text = text));
            }
            else
            {
                _label.Text = text;
            }
        }
    }
}
