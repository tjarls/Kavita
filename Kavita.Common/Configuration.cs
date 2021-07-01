using System;
using System.IO;
using System.Text.Json;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Hosting;

namespace Kavita.Common
{
    public static class Configuration
    {
        public static string GetAppSettingFilename()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isDevelopment = environment == Environments.Development;
            return "appsettings" + (isDevelopment ? ".Development" : "") + ".json";
        }
        
        #region JWT Token
        public static bool CheckIfJwtTokenSet(string filePath)
        {
            try {
                var json = File.ReadAllText(filePath);
                var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
                const string key = "TokenKey";
                
                if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
                {
                    return tokenElement.GetString() != "super secret unguessable key";
                }

                return false;
                
            }
            catch (Exception ex) {
                Console.WriteLine("Error writing app settings: " + ex.Message);
            }

            return false;
        }
        public static bool UpdateJwtToken(string filePath, string token)
        {
            try
            {
                var json = File.ReadAllText(filePath).Replace("super secret unguessable key", token);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion
        #region Port
        public static bool UpdatePort(string filePath, int port)
        {
            if (new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker)
            {
                return true;
            }
            
            try
            {
                var currentPort = GetPort(filePath);
                var json = File.ReadAllText(filePath).Replace("\"Port\": " + currentPort, "\"Port\": " + port);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static int GetPort(string filePath)
        {
            Console.WriteLine(GetAppSettingFilename());
            const int defaultPort = 5000;
            if (new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker)
            {
                return defaultPort;
            }
            
            try {
                var json = File.ReadAllText(filePath);
                var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
                const string key = "Port";
                
                if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
                {
                    return tokenElement.GetInt32();
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Error writing app settings: " + ex.Message);
            }

            return defaultPort;
        }
        #endregion
        #region LogLevel
        public static bool UpdateLogLevel(string filePath, string logLevel)
        {
            try
            {
                var currentLevel = GetLogLevel(filePath);
                var json = File.ReadAllText(filePath).Replace($"\"Default\": \"{currentLevel}\"", $"\"Default\": \"{logLevel}\"");
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static string GetLogLevel(string filePath)
        {
            try {
                var json = File.ReadAllText(filePath);
                var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
                if (jsonObj.TryGetProperty("Logging", out JsonElement tokenElement))
                {
                    foreach (var property in tokenElement.EnumerateObject())
                    {
                        if (!property.Name.Equals("LogLevel")) continue;
                        foreach (var logProperty in property.Value.EnumerateObject())
                        {
                            if (logProperty.Name.Equals("Default"))
                            {
                                return logProperty.Value.GetString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Error writing app settings: " + ex.Message);
            }

            return "Information";
        }
        #endregion
        
        public static string GetBranch(string filePath)
        {
            const string defaultBranch = "main";

            try {
                var json = File.ReadAllText(filePath);
                var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
                const string key = "Branch";
                
                if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
                {
                    return tokenElement.GetString();
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Error reading app settings: " + ex.Message);
            }

            return defaultBranch;
        }
        public static bool SetBranch(string filePath, string updatedBranch)
        {
            try
            {
                var currentBranch = GetBranch(filePath);
                var json = File.ReadAllText(filePath).Replace("\"Branch\": " + currentBranch, "\"Port\": " + updatedBranch);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}