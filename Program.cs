using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ConfigManager
{
    class Program
    {
        static void Main(string[] args)
        {
            using(SqlConnection conn = new SqlConnection()) 
            {
                conn.ConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;";
                conn.Open();
                Console.WriteLine(conn.State);

                var setting = new Setting
                {
                    ClientId = "BCBSID",
                    ConfigName = "enhance_cost",
                    ConfigType = "bool",
                    ConfigValue = "false"
                };

                SqlCommand cmd  = new SqlCommand("dbo.uspInsertSettingDefinition", conn) 
                {
                    CommandType = CommandType.StoredProcedure
                };
                
                cmd.Parameters.Add(new SqlParameter("@ClientId", setting.ClientId));
                cmd.Parameters.Add(new SqlParameter("@ConfigName", setting.ConfigName));
                cmd.Parameters.Add(new SqlParameter("@ConfigType", setting.ConfigType));
                cmd.Parameters.Add(new SqlParameter("@ConfigValue", setting.ConfigValue));
                cmd.ExecuteNonQuery();

                var settings = new List<Setting>();
                var sqlCommand = new SqlCommand();
                sqlCommand.Connection = conn;
                sqlCommand.CommandText = "SELECT * FROM dbo.ConfigSettings ORDER BY id";
                SqlDataReader reader = sqlCommand.ExecuteReader();
                while (reader.Read())
                {
                    var someComplexItem = new Setting();
                    someComplexItem.ClientId = reader[1].ToString();
                    someComplexItem.ConfigName = reader[2].ToString();
                    someComplexItem.ConfigType = reader[3].ToString();
                    someComplexItem.ConfigValue = reader[4].ToString();

                    settings.Add(someComplexItem);
                }

                var json = JsonConvert.SerializeObject(settings);

                Console.WriteLine(json);

                conn.Close();
                Console.WriteLine(conn.State);
                Console.Read();
            }
        }
    }

    class Setting
    {
        public string ClientId {get; set;}
        public string ConfigName {get; set;}
        public string ConfigType {get; set;}
        public string ConfigValue {get; set;}
    }
}
