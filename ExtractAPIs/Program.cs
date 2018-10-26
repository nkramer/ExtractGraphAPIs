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

    class ApiComparer : IEqualityComparer<Api>
    {
        public bool Equals(Api x, Api y) => x.method == y.method && x.path == y.path;
        public int GetHashCode(Api obj) => (obj.method + obj.path).GetHashCode();
    }

    class Program
    {
        static string rootpath = @"C:\Users\nkramer\source\repos\microsoft-graph-docs\api-reference\beta";

        static void Main(string[] args)
        {
            RecurseDirectory(rootpath);
        }

        static string GetMethod(string api) => api.Substring(0, api.IndexOf(' '));
        static string GetUrl(string api) {
            string s = GetMethod(api);
            int index = api.IndexOf(' ');
            return api.Substring(index + 1, api.Length - index - 1);
        }
        static void RecurseDirectory(string dir)
        {
            foreach (var path in Directory.EnumerateDirectories(dir))
            {
                RecurseDirectory(path);
            }

            List<Api> apis = new List<Api>();

            foreach (var path in Directory.EnumerateFiles(dir))
            {
                string shortPath = path.Replace(rootpath + "\\", "");
                if (Path.GetExtension(path).ToLowerInvariant() == ".md")
                {
                    //                    Console.WriteLine($"{path}, ");
                    IEnumerable<Api> newApis = ReadFile(path);

                    //Console.WriteLine(appPerms);

                    apis.AddRange(newApis);
                }
            }

            //apis = apis.Distinct().OrderBy(api => GetUrl(api) + GetMethod(api)).ToList();
            apis = apis.Distinct(new ApiComparer()).OrderBy(api => api.SortHandle).ToList();
            //apis = apis.Where(api => !api.path.Contains("/reports")).ToList();

            foreach (var a in apis)
            {
                //Console.WriteLine($"{a.method.PadRight("DELETE".Length, ' ')} {a.path}, {a.delegatedPermissions}, {a.appPermissions}");
                Console.WriteLine($"{a.method.PadRight("DELETE".Length, ' ')} {a.path}");
                //Console.WriteLine(a);
            }
        }

        private static string GetPermissions(IEnumerable<string> lines, string permissionType)
        {
            var permsLines = from line in lines
                                      where line.Trim().Replace(" ", "").Replace("\t", "").StartsWith($"|{permissionType}|")
                                      select line.Split('|');
            string perms = (permsLines.Count() == 0) ? "" : permsLines.First()[2].Trim().Replace(",", " ");
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

        private static IEnumerable<Api> ReadFile(string path)
        {
            string[] lines = File.ReadAllLines(path);
            lines = LinesBefore(lines, line => line.StartsWith("##") && line.EndsWith("Example")).ToArray();

            var teamsHttpCalls = lines.Skip(1)              
                .Where(line =>
                    line.ToLower().Contains("team")
                    && (line.Trim().StartsWith("GET")
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
                delegatedPermissions = delegatedPerms,
                appPermissions = appPerms
            });
            return newApis;
        }
    }
}
