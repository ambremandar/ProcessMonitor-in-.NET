using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProcessMonitor
{
    public partial class NotificationBox : Form
    {
        private NotificationBox newMessageBox;
        private Timer msgTimer;
        private int disposeFormTimer; 

        public NotificationBox()
        {
            InitializeComponent();
        }

        public void Show(string txtMessage)
        {
            newMessageBox = new NotificationBox {lblMessage = {Text = txtMessage}};
            newMessageBox.ShowDialog(this);
        }

        public void Show(string txtMessage, string txtTitle)
        {
            newMessageBox = new NotificationBox {lblTitle = {Text = txtTitle}, lblMessage = {Text = txtMessage}};
            newMessageBox.ShowDialog(this);
        } 

        public void Show(string txtMessage, string txtTitle,int Timeout,bool showMessageBoxButtons)
        {
            newMessageBox = new NotificationBox {lblTitle = {Text = txtTitle}, lblMessage = {Text = txtMessage}};

            if (!showMessageBoxButtons)
            {
                newMessageBox.btnOK.Visible = false;
            }

            disposeFormTimer = Timeout;
            msgTimer = new Timer {Interval = 1000, Enabled = true};
            msgTimer.Start();
            msgTimer.Tick += timer_tick;

            newMessageBox.ShowDialog(this);
        }

        private void NotificationBox_Load(object sender, EventArgs e)
        {
            msgTimer = new Timer { Interval = 1000, Enabled = true };
            newMessageBox = new NotificationBox();
        }

        private void NotificationBox_Paint(object sender, PaintEventArgs e)
        {
            var mGraphics = e.Graphics;
            var pen1 = new Pen(Color.FromArgb(96, 155, 173), 1);
            var Area1 = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            var LGB = new LinearGradientBrush(Area1, Color.FromArgb(96, 155, 173), Color.FromArgb(245, 251, 251), LinearGradientMode.Vertical);
            mGraphics.FillRectangle(LGB, Area1);
            mGraphics.DrawRectangle(pen1, Area1);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (newMessageBox != null)
            {
                if (newMessageBox.msgTimer != null)
                {
                    newMessageBox.msgTimer.Stop();
                    newMessageBox.msgTimer.Dispose();
                }
                newMessageBox.Dispose();
                this.Close();
            }
        }

        private void timer_tick(object sender, EventArgs e)
        {
            disposeFormTimer--;

            if (disposeFormTimer >= 0)
            {
                newMessageBox.lblTimer.Text = string.Format("Restarting in: {0} sec",disposeFormTimer.ToString());
            }
            else
            {
                newMessageBox.msgTimer.Stop();
                newMessageBox.msgTimer.Dispose();
                newMessageBox.Dispose();
            }
        } 
    }
}