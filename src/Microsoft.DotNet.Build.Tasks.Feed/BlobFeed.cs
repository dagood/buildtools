// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.CloudTestTasks;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public sealed class BlobFeed
    {
        private MSBuild.TaskLoggingHelper Log;

        public string AccountName { get; }

        public string AccountKey { get; }

        public string ContainerName { get; }

        public string RelativePath { get; }

        public string ConnectionString { get; }

        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = TokenSource.Token;

        public BlobFeed(string accountName, string accountKey, string containerName, string relativePath, MSBuild.TaskLoggingHelper loggingHelper)
        {
            AccountName = accountName;
            AccountKey = accountKey;
            ContainerName = containerName;
            RelativePath = relativePath;
            ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={AccountName};AccountKey={AccountKey};EndpointSuffix=core.windows.net";
            Log = loggingHelper;
        }

        public string FeedContainerUrl => AzureHelper.GetContainerRestUrl(AccountName, ContainerName);

        public async Task<bool> CheckIfBlobExists(string blobPath)
        {
            string url = $"{FeedContainerUrl}/{blobPath}?comp=metadata";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();
                var request = AzureHelper.RequestMessage("GET", url, AccountName, AccountKey).Invoke();
                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            $"Blob {blobPath} exists for {AccountName}: Status Code:{response.StatusCode} Status Desc: {await response.Content.ReadAsStringAsync()}");
                    }
                    else
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            $"Blob {blobPath} does not exist for {AccountName}: Status Code:{response.StatusCode} Status Desc: {await response.Content.ReadAsStringAsync()}");
                    }
                    return response.IsSuccessStatusCode;
                }
            }
        }

        public async Task<string> DownloadBlobAsString(string blobPath)
        {
            string url = $"{FeedContainerUrl}/{blobPath}";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();
                var request = AzureHelper.RequestMessage("GET", url, AccountName, AccountKey)();
                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                    return null;
                }
            }
        }

        public AzureBlobLease GetLease(string blobName, TimeSpan? maxWait = null, TimeSpan? delay = null)
        {
            return new AzureBlobLease(
                AccountName,
                AccountKey,
                ConnectionString,
                ContainerName,
                blobName,
                Log,
                maxWait?.ToString(),
                delay?.ToString());
        }
    }
}
