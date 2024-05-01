using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;

namespace EasyInstallerV2
{
    class Program
    {
        const string BASE_URL = "https://pastebin.com/raw/gH3mkWid";

        public static HttpClient httpClient = new HttpClient();

        static async Task Download(string downloadUrl, string version, string resultPath)
        {
            string fileExtension = "";
            string filePath = "";

            if (!Directory.Exists(resultPath))
                Directory.CreateDirectory(resultPath);

            using (var httpClient = new HttpClient())
            {
                try
                {
                    var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, downloadUrl + ".zip"));

                    if (response.IsSuccessStatusCode)
                    {
                        downloadUrl = downloadUrl + ".zip";
                        fileExtension = ".zip";
                    }
                    else
                    {
                        try
                        {
                            var response2 = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, downloadUrl + ".rar"));

                            if (response2.IsSuccessStatusCode)
                            {
                                downloadUrl = downloadUrl + ".rar";
                                fileExtension = ".rar";
                            }
                        } catch (HttpRequestException)
                        {
                            Console.Write("Could not download build");
                            Thread.Sleep(3000);
                        }
                    }
                }
                catch (HttpRequestException)
                {
                
                    Console.Write("Could not download build");
                    Thread.Sleep(3000);
                }
            }

            var progress = new Progress<string>(message =>
            {
                Console.Write($"\r{message}");
            });

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                var progressReporter = new ProgressReporter(progress);

                string fileName = $"{version}{fileExtension}";
                filePath = Path.Combine(resultPath, fileName);

                await httpClient.DownloadFileWithProgressAsync(downloadUrl, filePath, progressReporter);
            }

            if (fileExtension == ".zip")
            {
                using (var archive = ZipFile.OpenRead(filePath))
                {
                    var totalEntries = archive.Entries.Count;
                    var entriesExtracted = 0;

                    Console.WriteLine();
                    int originalTop = Console.CursorTop;
                    int originalLeft = Console.CursorLeft;

                    foreach (var entry in archive.Entries)
                    {
                        var destination = Path.Combine(resultPath, entry.FullName);

                        Directory.CreateDirectory(Path.GetDirectoryName(destination));

                        if (!entry.FullName.EndsWith("/"))
                        {
                            entry.ExtractToFile(destination, true);
                        }

                        entriesExtracted++;
                        var percentage = (double)entriesExtracted / totalEntries * 100;
                        Console.SetCursorPosition(originalLeft, originalTop);
                        Console.Write($"Decompressing... {percentage:F2}%");
                    }
                }
            }
            else if (fileExtension == ".rar")
            {
                using (var archive = RarArchive.Open(filePath))
                {
                    var totalSize = archive.TotalSize;
                    var totalUnpacked = 0L;

                    Console.WriteLine();
                    int originalTop = Console.CursorTop;
                    int originalLeft = Console.CursorLeft;

                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(resultPath, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });

                        totalUnpacked += entry.Size;
                        var percentage = (double)totalUnpacked / totalSize * 100;
                        Console.SetCursorPosition(originalLeft, originalTop);
                        Console.WriteLine($"Decompressing... {percentage:F2}%");
                    }
                }
            }

            Console.WriteLine("\n\nFinished Downloading.\nPress any key to exit!");
            Thread.Sleep(100);
            Console.ReadKey();
        }

        static async Task<List<string>> GetVersionsAsync()
        {
            var versionsResponse = await httpClient.GetStringAsync(BASE_URL);

            if (string.IsNullOrEmpty(versionsResponse))
            {
                throw new Exception("failed to get versions");
            }

            var versions = JsonConvert.DeserializeObject<List<string>>(versionsResponse);

            if (versions == null)
            {
                throw new Exception("failed to parse versions");
            }

            return versions;
        }

        static async Task Main(string[] args)
        {
            var versions = await GetVersionsAsync();

            Console.Clear();

            Console.Title = "EasyInstaller V2 made by Ender & blk";
            Console.Write("\n\nEasyInstaller V2 made by Ender & blk\n\n");
            Console.WriteLine("\nAvailable manifests:");

            for (int i = 0; i < versions.Count; i++)
            {
                Console.WriteLine($" * [{i}] {versions[i]}");
            }

            Console.WriteLine($"\nTotal: {versions.Count}");

            Console.Write("Please enter the number before the Build Version to select it: ");
            var targetVersionStr = Console.ReadLine();

            if (!int.TryParse(targetVersionStr, out int targetVersionIndex))
            {
                await Main(args);
                return;
            }

            if (versions.ElementAtOrDefault(targetVersionIndex) == null)
            {
                await Main(args);
                return;
            }

            var targetVersion = versions[targetVersionIndex].Split("-")[1];

            Console.Write("Please enter a game folder location: ");
            var targetPath = Console.ReadLine();
            Console.Write("\n");

            if (string.IsNullOrEmpty(targetPath))
            {
                await Main(args);
                return;
            }

            await Download($"https://cdn.fnbuilds.services/{targetVersion}", targetVersion, targetPath);
        }
    }

    public class ProgressReporter : IProgress<string>
    {
        private readonly IProgress<string> _progress;

        public ProgressReporter(IProgress<string> progress)
        {
            _progress = progress;
        }

        public void Report(string value)
        {
            _progress.Report(value);
        }
    }

    public static class HttpClientExtensions
    {
        public static async Task DownloadFileWithProgressAsync(this HttpClient httpClient, string requestUri, string filePath, IProgress<string> progress)
        {
            using (var response = await httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        var bytesRead = 0;
                        var totalBytesRead = 0L;
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var stopwatch = Stopwatch.StartNew();

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                var percentage = (double)totalBytesRead / totalBytes;
                                var bytesPerSecond = totalBytesRead / stopwatch.Elapsed.TotalSeconds;
                                var megabytesPerSecond = bytesPerSecond / (1024 * 1024);
                                var megabytesRead = totalBytesRead / (1024.0 * 1024.0);
                                var totalMegabytes = totalBytes / (1024.0 * 1024.0);

                                progress.Report($"Downloaded: {percentage:P0} | {megabytesRead:F2}MB of {totalMegabytes:F2}MB | Speed: {megabytesPerSecond:F2}MB/s");
                            }
                        }
                    }
                }
            }
        }
    }
}
