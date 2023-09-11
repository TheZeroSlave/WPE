using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel.Design;

namespace Sniffer
{
    public partial class HexBox : Form
    {
        private Be.Windows.Forms.HexBox byteviewer;
        private Be.Windows.Forms.DynamicByteProvider dataProvider;
        Action<byte[]> onChangedContent;
        MessagePackage messagePackage;
        Form1 form;

        System.Timers.Timer timer;
        int totalToSend;
        int remainsToSend;

        bool creationMode;

        public HexBox(Form1 f, MessagePackage mp, Action<byte[]> onChanged, string packageName)
        {
            form = f;
            onChangedContent = onChanged;
            messagePackage = mp.Clone();

            InitializeComponent();

            // Initialize the ByteViewer.
            byteviewer = new Be.Windows.Forms.HexBox();
            byteviewer.Location = new Point(0, 0);
            byteviewer.Dock = DockStyle.Fill;
            byteviewer.VScrollBarVisible = true;

            toolStripStatusLabel3.Text = mp.socketHandle.ToString();

            dataProvider = new Be.Windows.Forms.DynamicByteProvider(mp.data);
            dataProvider.LengthChanged += Bp_LengthChanged;
            dataProvider.Changed += DataProvider_Changed;

            Bp_LengthChanged(this, null);

            byteviewer.ByteProvider = dataProvider;
            byteviewer.ColumnInfoVisible = true;
            byteviewer.LineInfoVisible = true;
            byteviewer.StringViewVisible = true;

            dataPanel.Controls.Add(byteviewer);

            if (packageName == "")
                textBox1.Text = form.sendingList.GenerateUniqName();
            else
                textBox1.Text = packageName;
        }

        private void DataProvider_Changed(object sender, EventArgs e)
        {
            button3.Enabled = true;
            messagePackage.data = dataProvider.Bytes.ToArray();
        }

        private void Bp_LengthChanged(object sender, EventArgs e)
        {
            lengthStatusLabel.Text = dataProvider.Length + " bytes";
        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            onChangedContent(dataProvider.Bytes.ToArray());
            button3.Enabled = false;
        }

        bool startedSend;

        private void button1_Click(object sender, EventArgs e)
        {
            if (!startedSend)
            {
                remainsToSend = Decimal.ToInt32(numericUpDown1.Value);
                totalToSend = remainsToSend;
                
                timer = new System.Timers.Timer();
                timer.Interval = Decimal.ToDouble(numericUpDown2.Value);
                timer.AutoReset = true;
                timer.Elapsed += Timer_Elapsed;

                numericUpDown1.Enabled = false;
                numericUpDown2.Enabled = false;


                button1.Text = "Stop";
                sendStatusLabel.Visible = false;

                startedSend = true;
                timer.Start();
            }
            else
            {
                stopSending();
            }
        }

        void stopSending()
        {
            startedSend = false;
            button1.Text = "Send";
            timer.Stop();

            numericUpDown1.Enabled = true;
            numericUpDown2.Enabled = true;

        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            sendStatusLabel.Invoke((MethodInvoker)(() =>
            {
                if (remainsToSend == 0)
                {
                    stopSending();
                    return;
                }

                try
                {
                    form.SendPackage(messagePackage);
                    sendStatusLabel.ForeColor = Color.Black;
                    sendStatusLabel.Text = "sended: " + (totalToSend - remainsToSend + 1).ToString() + " of " + totalToSend.ToString();
                    sendStatusLabel.Visible = true;

                    remainsToSend--;
                }
                catch (Exception ex)
                {
                    stopSending();
                    sendStatusLabel.Visible = true;
                    sendStatusLabel.Text = ex.Message;
                    sendStatusLabel.ForeColor = Color.Red;
                    return;
                }
            }));
        }

        public void SetCreationMode(bool enable)
        {
            groupBox1.Visible = !enable;
            button3.Visible = !enable;

            creationMode = enable;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            form.sendingList.AddLast(textBox1.Text, messagePackage);

            button2.Enabled = false;

            if (creationMode)
            {
                Close();
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (form.sendingList.HasName(textBox1.Text))
            {
                button2.Enabled = false;
                sendingListStatusLabel.Text = "already exist";
            }
            else
            {
                button2.Enabled = true;
                sendingListStatusLabel.Text = "";
            }
        }
    }
}
