using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
            var sqlCommand = new SqlCommand();

            conn.Open();
            Console.WriteLine(Environment.NewLine + conn.State);

            sqlCommand.Connection = conn;
            sqlCommand.CommandText = "SELECT * FROM dbo.ConfigSettings ORDER BY id";

            var settings = new List<Setting>();
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

            string json = JsonConvert.SerializeObject(settings);

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

            // Generate json template for importing config settings
            string tempPath = @$"{Path.GetTempPath()}\ImportConfigSettings.json";
            File.WriteAllText(tempPath, GenerateTemplateSettings());
            
            // Open the json file in the user's default app
            var p = new Process();
            p.StartInfo = new ProcessStartInfo(tempPath)
            {
                UseShellExecute = true
            };
            p.Start();

            // Wait for the user to save the desired settings to import.
            Console.WriteLine("Press [Enter] after you save the desired setting to import.");
            Console.Read();

            string importFile = Regex.Replace(File.ReadAllText(tempPath), @"\t|\n|\r", string.Empty);

            files.Add(GenerateReport(conn));
            files.Add(importFile);

            // Remove temporary file
            File.Delete(tempPath);

            // Merge the historical data with the desired changes in a format for PR.
            var result = JToken.Parse(MergeJsons(files)).ToString(Formatting.Indented);
            Console.WriteLine(result);
        }

        private string GenerateTemplateSettings()
        {
            return JsonConvert.SerializeObject(
                new List<Setting>
                {
                    new Setting
                    {
                       ClientId = string.Empty,
                       ConfigName = string.Empty,
                       ConfigType = string.Empty,
                       ConfigValue = string.Empty
                    }
                }
            );
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
