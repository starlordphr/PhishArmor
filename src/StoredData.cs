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
    public partial class StoredData : UserControl
    {
        public static StoredData StoredData_Instance;
        String emailId = "";
        String trust = "";
        Dictionary<String, String> emailData;
        public char emailAlphabet = 'a';

        public StoredData()
        {
            InitializeComponent();

            StoredData_Instance = this;
            emailData = new Dictionary<String, String>();
            emailAlphabet = 'a';
        }

        private void StoredDataForm_Load(object sender, EventArgs e)
        {
            // TODO: This code loads data into the flowlayout          
            getData(emailAlphabet, true);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (emailAlphabet == 'z' + 1)
            {
                return;
            }
            if (emailAlphabet == 'z')
            {
                ++emailAlphabet;
                RemoveControl();
                getData(emailAlphabet, false);
                return;
            }

            RemoveControl();
            ++emailAlphabet;
            getData(emailAlphabet, true);
        }

        public void getData(char Alphabet, bool isAlphabet)
        {
            String Query;
            SqlCommand cmd;
            SqlDataReader reader;
            DatabaseConnection.InitializeConnection();

            if (isAlphabet)
            {
                Query = "SELECT * FROM addresslist WHERE EmailId LIKE '" + Alphabet + "%';";
            }
            else
            {
                Query = "SELECT * FROM addresslist WHERE EmailId LIKE '[0-9]%';";
            }

            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                emailId = String.Copy(reader.GetString(1));
                trust = String.Copy(reader.GetString(2));

                flowLayoutPanel1.Controls.Add(new DatabaseControl(emailId, trust));
            }
            reader.Close();
            DatabaseConnection.CloseDB();
        }

        public void RemoveControl()
        {
            flowLayoutPanel1.Controls.Clear();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (emailAlphabet == 'a')
            {
                return;
            }
            RemoveControl();
            --emailAlphabet;
            getData(emailAlphabet, true);
        }
    }
}
