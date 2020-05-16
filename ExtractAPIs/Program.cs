using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using System.Diagnostics;

namespace ExtractAPIs
{
    class NewPermissions
    {
        [Index(0)]
        public string verb { get; set; }
        [Index(1)]
        public string resource { get; set; }
        [Index(2)]
        public string delegated { get; set; }
        [Index(3)]
        public string appPerms { get; set; }

        public override string ToString()
        {
            return $"{verb} {resource} {delegated} {appPerms}";
        }
    }

    //public class FooMap : ClassMap<NewPermissions>
    //{
    //    public FooMap()
    //    {
    //        Map(m => m.verb).Index(0);
    //    }
    //}

    class Api 
    { 
        public string method;
        public string path;
        public string appPermissions;
        public string delegatedPermissions;
        public string endpoint;
        public string owner;
        public bool hasGranularPermissions;
        public bool inV1 = false; // only filled in very late in the program
        public string docFilePath;

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
        static string rootpath = @"C:\Users\Nick.000\source\microsoft-graph-docs\api-reference";
        //static string rootpath = @"C:\Users\Nick\sources\microsoft-graph-docs\api-reference";
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

        static StreamWriter writer;

            static void WriteOutput(string s)
        {
            Console.Write(s);
            writer.Write(s);
        }

        static void WriteOutputLine(string s)
        {
            Console.WriteLine(s);
            writer.WriteLine(s);
        }

        static NewPermissions[] newPerms;
        static ILookup<string, Api> pathToApi;


        static void Main(string[] args)
        {
            Stream output = File.OpenWrite(@"C:\Users\Nick.000\source\ExtractGraphAPIs\apis.csv");
            writer = new StreamWriter(output);

            using (var reader = new StreamReader(@"C:\Users\Nick.000\source\ExtractGraphAPIs\newPerms.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                //csv.Configuration.RegisterClassMap<FooMap>();
                csv.Configuration.HasHeaderRecord = false;
                csv.Configuration.TrimOptions = CsvHelper.Configuration.TrimOptions.Trim;
                var records = csv.GetRecords<NewPermissions>().ToArray();
                newPerms = records.Where(p => p.delegated != "").ToArray();
            }

            //TextReader reader = new StreamReader(@"C:\Users\Nick.000\source\ExtractGraphAPIs\newPerms.csv");
            //var csvReader = new CsvReader(reader, CultureInfo.CurrentCulture);
            //var records = csvReader.GetRecords<NewPermissions>();

            //newPerms = File.ReadLines(@"C:\Users\Nick.000\source\ExtractGraphAPIs\newPerms.csv").Select(line =>
            //{
            //    string[] parts = line.Split(',');
            //    parts = parts.Concat(new string[] { "", "", "", "", "", "", "", "", "", }).ToArray();
            //    return new NewPermissions() { verb = parts[0], resource = parts[1], delegated = parts[6], appPerms = parts[7] };
            //}).Where(p => p.delegated != "").ToArray();

            //Api[] v1 = ReadApis(rootpath + @"\v1.0\api");
            Api[] beta = ReadApis(rootpath + @"\beta\api");

            pathToApi = beta.ToLookup(api => api.docFilePath);
            WriteApis(rootpath + @"\beta\api");
            //OutputApis(beta, v1);
            //OutputApis(beta, beta);


            //WriteOutputLine("");

            //Api[] ourBeta = beta.Where(api => api.owner != "IC3" && api.owner != "Reports").ToArray();
            //WriteOutput((ourBeta.Count(api => api.hasGranularPermissions) * 1.0 / ourBeta.Count()).ToString("P0"));
            //WriteOutputLine(" of Teams Graph APIs have granular permissions (anything other than Group.Read/ReadWrite.All)");

            //WriteOutput((ourBeta.Count(api => api.inV1) * 1.0 / ourBeta.Count()).ToString("P0"));
            //WriteOutputLine(" of Teams Graph APIs are in v1.0 not just beta");

            //WriteOutput((ourBeta.Count(api => api.hasGranularPermissions && api.inV1) * 1.0 / ourBeta.Count()).ToString("P0"));
            //WriteOutputLine(" of Teams Graph APIs have granular permissions in v1.0");

            //writer.Close();
            //output.Close();
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

        static void OutputApis(Api[] apis, Api[] v1Apis)
        {
            var v1Lookup = v1Apis.ToLookup(api => api.ShortName);

            if (outputFormat == OutputFormat.ApiPaths || outputFormat == OutputFormat.ApiPathsAndPermissions)
            {
                foreach (var a in apis)
                {
                    var v1Api = v1Lookup[a.ShortName].FirstOrDefault();
                    a.inV1 = v1Api != null; // HACK doing this here
                    int maxMethodName = "DELETE".Length;
                    var paddedMethod = a.method.PadRight(maxMethodName, ' ');
                    WriteOutput($"{paddedMethod},{a.path}");

                    if (outputFormat == OutputFormat.ApiPathsAndPermissions)
                        WriteOutput($",{a.delegatedPermissions},{a.appPermissions},{a.owner},{v1Api != null},{a.hasGranularPermissions}");

                    WriteOutputLine("");
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
                }).OrderBy(resource => resource.First().owner);


                foreach (var resource in groupedApis)
                {
                    string delegated = String.Join(" ", resource.Where(api => api.delegatedPermissions.ToLower() != "not supported").Select(api => Verb(api, pathLookup)).ToArray());
                    string appCtx = String.Join(" ", resource.Where(api => api.appPermissions.ToLower() != "not supported").Select(api => Verb(api, pathLookup)).ToArray());
                    WriteOutputLine($"{resource.Key},{delegated},{appCtx},{resource.First().owner}");
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

        private static void WriteApis(string dir)
        {
            foreach (var path in Directory.EnumerateFiles(dir))
            {
                string shortPath = path.Replace(rootpath + "\\", "");
                if (Path.GetExtension(path).ToLowerInvariant() == ".md")
                {
                    WriteFile(path);
                }
            }
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
                owner = GetOwner(line),
                hasGranularPermissions = hasGranularPermissions,
                docFilePath = path,
            });
            return newApis;
        }

        class PermListEntry
        {
            public string perm;

            public string SortHandle 
            {
                get
                {
                    return GetSortHandle(this.perm);
                }
            }

            public static string GetSortHandle(string perm)
            {
                string readwrite = "w";
                if (perm.Contains("ReadBasic"))
                    readwrite = "b";
                else if (perm.Contains("Write"))
                    readwrite = "w";
                else if (perm.Contains("Read"))
                    readwrite = "r";

                string rsc = "z";
                if (perm.Contains(".Group"))
                    rsc = "r";

                string resource = "n";
                if (perm.Contains("Group."))
                    resource = "o";
                else if (perm.Contains("Directory."))
                    resource = "p";

                return $"{readwrite} {rsc} {resource} {perm}";
            }
        }

        private static string[] WritePermissions(IEnumerable<string> lines, string permissionType, string newPerm)
        {
            newPerm = newPerm.Replace('\n', ' ');
            var sorted = newPerm.Split(',').Select(p => new PermListEntry() { perm = p.Trim() })
                .Where(p => p.perm != "")
                .OrderBy(p => p.SortHandle)
                .Select(p => p.perm)
                .ToArray();

//            string[] newPerms = 
            //newPerm = string.Join(", ", sorted.Select(p => p.Trim()).ToArray());

            var permsLines = from line in lines
                             where line.Trim().Replace(" ", "").Replace("\t", "").StartsWith($"|{permissionType}|")
                             select line;
            lines = lines.Select(line =>
            {
                if (!permsLines.Contains(line))
                    return line;
                int snipStart = line.IndexOf("|", 1 + line.IndexOf("|"));
                int snipEnd = line.LastIndexOf("|");
                string oldstr = line.Substring(snipStart+1, snipEnd - snipStart -1);
                string[] oldPerms = oldstr.Split(',').Select(p => p.Trim()).ToArray();
                string[] union = oldPerms.Union(sorted).OrderBy(p => PermListEntry.GetSortHandle(p)).Where(p => p.Trim() != "").ToArray();
                union = union.Where(p => !p.StartsWith("Not supported")).ToArray();
                string replacement = string.Join(", ", union.Select(p => p.Trim()).ToArray());
                if (replacement.Trim() == "")
                    replacement = "Not supported.";

                string s = line.Substring(0, snipStart + 1) + " " + replacement + " " + line.Substring(snipEnd);
                //Console.WriteLine(line.Substring(snipStart) + " " + newPerm);
                Console.WriteLine(replacement);
                return s;
            });
            return lines.ToArray();
        }

        private static void WriteFile(string path)
        {
            if (!pathToApi.Contains(path))
                return;
            Api api = pathToApi[path].First();
            NewPermissions np = newPerms.FirstOrDefault(p => p.resource == api.path && p.verb == api.method);
            if (np == null)
                return;

            string endpoint = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)));

            string[] lines = File.ReadAllLines(path);

            var delegatedPerms = WritePermissions(lines, "Delegated(workorschoolaccount)", np.delegated);
            var appPerms = WritePermissions(delegatedPerms, "Application", np.appPerms);
            string result = string.Join("\n", appPerms);

            string newFilename = path.Replace(@"C:\Users\Nick.000\source\microsoft-graph-docs", @"C:\Users\Nick.000\source\docs-output");
            //Console.WriteLine(newFilename);
            File.WriteAllLines(newFilename, appPerms);
        }


        private static bool IsGranularPermission(string perm)
            //            => !ContainsAnyWord(perm, new string[] { "", "Group.Read.All", "Group.ReadWrite.All", "User.Read.All", "User.ReadWrite.All", "Directory.Read.All", "Directory.ReadWrite.All" });
            => !new string[] { "", "Group.Read.All", "Group.ReadWrite.All", "User.Read.All", "User.ReadWrite.All", "Directory.Read.All", "Directory.ReadWrite.All" }
            .Contains(perm);
    }
}
