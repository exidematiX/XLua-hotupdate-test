using System;
using System.Collections;
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

            log("Remote manifest version: " + remoteManifest.version + ", files: " + remoteManifest.files.Length);

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
                    yield return DownloadFileWithRetry(url, localPath, file, log);
                }

                completed++;
                progress(total == 0 ? 1f : completed / (float)total);
            }

            File.WriteAllText(Path.Combine(localRoot, ManifestName), remoteJson, Encoding.UTF8);
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

        private static IEnumerator DownloadFileWithRetry(string url, string localPath, ResourceFile expected, Action<string> log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(localPath));
            string tempPath = localPath + ".download";

            for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = 20;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        File.WriteAllBytes(tempPath, request.downloadHandler.data);
                        if (IsLocalFileValid(tempPath, expected))
                        {
                            if (File.Exists(localPath))
                            {
                                File.Delete(localPath);
                            }

                            File.Move(tempPath, localPath);
                            log("Downloaded: " + expected.path);
                            yield break;
                        }

                        File.Delete(tempPath);
                        log("Downloaded file validation failed: " + expected.path);
                    }
                    else
                    {
                        log(string.Format("Download failed ({0}/{1}) {2}: {3}", attempt, MaxRetryCount, expected.path, request.error));
                    }
                }

                yield return new WaitForSeconds(0.5f * attempt);
            }

            log("Giving up after retries: " + expected.path);
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
    }
}
