using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace PhishArmor
{
    public partial class DatabaseControl : UserControl
    {
        public DatabaseControl(String emailId, String trust)
        {
            InitializeComponent();
            label1.Text = emailId;

            if(trust == "1")
            {
                checkBox1.Checked = true;
            }
            else
            {
                checkBox1.Checked = false;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            DatabaseConnection.InitializeConnection();
            String Query;
            SqlCommand cmd;

            Query = "DELETE FROM AddressList WHERE EmailId='" + label1.Text.ToString() + "';";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            cmd.ExecuteNonQuery();
            DatabaseConnection.CloseDB();

            StoredData.StoredData_Instance.RemoveControl();
            if (StoredData.StoredData_Instance.emailAlphabet == 'z' + 1)
            {
                StoredData.StoredData_Instance.getData(StoredData.StoredData_Instance.emailAlphabet, false);
            }
            else
            {
                StoredData.StoredData_Instance.getData(StoredData.StoredData_Instance.emailAlphabet, true);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            
        }

        private void checkBox1_MouseClick(object sender, MouseEventArgs e)
        {
            DatabaseConnection.InitializeConnection();
            String Query;
            SqlCommand cmd;

            if (checkBox1.Checked == true)
            {
                Query = "UPDATE AddressList SET Trusted='1' WHERE EmailId='" + label1.Text.ToString() + "';";
            }
            else
            {
                Query = "UPDATE AddressList SET Trusted='0' WHERE EmailId='" + label1.Text.ToString() + "';";
            }

            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            cmd.ExecuteNonQuery();
            DatabaseConnection.CloseDB();

            StoredData.StoredData_Instance.RemoveControl();

            if(StoredData.StoredData_Instance.emailAlphabet == 'z' + 1)
            {
                StoredData.StoredData_Instance.getData(StoredData.StoredData_Instance.emailAlphabet, false);
            }
            else
            {
                StoredData.StoredData_Instance.getData(StoredData.StoredData_Instance.emailAlphabet, true);
            }
        }
    }
}
