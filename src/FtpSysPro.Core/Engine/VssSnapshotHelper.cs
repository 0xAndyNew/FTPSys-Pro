using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;

namespace FtpSysPro.Core.Engine
{
    /// <summary>
    /// Manages Volume Shadow Copy (VSS) snapshots using Windows native vssadmin utility or WMI Win32_ShadowCopy.
    /// Provides transparent access to locked/in-use files by creating a shadow copy of the volume,
    /// creating a temporary symbolic directory link to it, and releasing the shadow copy when done.
    /// </summary>
    public class VssSnapshotContext : IDisposable
    {
        private readonly List<string> _createdShadowIds = new List<string>();
        private readonly Dictionary<string, string> _volumeToShadowPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _createdSymlinks = new List<string>();
        private readonly string _tempMountDir;
        private bool _disposed;

        public bool IsActive => _volumeToShadowPath.Count > 0;
        public List<string> Warnings { get; } = new List<string>();

        public VssSnapshotContext(string tempBaseDir = null)
        {
            var baseDir = tempBaseDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FtpSysPro", "temp");
            _tempMountDir = Path.Combine(baseDir, "vss_mounts_" + Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Attempts to create shadow copies for all unique local volume drives present in the source directories.
        /// Returns a mapping function that transforms a regular file path to its shadow copy path if available.
        /// </summary>
        public bool TryInitializeForDirectories(IEnumerable<string> directories)
        {
            if (directories == null) return false;

            var uniqueVolumes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in directories)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    var root = Path.GetPathRoot(Path.GetFullPath(dir));
                    if (!string.IsNullOrEmpty(root) && !root.StartsWith(@"\\") && root.Length >= 2 && root[1] == ':')
                    {
                        uniqueVolumes.Add(root.TrimEnd('\\'));
                    }
                }
                catch { }
            }

            if (uniqueVolumes.Count == 0)
                return false;

            try
            {
                Directory.CreateDirectory(_tempMountDir);

                foreach (var vol in uniqueVolumes)
                {
                    try
                    {
                        CreateShadowCopyForVolume(vol);
                    }
                    catch (Exception ex)
                    {
                        Warnings.Add($"Không thể tạo VSS shadow copy cho ổ '{vol}': {ex.Message}. Sẽ đọc file trực tiếp.");
                    }
                }

                return _volumeToShadowPath.Count > 0;
            }
            catch (Exception ex)
            {
                Warnings.Add($"Lỗi khởi tạo VSS: {ex.Message}");
                return false;
            }
        }

        private void CreateShadowCopyForVolume(string volumeDrive)
        {
            // volumeDrive like "C:"
            var volLetter = volumeDrive.TrimEnd('\\').Substring(0, 1);
            var volParam = volLetter + ":";

            string shadowDevice = null;
            string shadowId = null;

            // Strategy 1: Attempt native vssadmin create shadow (supported on Windows Server)
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vssadmin.exe"),
                    Arguments = $"create shadow /for={volParam}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd();
                        var error = proc.StandardError.ReadToEnd();
                        proc.WaitForExit(60000);

                        if (proc.ExitCode == 0)
                        {
                            var match = Regex.Match(output, @"(\\\\(\?|.)\\GLOBALROOT\\Device\\HarddiskVolumeShadowCopy\d+)", RegexOptions.IgnoreCase);
                            if (!match.Success)
                                match = Regex.Match(output, @"(HarddiskVolumeShadowCopy\d+)", RegexOptions.IgnoreCase);

                            if (match.Success)
                            {
                                shadowDevice = match.Value;
                                if (!shadowDevice.StartsWith(@"\\"))
                                    shadowDevice = @"\\?\GLOBALROOT\Device\" + shadowDevice;

                                var idMatch = Regex.Match(output, @"\{[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}\}");
                                if (idMatch.Success)
                                    shadowId = idMatch.Value;
                            }
                        }
                    }
                }
            }
            catch { }

            // Strategy 2: If vssadmin not supported (Windows 10/11 client), use WMI Win32_ShadowCopy
            if (string.IsNullOrEmpty(shadowDevice))
            {
                if (TryCreateShadowCopyWmi(volLetter, out var wmiId, out var wmiDevice))
                {
                    shadowId = wmiId;
                    shadowDevice = wmiDevice;
                }
            }

            if (string.IsNullOrEmpty(shadowDevice))
            {
                throw new InvalidOperationException($"VSS shadow copy creation is not supported on this volume or system for '{volumeDrive}'");
            }

            if (!string.IsNullOrEmpty(shadowId))
            {
                _createdShadowIds.Add(shadowId);
            }

            // Create a directory symbolic link using mklink /d so standard .NET file APIs can traverse it
            var linkPath = Path.Combine(_tempMountDir, "vol_" + volLetter);
            CreateSymlink(linkPath, shadowDevice + @"\");
            _createdSymlinks.Add(linkPath);

            _volumeToShadowPath[volumeDrive] = linkPath;
        }

        private bool TryCreateShadowCopyWmi(string volLetter, out string shadowId, out string shadowDevice)
        {
            shadowId = null;
            shadowDevice = null;
            try
            {
                var volPath = volLetter + @":\";
                using (var shadowClass = new ManagementClass("Win32_ShadowCopy"))
                {
                    var inParams = shadowClass.GetMethodParameters("Create");
                    inParams["Volume"] = volPath;
                    inParams["Context"] = "ClientAccessible";

                    using (var outParams = shadowClass.InvokeMethod("Create", inParams, null))
                    {
                        if (outParams != null)
                        {
                            var retVal = Convert.ToUInt32(outParams["ReturnValue"]);
                            if (retVal == 0)
                            {
                                shadowId = outParams["ShadowID"]?.ToString();
                                if (!string.IsNullOrEmpty(shadowId))
                                {
                                    using (var searcher = new ManagementObjectSearcher($"SELECT DeviceObject FROM Win32_ShadowCopy WHERE ID = '{shadowId}'"))
                                    using (var results = searcher.Get())
                                    {
                                        foreach (ManagementObject obj in results)
                                        {
                                            var dev = obj["DeviceObject"]?.ToString();
                                            if (!string.IsNullOrEmpty(dev))
                                            {
                                                shadowDevice = dev;
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static void CreateSymlink(string linkPath, string targetPath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /d \"{linkPath}\" \"{targetPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            {
                proc?.WaitForExit(10000);
            }
        }

        /// <summary>
        /// Maps an original file path (e.g. C:\Data\db.mdf) to its VSS shadow copy path if available.
        /// If not available or mapping fails, returns the original path.
        /// </summary>
        public string MapPathToShadow(string originalFilePath)
        {
            if (string.IsNullOrEmpty(originalFilePath) || _volumeToShadowPath.Count == 0)
                return originalFilePath;

            try
            {
                var full = Path.GetFullPath(originalFilePath);
                var root = Path.GetPathRoot(full).TrimEnd('\\');

                if (_volumeToShadowPath.TryGetValue(root, out var shadowMount))
                {
                    var relative = full.Substring(root.Length).TrimStart('\\');
                    var mapped = Path.Combine(shadowMount, relative);
                    if (File.Exists(mapped) || Directory.Exists(mapped))
                    {
                        return mapped;
                    }
                }
            }
            catch { }

            return originalFilePath;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 1. Remove directory symlinks
            foreach (var link in _createdSymlinks)
            {
                try
                {
                    if (Directory.Exists(link))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c rmdir \"{link}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (var p = Process.Start(psi))
                        {
                            p?.WaitForExit(5000);
                        }
                    }
                }
                catch { }
            }

            // 2. Delete created shadow copies via vssadmin
            foreach (var shadowId in _createdShadowIds)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vssadmin.exe"),
                        Arguments = $"delete shadows /shadow={shadowId} /quiet",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        p?.WaitForExit(15000);
                    }
                }
                catch { }
            }

            // 3. Remove temp mount directory
            try
            {
                if (Directory.Exists(_tempMountDir))
                    Directory.Delete(_tempMountDir, true);
            }
            catch { }
        }
    }
}
