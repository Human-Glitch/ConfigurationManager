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
            using(SqlConnection conn = new SqlConnection()) 
            {
                var configManager = new ConfigurationManager(conn);

                ShowMenu();
                ConsoleKey option = Console.ReadKey().Key;
                
                while(option != ConsoleKey.D5)
                {
                    switch(option)
                    {
                        // Display Database Config Settings
                        case ConsoleKey.D1:
                            var json = JToken.Parse(configManager.GetDatabaseConfigSettings()).ToString(Formatting.Indented);
                            Console.WriteLine(json);

                            ShowMenu();
                            option = Console.ReadKey().Key;
                            break;

                        // PR New Settings
                        case ConsoleKey.D2:
                            configManager.GeneratePullRequestFile();
                            ShowMenu();
                            option = Console.ReadKey().Key;
                            break;

                        // Add New Settings
                        case ConsoleKey.D3:
                            configManager.AddNewSettings();
                            ShowMenu();
                            option = Console.ReadKey().Key;
                            break;

                        // Import New Settings
                        case ConsoleKey.D4:
                            configManager.ImportNewSettings();
                            ShowMenu();
                            option = Console.ReadKey().Key;
                            break;

                        // Exit Application
                        case ConsoleKey.D5:
                            break;

                        default:
                            option = Console.ReadKey().Key;
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
                "[1] Display Database Config Settings",
                "[2] Generate New Settings for PR",
                "[3] Manually Add New Settings",
                "[4] Import New Settings",
                "[5] Exit"
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

        public string GetDatabaseConfigSettings()
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

        public void AddNewSettings()
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

        public void ImportNewSettings()
        {
            try
            {
                Console.WriteLine(Environment.NewLine + "Enter a file path for the desired import settings.");
                var filePath = Console.ReadLine();
                var importSettingsJson = File.ReadAllText(filePath);
                var databaseSettingsJson = GetDatabaseConfigSettings();

                var databaseSettingsJsonModel = (JArray)JsonConvert.DeserializeObject(databaseSettingsJson);
                var importSettingsJsonModel = (JArray)JsonConvert.DeserializeObject(importSettingsJson);

                var historicalSettings = databaseSettingsJsonModel.Children().OrderBy(x => x["clientId"]).ToList();
                var importSettings = importSettingsJsonModel.Children().OrderBy(x => x["clientId"]).ToList();

                JArray modifySettingList = new JArray();
                foreach(var setting in importSettings)
                {
                    if(historicalSettings.Any(x => JToken.EqualityComparer.Equals(x, setting)))
                    {
                        continue;
                    }

                    modifySettingList.Add(setting);
                }

                var settings = JsonConvert.DeserializeObject<List<Setting>>(JsonConvert.SerializeObject(modifySettingList));

                Connection.Open();
                Console.WriteLine(Connection.State);

                foreach (var setting in settings)
                {
                    var settingToInject = new Setting
                    {
                        ClientId = setting.ClientId ?? string.Empty,
                        SettingName = setting.SettingName ?? string.Empty,
                        SettingType = setting.SettingType ?? string.Empty,
                        SettingValue = setting.SettingValue ?? string.Empty,
                        IsEncrypted = setting.IsEncrypted
                    };

                    SqlCommand cmd = new SqlCommand("dbo.uspInsertSettingDefinition", Connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    cmd.Parameters.Add(new SqlParameter("@ClientId", settingToInject.ClientId));
                    cmd.Parameters.Add(new SqlParameter("@SettingLevel", settingToInject.SettingLevel));
                    cmd.Parameters.Add(new SqlParameter("@SettingName", settingToInject.SettingName));
                    cmd.Parameters.Add(new SqlParameter("@SettingType", settingToInject.SettingType));
                    cmd.Parameters.Add(new SqlParameter("@SettingValue", settingToInject.SettingValue));
                    cmd.Parameters.Add(new SqlParameter("@IsEncrypted", settingToInject.IsEncrypted));
                    cmd.ExecuteNonQuery();
                }

                Connection.Close();
                Console.WriteLine(Connection.State);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void GeneratePullRequestFile()
        {
            var files = new List<string>();
            string importFile = GenerateImportSettings();

            files.Add(GetDatabaseConfigSettings());
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
            Console.WriteLine(Environment.NewLine + "Press [Enter] after you save the desired settings to import.");
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
