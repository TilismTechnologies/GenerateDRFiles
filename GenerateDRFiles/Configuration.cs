using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GenerateDRFiles
{
    
    class Configuration
    {
        private string _EngineName = string.Empty;
        private string _SetupConnectionString = string.Empty;
        private string _MonConnectionString = string.Empty;
        private string _RefreshTime = string.Empty;
        private string _TopRecord = string.Empty;
        private string _GenrateLogPath = string.Empty;
        private string _AppStartTime = string.Empty;
        private string _AppEndTime = string.Empty;
        private string _FilePath = string.Empty;
        private string _UseThread = string.Empty;


        public Configuration()
        {
            string ConfigFile = Application.StartupPath + "\\" + "Config.xml";
            DataSet dsConfig = new DataSet();
            if (System.IO.File.Exists(ConfigFile))
            {
                dsConfig.ReadXml(ConfigFile);
            }

            _EngineName = dsConfig.Tables.Contains("EngineName") ? dsConfig.Tables["EngineName"].Rows[0]["Value"].ToString() : string.Empty;
            _SetupConnectionString = dsConfig.Tables.Contains("SetupConnectionString") ? dsConfig.Tables["SetupConnectionString"].Rows[0]["Value"].ToString() : string.Empty;
            _MonConnectionString = dsConfig.Tables.Contains("MonConnectionString") ? dsConfig.Tables["MonConnectionString"].Rows[0]["Value"].ToString() : string.Empty;
            _RefreshTime = dsConfig.Tables.Contains("RefreshTime") ? dsConfig.Tables["RefreshTime"].Rows[0]["Value"].ToString() : string.Empty;
            _TopRecord = dsConfig.Tables.Contains("TopRecord") ? dsConfig.Tables["TopRecord"].Rows[0]["Value"].ToString() : string.Empty;
            _GenrateLogPath = dsConfig.Tables.Contains("GenrateLogPath") ? dsConfig.Tables["GenrateLogPath"].Rows[0]["Value"].ToString() : string.Empty;

            _AppStartTime = dsConfig.Tables.Contains("AppStartTime") ? dsConfig.Tables["AppStartTime"].Rows[0]["Value"].ToString() : string.Empty;
            _AppEndTime = dsConfig.Tables.Contains("AppEndTime") ? dsConfig.Tables["AppEndTime"].Rows[0]["Value"].ToString() : string.Empty;
            _FilePath = dsConfig.Tables.Contains("FilePath") ? dsConfig.Tables["FilePath"].Rows[0]["Value"].ToString() : string.Empty;
            _UseThread = dsConfig.Tables.Contains("UseThread") ? dsConfig.Tables["UseThread"].Rows[0]["Value"].ToString() : string.Empty;

        }

        public string EngineName { get { return _EngineName; } }
        public string SetupConnectionString { get { return _SetupConnectionString; } }
        public string MonConnectionString { get { return _MonConnectionString; } }
        public string RefreshTime { get { return _RefreshTime; } }
        public string TopRecord { get { return _TopRecord; } }
        public string GenrateLogPath { get { return _GenrateLogPath; } }
        public string FilePath { get { return _FilePath; } }
        public string UseThread { get { return _UseThread; } }
        public string AppStartTime { get { return _AppStartTime; } }
        public string AppEndTime { get { return _AppEndTime; } }
    }
}
