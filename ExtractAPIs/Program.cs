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
    enum OutputFormat
    {
        ApiPaths,
        ApiPathsAndPermissions,
        Resources,
    }

    class Program
    {
        public static string rootpath = @"C:\Users\Nick.000\source\microsoft-graph-docs2\api-reference";
        static string csvOutput = @"C:\Users\Nick.000\source\ExtractGraphAPIs\apis.csv";
        static string permsInput = @"C:\Users\Nick.000\source\ExtractGraphAPIs\newPerms.csv";
        static bool overwriteDocs = false;

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

        static ILookup<string, Api> pathToApi;


        static void Main(string[] args)
        {
            Stream output = File.OpenWrite(csvOutput);
            writer = new StreamWriter(output);

            Api[] v1 = ApiReader.ReadApis(rootpath + @"\v1.0\api", ownershipMap, requiredWords, docUrlSuffix: "?view=graph-rest-v1.0");
            Api[] beta = ApiReader.ReadApis(rootpath + @"\beta\api", ownershipMap, requiredWords, docUrlSuffix: "?view=graph-rest-beta");

            OutputApisToCsv(beta, v1);
            //OutputApis(beta, beta);

            WriteOutputLine("");

            WriteOutputLine("Graph Framework APIs:");
            ReportStats(beta.Where(api => api.owner == "GraphFw").ToArray());

            WriteOutputLine("Teamwork APIs (includes Shifts):");
            ReportStats(beta.Where(api => api.owner == "GraphFw" || api.owner == "Shifts").ToArray());

            WriteOutputLine("Teams Graph Ecosystem (includes Shifts and IC3):");
            ReportStats(beta);

            if (overwriteDocs)
            {
                NewPermissions[] newPerms;
                using (var reader = new StreamReader(permsInput))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    //csv.Configuration.RegisterClassMap<FooMap>();
                    csv.Configuration.HasHeaderRecord = false;
                    csv.Configuration.TrimOptions = CsvHelper.Configuration.TrimOptions.Trim;
                    var records = csv.GetRecords<NewPermissions>().ToArray();
                    newPerms = records.Where(p => p.delegated != "").ToArray();
                }

                pathToApi = v1.ToLookup(api => api.docFilePath);
                WriteApisToMarkdown(rootpath + @"\v1.0\api", newPerms);

                pathToApi = beta.ToLookup(api => api.docFilePath);
                WriteApisToMarkdown(rootpath + @"\beta\api", newPerms);

                //string[] uniquePerms = allPerms.Distinct().Where(s => !s.Contains(".Group")).OrderBy(s => s).ToArray();
                //foreach (string p in uniquePerms)
                //{
                //    Console.WriteLine("{");
                //    Console.WriteLine($"  name: \"{p}\",");
                //    Console.WriteLine("  description: \"Have full access to user calendars\",");
                //    Console.WriteLine("  longDescription: \"Allows the app to create, read, update, and delete events in user calendars.\",");
                //    Console.WriteLine("  preview: false,");
                //    Console.WriteLine("  admin: false");
                //    Console.WriteLine("},");
                //}
            }

            writer.Close();
            output.Close();
        }

        private static void ReportStats(Api[] ourBeta)
        {
            WriteOutput((ourBeta.Count(api => api.hasGranularPermissions) * 1.0 / ourBeta.Count()).ToString("P0"));
            WriteOutputLine(" of Teams Graph APIs have granular permissions (anything other than Group.Read/ReadWrite.All)");

            WriteOutput((ourBeta.Count(api => api.inV1) * 1.0 / ourBeta.Count()).ToString("P0"));
            WriteOutputLine(" of Teams Graph APIs are in v1.0 not just beta");

            WriteOutput((ourBeta.Count(api => api.hasGranularPermissions && api.inV1) * 1.0 / ourBeta.Count()).ToString("P0"));
            WriteOutputLine(" of Teams Graph APIs have granular permissions in v1.0");
            WriteOutputLine("");
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

        // Create a CSV of all APIs
        static void OutputApisToCsv(Api[] apis, Api[] v1Apis)
        {
            var v1Lookup = v1Apis.ToLookup(api => api.ShortName);

            if (outputFormat == OutputFormat.ApiPaths || outputFormat == OutputFormat.ApiPathsAndPermissions)
            {
                WriteOutput("Method,Path");
                if (outputFormat == OutputFormat.ApiPathsAndPermissions)
                    WriteOutput(",Delegated Permissions,App Permissions,Owner,In v1.0,Has Granular Permissions,v1.0 + granular");

                WriteOutputLine("");

                foreach (var a in apis)
                {
                    var v1Api = v1Lookup[a.ShortName].FirstOrDefault();
                    a.inV1 = v1Api != null; // HACK doing this here
                    int maxMethodName = "DELETE".Length;
                    var paddedMethod = a.method.PadRight(maxMethodName, ' ');
                    WriteOutput($"{paddedMethod},{a.path}");

                    if (outputFormat == OutputFormat.ApiPathsAndPermissions)
                        WriteOutput($",{a.delegatedPermissions},{a.appPermissions},{a.owner},{v1Api != null},{a.hasGranularPermissions},{v1Api != null && a.hasGranularPermissions},"
                            + $"{a.docUrl}");

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

        // Overwrite the .md files with new permissions info
        private static void WriteApisToMarkdown(string dir, NewPermissions[] newPerms)
        {
            foreach (var path in Directory.EnumerateFiles(dir))
            {
                string shortPath = path.Replace(rootpath + "\\", "");
                if (Path.GetExtension(path).ToLowerInvariant() == ".md")
                {
                    WriteFile(path, newPerms);
                }
            }
        }

        //static List<string> allPerms = new List<string>();


        private static void WriteFile(string path, NewPermissions[] newPerms)
        {
            if (!pathToApi.Contains(path))
                return;
            Api api = pathToApi[path].First();
            NewPermissions np = newPerms.FirstOrDefault(p => p.resource == api.path && p.verb == api.method);
            if (np == null)
                return;

            string endpoint = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)));

            string[] lines = File.ReadAllLines(path);

            var delegatedPerms = Permissions.WritePermissions(lines, "Delegated(workorschoolaccount)", np.delegated);
            var appPerms = Permissions.WritePermissions(delegatedPerms, "Application", np.appPerms);
            string result = string.Join("\n", appPerms);

            string newFilename = path.Replace(@"C:\Users\Nick.000\source\microsoft-graph-docs", @"C:\Users\Nick.000\source\docs-output");
            //Console.WriteLine(newFilename);
            File.WriteAllLines(newFilename, appPerms);
        }
    }
}
