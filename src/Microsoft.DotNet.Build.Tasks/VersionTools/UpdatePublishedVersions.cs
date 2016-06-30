using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools.Automation;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class UpdatePublishedVersions : Task
    {
        [Required]
        public ITaskItem[] ShippedNuGetPackage { get; set; }

        [Required]
        public string VersionsRepoPath { get; set; }

        [Required]
        public string GitHubAuthToken { get; set; }
        public string GitHubUser { get; set; }
        public string GitHubEmail { get; set; }

        public string VersionRepo { get; set; }
        public string VersionRepoOwner { get; set; }

        public override bool Execute()
        {
            MsBuildTraceListener[] listeners = Trace.Listeners.AddMsBuildTraceListeners(Log);

            var gitHubAuth = new GitHubAuth(GitHubAuthToken, GitHubUser, GitHubEmail);

            var updater = new VersionRepoUpdater(gitHubAuth, VersionRepoOwner, VersionRepo);

            updater.UpdateBuildInfoAsync(
                ShippedNuGetPackage.Select(item => item.ItemSpec),
                VersionsRepoPath)
                .Wait();

            Trace.Listeners.RemoveMsBuildTraceListeners(listeners);

            return true;
        }
    }
}
