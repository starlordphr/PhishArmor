using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PhishArmor
{
    public partial class Settings_Window : UserControl
    {

        public Settings_Window()
        {
            InitializeComponent();        
            ThisAddIn.Instance.InitialProtectionStatus();
        }

        public void SetStatusLabel()
        {
            if(ThisAddIn.emailProtection)
            {
                emailOn.BringToFront();
                emailOff.SendToBack();
                checkBox1.Checked = true;
            }
            else
            {
                emailOn.SendToBack();
                emailOff.BringToFront();
                checkBox1.Checked = false;
            }

            if(ThisAddIn.replytoProtection)
            {
                replyOn.BringToFront();
                replyOff.SendToBack();
                checkBox2.Checked = true;
            }
            else
            {
                replyOn.SendToBack();
                replyOff.BringToFront();
                checkBox2.Checked = false;
            }
        }

        private void checkBox1_Click(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                ThisAddIn.Instance.UpdateEmailProtectionStatus(true);
            }
            else
            {
                ThisAddIn.Instance.UpdateEmailProtectionStatus(false);
            }
            SetStatusLabel();
        }

        private void checkBox2_Click(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                ThisAddIn.Instance.UpdateReplyToProtectionStatus(true);
            }
            else
            {
                ThisAddIn.Instance.UpdateReplyToProtectionStatus(false);
            }
            SetStatusLabel();
        }
    }
}
