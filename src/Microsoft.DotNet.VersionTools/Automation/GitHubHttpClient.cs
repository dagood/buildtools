// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.VersionTools.Automation.PullRequest;
using Newtonsoft.Json.Serialization;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class GitHubHttpClient : HttpClient
    {
        private GitHubAuth _auth;

        public GitHubHttpClient(GitHubAuth auth)
        {
            DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            DefaultRequestHeaders.Add("Authorization", $"token {auth.AuthToken}");
            DefaultRequestHeaders.Add("User-Agent", auth.User);
            _auth = auth;
        }

        public async Task PutGitHubFileAsync(
            string fileUrl,
            string commitMessage,
            string newFileContents)
        {
            Trace.TraceInformation($"Getting the 'sha' of the current contents of file '{fileUrl}'");

            string currentFile = await GetStringAsync(fileUrl);
            string currentSha = JObject.Parse(currentFile)["sha"].ToString();

            Trace.TraceInformation($"Got 'sha' value of '{currentSha}'");

            Trace.TraceInformation($"Request to update file '{fileUrl}' contents to:");
            Trace.TraceInformation(newFileContents);

            string updateFileBody = JsonConvert.SerializeObject(new
            {
                message = commitMessage,
                committer = new
                {
                    name = _auth.User,
                    email = _auth.Email
                },
                content = ToBase64(newFileContents),
                sha = currentSha
            }, Formatting.Indented);

            var bodyContent = new StringContent(updateFileBody);
            using (HttpResponseMessage response = await PutAsync(fileUrl, bodyContent))
            {
                response.EnsureSuccessStatusCode();
                Trace.TraceInformation("Updated the file successfully.");
            }
        }

        public async Task PostGitHubPullRequestAsync(
            string title,
            string description,
            string originOwner,
            string originBranch,
            string upstreamOwner,
            string upstreamBranch,
            string project)
        {
            string createPrBody = JsonConvert.SerializeObject(new
            {
                title = title,
                body = description,
                head = $"{originOwner}:{originBranch}",
                @base = upstreamBranch
            }, Formatting.Indented);

            string pullUrl = $"https://api.github.com/repos/{upstreamOwner}/{project}/pulls";

            var bodyContent = new StringContent(createPrBody);
            using (HttpResponseMessage response = await PostAsync(pullUrl, bodyContent))
            {
                response.EnsureSuccessStatusCode();

                Trace.TraceInformation($"Created pull request.");

                JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());
                string htmlUrl = responseContent["html_url"].ToString();

                Trace.TraceInformation($"Pull request page link: {htmlUrl}");
            }
        }

        public async Task<GitHubPullRequest> FindPullRequestByHeadAsync(
            string owner,
            string project,
            string headPrefix,
            string author,
            string sortType = "created")
        {
            int pullRequestNumber;

            // First: find the number of the pull request.
            string queryString = $"repo:{owner}/{project}+head:{headPrefix}+author:{author}+state:open";
            string queryUrl = $"https://api.github.com/search/issues?q={queryString}&sort={sortType}&order=desc";


            using (HttpResponseMessage response = await GetAsync(queryUrl))
            {
                response.EnsureSuccessStatusCode();
                JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());

                int totalCount = responseContent["total_count"].Value<int>();
                JArray items = (JArray)responseContent["items"];

                if (totalCount == 0)
                {
                    Trace.TraceInformation($"Could not find any pull request with head {headPrefix}");
                    return null;
                }
                if (totalCount > 1)
                {
                    Trace.TraceInformation($"Found multiple pull requests with head {headPrefix}.");

                    IEnumerable<int> allIds = items.Select(item => item["id"].Value<int>());

                    Trace.TraceInformation($"On this page, found {string.Join(", ", allIds)}");
                }

                pullRequestNumber = items.First()["number"].Value<int>();
            }
            // Second: fetch details for the pull request.
            string pullRequestUrl = $"https://api.github.com/repos/{owner}/{project}/pulls/{pullRequestNumber}";

            using (HttpResponseMessage response = await GetAsync(pullRequestUrl))
            {
                response.EnsureSuccessStatusCode();
                return JsonConvert.DeserializeObject<GitHubPullRequest>(
                    await response.Content.ReadAsStringAsync(),
                    new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    });
            }
        }

        private static string ToBase64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }
    }
}
