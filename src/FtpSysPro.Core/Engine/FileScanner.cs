using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace FtpSysPro.Core.Engine
{
    public class ScannedFile
    {
        public string SourceRoot { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty; // relative to SourceRoot
        public long SizeBytes { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
    }

    public class ScanResult
    {
        public List<ScannedFile> Files { get; set; } = new List<ScannedFile>();
        public List<string> Warnings { get; set; } = new List<string>();
        public long TotalSizeBytes { get; set; }
    }

    public static class FileScanner
    {
        private static readonly HashSet<string> DefaultSystemFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$RECYCLE.BIN",
            "$Recycle.Bin",
            "System Volume Information",
            "Recovery",
            "$WinREAgent",
            "Config.Msi"
        };

        private static readonly HashSet<string> DefaultSystemFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pagefile.sys",
            "swapfile.sys",
            "hiberfil.sys",
            "DumpStack.log",
            "DumpStack.log.tmp"
        };

        public static ScanResult Scan(
            IEnumerable<string> sourceDirectories,
            IEnumerable<string> excludePatterns,
            CancellationToken cancellationToken = default)
        {
            var result = new ScanResult();
            var compiledPatterns = CompilePatterns(excludePatterns);

            foreach (var root in sourceDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(root))
                    continue;

                if (!Directory.Exists(root))
                {
                    result.Warnings.Add($"Source directory does not exist or is inaccessible: {root}");
                    continue;
                }

                ScanDirectoryRecursive(root, root, compiledPatterns, result, cancellationToken);
            }

            return result;
        }

        private static void ScanDirectoryRecursive(
            string root,
            string currentDir,
            List<Regex> excludePatterns,
            ScanResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip reparse points (junctions, directory symbolic links) to prevent infinite loops and recursive traps
            if (!string.Equals(currentDir, root, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var di = new DirectoryInfo(currentDir);
                    if ((di.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        return;
                    }
                }
                catch
                {
                    return;
                }
            }

            // Check if directory itself is excluded or special system volume directory
            var dirName = Path.GetFileName(currentDir.TrimEnd('\\', '/'));
            if (!string.IsNullOrEmpty(dirName) && (DefaultSystemFolders.Contains(dirName) || IsExcluded(dirName, excludePatterns)))
                return;

            string[] subDirs = null;
            string[] files = null;

            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Cannot enumerate subdirectories in '{currentDir}': {ex.Message}");
            }

            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Cannot enumerate files in '{currentDir}': {ex.Message}");
            }

            if (files != null)
            {
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(file);
                    if (DefaultSystemFiles.Contains(fileName) || IsExcluded(fileName, excludePatterns))
                        continue;

                    try
                    {
                        var fi = new FileInfo(file);
                        var relPath = GetRelativePath(root, file);

                        result.Files.Add(new ScannedFile
                        {
                            SourceRoot = root,
                            FullPath = file,
                            RelativePath = relPath,
                            SizeBytes = fi.Length,
                            LastWriteTimeUtc = fi.LastWriteTimeUtc
                        });
                        result.TotalSizeBytes += fi.Length;
                    }
                    catch (Exception ex)
                    {
                        // File locked or inaccessible
                        result.Warnings.Add($"Cannot access file '{file}': {ex.Message}");
                    }
                }
            }

            if (subDirs != null)
            {
                foreach (var subDir in subDirs)
                {
                    ScanDirectoryRecursive(root, subDir, excludePatterns, result, cancellationToken);
                }
            }
        }

        private static string GetRelativePath(string root, string fullPath)
        {
            root = root.TrimEnd('\\', '/');
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var rel = fullPath.Substring(root.Length).TrimStart('\\', '/');
                return rel;
            }
            return Path.GetFileName(fullPath);
        }

        private static bool IsExcluded(string name, List<Regex> patterns)
        {
            foreach (var p in patterns)
            {
                if (p.IsMatch(name))
                    return true;
            }
            return false;
        }

        private static List<Regex> CompilePatterns(IEnumerable<string> patterns)
        {
            var list = new List<Regex>();
            if (patterns == null) return list;

            foreach (var p in patterns)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                var trimmed = p.Trim();
                // Convert wildcard glob to regex: *.tmp -> ^.*\.tmp$
                var regex = "^" + Regex.Escape(trimmed)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".") + "$";
                list.Add(new Regex(regex, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }
            return list;
        }
    }
}
