using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace ConfigManager
{

    class Program
    {
       
        static void Main(string[] args)
        {
            var menu = new Menu();
            menu.ShowMenu();
            ConsoleKey option = Console.ReadKey().Key;

            using(SqlConnection conn = new SqlConnection()) 
            {
                conn.ConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;";
                
                while(option != ConsoleKey.D4)
                {
                    switch(option)
                    {
                        case ConsoleKey.D1:
                            var json = menu.GenerateReport(conn);
                            Console.WriteLine(json);

                            menu.ShowMenu();
                            option = Console.ReadKey().Key;
                            break;

                        case ConsoleKey.D2:
                            menu.InsertNewSetting(conn);
                            menu.ShowMenu();
                            option = Console.ReadKey().Key;
                            break;

                        case ConsoleKey.D3:
                            menu.PullRequestNewConfigSettings(conn);
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

        public string GenerateReport(SqlConnection conn)
        {
            var settings = new List<Setting>();
            var sqlCommand = new SqlCommand();

            conn.Open();
            Console.WriteLine(Environment.NewLine + conn.State);

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

            string json = JToken.Parse(JsonConvert.SerializeObject(settings)).ToString(Formatting.Indented);

            conn.Close();
            Console.WriteLine(conn.State);

            return json;
        }

        public void InsertNewSetting(SqlConnection conn)
        {
            conn.Open();
            Console.WriteLine(Environment.NewLine + conn.State);

            Console.WriteLine("Which client?");
            var clientId = Console.ReadLine();

            Console.WriteLine("What's the config setting name?");
            var configName = Console.ReadLine();

            Console.WriteLine("What's the config setting type?");
            var configType = Console.ReadLine();

            Console.WriteLine("What's the config value?");
            var configValue = Console.ReadLine();

            var setting = new Setting
            {
                ClientId = clientId,
                ConfigName = configName,
                ConfigType = configType,
                ConfigValue = configValue
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

        public void PullRequestNewConfigSettings(SqlConnection conn)
        {
            var files = new List<string>();
            files.Add(GenerateReport(conn));

            Console.WriteLine("What's the file path to the settings you want to add?");
            //string settingsFile = Console.ReadLine();

            var importFile = System.IO.File.ReadAllText(@"C:\Users\kodyb\Documents\WriteText.json");
            files.Add(importFile);

            var result = MergeJsons(files);

            Console.WriteLine(result);

        }

        private string RemoveBrackets(string content)
        {
            var openB = content.IndexOf("[");
            content = content.Substring(openB + 1, content.Length - openB - 1);

            var closeB = content.LastIndexOf("]");
            content = content.Substring(0, closeB);

            return content;
        }

        private string MergeJsons(List<string> jsons)
        {
            var sb = new StringBuilder();

            sb.AppendLine("[");     
            for(var i=0; i<jsons.Count; i++)   
            {
                var json = jsons[i];
                var cleared = RemoveBrackets(json);
                sb.AppendLine(cleared);
                if (i != jsons.Count-1) sb.Append(",");
            }

            sb.AppendLine("]");     
            return sb.ToString();
        }

        public bool EncryptSettings()
        {
            return true;
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
