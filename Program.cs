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
            var menu = new Menu();
            // Menu
            menu.ShowMenu();

            using(SqlConnection conn = new SqlConnection()) 
            {
                conn.ConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;";

                ConsoleKey option = Console.ReadKey().Key;

                while(option != ConsoleKey.D4)
                {
                    switch(option)
                    {
                        case ConsoleKey.D1:
                            menu.GenerateReport(conn);
                            menu.ShowMenu();
                            option = Console.ReadKey().Key;
                            break;
                        case ConsoleKey.D2:
                            menu.InsertNewSettings(conn);
                            menu.ShowMenu();
                            option = Console.ReadKey().Key;
                            break;
                        case ConsoleKey.D3:
                            menu.ShowMenu();
                            option = Console.ReadKey().Key;
                            break;
                        case ConsoleKey.D4:
                            break;
                    }
                }

                Console.Read();
            }
        } 
    }

    public class Menu
    {
        public void ShowMenu()
        {
            foreach (var item in MenuOptions)
            {
                Console.WriteLine(item);
            }
        }

        public enum Options
        {
            GenerateReport = 1,
            InsertNewSetting = 2,
            PR = 3,
            Exit = 4
        };

        public List<string> MenuOptions {get; } = new List<string>
        {
            Environment.NewLine,
            "[1] Generate Config Settings Report",
            "[2] Insert New Setting",
            "[3] PR New Settings",
            "[4] Exit"
        };

        public void GenerateReport(SqlConnection conn)
        {
            var settings = new List<Setting>();
            var sqlCommand = new SqlCommand();

            conn.Open();
            Console.WriteLine(conn.State);

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
        }

        public void InsertNewSettings(SqlConnection conn)
        {
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

            conn.Close();
            Console.WriteLine(conn.State);
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
