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
    public partial class Search : UserControl
    {
        String emailId;
        String trust;

        public Search()
        {
            InitializeComponent();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            RemoveControl();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            RemoveControl();
            if (textBox1.Text.ToString() == null || textBox1.Text.ToString() == String.Empty)
            {
                MessageBox.Show("Enter search parameter");
                return;
            }

            RemoveControl();
            String Query;
            SqlCommand cmd;
            SqlDataReader reader;
            DatabaseConnection.InitializeConnection();

            Query = "SELECT * FROM addresslist WHERE EmailId LIKE '" + textBox1.Text.ToString() + "';";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                emailId = String.Copy(reader.GetString(1));
                trust = String.Copy(reader.GetString(2));

                this.flowLayoutPanel1.Controls.Add(new DatabaseControl(emailId, trust));
            }
            reader.Close();
            DatabaseConnection.CloseDB();
        }

        public void RemoveControl()
        {
            this.flowLayoutPanel1.Controls.Clear();
        }
    }
}
