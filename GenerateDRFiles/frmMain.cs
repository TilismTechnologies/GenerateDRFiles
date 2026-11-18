using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace GenerateDRFiles
{
    public partial class frmMain : Form
    {
        Configuration AppConfig;
        Stopwatch objStopWatch;
        private TimeSpan _AppStartTime;
        private TimeSpan _AppEndTime;
        Thread ThreadProcessGenerateDRFile;
        Thread ThreadProcessBalanceUpdate;

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                MessageBox.Show("Another Instance of this application is already running", "Multiple Instances are Forbidden", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                Application.Exit();
            }

            btnStart.Enabled = true;
            btnExit.Enabled = true;
            btnConfiguration.Enabled = true;
            btnStop.Enabled = false;

            tmrProcess.Enabled = false;
            tmrUpdateStatus.Enabled = false;

            AppConfig = new Configuration();
            this.Text = AppConfig.EngineName;
            lblAppName.Text = AppConfig.EngineName;

            if (string.IsNullOrEmpty(tbStartTime.Text))
            {
                tbStartTime.Text = DateTime.Now.ToString("dd-MMM-yyyy hh:mm:ss tt");
            }

            if (string.IsNullOrEmpty(tbTimeElapsed.Text))
            {
                tbTimeElapsed.Text = "0 : 00 : 00";
            }

            if (string.IsNullOrEmpty(tbTransactions.Text))
            {
                tbTransactions.Text = "0";
            }

            tmrUpdateStatus.Interval = 10000;
            tmrTime.Interval =  1000;
            objStopWatch = new Stopwatch();
            ThreadProcessGenerateDRFile = new Thread(ProcessGenerateDRFile);
            ThreadProcessBalanceUpdate = new Thread(ProcessUpdateBalance);


            try
            {
                _AppStartTime = TimeSpan.Parse(AppConfig.AppStartTime);
                _AppEndTime = TimeSpan.Parse(AppConfig.AppEndTime);
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrorList, $"Time Parsing Error: {ex.Message}");
                CreateLog($"Time Parsing Error: {ex.Message}", "TimeParseError", AppConfig.GenrateLogPath);
            }
        }

        private  void tmrProcess_Tick(object sender, EventArgs e)
        {
            try
            {

                TimeSpan currentTime = DateTime.Now.TimeOfDay;
                if (currentTime >= _AppStartTime && currentTime <= _AppEndTime)
                {
                    if (ThreadProcessGenerateDRFile.IsAlive == false)
                    {
                        ThreadProcessGenerateDRFile = new Thread(ProcessGenerateDRFile);
                        ThreadProcessGenerateDRFile.IsBackground = false;
                        ThreadProcessGenerateDRFile.Start();
                    }
                }

                if (ThreadProcessBalanceUpdate.IsAlive == false)
                {
                    ThreadProcessBalanceUpdate = new Thread(ProcessUpdateBalance);
                    ThreadProcessBalanceUpdate.IsBackground = false;
                    ThreadProcessBalanceUpdate.Start();
                }

            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrorList, $"Error in Timer Tick: {ex.Message}");
                CreateLog($"Error in Timer Tick: {ex.Message}", "TimerError", AppConfig.GenrateLogPath);
            }
        }
     
        public void ProcessUpdateBalance()
        {
            string SQL = string.Empty;
            DataTable BalanceDT = new DataTable();

            string balanceId = string.Empty;
            string balanceSystemId = string.Empty;
            string balanceSystemPass = string.Empty;
            string _URL = string.Empty;
            string _Response = string.Empty;
            string _balance1 = string.Empty;
            string _balance2 = string.Empty;
            try
            {
                DAL balanceDAL = new DAL(AppConfig.SetupConnectionString);

                SQL = "SELECT id, SMPPSystemId, SMPPPass " +
                    "FROM MonitoringDB..SMPPBalance " +
                    " where Telco = 'Mobilink' " +
                    " and active = 'Y' " +
                    " and DATEDIFF(MINUTE, LastUpdate, getdate()) > 10";
                BalanceDT = balanceDAL.ExecuteDataTable(SQL);

                foreach (DataRow bulkMsgRow in BalanceDT.Rows)
                {
                    balanceId = bulkMsgRow["id"].ToString();
                    balanceSystemId = bulkMsgRow["SMPPSystemId"].ToString();
                    balanceSystemPass = bulkMsgRow["SMPPPass"].ToString();

                    _URL = string.Empty;
                    _Response = string.Empty;
                    _balance1 = "0";
                    _balance2 = "0";

                    _URL = string.Format("https://connect.jazzcmt.com/request_sms_check.html?Username={0}&Password={1}", balanceSystemId, balanceSystemPass);
                    using (WebClient client = new WebClient())
                    {
                        _Response = client.DownloadString(_URL);
                    }
                    if (_Response.Length > 0)
                    {
                        if (_Response.Contains(","))
                        {
                            string[] aMTBalance = _Response.Split(',');
                            _balance1 = aMTBalance[0];
                            _balance2 = aMTBalance[1];
                        }
                        else
                        {
                            _balance1 = _Response;
                        }
                    }
                    ShowActivity(ActivityType.AddTransactionList, string.Format("Update Balance for : {0} | Balance1: {1} | Balance1: {2}", balanceSystemId, _balance1, _balance2));
                    SQL = string.Format(" update MonitoringDB..SMPPBalance set Balance1 = '{0}', Balance2 = '{1}', LastUpdate = getdate() where id = '{2}'",_balance1, _balance2, balanceId);
                    balanceDAL.doExecute(SQL, AppConfig.SetupConnectionString);
                }

            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrorList, string.Format("[ProcessUpdateBalance]: {0}", ex.Message));
                CreateLog(string.Format("Error in ProcessUpdateBalance: {0}", ex.Message), "UpdateBalanceErr", AppConfig.GenrateLogPath);
            }
        }
        public static string GetFolderName()
        {
            return string.Format("DL_{0}", DateTime.Now.ToString("yyyyMMM"));
        }

        private void ProcessGenerateDRFile()
        {
            string SQL = string.Empty;
            DataTable bulkMessagesDT = new DataTable();

            string FileId = string.Empty;
            string SCode = string.Empty;
            string FileName = string.Empty;
            string SaveFileName = string.Empty;
            string FilePath = AppConfig.FilePath;
            string FolderName = GetFolderName();
            string CompleteFilePath = Path.Combine(FilePath, FolderName);

            try
            {
                DAL setupDAL = new DAL(AppConfig.SetupConnectionString);

                SQL = "SELECT TOP " + AppConfig.TopRecord + " id, SCode, aDate FROM tblSMS_BulkMessages " +
                      "WHERE aDate >= '21-Sep-2026' AND Approved = 'Y' AND sent = 'Y' " +
                      "AND (DRMediaFileHex IS NULL OR DRMediaFileHex = '') and scode in ('14250', '14251', '14252', '1722', '17267', '18257', '18258', '18259', '8042', '8287') ORDER BY id";
                bulkMessagesDT = setupDAL.ExecuteDataTable(SQL);

                if (bulkMessagesDT.Rows.Count == 0) return;

                if (!Directory.Exists(CompleteFilePath))
                {
                    Directory.CreateDirectory(CompleteFilePath);
                }

                foreach (DataRow bulkMsgRow in bulkMessagesDT.Rows)
                {
                    FileId = bulkMsgRow["id"].ToString();
                    SCode = bulkMsgRow["SCode"].ToString();

                    // Skip processing if MT messages are still pending for this file
                    if (IsPendingMessage(FileId))
                    {
                        continue;
                    }

                    ShowActivity(ActivityType.AddTransactionList, "-----------------DR File Generation------------------------");
                    ShowActivity(ActivityType.AddTransactionList, string.Format("Processing DR for FileId: {0} | ShortCode: {1}", FileId, SCode));

                    // Step 2: Fetch the specific client DB setup matching this SCode from TLSMSetup
                    SQL = string.Format("SELECT DISTINCT ConnStr, InTable FROM TLSMSetup..tbl_DeliverySetup WHERE SCode = '{0}' AND ISNULL(ConnStr, '') <> ''", SCode);
                    DataTable clientSetupDT = setupDAL.ExecuteDataTable(SQL);

                    if (clientSetupDT.Rows.Count == 0)
                    {
                        ShowActivity(ActivityType.AddErrorList, string.Format("[ProcessGenerateDRFile]: No DR setup mapping found for SCode [{0}] in tbl_DeliverySetup.", SCode));
                        CreateLog(string.Format("No DR setup mapping found for SCode [{0}] in tbl_DeliverySetup for FileId: {1}", SCode, FileId), "GenerateDRErr", AppConfig.GenrateLogPath);
                        continue;
                    }

                    DataTable consolidatedDRTable = new DataTable();

                    // Step 3: Query the specific client DB table(s) configured for this Short Code
                    foreach (DataRow setupRow in clientSetupDT.Rows)
                    {
                        string clientConnStr = setupRow["ConnStr"].ToString();
                        string drTableName = setupRow["InTable"].ToString();

                        try
                        {
                            DAL clientDAL = new DAL(clientConnStr);

                            // Fetch DR records matching FileId from the target client DR table
                            string drQuery = string.Format(
                                "SELECT aDate as Date, Mask, Telco, Mobile, Scode, MsgId, dDate, dstatus as DeliveryStatus, FileId, sms " +
                                "FROM {0} WHERE FileId = '{1}' ORDER BY id",
                                drTableName, FileId);

                            DataTable clientDRTable = clientDAL.ExecuteDataTable(drQuery);

                            if (clientDRTable != null && clientDRTable.Rows.Count > 0)
                            {
                                if (consolidatedDRTable.Columns.Count == 0)
                                {
                                    consolidatedDRTable = clientDRTable.Clone(); // Copy structure
                                }

                                foreach (DataRow drRow in clientDRTable.Rows)
                                {
                                    consolidatedDRTable.ImportRow(drRow);
                                }
                            }
                        }
                        catch (Exception clientEx)
                        {
                            CreateLog(string.Format("Error querying DR Table [{0}] on ConnStr [{1}] for SCode [{2}]: {3}", drTableName, clientConnStr, SCode, clientEx.Message), "GenerateDRErr", AppConfig.GenrateLogPath);
                        }
                    }

                    // Step 4: Export CSV log file
                    if (consolidatedDRTable.Rows.Count == 0)
                    {
                        SaveFileName = "0";
                    }
                    else
                    {
                        FileName = string.Format("DR_{0}.csv", FileId);
                        SaveFileName = string.Format("/{0}/{1}", FolderName, FileName);

                        string csvData = ConvertDataTableToCSV(consolidatedDRTable);
                        string fullPath = Path.Combine(CompleteFilePath, FileName);

                        File.WriteAllText(fullPath, csvData);

                        ShowActivity(ActivityType.AddTransactionList, string.Format("DR File generated: {0}", fullPath));
                        CreateLog(string.Format("DR File generated: {0} (Records: {1})", fullPath, consolidatedDRTable.Rows.Count), "GenerateDRFile", AppConfig.GenrateLogPath);
                    }

                    // Step 5: Update tblSMS_BulkMessages with the generated file path
                    SQL = string.Format("UPDATE tblSMS_BulkMessages SET DRMediaFileHex = '{0}' WHERE id = {1}", SaveFileName, FileId);
                    setupDAL.ExecuteNonQuery(SQL);

                    ShowActivity(ActivityType.AddTransactions, "1");
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrorList, string.Format("[ProcessGenerateDRFile]: {0}", ex.Message));
                CreateLog(string.Format("Error in ProcessGenerateDRFile: {0}", ex.Message), "GenerateDRErr", AppConfig.GenrateLogPath);
            }
        }
        private string ConvertDataTableToCSV(DataTable dt)
        {

            StringBuilder csvData = new StringBuilder();
            IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
            csvData.AppendLine(string.Join(",", columnNames));


            foreach (DataRow row in dt.Rows)
            {
                //IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString().Replace(",", " "));
                IEnumerable<string> fields = row.ItemArray.Select(field => string.Concat("\"", field.ToString().Replace("\"", "\"\""), "\"").Replace(",", " ").Replace("[Char44]", "'"));
                csvData.AppendLine(string.Join(",", fields));
            }

            return csvData.ToString();
        }

        private Boolean IsPendingMessage(string FileId)
        {
            string SQL = string.Empty;
            DataTable pmDT = new DataTable();
            try
            {
                DAL objDAL = new DAL(AppConfig.SetupConnectionString);
                SQL = "SELECT TOP 1 * FROM  tblSMS_MPortal " +
                    "WHERE FileId = '" + FileId + "' ";
                pmDT = objDAL.ExecuteDataTable(SQL);
                if (pmDT.Rows.Count > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrorList, string.Format("[CheckPendingMessage]: {0}", ex.Message));
                CreateLog(string.Format("{0}", "----------------------------------------------------"), "GenerateFileErr", AppConfig.GenrateLogPath);
                CreateLog(string.Format("Process   : CheckPendingMessage"), "GenerateFileErr", AppConfig.GenrateLogPath);
                CreateLog(string.Format("Error   : {0}", ex.Message), "GenerateFileErr", AppConfig.GenrateLogPath);
                return false;
            }
        }
        private string EscapeXml(string s)
        {
            string toxml = s;
            if (!string.IsNullOrEmpty(toxml))
            {
                // replace literal values with entities
                toxml = toxml.Replace("&", "&amp;");
                toxml = toxml.Replace("'", "&apos;");
                toxml = toxml.Replace("\"", "&quot;");
                toxml = toxml.Replace(">", "&gt;");
                toxml = toxml.Replace("<", "&lt;");
            }
            return toxml;
        }

        private string UnescapeXml(string s)
        {
            string unxml = s;
            if (!string.IsNullOrEmpty(unxml))
            {
                // replace entities with literal values
                unxml = unxml.Replace("&apos;", "'");
                unxml = unxml.Replace("&quot;", "\"");
                unxml = unxml.Replace("&gt;", ">");
                unxml = unxml.Replace("&lt;", "<");
                unxml = unxml.Replace("&amp;", "&");
            }
            return unxml;
        }

        public string HexToStr(string aStr, int ChrLen)
        {
            string mHex = "";
            string subHex = "";

            try
            {
                for (int i = 0; i < aStr.Length; i += ChrLen)
                {
                    subHex = aStr.Substring(i, ChrLen);
                    if (subHex != "00")
                    {
                        subHex = HexToChr(subHex);
                        mHex += subHex;
                    }
                }
                return mHex;
            }
            catch (Exception)
            {
                return "";
            }
        }
        public string HexToChr(string aHex)
        {
            try
            {
                return char.ConvertFromUtf32(Convert.ToInt32(aHex.Trim(), 16));
            }
            catch (Exception)
            {
                return "";
            }
        }
       
        #region Button and Timer Processes
        private void btnStart_Click(object sender, EventArgs e)
        {

            btnStart.Enabled = false;
            btnExit.Enabled = false;
            btnConfiguration.Enabled = false;
            btnStop.Enabled = true;
            
            if (!string.IsNullOrEmpty(AppConfig.RefreshTime))
            { tmrProcess.Interval = Convert.ToInt16(AppConfig.RefreshTime) * 1000; }
            else
            { tmrProcess.Interval = 1000; }

            tmrProcess.Enabled = true;
            tmrUpdateStatus.Enabled = true;
            tmrTime.Enabled = true;
            objStopWatch.Start();
            ShowActivity(ActivityType.AddTransactionList, "Application Started");
        }

        private void StopService()
        {
            tmrProcess.Enabled = false;
            tmrUpdateStatus.Enabled = false;
            tmrTime.Enabled = false;

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnConfiguration.Enabled = true;
            btnExit.Enabled = true;
            objStopWatch.Stop();
            ShowActivity(ActivityType.AddTransactionList, "Application Stopped.");
            
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            StopService();   
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            DialogResult a = MessageBox.Show("Are you sure you wish to exit?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (a == DialogResult.Yes)
                this.Close();
            else return;
        }

        private void btnConfiguration_Click(object sender, EventArgs e)
        {
            frmConfig objConfig = new frmConfig();
            objConfig.ShowDialog();
            objConfig = null;

            AppConfig = new Configuration();
            this.Text = AppConfig.EngineName;
            lblAppName.Text = AppConfig.EngineName;
            _AppStartTime = TimeSpan.Parse(AppConfig.AppStartTime);
            _AppEndTime = TimeSpan.Parse(AppConfig.AppEndTime);
        }

        private void tmrTime_Tick(object sender, EventArgs e)
        {
            tbTimeElapsed.Text = objStopWatch.Elapsed.ToString().Remove(8);
        }

        private void tmrUpdateStatus_Tick(object sender, EventArgs e)
        {
            try
            {
                DAL objDal = new DAL(AppConfig.MonConnectionString); // Pass the connection string here
                objDal.doExecute("Exec sp_Mon_AppStatus '" + AppConfig.EngineName + "'", AppConfig.MonConnectionString);
                if (!tmrProcess.Enabled)
                {
                    CreateLog("tmrProcess was disabled. Restarting it.", "TimerRestart", AppConfig.GenrateLogPath);
                    tmrProcess.Enabled = true;
                }

            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrorList, ex.Message);
                CreateLog($"Error in Status Update: {ex.Message}", "UpdateStatusError", AppConfig.GenrateLogPath);
            }
        }

        #endregion

        #region Activity

        private Boolean IsNumeric(String value)
        {
            return value.All(Char.IsDigit);
        }

        private enum ActivityType
        {
            AddTransactionList,
            AddErrorList,
            AddTransactions,
        }

        delegate void SetListBox(ListBox _ListBoxe, string Data);
        delegate void SetTextBox(TextBox objTextBox);

        private void ShowActivity(ActivityType _ActivityType, string Data)
        {
            if (string.IsNullOrEmpty(Data)) return;

            if (_ActivityType == ActivityType.AddTransactionList)
            {
                if (lbTransactions.InvokeRequired)
                {
                    SetListBox d = new SetListBox(AddItem);
                    this.Invoke(d, new object[] { lbTransactions, Data });
                }
                else
                {
                    AddItem(lbTransactions, Data);
                }
            }
            else if (_ActivityType == ActivityType.AddErrorList)
            {
                if (lbError.InvokeRequired)
                {
                    SetListBox d = new SetListBox(AddItem);
                    this.Invoke(d, new object[] { lbError, Data });
                }
                else
                {
                    AddItem(lbError, Data);
                }
            }
            else if (_ActivityType == ActivityType.AddTransactions)
            {
                if (tbTransactions.InvokeRequired)
                {
                    SetTextBox t = new SetTextBox(UpdateCount);
                    this.Invoke(t, new object[] { tbTransactions });
                }
                else
                {
                    UpdateCount(tbTransactions);
                }
            }
        }

        private void AddItem(ListBox objListBox, string Data)
        {
            if (objListBox.Items.Count > 100)
            { objListBox.Items.Clear(); }

            objListBox.Items.Add(string.Format("{0} - {1}", DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss"), Data));
            objListBox.SelectedIndex = objListBox.Items.Count - 1;
        }

        private void UpdateCount(TextBox objTextBox)
        {
            double TotalCount = double.Parse(objTextBox.Text) + 1;
            objTextBox.Text = TotalCount.ToString();
            objTextBox.Refresh();
        }

        public void CreateLog(string LogData, string sService, string sLogPath)
        {
            string FileName = sService + "_" + DateTime.Now.ToString("yyyyMMMdd") + ".txt";
            try
            {
                if (!Directory.Exists(sLogPath))
                {
                    Directory.CreateDirectory(sLogPath);
                }
                if (!File.Exists(sLogPath + FileName))
                {
                    StreamWriter swc = File.CreateText(sLogPath + FileName);
                    swc.Close();
                    swc.Dispose();
                }
                StreamWriter sw = File.AppendText(sLogPath + FileName);
                sw.WriteLine(DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "|" + LogData);
                sw.Close();
                sw.Dispose();
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrorList, ex.Message);
            }
        }


        private void lbTransactions_DoubleClick(object sender, EventArgs e)
        {

            if (!string.IsNullOrEmpty(lbTransactions.SelectedItem.ToString()))
            {
                frmDescription objFrmDescription = new frmDescription();
                objFrmDescription.tbDescription.Text = lbTransactions.SelectedItem.ToString();
                objFrmDescription.tbDescription.ForeColor = Color.Black;
                objFrmDescription.ShowDialog();

            }
        }

        private void lbError_DoubleClick(object sender, EventArgs e)
        {

            if (!string.IsNullOrEmpty(lbError.SelectedItem.ToString()))
            {
                frmDescription objFrmDescription = new frmDescription();
                objFrmDescription.tbDescription.Text = lbError.SelectedItem.ToString();
                objFrmDescription.tbDescription.ForeColor = Color.Red;
                objFrmDescription.ShowDialog();

            }
        }







        #endregion Activity
    }
}
