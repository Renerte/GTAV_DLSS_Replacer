using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace RDR2_DLSS_Replacer
{
    internal static class Program
    {
        public const string ProcessName = "RDR2";

        public const string DlssToUse = "nvngx_dlss.dll";

        public const string DlssToDownloadIfNotFound =
            "https://github.com/Bullz3y3/GTAV_DLSS_Replacer/raw/master/DLSS/nvngx_dlss_3.5.10.dll";

        public const string DlssDownloadProgressString = "Please wait... downloading: ";

        public const string DlssFileToReplaceInRdr2Location = "nvngx_dlss.dll";
        public const string DlssBackupSuffix = "_backup";

        public const string Separator = "==========";

        public static string Rdr2Folder;
        public static string CurrentFolder; //to store dlss backup

        public const string Rdr2LocationFileForAutoStart = "rdr2_location_for_auto_start.txt"; //to auto launch RDR2 as told from this location

        public static string Rdr2LocationForAutoStart; //start rdr2 automatically if this exist

        public static WebClient DownloadDlssWebClient;

        public static void Main()
        {
            var appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value;
            var mutexId = $"Global\\{{{appGuid}}}";
            using (var mutex = new Mutex(false, mutexId))
            {
                if (!mutex.WaitOne(0, false))
                {
                    Console.WriteLine(
                        "ERROR: This program is already running. To ensure stability only one instance of this program can be opened.\n");
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    Environment.Exit(1);
                }

                if (isAdministrator())
                {
                    CurrentFolder = Directory.GetCurrentDirectory();
                    checkDlssFileExistsOrDownload();
                }
                else
                {
                    Console.WriteLine(
                        "ERROR: Open this program with Administrator privileges. It is required as to replace files in RDR2 Folder.\n");
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    Environment.Exit(1);
                }
            }
        }

        public static void checkDlssFileExistsOrDownload()
        {
            if (File.Exists(DlssToUse))
            {
                //dlss file exists
                startProcess();
            }
            else
            {
                //download dlss file.
                Console.WriteLine(
                    "{0}\nDownloading DLSS file to use\n{1}\n\nDLSS file to use: {2} does not found, so we're downloading from: {3}\n\nIf you want to use your own, you can replace it with your own version.\n",
                    Separator, Separator, DlssToUse, DlssToDownloadIfNotFound);
                Console.Write("\r{0} {1}%", DlssDownloadProgressString, 0);

                DownloadDlssWebClient = new WebClient();
                DownloadDlssWebClient.DownloadProgressChanged += client_DownloadProgressChanged;
                DownloadDlssWebClient.DownloadFileCompleted += client_DownloadFileCompleted;

                var tempFile =
                    DlssToUse +
                    "_temp"; //download with _temp so if download is corrupted, the process can start again.
                DownloadDlssWebClient.DownloadFileAsync(new Uri(DlssToDownloadIfNotFound), tempFile);
                Console.ReadLine();
            }
        }

        public static void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e) // NEW
        {
            Console.Write("\r{0} {1}%", DlssDownloadProgressString, e.ProgressPercentage);
        }

        private static void
            client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e) // This is our new method!
        {
            Console.Write("\r{0} {1}", DlssDownloadProgressString, "Finished");
            Console.WriteLine("\n");
            File.Move(DlssToUse + "_temp", DlssToUse);

            startProcess();
        }

        public static void startProcess()
        {
            //release webclient memory if not null
            if (DownloadDlssWebClient != null) DownloadDlssWebClient.Dispose();

            ManagementEventWatcher w = null;
            ManagementEventWatcher w2 = null;

            var processNameComplete = ProcessName + ".exe";
            Console.WriteLine(
                "{0}\nRDR2 DLSS Replacer running.\n{1}\n\nDLSS to use: {2} (version: {3})\nListening for process: {4}\n",
                Separator, Separator, DlssToUse, getDlssVersion(DlssToUse), processNameComplete);

            updateConsole("idle");

            try
            {
                //detect start of apps
                w = new ManagementEventWatcher("Select * From Win32_ProcessStartTrace WHERE ProcessName='" +
                                               processNameComplete + "'");
                w.EventArrived += ProcessStartEventArrived;
                w.Start();

                //detect exit of apps
                w2 = new ManagementEventWatcher("Select * From Win32_ProcessStopTrace WHERE ProcessName='" +
                                                processNameComplete + "'");
                w2.EventArrived += ProcessStopEventArrived;
                w2.Start();

                //auto start RDR2 if rdr2LocationFileForAutoStart exist and is valid
                autoStartRdr2IfEnabled();

                //Keep it running.
                Console.ReadLine();
            }
            finally
            {
                w.Stop();
                w2.Stop();
            }
        }

        public static void ProcessStartEventArrived(object sender, EventArrivedEventArgs ev)
        {
            //Execute when RDR2 is launched.
            foreach (var pd in ev.NewEvent.Properties)
                if (pd.Name == "ProcessName")
                {
                    updateConsole("started");
                    CopyDlssFile(ProcessName, "started");
                }
        }

        public static void ProcessStopEventArrived(object sender, EventArrivedEventArgs e)
        {
            //Execute when RDR2 is stopped.
            foreach (var pd in e.NewEvent.Properties)
                if (pd.Name == "ProcessName")
                {
                    updateConsole("stopped");

                    CopyDlssFile(ProcessName, "stopped");

                    Thread.Sleep(2000);
                    updateConsole("idle");
                }
        }

        public static void autoStartRdr2IfEnabled()
        {
            if (File.Exists(Rdr2LocationFileForAutoStart))
            {
                //read file
                var rdr2LocationS = File.ReadAllText(Rdr2LocationFileForAutoStart).Trim();
                if (!string.IsNullOrEmpty(rdr2LocationS))
                {
                    //remove comments (starting with hash) and empty lines from file
                    var rdr2LocationA = File.ReadLines(Rdr2LocationFileForAutoStart)
                        .Where(line => !line.StartsWith("#") && !string.IsNullOrEmpty(line)).ToArray();

                    //read only first
                    var rdr2Location = rdr2LocationA.FirstOrDefault();

                    if (rdr2Location != null)
                    {
                        rdr2Location = rdr2Location.Replace("\"", "");
                        if (!string.IsNullOrEmpty(rdr2Location))
                        {
                            //check if rdr2location exist.
                            if (File.Exists(rdr2Location))
                            {
                                Rdr2LocationForAutoStart = rdr2Location;
                                Console.WriteLine("Auto start location found: {0}\n", Rdr2LocationForAutoStart);

                                updateConsole("starting");
                                startRdr2();
                            }
                            else
                            {
                                Console.WriteLine("ERROR: `{0}` does not exist.\n", rdr2Location);
                                Console.WriteLine(
                                    "- Either delete `{0}` so you can start RDR2 manually, or put correct location in `{1}`\n",
                                    Rdr2LocationFileForAutoStart, Rdr2LocationFileForAutoStart);
                                Console.WriteLine("Press any key to exit.");
                                Console.ReadKey();
                                Environment.Exit(1);
                            }
                        }
                    }
                }
            }

            if (Rdr2LocationForAutoStart == null) Console.WriteLine("Start your game now.\n");
        }

        public static void startRdr2()
        {
            var processStartInfo = new ProcessStartInfo(Rdr2LocationForAutoStart)
            {
                WorkingDirectory = Path.GetDirectoryName(Rdr2LocationForAutoStart) ?? throw new InvalidOperationException()
            };
            Process.Start(processStartInfo);
        }

        public static void CopyDlssFile(string processName, string type)
        {
            try
            {
                //get RDR2.exe location
                if (Rdr2Folder == null)
                {
                    var process = Process.GetProcessesByName(processName);
                    if (process == null || process.Length <= 0)
                        throw new Exception("Unable to get process by name: " + processName);

                    var firstProcess = process.First();

                    var processPath = firstProcess.GetMainModuleFileName();
                    Rdr2Folder = Path.GetDirectoryName(processPath);
                }

                var source = DlssToUse;
                var destination = Rdr2Folder + "/" + DlssFileToReplaceInRdr2Location;
                var destinationForBackup =
                    CurrentFolder + "/" + DlssFileToReplaceInRdr2Location + DlssBackupSuffix;

                Console.WriteLine("RDR2 Location: {0}\\{1}", Rdr2Folder, ProcessName + ".exe");

                if (type == "started")
                {
                    //create backup.
                    File.Copy(destination, destinationForBackup, true);
                    Console.WriteLine("✓ Successfully backed up DLSS: {0}", getDlssVersion(destinationForBackup));

                    //replace dlss.
                    File.Copy(source, destination, true);
                    Console.WriteLine("✓ Successfully copied DLSS: {0}\n", getDlssVersion(destination));
                }
                else if (type == "stopped")
                {
                    //restore from backup
                    File.Copy(destinationForBackup, destination, true);
                    Console.WriteLine("✓ Successfully restored backup DLSS: {0}\n", getDlssVersion(destination));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Unable to copy file.\n\nException: {0}", ex);
            }
        }

        public static void updateConsole(string type)
        {
            switch (type)
            {
                case "idle":
                {
                    Console.Title = "Idle - RDR2 DLSS Replacer";
                    Console.WriteLine("{0}\nIdle - Waiting for process to launch.\n{1}\n", Separator, Separator);
                    break;
                }
                case "starting":
                {
                    Console.Title = "Starting RDR2 - RDR2 DLSS Replacer";
                    Console.WriteLine("- Starting RDR2...\n");
                    break;
                }
                case "started":
                {
                    Console.Title = ProcessName + " Started - RDR2 DLSS Replacer";
                    Console.WriteLine("{0}\n{1} Started\n{2}\n", Separator, ProcessName, Separator);
                    Console.WriteLine("Trying to use new DLSS.\n");
                    break;
                }
                case "stopped":
                {
                    Console.Title = ProcessName + " Stopped - RDR2 DLSS Replacer";
                    Console.WriteLine("{0}\n{1} Process Stopped\n{2}\n", Separator, ProcessName, Separator);
                    Console.WriteLine("Trying to restore original RDR2's DLSS.\n");
                    break;
                }
            }
        }

        public static string getDlssVersion(string file)
        {
            var dlssVersionInfo = FileVersionInfo.GetVersionInfo(file);
            return dlssVersionInfo.FileVersion.Replace(",", ".");
        }

        public static bool isAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }

    internal static class Extensions
    {
        [DllImport("Kernel32.dll")]
        private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags,
            [Out] StringBuilder lpExeName, [In] [Out] ref uint lpdwSize);

        public static string GetMainModuleFileName(this Process process, int buffer = 1024)
        {
            var fileNameBuilder = new StringBuilder(buffer);
            var bufferLength = (uint)fileNameBuilder.Capacity + 1;
            return QueryFullProcessImageName(process.Handle, 0, fileNameBuilder, ref bufferLength)
                ? fileNameBuilder.ToString()
                : null;
        }
    }
}