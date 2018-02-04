// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Net
{
    public class HttpUnsuccessfulResponseException : HttpRequestException
    {
        public static async Task ThrowIfUnsuccessfulAsync(
            HttpResponseMessage response,
            string customMessage = null)
        {
            if (!response.IsSuccessStatusCode)
            {
                string failureContent = await response.Content.ReadAsStringAsync();

                string message =
                    $"{customMessage ?? "HTTP call unsuccessful."} Response status code: " +
                    $"{(int)response.StatusCode} ({response.StatusCode})";

                if (!string.IsNullOrWhiteSpace(failureContent))
                {
                    message += $" with content: '{failureContent}'";
                }

                throw new HttpUnsuccessfulResponseException(
                    response.StatusCode,
                    message,
                    failureContent);
            }
        }

        public HttpStatusCode HttpStatusCode { get; }

        public string Content { get; }

        public HttpUnsuccessfulResponseException(
            HttpStatusCode httpStatusCode,
            string message,
            string content)
            : base(message)
        {
            HttpStatusCode = httpStatusCode;
            Content = content;
        }
    }
}
