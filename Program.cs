﻿using Newtonsoft.Json;
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
            using(SqlConnection conn = new SqlConnection()) 
            {
                var configManager = new ConfigurationManager(conn);

                ShowMenu();
                ConsoleKey option = Console.ReadKey().Key;
                
                while(option != ConsoleKey.D4)
                {
                    switch(option)
                    {
                        // Display Historical Config Settings
                        case ConsoleKey.D1:
                            var json = JToken.Parse(configManager.GetHistoricalConfigSettings()).ToString(Formatting.Indented);
                            Console.WriteLine(json);

                            ShowMenu();
                            option = Console.ReadKey().Key;
                            break;

                        // Deploy New Settings
                        case ConsoleKey.D2:
                            configManager.DeployNewSettings();
                            ShowMenu();
                            option = Console.ReadKey().Key;
                            break;

                        // PR New Settings
                        case ConsoleKey.D3:
                            configManager.PullRequestNewConfigSettings();
                            ShowMenu();
                            option = Console.ReadKey().Key;
                            break;

                        // Exit Application
                        case ConsoleKey.D4:
                            break;
                    }
                }
            }
            Console.Read();
        }
        
        public static void ShowMenu()
        {
            var menuOptions = new List<string>
            {
                Environment.NewLine,
                "[1] Generate Config Settings Report",
                "[2] Insert New Setting",
                "[3] PR New Settings",
                "[4] Exit"
            };

            foreach (var item in menuOptions)
            {
                Console.WriteLine(item);
            }
        }
    }

    public class ConfigurationManager
    {
        protected SqlConnection Connection {get; set;}

        public ConfigurationManager(SqlConnection conn)
        {
            Connection = conn;
            Connection.ConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;";
        }

        public string GetHistoricalConfigSettings()
        {
            var sqlCommand = new SqlCommand();

            Connection.Open();
            Console.WriteLine(Environment.NewLine + Connection.State);

            sqlCommand.Connection = Connection;
            sqlCommand.CommandText = "SELECT * FROM dbo.ConfigSettings ORDER BY clientid ASC, SettingName ASC";

            var settings = new List<Setting>();
            SqlDataReader reader = sqlCommand.ExecuteReader();
            while (reader.Read())
            {
                bool isEncrypted;
                bool.TryParse(reader[5].ToString(), out isEncrypted);

                var setting = new Setting
                {
                    ClientId = reader[1].ToString(),
                    SettingName = reader[2].ToString(),
                    SettingType = reader[3].ToString(),
                    SettingValue = isEncrypted ? Security.EncryptString(reader[4].ToString()) : reader[4].ToString(),
                    SettingLevel = reader[6].ToString(),
                    IsEncrypted = isEncrypted
                };

                settings.Add(setting);
            }

            string json = JsonConvert.SerializeObject(settings);

            Connection.Close();
            Console.WriteLine(Connection.State);

            return json;
        }

        public void DeployNewSettings()
        {
            Connection.Open();
            Console.WriteLine(Environment.NewLine + Connection.State);

            var importSettings = JsonConvert.DeserializeObject<List<Setting>>(GenerateImportSettings());

            foreach(var item in importSettings)
            {
                var setting = new Setting
                {
                    ClientId = item.ClientId ?? string.Empty,
                    SettingName = item.SettingName ?? string.Empty,
                    SettingType = item.SettingType ?? string.Empty,
                    SettingValue = item.SettingValue ?? string.Empty,
                    IsEncrypted = item.IsEncrypted
                };

                SqlCommand cmd  = new SqlCommand("dbo.uspInsertSettingDefinition", Connection) 
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.Add(new SqlParameter("@ClientId", setting.ClientId));
                cmd.Parameters.Add(new SqlParameter("@SettingLevel", setting.ClientId));
                cmd.Parameters.Add(new SqlParameter("@SettingName", setting.SettingName));
                cmd.Parameters.Add(new SqlParameter("@SettingType", setting.SettingType));
                cmd.Parameters.Add(new SqlParameter("@SettingValue", setting.SettingValue));
                cmd.Parameters.Add(new SqlParameter("@IsEncrypted", setting.IsEncrypted));
                cmd.ExecuteNonQuery();
            }

            Connection.Close();
            Console.WriteLine(Connection.State);
        }

        public void PullRequestNewConfigSettings()
        {
            var files = new List<string>();
            string importFile = GenerateImportSettings();

            files.Add(GetHistoricalConfigSettings());
            files.Add(importFile);
            
            // Merge the historical data with the desired changes in a format for PR.
            var result = JToken.Parse(MergeJsons(files)).ToString(Formatting.Indented);
            Console.WriteLine(result);
        }

        private string GenerateImportSettings()
        {
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
            Console.WriteLine(Environment.NewLine + "Press [Enter] after you save the desired setting to import.");
            Console.Read();

            // Remove formatting from file
            var result = Regex.Replace(File.ReadAllText(tempPath), @"\t|\n|\r", string.Empty);

            // Remove temporary file
            File.Delete(tempPath);

            return result;
        }

        private string GenerateTemplateSettings()
        {
            return JsonConvert.SerializeObject(
                new List<Setting>
                {
                    new Setting
                    {
                       ClientId = string.Empty,
                       SettingLevel = string.Empty,
                       SettingName = string.Empty,
                       SettingType = string.Empty,
                       SettingValue = string.Empty,
                       IsEncrypted = false
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
    }

    public static class Security
    {
        public static string DecryptString(string encryptString)  
        {  
            byte[] b;  
            string decrypted;  
            try  
            {  
                b = Convert.FromBase64String(encryptString);  
                decrypted = System.Text.ASCIIEncoding.ASCII.GetString(b);  
            }  
            catch (FormatException fe)   
            {  
                decrypted = string.Empty;  
            }  

            return decrypted;  
        }  
  
        public static string EncryptString(string encryptedString)   
        {  
            byte[] b = System.Text.ASCIIEncoding.ASCII.GetBytes(encryptedString);  
            string encrypted = Convert.ToBase64String(b);  
            return encrypted;  
        }
    }

    class Setting
    {
        public string ClientId {get; set;}
        public string SettingLevel {get; set;}
        public string SettingName {get; set;}
        public string SettingType {get; set;}
        public string SettingValue {get; set;}
        public bool IsEncrypted {get; set;}
    }
}
