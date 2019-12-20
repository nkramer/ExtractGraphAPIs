using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ExtractAPIs
{
    class Api 
    { 
        public string method;
        public string path;
        public string appPermissions;
        public string delegatedPermissions;
        public string endpoint;
        public string owner;

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

    // Equality comparison by method and URL
    class ApiComparer : IEqualityComparer<Api>
    {
        public bool Equals(Api x, Api y) => x.method == y.method && x.path == y.path;
        public int GetHashCode(Api obj) => (obj.method + obj.path).GetHashCode();
    }

    enum OutputFormat
    {
        ApiPaths,
        ApiPathsAndPermissions,
        Resources,
    }

    class Ownership
    {
        public string Name;
        public string[] KeywordsInPath;
    }

    class Program
    {
        static string rootpath = @"C:\Users\nkramer\source\repos\microsoft-graph-docs\api-reference";
        static string[] requiredWords = new string[] { "team", "chat", "calls", "onlineMeetings", "presence" };
        static string[] requiredWordsForIC3 = new string[] { "calls", "onlineMeetings", "presence" };
        static string[] requiredWordsForShifts = new string[] { "schedule", "workforceIntegrations" };

        // A list of (string, string list) pairs. First string is the owner, 
        // second string is the keywords the path needs to contain to belong to that owner. Order matters.
        static Ownership[] ownershipMap = new Ownership[]
            {
                new Ownership() { Name = "IC3", KeywordsInPath = new string[] { "calls", "onlineMeetings", "presence" } },
                new Ownership() { Name = "Reports", KeywordsInPath = new string[] { "reports" } },
                new Ownership() { Name = "Shifts", KeywordsInPath = new string[] { "schedule", "workforceIntegrations" } },
                new Ownership() { Name = "GraphFw", KeywordsInPath = new string[] { "/" } },
            };

        //static OutputFormat outputFormat = OutputFormat.Resources;
        static OutputFormat outputFormat = OutputFormat.ApiPathsAndPermissions;

        static void Main(string[] args)
        {
            //Api[] v1 = ReadApis(rootpath + @"\v1.0\api");
            Api[] beta = ReadApis(rootpath + @"\beta\api");
            OutputApis(beta);
        }

        static string GetMethod(string api) => api.Substring(0, api.IndexOf(' '));
        static string GetUrl(string api) {
            int index = api.IndexOf(' ');
            string url = api.Substring(index + 1, api.Length - index - 1);
            if (url.Contains("("))
                url = url.Substring(0, url.LastIndexOf("("));
            url = url.Replace("{teamId}", "{id}");
            return url;
        }

        static bool EndsWithId(string path) => path.EndsWith("}");

        static string BasePath(string path)
        {
            var stop = path.LastIndexOf("/");
            string result = path.Substring(0, stop);
            return result;
        }

        static string StripIds(string path) => EndsWithId(path) ? BasePath(path) : path;

        static bool IsAction(Api api, ILookup<string, Api> pathLookup)
            => api.method == "POST" 
            && !pathLookup[api.path].Any(a => a.method != "POST")
            && api.path != "/teams" && api.path != "/app/calls"; // hack

        static string Verb(Api api, ILookup<string, Api> pathLookup)
        {
            if (IsAction(api, pathLookup))
                return Path.GetFileName(api.path);
            switch (api.method)
            {
                case "GET":
                    if (EndsWithId(api.path) || Path.GetFileName(api.path).StartsWith("get"))
                        return "read";
                    else
                        return "list";
                case "PUT":
                    if (api.path.Contains("/schedule/"))
                        return "update";
                    else
                        return "create";
                case "POST": return "create";
                case "PATCH": return "update";
                case "DELETE": return "delete";
                default: return "???";
            }
        }

        static void OutputApis(Api[] apis)
        {
            if (outputFormat == OutputFormat.ApiPaths || outputFormat == OutputFormat.ApiPathsAndPermissions)
            {
                foreach (var a in apis)
                {
                    int maxMethodName = "DELETE".Length;
                    var paddedMethod = a.method.PadRight(maxMethodName, ' ');
                    Console.Write($"{paddedMethod}, {a.path}");

                    if (outputFormat == OutputFormat.ApiPathsAndPermissions)
                        Console.Write($", {a.delegatedPermissions}, {a.appPermissions}, {a.owner}");

                    Console.WriteLine();
                }
            }
            else if (outputFormat == OutputFormat.Resources)
            {
                var pathLookup = apis.ToLookup(api => api.path);
                var groupedApis =
                apis.GroupBy(api =>
                {
                    if (IsAction(api, pathLookup))
                        return StripIds(BasePath(api.path));
                    else
                        return StripIds(api.path);
                });

                foreach (var resource in groupedApis)
                {
                    string delegated = String.Join(" ", resource.Where(api => api.delegatedPermissions.ToLower() != "not supported").Select(api => Verb(api, pathLookup)).ToArray());
                    string appCtx = String.Join(" ", resource.Where(api => api.appPermissions.ToLower() != "not supported").Select(api => Verb(api, pathLookup)).ToArray());
                    Console.WriteLine($"{resource.Key}, {delegated}, {appCtx}");
                }
            }
        }

        private static Api[] ReadApis(string dir)
        {
            List<Api> apis = new List<Api>();

            foreach (var path in Directory.EnumerateFiles(dir))
            {
                string shortPath = path.Replace(rootpath + "\\", "");
                if (Path.GetExtension(path).ToLowerInvariant() == ".md")
                {
                    IEnumerable<Api> newApis = ReadFile(path);
                    apis.AddRange(newApis);
                }
            }

            Api[] result = apis.Distinct(new ApiComparer()).OrderBy(api => api.SortHandle).ToArray();
            return result;
        }

        private static string GetPermissions(IEnumerable<string> lines, string permissionType)
        {
            var permsLines = from line in lines
                                      where line.Trim().Replace(" ", "").Replace("\t", "").StartsWith($"|{permissionType}|")
                                      select line.Split('|');
            string perms = (permsLines.Count() == 0) ? "" : permsLines.First()[2].Trim().Replace(",", " ");
            if (perms.EndsWith("."))
                perms = perms.Substring(0, perms.Length - 1);
            return perms;
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

        private static string GetOwner(string path)
        {
            foreach (var owner in ownershipMap)
            {
                if (ContainsAnyWord(path, owner.KeywordsInPath))
                    return owner.Name;
            }
            return "GraphFW";
        }

        private static bool ContainsAnyWord(string line, IEnumerable<string> words)
            => words.Any(word => line.ToLower().Contains(word));

        private static IEnumerable<Api> ReadFile(string path)
        {
            string endpoint = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)));

            string[] lines = File.ReadAllLines(path);
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

            var newApis = teamsHttpCalls.Select(line => new Api()
            {
                method = GetMethod(line),
                path = GetUrl(line),
                endpoint = endpoint,
                delegatedPermissions = delegatedPerms,
                appPermissions = appPerms,
                owner = GetOwner(line),
            });
            return newApis;
        }
    }
}
