using NuGet.Versioning;

namespace Microsoft.DotNet.VersionTools
{
    public class PackageInfo
    {
        public string Id { get; }
        public NuGetVersion Version { get; }

        public PackageInfo(string id, NuGetVersion version)
        {
            Id = id;
            Version = version;
        }
    }
}
