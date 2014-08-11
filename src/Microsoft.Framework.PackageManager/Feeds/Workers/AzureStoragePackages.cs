using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Framework.PackageManager.Feeds.Workers
{
    public class AzureStoragePackages : AbstractRepositoryPublisher
    {
        private string _accessKey;
        private string _uri;
        HttpClient _client;

        public AzureStoragePackages(string uri, string accessKey)
        {
            _uri = uri;
            _accessKey = accessKey;
            _client = new HttpClient();
        }

        public override Stream ReadArtifactStream(string path)
        {
            var uri = _uri + "/" + path;
            var response = _client.GetAsync(uri).Result;
            if ((int)response.StatusCode != 200)
            {
                return null;
            }
            var stream = response.Content.ReadAsStreamAsync().Result;
            return stream;
        }

        public override void WriteArtifactStream(string path, Stream content, bool createNew)
        {
            var uri = _uri + "/" + path;

            var request = new HttpRequestMessage(HttpMethod.Put, uri)
            {
                Headers =
                {
                    { "x-ms-blob-type", "BlockBlob" },
                    { "x-ms-date",  DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture) },
                    { "x-ms-version", "2009-09-19" },
                },
                Content = new StreamContent(content)
                {
                    Headers =
                    {
                        ContentLength =content.Length,
                        ContentMD5 = MD5.Create().ComputeHash(content),
                    }
                },
            };
            content.Position = 0;

            Transmit(request);
        }

        public override void RemoveArtifact(string path)
        {
            var uri = _uri + "/" + path;

            var request = new HttpRequestMessage(HttpMethod.Delete, uri)
            {
                Headers =
                {
                    { "x-ms-date",  DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture) },
                    { "x-ms-version", "2009-09-19" },
                }
            };

            Transmit(request);
        }

        public override IEnumerable<string> EnumerateArtifacts(Func<string, bool> folderPredicate, Func<string, bool> artifactPredicate)
        {
            throw new NotImplementedException();
        }

        private void Transmit(HttpRequestMessage request)
        {
            Report.WriteLine(
                "  {0} {1}",
                request.Method.ToString().Green(),
                request.RequestUri.ToString().Bold());

            Sign(request);

            var sw = new Stopwatch();
            sw.Start();
            var response = _client.SendAsync(request).Result;

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Report.WriteLine(
                    "  {0} {1}ms",
                    response.StatusCode.ToString().Green().Bold(),
                    sw.ElapsedMilliseconds.ToString().Bold());
            }
            else
            {
                Report.WriteLine(
                    "  {0} {1}\r\n{2}",
                    response.StatusCode.ToString().Red().Bold(),
                    response.ReasonPhrase,
                    response.Content.ReadAsStringAsync().Result);
                throw new HttpRequestException(response.ReasonPhrase);
            }
        }

        private void Sign(HttpRequestMessage request)
        {
            var account = request.RequestUri.Host.Split(new[] { '.' }, 2).FirstOrDefault();
            var method = request.Method.ToString().ToUpperInvariant();

            var contentLength = "0";
            if (request.Content != null &&
                request.Content.Headers.ContentLength != null)
            {
                contentLength = request.Content.Headers.ContentLength.Value.ToString(CultureInfo.InvariantCulture);
            }

            var contentMd5 = "";
            if (request.Content != null &&
                request.Content.Headers.ContentMD5 != null)
            {
                contentMd5 = Convert.ToBase64String(request.Content.Headers.ContentMD5);
            }

            var sig = new Sig(method, account, _accessKey) /*HTTP Verb*/
                .Add("")    /*Content-Encoding*/
                .Add("")    /*Content-Language*/
                .Add(contentLength)    /*Content-Length*/
                .Add(contentMd5)    /*Content-MD5*/
                .Add("")    /*Content-Type*/
                .Add("")    /*Date*/
                .Add("")    /*If-Modified-Since */
                .Add("")    /*If-Match*/
                .Add("")    /*If-None-Match*/
                .Add("")    /*If-Unmodified-Since*/
                .Add("")    /*Range*/
                ;

            IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = request.Headers;
            if (request.Content != null)
            {
                headers = headers.Concat(request.Content.Headers);
            }

            foreach (var header in headers
                .Where(kv => kv.Key.StartsWith("x-ms-"))
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                sig.Add(header.Key, string.Join(",", header.Value));
            }
            sig.Add("/" + account + request.RequestUri.LocalPath);
            request.Headers.Authorization = sig.GetAuthorization();
        }

        class Sig
        {
            private string _value;
            private string _account;
            private string _accessKey;

            public Sig(string method, string account, string accessKey)
            {
                _value = method;
                _account = account;
                _accessKey = accessKey;
            }

            public Sig Add(string value)
            {
                _value = _value + "\n" + value;
                return this;
            }

            public Sig Add(string name, string value)
            {
                _value = _value + "\n" + name + ":" + value;
                return this;
            }

            public AuthenticationHeaderValue GetAuthorization()
            {
                var hmac = new HMACSHA256(Convert.FromBase64String(_accessKey));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(_value));
                return new AuthenticationHeaderValue("SharedKey", _account + ":" + Convert.ToBase64String(hash));
            }

            public override string ToString()
            {
                return _value;
            }
        }
    }
}