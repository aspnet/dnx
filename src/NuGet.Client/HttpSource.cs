// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Client
{
    internal class HttpSource
    {
        private HttpClient _client;

        private string _baseUri;
        private string _userName;
        private string _password;
        private ILogger _logger;
#if DNXCORE50
        private string _proxyUserName;
        private string _proxyPassword;
#endif

        public HttpSource(
            string baseUri,
            string userName,
            string password,
            ILogger logger)
        {
            _baseUri = baseUri + (baseUri.EndsWith("/") ? "" : "/");
            _userName = userName;
            _password = password;
            _logger = logger;

            var proxy = Environment.GetEnvironmentVariable("http_proxy");
            if (string.IsNullOrEmpty(proxy))
            {
#if DNX451
                _client = new HttpClient();
#else
                _client = new HttpClient(new Microsoft.Net.Http.Client.ManagedHandler());
#endif
            }
            else
            {
                // To use an authenticated proxy, the proxy address should be in the form of
                // "http://user:password@proxyaddress.com:8888"
                var proxyUriBuilder = new UriBuilder(proxy);
#if DNX451
                var webProxy = new WebProxy(proxy);
                if (string.IsNullOrEmpty(proxyUriBuilder.UserName))
                {
                    // If no credentials were specified we use default credentials
                    webProxy.Credentials = CredentialCache.DefaultCredentials;
                }
                else
                {
                    ICredentials credentials = new NetworkCredential(proxyUriBuilder.UserName,
                        proxyUriBuilder.Password);
                    webProxy.Credentials = credentials;
                }

                var handler = new HttpClientHandler
                {
                    Proxy = webProxy,
                    UseProxy = true
                };
                _client = new HttpClient(handler);
#else
                if (!string.IsNullOrEmpty(proxyUriBuilder.UserName))
                {
                    _proxyUserName = proxyUriBuilder.UserName;
                    _proxyPassword = proxyUriBuilder.Password;
                }

                _client = new HttpClient(new Microsoft.Net.Http.Client.ManagedHandler()
                {
                    ProxyAddress = new Uri(proxy)
                });
#endif
            }
        }

        internal async Task<HttpSourceResult> GetAsync(string uri, string cacheKey, TimeSpan cacheAgeLimit)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var result = await TryCache(uri, cacheKey, cacheAgeLimit);
            if (result.Stream != null)
            {
                _logger.WriteQuiet(string.Format("  {0} {1}", "CACHE".Green(), uri));
                return result;
            }

            _logger.WriteQuiet(string.Format("  {0} {1}.", "GET".Yellow(), uri));

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (_userName != null)
            {
                var token = Convert.ToBase64String(Encoding.ASCII.GetBytes(_userName + ":" + _password));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            };

#if DNXCORE50
            if (_proxyUserName != null)
            {
                var proxyToken = Convert.ToBase64String(Encoding.ASCII.GetBytes(_proxyUserName + ":" + _proxyPassword));
                request.Headers.ProxyAuthorization = new AuthenticationHeaderValue("Basic", proxyToken);
            }
#endif

            var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var newFile = result.CacheFileName + "-new";

            // Zero value of TTL means we always download the latest package
            // So we write to a temp file instead of cache
            if (cacheAgeLimit.Equals(TimeSpan.Zero))
            {
                result.CacheFileName = Path.GetTempFileName();
                newFile = Path.GetTempFileName();
            }

            // The update of a cached file is divided into two steps:
            // 1) Delete the old file. 2) Create a new file with the same name.
            // To prevent race condition among multiple processes, here we use a lock to make the update atomic.
            await ConcurrencyUtilities.ExecuteWithFileLocked(result.CacheFileName, async _ =>
            {
                using (var stream = CreateAsyncFileStream(
                    newFile,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    await response.Content.CopyToAsync(stream);
                    await stream.FlushAsync();
                }

                if (File.Exists(result.CacheFileName))
                {
                    // Process B can perform deletion on an opened file if the file is opened by process A
                    // with FileShare.Delete flag. However, the file won't be actually deleted until A close it.
                    // This special feature can cause race condition, so we never delete an opened file.
                    if (!IsFileAlreadyOpen(result.CacheFileName))
                    {
                        File.Delete(result.CacheFileName);
                    }
                }

                // If the destination file doesn't exist, we can safely perform moving operation.
                // Otherwise, moving operation will fail.
                if (!File.Exists(result.CacheFileName))
                {
                    File.Move(
                        newFile,
                        result.CacheFileName);
                }

                // Even the file deletion operation above succeeds but the file is not actually deleted,
                // we can still safely read it because it means that some other process just updated it
                // and we don't need to update it with the same content again.
                result.Stream = CreateAsyncFileStream(
                    result.CacheFileName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete);

                return 0;
            });

            _logger.WriteQuiet(string.Format("  {1} {0} {2}ms", uri, response.StatusCode.ToString().Green(), sw.ElapsedMilliseconds.ToString().Bold()));

            return result;
        }

        private async Task<HttpSourceResult> TryCache(string uri, string cacheKey, TimeSpan cacheAgeLimit)
        {
            var baseFolderName = RemoveInvalidFileNameChars(ComputeHash(_baseUri));
            var baseFileName = RemoveInvalidFileNameChars(cacheKey) + ".dat";

#if DNX451
            var localAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#else
            var localAppDataFolder = Environment.GetEnvironmentVariable("LocalAppData");
#endif
            var cacheFolder = Path.Combine(localAppDataFolder, "dnu", "cache", baseFolderName);
            var cacheFile = Path.Combine(cacheFolder, baseFileName);

            if (!Directory.Exists(cacheFolder) && !cacheAgeLimit.Equals(TimeSpan.Zero))
            {
                Directory.CreateDirectory(cacheFolder);
            }

            // Acquire the lock on a file before we open it to prevent this process
            // from opening a file deleted by the logic in HttpSource.GetAsync() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLocked(cacheFile, _ =>
            {
                if (File.Exists(cacheFile))
                {
                    var fileInfo = new FileInfo(cacheFile);
#if DNX451
                    var age = DateTime.UtcNow.Subtract(fileInfo.LastWriteTimeUtc);
#else
                    var age = DateTime.Now.Subtract(fileInfo.LastWriteTime);
#endif
                    if (age < cacheAgeLimit)
                    {
                        var stream = CreateAsyncFileStream(
                            cacheFile,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read | FileShare.Delete);

                        return Task.FromResult(new HttpSourceResult
                        {
                            CacheFileName = cacheFile,
                            Stream = stream,
                        });
                    }
                }

                return Task.FromResult(new HttpSourceResult
                {
                    CacheFileName = cacheFile,
                });
            });
        }

        private static string ComputeHash(string value)
        {
            var trailing = value.Length > 32 ? value.Substring(value.Length - 32) : value;
            var sha = SHA1.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var hex = "0123456789abcdef";
            return hash.Aggregate("$" + trailing, (result, ch) => "" + hex[ch / 0x10] + hex[ch % 0x10] + result);
        }

        private static string RemoveInvalidFileNameChars(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new String(
                    value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()
                )
                .Replace("__", "_")
                .Replace("__", "_");
        }

        private static bool IsFileAlreadyOpen(string filePath)
        {
            FileStream stream = null;

            try
            {
                stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch
            {
                return true;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            return false;
        }

        private static FileStream CreateAsyncFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        {
#if DNX451
            return new FileStream(path, mode, access, share, bufferSize: 8192, useAsync: true);
#else
            return new FileStream(path, mode, access, share);
#endif
        }
    }
}
