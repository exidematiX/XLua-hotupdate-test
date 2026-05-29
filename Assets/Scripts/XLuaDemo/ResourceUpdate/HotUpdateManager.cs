using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace XLuaDemo.ResourceUpdate
{
    public sealed class HotUpdateManager
    {
        private const int MaxRetryCount = 3;
        private const string ManifestName = "manifest.json";
        private const int DownloadBufferSize = 64 * 1024;

        public IEnumerator UpdateFromRemote(string remoteBaseUrl, string localRoot, Action<string> log, Action<float> progress)
        {
            Directory.CreateDirectory(localRoot);
            progress(0f);

            string manifestUrl = CombineUri(remoteBaseUrl, ManifestName);
            string remoteJson = null;
            yield return DownloadTextWithRetry(manifestUrl, log, result => remoteJson = result);

            if (string.IsNullOrEmpty(remoteJson))
            {
                log("Remote manifest unavailable. Demo continues with local files if present.");
                progress(1f);
                yield break;
            }

            ResourceManifest remoteManifest = JsonUtility.FromJson<ResourceManifest>(remoteJson);
            if (remoteManifest == null || remoteManifest.files == null)
            {
                log("Remote manifest parse failed.");
                progress(1f);
                yield break;
            }

            string localManifestVersion = ReadLocalManifestVersion(localRoot);
            log("Local manifest version: " + (string.IsNullOrEmpty(localManifestVersion) ? "<none>" : localManifestVersion));
            log("Remote manifest version: " + remoteManifest.version + ", files: " + remoteManifest.files.Length);

            string rollbackRoot = Path.Combine(localRoot, "_rollback_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            List<string> newFiles = new List<string>();
            bool failed = false;
            int total = remoteManifest.files.Length;
            int completed = 0;
            foreach (ResourceFile file in remoteManifest.files)
            {
                if (file == null || string.IsNullOrEmpty(file.path))
                {
                    completed++;
                    progress(total == 0 ? 1f : completed / (float)total);
                    continue;
                }

                string localPath = GetLocalFilePath(localRoot, file.path);
                if (IsLocalFileValid(localPath, file))
                {
                    log("Skip unchanged: " + file.path);
                }
                else
                {
                    string url = string.IsNullOrEmpty(file.url) ? CombineUri(remoteBaseUrl, file.path) : ResolveFileUrl(remoteBaseUrl, file.url);
                    bool downloaded = false;
                    yield return DownloadFileWithRetry(url, localRoot, localPath, file, rollbackRoot, newFiles, log, result => downloaded = result);
                    if (!downloaded)
                    {
                        failed = true;
                        break;
                    }
                }

                completed++;
                progress(total == 0 ? 1f : completed / (float)total);
            }

            if (failed)
            {
                RollbackFiles(rollbackRoot, localRoot, newFiles, log);
                log("Incremental update failed. Rolled back changed files.");
                progress(1f);
                yield break;
            }

            File.WriteAllText(Path.Combine(localRoot, ManifestName), remoteJson, Encoding.UTF8);
            CleanupRollback(rollbackRoot);
            log("Incremental update finished.");
            progress(1f);
        }

        private static IEnumerator DownloadTextWithRetry(string url, Action<string> log, Action<string> onSuccess)
        {
            for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = 10;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        onSuccess(request.downloadHandler.text);
                        yield break;
                    }

                    log(string.Format("Manifest request failed ({0}/{1}): {2}", attempt, MaxRetryCount, request.error));
                }

                yield return new WaitForSeconds(0.35f * attempt);
            }
        }

        private static IEnumerator DownloadFileWithRetry(
            string url,
            string localRoot,
            string localPath,
            ResourceFile expected,
            string rollbackRoot,
            List<string> newFiles,
            Action<string> log,
            Action<bool> onComplete)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(localPath));
            string tempPath = localPath + ".download";
            long existingBytes = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0L;

            for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    if (existingBytes > 0)
                    {
                        request.SetRequestHeader("Range", "bytes=" + existingBytes + "-");
                    }

                    request.timeout = 20;
                    request.disposeDownloadHandlerOnDispose = false;
                    using (ResumeDownloadHandler handler = new ResumeDownloadHandler(tempPath, existingBytes > 0))
                    {
                        request.downloadHandler = handler;
                        yield return request.SendWebRequest();

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            bool acceptedRange = request.responseCode == 206;
                            if (existingBytes > 0 && !acceptedRange && handler.BytesWritten > 0)
                            {
                                log("Server ignored range request, restarted download: " + expected.path);
                            }

                            if (IsLocalFileValid(tempPath, expected))
                            {
                                bool existedBeforeReplace = File.Exists(localPath);
                                BackupExistingFile(localRoot, localPath, rollbackRoot);
                                if (existedBeforeReplace)
                                {
                                    File.Delete(localPath);
                                }

                                File.Move(tempPath, localPath);
                                if (!existedBeforeReplace)
                                {
                                    newFiles.Add(localPath);
                                }

                                log("Downloaded: " + expected.path);
                                onComplete(true);
                                yield break;
                            }

                            existingBytes = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0L;
                            if (expected.size > 0 && existingBytes > expected.size)
                            {
                                File.Delete(tempPath);
                                existingBytes = 0L;
                            }

                            log("Downloaded file validation failed, will retry: " + expected.path);
                        }
                        else
                        {
                            log(string.Format("Download failed ({0}/{1}) {2}: {3}", attempt, MaxRetryCount, expected.path, request.error));
                        }
                    }
                }

                existingBytes = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0L;
                yield return new WaitForSeconds(0.5f * attempt);
            }

            log("Giving up after retries: " + expected.path);
            onComplete(false);
        }

        private static bool IsLocalFileValid(string localPath, ResourceFile file)
        {
            if (!File.Exists(localPath))
            {
                return false;
            }

            FileInfo info = new FileInfo(localPath);
            if (file.size > 0 && info.Length != file.size)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(file.md5))
            {
                return string.Equals(CalculateMd5(localPath), file.md5, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static string GetLocalFilePath(string localRoot, string relativePath)
        {
            return Path.Combine(localRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ResolveFileUrl(string remoteBaseUrl, string url)
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            return CombineUri(remoteBaseUrl, url);
        }

        private static string ReadLocalManifestVersion(string localRoot)
        {
            string manifestPath = Path.Combine(localRoot, ManifestName);
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            ResourceManifest manifest = JsonUtility.FromJson<ResourceManifest>(File.ReadAllText(manifestPath));
            return manifest == null ? null : manifest.version;
        }

        private static void BackupExistingFile(string localRoot, string localPath, string rollbackRoot)
        {
            if (!File.Exists(localPath))
            {
                return;
            }

            string relative = localPath.StartsWith(localRoot, StringComparison.OrdinalIgnoreCase)
                ? localPath.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : Path.GetFileName(localPath);
            string backupPath = Path.Combine(rollbackRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
            File.Copy(localPath, backupPath, true);
        }

        private static void RollbackFiles(string rollbackRoot, string localRoot, List<string> newFiles, Action<string> log)
        {
            foreach (string newFile in newFiles)
            {
                if (File.Exists(newFile))
                {
                    File.Delete(newFile);
                    log("Removed new file from failed update: " + newFile);
                }
            }

            if (!Directory.Exists(rollbackRoot))
            {
                return;
            }

            foreach (string backupPath in Directory.GetFiles(rollbackRoot, "*", SearchOption.AllDirectories))
            {
                string relative = backupPath.Substring(rollbackRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(localRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(backupPath, target, true);
                log("Rolled back: " + target);
            }

            CleanupRollback(rollbackRoot);
        }

        private static void CleanupRollback(string rollbackRoot)
        {
            if (Directory.Exists(rollbackRoot))
            {
                Directory.Delete(rollbackRoot, true);
            }
        }

        private static string CombineUri(string baseUrl, string relativePath)
        {
            return baseUrl.TrimEnd('/', '\\') + "/" + relativePath.Replace("\\", "/").TrimStart('/');
        }

        private static string CalculateMd5(string path)
        {
            using (MD5 md5 = MD5.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = md5.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private sealed class ResumeDownloadHandler : DownloadHandlerScript
        {
            private readonly FileStream stream;

            public long BytesWritten { get; private set; }

            public ResumeDownloadHandler(string path, bool append)
                : base(new byte[DownloadBufferSize])
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, DownloadBufferSize);
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                {
                    return true;
                }

                stream.Write(data, 0, dataLength);
                BytesWritten += dataLength;
                return true;
            }

            protected override void CompleteContent()
            {
                stream.Flush();
            }

            protected void Dispose(bool disposing)
            {
                if (disposing)
                {
                    stream.Dispose();
                }

                //base.Dispose(disposing);
            }
        }
    }
}
