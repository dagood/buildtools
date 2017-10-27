using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.VersionTools.Dependencies.Repository
{
    public class FileRepoUpdater : IDependencyUpdater
    {
        public string Repository { get; set; }
        public string Ref { get; set; }

        public string LocalRootDir { get; set; }
        public string RemoteRootDir { get; set; }

        public string[] RelativePaths { get; set; }

        private GitHubClient ProvidedClient { get; set; }

        public IEnumerable<DependencyUpdateTask> GetUpdateTasks(
            IEnumerable<IDependencyInfo> dependencyInfos)
        {
            RepositoryDependencyInfo matchingInfo = DependencyInfoUtils
                .FindRepositoryDependencyInfo(dependencyInfos, Repository, Ref);

            UseClient(client =>
            {
                foreach (string path in RelativePaths)
                {
                    var remoteContents = client.GetGitHubFileContentsAsync(
                        string.Join("/", RemoteRootDir, path),
                        GitHubProject.ParseUrl(Repository),
                        Ref)
                        .Result;

                    
                }
            });

            matchingInfo.Commit;
        }

        private void UseClient(Action<GitHubClient> action)
        {
            if (ProvidedClient != null)
            {
                action(ProvidedClient);
            }
            else
            {
                using (var client = new GitHubClient(null))
                {
                    action(client);
                }
            }
        }
    }
}
