using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Handles downloading and installing updates, with backup/rollback support.
    /// </summary>
    public interface UpdateDownloader
    {
        /// <summary>
        /// Downloads an update .msi file to the updates directory.
        /// Shows progress and validates the download.
        /// </summary>
        Task<bool> DownloadUpdateAsync(string downloadUrl, string targetVersion);

        /// <summary>
        /// Installs a previously downloaded .msi file in silent mode.
        /// </summary>
        Task<bool> InstallUpdateAsync(string msiPath);

        /// <summary>
        /// Creates a backup of the current app installation.
        /// </summary>
        Task<bool> CreateBackupAsync(string fromVersion);

        /// <summary>
        /// Restores the app from a previous backup.
        /// </summary>
        Task<bool> RollbackToPreviousVersionAsync();

        /// <summary>
        /// Gets the path to the updates directory.
        /// </summary>
        string GetUpdatesDirectory();
    }

    /// <summary>
    /// Concrete implementation of UpdateDownloader.
    /// </summary>
    public class UpdateDownloaderImpl : UpdateDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly OutputWriter? _outputWriter;
        private readonly string _appDataPath;
        private readonly int _maxBackupRetention;

        private const string UpdatesDirName = "updates";
        private const string BackupsDirName = "backups";
        private const string BackupMetadataFileName = "backup.json";

        /// <summary>
        /// Creates a new UpdateDownloaderImpl.
        ///
        /// Parameters:
        /// - httpClient: HttpClient for downloads
        /// - outputWriter: Optional logging
        /// - appDataPath: Base app data directory (usually %APPDATA%\EliteDangerousDataCollector)
        /// - maxBackupRetention: Maximum number of backups to keep (default 3)
        /// </summary>
        public UpdateDownloaderImpl(
            HttpClient httpClient,
            OutputWriter? outputWriter,
            string appDataPath,
            int maxBackupRetention = 3)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _outputWriter = outputWriter;
            _appDataPath = appDataPath ?? throw new ArgumentNullException(nameof(appDataPath));
            _maxBackupRetention = maxBackupRetention;
        }

        public string GetUpdatesDirectory()
        {
            return Path.Combine(_appDataPath, UpdatesDirName);
        }

        public async Task<bool> DownloadUpdateAsync(string downloadUrl, string targetVersion)
        {
            try
            {
                _outputWriter?.WriteLine($"[UpdateDownloader] Starting download: {downloadUrl}", LogLevel.Info);

                string updatesDir = GetUpdatesDirectory();
                Directory.CreateDirectory(updatesDir);

                // Extract filename from URL
                string fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
                if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".msi"))
                {
                    fileName = $"EliteDataCollector-{targetVersion}.msi";
                }

                string msiPath = Path.Combine(updatesDir, fileName);

                // Download the file
                _outputWriter?.WriteLine($"[UpdateDownloader] Downloading to: {msiPath}", LogLevel.Debug);

                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        _outputWriter?.WriteLine($"[UpdateDownloader] Download failed with status {response.StatusCode}", LogLevel.Error);
                        return false;
                    }

                    long? contentLength = response.Content.Headers.ContentLength;
                    
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(msiPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        long totalBytesRead = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            if (contentLength.HasValue)
                            {
                                int percentComplete = (int)(totalBytesRead * 100 / contentLength.Value);
                                _outputWriter?.WriteLine($"[UpdateDownloader] Download progress: {percentComplete}%", LogLevel.Debug);
                            }
                        }
                    }
                }

                // Verify file exists and has content
                if (!File.Exists(msiPath) || new FileInfo(msiPath).Length == 0)
                {
                    _outputWriter?.WriteLine($"[UpdateDownloader] Downloaded file is empty or missing", LogLevel.Error);
                    return false;
                }

                _outputWriter?.WriteLine($"[UpdateDownloader] Download complete: {new FileInfo(msiPath).Length} bytes", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[UpdateDownloader] Download failed: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        public async Task<bool> InstallUpdateAsync(string msiPath)
        {
            try
            {
                if (!File.Exists(msiPath))
                {
                    _outputWriter?.WriteLine($"[UpdateDownloader] MSI file not found: {msiPath}", LogLevel.Error);
                    return false;
                }

                _outputWriter?.WriteLine($"[UpdateDownloader] Installing update: {msiPath}", LogLevel.Info);

                // Create backup before installing
                if (!await CreateBackupAsync("pre-update"))
                {
                    _outputWriter?.WriteLine("[UpdateDownloader] Warning: backup creation failed, but proceeding with install", LogLevel.Warning);
                }

                // Run MSI installer in silent mode with no restart
                var processInfo = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{msiPath}\" /quiet /norestart",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        _outputWriter?.WriteLine("[UpdateDownloader] Failed to start installer", LogLevel.Error);
                        return false;
                    }

                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        _outputWriter?.WriteLine("[UpdateDownloader] Installation completed successfully", LogLevel.Info);
                        
                        // Clean up old backups
                        await CleanupOldBackupsAsync();
                        
                        return true;
                    }
                    else
                    {
                        _outputWriter?.WriteLine($"[UpdateDownloader] Installation failed with exit code {process.ExitCode}", LogLevel.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[UpdateDownloader] Installation error: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        public async Task<bool> CreateBackupAsync(string fromVersion)
        {
            try
            {
                // Get the current app installation path (where the .exe is running)
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                
                if (!Directory.Exists(appDir))
                {
                    _outputWriter?.WriteLine($"[UpdateDownloader] App directory not found: {appDir}", LogLevel.Warning);
                    return false;
                }

                string backupsDir = Path.Combine(_appDataPath, BackupsDirName);
                string backupDir = Path.Combine(backupsDir, $"v{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{fromVersion}");

                _outputWriter?.WriteLine($"[UpdateDownloader] Creating backup: {backupDir}", LogLevel.Info);

                Directory.CreateDirectory(backupDir);

                // Recursively copy app directory to backup
                CopyDirectory(appDir, backupDir);

                // Write backup metadata
                var metadata = new
                {
                    Version = fromVersion,
                    BackupDate = DateTime.Now.ToString("O"),
                    OriginalLocation = appDir
                };

                string metadataPath = Path.Combine(backupDir, BackupMetadataFileName);
                string jsonString = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metadataPath, jsonString);

                _outputWriter?.WriteLine($"[UpdateDownloader] Backup created successfully", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[UpdateDownloader] Backup creation failed: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        public async Task<bool> RollbackToPreviousVersionAsync()
        {
            try
            {
                string backupsDir = Path.Combine(_appDataPath, BackupsDirName);

                if (!Directory.Exists(backupsDir))
                {
                    _outputWriter?.WriteLine("[UpdateDownloader] No backups found", LogLevel.Warning);
                    return false;
                }

                // Get the most recent backup
                var backups = Directory.GetDirectories(backupsDir);
                
                if (backups.Length == 0)
                {
                    _outputWriter?.WriteLine("[UpdateDownloader] No backups available for rollback", LogLevel.Warning);
                    return false;
                }

                // Sort to get the most recent (last directory by name)
                Array.Sort(backups);
                string latestBackup = backups[backups.Length - 1];

                _outputWriter?.WriteLine($"[UpdateDownloader] Rolling back to: {latestBackup}", LogLevel.Info);

                // Read metadata to display version info
                string metadataPath = Path.Combine(latestBackup, BackupMetadataFileName);
                if (File.Exists(metadataPath))
                {
                    string jsonString = await File.ReadAllTextAsync(metadataPath);
                    _outputWriter?.WriteLine($"[UpdateDownloader] Backup metadata: {jsonString}", LogLevel.Debug);
                }

                // Get app directory
                string appDir = AppDomain.CurrentDomain.BaseDirectory;

                // Backup current version first
                await CreateBackupAsync("pre-rollback");

                // Restore from backup
                string[] filesToCopy = Directory.GetFiles(latestBackup);
                foreach (string file in filesToCopy)
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName != BackupMetadataFileName)
                    {
                        string destPath = Path.Combine(appDir, fileName);
                        File.Copy(file, destPath, overwrite: true);
                    }
                }

                _outputWriter?.WriteLine("[UpdateDownloader] Rollback completed successfully", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[UpdateDownloader] Rollback failed: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Removes old backups, keeping only the most recent N backups.
        /// </summary>
        private async Task CleanupOldBackupsAsync()
        {
            try
            {
                string backupsDir = Path.Combine(_appDataPath, BackupsDirName);

                if (!Directory.Exists(backupsDir))
                    return;

                var backups = Directory.GetDirectories(backupsDir);
                
                if (backups.Length <= _maxBackupRetention)
                    return;

                // Sort ascending (oldest first)
                Array.Sort(backups);

                // Delete oldest backups
                int toDelete = backups.Length - _maxBackupRetention;
                for (int i = 0; i < toDelete; i++)
                {
                    try
                    {
                        _outputWriter?.WriteLine($"[UpdateDownloader] Removing old backup: {backups[i]}", LogLevel.Debug);
                        Directory.Delete(backups[i], recursive: true);
                    }
                    catch (Exception ex)
                    {
                        _outputWriter?.WriteLine($"[UpdateDownloader] Failed to delete backup {backups[i]}: {ex.Message}", LogLevel.Warning);
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[UpdateDownloader] Cleanup error: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// Recursively copies a directory.
        /// </summary>
        private void CopyDirectory(string source, string destination)
        {
            var dir = new DirectoryInfo(source);
            Directory.CreateDirectory(destination);

            foreach (FileInfo file in dir.GetFiles())
            {
                file.CopyTo(Path.Combine(destination, file.Name), overwrite: true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                CopyDirectory(subDir.FullName, Path.Combine(destination, subDir.Name));
            }
        }
    }
}
