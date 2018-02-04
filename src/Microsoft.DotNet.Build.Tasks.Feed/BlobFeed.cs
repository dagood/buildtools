// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.CloudTestTasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public sealed class BlobFeed
    {
        private MSBuild.TaskLoggingHelper Log;

        public string AccountName { get; set; }

        public string AccountKey { get; set; }

        public string ContainerName { get; set; }

        public string RelativePath { get; set; }


        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = TokenSource.Token;

        public BlobFeed(string accountName, string accountKey, string containerName, string relativePath, MSBuild.TaskLoggingHelper loggingHelper)
        {
            AccountName = accountName;
            AccountKey = accountKey;
            ContainerName = containerName;
            Log = loggingHelper;
            RelativePath = relativePath;
        }

        public string FeedContainerUrl => AzureHelper.GetContainerRestUrl(AccountName, ContainerName);

        public async Task<bool> CheckIfBlobExistsAsync(string blobPath)
        {
            string url = $"{FeedContainerUrl}/{blobPath}?comp=metadata";
            using (HttpClient client = AzureHelper.CreateClient())
            {
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

        public async Task<string> DownloadBlobAsStringAsync(string blobPath)
        {
            return await DownloadBlobAsync(
                blobPath,
                response => response.Content.ReadAsStringAsync());
        }

        public async Task<byte[]> DownloadBlobAsBytesAsync(string blobPath)
        {
            return await DownloadBlobAsync(
                blobPath,
                response => response.Content.ReadAsByteArrayAsync());
        }

        private async Task<TResult> DownloadBlobAsync<TResult>(
            string blobPath,
            Func<HttpResponseMessage, Task<TResult>> process)
        {
            try
            {
                using (HttpResponseMessage response = await AzureHelper.DownloadBlobAsync(
                    Log,
                    AccountName,
                    AccountKey,
                    ContainerName,
                    blobPath))
                {
                    return await process(response);
                }
            }
            catch (HttpRequestException)
            {
                return default(TResult);
            }
        }
    }
}
