using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Outlook = Microsoft.Office.Interop.Outlook;
using Office = Microsoft.Office.Core;
using System.Data.SqlClient;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;

namespace PhishArmor
{
    public partial class ThisAddIn
    {
        //Declare variables
        private Outlook.Accounts accounts = null;
        private Outlook.Folder selectedFolder = null;
        public static List<string> emailAddresses = new List<string>();
        public static List<string> replyToList = new List<string>();
        public List<string> perEmailReplyList = new List<string>();

        public static ThisAddIn Instance = null;
        public ModernAlertForm modernForm;

        public static bool emailProtection = false;
        public static bool replytoProtection = false;

        //Declare Outlook objects
        Outlook.NameSpace outlookNameSpace;
        Outlook.MAPIFolder inbox;
        Outlook.Items items;
        Outlook.Explorer currentExplorer = null;

        //Variable for tackling trigger being called twice
        //The explorer ui event is called twice whenever you click on an email. This is used for triggering the email check only once
        int Occurrence = 0;

        /* This method is called when the addin first starts
         * Functionality : This method initializes connection, updates the DB for the first time
         * and sets up all the Event Handlers.
         */
        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); //Important DO NOT Delete

            //Startup and ItemLoad Event Handlers
            this.Application.Startup += ApplicationStartup;
            this.Application.ItemLoad += ApplicationItemLoad;
            Instance = this;

            //Check if table is populated
            bool isUpdated = CheckInitialStatus();
            if (!isUpdated)
            {
                GetEmailFromInbox(); //Get EmailId from Inbox
                InsertAddress(); //Insert EmailId in DB
                NoteStatus(); //Update config table
                emailProtection = true; //If first run, enable email protection
                replytoProtection = true; //If first run, enable reply-to protection

            }
            outlookNameSpace = this.Application.GetNamespace("MAPI");
            inbox = outlookNameSpace.GetDefaultFolder(Microsoft.Office.Interop.Outlook.OlDefaultFolders.olFolderInbox);
            items = inbox.Items;
            items.ItemAdd += new Outlook.ItemsEvents_ItemAddEventHandler(itemsEmailReceived); //Event Handler for new incoming emails

            //Explorer Events Event Handler : Main Trigger
            currentExplorer = this.Application.ActiveExplorer();
            currentExplorer.SelectionChange += new Outlook.ExplorerEvents_10_SelectionChangeEventHandler(CurrentExplorerEvent);
        }

        /* 
         * Functionality : Helper method to obtain email address from inbox
         */
        private void GetEmailFromInbox()
        {
            accounts = Application.Session.Accounts;
            selectedFolder = Application.Session.DefaultStore.GetRootFolder() as Outlook.Folder;

            //Traverse through all the logged in accounts in outlook
            foreach (Outlook.Account account in accounts)
            {
                selectedFolder = GetFolder(@"\\" + account.DisplayName);
                EnumerateFolders(selectedFolder);
            }
        }

        /* 
         * Functionality : Insert email Id into DB
         */
        private void InsertAddress()
        {
            for (int i = 0; i < emailAddresses.Count; i++)
            {
                AddEmailId(emailAddresses[i], replyToList[i], true);
            }
        }

        /* 
         * Functionality : Update the status of config table once the plugin initialisation is completed.
         */
        private void NoteStatus()
        {
            String Query;
            SqlCommand cmd;
            DatabaseConnection.InitializeConnection();

            //Change the config parameter for addin
            // DatabaseStatus = True specifies that the initialisation is completed and the metadata table is populated
            // EmailProtection and ReplyToProtection = True specifies that both of these protections are enabled
            Query = "UPDATE Configuration SET DatabaseStatus = 'True', EmailProtection = 'True', ReplyToProtection = 'True' WHERE Id = 1";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            cmd.ExecuteNonQuery();

            DatabaseConnection.CloseDB();
        }

        /* 
         * Functionality : This function is called when a new email is received. It is used to update the database with new email Id metadata.
         */
        void itemsEmailReceived(object Item)
        {
            Outlook.MailItem mail = (Outlook.MailItem)Item;
            List<String> suspiciousEmailIds = new List<String>(); //List to store Suspicious EmailIds

            if (Item != null)
            {
                //Check if the email id is in the database and if it is not then
                if (!CheckIfExists(mail))
                {
                    suspiciousEmailIds = GetSuspiciousAddresses(mail.Sender.Address); //Get Suspicious Email Ids
                    if (suspiciousEmailIds.Count == 0)
                    {
                        emailAddresses.Add(MySqlEscape(mail.Sender.Address, "OUTPUT")); //Add email Id into list
                        string replyRecipientList = mail.ReplyRecipientNames;

                        if (!String.IsNullOrEmpty(replyRecipientList))
                        {
                            replyToList.Add(ReplaceSemiColon(MySqlEscape(replyRecipientList, "OUTPUT")));
                            AddEmailId(mail.Sender.Address, replyRecipientList); //Insert email Id into DB
                        }
                        else
                        {
                            AddEmailId(mail.Sender.Address, replyRecipientList); //Insert email Id into DB
                        }
                    }
                }
                else if(CheckIfExists(mail)) //If the email exist in the database
                {
                    string replyRecipientList = mail.ReplyRecipientNames;
                    if (!String.IsNullOrEmpty(replyRecipientList))
                    {
                        replyToList.Add(ReplaceSemiColon(MySqlEscape(replyRecipientList, "OUTPUT")));
                    }
                    AddEmailId(mail.Sender.Address, replyRecipientList);
                }
            }
        }

        /* Triggered when the user selects a different or additional Microsoft Outlook item programmatically or by interacting with the user interface.
         * Functionality : This Event Handler is used to trigger the eamil spear phishing detection.
         */
        private void CurrentExplorerEvent()
        {           
            if (Occurrence == 0) //If first call to trigger
            {
                Outlook.MAPIFolder selectedFolder = this.Application.ActiveExplorer().CurrentFolder;
                List<String> suspiciousEmailIds = new List<String>(); //List to store Suspicious EmailIds
                List<String> suspiciousDisplayEmailIds = new List<String>();
                String supiciousEmailList = ""; //String of suspicious emails detected
                String displayString = ""; //The string to display in the error form
                
                try
                {
                    if (this.Application.ActiveExplorer().Selection.Count == 1 && this.Application.ActiveExplorer().Selection[1] is Outlook.MailItem)
                    {
                        Object selObject = this.Application.ActiveExplorer().Selection[1]; //Select first object
                        Outlook.MailItem mailItem = (selObject as Outlook.MailItem); //Typecast it to MailItem

                        //Email Protection: Make sure that the email id is not trusted and that email protection is enabled
                        if (!CheckIfTrusted(mailItem.Sender.Address) && emailProtection)
                        {
                            suspiciousEmailIds = GetSuspiciousAddresses(mailItem.Sender.Address); //Get Suspicious Email Ids
                          
                            /* suspiciousEmailIds contains a list of all the suspicious email ids:
                             * This list helps us to reduce the number of possible candidates for calculating
                             * Levenshtein Distance. This step is important for speedup of the addin.
                             */
                            for (int i = 0; i < suspiciousEmailIds.Count; i++)
                            {
                                displayString += suspiciousEmailIds[i] + "\n";
                                int numChanges = LevenshteinDistance.Compute(suspiciousEmailIds[i], mailItem.Sender.Address); //Calculate Levenshtein Distance and return number of changes
                                if (numChanges >= 1 && numChanges <= 3) //Changes between 1 to 3 are reported as suspicious
                                {
                                    suspiciousDisplayEmailIds.Add(suspiciousEmailIds[i]); //Create a list of all the suspicious matching email ids
                                }
                            }
                      
                            if (suspiciousDisplayEmailIds.Count > 0) //If suspicious email ids detected
                            {

                                if(suspiciousDisplayEmailIds.Count == 1) //If only one suspicious email id
                                {
                                    supiciousEmailList = suspiciousDisplayEmailIds[0];
                                }
                                else if(suspiciousDisplayEmailIds.Count > 1) //If more than one suspicious email id
                                {
                                    int i = 0;
                                    //Preparing for display
                                    for (i=0;i<suspiciousDisplayEmailIds.Count;++i)
                                    {
                                        if(i == suspiciousDisplayEmailIds.Count - 1)
                                        {
                                            supiciousEmailList += suspiciousDisplayEmailIds[i];
                                        }
                                        else
                                        {
                                            supiciousEmailList += suspiciousDisplayEmailIds[i]+"\n";
                                        }                                   
                                    }
                                }
                         
                                try
                                {
                                    //Display alert form
                                    modernForm = new ModernAlertForm(mailItem.Sender.Address, supiciousEmailList, mailItem, "EMAIL", perEmailReplyList);
                                    modernForm.Show();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Form Error in ThisAddin : "+ex.ToString());
                                }
                            }
                        }

                        //Reply-To Protection: This is triggered for reply-to check in which the sender's reply-to address is checked with the stored
                        //reply-to addresses
                        if(CheckIfExists(mailItem) && replytoProtection)
                        {
                            //If not stored reply-to addresses match then display alert
                            if(!CheckReplyToAddress(mailItem))
                            {
                                try
                                {
                                    //Display alert
                                    modernForm = new ModernAlertForm(mailItem.Sender.Address, supiciousEmailList, mailItem, "REPLY-TO", perEmailReplyList);
                                    modernForm.Show();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Form Error in ThisAddin : " + ex.ToString());
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Explorer Event Error : "+ex.Message);
                }
                ++Occurrence;
            }
            else //If second call to trigger
            {
                Occurrence = 0;
            }
        }

        /* 
         * Functionality : Check if the new email id exists in the stored table.
         */
        private bool CheckIfExists(Outlook.MailItem mailItem)
        {
            String Query;
            SqlCommand cmd;
            SqlDataReader reader;
            DatabaseConnection.InitializeConnection();

            //Check if email id exists 
            Query = "SELECT EmailId FROM AddressList WHERE EmailId ='" + mailItem.SenderEmailAddress + "';";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            reader = cmd.ExecuteReader();

            if (reader.Read() && reader.HasRows)
            {
                DatabaseConnection.CloseDB();
                return true;
            }
            reader.Close();
            DatabaseConnection.CloseDB();
            return false;
        }

        /* 
         * Functionality : To check if there is a match between the new reply-to address and any of the stored reply-to addresses
         */
        private bool CheckReplyToAddress(Outlook.MailItem mailItem)
        {
            perEmailReplyList.Clear();
            String Query;
            SqlCommand cmd;
            SqlDataReader reader;
            DatabaseConnection.InitializeConnection();
            string replyToList;
            string toCheckReplyAddress = mailItem.ReplyRecipientNames;
            List<string> replyToRecipients = new List<string>();
            List<string> mailRecipientNames = new List<string>();
            List<bool> arrStatus = new List<bool>();

            //Check if the new reply-to address is not empty
            if (!String.IsNullOrEmpty(toCheckReplyAddress))
            {
                for (int i = 0; i < toCheckReplyAddress.Split(';').Length; ++i)
                {
                    arrStatus.Add(false);
                }
            }
            else if (String.IsNullOrEmpty(toCheckReplyAddress))
                return true;             

            bool status = false;

            //Get the store reply-to address from the table based on the new email id
            Query = "SELECT ReplyToList FROM AddressList WHERE EmailId ='" + mailItem.SenderEmailAddress + "';";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            reader = cmd.ExecuteReader();

            if (reader.Read() && reader.HasRows)
            {
                replyToList = String.Copy(reader.GetString(0));
                DatabaseConnection.CloseDB();
                replyToList = MySqlEscape(replyToList, "OUTPUT");
                if (String.IsNullOrEmpty(toCheckReplyAddress))
                {
                    return true;
                }

                //If the stored reply-to addresses is a list, split it for comparison
                if(replyToList.Contains(':'))
                {
                    replyToRecipients = replyToList.Split(':').ToList();
                    perEmailReplyList = replyToList.Split(':').ToList();
                }
                else
                {
                    replyToRecipients.Add(replyToList);
                    perEmailReplyList.Add(replyToList);
                }
            }

            reader.Close();
            DatabaseConnection.CloseDB();
            //Compare
            if (!String.IsNullOrEmpty(toCheckReplyAddress))
            {
                mailRecipientNames = toCheckReplyAddress.Split(';').ToList();
            }
            else if (String.IsNullOrEmpty(toCheckReplyAddress))
                return true;

            int counter = 0;
            foreach (string replyAddress in mailRecipientNames)
            {
                foreach (string element in replyToRecipients)
                {
                    if (String.Compare(element, replyAddress) == 0)
                    {
                        status = true;
                    }
                }
                if (status == true)
                {
                    arrStatus[counter] = true;
                }
                ++counter;
                status = false;
            }

            for (int i=0;i<arrStatus.Count;++i)
            {
                //If there is no match, return false
                if(arrStatus[i] == false)
                {
                    return false;
                }
            }
            //else return true
            return true;
        }

        /* 
         * Functionality : Update the trust value of an email id.
         */
        public void CheckTrust(String EmailId)
        {
            DatabaseConnection.InitializeConnection();
            String Query;
            SqlCommand cmd;

            Query = "UPDATE AddressList SET Trusted='1' WHERE EmailId='" + EmailId + "';";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            cmd.ExecuteNonQuery();
            DatabaseConnection.CloseDB();
        }

        /* 
       * Functionality : Helper function for iterating through various folders in Outlook
       */
        private Outlook.Folder GetFolder(string folderPath)
        {
            Outlook.Folder folder;
            string backslash = @"\";
            try
            {
                if (folderPath.StartsWith(@"\\"))
                {
                    folderPath = folderPath.Remove(0, 2);
                }
                String[] folders = folderPath.Split(backslash.ToCharArray());
                folder = Application.Session.Folders[folders[0]] as Outlook.Folder;
                if (folder != null)
                {
                    for (int i = 1; i <= folders.GetUpperBound(0); i++)
                    {
                        Outlook.Folders subFolders = folder.Folders;
                        folder = subFolders[folders[i]] as Outlook.Folder;
                        if (folder == null)
                        {
                            return null;
                        }
                    }
                }
                return folder;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        /* 
         * Functionality : Helper function to enumerate through various folders
         */
        private void EnumerateFolders(Outlook.Folder folder)
        {
            Outlook.Folders childFolders = folder.Folders;
            if (childFolders.Count > 0)
            {
                foreach (Outlook.Folder childFolder in childFolders)
                {
                    // We only want Inbox folders - ignore Contacts and others
                    if (childFolder.FolderPath.Contains("Inbox"))
                    {
                        // Call EnumerateFolders using childFolder, to see if there are any sub-folders within this one
                        EnumerateFolders(childFolder);
                    }
                }
            }
            IterateMessages(folder);
        }

        /* 
       * Functionality : Helper method to iterate through messages for getting the email Ids.
       */
        private void IterateMessages(Outlook.Folder folder)
        {
            // attachment extensions to save
            string[] extensionsArray = { ".pdf", ".doc", ".xls", ".ppt", ".vsd", ".zip", ".rar", ".txt", ".csv", ".proj" };

            // Iterate through all items ("messages") in a folder
            if (folder.Items != null)
            {

                try
                {
                    foreach (Object item in folder.Items)
                    {
                        string senderAddress = null;
                        string replyRecipientList = null;

                        if (item is Outlook.MailItem)
                        {
                            Outlook.MailItem mailitem = (Outlook.MailItem)item;                        

                            //senderAddress = mailitem.Sender.Address;
                            senderAddress = mailitem.SenderEmailAddress;

                            if(senderAddress == null)
                            {
                                continue;
                            }

                            //Get Reply-To Email Id List
                            replyRecipientList = mailitem.ReplyRecipientNames;

                            if (!String.IsNullOrEmpty(replyRecipientList))
                            {
                                AddAddressToList(senderAddress, ReplaceSemiColon(replyRecipientList));
                            }
                            else
                            {
                                replyRecipientList = String.Copy(senderAddress);
                                AddAddressToList(senderAddress, replyRecipientList);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occurred test: " + ex.Message);
                }
            }
        }

        /* 
         * Functionality : Stores all the email Ids into a list for further processing
         */
        private void AddAddressToList(string emailAddress, string replyToRecipient)
        {
            //To check if email Id is correct
            if (emailAddress.Contains("@") && emailAddress.Contains("."))
            {
                bool found = false;
                if (emailAddresses.Contains(emailAddress)) //Email address was already present and found in the list
                {
                    // email address was found
                    found = true;
                }
                if (!found)
                {
                    // email address wasn't found, so add it
                    emailAddresses.Add(MySqlEscape(emailAddress, "OUTPUT"));
                    replyToList.Add(MySqlEscape(replyToRecipient, "OUTPUT"));
                }
            }
        }

        /* 
         * Functionality : Method to insert Email Id into the DB
         */
        public void AddEmailId(String emailId, string recipientList, bool initialPush = false)
        {
            String Query;
            SqlCommand cmd;
            SqlDataReader reader;
            DatabaseConnection.InitializeConnection();
            String fetchedReplyToList;

            if (String.IsNullOrEmpty(recipientList))
                recipientList = String.Copy(emailId);

            //Check if email id exists 
            Query = "SELECT ReplyToList FROM AddressList WHERE EmailId ='" + emailId + "';";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            reader = cmd.ExecuteReader();

            if (reader.Read() && reader.HasRows)
            {
                fetchedReplyToList = String.Copy(reader.GetString(0));

                //Email Id exists in DB
                DatabaseConnection.CloseDB();
                //Append to stored Reply-To List                                                
                if (String.Compare(fetchedReplyToList, MySqlEscape(recipientList, "INPUT")) == 0)
                {
                    return;
                }

                string tempConcat = fetchedReplyToList + ":" + recipientList;

                //Update Reply-To List
                DatabaseConnection.InitializeConnection();
                Query = "UPDATE AddressList SET ReplyToList = '" + MySqlEscape(tempConcat, "INPUT") + "' WHERE EmailId = '" + MySqlEscape(emailId, "INPUT") + "';";
                cmd = new SqlCommand(Query, DatabaseConnection.myConn);
                cmd.ExecuteNonQuery();
                reader.Close();
                DatabaseConnection.CloseDB();
            }
            else
            {
                DatabaseConnection.CloseDB();
                //Email Id does not exist in DB
                //Insert into table unique email Ids
                DatabaseConnection.InitializeConnection();
                if(initialPush)
                {
                    Query = "INSERT INTO AddressList(EmailId, Trusted, ReplyToList) values('" + MySqlEscape(emailId, "INPUT") + "', '1' ,'" + MySqlEscape(recipientList, "INPUT") + "')";
                }
                else
                {
                    Query = "INSERT INTO AddressList(EmailId, ReplyToList) values('" + MySqlEscape(emailId, "INPUT") + "', '" + MySqlEscape(recipientList, "INPUT") + "')";
                }
                 
                cmd = new SqlCommand(Query, DatabaseConnection.myConn);
                cmd.ExecuteNonQuery();
                DatabaseConnection.CloseDB();
            }
        }

        /* 
         * Functionality : Retrieve the trust value of the stored email id and check if it trusted.
         */
        public Boolean CheckIfTrusted(String EmailId)
        {
            
            String Query;
            SqlCommand cmd;
            SqlDataReader reader;
            String trustValue = "0";

            DatabaseConnection.InitializeConnection();

            Query = "SELECT * FROM AddressList WHERE EmailId ='" + EmailId + "';";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                trustValue = String.Copy(reader.GetString(2));
            }
            reader.Close();
            DatabaseConnection.CloseDB();

            if (trustValue.Equals("1"))
                return true;
            else
                return false;
        }

        /* 
         * Functionality : This method is used to get a subset of DB which is more likely to be suspicious and hence speeds up the detection.
         */
        List<String> GetSuspiciousAddresses(String receivedEmailId)
        {
            String queryEmailId = receivedEmailId;
            String Query;
            SqlCommand cmd;
            SqlDataReader reader;
            String Data;
            List<String> suspiciousEmailIds = new List<String>();

            //Initialize connection to Database
            DatabaseConnection.InitializeConnection();

            for (int i = 0; i < queryEmailId.Length; i++)
            {
                for (int j = 0; j < queryEmailId.Length; j++)
                {
                    if (i == j) //Skip the same element
                        continue;

                    //Replace two positions with wildcard element '%'
                    StringBuilder sb = new StringBuilder(queryEmailId);
                    sb[i] = '%';
                    sb[j] = '%';

                    String tempEmailId = sb.ToString();

                    //Query for finding possible suspicious candidate email ids
                    Query = "SELECT EmailId FROM Addresslist WHERE EmailId LIKE '" + tempEmailId + "';";
                    cmd = new SqlCommand(Query, DatabaseConnection.myConn);
                    reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        Data = reader.GetString(0); //Get suspicious email id from DB and store it for calculating Levenshtein Distance
                        if (!suspiciousEmailIds.Contains(Data))
                            suspiciousEmailIds.Add(Data); //Add EmailId into a list for processing
                    }
                    reader.Close();
                }
            }
            //Close connection to Database
            DatabaseConnection.CloseDB();
            return suspiciousEmailIds;
        }    

        /* 
         * Functionality : To check the config table for detecting and setting initital value.
         */
        private bool CheckInitialStatus()
        {
            String Data;
            String Query;
            SqlCommand cmd;
            SqlDataReader reader;

            //This keeps executing if the config table already exists. So, for all the future execution of the plugin.
            try
            {
                DatabaseConnection.InitializeConnection();

                //Select config parameter from DB
                Query = "SELECT * FROM Configuration WHERE Id = 1";
                cmd = new SqlCommand(Query, DatabaseConnection.myConn);
                reader = cmd.ExecuteReader();

                reader.Read();
                Data = reader.GetString(1);
                UpdateProtectionStatus(reader.GetString(2), reader.GetString(3));

                reader.Close();
                DatabaseConnection.CloseDB();

                InitialProtectionStatus();

                //To check if DB is initially populated or not
                if (Data == "False")
                    return false;

                if (Data == "True")
                    return true;
            }

            //This is triggered for the first time execution of the plugin
            catch (Exception ex)
            {
                DatabaseConnection.InitializeConnection();

                Query = "DBCC CHECKIDENT('AddressList', RESEED, 0)";
                cmd = new SqlCommand(Query, DatabaseConnection.myConn);
                cmd.ExecuteNonQuery();

                //Create config table for addin
                Query = "INSERT INTO Configuration (Id,DatabaseStatus,EmailProtection,ReplyToProtection) VALUES (1,'False','False','False')";
                cmd = new SqlCommand(Query, DatabaseConnection.myConn);
                cmd.ExecuteNonQuery();

                DatabaseConnection.CloseDB();
                InitialProtectionStatus();

                return false;
            }
            return false;
        }

        /* 
         * Functionality : To check the initial protection status of the email and reply-to protection. 
         * Based on this all the further checks will be performed once the plugin starts.
         */
        public void InitialProtectionStatus()
        {
            String Query;
            SqlCommand cmd;
            SqlDataReader reader;
            String emailProtectionStatus;
            String replytoProtectionStatus;

            DatabaseConnection.InitializeConnection();

            Query = "SELECT * FROM Configuration WHERE Id = 1";
            cmd = new SqlCommand(Query, DatabaseConnection.myConn);
            reader = cmd.ExecuteReader();

            reader.Read();
            emailProtectionStatus = reader.GetString(2);
            replytoProtectionStatus = reader.GetString(3);
            reader.Close();

            if (String.Compare(emailProtectionStatus, "True") == 0)
            {
                emailProtection = true;
            }
            else
            {
                emailProtection = false;
            }

            if (String.Compare(replytoProtectionStatus, "True") == 0)
            {
                replytoProtection = true;
            }
            else
            {
                replytoProtection = false;
            }
          
            DatabaseConnection.CloseDB();
        }

        /* 
         * Functionality : To update the protection status
         */
        public void UpdateProtectionStatus(String emailStatus, String replytoStatus)
        {
            if(String.Compare(emailStatus, "False") == 0)
            {
                emailProtection = false;
            }
            if(String.Compare(replytoStatus, "False") == 0)
            {
                replytoProtection = false;
            }
            if (String.Compare(emailStatus, "True") == 0)
            {
                emailProtection = true;
            }
            if (String.Compare(replytoStatus, "True") == 0)
            {
                replytoProtection = true;
            }
        }

        /* 
         * Functionality : To update the email protection status
         */
        public void UpdateEmailProtectionStatus(bool emailProtectionStatus)
        {
            String Query;
            SqlCommand cmd;

            DatabaseConnection.InitializeConnection();

            if (emailProtectionStatus)
            {
                //Create config table for addin
                Query = "UPDATE Configuration SET EmailProtection = 'True' WHERE Id = 1";
                cmd = new SqlCommand(Query, DatabaseConnection.myConn);
                cmd.ExecuteNonQuery();
                emailProtection = true;           
            }
            else
            {
                //Create config table for addin
                Query = "UPDATE Configuration SET EmailProtection = 'False' WHERE Id = 1";
                cmd = new SqlCommand(Query, DatabaseConnection.myConn);
                cmd.ExecuteNonQuery();
                emailProtection = false;
            }

            DatabaseConnection.CloseDB();
        }

        /* 
         * Functionality : To update the reply-to protection status
         */
        public void UpdateReplyToProtectionStatus(bool replytoProtectionStatus)
        {
            String Query;
            SqlCommand cmd;

            DatabaseConnection.InitializeConnection();

            if (replytoProtectionStatus)
            {
                //Create config table for addin
                Query = "UPDATE Configuration SET ReplyToProtection = 'True' WHERE Id = 1";
                cmd = new SqlCommand(Query, DatabaseConnection.myConn);
                cmd.ExecuteNonQuery();
                replytoProtection = true;
            }
            else
            {
                //Create config table for addin
                Query = "UPDATE Configuration SET ReplyToProtection = 'False' WHERE Id = 1";
                cmd = new SqlCommand(Query, DatabaseConnection.myConn);
                cmd.ExecuteNonQuery();
                replytoProtection = false;
            }

            DatabaseConnection.CloseDB();
        }

        /* 
         * Functionality : To remove all the problematic characters in email id and reply-to address
         */
        public string MySqlEscape(string usString, string type)
        {
            if (usString == null)
            {
                return null;
            }

            if(String.Compare(type, "INPUT") == 0)
                return Regex.Replace(usString, @"'", @"~");

            if (String.Compare(type, "OUTPUT") == 0)
                return Regex.Replace(usString, @"~", @"'");

            return Regex.Replace(usString, @"'", @"");
        }

        /* 
         * Functionality : To remove all the problematic characters in email id and reply-to address
         */
        public string ReplaceSemiColon(string recipientList)
        {
            if (String.IsNullOrEmpty(recipientList))
                return recipientList;
            else if (recipientList.Contains(';'))
                return recipientList.Replace(';', ':');

            return recipientList;
        }

        /* This method is required by default
        */
        private void ApplicationItemLoad(object Item)
        {
            //Do something
            //Item Loaded!
        }

        /* This method is required by default
         */
        private void ApplicationStartup()
        {
            //Do something
            //Application Started!
        }

        private void ThisAddIn_Shutdown(object sender, System.EventArgs e)
        {
            // Note: Outlook no longer raises this event.
        }

        #region VSTO generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InternalStartup()
        {
            this.Startup += new System.EventHandler(ThisAddIn_Startup);
            this.Shutdown += new System.EventHandler(ThisAddIn_Shutdown);
        }

        protected override Office.IRibbonExtensibility CreateRibbonExtensibilityObject()
        {
            return new PhishingTab();
        }

        #endregion
    }

    /* 
     * Functionality : Levenshtein distance implementation
    */
    //Credit: https://www.dotnetperls.com/levenshtein
    static class LevenshteinDistance
    {
        //Compute the distance between two strings.
        public static int Compute(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // Step 1
            if (n == 0)
            {
                return m;
            }

            if (m == 0)
            {
                return n;
            }

            // Step 2
            for (int i = 0; i <= n; d[i, 0] = i++)
            {
            }

            for (int j = 0; j <= m; d[0, j] = j++)
            {
            }

            // Step 3
            for (int i = 1; i <= n; i++)
            {
                //Step 4
                for (int j = 1; j <= m; j++)
                {
                    // Step 5
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                    // Step 6
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);

                }
            }
            // Step 7

            return d[n, m];
        }
    }

    static class DatabaseConnection
    {
        public static SqlConnection myConn;
        //Get the assembly information

        public static System.Reflection.Assembly assemblyInfo = System.Reflection.Assembly.GetExecutingAssembly();

        //Location is where the assembly is run from 
        public static string assemblyLocation = assemblyInfo.Location;

        //CodeBase is the location of the ClickOnce deployment files
        public static Uri uriCodeBase = new Uri(assemblyInfo.CodeBase);
        public static string ClickOnceLocation = Path.GetDirectoryName(uriCodeBase.LocalPath.ToString());

        /* 
         * Functionality : To initialize connection to the local db file
        */
        public static void InitializeConnection()
        {
            try
            {
                myConn = new SqlConnection(@"Data Source = (LocalDB)\MSSQLLocalDB; AttachDbFilename = '|DataDirectory|\EmailDataset.mdf'; Integrated Security = True");
                myConn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

      /* 
       * Functionality : Close db connection
      */
        public static void CloseDB()
        {
            myConn.Close(); //Close DB connection
        }
    }
}
