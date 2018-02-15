using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Outlook = Microsoft.Office.Interop.Outlook;
using DiffMatchPatch;

namespace PhishArmor
{
    public partial class ModernAlertForm : Form
    {
        String senderEmail;
        String suspiciousMatch;
        Outlook.MailItem mailItem;
        String type;
        List<String> fetchedRecipientList = new List<String>();
        public ModernSettingsForm settingForm = null;

        public ModernAlertForm(String senderEmail, String suspiciousMatch, Outlook.MailItem mailItem, string type, List<String> fetchedRecipientList)
        {
            InitializeComponent();

            emailoption.ReadOnly = true;
            currentEmailID.ReadOnly = true;
            radioButton2.Checked = true;
            message.ReadOnly = true;
            text1.ReadOnly = true;
            text2.ReadOnly = true;
            replyto_text.ReadOnly = true;

            this.senderEmail = senderEmail;
            this.suspiciousMatch = suspiciousMatch;
            this.mailItem = mailItem;
            this.type = type;
            this.fetchedRecipientList = fetchedRecipientList;
            checkBox1.Visible = false;
            checkBox1.Checked = false;

            if(suspiciousMatch.Split('\t').Length - 1 == 1)
            {
                suspiciousMatch = suspiciousMatch.Replace("\t", "");
            }
            else
            {
                suspiciousMatch = suspiciousMatch.Replace("\t", "");
            }
        }

        private void ModernAlertForm_Shown(object sender, EventArgs e)
        {
            List<String> emailList = new List<String>();
            if (!String.IsNullOrEmpty(this.mailItem.ReplyRecipientNames) && this.mailItem.ReplyRecipientNames.Contains(';'))
            {
                emailList = this.mailItem.ReplyRecipientNames.Split(';').ToList();
            }
            else
            {
                emailList.Add(this.mailItem.ReplyRecipientNames);
            }

            try
            {
                if (String.Compare(this.type, "EMAIL") == 0)
                {
                    var dmp = new diff_match_patch();

                    message.DeselectAll();
                    message.SelectionColor = Color.Black;
                    message.SelectionFont = new Font("Century Gothic", 21, FontStyle.Bold);
                    message.AppendText("This might be a phishing attack!");

                    text1.SelectionColor = Color.Black;
                    text1.SelectionFont = new Font("Century Gothic", 16, FontStyle.Bold);
                    text1.SelectionAlignment = HorizontalAlignment.Right;
                    text1.AppendText("Sender's Email ID:");

                    diff_rebuildtexts(dmp.diff_main(this.senderEmail, this.suspiciousMatch), "sender", currentEmailID);

                    text2.SelectionColor = Color.Black;
                    text2.SelectionFont = new Font("Century Gothic", 16, FontStyle.Bold);
                    text2.SelectionAlignment = HorizontalAlignment.Right;
                    text2.AppendText("Matched Email ID:");

                    diff_rebuildtexts(dmp.diff_main(this.senderEmail, this.suspiciousMatch), "suspicious", suspiciousEmailID);

                    emailoption.SelectionColor = Color.Black;
                    emailoption.SelectionFont = new Font("Century Gothic", 17, FontStyle.Regular);
                    emailoption.SelectionAlignment = HorizontalAlignment.Right;
                    emailoption.AppendText("Do you trust this email Id?");
                }

                if (String.Compare(this.type, "REPLY-TO") == 0)
                {
                    //richTextBox2.DeselectAll();
                    message.SelectionColor = Color.Black;
                    message.SelectionFont = new Font("Century Gothic", 21, FontStyle.Bold);
                    message.AppendText("This might be a phishing attack!");

                    text1.SelectionColor = Color.Black;
                    text1.SelectionFont = new Font("Century Gothic", 16, FontStyle.Bold);
                    text1.SelectionAlignment = HorizontalAlignment.Right;
                    text1.AppendText("Sender's Reply-To ID:");

                    currentEmailID.SelectionColor = Color.Red;
                    currentEmailID.SelectionFont = new Font("Century Gothic", 12, FontStyle.Bold);
                    for (int i = 0; i < emailList.Count; ++i)
                    {
                        currentEmailID.AppendText(emailList[i]);
                        if (i != emailList.Count - 1)
                        {
                            currentEmailID.AppendText(" , ");
                        }
                    }

                    text2.SelectionColor = Color.Black;
                    text2.SelectionFont = new Font("Century Gothic", 16, FontStyle.Bold);
                    text2.SelectionAlignment = HorizontalAlignment.Right;
                    text2.AppendText("Stored Reply-To ID:");

                    suspiciousEmailID.SelectionColor = Color.Red;
                    suspiciousEmailID.SelectionFont = new Font("Century Gothic", 12, FontStyle.Bold);
                    for (int i = 0; i < fetchedRecipientList.Count; ++i)
                    {
                        String stringTest = fetchedRecipientList[i].Trim();
                        stringTest = stringTest.Replace(" ", "");
                        suspiciousEmailID.AppendText(stringTest);
                    }

                    emailoption.SelectionColor = Color.Black;
                    emailoption.SelectionFont = new Font("Century Gothic", 17, FontStyle.Regular);
                    emailoption.SelectionAlignment = HorizontalAlignment.Right;
                    emailoption.AppendText("Do you trust this email Id?");

                    replyto_text.SelectionColor = Color.Black;
                    replyto_text.SelectionFont = new Font("Century Gothic", 17, FontStyle.Regular);
                    replyto_text.SelectionAlignment = HorizontalAlignment.Left;
                    replyto_text.AppendText("Do you trust this reply-to address?");

                    checkBox1.Checked = true;
                    checkBox1.Visible = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Form Error : " + ex.ToString());
            }
        }

        private static void diff_rebuildtexts(List<Diff> diffs, string email_type, RichTextBox richtextbox)
        {
            string[] text = { "", "" };
            foreach (Diff myDiff in diffs)
            {
                if (myDiff.operation == Operation.INSERT && email_type == "suspicious")
                {
                    text[1] += myDiff.text;

                    richtextbox.SelectionColor = Color.Red;
                    richtextbox.SelectionFont = new Font("Century Gothic", 16, FontStyle.Bold);
                    richtextbox.AppendText(myDiff.text);

                }
                if (myDiff.operation == Operation.DELETE && email_type == "sender")
                {
                    text[0] += myDiff.text;

                    richtextbox.SelectionColor = Color.Red;
                    richtextbox.SelectionFont = new Font("Century Gothic", 16, FontStyle.Bold);
                    richtextbox.AppendText(myDiff.text);

                }
                if (myDiff.operation == Operation.EQUAL)
                {
                    richtextbox.SelectionColor = Color.Blue;
                    richtextbox.SelectionFont = new Font("Century Gothic", 16, FontStyle.Bold);
                    richtextbox.AppendText(myDiff.text);

                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                DatabaseConnection.InitializeConnection();
                ThisAddIn.Instance.AddEmailId(senderEmail, ThisAddIn.Instance.ReplaceSemiColon(this.mailItem.ReplyRecipientNames));
                ThisAddIn.Instance.CheckTrust(senderEmail);
                DatabaseConnection.CloseDB();
                MessageBox.Show("Email Id added to database.");
            }

            if (radioButton2.Checked && checkBox1.Checked)
            {
                DatabaseConnection.InitializeConnection();
                ThisAddIn.Instance.AddEmailId(senderEmail, ThisAddIn.Instance.ReplaceSemiColon(this.mailItem.ReplyRecipientNames));
                DatabaseConnection.CloseDB();
                MessageBox.Show("Information added to database.");
            }

            if (radioButton2.Checked && !checkBox1.Checked)
            {
                Outlook.MAPIFolder spamFolder = (Outlook.MAPIFolder)ThisAddIn.Instance.Application.ActiveExplorer().Session.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderJunk);
                mailItem.Move(spamFolder);
                MessageBox.Show("Email moved to Spam folder.");
            }
            this.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void label2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            settingForm = new ModernSettingsForm();
            settingForm.Show();
        }
    }
}
