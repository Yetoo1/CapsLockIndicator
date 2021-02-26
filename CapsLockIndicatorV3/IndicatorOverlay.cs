/*
 * Created 09.07.2017 20:22
 * 
 * Copyright (c) Jonas Kohl <https://jonaskohl.de/>
 */
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CapsLockIndicatorV3
{
    /// <summary>
    /// A form that indicates if a keystate has changed.
    /// </summary>
    public partial class IndicatorOverlay : Form
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern int SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLong")]
        static extern int GetWindowLongInt(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll")]
        static extern int GetDpiForWindow(IntPtr hWnd);

        static float GetDisplayScaleFactor(IntPtr windowHandle)
        {
            try
            {
                return GetDpiForWindow(windowHandle) / 96f;
            }
            catch
            {
                return 1;
            }
        }

        const int GWL_EXSTYLE = -20;
        const uint WS_EX_LAYERED = 0x80000;
        const uint LWA_ALPHA = 0x2;
        const uint LWA_COLORKEY = 0x1;
        const uint WS_EX_TRANSPARENT = 0x00000020;
        const int WM_NCHITTEST = 0x84;
        const int HTTRANSPARENT = -1;

        private Size originalSize;

        private IndicatorDisplayPosition pos = IndicatorDisplayPosition.BottomRight;

        const int WINDOW_MARGIN = 16;
        private double lastOpacity = 1;
        double opacity_timer_value = 2.0;

        Color BorderColour = Color.FromArgb(
            0xFF,
            0x34,
            0x4D,
            0xB4
        );
        int BorderSize = 4;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            fadeTimer.Stop();
            windowCloseTimer.Stop();
            positionUpdateTimer.Stop();
            base.OnFormClosing(e);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000008 | 0x80;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
                m.Result = (IntPtr)HTTRANSPARENT;
            else
                base.WndProc(ref m);
        }

        protected override void OnShown(EventArgs e)
        {
            UpdatePosition();

            base.OnShown(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
        }

        void UpdatePosition()
        {
            Rectangle workingArea = Screen.GetWorkingArea(Cursor.Position);

            var factor = GetDisplayScaleFactor(Handle);

            Size = new Size(
                (int)(originalSize.Width * factor),
                (int)(originalSize.Height * factor)
            );

            switch (pos)
            {
                case IndicatorDisplayPosition.TopLeft:
                    Left = workingArea.X + WINDOW_MARGIN;
                    Top = workingArea.Y + WINDOW_MARGIN;
                    break;
                case IndicatorDisplayPosition.TopCenter:
                    Left = workingArea.X + (workingArea.Width / 2 - Width / 2);
                    Top = workingArea.Y + WINDOW_MARGIN;
                    break;
                case IndicatorDisplayPosition.TopRight:
                    Left = workingArea.X + workingArea.Left + (workingArea.Width - Width - WINDOW_MARGIN - workingArea.Left);
                    Top = workingArea.Y + WINDOW_MARGIN;
                    break;
                case IndicatorDisplayPosition.MiddleLeft:
                    Left = workingArea.X + WINDOW_MARGIN;
                    Top = workingArea.Y + (workingArea.Height / 2 - Height / 2);
                    break;
                case IndicatorDisplayPosition.MiddleCenter:
                    Left = workingArea.X + (workingArea.Width / 2 - Width / 2);
                    Top = workingArea.Y + (workingArea.Height / 2 - Height / 2);
                    break;
                case IndicatorDisplayPosition.MiddleRight:
                    Left = workingArea.X + workingArea.Left + (workingArea.Width - Width - WINDOW_MARGIN - workingArea.Left);
                    Top = workingArea.Y + (workingArea.Height / 2 - Height / 2);
                    break;
                case IndicatorDisplayPosition.BottomLeft:
                    Left = workingArea.X + WINDOW_MARGIN;
                    Top = workingArea.Y + workingArea.Top + (workingArea.Height - Height - WINDOW_MARGIN - workingArea.Top);
                    break;
                case IndicatorDisplayPosition.BottomCenter:
                    Left = workingArea.X + (workingArea.Width / 2 - Width / 2);
                    Top = workingArea.Y + workingArea.Top + (workingArea.Height - Height - WINDOW_MARGIN - workingArea.Top);
                    break;
                case IndicatorDisplayPosition.BottomRight:
                    Left = workingArea.X + workingArea.Left + (workingArea.Width - Width - WINDOW_MARGIN - workingArea.Left);
                    Top = workingArea.Y + workingArea.Top + (workingArea.Height - Height - WINDOW_MARGIN - workingArea.Top);
                    break;
                default:
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (BorderSize > 0)
                e.Graphics.DrawRectangle(new Pen(BorderColour, BorderSize), e.ClipRectangle);

            using (var sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(contentLabel.ForeColor))
                e.Graphics.DrawString(contentLabel.Text, contentLabel.Font, b, ClientRectangle, sf);
        }

        private void ClickThroughWindow(double opacity = 1d)
        {
            if (IsDisposed)
                return;
            //Opacity = opacity;
            try
            {
                IntPtr Handle = this.Handle;
                uint windowLong = GetWindowLong(Handle, GWL_EXSTYLE);
                SetWindowLong32(Handle, GWL_EXSTYLE, windowLong ^ WS_EX_LAYERED);
                SetLayeredWindowAttributes(Handle, 0, (byte)(opacity * 255), LWA_ALPHA);

                var style = GetWindowLong(this.Handle, GWL_EXSTYLE);
                SetWindowLong(this.Handle, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }
            catch (ObjectDisposedException)
            { }
        }

        public IndicatorOverlay(string content)
        {
            InitializeComponent();

            originalSize = Size;

            contentLabel.Text = content;

            ClickThroughWindow();
        }

        public IndicatorOverlay(string content, int timeoutInMs, IndicatorDisplayPosition position)
        {
            pos = position;
            InitializeComponent();

            contentLabel.Text = content;
            Application.DoEvents();
            originalSize = Size;

            if (timeoutInMs < 1)
            {
                windowCloseTimer.Enabled = false;
                fadeTimer.Enabled = false;
            }
            else
            {
                windowCloseTimer.Interval = timeoutInMs;
                fadeTimer.Interval = (int)Math.Floor((decimal)(timeoutInMs / 20));
            }
            ClickThroughWindow();
        }

        public IndicatorOverlay(string content, int timeoutInMs, Color bgColour, Color fgColour, Color bdColour, int bdSize, Font font, IndicatorDisplayPosition position, int indOpacity, bool alwaysShow)
        {
            pos = position;
            InitializeComponent();

            contentLabel.Text = content;
            Font = font;
            Application.DoEvents();
            originalSize = Size;

            var op = indOpacity / 100d;
            lastOpacity = op;
            SetOpacity(op);
            if (timeoutInMs < 0 || alwaysShow)
            {
                windowCloseTimer.Enabled = false;
                fadeTimer.Enabled = false;
            }
            else
            {
                windowCloseTimer.Interval = timeoutInMs;
                fadeTimer.Interval = (int)Math.Floor((decimal)(timeoutInMs / 20));
            }
            BackColor = bgColour;
            ForeColor = fgColour;
            BorderColour = bdColour;
            BorderSize = bdSize;
            ClickThroughWindow(op);
        }

        private void SetOpacity(double op)
        {
            if (IsDisposed)
                return;

            try
            {
                //Opacity = op;
                byte opb = (byte)Math.Min(255, op * 0xFF);
                SetLayeredWindowAttributes(Handle, 0, opb, LWA_ALPHA);
            }
            catch (ObjectDisposedException)
            { }
        }

        public void UpdateIndicator(string content, IndicatorDisplayPosition position)
        {
            pos = position;
            Opacity = 1;
            contentLabel.Text = content;
            opacity_timer_value = 2.0;
            windowCloseTimer.Stop();
            windowCloseTimer.Start();
            fadeTimer.Stop();
            fadeTimer.Start();
            UpdatePosition();
        }

        public void UpdateIndicator(string content, int timeoutInMs, IndicatorDisplayPosition position)
        {
            pos = position;
            Opacity = 1;
            contentLabel.Text = content;
            opacity_timer_value = 2.0;
            if (timeoutInMs < 0)
            {
                windowCloseTimer.Enabled = false;
                fadeTimer.Enabled = false;
            }
            else
            {
                windowCloseTimer.Stop();
                windowCloseTimer.Interval = timeoutInMs;
                windowCloseTimer.Start();
                fadeTimer.Stop();
                fadeTimer.Start();
            }
            UpdatePosition();
        }

        public void UpdateIndicator(string content, int timeoutInMs, Color bgColour, Color fgColour, Color bdColour, int bdSize, Font font, IndicatorDisplayPosition position, int indOpacity, bool alwaysShow)
        {
            pos = position;
            var op = indOpacity / 100d;
            lastOpacity = op;
            contentLabel.Text = content;
            Font = font;
            opacity_timer_value = 2.0;
            if (timeoutInMs < 0 || alwaysShow)
            {
                windowCloseTimer.Enabled = false;
                fadeTimer.Enabled = false;
            }
            else
            {
                windowCloseTimer.Stop();
                windowCloseTimer.Interval = timeoutInMs;
                windowCloseTimer.Start();
                fadeTimer.Stop();
                fadeTimer.Start();
            }
            SetOpacity(op);
            BackColor = bgColour;
            ForeColor = fgColour;
            BorderColour = bdColour;
            BorderSize = bdSize;
            Invalidate();
            UpdatePosition();
        }

        void WindowCloseTimerTick(object sender, EventArgs e)
        {
            Close();
        }

        private void fadeTimer_Tick(object sender, EventArgs e)
        {
            opacity_timer_value -= 0.1;
            if (opacity_timer_value <= 1.0)
                SetOpacity(opacity_timer_value * lastOpacity);
        }

        private void positionUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdatePosition();
        }
    }
}
