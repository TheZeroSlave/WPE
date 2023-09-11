using System;
using System.Windows.Forms;

namespace Sniffer
{
    public partial class NameBox : Form
    {
        public delegate bool IsNameExist(string text);

        IsNameExist existText;

        public NameBox(string defaultName, IsNameExist et)
        {
            InitializeComponent();

            nameBox1.Text = defaultName;
            statusLabel.Text = "";
            existText = et;
        }

        public string GetName()
        {
            return nameBox1.Text;
        }

        private void nameBox1_TextChanged(object sender, EventArgs e)
        {
            if (nameBox1.Text.Length == 0)
            {
                okButton.Enabled = false;
                statusLabel.Text = "cannot be empty";
            }
            else
            {
                if (existText != null && existText(nameBox1.Text))
                {
                    okButton.Enabled = false;
                    statusLabel.Text = "already exist";
                    return;
                }

                okButton.Enabled = true;
                statusLabel.Text = "";
            }
        }

        private void NameBox_KeyPress(object sender, KeyPressEventArgs e)
        {
        }

        private void NameBox_KeyDown(object sender, KeyEventArgs e)
        {

        }
    }
}
