using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PhishArmor
{
    public partial class ModernSettingsForm : Form
    {
        public ModernSettingsForm()
        {
            InitializeComponent();
            SidePanel.Height = button1.Height;
            SidePanel.Top = button1.Top;

            storedData1.BringToFront();
            search1.SendToBack();
            settings_Window1.SendToBack();
        }

        private void ModernSettingForm_Shown(object sender, EventArgs e)
        {
            ThisAddIn.Instance.InitialProtectionStatus();
        }

        private void label2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SidePanel.Height = button1.Height;
            SidePanel.Top = button1.Top;

            storedData1.BringToFront();
            search1.SendToBack();
            settings_Window1.SendToBack();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SidePanel.Height = button2.Height;
            SidePanel.Top = button2.Top;

            storedData1.SendToBack();
            search1.BringToFront();
            settings_Window1.SendToBack();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            DatabaseConnection.InitializeConnection();
            String Query;
            SqlCommand cmd;

            Query = "UPDATE AddressList SET Trusted='1';";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            cmd.ExecuteNonQuery();
            DatabaseConnection.CloseDB();

            storedData1.RemoveControl();
            storedData1.getData(storedData1.emailAlphabet, true);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            DatabaseConnection.InitializeConnection();
            String Query;
            SqlCommand cmd;

            Query = "UPDATE AddressList SET Trusted='0';";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            cmd.ExecuteNonQuery();
            DatabaseConnection.CloseDB();

            storedData1.RemoveControl();
            storedData1.getData(storedData1.emailAlphabet, true);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            settings_Window1.SetStatusLabel();

            SidePanel.Height = button3.Height;
            SidePanel.Top = button3.Top;

            storedData1.SendToBack();
            search1.SendToBack();
            settings_Window1.BringToFront();
        }
    }
}
