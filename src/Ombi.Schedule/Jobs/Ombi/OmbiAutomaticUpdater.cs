﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Extensions.Logging;

using Ombi.Api.Service;
using Ombi.Api.Service.Models;
using Ombi.Helpers;
using Ombi.Schedule.Ombi;

namespace Ombi.Schedule.Jobs.Ombi
{
    public class OmbiAutomaticUpdater : IOmbiAutomaticUpdater
    {
        public OmbiAutomaticUpdater(ILogger<OmbiAutomaticUpdater> log, IOmbiService service)
        {
            Logger = log;
            OmbiService = service;
        }

        private ILogger<OmbiAutomaticUpdater> Logger { get; }
        private IOmbiService OmbiService { get; }
        private static PerformContext Ctx { get; set; }

        public async Task Update(PerformContext c)
        {
            Ctx = c;
            Ctx.WriteLine("Starting the updater");
            // IF AutoUpdateEnabled =>
            // ELSE Return;
            var currentLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            Ctx.WriteLine("Path: {0}", currentLocation);

            var productVersion = AssemblyHelper.GetRuntimeVersion();
            Logger.LogInformation(LoggingEvents.Updater, "Product Version {0}", productVersion);
            Ctx.WriteLine("Product Version {0}", productVersion);

            try
            {
                var productArray = productVersion.Split('-');
                var version = productArray[0];
                Ctx.WriteLine("Version {0}", version);
                var branch = productArray[1];
                Ctx.WriteLine("Branch Version {0}", branch);

                Logger.LogInformation(LoggingEvents.Updater, "Version {0}", version);
                Logger.LogInformation(LoggingEvents.Updater, "Branch {0}", branch);

                Ctx.WriteLine("Looking for updates now");
                var updates = await OmbiService.GetUpdates(branch);
                Ctx.WriteLine("Updates: {0}", updates);
                var serverVersion = updates.UpdateVersionString;

                Logger.LogInformation(LoggingEvents.Updater, "Service Version {0}", updates.UpdateVersionString);
                Ctx.WriteLine("Service Version {0}", updates.UpdateVersionString);

                if (!serverVersion.Equals(version, StringComparison.CurrentCultureIgnoreCase))
                {
                    // Let's download the correct zip
                    var desc = RuntimeInformation.OSDescription;
                    var proce = RuntimeInformation.ProcessArchitecture;

                    Logger.LogInformation(LoggingEvents.Updater, "OS Information: {0} {1}", desc, proce);
                    Ctx.WriteLine("OS Information: {0} {1}", desc, proce);
                    Download download;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Logger.LogInformation(LoggingEvents.Updater, "We are Windows");
                        download = updates.Downloads.FirstOrDefault(x => x.Name.Contains("windows", CompareOptions.IgnoreCase));
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        Logger.LogInformation(LoggingEvents.Updater, "We are OSX");
                        download = updates.Downloads.FirstOrDefault(x => x.Name.Contains("osx", CompareOptions.IgnoreCase));
                    }
                    else
                    {
                        // Linux
                        if (desc.Contains("ubuntu", CompareOptions.IgnoreCase))
                        {
                            // Ubuntu
                            Logger.LogInformation(LoggingEvents.Updater, "We are ubuntu");
                            download = updates.Downloads.FirstOrDefault(x => x.Name.Contains("ubuntu", CompareOptions.IgnoreCase));

                        }
                        else if (desc.Contains("debian", CompareOptions.IgnoreCase))
                        {
                            // Debian
                            Logger.LogInformation(LoggingEvents.Updater, "We are debian");
                            download = updates.Downloads.FirstOrDefault(x => x.Name.Contains("debian", CompareOptions.IgnoreCase));
                        }
                        else if (desc.Contains("centos", CompareOptions.IgnoreCase))
                        {
                            // Centos
                            Logger.LogInformation(LoggingEvents.Updater, "We are centos");
                            download = updates.Downloads.FirstOrDefault(x => x.Name.Contains("centos",
                                CompareOptions.IgnoreCase));
                        }
                        else
                        {
                            return;
                        }
                    }
                    if (download == null)
                    {
                        Ctx.WriteLine("There were no downloads");
                        return;
                    }

                    Ctx.WriteLine("Found the download! {0}", download.Name);
                    Ctx.WriteLine("URL {0}", download.Url);

                    // Download it
                    Logger.LogInformation(LoggingEvents.Updater, "Downloading the file {0} from {1}", download.Name, download.Url);
                    var extension = download.Name.Split('.').Last();
                    var zipDir = Path.Combine(currentLocation, $"Ombi.{extension}");
                    Ctx.WriteLine("Zip Dir: {0}", zipDir);
                    try
                    {
                        if (File.Exists(zipDir))
                        {
                            File.Delete(zipDir);
                        }

                        Ctx.WriteLine("Starting Download");
                        await DownloadAsync(download.Url, zipDir);
                        Ctx.WriteLine("Finished Download");
                    }
                    catch (Exception e)
                    {
                        Ctx.WriteLine("Error when downloading");
                        Ctx.WriteLine(e.Message);
                        Logger.LogError(LoggingEvents.Updater, e, "Error when downloading the zip");
                        throw;
                    }
                    Ctx.WriteLine("Clearing out Temp Path");
                    var tempPath = Path.Combine(currentLocation, "TempUpdate");
                    if (Directory.Exists(tempPath))
                    {
                        Directory.Delete(tempPath, true);
                    }
                    // Extract it
                    Ctx.WriteLine("Extracting ZIP");
                    using (var files = ZipFile.OpenRead(zipDir))
                    {
                        // Temp Path
                        Directory.CreateDirectory(tempPath);
                        foreach (var entry in files.Entries)
                        {
                            if (entry.FullName.Contains("/"))
                            {
                                var path = Path.GetDirectoryName(Path.Combine(tempPath, entry.FullName));
                                Directory.CreateDirectory(path);
                            }

                            entry.ExtractToFile(Path.Combine(tempPath, entry.FullName));
                        }
                    }
                    Ctx.WriteLine("Finished Extracting files");
                    Ctx.WriteLine("Starting the Ombi.Updater process");
                    var updaterExtension = string.Empty;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        updaterExtension = ".exe";
                    }
                    // There must be an update
                    var start = new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        FileName = $"Ombi.Updater{updaterExtension}",
                        Arguments = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + " " + extension,
                    };
                    using (var proc = new Process { StartInfo = start })
                    {
                        proc.Start();
                    }
                    Ctx.WriteLine("Bye bye");
                }
            }
            catch (Exception e)
            { 
                Ctx.WriteLine(e);
                throw;
            }
        }

        public static async Task DownloadAsync(string requestUri, string filename)
        {
            using (var client = new WebClient())
            {
                await client.DownloadFileTaskAsync(requestUri, filename);
            }
        }
    }
}