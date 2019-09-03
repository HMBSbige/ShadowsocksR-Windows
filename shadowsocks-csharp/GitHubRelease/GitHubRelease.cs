﻿using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Shadowsocks.GitHubRelease
{
    public class GitHubRelease
    {
        private readonly string _owner;
        private readonly string _repo;

        private const string UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.100 Safari/537.36";

        private string LatestReleaseUrl => $@"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
        private string AllReleaseUrl => $@"https://api.github.com/repos/{_owner}/{_repo}/releases";

        public GitHubRelease(string owner, string repo)
        {
            _owner = owner;
            _repo = repo;
        }

        public async Task<string> GetLatestAsync()
        {
            return await Get(LatestReleaseUrl, true);
        }

        public async Task<string> GetAllReleaseAsync()
        {
            return await Get(AllReleaseUrl, true);
        }

        private static async Task<string> Get(string url, bool useProxy = false)
        {
            var httpClientHandler = new HttpClientHandler
            {
                UseProxy = useProxy
            };

            var httpClient = new HttpClient(httpClientHandler);
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(@"User-Agent", UserAgent);

            var response = await httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();
            var resultStr = await response.Content.ReadAsStringAsync();

            Debug.WriteLine(resultStr);
            return resultStr;
        }
    }
}
