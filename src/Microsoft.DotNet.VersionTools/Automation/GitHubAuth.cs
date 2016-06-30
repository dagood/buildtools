using System;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class GitHubAuth
    {
        public string AuthToken { get; }
        public string User { get; }
        public string Email { get; }

        public GitHubAuth(
            string authToken,
            string user = null,
            string email = null)
        {
            if (authToken == null)
            {
                throw new ArgumentNullException(nameof(authToken));
            }
            AuthToken = authToken;
            User = user ?? "dotnet-bot";
            Email = email ?? "dotnet-bot@microsoft.com";
        }
    }
}
