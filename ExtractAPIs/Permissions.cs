using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    class Permissions
    {
        internal static string[] WritePermissions(IEnumerable<string> lines, string permissionType, string newPerm)
        {
            newPerm = newPerm.Replace('\n', ' ');
            var sorted = newPerm.Split(',').Select(p => new PermListEntry() { perm = p.Trim() })
                .Where(p => p.perm != "")
                .OrderBy(p => p.SortHandle)
                .Select(p => p.perm)
                .ToArray();

            var permsLines = from line in lines
                             where line.Trim().Replace(" ", "").Replace("\t", "").StartsWith($"|{permissionType}|")
                             select line;
            lines = lines.Select(line =>
            {
                if (!permsLines.Contains(line))
                    return line;
                int snipStart = line.IndexOf("|", 1 + line.IndexOf("|"));
                int snipEnd = line.LastIndexOf("|");
                string oldstr = line.Substring(snipStart + 1, snipEnd - snipStart - 1);
                string[] oldPerms = oldstr.Split(',').Select(p => p.Trim()).ToArray();
                string[] union = oldPerms.Union(sorted).OrderBy(p => PermListEntry.GetSortHandle(p)).Where(p => p.Trim() != "").ToArray();
                union = union.Select(p => p.Replace(".Group*", ".Group")).ToArray();
                union = union.Select(p => p.Replace(".Group", ".Group*")).ToArray();
                union = union.Where(p => !p.StartsWith("Not supported")).ToArray();
                //allPerms.AddRange(union);
                string replacement = string.Join(", ", union.Select(p => p.Trim()).ToArray());
                if (replacement.Trim() == "")
                    replacement = "Not supported.";

                string s = line.Substring(0, snipStart + 1) + " " + replacement + " " + line.Substring(snipEnd);
                //Console.WriteLine(line.Substring(snipStart) + " " + newPerm);
                //Console.WriteLine(replacement);
                return s;
            });
            return lines.ToArray();
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
                string readwrite = "s";
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

    }
}
