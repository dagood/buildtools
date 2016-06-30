using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.VersionTools.Automation
{
    class NupkgNameInfo
    {
        private static Regex s_nugetFileRegex = new Regex(
            @"^(?<id>.*?)\.(?<version>([0-9]+\.)?[0-9]+\.[0-9]+(-(?<prerelease>[A-z0-9-]+))?)(?<symbols>\.symbols)?\.nupkg$");

        public NupkgNameInfo(string path)
        {
            Match match = s_nugetFileRegex.Match(Path.GetFileName(path));

            Id = match.Groups["id"].Value;
            Version = match.Groups["version"].Value;
            Prerelease = match.Groups["prerelease"].Value;
            SymbolPackage = !string.IsNullOrEmpty(match.Groups["symbols"].Value);
        }

        public string Id { get; set; }
        public string Version { get; set; }
        public string Prerelease { get; set; }
        public bool SymbolPackage { get; set; }
    }
}
