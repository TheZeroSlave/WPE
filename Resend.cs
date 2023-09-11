using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sniffer
{
    partial class Resend : Form
    {
        Form1 form;
        System.Timers.Timer timer;
        int remains;
        int total;
        MessagePackage[] packages;
        int packageCursor;

        bool started;

        public Resend(Form1 f, MessagePackage[] ps)
        {
            InitializeComponent();
            progressBar1.Visible = false;
            form = f;
            packages = ps;
        }

        #region Assembly Attribute Accessors

        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public string AssemblyProduct
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }
        #endregion

        private void button1_Click(object sender, EventArgs e)
        {
            statusLabel.Text = "";
            if (started)
            {
                stop();
                return;
            }

            progressBar1.Visible = true;
            

            timer = new System.Timers.Timer();
            timer.Interval = Decimal.ToDouble(numericUpDown2.Value);
            timer.AutoReset = true;
            timer.Elapsed += Timer_Elapsed;
            remains = Decimal.ToInt32(numericUpDown1.Value);
            total = remains;
            progressBar1.Value = 0;
            progressBar1.Maximum = total;

            numericUpDown1.Enabled = false;
            numericUpDown2.Enabled = false;
            timer.Start();
            
            started = true;
            packageCursor = 0;
            button1.Text = "stop";
        }

        void stop()
        {
            started = false;
            button1.Text = "send";
            timer.Stop();
            progressBar1.Visible = false;

            numericUpDown1.Enabled = true;
            numericUpDown2.Enabled = true;
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            statusLabel.Invoke((MethodInvoker)(() =>
            {
                progressBar1.Value = total - remains;

                if (remains == 0)
                {
                    stop();
                    return;
                }

                try
                {
                    form.SendPackage(packages[packageCursor]);
                    packageCursor = (packageCursor + 1) % packages.Length;

                    if (packageCursor == 0)
                    {
                        remains--;
                    }
                }
                catch (Exception ex)
                {
                    stop();
                    statusLabel.Text = ex.Message;
                    return;
                }
            })
            );
        }
    }
}
