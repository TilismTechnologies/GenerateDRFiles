using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenerateDRFiles
{
    class DAL
    {
        private string SetupConnectionString;

        public DAL(string connectionString)
        {
            this.SetupConnectionString = connectionString;
            
        }

        public DataTable ExecuteDataTable(string query)
        {
            using (SqlConnection conn = new SqlConnection(SetupConnectionString))
            {
                
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandTimeout = 100;
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        public int ExecuteNonQuery(string query)
        {
            using (SqlConnection conn = new SqlConnection(SetupConnectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        public DataTable doSelect(string strQry, string StrConnectionString)
        {
            SqlConnection conn = new SqlConnection(StrConnectionString);
            SqlDataAdapter da = new SqlDataAdapter(strQry, conn);
            DataTable dt = new DataTable();
            try
            {
                conn.Open();
                da.Fill(dt);
                conn.Close();
                return dt;
            }
            catch (Exception ex)
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
                throw new Exception(ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
            }
        }

        public void doExecute(string strQry, string StrConnectionString)
        {
            SqlConnection conn = new SqlConnection(StrConnectionString);
            SqlCommand cmd = new SqlCommand(strQry, conn);
            try
            {
                conn.Open();
                cmd.ExecuteNonQuery();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
                throw new Exception(ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
            }

        }

        public string doExecuteScalar(string strQry, string StrConnectionString)
        {
            string res;
            SqlConnection conn = new SqlConnection(StrConnectionString);
            SqlCommand cmd = new SqlCommand(strQry, conn);

            try
            {
                conn.Open();
                res = Convert.ToString(cmd.ExecuteScalar());
                conn.Close();
                return res;
            }
            catch (Exception ex)
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
                throw new Exception(ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
            }

        }
    }
}
