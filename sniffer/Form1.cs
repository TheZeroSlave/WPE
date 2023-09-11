using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Sniffer
{
    public partial class Form1 : Form
    {
        Process[] savedProcesses;
        Process injectedProcess;
        Dictionary<int, bool> is32bitProcess = new Dictionary<int, bool>(); // by pid

        List<MessagePackage> packages = new List<MessagePackage>();
        public SendingList sendingList;

        string dataToString(byte[] data)
        {
            StringBuilder strBuilder = new StringBuilder();
            const int maxToPrint = 48;
            for (int i = 0; i < data.Length; i++)
            {
                if (i == maxToPrint)
                    break;

                strBuilder.Append(data[i].ToString("X2"));

                if (i != data.Length - 1 && i != maxToPrint - 1)
                    strBuilder.Append(" ");
            }

            return strBuilder.ToString();
        }

        public void AddPackage(MessagePackage p)
        {
            packages.Add(p);

            ListViewItem viewItem = new ListViewItem();
            viewItem.Text = p.type;
            viewItem.SubItems.Add(p.data.Length.ToString());
            viewItem.SubItems.Add(p.from);
            viewItem.SubItems.Add(p.socketHandle.ToString());
            viewItem.SubItems.Add(dataToString(p.data));

            GetListView().Items.Add(viewItem);
        }

        public ListView GetListView()
        {
            return this.listView1;
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWow64Process(
             [In] IntPtr hProcess,
             [Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process
             );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryFullProcessImageName(
      [In] IntPtr hProcess,
      [In] int dwFlags,
      [Out] StringBuilder lpExeName,
      ref int lpdwSize);

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            QueryLimitedInformation = 0x00001000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
         ProcessAccessFlags processAccess,
         bool bInheritHandle,
         int processId);

        void fillProcesses()
        {
            this.comboBox1.Items.Clear();
            is32bitProcess.Clear();

            Process[] processCollection = Process.GetProcesses();

            Array.Sort(processCollection, (x, y) => x.ProcessName.CompareTo(y.ProcessName));
            
            foreach (Process p in processCollection)
            {
                IntPtr ptr = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, p.Id);
                bool is32Bit = false;
                IsWow64Process(ptr, out is32Bit);

                is32bitProcess.Add(p.Id, is32Bit);
                this.comboBox1.Items.Add(string.Format("{0} [{2}] (pid={1})", p.ProcessName, p.Id, is32Bit ? "x86" : "x64"));
            }

            savedProcesses = processCollection;
        }

        RemoteChannel remoteChannel;

        public Form1()
        {
            InitializeComponent();

            sendingList = new SendingList(checkedListBox1);
            sendingList.OnItemCountChanged += SendingList_OnSendingListItemCountChanged;

            saveFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            listView1.GetType()
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(listView1, true, null);

            Application.ApplicationExit += Application_ApplicationExit;

            /*
             * 
            var mp = new MessagePackage();

            var bytes = new byte[1024];

            mp.data = bytes;
            mp.from = "127.0.0.1";
            mp.type = "Send";

            AddPackage(mp);
            
             */
        }

        private void SendingList_OnSendingListItemCountChanged(int count)
        {
            if (count == 0)
            {
                playPackagesButton.Enabled = false;
                saveButton.Enabled = false;
            }
            else
            {
                playPackagesButton.Enabled = true;
                saveButton.Enabled = true;
            }
        }

        private void Application_ApplicationExit(object sender, EventArgs e)
        {
            if (remoteChannel != null)
                remoteChannel.Close();
        }

        bool isRecording = false;
           

        private void button2_Click(object sender, EventArgs e)
        {
            fillProcesses();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            fillProcesses();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            updateControlButtons();
        }

        int injectIntoProcess(Process process, bool is32bit)
        {
            var path = Path.GetDirectoryName(Application.ExecutablePath);
            var args = "attach -pid " + process.Id;

            Process p = new Process();
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.FileName = path + "\\console_injector_" + (is32bit ? "x86" : "x64" ) + ".exe";
            p.StartInfo.Arguments = args;
            
            if (System.Environment.OSVersion.Version.Major >= 6)
            {
                p.StartInfo.Verb = "runas";
            }

            p.Start();
            p.WaitForExit(5000);

            return p.ExitCode;
        }

        void updateControlButtons()
        {
            if (comboBox1.SelectedIndex < 0)
                return;

            if (!isRecording)
            {
                var process = savedProcesses[comboBox1.SelectedIndex];

                // selection is changed
                if (injectedProcess == null || (injectedProcess != null && process.Id != injectedProcess.Id))
                {
                    if (remoteChannel != null)
                        remoteChannel.Close();
                    
                    remoteChannel = new RemoteChannel(this);

                    var is32bit = is32bitProcess[process.Id];

                    var exitCode = injectIntoProcess(process, is32bit);
                    if (exitCode != 0)
                    {
                        MessageBox.Show("Cannot inject into process. Exit code=" + exitCode.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    remoteChannel.Start();
                    injectedProcess = process;
                }
  
                comboBox1.Enabled = false;

                button3.Text = "stop";
                this.listView1.Items.Clear();
                packages.Clear();
                remoteChannel.SendStartRecord();
            }
            else
            {
                comboBox1.Enabled = true;
                button3.Text = "record";
                remoteChannel.SendPauseRecord();
            }

            isRecording = !isRecording;
        }

        private void listView1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var si = listView1.SelectedIndices;
               // if (si.Count > 0)
                {
                    contextMenuStrip1.Show(listView1, e.Location);
                }
            }
        }

        public MessagePackage GetSelectedPackage()
        {
            var si = listView1.SelectedIndices;
            if (si.Count == 0)
                return null;

            return packages[si[0]];
        }

        public void SendPackage(MessagePackage p)
        {
            remoteChannel.SendPackage(p);
        }

        private void resendToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Resend resendForm = new Resend(this, new MessagePackage[] { this.GetSelectedPackage() });
            resendForm.ShowDialog();
        }

        private void splitContainer1_Panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                var package = packages[listView1.SelectedIndices[0]];

                HexBox h = new HexBox(this, package, (byte []data) =>
                {
                    //
                    package.data = data;
                    listView1.SelectedItems[0].SubItems[3].Text = dataToString(data);

                }, "");
                h.ShowDialog();
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        bool nameAlreadyExist(string name)
        {
            return sendingList.HasName(name);
        }

        private void toSendingListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 0)
            {
                return;
            }

            var package = packages[listView1.SelectedIndices[0]];

            NameBox n = new NameBox(sendingList.GenerateUniqName(), nameAlreadyExist);
            n.StartPosition = FormStartPosition.Manual;
            n.Location = new Point(Cursor.Position.X - n.Width / 2, Cursor.Position.Y - n.Height / 2 + 10);

            var res = n.ShowDialog();
            if (res == DialogResult.OK)
            {
                sendingList.AddLast(n.GetName(), package);
            }
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (checkedListBox1.SelectedIndex == -1)
                return;

            sendingList.Remove(checkedListBox1.SelectedIndex);
        }


        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (checkedListBox1.SelectedIndex == -1)
                return;

            NameBox nb = new NameBox(checkedListBox1.SelectedItem.ToString(), nameAlreadyExist);
            nb.StartPosition = FormStartPosition.Manual;
            var screenLocationOfListbox = checkedListBox1.PointToScreen(Point.Empty);

            nb.Location = new Point(Cursor.Position.X - nb.Width / 2, Cursor.Position.Y - nb.Height / 2 + 10);
            if (nb.ShowDialog(this) == DialogResult.OK)
            {
                sendingList.Rename(checkedListBox1.SelectedIndex, nb.GetName());
            }
        }

        private void checkedListBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                checkedListBox1.SelectedIndex = checkedListBox1.IndexFromPoint(e.X, e.Y);
                if (checkedListBox1.SelectedIndex >= 0)
                {
                    contextMenuStrip2.Show(this.checkedListBox1, e.Location);
                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                checkedListBox1.SelectedIndex = checkedListBox1.IndexFromPoint(e.X, e.Y);
            }
        }

        private void setSocketIdToSendingListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 0)
            {
                return;
            }

            var package = packages[listView1.SelectedIndices[0]];

            string text = string.Format("We are going to set socket id({0}) to all packets from sending list. Continue?", package.socketHandle);
            var res = MessageBox.Show(text, "Just a question", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                sendingList.SetSocketHandle(package.socketHandle);
            }
        }

        private void playPackagesButton_Click(object sender, EventArgs e)
        {
            var msgs = sendingList.SelectedPackages();
            if (msgs.Length == 0)
                return;
            Resend sendForm = new Resend(this, msgs);
            sendForm.ShowDialog();
        }

        private void addNewPackageButton_Click(object sender, EventArgs e)
        {
            HexBox box = new HexBox(this, new MessagePackage(new byte[]{ 0, 0, 0 }, -1), (byte[] data) =>
            {

            }, "");
            box.SetCreationMode(true);
            box.ShowDialog();
        }

        private void editToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (checkedListBox1.SelectedIndex < 0)
                return;

            var mp = sendingList.GetPackage(checkedListBox1.SelectedIndex);
            HexBox box = new HexBox(this, mp, (byte[] data) =>
            {
                mp.data = data;
            }, checkedListBox1.SelectedItem.ToString());
            box.ShowDialog();
        }

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void checkedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            this.BeginInvoke((MethodInvoker)(() => {
                if (checkedListBox1.CheckedItems.Count > 0)
                {
                    playPackagesButton.Enabled = true;
                }
                else
                {
                    playPackagesButton.Enabled = false;
                }
            }));
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            /*saveFileDialog1.FileName = "saved_filters.csv";
            
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                sendingList.SaveAllToFile(saveFileDialog1.FileName);
            }
            */
        }

        private void button1_Click(object sender, EventArgs e)
        {/*
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                sendingList.LoadFromFile(openFileDialog1.FileName);
            }*/
        }
    }
}
