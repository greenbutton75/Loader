using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Data;
using System.Diagnostics;

namespace Loader
{
    public delegate void DelegateVoid(object sender, EventArgs e);

    public static class ExtensionString
    {
        private static string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

        public static string ExtTransferWordsAfterNSymbols(this String str, int symbolsCount)
        {
            if (str.Length <= symbolsCount) return str;

            var retStr = str;
            for (int i = symbolsCount; i < retStr.Length; i++)
            {
                if (retStr[i] == ' ')
                {
                    var sb = new StringBuilder(retStr);
                    sb[i] = '\n';
                    retStr = sb.ToString();
                    break;
                }
            }

            return retStr;
        }

        public static IEnumerable<int> AllIndicesOf(this string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                throw new ArgumentNullException(nameof(pattern));
            }
            return Kmp(text, pattern);
        }

        private static IEnumerable<int> Kmp(string text, string pattern)
        {
            int M = pattern.Length;
            int N = text.Length;

            int[] lps = LongestPrefixSuffix(pattern);
            int i = 0, j = 0;

            while (i < N)
            {
                if (pattern[j] == text[i])
                {
                    j++;
                    i++;
                }
                if (j == M)
                {
                    yield return i - j;
                    j = lps[j - 1];
                }

                else if (i < N && pattern[j] != text[i])
                {
                    if (j != 0)
                    {
                        j = lps[j - 1];
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        private static int[] LongestPrefixSuffix(string pattern)
        {
            int[] lps = new int[pattern.Length];
            int length = 0;
            int i = 1;

            while (i < pattern.Length)
            {
                if (pattern[i] == pattern[length])
                {
                    length++;
                    lps[i] = length;
                    i++;
                }
                else
                {
                    if (length != 0)
                    {
                        length = lps[length - 1];
                    }
                    else
                    {
                        lps[i] = length;
                        i++;
                    }
                }
            }
            return lps;
        }

        public static string ReplaceAt(this string str, int index, int length, string replace)
        {
            if (index < 0 || length <= 0 || str.Length< index) return str;
            return str.Remove(index, Math.Min(length, str.Length - index))
                    .Insert(index, replace);
        }
        public static string DeleteBadSymbols(this String str)
        {
            return string.Concat(str.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        }
        public static T ConvertToEnum<T>(this String str)
        {
            return (T)Enum.Parse(typeof(T), str, true);
        }
        public static string ReplaceSeparator(this String str)
        {
            return str.Replace(",", separator).Replace(".", separator);
        }
        public static string SQLString(this String str)
        {
            return str.Replace("'", "''");
        }
        public static string UCFirst(this String str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }
            char[] a = str.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }
        public static string TakeFirstNLetters(this String str, int symbolsCount)
        {
            return new string(str.Take(symbolsCount).ToArray());
        }
        public static string XPathString(this String value)
        {
            // If the value contains only single or double quotes, construct
            // an XPath literal

            if (!value.Contains("'"))
                return "'" + value + "'";

            // If the value contains both single and double quotes, construct an
            // expression that concatenates all non-double-quote substrings with
            // the quotes, e.g.:
            //
            //    concat("foo",'"',"bar")

            List<string> parts = new List<string>();

            // First, put a '"' after each component in the string.
            foreach (var str in value.Split('\''))
            {
                if (!string.IsNullOrEmpty(str))
                    parts.Add("'" + str + "'"); // (edited -- thanks Daniel :-)

                parts.Add("\"'\"");
            }

            // Then remove the extra '"' after the last component.
            parts.RemoveAt(parts.Count - 1);

            // Finally, put it together into a concat() function call.
            return "concat(" + string.Join(",", parts.ToArray()) + ")";
        }
        public static string Reverse(this String str)
        {
            return new string(str.ToCharArray().Reverse().ToArray());
        }

        private static readonly Dictionary<string, Regex> Regexes = new Dictionary<string, Regex>();
        private static readonly object lockObject = new object();

        private static Regex GetRegexWithCache(string pattern, RegexOptions regexOptions)
        {
            Regex regex;
            var options = RegexOptions.Compiled;

            if (regexOptions == RegexOptions.IgnoreCase)
            {
                pattern += "(?i)";
                options = RegexOptions.Compiled | RegexOptions.IgnoreCase;
            }

            lock (lockObject)
            {
                if (!Regexes.TryGetValue(pattern, out regex))
                {
                    regex = new Regex(pattern, options);
                    Regexes.Add(pattern, regex);
                }
            }

            return regex;
        }

        public static string ReplaceWholeWord(this string original, string wordToFind, string replacement, RegexOptions regexOptions = RegexOptions.None)
        {
            string pattern = string.Format(@"\b{0}\b", wordToFind);
            return GetRegexWithCache(pattern, regexOptions).Replace(original, replacement);
        }
        public static string GetNWord(this string original, int index, RegexOptions regexOptions = RegexOptions.None)
        {
            // Returns Nth word from original string
            Regex regex = new Regex(@"\b\w+\b");
            if (index >= regex.Matches(original).Count) return "";
            return regex.Matches(original)[index].Value;
        }
        public static bool ContainsWholeWord(this string original, string wordToFind, RegexOptions regexOptions = RegexOptions.None)
        {
            string pattern = string.Format(@"\b{0}\b", wordToFind);
            return GetRegexWithCache(pattern, regexOptions).IsMatch(original);
        }

        public static bool EndsWithsWholeWord(this string original, string wordToFind, RegexOptions regexOptions = RegexOptions.None)
        {
            string pattern = string.Format(@"\b{0}$", wordToFind);
            return GetRegexWithCache(pattern, regexOptions).IsMatch(original);

        }
        public static bool StartsWithsWholeWord(this string original, string wordToFind, RegexOptions regexOptions = RegexOptions.None)
        {
            string pattern = string.Format(@"^{0}\b", wordToFind);
            return GetRegexWithCache(pattern, regexOptions).IsMatch(original);
        }
        public static string CalculateMD5Hash(this string original)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(original);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }
        public static string CalculateSHA512Hash(this string original)
        {
            // step 1, calculate SHA512 hash from input
            SHA512Managed SHhash = new SHA512Managed();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(original);
            byte[] hash = SHhash.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }
        public static string CalculateSHA256Hash(this string original)
        {
            // step 1, calculate SHA256 hash from input
            SHA256Managed SHhash = new SHA256Managed();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(original);
            byte[] hash = SHhash.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }
        public static string Between(this string value, string a, string b)
        {
            int posA = value.IndexOf(a);
            int posB = value.LastIndexOf(b);
            if (posA == -1)
            {
                return "";
            }
            if (posB == -1)
            {
                return "";
            }
            int adjustedPosA = posA + a.Length;
            if (adjustedPosA >= posB)
            {
                return "";
            }
            return value.Substring(adjustedPosA, posB - adjustedPosA);
        }
        public static string Before(this string value, string a)
        {
            int posA = value.IndexOf(a);
            if (posA == -1)
            {
                return "";
            }
            return value.Substring(0, posA);
        }
        public static string BeforeSafe(this string value, string a)
        {
            int posA = value.IndexOf(a);
            if (posA < 0)
            {
                return value;
            }
            return value.Substring(0, posA);
        }
        public static string After(this string value, string a)
        {
            int posA = value.IndexOf(a);
            if (posA == -1)
            {
                return "";
            }
            int adjustedPosA = posA + a.Length;
            if (adjustedPosA >= value.Length)
            {
                return "";
            }
            return value.Substring(adjustedPosA);
        }
        public static string RemoveNonAlphaNumericSymbols(this string value)
        {
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            return rgx.Replace(value, "");
        }
        public static string RemoveNonNumericSymbols(this string value)
        {
            Regex rgx = new Regex("[^0-9 -]");
            return rgx.Replace(value, "");
        }
        public static bool DigitsOnly(this string s)
        {
            int len = s.Length;
            for (int i = 0; i < len; ++i)
            {
                char c = s[i];
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }
        public static string[] Split(this string value, string separator)
        {
            return value.Split(new string[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        }
        public static string[] Split(this string value, string separator, StringSplitOptions param)
        {
            return value.Split(new string[] { separator }, param);
        }


        public static DateTime FromJapan_yyyyMMddHHmmss(this string v)
        {
            DateTime res = DateTime.MinValue;
            DateTime.TryParseExact(v, "yyyyMMddHHmmss", null, DateTimeStyles.None, out res);
            return res;
        }
        public static DateTime FromJapan_yyyyMMdd(this string v)
        {
            DateTime res = DateTime.MinValue;
            DateTime.TryParseExact(v, "yyyyMMdd", null, DateTimeStyles.None, out res);
            return res;
        }
    }

    public static class ExtensionNumeric
    {
        public static string RandomString(this int size)
        {
            //Random _random = new Random(Environment.TickCount);
            Random _random = new Random((int)DateTime.Now.Ticks);
            string chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            StringBuilder builder = new StringBuilder(size);

            for (int i = 0; i < size; ++i)
            {
                builder.Append(chars[_random.Next(chars.Length)]);
            }

            return builder.ToString();
        }
        public static int ToInt32(this object value)
        {
            return Convert.ToInt32(value);
        }
        public static long ToInt64(this object value)
        {
            return Convert.ToInt64(value);
        }
        public static double ToDouble(this object value)
        {
            return Convert.ToDouble(value);
        }
        public static string ToDoubleFormat(this object value, string format)
        {
            double ret = 0;
            if (double.TryParse(value.ToString(), out ret)) return ret.ToString(format);
            return "";
        }
    }

    public static class ExtensionCollections
    {
        public static IEnumerable<IList<T>> Batch<T>(this IEnumerable<T> source, int size)
        {
            T[] bucket = null;
            var count = 0;
            size = size < 1 ? 1 : size;
            foreach (var item in source)
            {
                if (bucket == null)
                    bucket = new T[size];

                bucket[count++] = item;

                if (count != size)
                    continue;

                yield return bucket;

                bucket = null;
                count = 0;
            }

            // Return the last bucket with all remaining elements
            if (bucket != null && count > 0)
                yield return bucket.Take(count).ToList();
        }
        public static void AddIfNotExist<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (!dict.ContainsKey(key)) dict.Add(key, value);
        }
        public static void AddIfNotExist<T>(this List<T> list, T item)
        {
            if (!list.Contains(item)) list.Add(item);
        }
        public static void AddIfNotExist<T>(this HashSet<T> list, T item)
        {
            if (!list.Contains(item)) list.Add(item);
        }
        public static string Join(this List<string> lst, string separator)
        {
            return string.Join(separator, lst.ToArray());
        }
    }

    public static class ExtensionIO
    {
        public static FileSystemInfo[] GetFileSystemInfosEx(this DirectoryInfo di, string searchPattern)
        {

            FileSystemInfo[] files = di.GetFileSystemInfos();


            var pattern = String.Format(".*{0}", searchPattern.Replace(".", "\\.").Replace("*", ".*"));

            var fileSpecRegex = new Regex(pattern, RegexOptions.IgnoreCase);

            var matchingFiles = from o in files
                                where fileSpecRegex.IsMatch(o.Name)
                                select o;

            return matchingFiles.ToArray();

        }
        public static FileInfo[] GetFilesEx(this DirectoryInfo di, string searchPattern)
        {

            FileInfo[] files = di.GetFiles();


            var pattern = String.Format(".*{0}", searchPattern.Replace(".", "\\.").Replace("*", ".*"));

            var fileSpecRegex = new Regex(pattern, RegexOptions.IgnoreCase);

            var matchingFiles = from o in files
                                where fileSpecRegex.IsMatch(o.Name)
                                select o;

            return matchingFiles.ToArray();

        }
    }

    public static class ExtensionControl
    {
        [DllImport("user32.dll", EntryPoint = "SendMessageA", ExactSpelling = true, CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);
        private const int WM_SETREDRAW = 0xB;

        public static void SuspendDrawing(this Control target)
        {
            SendMessage(target.Handle, WM_SETREDRAW, 0, 0);
        }
        public static void ResumeDrawing(this Control target) { ResumeDrawing(target, true); }
        public static void ResumeDrawing(this Control target, bool redraw)
        {
            SendMessage(target.Handle, WM_SETREDRAW, 1, 0);

            if (redraw)
            {
                target.Refresh();
            }
        }

        public static void Invoke(this Form form, Action method)
        {
            form.Invoke(new MethodInvoker(delegate
            {
                method();
            }));
        }
    }

    public static class ExtensionSerialize
    {
        public static string Serialize<T>(this T toSerialize)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(toSerialize.GetType());
            StringWriter textWriter = new StringWriter();

            xmlSerializer.Serialize(textWriter, toSerialize);
            return textWriter.ToString().RemoveXmlns();
        }
        public static T Deserialize<T>(this String toDeserialize)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));

            using (StringReader reader = new StringReader(toDeserialize.RemoveXmlns()))
            {
                return (T)serializer.Deserialize(reader);
            }
        }

        public static string SerializeNS<T>(this T toSerialize)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(toSerialize.GetType());
            StringWriter textWriter = new StringWriter();

            xmlSerializer.Serialize(textWriter, toSerialize);
            return textWriter.ToString();
        }
        public static T DeserializeNS<T>(this String toDeserialize)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));

            using (StringReader reader = new StringReader(toDeserialize))
            {
                return (T)serializer.Deserialize(reader);
            }
        }
    }

    public static class Util
    {
        public static bool InList<T>(this T source, params T[] values)
        {
            return values.Contains(source);
        }
    }

    public static class ExtensionXML
    {
        public static XmlDocument RemoveXmlns(this XmlDocument doc)
        {
            XDocument d;
            using (var nodeReader = new XmlNodeReader(doc))
                d = XDocument.Load(nodeReader);

            d.Root.Attributes().Where(x => x.IsNamespaceDeclaration).Remove();
            d.Root.Descendants().Attributes().Where(x => x.IsNamespaceDeclaration).Remove();

            foreach (var elem in d.Descendants())
                elem.Name = elem.Name.LocalName;

            var xmlDocument = new XmlDocument();
            using (var xmlReader = d.CreateReader())
                xmlDocument.Load(xmlReader);

            return xmlDocument;
        }
        public static string RemoveXmlns(this string xml)
        {
            XDocument d = XDocument.Parse(xml);

            d.Root.Attributes().Where(x => x.IsNamespaceDeclaration).Remove();
            d.Root.Descendants().Attributes().Where(x => x.IsNamespaceDeclaration).Remove();

            foreach (var elem in d.Descendants())
                elem.Name = elem.Name.LocalName;

            var xmlDocument = new XmlDocument();
            xmlDocument.Load(d.CreateReader());

            return xmlDocument.OuterXml;
        }
        public static string ToFormatString(this XmlDocument doc)
        {
            try
            {
                XDocument outTDoc = XDocument.Parse(doc.OuterXml);
                return outTDoc.ToString();
            }
            catch (Exception)
            {
                return doc.OuterXml;
            }
        }
        public static string GetAttributeOrInnerText(this XmlElement node, string name)
        {
            string res = "";

            res = node.GetAttribute(name);
            if (res == "")
            {
                XmlNode item = node.SelectSingleNode("./" + name);
                if (item != null) res = item.InnerText;
            }

            return res;
        }
        public static void UpdateAttributeOrInnerText(this XmlElement node, string name, string value)
        {
            string res = "";

            res = node.GetAttribute(name);
            if (res == "")
            {
                XmlNode item = node.SelectSingleNode("./" + name);
                if (item != null) item.InnerText = value;
            }
            else
            {
                if (node.HasAttribute(name)) node.Attributes[name].Value = value;
            }

        }
    }

    public static class ExtensionDataTable
    {
        public static void WriteCSV(this DataTable dt, string filePath)
        {
            dt.WriteCSV(filePath, ";");
        }
        public static void WriteCSV(this DataTable dt, string filePath, string delimiter)
        {
            // Unload DT -> CSV
            System.IO.File.WriteAllText(filePath, DataTableToCSVString(dt, delimiter));
        }
        private static string DataTableToCSVString(DataTable dt)
        {
            return DataTableToCSVString(dt, ";");
        }
        private static string DataTableToCSVString(DataTable dt, string delimiter)
        {
            string res = "";
            // DT -> CSV
            if (dt != null)
            {
                StringBuilder sb = new StringBuilder();

                string[] columnNames = dt.Columns.Cast<DataColumn>().
                                                  Select(column => column.ColumnName).
                                                  ToArray();
                sb.AppendLine(string.Join(delimiter, columnNames));

                foreach (DataRow row in dt.Rows)
                {
                    string[] fields = row.ItemArray.Select(field => field.ToString()).
                                                    ToArray();
                    sb.AppendLine(string.Join(delimiter, fields));
                }

                res = sb.ToString();
            }
            return res;
        }
    }

    public static class ExtensionDateTime
    {
        public static string ToJapanFullFormat(this DateTime time)
        {
            return time.ToString("yyyyMMddHHmmss");
        }
        public static string ToJapanHHMMFormat(this DateTime time)
        {
            return time.ToString("yyyyMMddHHmm");
        }
        public static string ToJapanShortFormat(this DateTime time)
        {
            return time.ToString("yyyyMMdd");
        }
    }

    public static class ExtensionException
    {
        public static string GetLine(this Exception ex)
        {
            // Get stack trace for the exception with source file information
            var st = new StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(0);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();

            return line.ToString();
        }
    }
}
