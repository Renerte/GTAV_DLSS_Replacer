using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace GTAV_DLSS_Replacer;

[SupportedOSPlatform("windows")]
internal static class Program
{
    private const string ProcessName = "GTA5_Enhanced";

    private const string DlssToUse = "nvngx_dlss.dll";

    private const string DlssToDownloadIfNotFound =
        "https://github.com/NVIDIAGameWorks/Streamline/raw/refs/heads/main/bin/x64/nvngx_dlss.dll";

    private const string DlssDownloadProgressString = "Please wait... downloading: ";

    private const string DlssFileToReplaceInGtaVLocation = "nvngx_dlss.dll";
    private const string DlssBackupSuffix = "_backup";

    private const string Separator = "==========";

    private const string
        GtaVLocationFileForAutoStart =
            "GtaV_location_for_auto_start.txt"; //to auto launch GTAV as told from this location

    private static string _gtaVFolder;
    private static string _currentFolder; //to store dlss backup

    private static string _gtaVLocationForAutoStart; //start GtaV automatically if this exist

    private static WebClient _downloadDlssWebClient;

    private static void Main()
    {
        var appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly()
            .GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)!).Value;
        var mutexId = $"Global\\{{{appGuid}}}";
        using var mutex = new Mutex(false, mutexId);
        if (!mutex.WaitOne(0, false))
        {
            Console.WriteLine(
                "ERROR: This program is already running. To ensure stability only one instance of this program can be opened.\n");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            Environment.Exit(1);
        }

        if (IsAdministrator())
        {
            _currentFolder = Directory.GetCurrentDirectory();
            CheckDlssFileExistsOrDownload();
        }
        else
        {
            Console.WriteLine(
                "ERROR: Open this program with Administrator privileges. It is required as to replace files in GTAV Folder.\n");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            Environment.Exit(1);
        }
    }

    private static void CheckDlssFileExistsOrDownload()
    {
        if (File.Exists(DlssToUse))
        {
            //dlss file exists
            StartProcess();
        }
        else
        {
            //download dlss file.
            Console.WriteLine(
                "{0}\nDownloading DLSS file to use\n{1}\n\nDLSS file to use: {2} does not found, so we're downloading from: {3}\n\nIf you want to use your own, you can replace it with your own version.\n",
                Separator, Separator, DlssToUse, DlssToDownloadIfNotFound);
            Console.Write("\r{0} {1}%", DlssDownloadProgressString, 0);

            _downloadDlssWebClient = new WebClient();
            _downloadDlssWebClient.DownloadProgressChanged += client_DownloadProgressChanged;
            _downloadDlssWebClient.DownloadFileCompleted += client_DownloadFileCompleted;

            var tempFile =
                DlssToUse +
                "_temp"; //download with _temp so if download is corrupted, the process can start again.
            _downloadDlssWebClient.DownloadFileAsync(new Uri(DlssToDownloadIfNotFound), tempFile);
            Console.ReadLine();
        }
    }

    private static void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e) // NEW
    {
        Console.Write("\r{0} {1}%", DlssDownloadProgressString, e.ProgressPercentage);
    }

    private static void
        client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e) // This is our new method!
    {
        Console.Write("\r{0} {1}", DlssDownloadProgressString, "Finished");
        Console.WriteLine("\n");
        File.Move(DlssToUse + "_temp", DlssToUse);

        StartProcess();
    }

    private static void StartProcess()
    {
        //release webclient memory if not null
        if (_downloadDlssWebClient != null) _downloadDlssWebClient.Dispose();

        ManagementEventWatcher w = null;
        ManagementEventWatcher w2 = null;

        var processNameComplete = ProcessName + ".exe";
        Console.WriteLine(
            "{0}\nGTAV DLSS Replacer running.\n{1}\n\nDLSS to use: {2} (version: {3})\nListening for process: {4}\n",
            Separator, Separator, DlssToUse, GetDlssVersion(DlssToUse), processNameComplete);

        UpdateConsole("idle");

        try
        {
            //detect start of apps
            w = new ManagementEventWatcher(
                $"Select * From Win32_ProcessStartTrace WHERE ProcessName='{processNameComplete}'");
            w.EventArrived += ProcessStartEventArrived;
            w.Start();

            //detect exit of apps
            w2 = new ManagementEventWatcher(
                $"Select * From Win32_ProcessStopTrace WHERE ProcessName='{processNameComplete}'");
            w2.EventArrived += ProcessStopEventArrived;
            w2.Start();

            //auto start GTAV if GtaVLocationFileForAutoStart exist and is valid
            AutoStartGtaVIfEnabled();

            //Keep it running.
            Console.ReadLine();
        }
        finally
        {
            w?.Stop();
            w2?.Stop();
        }
    }

    private static void ProcessStartEventArrived(object sender, EventArrivedEventArgs ev)
    {
        //Execute when GTAV is launched.
        foreach (var pd in ev.NewEvent.Properties)
            if (pd.Name == "ProcessName")
            {
                UpdateConsole("started");
                CopyDlssFile(ProcessName, "started");
            }
    }

    private static void ProcessStopEventArrived(object sender, EventArrivedEventArgs e)
    {
        //Execute when GTAV is stopped.
        foreach (var pd in e.NewEvent.Properties)
            if (pd.Name == "ProcessName")
            {
                UpdateConsole("stopped");

                CopyDlssFile(ProcessName, "stopped");

                Thread.Sleep(2000);
                UpdateConsole("idle");
            }
    }

    private static void AutoStartGtaVIfEnabled()
    {
        if (File.Exists(GtaVLocationFileForAutoStart))
        {
            //read file
            var gtaVLocationS = File.ReadAllText(GtaVLocationFileForAutoStart).Trim();
            if (!string.IsNullOrEmpty(gtaVLocationS))
            {
                //remove comments (starting with hash) and empty lines from file
                var gtaVLocationA = File.ReadLines(GtaVLocationFileForAutoStart)
                    .Where(line => !line.StartsWith('#') && !string.IsNullOrEmpty(line)).ToArray();

                //read only first
                var gtaVLocation = gtaVLocationA.FirstOrDefault();

                if (gtaVLocation != null)
                {
                    gtaVLocation = gtaVLocation.Replace("\"", "");
                    if (!string.IsNullOrEmpty(gtaVLocation))
                    {
                        //check if gtaVlocation exist.
                        if (File.Exists(gtaVLocation))
                        {
                            _gtaVLocationForAutoStart = gtaVLocation;
                            Console.WriteLine("Auto start location found: {0}\n", _gtaVLocationForAutoStart);

                            UpdateConsole("starting");
                            StartGtaV();
                        }
                        else
                        {
                            Console.WriteLine("ERROR: `{0}` does not exist.\n", gtaVLocation);
                            Console.WriteLine(
                                "- Either delete `{0}` so you can start GTAV manually, or put correct location in `{1}`\n",
                                GtaVLocationFileForAutoStart, GtaVLocationFileForAutoStart);
                            Console.WriteLine("Press any key to exit.");
                            Console.ReadKey();
                            Environment.Exit(1);
                        }
                    }
                }
            }
        }

        if (_gtaVLocationForAutoStart == null) Console.WriteLine("Start your game now.\n");
    }

    private static void StartGtaV()
    {
        var processStartInfo = new ProcessStartInfo(_gtaVLocationForAutoStart)
        {
            WorkingDirectory = Path.GetDirectoryName(_gtaVLocationForAutoStart) ?? throw new InvalidOperationException()
        };
        Process.Start(processStartInfo);
    }

    private static void CopyDlssFile(string processName, string type)
    {
        try
        {
            //get GTA5_Enhanced.exe location
            if (_gtaVFolder == null)
            {
                var process = Process.GetProcessesByName(processName);
                if (process == null || process.Length <= 0)
                    throw new Exception("Unable to get process by name: " + processName);

                var firstProcess = process.First();

                var processPath = firstProcess.GetMainModuleFileName();
                _gtaVFolder = Path.GetDirectoryName(processPath);
            }

            var source = DlssToUse;
            var destination = _gtaVFolder + "/" + DlssFileToReplaceInGtaVLocation;
            var destinationForBackup =
                _currentFolder + "/" + DlssFileToReplaceInGtaVLocation + DlssBackupSuffix;

            Console.WriteLine("GTAV Location: {0}\\{1}", _gtaVFolder, ProcessName + ".exe");

            switch (type)
            {
                case "started":
                    //create backup.
                    File.Copy(destination, destinationForBackup, true);
                    Console.WriteLine("✓ Successfully backed up DLSS: {0}", GetDlssVersion(destinationForBackup));

                    //replace dlss.
                    File.Copy(source, destination, true);
                    Console.WriteLine("✓ Successfully copied DLSS: {0}\n", GetDlssVersion(destination));
                    break;
                case "stopped":
                    //restore from backup
                    File.Copy(destinationForBackup, destination, true);
                    Console.WriteLine("✓ Successfully restored backup DLSS: {0}\n", GetDlssVersion(destination));
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: Unable to copy file.\n\nException: {0}", ex);
        }
    }

    private static void UpdateConsole(string type)
    {
        switch (type)
        {
            case "idle":
            {
                Console.Title = "Idle - GTAV DLSS Replacer";
                Console.WriteLine("{0}\nIdle - Waiting for process to launch.\n{1}\n", Separator, Separator);
                break;
            }
            case "starting":
            {
                Console.Title = "Starting GTAV - GTAV DLSS Replacer";
                Console.WriteLine("- Starting GTAV...\n");
                break;
            }
            case "started":
            {
                Console.Title = ProcessName + " Started - GTAV DLSS Replacer";
                Console.WriteLine("{0}\n{1} Started\n{2}\n", Separator, ProcessName, Separator);
                Console.WriteLine("Trying to use new DLSS.\n");
                break;
            }
            case "stopped":
            {
                Console.Title = ProcessName + " Stopped - GTAV DLSS Replacer";
                Console.WriteLine("{0}\n{1} Process Stopped\n{2}\n", Separator, ProcessName, Separator);
                Console.WriteLine("Trying to restore original GTAV's DLSS.\n");
                break;
            }
        }
    }

    private static string GetDlssVersion(string file)
    {
        var dlssVersionInfo = FileVersionInfo.GetVersionInfo(file);
        return dlssVersionInfo.FileVersion?.Replace(",", ".");
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}

internal static class Extensions
{
    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags,
        [Out] StringBuilder lpExeName, [In] [Out] ref uint lpdwSize);

    internal static string GetMainModuleFileName(this Process process, int buffer = 1024)
    {
        var fileNameBuilder = new StringBuilder(buffer);
        var bufferLength = (uint)fileNameBuilder.Capacity + 1;
        return QueryFullProcessImageName(process.Handle, 0, fileNameBuilder, ref bufferLength)
            ? fileNameBuilder.ToString()
            : null;
    }
}