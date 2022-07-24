using System;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AutoJudge.NETCore
{
    class Program
    {
        const string SRC_FILE = "-src";
        const string OSD_DIR = "-osd";
        const string USER = "-user";

        const string THEMIS_PROCESS_NAME = "Themis";

        const string CONFIG_FILENAME = "autojudge_config.json";

        static string DEFAULT_OSD = @"C:\OSD";
        const string DEFAULT_USERNAME = "AutoJudge";

        static string osd_runtime = DEFAULT_OSD;
        static string username_runtime = DEFAULT_USERNAME;

        const int MAXIMUM_TIMEOUT_MILISECONDS = 1000 * 600;

        static bool INVALID_ARGUMENTS_FLAG = false;

        static void Main(string[] args)
        {
            if (args.Length % 2 == 1 || args.Length == 0)
            {
                if (args.Length == 1 && args[0] == "-reset")
                {
                    Reset();
                    return;
                }
                else
                {
                    INVALID_ARGUMENTS_FLAG = true;
                }
            }

            Initialize();

            if (!FoundThemisProcess())
            {
                InvalidProcessTerminate();
                return;
            }

            if (INVALID_ARGUMENTS_FLAG)
            {
                InvalidArgumentsTerminate();
                return;
            }

            string filePath = "";
            string osd = osd_runtime;
            string userName = username_runtime;

            bool hasFile = false;

            for (int i = 0; i < args.Length; i += 2)
            {
                string prefix = args[i];
                string content = args[i + 1];

                switch (prefix.ToLower())
                {
                    case SRC_FILE:
                        filePath = Path.GetFullPath(content);
                        hasFile = true;
                        break;
                    case OSD_DIR:
                        osd = content;
                        break;
                    case USER:
                        userName = content;
                        break;
                    default:
                        break;
                }
            }

            if (!hasFile || !Directory.Exists(osd))
            {
                InvalidArgumentsTerminate();
                return;
            }

            string logPath = CopyFileAndGetLogPath(filePath, osd, userName);

            DateTime prev = DateTime.Now;

            bool hasResult = true;
            while (!File.Exists(logPath))
            {
                DateTime cur = DateTime.Now;
                TimeSpan duration = cur - prev;

                if (duration.Milliseconds > MAXIMUM_TIMEOUT_MILISECONDS)
                {
                    hasResult = false;
                    break;
                }
            }

            if (!hasResult)
            {
                NoFileTerminate();
                return;
            }

            PrintResult(logPath, prev);
        }

        static void Reset()
        {
            string configPath = GetConfigurationFilePath();
            if (File.Exists(configPath)) File.Delete(configPath);
        }

        static void AnimateThreeDots()
        {
            for (int i = 1; i <= 3; i++)
            {
                Console.Write(".");
                Task.Delay(1000);
            }
        }

        static void Initialize()
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Write("Starting AutoJudge - Themis OSD Utility ");

            AnimateThreeDots();
            Console.WriteLine();

            Console.WriteLine($"Copyright (C) {DateTime.Now.Year}, Vu Truong Giang, LQDVT 1720.");
            Console.WriteLine();

            Console.WriteLine($"{SRC_FILE} <FILE_NAME> (mandatory) - The path of the source code file to be copied.");
            Console.WriteLine($"{OSD_DIR} <FOLDER_PATH> - Custom Online Submission Directory.");
            Console.WriteLine($"{USER} <USER_NAME> - Custom user name in Themis");

            GetSettings();
        }

        static bool FoundThemisProcess()
        {
            Process[] processes = Process.GetProcessesByName(THEMIS_PROCESS_NAME);
            return (processes.Length > 0);
        }

        static string GetAppDataPath()
        {
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDataPath = $@"{localAppDataPath}\AutoJudge";
            Directory.CreateDirectory(appDataPath);

            return appDataPath;
        }

        static string GetConfigurationFilePath()
        {
            string configPath = Path.Combine(GetAppDataPath(), CONFIG_FILENAME);
            return configPath;
        }

        static void GetSettings()
        {
            string appDataPath = GetAppDataPath();
            DEFAULT_OSD = $@"{appDataPath}\OSD";

            string configPath = GetConfigurationFilePath();

            if (!File.Exists(configPath))
            {
                Console.WriteLine("First time setup is required.");
                Console.Write("Default Themis OSD: ");
                string osd = Console.ReadLine() ?? DEFAULT_OSD;

                try
                {
                    Directory.CreateDirectory(osd);

                    Console.Write("Username: ");
                    string username = Console.ReadLine() ?? DEFAULT_USERNAME;

                    CreateConfigurationFile(osd, username);
                }
                catch (Exception)
                {
                    INVALID_ARGUMENTS_FLAG = true;
                    return;
                }
            }

            string json = File.ReadAllText(configPath);
            Configuration config = JsonConvert.DeserializeObject<Configuration>(json);

            osd_runtime = config.OSD_DIR;
            username_runtime = config.USER_NAME;

            Directory.CreateDirectory(osd_runtime);
        }

        static void CreateConfigurationFile(string osd, string username)
        {
            string appDataPath = GetAppDataPath();
            string configPath = Path.Combine(appDataPath, CONFIG_FILENAME);

            string json = JsonConvert.SerializeObject(new Configuration() { OSD_DIR = osd, USER_NAME = username });

            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }

            using (StreamWriter sw = new StreamWriter(configPath))
            {
                sw.Write(json);
            }
        }

        static string CopyFileAndGetLogPath(string filePath, string osd, string userName)
        {
            int rnd = (new Random()).Next();

            string task = Path.GetFileNameWithoutExtension(filePath).ToUpper();
            string ext = Path.GetExtension(filePath);

            string destFileName = $"{rnd}[{userName}][{task}]{ext}";
            string logFileName = $"Logs\\{destFileName}.log";

            string dest = Path.Combine(osd, destFileName);
            string logPath = Path.Combine(osd, logFileName);

            File.Copy(filePath, dest);

            return logPath;
        }

        static void InvalidArgumentsTerminate()
        {
            Console.WriteLine("Argument ERROR: Invalid arguments.");
            Console.WriteLine("The program will now be terminated.");
        }

        static void InvalidProcessTerminate()
        {
            Console.WriteLine("Invalid process ERROR: Themis is not running.");
            Console.WriteLine("The program will now be terminated.");
        }

        static void NoFileTerminate()
        {
            Console.WriteLine("Timeout ERROR: Idleness Limit Exceeded.");
            Console.WriteLine($"Maximum time allowed: {TimeSpan.FromMilliseconds(MAXIMUM_TIMEOUT_MILISECONDS).Seconds} seconds.");
            Console.WriteLine("The program will now be terminated.");
        }

        static void PrintResult(string logPath, DateTime prev)
        {
            Task.Delay(2000);

            Console.WriteLine();

            using (StreamReader sr = new StreamReader(logPath))
            {
                string points = sr.ReadLine();
                Console.WriteLine(sr.ReadToEnd());
                Console.WriteLine(points);
            }

            DateTime now = DateTime.Now;

            TimeSpan length = now - prev;

            Console.WriteLine(string.Format("Task approximately completed in {0:c}", length));
            Console.WriteLine("Done.");
        }
    }

    public class Configuration
    {
        public string USER_NAME { get; set; }
        public string OSD_DIR { get; set; }
    }
}
