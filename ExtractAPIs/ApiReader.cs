using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractAPIs
{
    public class Api
    {
        public string method;
        public string path;
        public string appPermissions;
        public string delegatedPermissions;
        public string appPermissionsDocs;
        public string delegatedPermissionsDocs;
        public string endpoint;
        public string owner;
        public bool hasGranularPermissions;
        public bool inV1 = false; // only filled in very late in the program
        public string docFilePath;
        public string docUrl;

        public string ShortName
        {
            get => $"{method} {path}";
        }

        public string SortHandle
        {
            get
            {
                int rank = 0;
                switch (method)
                {
                    case "GET": rank = 1; break;
                    case "PUT": rank = 4; break;
                    case "POST": rank = 2; break;
                    case "PATCH": rank = 3; break;
                    case "DELETE": rank = 5; break;
                }

                string[] parts = path.Split('/').Select(s => s.PadRight(25).Replace(' ', 'a')).ToArray();
                string all = $"{String.Join("/", parts)} {rank}";
                return all;
            }
        }
    }

    public class Ownership
    {
        public string Name;
        public string[] KeywordsInPath;
    }

    // Equality comparison by method and URL
    class ApiComparer : IEqualityComparer<Api>
    {
        public bool Equals(Api x, Api y) => x.method == y.method && x.path == y.path;
        public int GetHashCode(Api obj) => (obj.method + obj.path).GetHashCode();
    }

    public class ApiReader
    {
        public static Api[] ReadApis(string docDirectoryPath, Ownership[] ownershipMap, string[] requiredWords, string docUrlSuffix)
        {
            List<Api> apis = new List<Api>();

            foreach (var path in Directory.EnumerateFiles(docDirectoryPath))
            {
                string shortPath = path.Replace(Program.rootpath + "\\", "");
                if (Path.GetExtension(path).ToLowerInvariant() == ".md")
                {
                    IEnumerable<Api> newApis = ReadFile(path, ownershipMap, requiredWords,
                        docUrl: "https://docs.microsoft.com/graph/api/" + path.Substring(docDirectoryPath.Length + 1).Replace(".md", "") + docUrlSuffix);
                    apis.AddRange(newApis);
                }
            }

            Api[] result = apis.Distinct(new ApiComparer()).OrderBy(api => api.SortHandle).ToArray();
            return result;
        }

        private static IEnumerable<Api> ReadFile(string docFilePath, Ownership[] ownershipMap, string[] requiredWords, string docUrl)
        {
            string endpoint = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(docFilePath)));

            string[] lines = File.ReadAllLines(docFilePath);
            lines = LinesBefore(lines, line => line.StartsWith("##") &&
                    (line.EndsWith("Example") || line.EndsWith("Examples")))
                .ToArray();

            var teamsHttpCalls = lines.Skip(1)
                .Where(line =>
                    ContainsAnyWord(line, requiredWords)
                    &&
                    (line.Trim().StartsWith("GET")
                    || line.Trim().StartsWith("PUT")
                    || line.Trim().StartsWith("POST")
                    || line.Trim().StartsWith("PATCH")
                    || line.Trim().StartsWith("DELETE")))
                .Select(line => line.Replace("https://graph.microsoft.com/beta", "").Replace("https://graph.microsoft.com/v1.0", ""))
                .ToArray();

            string delegatedPerms = GetPermissions(lines, "Delegated(workorschoolaccount)");
            string appPerms = GetPermissions(lines, "Application");

            bool hasGranularPermissions
                = delegatedPerms.Split(' ').Where(perm => IsGranularPermission(perm)).Count() > 0
                && appPerms.Split(' ').Where(perm => IsGranularPermission(perm)).Count() > 0;

            var newApis = teamsHttpCalls.Select(line => new Api()
            {
                method = GetMethod(line),
                path = GetUrl(line),
                endpoint = endpoint,
                delegatedPermissions = delegatedPerms,
                appPermissions = appPerms,
                delegatedPermissionsDocs = GetPermissionsDocs(lines, "Delegated(workorschoolaccount)"),
                appPermissionsDocs = GetPermissionsDocs(lines, "Application"),
                owner = GetOwner(line, ownershipMap),
                hasGranularPermissions = hasGranularPermissions,
                docFilePath = docFilePath,
                docUrl = docUrl,
            });
            return newApis;
        }

        private static IEnumerable<T> LinesBefore<T>(IEnumerable<T> list, Func<T, bool> test)
        {
            foreach (var item in list)
            {
                if (test(item))
                    break;
                else
                    yield return item;
            }
        }

        static string GetMethod(string api) => api.Substring(0, api.IndexOf(' '));

        static string GetUrl(string api)
        {
            int index = api.IndexOf(' ');
            string url = api.Substring(index + 1, api.Length - index - 1);
            if (url.Contains("("))
                url = url.Substring(0, url.LastIndexOf("("));
            
            // hack
            url = url.Replace("{teamId}", "{id}");
            url = url.Replace("{team-id}", "{id}");
            url = url.Replace("{channel-id}", "{id}");
            url = url.Replace("{chat-id}", "{id}");
            url = url.Replace("{chatId}", "{id}");
            url = url.Replace("{app-installation-id}", "{id}");
            url = url.Replace("{message-id}", "{id}");
            url = url.Replace("{tab-id}", "{id}");
            url = url.Replace("{chatThread-id}", "{id}");
            url = url.Replace("{membership-id}", "{id}");
            url = url.Replace("{reply-id}", "{id}");
            url = url.Replace("{app-id}", "{id}");
            url = url.Replace("{hosted-content-id}", "{id}");
            url = url.Replace("{user-id}", "{id}");
            url = url.Replace("{userId}", "{id}");
            url = url.Replace("{meetingId}", "{id}");
            url = url.Replace("{userId}", "{id}");
            url = url.Replace("{userId}", "{id}");

            return url;
        }

        private static string GetPermissions(IEnumerable<string> lines, string permissionType)
        {
            var permsLines = from line in lines
                             where line.Trim().Replace(" ", "").Replace("\t", "").StartsWith($"|{permissionType}|")
                             select line.Split('|');
            string perms = (permsLines.Count() == 0) ? "" : permsLines.First()[2].Trim().Replace(",", " ");
            if (perms.EndsWith("."))
                perms = perms.Substring(0, perms.Length - 1);
            perms = perms.Replace("**", "");
            return perms;
        }

        private static string GetPermissionsDocs(IEnumerable<string> lines, string permissionType)
        {
            var permsLines = from line in lines
                             where line.Trim().Replace(" ", "").Replace("\t", "").StartsWith($"|{permissionType}|")
                             select line.Split('|');
            string perms = (permsLines.Count() == 0) ? "" : permsLines.First()[2];
            return perms;
        }

        private static string GetOwner(string docFilePath, Ownership[] ownershipMap)
        {
            foreach (var owner in ownershipMap)
            {
                if (ContainsAnyWord(docFilePath, owner.KeywordsInPath))
                    return owner.Name;
            }
            return "GraphFW";
        }

        private static bool ContainsAnyWord(string line, IEnumerable<string> words)
            => words.Any(word => line.ToLower().Contains(word.ToLower()));


        private static bool IsGranularPermission(string perm)
            //            => !ContainsAnyWord(perm, new string[] { "", "Group.Read.All", "Group.ReadWrite.All", "User.Read.All", "User.ReadWrite.All", "Directory.Read.All", "Directory.ReadWrite.All" });
            => !new string[] { "", "Group.Read.All", "Group.ReadWrite.All", "User.Read.All", "User.ReadWrite.All", "Directory.Read.All", "Directory.ReadWrite.All" }
            .Contains(perm);
    }
}
