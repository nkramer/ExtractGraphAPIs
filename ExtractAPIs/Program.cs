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
            //Recurse(@"C:\Users\nkramer\source\repos\service-shared_platform_api-spec\resource_model"); 
            Recurse2(rootpath);
        }

        static string GetMethod(string api) => api.Substring(0, api.IndexOf(' '));
        static string GetUrl(string api) {
            string s = GetMethod(api);
            int index = api.IndexOf(' ');
            return api.Substring(index + 1, api.Length - index - 1);
        }
        static void Recurse2(string dir)
        {
            foreach (var path in Directory.EnumerateDirectories(dir))
            {
                Recurse2(path);
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

        private static IEnumerable<Api> ReadFile(string path)
        {
            string[] lines = File.ReadAllLines(path);
            var moreApis = lines.Skip(1)
                .Where(line =>
                    line.ToLower().Contains("team")
                    && (line.Trim().StartsWith("GET")
                    || line.Trim().StartsWith("PUT")
                    || line.Trim().StartsWith("POST")
                    || line.Trim().StartsWith("PATCH")
                    || line.Trim().StartsWith("DELETE")))
                .Select(line => line.Replace("https://graph.microsoft.com/beta", "").Replace("https://graph.microsoft.com/v1.0", ""));

            var delegatedPermsLines = from line in lines
                                      where line.Trim().Replace(" ", "").Replace("\t", "").StartsWith("|Delegated(workorschoolaccount)|")
                                      select line.Split('|');
            string delegatedPerms = (delegatedPermsLines.Count() == 0) ? "" : delegatedPermsLines.First()[2].Trim().Replace(",", " ");

            var appPermsLines = from line in lines
                                where line.Trim().Replace(" ", "").Replace("\t", "").StartsWith("|Application|")
                                select line.Split('|');
            string appPerms = (appPermsLines.Count() == 0) ? "" : appPermsLines.First()[2].Trim().Replace(",", " ");

            var newApis = moreApis.Select(line => new Api()
            {
                method = GetMethod(line),
                path = GetUrl(line),
                delegatedPermissions = delegatedPerms,
                appPermissions = appPerms
            });
            return newApis;
        }


        //        static void Recurse(string dir)
        //        {
        //            foreach (var path in Directory.EnumerateDirectories(dir))
        //            {
        //                Recurse(path);
        //            }
        //            foreach (var path in Directory.EnumerateFiles(dir))
        //            {
        //                string shortPath = path.Replace(@"C:\Users\nkramer\source\repos\service-shared_platform_api-spec\resource_model\", "");
        //                if (Path.GetExtension(path).ToLowerInvariant() == ".md")
        //                {
        ////                    Console.WriteLine($"{path}, ");
        //                    string[] lines = File.ReadAllLines(path);
        //                    string summary = lines.Skip(1).First(line => line.Trim() != "").Trim();
        //                    var httpLine = lines
        //                        .Zip(Enumerable.Range(0, lines.Length), (a, b) => Tuple.Create(a, b))
        //                        .FirstOrDefault(line => line.Item1.Trim().StartsWith("```")
        //                            && line.Item1.Trim().EndsWith("http"));
        //                    if (httpLine != null)
        //                    {
        //                        string[] apiParts = lines[httpLine.Item2 + 1].Trim().Split(' ');
        //                        // Http verb + URL
        //                        Console.WriteLine($"{shortPath}, {summary}, {apiParts[0]}, {apiParts[1]}");
        //                    }
        //                }
        //            }
        //        }
    }
}
