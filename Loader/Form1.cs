using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using ZeroRpc.Net;

namespace Loader
{
    public partial class Form1 : Form
    {
        public SqlConnection cn;
        public MySqlConnection mycn;
        public Dictionary<string, string> ICD10 = new Dictionary<string, string>();

        static object locker = new object();
        int batchsize = 0;
        int maxSentence_id = 0;
        int firstTime = 1;

        public string host = "";
        public string mysqlpwd = "";
        HashSet<string> allowedCUIs = new HashSet<string>();

        // Home
        //public string host = "192.168.0.120";
        //public string mysqlpwd = "root";

        //WellAI server
        //public string host = "localhost";
        //public string mysqlpwd = "Vjoysq47";

        public string searchType = "'Disease or Syndrome'";
        public string MainCUI = "";
        public string MainCUIName = "";
        public HashSet<string> clusterCUIs = new HashSet<string>();
        public HashSet<string> clusterMainCUIs = new HashSet<string>();

        HashSet<string> stopWords = null;
        Dictionary<string, Int32> cuinamepopularity = new Dictionary<string, int>();
        Dictionary<string, string> clusters = new Dictionary<string, string>();
        HashSet<string> allCUIs = new HashSet<string>();

        HashSet<string> sym_findings_CUIs = new HashSet<string>();
        HashSet<string> dis_CUIs = new HashSet<string>();
        HashSet<string> dis_CUIs_ShortList = new HashSet<string>();
        HashSet<string> additional_CUIs = new HashSet<string>();

        Dictionary<string, string> w1 = new Dictionary<string, string>();
        Dictionary<string, Dictionary<string, string>> w2 = new Dictionary<string, Dictionary<string, string>>();
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> w3 = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
        Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>> w4 = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>();
        Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>> w5 = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>();
        Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>> w6 = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>>();
        Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>>> w7 = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>>>();

        public void GetConnection()
        {
            // If connection is ready - just exit
            if (cn != null && cn.State == ConnectionState.Open) { return; }

            try
            {
                cn = new SqlConnection();
                cn.ConnectionString = "Data Source=(local);Initial Catalog=PubMed;Trusted_Connection=True;MultipleActiveResultSets=True";
                cn.Open();
            }
            catch
            {
            }
        }
        public SqlDataReader CommandExecutorDataReader(string SQL)
        {
            if (cn == null || cn.State != ConnectionState.Open) { this.GetConnection(); }

            System.Data.SqlClient.SqlCommand comm = new SqlCommand(SQL, cn);
            comm.CommandTimeout = 10800 * 2;

            return comm.ExecuteReader();
        }

        public void CommandExecutorNonQuery(string SQL)
        {
            if (cn == null || cn.State != ConnectionState.Open) { this.GetConnection(); }

            System.Data.SqlClient.SqlCommand comm = new SqlCommand(SQL, cn);
            comm.CommandTimeout = 10800 * 2;

            comm.ExecuteNonQuery();

        }
        public DataTable CommandExecutor(string SQL)
        {
            if (cn == null || cn.State != ConnectionState.Open) { this.GetConnection(); }

            DataSet ds = new DataSet();
            System.Data.SqlClient.SqlCommand comm = new SqlCommand(SQL, cn);
            comm.CommandTimeout = 10800 * 2;

            SqlDataAdapter adapter = new SqlDataAdapter(comm);
            try
            {
                adapter.Fill(ds);
            }
            catch { }
            if (ds.Tables.Count == 0) return null;

            return ds.Tables[0];
        }

        public void InsertOverNItems(ref List<string> insert_list, string sql_insert, int over_rows)
        {
            if (insert_list.Count >= over_rows)
            {
                this.CommandExecutorNonQuery(sql_insert + insert_list.Join(","));
                insert_list.Clear();
            }
        }
        public void InsertOverNItems(ref List<string> insert_list, string sql_insert)
        {
            if (insert_list.Count > 0)
            {
                this.CommandExecutorNonQuery(sql_insert + insert_list.Join(","));
                insert_list.Clear();
            }
        }

        /*---------------------------------------------------------*/
        public void GetMyConnection()
        {
            // If connection is ready - just exit
            if (mycn != null && mycn.State == ConnectionState.Open) { return; }

            try
            {
                mycn = new MySqlConnection();
                //mycn.ConnectionString = "server=localhost;userid=root;password=Vjoysq47;database=pubmed";
                mycn.ConnectionString = "server=" + host + ";userid=root;password=" + mysqlpwd + ";database=pubmed";
                mycn.Open();
            }
            catch
            {
            }
        }
        public void GetMyConnection(ref MySqlConnection mycn)
        {
            // If connection is ready - just exit
            if (mycn != null && mycn.State == ConnectionState.Open) { return; }

            try
            {
                mycn = new MySqlConnection();
                //mycn.ConnectionString = "server=localhost;userid=root;password=Vjoysq47;database=pubmed";
                mycn.ConnectionString = "server=" + host + ";userid=root;password=" + mysqlpwd + ";database=pubmed;Max Pool Size=100";
                mycn.Open();
            }
            catch (Exception ex)
            {
                int ui = 0;
            }
        }
        public MySqlDataReader MyCommandExecutorDataReader(string SQL)
        {
            if (mycn == null || mycn.State != ConnectionState.Open) { this.GetMyConnection(); }

            MySqlCommand comm = new MySqlCommand(SQL, mycn);
            comm.CommandTimeout = 10800 * 2;

            return comm.ExecuteReader();
        }
        public MySqlDataReader MyCommandExecutorDataReader(string SQL, MySqlConnection mycn)
        {
            if (mycn == null || mycn.State != ConnectionState.Open) { this.GetMyConnection(ref mycn); }

            MySqlCommand comm = new MySqlCommand(SQL, mycn);
            comm.CommandTimeout = 10800 * 2;

            return comm.ExecuteReader();
        }
        public DataTable MyCommandExecutor(string SQL)
        {
            if (mycn == null || mycn.State != ConnectionState.Open) { this.GetMyConnection(); }

            DataSet ds = new DataSet();
            MySqlCommand comm = new MySqlCommand(SQL, mycn);
            comm.CommandTimeout = 10800 * 2;

            MySqlDataAdapter adapter = new MySqlDataAdapter(comm);
            try
            {
                adapter.Fill(ds);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            if (ds.Tables.Count == 0) return null;

            return ds.Tables[0];
        }

        public void MyCommandExecutorNonQuery(string SQL)
        {
            if (mycn == null || mycn.State != ConnectionState.Open) { this.GetMyConnection(); }

            MySqlCommand comm = new MySqlCommand(SQL, mycn);
            comm.CommandTimeout = 10800 * 2;

            comm.ExecuteNonQuery();

        }
        public void MyCommandExecutorNonQuery(MySqlConnection mycn, string SQL)
        {
            if (mycn == null || mycn.State != ConnectionState.Open) { this.GetMyConnection(ref mycn); }

            MySqlCommand comm = new MySqlCommand(SQL, mycn);
            comm.CommandTimeout = 10800 * 2;

            comm.ExecuteNonQuery();

        }
        public void MyInsertOverNItems(MySqlConnection mycn, ref List<string> insert_list, string sql_insert, int over_rows)
        {
            if (insert_list.Count >= over_rows)
            {
                this.MyCommandExecutorNonQuery(mycn, sql_insert + insert_list.Join(","));
                insert_list.Clear();
            }
        }
        public void MyInsertOverNItems(MySqlConnection mycn, ref List<string> insert_list, string sql_insert)
        {
            if (insert_list.Count > 0)
            {
                this.MyCommandExecutorNonQuery(mycn, sql_insert + insert_list.Join(","));
                insert_list.Clear();
            }
        }
        public void MyInsertOverNItems(ref List<string> insert_list, string sql_insert, int over_rows)
        {
            if (insert_list.Count >= over_rows)
            {
                this.MyCommandExecutorNonQuery(sql_insert + insert_list.Join(","));
                insert_list.Clear();
            }
        }
        public void MyInsertOverNItems(ref List<string> insert_list, string sql_insert)
        {
            if (insert_list.Count > 0)
            {
                this.MyCommandExecutorNonQuery(sql_insert + insert_list.Join(","));
                insert_list.Clear();
            }
        }
        public void MyExecOverNItems(ref List<string> exec_list, string sql_exec_delimiter, int over_rows)
        {
            if (exec_list.Count >= over_rows)
            {
                this.MyCommandExecutorNonQuery(exec_list.Join(sql_exec_delimiter));
                exec_list.Clear();
            }
        }
        public void MyExecOverNItems(ref List<string> exec_list, string sql_exec_delimiter)
        {
            if (exec_list.Count > 0)
            {
                this.MyCommandExecutorNonQuery(exec_list.Join(sql_exec_delimiter));
                exec_list.Clear();
            }
        }
        public int MyCommandExecutorInt(string SQL)
        {
            if (mycn == null || mycn.State != ConnectionState.Open) { this.GetMyConnection(); }

            DataSet ds = new DataSet();
            MySqlCommand comm = new MySqlCommand(SQL, mycn);
            comm.CommandTimeout = 10800 * 2;

            MySqlDataAdapter adapter = new MySqlDataAdapter(comm);
            try
            { adapter.Fill(ds); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            if (ds.Tables.Count == 0) return 0;
            if (ds.Tables[0].Rows.Count == 1) return ds.Tables[0].Rows[0][0].ToInt32();
            return 0;
        }

        /*---------------------------------------------------------*/

        public Form1()
        {
            InitializeComponent();

            //Visual.SetDoubleBuffered(dataGridView3);

            // set instance non-public property with name "DoubleBuffered" to true

            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, dataGridView1, new object[] { true });
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, dataGridView2, new object[] { true });
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, dataGridView3, new object[] { true });

            if (Environment.MachineName.ToUpper().Contains("B699"))//            B6997025 Server  Machine Name
            {
                //WellAI server
                host = "localhost";
                mysqlpwd = "Vjoysq47";
            }
            else
            {
                // Home
                host = "192.168.0.120";
                mysqlpwd = "root";
            }

        }

        public GZipStream DecompressFile(string FileName)
        {
            GZipStream Decompress = null;
            try
            {
                FileStream fStream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                Decompress = new GZipStream(fStream, CompressionMode.Decompress);
            }
            catch (Exception e)
            {
                throw new Exception("The file could not be read: " + e.Message);
            }
            return Decompress;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //string file = @"V:\PubMed\pubmed19n0965.xml\pubmed19n0965.xml";

            FileSystemInfo[] files = new FileSystemInfo[0];
            DirectoryInfo dir;

            dir = new DirectoryInfo(@"V:\PubMed\");
            files = dir.GetFileSystemInfosEx("pubmed*.xml.gz");

            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 2 }, (file) =>
            {
                try
                {
                    //label2.Text = file.Name;

                    label2.Invoke((Action)delegate { label2.Text = file.Name; });

                    LoadOneXML(file.FullName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });

            /*
            foreach (FileSystemInfo file in files)
            {
                try
                {
                    label2.Text = file.Name;
                    LoadOneXML(file.FullName);
                }
                catch (Exception)
                { }
            }
            */



            label1.Text = "END !";

        }

        private void LoadOneXML(string file)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;

            XmlReader reader = null;
            GZipStream gzStream = null;

            if (file.ToLower().EndsWith(".gz"))
            {
                gzStream = DecompressFile(file);
                reader = XmlReader.Create(gzStream, settings);
            }
            else if (file.ToLower().EndsWith(".xml"))
            {
                reader = XmlReader.Create(file, settings);
            }

            string fileNum = Path.GetFileName(file).Replace("pubmed19n", "").Replace(".xml.gz", "");

            XmlDocument doc1 = new XmlDocument();
            doc1.Load(reader);
            List<string> insert_list = new List<string>();
            int count = 0;


            XmlNodeList nodeList = doc1.SelectNodes("//PubmedArticle/MedlineCitation");
            foreach (XmlNode node in nodeList)
            {
                int PMID = node.SelectSingleNode("./PMID").InnerText.ToInt32();
                //int date = node.SelectSingleNode("./DateRevised").InnerText.ToInt32();
                //int journal = node.SelectSingleNode(".//NlmUniqueID").InnerText.RemoveNonNumericSymbols().ToInt32();

                string OBJECTIVE = "";
                string BACKGROUND = "";
                string METHODS = "";
                string RESULTS = "";
                string HEAD = node.SelectSingleNode("./Article/ArticleTitle").InnerText;
                //string CONCLUSIONS = "";


                /*
                List<string> kwrds = new List<string>();
                XmlNodeList KeyWordsList = node.SelectNodes("./KeywordList/Keyword");
                foreach (XmlNode kw in KeyWordsList)
                {
                    kwrds.Add(kw.InnerText);
                }
                */

                XmlNodeList AbsList = node.SelectNodes(".//AbstractText");
                foreach (XmlNode abs in AbsList)
                {
                    string txt = abs.InnerText;

                    if (abs.Attributes["NlmCategory"] != null)
                    {
                        string cat = abs.Attributes["NlmCategory"].Value.ToString();

                        if (cat == "OBJECTIVE") OBJECTIVE = txt;
                        if (cat == "BACKGROUND") BACKGROUND = txt;
                        if (cat == "METHODS") METHODS = METHODS + " " + txt;
                        //if (cat == "RESULTS") RESULTS = txt;
                        //if (cat == "CONCLUSIONS") CONCLUSIONS = txt;
                    }
                    else
                    {
                        RESULTS = txt;
                    }
                }

                string cOBJ = "null";
                if (OBJECTIVE != "") cOBJ = "'" + OBJECTIVE.SQLString() + "'";
                string cMETH = "null";
                if (METHODS != "") cMETH = "'" + METHODS.SQLString() + "'";
                string cBKG = "null";
                if (BACKGROUND != "") cBKG = "'" + BACKGROUND.SQLString() + "'";

                // WriteToDB
                //CommandExecutorNonQuery("INSERT INTO Articles(PMID,Date,Journal,AbsObjective,AbsMethods,AbsResults,AbsConclusions,Keywords)VALUES(,");
                count++;

                if (RESULTS.StartsWith("[This corrects the article") || RESULTS == "") continue;

                //insert_list.Add("(" + PMID + "," + date + "," + journal + ",'" + OBJECTIVE.SQLString() + "','" + METHODS.SQLString() + "','" + RESULTS.SQLString() + "','" + CONCLUSIONS.SQLString() + "','" + string.Join("|", kwrds.ToArray()).SQLString() + "')");
                //InsertOverNItems(ref insert_list, "INSERT INTO Articles(PMID,Date,Journal,AbsObjective,AbsMethods,AbsResults,AbsConclusions,Keywords)VALUES", 500);
                insert_list.Add("(" + PMID + "," + cOBJ + "," + cMETH + "," + cBKG + "," + fileNum + ",'" + HEAD.SQLString() + "')");
                InsertOverNItems(ref insert_list, "INSERT INTO Articles2(PMID,AbsObjective,AbsMethods,AbsBackground,[file],Head)VALUES", 500);

                /*
                if (count % 100 == 0)
                {
                    label1.Text = count.ToString();
                    Application.DoEvents();
                }
                */
            }
            //InsertOverNItems(ref insert_list, "INSERT INTO Articles(PMID,Date,Journal,AbsObjective,AbsMethods,AbsResults,AbsConclusions,Keywords)VALUES");
            InsertOverNItems(ref insert_list, "INSERT INTO Articles2(PMID,AbsObjective,AbsMethods,AbsBackground,[file],Head)VALUES");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DataTable codes = CommandExecutor("select * from ICD10 WHERE LEN(code) <=6");
            foreach (DataRow row in codes.Rows)
            {
                string name = row["Name"].ToString().ToLower();
                if (name.Contains(",")) name = name.Before(",");
                if (name.Contains("unspecified")) name = name.Before("unspecified");
                if (name.StartsWith("other ")) name = name.Replace("other ", "");
                name = name.Trim();
                if (name == "") continue;
                if (name == "and") continue;

                ICD10.AddIfNotExist(name, row["Code"].ToString());
            }



            List<string> insert_list = new List<string>();

            using (SqlDataReader dataRdr = CommandExecutorDataReader("select top 1 AbsResults,PMID from Articles"))
            {
                while (dataRdr.Read())
                {
                    string AbsResults = dataRdr.GetString(0).ToLower();
                    int PMID = dataRdr.GetInt32(1);

                    AbsResults = @"OBJECTIVE:
To determine the prevalence of comorbidities in rheumatoid arthritis(RA), discover which comorbidities might predispose to developing RA, and identify which comorbidities are more likely to develop after RA.

PATIENTS AND METHODS:
                    We performed a case-control study using a single-center biobank, identifying 821 cases of RA(143 incident RA) between January 1, 2009, and February 28, 2018, defined as 2 diagnosis codes plus a disease - modifying antirheumatic drug. We matched each case to 3 controls based on age and sex. Participants self-reported the presence and onset of 74 comorbidities.Logistic regression models adjusted for race, body mass index, education, smoking, and Charlson comorbidity index.

                    RESULTS:
After adjustment for confounders and multiple comparisons, 11 comorbidities were associated with RA, including epilepsy(odds ratio[OR], 2.13; P = .009), obstructive sleep apnea(OR, 1.49; P = .001), and pulmonary fibrosis(OR, 4.63; P < .001), but cancer was not. Inflammatory bowel disease(OR, 3.82; P < .001), type 1 diabetes(OR, 3.07; P = .01), and venous thromboembolism(VTE; OR, 1.80; P < .001) occurred more often before RA diagnosis compared with controls.In contrast, myocardial infarction(OR, 3.09; P < .001) and VTE(OR, 1.84; P < .001) occurred more often after RA diagnosis compared with controls.Analyses restricted to incident RA cases and their matched controls mirrored these results.

     CONCLUSION:
Inflammatory bowel disease, type 1 diabetes, and VTE might predispose to RA development, whereas cardiovascular disease, VTE, and obstructive sleep apnea can result from RA.These findings have important implications for RA pathogenesis, early detection, and recommended screening.";

                    HashSet<string> artcodes = new HashSet<string>();

                    for (int ln = 6; ln >= 3; ln--)
                    {
                        Dictionary<string, string> lvl = ICD10.Where(x => x.Value.Length == ln).ToDictionary(x => x.Key, x => x.Value);
                        foreach (var item in lvl)
                        {
                            if (AbsResults.Contains(item.Key))
                            {
                                if (AbsResults.ContainsWholeWord(item.Key))
                                    artcodes.AddIfNotExist(item.Value);
                            }
                        }
                    }

                    if (artcodes.Count > 0)
                    {
                        foreach (var item in artcodes)
                        {
                            insert_list.Add("(" + PMID + ",'" + item + "')");
                        }

                        InsertOverNItems(ref insert_list, "INSERT INTO Disease(PMID,Code)VALUES", 500);

                    }
                }
            }

            InsertOverNItems(ref insert_list, "INSERT INTO Disease(PMID,Code)VALUES");

            label1.Text = "End !";



        }
        private int countCommas(string source)
        {
            int count = 0;
            char[] testchars = source.ToCharArray();
            int length = testchars.Length;
            for (int n = length - 1; n >= 0; n--)
            {
                if (testchars[n] == ',')
                    count++;
            }
            return count;
        }
        private int countSpaces(string source)
        {
            int count = 0;
            char[] testchars = source.ToCharArray();
            int length = testchars.Length;
            for (int n = length - 1; n >= 0; n--)
            {
                if (testchars[n] == ' ')
                    count++;
            }
            return count;
        }
        private void button3_Click(object sender, EventArgs e)
        {
            //batchsize = 100_000;
            //maxSentence_id = 332_724_280;

            batchsize = 100_000;
            maxSentence_id = 332_724_280;

            //batchsize = 500;
            //maxSentence_id = 60100;
            int pages = (Int32)Math.Ceiling((double)maxSentence_id / (double)batchsize);



            // If we want to calculate only SPECIFIC sty
            MySqlConnection mylclcn = null;
            // removed ,
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT CUI FROM cuinamepopularity  where sty IN       ('Finding','Diagnostic PROCEDURE','Therapeutic or Preventive PROCEDURE','Vitamins and Supplements','Disease or Syndrome','Sign or Symptom') ; ", mylclcn))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);

                    allowedCUIs.AddIfNotExist(CUI);
                }
            }

            //Load clustering information
            string clusterFile = @"D:\PubMed\clusters.csv";

            using (var reader = new StreamReader(clusterFile))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");

                    if (!clusters.ContainsKey(lbl[0]))
                    {
                        clusters.Add(lbl[0], lbl[1]);
                    }
                }
            }

            // Remove general concepts.
            allowedCUIs.Remove("C0039082"); // Syndrome
            allowedCUIs.Remove("C0012634"); // Disease
            allowedCUIs.Remove("C0221198"); // Lesion
            allowedCUIs.Remove("C0205082"); // Severe
            allowedCUIs.Remove("C1457887"); // Symptoms
            allowedCUIs.Remove("C0205160"); // Negative
            allowedCUIs.Remove("C0221444"); // clinical syndromes

            // Add age and sex
            allowedCUIs.Add("C0021289");// Neonate 0 - 1 month
            allowedCUIs.Add("C0021270");// Infant 1 month - 3 year
            allowedCUIs.Add("C0682053");// Toddler 1 - 3 year
            allowedCUIs.Add("C0008100");// Preschool Children 3 - 5
            allowedCUIs.Add("C0260267");// School children 6 - 16
            allowedCUIs.Add("C0008059");// Children 2 - 12

            allowedCUIs.Add("C0205653");// Adolescent 12 - 18
            allowedCUIs.Add("C0087178");// Youth 18 - 21
            allowedCUIs.Add("C0238598");// Young adult 21 - 30
            allowedCUIs.Add("C0001675");// Adult 21 - 59
            allowedCUIs.Add("C0205847");// Middle Ages 45 - 59
            allowedCUIs.Add("C0001792");// Older adults
            allowedCUIs.Add("C0079377");// Frail Elders > 60
            allowedCUIs.Add("C0001792");// Aged 65 and Over

            allowedCUIs.Add("C0028829");// Octogenarian > 80
            allowedCUIs.Add("C0001795");// aged over 80
            allowedCUIs.Add("C0028296");// Nonagenarians > 90
            allowedCUIs.Add("C0007667");// Centenarian > 100


            allowedCUIs.Add("C0870604");// Girl
            allowedCUIs.Add("C0043210");// Woman

            allowedCUIs.Add("C0870221");// Boys
            allowedCUIs.Add("C0025266");// Men

            allowedCUIs.Add("C0032961");// pregnancy


            try
            {
                Parallel.For(0, pages + 1, new ParallelOptions { MaxDegreeOfParallelism = 11 }, i =>
                {
                    DoOneBlock(i);
                });

            }
            catch (AggregateException ae)
            {
                var ignoredExceptions = new List<Exception>();
                // This is where you can choose which exceptions to handle.
                foreach (var ex in ae.Flatten().InnerExceptions)
                {
                    if (ex is ArgumentException)
                        Console.WriteLine(ex.Message);
                    else
                        ignoredExceptions.Add(ex);
                }
                if (ignoredExceptions.Count > 0) throw new AggregateException(ignoredExceptions);
            }


            MessageBox.Show("Ready!");

            /*
            for (int i = 0; i < maxSentence_id; i = i + batchsize)
            {
                DoOneBlock(i);
                Application.DoEvents();
                label1.Text = i.ToString();
            }
            */
        }

        private void DoOneBlock(int page)
        {
            int from = page * batchsize;
            int to = (page + 1) * batchsize;

            Console.WriteLine($"Page {page} From {from} to {to}");

            Dictionary<int, List<Tuple<string, int, int>>> concepts = new Dictionary<int, List<Tuple<string, int, int>>>();
            List<string> content = new List<string>();
            List<string> onlyCUI = new List<string>();
            //List<string> skipped = new List<string>();
            //List<string> skippedCUI = new List<string>();
            //using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT SENTENCE_ID,CUI,START_INDEX,END_INDEX FROM entity WHERE sentence_id BETWEEN " + from.ToString() + " AND " + to.ToString() + "  ORDER BY sentence_id, START_INDEX desc ;"))

            MySqlConnection mylclcn = null;
            try
            {
                using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT SENTENCE_ID,           /* ClusterCUI */  CUI             ,START_INDEX,END_INDEX FROM entity WHERE sentence_id BETWEEN " + from.ToString() + " AND " + to.ToString() + "  ORDER BY sentence_id, START_INDEX desc ;", mylclcn))
                {
                    while (dataRdr.Read())
                    {
                        int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);
                        string CUI = dataRdr.GetString(1);
                        int START_INDEX = (Int32)dataRdr.GetUInt32(2);
                        int END_INDEX = (Int32)dataRdr.GetUInt32(3);

                        // Use code clustering - not DB clustering
                        // WITHOUT CLUSTERS comment line below
                        if (clusters.ContainsKey(CUI)) CUI = clusters[CUI]; // Replace with MainCUI


                        if (!allowedCUIs.Contains(CUI)) continue; // If we want to calculate only SPECIFIC sty

                        if (!concepts.ContainsKey(SENTENCE_ID)) concepts.Add(SENTENCE_ID, new List<Tuple<string, int, int>>());
                        concepts[SENTENCE_ID].Add(new Tuple<string, int, int>(CUI, START_INDEX, END_INDEX));

                        //if (CUI == "C1384666")
                        //{
                        //    string b = "bingo";
                        //}
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Err1 Page {page}");
            }
            finally
            {
                if (mylclcn != null && mycn.State == ConnectionState.Open) mylclcn.Close();
            }

            try
            {
                using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT SENTENCE_ID,SENT_START_INDEX,SENTENCE FROM sentence WHERE sentence_id BETWEEN " + from.ToString() + " AND " + to.ToString() + ";", mylclcn))
                {
                    //bool skip = false;
                    while (dataRdr.Read())
                    {
                        //skip = false;
                        int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);
                        int SENT_START_INDEX = (Int32)dataRdr.GetUInt32(1);
                        string SENTENCE = dataRdr.GetString(2).ToLower();

                        List<string> oneLineCUIs = new List<string>();

                        // Skip whose sentences since they are supposed to be about side effects - vomiting, diarrhea and so on 
                        if (SENTENCE.Contains("frequent events") || SENTENCE.Contains("adverse") || SENTENCE.Contains("toxicity") || SENTENCE.Contains("induced") || SENTENCE.Contains("side effects")) continue;


                        int sentence_modified = 0;
                        // Replace words with concept
                        if (concepts.ContainsKey(SENTENCE_ID))
                        {
                            int startNext = 999999;
                            foreach (var item in concepts[SENTENCE_ID])
                            {
                                if (startNext >= item.Item3)
                                {
                                    SENTENCE = SENTENCE.ReplaceAt(item.Item2 - SENT_START_INDEX, item.Item3 - item.Item2, item.Item1);

                                    oneLineCUIs.Add(item.Item1);
                                    //if (item.Item1 == "C1384666")
                                    //{
                                    //    string b = "bingo";
                                    //}

                                    sentence_modified++;

                                }

                                startNext = item.Item2;
                            }
                        }

                        // Exclude Sentences with long enumeration like ...symptoms that included abdominal pain, diarrhea, anorexia, nausea and/or vomiting, myalgias, fatigue, fever, body fat redistribution, dizziness, headaches, paresthesias, xerostomia, nephrolithiasis, and rash
                        // or "Withdrawal of these agents can cause nausea, emesis, anorexia, diarrhea, rhinorrhea, diaphoresis, myalgia, paresthesia, anxiety, agitation, restlessness, and..."
                        if (countCommas(SENTENCE) >= 7 && sentence_modified > 8)
                        {
                            //skippedCUI.Add(SENTENCE);
                            continue;
                        }

                        //Normalize
                        SENTENCE = new string(SENTENCE.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());

                        if (sentence_modified > 0) content.Add(SENTENCE); // do not add unmodified sentences - without CUI

                        if (oneLineCUIs.Count > 1) onlyCUI.Add(string.Join(" ", oneLineCUIs.ToArray()));
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine($"Err2 Page {page}");
            }
            finally
            {
                if (mylclcn != null && mycn.State == ConnectionState.Open) mylclcn.Close();
            }




            lock (locker)
            {
                Console.WriteLine($"writes Page {page}  {content.Count }");
                try
                {
                    //System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_clu.txt", content.ToArray());
                    System.IO.File.AppendAllLines(@"D:\PubMed\only_CUI_clu.txt", onlyCUI.ToArray());

                    //long length = new System.IO.FileInfo(@"D:\PubMed\abstracts_CUI_noclu.txt").Length;
                    // Get 10 % of the whole corpus
                    //if (length > 1301522135 && firstTime==1)
                    //{
                    //    File.Copy(@"D:\PubMed\abstracts_CUI_noclu.txt", @"D:\PubMed\abstracts_CUI_noclu_short.txt");
                    //    File.Copy(@"D:\PubMed\only_CUI_noclu_short.txt", @"D:\PubMed\only_CUI_noclu_short.txt");
                    //    firstTime = 0;
                    //}
                    //System.IO.File.AppendAllLines(@"D:\PubMed\__skipped.txt", skipped.ToArray());
                }
                catch (Exception)
                {
                    Console.WriteLine($"Err3 Page {page}");
                }
            }

        }

        private void button4_Click(object sender, EventArgs e)
        {

            List<string> insert_list = new List<string>();

            var lines = File.ReadLines(@"D:\PubMed\J_Medline");
            string ID = "";
            string Title = "";
            string Abbr = "";
            string ISSNPrint = "";
            string ISSNOnline = "";
            string NLMID = "";

            foreach (var line in lines)
            {
                if (line.StartsWith("JrId:")) ID = line.After("JrId:").Trim();
                if (line.StartsWith("JournalTitle:")) Title = line.After("JournalTitle:").Trim().TakeFirstNLetters(250);
                if (line.StartsWith("MedAbbr:")) Abbr = line.After("MedAbbr:").Trim().TakeFirstNLetters(50);
                if (line.StartsWith("ISSN (Print):")) ISSNPrint = line.After("ISSN (Print):").Trim().TakeFirstNLetters(50);
                if (line.StartsWith("ISSN (Online):")) ISSNOnline = line.After("ISSN (Online):").Trim().TakeFirstNLetters(50);
                if (line.StartsWith("NlmId:")) NLMID = line.After("NlmId:").Trim();



                if (line.StartsWith("-----------------") && ID != "")
                {
                    insert_list.Add("(" + ID + ",'" + Title.SQLString() + "','" + Abbr.SQLString() + "', '" + ISSNPrint.SQLString() + "', '" + ISSNOnline.SQLString() + "', '" + NLMID.SQLString() + "')");
                    MyInsertOverNItems(ref insert_list, "INSERT INTO journals(ID,Title, Abbr, ISSNPrint, ISSNOnline,NLMID)VALUES", 500);
                }
            }


            MyInsertOverNItems(ref insert_list, "INSERT INTO journals(ID,Title, Abbr, ISSNPrint, ISSNOnline,NLMID)VALUES");
            /*
            CREATE TABLE journals(
ID int(10) UNSIGNED NOT NULL,
Title varchar(250) DEFAULT '',
Abbr varchar(50) DEFAULT '',
ISSNPrint varchar(50) DEFAULT '',
ISSNOnline varchar(50) DEFAULT '',
NLMID   varchar(50) DEFAULT ''
);
*/

        }

        public DataTable GetMostPopular(string srch)
        {
            string SQL = "SELECT mc.CUI, MIN(mc.STR) AS Name, MIN(p.Name) AS CUIName,1 HasCluster,p.Popularity FROM mrconsoshort  mc, (SELECT * FROM cuinamepopularity WHERE STY IN (" + searchType + ") ORDER BY Popularity DESC LIMIT 50000) p  WHERE  STR LIKE '" + srch.SQLString() + "%'   AND p.CUI=mc.CUI /*and not EXISTS(SELECT * FROM clusters cl WHERE cl.CUI = mc.CUI and cl.CUI != cl.MainCUI)*/  GROUP BY mc.CUI ORDER BY p.Popularity DESC LIMIT 500; ";


            if (srch.StartsWith("C") && srch.Length == 8 && (srch[1] == '0' || srch[1] == '1'))
                SQL = "SELECT mc.CUI, MIN(mc.STR) AS Name, MIN(p.Name) AS CUIName,1 HasCluster,p.Popularity FROM mrconsoshort  mc, (SELECT * FROM cuinamepopularity WHERE STY IN (" + searchType + ") ORDER BY Popularity DESC ) p  WHERE  p.CUI='" + srch.SQLString() + "'   AND p.CUI=mc.CUI /*and not EXISTS(SELECT * FROM clusters cl WHERE cl.CUI = mc.CUI and cl.CUI != cl.MainCUI)*/  GROUP BY mc.CUI ORDER BY p.Popularity DESC LIMIT 500; ";

            return MyCommandExecutor(SQL);
        }

        public DataTable GetMostPopular2(string srch)
        {
            string SQL = "SELECT mc.CUI, MIN(mc.STR) AS Name, MIN(p.Name) AS CUIName,1 HasCluster,p.Popularity FROM mrconsoshort  mc, (SELECT * FROM cuinamepopularity WHERE STY IN (" + searchType + ") ORDER BY Popularity DESC LIMIT 50000) p  WHERE  STR LIKE '%" + srch.SQLString() + "%'   AND p.CUI=mc.CUI /*and not EXISTS(SELECT * FROM clusters cl WHERE cl.CUI = mc.CUI and cl.CUI != cl.MainCUI)*/  GROUP BY mc.CUI ORDER BY p.Popularity DESC LIMIT 500; ";


            if (srch.StartsWith("C") && srch.Length == 8 && (srch[1] == '0' || srch[1] == '1'))
                SQL = "SELECT mc.CUI, MIN(mc.STR) AS Name, MIN(p.Name) AS CUIName,1 HasCluster,p.Popularity FROM mrconsoshort  mc, (SELECT * FROM cuinamepopularity WHERE STY IN (" + searchType + ") ORDER BY Popularity DESC ) p  WHERE  p.CUI='" + srch.SQLString() + "'   AND p.CUI=mc.CUI /*and not EXISTS(SELECT * FROM clusters cl WHERE cl.CUI = mc.CUI and cl.CUI != cl.MainCUI)*/  GROUP BY mc.CUI ORDER BY p.Popularity DESC LIMIT 500; ";

            return MyCommandExecutor(SQL);
        }
        public DataTable GetCluster(string MainCUI)
        {
            DataTable dt = MyCommandExecutor("SELECT MainCUI, NAME AS MainName, c.CUI, (SELECT NAME FROM cuinamepopularity p2 WHERE p2.CUI = c.CUI)CUIName,  (SELECT popularity FROM cuinamepopularity p2 WHERE p2.CUI = c.CUI)popularity FROM  clusters c, cuinamepopularity p WHERE MainCUI = '" + MainCUI + "' AND MainCUI = p.CUI");

            int ppl = 0;
            //clusterCUI.Clear();
            foreach (DataRow item in dt.Rows)
            {
                //clusterCUI.AddIfNotExist(item["CUI"].ToString());
                ppl = ppl + item["popularity"].ToInt32();
            }

            if (dt.Rows.Count > 0)
            {
                label5.Text = "Cluster " + dt.Rows[0]["CUIName"].ToString();
                label9.Text = "Popularity " + ppl.ToString();
            }
            return dt;
        }
        public void InsertCluster(string MainCUI, string CUI)
        {
            if (MainCUI == CUI)
            {
                //MessageBox.Show("Can not add itself");
                //MessageBox.Show(this, "Can not add itself", "Exception", MessageBoxButtons.OK);
                this.Text = "Can not add itself";
                return;
            }

            DataTable dt;

            dt = MyCommandExecutor("SELECT (SELECT name FROM cuinamepopularity p WHERE p.CUI=cl.MainCUI)main  FROM clusters cl WHERE MainCUI = '" + CUI + "' LIMIT 1");
            if (dt.Rows.Count == 1)
            {
                //MessageBox.Show(dt.Rows[0][0].ToString() + " is a head of cluster");
                this.Text = dt.Rows[0][0].ToString() + " is a head of cluster";
                return;
            }

            dt = MyCommandExecutor("SELECT(SELECT name FROM cuinamepopularity p WHERE p.CUI = cl.MainCUI)main, (SELECT name FROM cuinamepopularity p WHERE p.CUI=cl.CUI)slave FROM clusters cl WHERE CUI = '" + MainCUI + "' LIMIT 1");
            if (dt.Rows.Count > 0)
            {
                //MessageBox.Show(dt.Rows[0]["slave"].ToString() + " is in cluster " + dt.Rows[0]["main"].ToString());
                this.Text = dt.Rows[0]["slave"].ToString() + " is in cluster " + dt.Rows[0]["main"].ToString();
                return;
            }

            dt = MyCommandExecutor("SELECT 1 FROM clusters cl WHERE MainCUI = '" + MainCUI + "' and CUI = '" + CUI + "'");
            if (dt.Rows.Count > 0)
            {
                // This record is already exists
                return;
            }

            dt = MyCommandExecutor("SELECT(SELECT name FROM cuinamepopularity p WHERE p.CUI = cl.MainCUI)main, (SELECT name FROM cuinamepopularity p WHERE p.CUI=cl.CUI)slave FROM clusters cl WHERE CUI = '" + CUI + "' LIMIT 1");
            if (dt.Rows.Count > 0)
            {
                //MessageBox.Show(dt.Rows[0]["slave"].ToString() + " is already in cluster " + dt.Rows[0]["main"].ToString());
                this.Text = dt.Rows[0]["slave"].ToString() + " is already in cluster " + dt.Rows[0]["main"].ToString();
                return;
            }

            MyCommandExecutorNonQuery("INSERT INTO clusters (MainCUI,CUI, Author) VALUES ('" + MainCUI + "','" + CUI + "','" + System.Security.Principal.WindowsIdentity.GetCurrent().Name.SQLString() + "')");
            clusterCUIs.AddIfNotExist(CUI);
            clusterMainCUIs.AddIfNotExist(MainCUI);

            dataGridView1.Refresh();
            dataGridView2.Refresh();
            dataGridView3.Refresh();
        }
        public void DeleteCluster(string MainCUI, string CUI)
        {
            MyCommandExecutorNonQuery("DELETE from clusters where MainCUI='" + MainCUI + "' and CUI='" + CUI + "'");

            clusterCUIs.Remove(CUI);
            clusterMainCUIs.Remove(MainCUI);

            dataGridView1.Refresh();
            dataGridView2.Refresh();
            dataGridView3.Refresh();
        }
        public DataTable GetSynonyms(string CUI)
        {
            return MyCommandExecutor("SELECT STR FROM mrconsoshort WHERE CUI= '" + CUI + "'");
        }
        public DataTable GetDescriptions(string CUI)
        {
            return MyCommandExecutor("SELECT DEF FROM mrdef WHERE CUI= '" + CUI + "' AND SAB IN('MSH','HPO','NCI')");
        }
        public DataTable GetSimilar(string CUI)
        {

            if (cuinamepopularity.Count == 0)
            {
                using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT CUI, Popularity FROM cuinamepopularity WHERE Popularity> 0"))
                {
                    while (dataRdr.Read())
                    {
                        string _CUI = dataRdr.GetString(0);
                        int _Popularity = dataRdr.GetInt32(1);
                        cuinamepopularity.AddIfNotExist(_CUI, _Popularity);
                    }
                }

            }

            DataTable dt = new DataTable();
            dt.Clear();
            dt.Columns.Add("CUI");
            dt.Columns.Add("Name");
            dt.Columns.Add("Popularity");

            // When work from home - disable ZeroRPC
            //if (host == "192.168.0.120") return dt;
            //MessageBox.Show(CUI);

            // ZeroRPC  TO RUN IT -- C:\Anaconda3\envs\ttt\python.exe D:\PubMed\WillAInode.js\server\py\server.py
            Client c = new Client();
            c.Connect("tcp://" + host + ":4345");
            //c.Connect("tcp://localhost:4345");

            string res = "";
            //string res = c.Invoke<string>("allresdc", "{ \"codes\":\"[\\\"c0687152\\\"]\",\"coeffvec\":\"false\",\"action\":\"allresd\",\"system\":\"LEXE\",\"fingerprint\":\"\"}");
            if (searchType == "'Disease or Syndrome'" || searchType == "'Sign or Symptom','Finding'")
                res = c.Invoke<string>("allresdc", "{ \"codes\":\"[\\\"" + CUI.ToLower() + "\\\"]\",\"coeffvec\":\"false\",\"action\":\"allresd\",\"system\":\"LEXE\",\"fingerprint\":\"\"}");
            else
                res = c.Invoke<string>("specificresdc", "{ \"codes\":\"[\\\"" + CUI.ToLower() + "\\\"]\",\"vid\":\"" + searchType.Trim('\'') + "\",\"action\":\"specificresdc\",\"system\":\"LEXE\",\"fingerprint\":\"\"}");

            JObject j_data;
            try
            {
                j_data = JObject.Parse(res);
            }
            catch (Exception)
            {
                return dt;
            }


            c.Dispose();
            //MessageBox.Show(CUI +" -- " + res.Substring(0, 50));

            JToken jiter = null;

            if (searchType == "'Disease or Syndrome'")
            {
                jiter = j_data["dec"];
            }
            else if (searchType == "'Sign or Symptom','Finding'")
            {
                jiter = j_data["sym"];
            }
            else
            {
                jiter = j_data["lst"];
            }
            foreach (var item in jiter)
            {
                int pop = 0;
                if (cuinamepopularity.ContainsKey(item["CUI"].ToString())) pop = cuinamepopularity[item["CUI"].ToString()];
                //if (!clusterCUI.Contains(item["CUI"].ToString())) 
                dt.Rows.Add(item["CUI"].ToString(), item["Name"].ToString(), pop); //, item["Popularity"].ToString()
            }


            return dt;

        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) { button5_Click(null, null); }
        }
        private void GetAllClusters()
        {
            if (clusterCUIs.Count == 0)
            {
                using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT MainCUI,CUI FROM clusters;"))
                {
                    //bool skip = false;
                    while (dataRdr.Read())
                    {
                        string MainCUI = dataRdr.GetString(0);
                        string CUI = dataRdr.GetString(1);

                        clusterCUIs.AddIfNotExist(CUI);
                        clusterMainCUIs.AddIfNotExist(MainCUI);
                    }
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            GetAllClusters();

            dataGridView1.DataSource = GetMostPopular(textBox1.Text);
            dataGridView1.Columns[0].Width = 60;
            dataGridView1.Columns[1].Width = 156;
            dataGridView1.Columns[2].Width = 156;
            dataGridView1.Columns["HasCluster"].Visible = false;
            dataGridView1.Refresh();
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked) { searchType = "'Disease or Syndrome'"; comboBox1.Text = ""; }
            if (radioButton2.Checked) { searchType = "'Sign or Symptom','Finding'"; comboBox1.Text = ""; }
        }
        private void tabPage2_Enter(object sender, EventArgs e)
        {
            radioButton1.Checked = true;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            dataGridView2.DataSource = GetSimilar(MainCUI);
            dataGridView2.Columns[0].Width = 60;
            dataGridView2.Columns[1].Width = 250;
            dataGridView2.Refresh();
        }

        private void dataGridView1_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            MainCUIName = dataGridView1.Rows[e.RowIndex].Cells["CUIName"].Value.ToString();
            MainCUI = dataGridView1.Rows[e.RowIndex].Cells["CUI"].Value.ToString();

            // Get Similar
            groupBox1.Text = MainCUIName;
            button6_Click(null, null);

            // Get search text
            textBox2.Text = MainCUIName;
            button7_Click(null, null);

            // Get description and synonyms
            string desc = "";
            foreach (DataRow item in GetDescriptions(MainCUI).Rows)
            {
                desc = desc + "\r\n\r\n" + item["DEF"].ToString();
            }
            label6.Text = desc.Trim();

            dataGridView5.DataSource = GetSynonyms(MainCUI);
            dataGridView5.Columns[0].Width = 400;

            // Get cluster
            dataGridView4.DataSource = GetCluster(MainCUI);
            dataGridView4.Columns[0].Visible = false;
            dataGridView4.Columns[1].Visible = false;
            dataGridView4.Columns[2].Visible = false;
            dataGridView4.Columns[3].Width = 300;

            dataGridView1.Refresh();
            textBox2.Refresh();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            dataGridView3.DataSource = GetMostPopular2(textBox2.Text);
            dataGridView3.Columns[0].Width = 60;
            dataGridView3.Columns[1].Width = 180;
            dataGridView3.Columns[2].Width = 180;
            dataGridView3.Columns[3].Visible = false;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            string aCUI = dataGridView3.CurrentRow.Cells[0].Value.ToString();

            dataGridView2.DataSource = GetSimilar(aCUI);
            dataGridView2.Columns[0].Width = 60;
            dataGridView2.Columns[1].Width = 250;

        }

        private void dataGridView3_RowEnter(object sender, DataGridViewCellEventArgs e)
        {

            DataGridView itemDGV = sender as DataGridView;

            try
            {
                string aCUI = itemDGV.Rows[e.RowIndex].Cells[0].Value.ToString();
                groupBox2.Text = itemDGV.Rows[e.RowIndex].Cells[1].Value.ToString();

                // Get description and synonyms
                string desc = "";
                foreach (DataRow item in GetDescriptions(aCUI).Rows)
                {
                    desc = desc + "\r\n\r\n" + item["DEF"].ToString();
                }
                label7.Text = desc.Trim();

                dataGridView6.DataSource = GetSynonyms(aCUI);
                dataGridView6.Columns[0].Width = 400;
            }
            catch
            {
            }
            dataGridView2.Refresh();
        }

        private void dataGridView3_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            DataGridView itemDGV = sender as DataGridView;
            this.Text = "";

            string aCUI = itemDGV.CurrentRow.Cells[0].Value.ToString();
            InsertCluster(MainCUI, aCUI);

            // Get cluster
            dataGridView4.DataSource = GetCluster(MainCUI);
            dataGridView4.Columns[0].Visible = false;
            dataGridView4.Columns[1].Visible = false;
            dataGridView4.Columns[2].Visible = false;
            dataGridView4.Columns[3].Width = 300;
        }

        private void dataGridView4_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            DataGridView itemDGV = sender as DataGridView;

            string aMainCUI = itemDGV.CurrentRow.Cells["MainCUI"].Value.ToString();
            string aCUI = itemDGV.CurrentRow.Cells["CUI"].Value.ToString();
            DeleteCluster(aMainCUI, aCUI);

            // Get cluster
            dataGridView4.DataSource = GetCluster(MainCUI);
            dataGridView4.Columns[0].Visible = false;
            dataGridView4.Columns[1].Visible = false;
            dataGridView4.Columns[2].Visible = false;
            dataGridView4.Columns[3].Width = 300;
        }



        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = this.comboBox1.GetItemText(this.comboBox1.SelectedItem);
            if (item != "")
            {
                radioButton1.Checked = false;
                radioButton2.Checked = false;

                searchType = "'" + item + "'";

            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            string fn = textBox3.Text;
            string fnRes = textBox3.Text.Replace(".txt", "Res.txt");
            string fnDeb = textBox3.Text.Replace(".txt", "Deb.txt");

            var fs = new FileStream(fn, FileMode.Open, FileAccess.Read);
            var sr = new StreamReader(fs, Encoding.UTF8);

            Dictionary<string, int> SubsCounter = new Dictionary<string, int>();

            Dictionary<string, string> Substitutes = new Dictionary<string, string>();
            Substitutes.Add("gentamicin", "C0017436");
            Substitutes.Add("acetylcholinesterase2", "C1610422");
            Substitutes.Add("ace2", "C1610422");
            Substitutes.Add("human coronaviruses", "C0206422");
            Substitutes.Add("human coronavirus", "C0206422");
            Substitutes.Add("coronaviruses", "C0206422");
            Substitutes.Add("coronavirus", "C0206422");
            Substitutes.Add("hcov-oc43", "C0206422");
            Substitutes.Add("hcov-229e", "C0206422");
            Substitutes.Add("hcov-nl63", "C0206422");
            Substitutes.Add("hcov-hku1", "C0206422");
            Substitutes.Add("sars-coronavirus", "C1175743");
            Substitutes.Add("sars-cov", "C1175743");
            Substitutes.Add("sars-associated coronavirus", "C1175743");
            Substitutes.Add("(mers)-coronavirus", "C3698360");
            Substitutes.Add("mers-cov", "C3698360");
            Substitutes.Add("(mers)", "C3698360");

            Dictionary<string, string> SubstitutesWords = new Dictionary<string, string>();
            SubstitutesWords.Add("hcovs", "C0206422");
            SubstitutesWords.Add("hcov", "C0206422");


            Dictionary<string, string> Clusters = new Dictionary<string, string>();

            #region Clusters
            Clusters.AddIfNotExist("C0041327", "C0041296");
            Clusters.AddIfNotExist("C0151332", "C0041296");
            Clusters.AddIfNotExist("C1609538", "C0041296");
            Clusters.AddIfNotExist("C0347950", "C0004096");
            Clusters.AddIfNotExist("C0582415", "C0004096");
            Clusters.AddIfNotExist("C0004096", "C0004096");
            Clusters.AddIfNotExist("C0856716", "C0004096");
            Clusters.AddIfNotExist("C0687152", "C0010200");
            Clusters.AddIfNotExist("C0011849", "C0011847");
            Clusters.AddIfNotExist("C0011860", "C0011847");
            Clusters.AddIfNotExist("C0342257", "C0011847");
            Clusters.AddIfNotExist("C0011854", "C0011847");
            Clusters.AddIfNotExist("C0020456", "C0011847");
            Clusters.AddIfNotExist("C0948379", "C0011847");
            Clusters.AddIfNotExist("C0011881", "C0011847");
            Clusters.AddIfNotExist("C0011884", "C0011847");
            Clusters.AddIfNotExist("C0011882", "C0011847");
            Clusters.AddIfNotExist("C0342276", "C0011847");
            Clusters.AddIfNotExist("C0740447", "C0011847");
            Clusters.AddIfNotExist("C0206172", "C0011847");
            Clusters.AddIfNotExist("C0011875", "C0011847");
            Clusters.AddIfNotExist("C0085207", "C0011847");
            Clusters.AddIfNotExist("C0271680", "C0011847");
            Clusters.AddIfNotExist("C0011880", "C0011847");
            Clusters.AddIfNotExist("C0158981", "C0011847");
            Clusters.AddIfNotExist("C0271686", "C0011847");
            Clusters.AddIfNotExist("C0022638", "C0011847");
            Clusters.AddIfNotExist("C0025945", "C0011847");
            Clusters.AddIfNotExist("C0853897", "C0011847");
            Clusters.AddIfNotExist("C0205734", "C0011847");
            Clusters.AddIfNotExist("C0751074", "C0011847");
            Clusters.AddIfNotExist("C1456868", "C0011847");
            Clusters.AddIfNotExist("C0154830", "C0011847");
            Clusters.AddIfNotExist("C0017667", "C0011847");
            Clusters.AddIfNotExist("C0744130", "C0011847");
            Clusters.AddIfNotExist("C1263960", "C0011847");
            Clusters.AddIfNotExist("C0267176", "C0011847");
            Clusters.AddIfNotExist("C0004606", "C0011847");
            Clusters.AddIfNotExist("C0011876", "C0011847");
            Clusters.AddIfNotExist("C0036413", "C0011847");
            Clusters.AddIfNotExist("C0017980", "C0011847");
            Clusters.AddIfNotExist("C0339471", "C0011847");
            Clusters.AddIfNotExist("C0854078", "C0011847");
            Clusters.AddIfNotExist("C0854110", "C0011847");
            Clusters.AddIfNotExist("C0362046", "C0011847");
            Clusters.AddIfNotExist("C0155616", "C0020538");
            Clusters.AddIfNotExist("C0262534", "C0020538");
            Clusters.AddIfNotExist("C0085580", "C0020538");
            Clusters.AddIfNotExist("C0149721", "C0020538");
            Clusters.AddIfNotExist("C0221155", "C0020538");
            Clusters.AddIfNotExist("C0745133", "C0020538");
            Clusters.AddIfNotExist("C0235222", "C0020538");
            Clusters.AddIfNotExist("C0020544", "C0020538");
            Clusters.AddIfNotExist("C0020545", "C0020538");
            Clusters.AddIfNotExist("C0497248", "C0020538");
            Clusters.AddIfNotExist("C0020540", "C0020538");
            Clusters.AddIfNotExist("C0262395", "C0020538");
            Clusters.AddIfNotExist("C0152105", "C0020538");
            Clusters.AddIfNotExist("C0598428", "C0020538");
            Clusters.AddIfNotExist("C0565599", "C0020538");
            Clusters.AddIfNotExist("C0745136", "C0020538");
            Clusters.AddIfNotExist("C1398850", "C0020538");
            Clusters.AddIfNotExist("C0007787", "C0038454");
            Clusters.AddIfNotExist("C0948008", "C0038454");
            Clusters.AddIfNotExist("C0751956", "C0038454");
            Clusters.AddIfNotExist("C0007785", "C0038454");
            Clusters.AddIfNotExist("C0333559", "C0038454");
            Clusters.AddIfNotExist("C0751955", "C0038454");
            Clusters.AddIfNotExist("C0007820", "C0038454");
            Clusters.AddIfNotExist("C0262469", "C0038454");
            Clusters.AddIfNotExist("C1531624", "C0038454");
            Clusters.AddIfNotExist("C0521542", "C0038454");
            Clusters.AddIfNotExist("C0007780", "C0038454");
            Clusters.AddIfNotExist("C0079102", "C0038454");
            Clusters.AddIfNotExist("C0740392", "C0038454");
            Clusters.AddIfNotExist("C0917805", "C0038454");
            Clusters.AddIfNotExist("C0236073", "C0038454");
            Clusters.AddIfNotExist("C0867390", "C0038454");
            Clusters.AddIfNotExist("C0151945", "C0038454");
            Clusters.AddIfNotExist("C0022118", "C0038454");
            Clusters.AddIfNotExist("C0340569", "C0038454");
            Clusters.AddIfNotExist("C0042568", "C0038454");
            Clusters.AddIfNotExist("C0038525", "C0038454");
            Clusters.AddIfNotExist("C0038454", "C0038454");
            Clusters.AddIfNotExist("C0745413", "C0038454");
            Clusters.AddIfNotExist("C0155626", "C0027051");
            Clusters.AddIfNotExist("C1536220", "C0027051");
            Clusters.AddIfNotExist("C0151744", "C0027051");
            Clusters.AddIfNotExist("C0340293", "C0027051");
            Clusters.AddIfNotExist("C0948089", "C0027051");
            Clusters.AddIfNotExist("C0741923", "C0027051");
            Clusters.AddIfNotExist("C1442837", "C0027051");
            Clusters.AddIfNotExist("C0010068", "C0027051");
            Clusters.AddIfNotExist("C1303258", "C0027051");
            Clusters.AddIfNotExist("C0542269", "C0027051");
            Clusters.AddIfNotExist("C1536221", "C0027051");
            Clusters.AddIfNotExist("C0018801", "C0027051");
            Clusters.AddIfNotExist("C0746731", "C0027051");
            Clusters.AddIfNotExist("C0151814", "C0027051");
            Clusters.AddIfNotExist("C0010072", "C0027051");
            Clusters.AddIfNotExist("C0589368", "C0027051");
            Clusters.AddIfNotExist("C0340305", "C0027051");
            Clusters.AddIfNotExist("C0264714", "C0027051");
            Clusters.AddIfNotExist("C0340291", "C0027051");
            Clusters.AddIfNotExist("C0870074", "C0027051");
            Clusters.AddIfNotExist("C0027051", "C0027051");
            Clusters.AddIfNotExist("C0028756", "C0028754");
            Clusters.AddIfNotExist("C0025517", "C0028754");
            Clusters.AddIfNotExist("C0028754", "C0028754");
            Clusters.AddIfNotExist("C0451819", "C0028754");
            Clusters.AddIfNotExist("C0276496", "C0002395");
            Clusters.AddIfNotExist("C0750901", "C0002395");
            Clusters.AddIfNotExist("C0949664", "C0002395");
            Clusters.AddIfNotExist("C0001175", "C0001175");
            Clusters.AddIfNotExist("C0001857", "C0001175");
            Clusters.AddIfNotExist("C0019693", "C0001175");
            Clusters.AddIfNotExist("C0854094", "C0001175");
            Clusters.AddIfNotExist("C1141957", "C0001175");
            Clusters.AddIfNotExist("C0343752", "C0001175");
            Clusters.AddIfNotExist("C1142553", "C0001175");
            Clusters.AddIfNotExist("C0021051", "C0001175");
            Clusters.AddIfNotExist("C0078911", "C0001175");
            Clusters.AddIfNotExist("C0020097", "C0001175");
            Clusters.AddIfNotExist("C0740830", "C0001175");
            Clusters.AddIfNotExist("C0237967", "C0001175");
            Clusters.AddIfNotExist("C0276548", "C0001175");
            Clusters.AddIfNotExist("C0206019", "C0001175");
            Clusters.AddIfNotExist("C0080151", "C0001175");
            Clusters.AddIfNotExist("C0206526", "C0041296");
            Clusters.AddIfNotExist("C0679362", "C0041296");
            Clusters.AddIfNotExist("C0041326", "C0041296");
            Clusters.AddIfNotExist("C0026918", "C0041296");
            Clusters.AddIfNotExist("C0206525", "C0041296");
            Clusters.AddIfNotExist("C0152915", "C0041296");
            Clusters.AddIfNotExist("C0041316", "C0041296");
            Clusters.AddIfNotExist("C0275965", "C0041296");
            Clusters.AddIfNotExist("C0275962", "C0041296");
            Clusters.AddIfNotExist("C0152545", "C0041296");
            Clusters.AddIfNotExist("C0041324", "C0041296");
            Clusters.AddIfNotExist("C0041321", "C0041296");
            Clusters.AddIfNotExist("C0041330", "C0041296");
            Clusters.AddIfNotExist("C0275922", "C0041296");
            Clusters.AddIfNotExist("C0740652", "C0041296");
            Clusters.AddIfNotExist("C0275911", "C0041296");
            Clusters.AddIfNotExist("C0026916", "C0041296");
            Clusters.AddIfNotExist("C0041309", "C0041296");
            Clusters.AddIfNotExist("C0041328", "C0041296");
            Clusters.AddIfNotExist("C0041325", "C0041296");
            Clusters.AddIfNotExist("C0041318", "C0041296");
            Clusters.AddIfNotExist("C0041333", "C0041296");
            Clusters.AddIfNotExist("C0041295", "C0041296");
            Clusters.AddIfNotExist("C0850878", "C0041296");
            Clusters.AddIfNotExist("C0041307", "C0041296");
            Clusters.AddIfNotExist("C0022415", "C0041296");
            Clusters.AddIfNotExist("C0559523", "C0041296");
            Clusters.AddIfNotExist("C0041296", "C0041296");
            Clusters.AddIfNotExist("C0031049", "C0041296");
            Clusters.AddIfNotExist("C0024131", "C0041296");
            Clusters.AddIfNotExist("C0041322", "C0041296");
            Clusters.AddIfNotExist("C0041315", "C0041296");
            Clusters.AddIfNotExist("C0014741", "C0041296");
            Clusters.AddIfNotExist("C0275904", "C0041296");
            Clusters.AddIfNotExist("C0041313", "C0041296");
            Clusters.AddIfNotExist("C0036467", "C0041296");
            Clusters.AddIfNotExist("C0041311", "C0041296");
            Clusters.AddIfNotExist("C0242422", "C0030567");
            Clusters.AddIfNotExist("C0030567", "C0030567");
            Clusters.AddIfNotExist("C0393568", "C0030567");
            Clusters.AddIfNotExist("C0043202", "C0030567");
            Clusters.AddIfNotExist("C0003864", "C0003873");
            Clusters.AddIfNotExist("C0029408", "C0003873");
            Clusters.AddIfNotExist("C0409651", "C0003873");
            Clusters.AddIfNotExist("C0162323", "C0003873");
            Clusters.AddIfNotExist("C0683381", "C0003873");
            Clusters.AddIfNotExist("C0263680", "C0003873");
            Clusters.AddIfNotExist("C0238694", "C0003873");
            Clusters.AddIfNotExist("C1384600", "C0003873");
            Clusters.AddIfNotExist("C0085253", "C0003873");
            Clusters.AddIfNotExist("C0038013", "C0003873");
            Clusters.AddIfNotExist("C0015773", "C0003873");
            Clusters.AddIfNotExist("C0240903", "C0003873");
            Clusters.AddIfNotExist("C0409652", "C0003873");
            Clusters.AddIfNotExist("C0003873", "C0003873");
            Clusters.AddIfNotExist("C0014547", "C0014544");
            Clusters.AddIfNotExist("C0751495", "C0014544");
            Clusters.AddIfNotExist("C1096063", "C0014544");
            Clusters.AddIfNotExist("C0014556", "C0014544");
            Clusters.AddIfNotExist("C0038220", "C0014544");
            Clusters.AddIfNotExist("C0270850", "C0014544");
            Clusters.AddIfNotExist("C0014548", "C0014544");
            Clusters.AddIfNotExist("C0014553", "C0014544");
            Clusters.AddIfNotExist("C0149958", "C0014544");
            Clusters.AddIfNotExist("C0238111", "C0014544");
            Clusters.AddIfNotExist("C0085541", "C0014544");
            Clusters.AddIfNotExist("C0270853", "C0014544");
            Clusters.AddIfNotExist("C0391957", "C0014544");
            Clusters.AddIfNotExist("C0234533", "C0014544");
            Clusters.AddIfNotExist("C0494475", "C0014544");
            Clusters.AddIfNotExist("C0376532", "C0014544");
            Clusters.AddIfNotExist("C0037769", "C0014544");
            Clusters.AddIfNotExist("C0014550", "C0014544");
            Clusters.AddIfNotExist("C0311335", "C0014544");
            Clusters.AddIfNotExist("C0270844", "C0014544");
            Clusters.AddIfNotExist("C0234535", "C0014544");
            Clusters.AddIfNotExist("C0751778", "C0014544");
            Clusters.AddIfNotExist("C0751783", "C0014544");
            Clusters.AddIfNotExist("C0085543", "C0014544");
            Clusters.AddIfNotExist("C0014549", "C0014544");
            Clusters.AddIfNotExist("C0270820", "C0014544");
            Clusters.AddIfNotExist("C0085417", "C0014544");
            Clusters.AddIfNotExist("C0393720", "C0014544");
            Clusters.AddIfNotExist("C0751785", "C0014544");
            Clusters.AddIfNotExist("C0270857", "C0014544");
            Clusters.AddIfNotExist("C0220669", "C0014544");
            Clusters.AddIfNotExist("C0086237", "C0014544");
            Clusters.AddIfNotExist("C0014544", "C0014544");
            Clusters.AddIfNotExist("C0235480", "C0004238");
            Clusters.AddIfNotExist("C0694539", "C0004238");
            Clusters.AddIfNotExist("C0741282", "C0004238");
            Clusters.AddIfNotExist("C0340489", "C0004238");
            Clusters.AddIfNotExist("C0003811", "C0004238");
            Clusters.AddIfNotExist("C0232197", "C0004238");
            Clusters.AddIfNotExist("C1095979", "C0026769");
            Clusters.AddIfNotExist("C0011303", "C0026769");
            Clusters.AddIfNotExist("C0751965", "C0026769");
            Clusters.AddIfNotExist("C0751964", "C0026769");
            Clusters.AddIfNotExist("C0270922", "C0026769");
            Clusters.AddIfNotExist("C0026769", "C0026769");
            Clusters.AddIfNotExist("C0856120", "C0026769");
            Clusters.AddIfNotExist("C0393665", "C0026769");
            Clusters.AddIfNotExist("C0004153", "C0007222");
            Clusters.AddIfNotExist("C0042373", "C0007222");
            Clusters.AddIfNotExist("C0242339", "C0007222");
            Clusters.AddIfNotExist("C0856169", "C0007222");
            Clusters.AddIfNotExist("C0577631", "C0007222");
            Clusters.AddIfNotExist("C1301700", "C0007222");
            Clusters.AddIfNotExist("C0010054", "C0007222");
            Clusters.AddIfNotExist("C0018799", "C0007222");
            Clusters.AddIfNotExist("C0020538", "C0007222");
            Clusters.AddIfNotExist("C0020473", "C0007222");
            Clusters.AddIfNotExist("C0020443", "C0007222");
            Clusters.AddIfNotExist("C0154251", "C0007222");
            Clusters.AddIfNotExist("C0741949", "C0007222");
            Clusters.AddIfNotExist("C0003850", "C0007222");
            Clusters.AddIfNotExist("C0342649", "C0007222");
            Clusters.AddIfNotExist("C1096293", "C0007222");
            Clusters.AddIfNotExist("C0598608", "C0007222");
            Clusters.AddIfNotExist("C0020445", "C0007222");
            Clusters.AddIfNotExist("C0018802", "C0007222");
            Clusters.AddIfNotExist("C1290386", "C0007222");
            Clusters.AddIfNotExist("C0020557", "C0007222");
            Clusters.AddIfNotExist("C0206064", "C0007222");
            Clusters.AddIfNotExist("C1273070", "C0007222");
            Clusters.AddIfNotExist("C0264716", "C0007222");
            Clusters.AddIfNotExist("C0264694", "C0007222");
            Clusters.AddIfNotExist("C0027821", "C0007222");
            Clusters.AddIfNotExist("C1135191", "C0007222");
            Clusters.AddIfNotExist("C1135196", "C0007222");
            Clusters.AddIfNotExist("C0747057", "C0007222");
            Clusters.AddIfNotExist("C0007222", "C0007222");
            Clusters.AddIfNotExist("C0850624", "C0007222");
            Clusters.AddIfNotExist("C0730607", "C0024117");
            Clusters.AddIfNotExist("C0600260", "C0024117");
            Clusters.AddIfNotExist("C1527303", "C0024117");
            Clusters.AddIfNotExist("C0024115", "C0024117");
            Clusters.AddIfNotExist("C0740304", "C0024117");
            Clusters.AddIfNotExist("C0264492", "C0024117");
            Clusters.AddIfNotExist("C0746102", "C0024117");
            Clusters.AddIfNotExist("C0155874", "C0024117");
            Clusters.AddIfNotExist("C0699949", "C0024117");
            Clusters.AddIfNotExist("C0001883", "C0024117");
            Clusters.AddIfNotExist("C1145670", "C0024117");
            Clusters.AddIfNotExist("C0221725", "C0024117");
            Clusters.AddIfNotExist("C0746982", "C0024117");
            Clusters.AddIfNotExist("C0006266", "C0024117");
            Clusters.AddIfNotExist("C0024117", "C0024117");
            Clusters.AddIfNotExist("C0340044", "C0024117");
            Clusters.AddIfNotExist("C0858318", "C0024530");
            Clusters.AddIfNotExist("C0858321", "C0024530");
            Clusters.AddIfNotExist("C0024535", "C0024530");
            Clusters.AddIfNotExist("C0024537", "C0024530");
            Clusters.AddIfNotExist("C0024534", "C0024530");
            Clusters.AddIfNotExist("C0858319", "C0024530");
            Clusters.AddIfNotExist("C0858320", "C0024530");
            Clusters.AddIfNotExist("C0743841", "C0024530");
            Clusters.AddIfNotExist("C1336827", "C0024530");
            Clusters.AddIfNotExist("C0033740", "C0024530");
            Clusters.AddIfNotExist("C0024530", "C0024530");
            Clusters.AddIfNotExist("C0024533", "C0024530");
            Clusters.AddIfNotExist("C0276832", "C0024530");
            Clusters.AddIfNotExist("C0032300", "C0032285");
            Clusters.AddIfNotExist("C0004626", "C0032285");
            Clusters.AddIfNotExist("C0032310", "C0032285");
            Clusters.AddIfNotExist("C0006285", "C0032285");
            Clusters.AddIfNotExist("C0264515", "C0032285");
            Clusters.AddIfNotExist("C0949083", "C0032285");
            Clusters.AddIfNotExist("C0032302", "C0032285");
            Clusters.AddIfNotExist("C0740766", "C0032285");
            Clusters.AddIfNotExist("C1412002", "C0032285");
            Clusters.AddIfNotExist("C0155870", "C0032285");
            Clusters.AddIfNotExist("C0032290", "C0032285");
            Clusters.AddIfNotExist("C0339968", "C0032285");
            Clusters.AddIfNotExist("C0519030", "C0032285");
            Clusters.AddIfNotExist("C1142578", "C0032285");
            Clusters.AddIfNotExist("C0276253", "C0032285");
            Clusters.AddIfNotExist("C0264383", "C0032285");
            Clusters.AddIfNotExist("C0032298", "C0032285");
            Clusters.AddIfNotExist("C1527407", "C0032285");
            Clusters.AddIfNotExist("C0242770", "C0032285");
            Clusters.AddIfNotExist("C1279386", "C0032285");
            Clusters.AddIfNotExist("C0032241", "C0032285");
            Clusters.AddIfNotExist("C0032285", "C0032285");
            Clusters.AddIfNotExist("C0023241", "C0032285");
            Clusters.AddIfNotExist("C0029291", "C0032285");
            Clusters.AddIfNotExist("C0276688", "C0032285");
            Clusters.AddIfNotExist("C0242459", "C0032285");
            Clusters.AddIfNotExist("C0238378", "C0032285");
            Clusters.AddIfNotExist("C0008680", "C0032285");
            Clusters.AddIfNotExist("C0543829", "C0032285");
            Clusters.AddIfNotExist("C0155866", "C0032285");
            Clusters.AddIfNotExist("C1142536", "C0032285");
            Clusters.AddIfNotExist("C0524688", "C0032285");
            Clusters.AddIfNotExist("C0032308", "C0032285");
            Clusters.AddIfNotExist("C0032306", "C0032285");
            Clusters.AddIfNotExist("C0747690", "C0032285");
            Clusters.AddIfNotExist("C0339971", "C0032285");
            Clusters.AddIfNotExist("C0339961", "C0032285");
            Clusters.AddIfNotExist("C1535939", "C0032285");
            Clusters.AddIfNotExist("C0206061", "C0032285");
            Clusters.AddIfNotExist("C0085786", "C0032285");
            Clusters.AddIfNotExist("C0242966", "C0036690");
            Clusters.AddIfNotExist("C0684256", "C0036690");
            Clusters.AddIfNotExist("C0036685", "C0036690");
            Clusters.AddIfNotExist("C1141926", "C0036690");
            Clusters.AddIfNotExist("C0456103", "C0036690");
            Clusters.AddIfNotExist("C0149801", "C0036690");
            Clusters.AddIfNotExist("C0243026", "C0036690");
            Clusters.AddIfNotExist("C0036690", "C0036690");
            Clusters.AddIfNotExist("C0042749", "C0036690");
            Clusters.AddIfNotExist("C0158944", "C0036690");
            Clusters.AddIfNotExist("C0025306", "C0036690");
            Clusters.AddIfNotExist("C0343525", "C0036690");
            Clusters.AddIfNotExist("C0393642", "C0036690");
            Clusters.AddIfNotExist("C0152965", "C0036690");
            Clusters.AddIfNotExist("C1096452", "C0036690");
            Clusters.AddIfNotExist("C0269936", "C0036690");
            Clusters.AddIfNotExist("C0152964", "C0036690");
            Clusters.AddIfNotExist("C0152966", "C0036690");
            Clusters.AddIfNotExist("C1142182", "C0036690");
            Clusters.AddIfNotExist("C1565489", "C0022661");
            Clusters.AddIfNotExist("C0035078", "C0022661");
            Clusters.AddIfNotExist("C0743496", "C0022661");
            Clusters.AddIfNotExist("C0022661", "C0022661");
            Clusters.AddIfNotExist("C0521839", "C0021400");
            Clusters.AddIfNotExist("C0858004", "C0021400");
            Clusters.AddIfNotExist("C0016627", "C0021400");
            Clusters.AddIfNotExist("C0276357", "C0021400");
            Clusters.AddIfNotExist("C0021400", "C0021400");
            Clusters.AddIfNotExist("C0030389", "C0021400");
            Clusters.AddIfNotExist("C0264219", "C0021400");
            Clusters.AddIfNotExist("C0042769", "C0021400");
            Clusters.AddIfNotExist("C0035243", "C0021400");
            Clusters.AddIfNotExist("C0339901", "C0021400");
            Clusters.AddIfNotExist("C0023882", "C0023882");
            Clusters.AddIfNotExist("C0007789", "C0023882");
            Clusters.AddIfNotExist("C0034372", "C0023882");
            Clusters.AddIfNotExist("C0221165", "C0023882");
            Clusters.AddIfNotExist("C0392549", "C0023882");
            Clusters.AddIfNotExist("C0024143", "C0024141");
            Clusters.AddIfNotExist("C0004364", "C0024141");
            Clusters.AddIfNotExist("C0409974", "C0024141");
            Clusters.AddIfNotExist("C0752335", "C0024141");
            Clusters.AddIfNotExist("C0024137", "C0024141");
            Clusters.AddIfNotExist("C0024138", "C0024141");
            Clusters.AddIfNotExist("C0024140", "C0024141");
            Clusters.AddIfNotExist("C0009326", "C0024141");
            Clusters.AddIfNotExist("C0409979", "C0024141");
            Clusters.AddIfNotExist("C0030327", "C0024141");
            Clusters.AddIfNotExist("C0024141", "C0024141");
            Clusters.AddIfNotExist("C0238644", "C0002871");
            Clusters.AddIfNotExist("C0162316", "C0002871");
            Clusters.AddIfNotExist("C0581384", "C0002871");
            Clusters.AddIfNotExist("C0002886", "C0002871");
            Clusters.AddIfNotExist("C1142276", "C0002871");
            Clusters.AddIfNotExist("C0002873", "C0002871");
            Clusters.AddIfNotExist("C0002888", "C0002871");
            Clusters.AddIfNotExist("C0002878", "C0002871");
            Clusters.AddIfNotExist("C0039730", "C0002871");
            Clusters.AddIfNotExist("C0271979", "C0002871");
            Clusters.AddIfNotExist("C0002875", "C0002871");
            Clusters.AddIfNotExist("C0472767", "C0002871");
            Clusters.AddIfNotExist("C0002893", "C0002871");
            Clusters.AddIfNotExist("C0002895", "C0002871");
            Clusters.AddIfNotExist("C0019034", "C0002871");
            Clusters.AddIfNotExist("C0002896", "C0002871");
            Clusters.AddIfNotExist("C0221021", "C0002871");
            Clusters.AddIfNotExist("C0002874", "C0002871");
            Clusters.AddIfNotExist("C0002892", "C0002871");
            Clusters.AddIfNotExist("C0002880", "C0002871");
            Clusters.AddIfNotExist("C0037889", "C0002871");
            Clusters.AddIfNotExist("C0085576", "C0002871");
            Clusters.AddIfNotExist("C0002881", "C0002871");
            Clusters.AddIfNotExist("C0002884", "C0002871");
            Clusters.AddIfNotExist("C0002879", "C0002871");
            Clusters.AddIfNotExist("C0267834", "C0010709");
            Clusters.AddIfNotExist("C0022679", "C0010709");
            Clusters.AddIfNotExist("C0078981", "C0010709");
            Clusters.AddIfNotExist("C0268800", "C0010709");
            Clusters.AddIfNotExist("C0158683", "C0010709");
            Clusters.AddIfNotExist("C0034543", "C0010709");
            Clusters.AddIfNotExist("C0085413", "C0010709");
            Clusters.AddIfNotExist("C0030283", "C0010709");
            Clusters.AddIfNotExist("C0029927", "C0010709");
            Clusters.AddIfNotExist("C0022680", "C0010709");
            Clusters.AddIfNotExist("C0334054", "C0010709");
            Clusters.AddIfNotExist("C0025060", "C0010709");
            Clusters.AddIfNotExist("C0272407", "C0010709");
            Clusters.AddIfNotExist("C0028879", "C0010709");
            Clusters.AddIfNotExist("C0011428", "C0010709");
            Clusters.AddIfNotExist("C0040072", "C0010709");
            Clusters.AddIfNotExist("C0085548", "C0010709");
            Clusters.AddIfNotExist("C0341038", "C0010709");
            Clusters.AddIfNotExist("C0025467", "C0010709");
            Clusters.AddIfNotExist("C0849748", "C0010709");
            Clusters.AddIfNotExist("C0152244", "C0010709");
            Clusters.AddIfNotExist("C0031925", "C0010709");
            Clusters.AddIfNotExist("C0085648", "C0010709");
            Clusters.AddIfNotExist("C1258666", "C0010709");
            Clusters.AddIfNotExist("C0031038", "C0010709");
            Clusters.AddIfNotExist("C1142249", "C0010709");
            Clusters.AddIfNotExist("C0546483", "C0010709");
            Clusters.AddIfNotExist("C0155285", "C0010709");
            Clusters.AddIfNotExist("C0340978", "C0340978");
            Clusters.AddIfNotExist("C0029453", "C0029456");
            Clusters.AddIfNotExist("C0029458", "C0029456");
            Clusters.AddIfNotExist("C0521170", "C0029456");
            Clusters.AddIfNotExist("C0858714", "C0029456");
            Clusters.AddIfNotExist("C0005944", "C0029456");
            Clusters.AddIfNotExist("C0747079", "C0029456");
            Clusters.AddIfNotExist("C0853662", "C0029456");
            Clusters.AddIfNotExist("C0001787", "C0029456");
            Clusters.AddIfNotExist("C0005940", "C0029456");
            Clusters.AddIfNotExist("C0035579", "C0029456");
            Clusters.AddIfNotExist("C0035086", "C0029456");
            Clusters.AddIfNotExist("C0029456", "C0029456");
            Clusters.AddIfNotExist("C0158447", "C0029456");
            Clusters.AddIfNotExist("C0029459", "C0029456");
            #endregion Clusters

            string line = String.Empty;
            long cnt = Int64.MaxValue;
            //long cnt = 1000000;
            int block = 10000;
            List<string> content = new List<string>();

            while ((line = sr.ReadLine()) != null)
            {
                foreach (var item in Substitutes)
                {
                    if (line.Contains(item.Key))
                    {
                        System.IO.File.AppendAllText(fnDeb, line + " ==> " + item.Key + "\r\n");
                        SubsCounter.AddIfNotExist(item.Value, 0);
                        SubsCounter[item.Value]++;

                        line = line.Replace(item.Key, item.Value);
                    }
                }
                foreach (var item in SubstitutesWords)
                {
                    if (line.Contains(item.Key))
                    {
                        System.IO.File.AppendAllText(fnDeb, line + " --> " + item.Key + "\r\n");
                        SubsCounter.AddIfNotExist(item.Value, 0);
                        SubsCounter[item.Value]++;

                        line = line.ReplaceWholeWord(item.Key, item.Value);
                    }
                }

                foreach (var item in Clusters)
                {
                    if (item.Key != item.Value && line.Contains(item.Key))
                    {
                        System.IO.File.AppendAllText(fnDeb, line + " ~~> " + item.Key + "\r\n");
                        SubsCounter.AddIfNotExist(item.Value, 0);
                        SubsCounter[item.Value]++;

                        line = line.Replace(item.Key, item.Value);
                    }
                }

                content.Add(line);
                cnt--;
                block--;

                if (cnt <= 0 || block == 0)
                {
                    System.IO.File.AppendAllLines(fnRes, content.ToArray());
                    content.Clear();
                    block = 10000;
                    if (cnt <= 0) break;
                }

            }

            foreach (var item in SubsCounter)
            {
                System.IO.File.AppendAllText(fnDeb, item.Key + " ==== " + item.Value + "\r\n");
            }
            MessageBox.Show("Ready!");
        }

        private int CountWords(string source)
        {
            int count = 1;
            int n = 0;

            while ((n = source.IndexOf(' ', n)) != -1)
            {
                n++;
                count++;
            }
            return count;
        }

        private void ProcessEntityChunk(int from, int to)
        {
            Dictionary<string, string> CUIs = new Dictionary<string, string>();
            Dictionary<string, int> CUIcnt = new Dictionary<string, int>();

            MySqlConnection mycn = null;
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader($"select Text, CUI from entity where SENTENCE_ID BETWEEN {from} AND {to}", mycn))
            {
                while (dataRdr.Read())
                {
                    string text = dataRdr.GetString(0).ToLower();
                    string CUI = dataRdr.GetString(1);

                    if (CUIcnt.ContainsKey(text))
                    {
                        CUIcnt[text]++;
                    }
                    else
                    {
                        CUIcnt.Add(text, 1);
                        CUIs.Add(text, CUI);
                    }
                }
            }

            var t = CUIs.Select(x => CountWords(x.Key).ToString() + "|" + x.Key + "|" + x.Value + "|" + CUIcnt[x.Key].ToString()).ToArray();
            File.WriteAllText($@"R:\PubMed\Entity2CUI\{from}-{to}.csv", string.Join("\r\n", t));
        }

        private void button10_Click(object sender, EventArgs e)
        {

            Task[] tasks1 = new Task[19]
{
new Task(() => ProcessEntityChunk(153000001,163000000)),
new Task(() => ProcessEntityChunk(163000001,173000000)),
new Task(() => ProcessEntityChunk(173000001,183000000)),
new Task(() => ProcessEntityChunk(183000001,193000000)),
new Task(() => ProcessEntityChunk(193000001,203000000)),
new Task(() => ProcessEntityChunk(203000001,213000000)),
new Task(() => ProcessEntityChunk(213000001,223000000)),
new Task(() => ProcessEntityChunk(223000001,233000000)),
new Task(() => ProcessEntityChunk(233000001,243000000)),
new Task(() => ProcessEntityChunk(243000001,253000000)),
new Task(() => ProcessEntityChunk(253000001,263000000)),
new Task(() => ProcessEntityChunk(263000001,273000000)),
new Task(() => ProcessEntityChunk(273000001,283000000)),
new Task(() => ProcessEntityChunk(283000001,293000000)),
new Task(() => ProcessEntityChunk(293000001,303000000)),
new Task(() => ProcessEntityChunk(303000001,313000000)),
new Task(() => ProcessEntityChunk(313000001,323000000)),
new Task(() => ProcessEntityChunk(323000001,333000000)),
new Task(() => ProcessEntityChunk(333000001,343000000))


};
            foreach (var t in tasks1)
                t.Start();
            Task.WaitAll(tasks1); // ожидаем завершения задач 

            MessageBox.Show("!");

        }

        private void button11_Click(object sender, EventArgs e)
        {
            StreamReader reader = new StreamReader(@"R:\PubMed\Entity2CUI\output.txt");
            string line;
            Dictionary<string, int> CUIs = new Dictionary<string, int>();

            while ((line = reader.ReadLine()) != null)
            {
                string[] splitted = line.Split('|');
                string l = splitted[0];
                string word = splitted[1];
                string CUI = splitted[2];
                int cnt = splitted[3].ToInt32();

                string key = (l + "|" + word + "|" + CUI).Replace("(", "").Replace(")", "").Replace(",", "").Replace(";", "");

                if (!CUIs.ContainsKey(key))
                {
                    CUIs.Add(key, cnt);
                }
                else
                {
                    CUIs[key] = CUIs[key] + cnt;
                }
            }

            var sorted = CUIs.OrderByDescending(x => x.Value);
            var t = sorted.Select(x => x.Key + "|" + x.Value).ToArray();
            File.WriteAllText(@"R:\PubMed\Entity2CUI\Words2CUI.csv", string.Join("\r\n", t));

        }

        private void PrepareConcepts(string Words2CUI = "")
        {
            var sw = File.ReadLines(@"R:\PubMed\Entity2CUI\stopWords.txt").ToArray();
            stopWords = new HashSet<string>(sw);

            string _Words2CUI = @"R:\PubMed\Entity2CUI\Words2CUI.csv";
            if (Words2CUI != "") _Words2CUI = Words2CUI;

            StreamReader reader = new StreamReader(_Words2CUI);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] splitted = line.Split('|');
                int l = splitted[0].ToInt32();
                string word = splitted[1];
                string CUI = splitted[2];
                int cnt = splitted[3].ToInt32();

                if (CUI == "") continue;

                //if (word == "solitary ectopic kidney")
                //{
                //    int oi = 0;
                //}

                if (cnt < 70) break; // Do not read rare concepts at all - they are sorted

                if (l < 8 && cnt > 70 && word.Length > 1)
                {
                    if (l == 1) w1.AddIfNotExist(word, CUI);
                    else
                    {
                        string[] splittedWord = word.Split(' ');

                        if (l == 2)
                        {
                            if (!w2.ContainsKey(splittedWord[0]))
                            {
                                w2.Add(splittedWord[0], new Dictionary<string, string>());
                            }
                            w2[splittedWord[0]].AddIfNotExist(splittedWord[1], CUI);
                        }


                        if (l == 3)
                        {
                            if (!w3.ContainsKey(splittedWord[0]))
                            {
                                w3.Add(splittedWord[0], new Dictionary<string, Dictionary<string, string>>());
                            }
                            if (!w3[splittedWord[0]].ContainsKey(splittedWord[1]))
                            {
                                w3[splittedWord[0]].Add(splittedWord[1], new Dictionary<string, string>());
                            }
                            w3[splittedWord[0]][splittedWord[1]].AddIfNotExist(splittedWord[2], CUI);
                        }

                        if (l == 4)
                        {
                            if (!w4.ContainsKey(splittedWord[0]))
                            {
                                w4.Add(splittedWord[0], new Dictionary<string, Dictionary<string, Dictionary<string, string>>>());
                            }
                            if (!w4[splittedWord[0]].ContainsKey(splittedWord[1]))
                            {
                                w4[splittedWord[0]].Add(splittedWord[1], new Dictionary<string, Dictionary<string, string>>());
                            }
                            if (!w4[splittedWord[0]][splittedWord[1]].ContainsKey(splittedWord[2]))
                            {
                                w4[splittedWord[0]][splittedWord[1]].Add(splittedWord[2], new Dictionary<string, string>());
                            }
                            w4[splittedWord[0]][splittedWord[1]][splittedWord[2]].AddIfNotExist(splittedWord[3], CUI);
                        }

                        if (l == 5)
                        {
                            if (!w5.ContainsKey(splittedWord[0]))
                            {
                                w5.Add(splittedWord[0], new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>());
                            }
                            if (!w5[splittedWord[0]].ContainsKey(splittedWord[1]))
                            {
                                w5[splittedWord[0]].Add(splittedWord[1], new Dictionary<string, Dictionary<string, Dictionary<string, string>>>());
                            }
                            if (!w5[splittedWord[0]][splittedWord[1]].ContainsKey(splittedWord[2]))
                            {
                                w5[splittedWord[0]][splittedWord[1]].Add(splittedWord[2], new Dictionary<string, Dictionary<string, string>>());
                            }
                            if (!w5[splittedWord[0]][splittedWord[1]][splittedWord[2]].ContainsKey(splittedWord[3]))
                            {
                                w5[splittedWord[0]][splittedWord[1]][splittedWord[2]].Add(splittedWord[3], new Dictionary<string, string>());
                            }

                            w5[splittedWord[0]][splittedWord[1]][splittedWord[2]][splittedWord[3]].AddIfNotExist(splittedWord[4], CUI);
                        }


                        if (l == 6)
                        {
                            if (!w6.ContainsKey(splittedWord[0]))
                            {
                                w6.Add(splittedWord[0], new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>());
                            }
                            if (!w6[splittedWord[0]].ContainsKey(splittedWord[1]))
                            {
                                w6[splittedWord[0]].Add(splittedWord[1], new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>());
                            }
                            if (!w6[splittedWord[0]][splittedWord[1]].ContainsKey(splittedWord[2]))
                            {
                                w6[splittedWord[0]][splittedWord[1]].Add(splittedWord[2], new Dictionary<string, Dictionary<string, Dictionary<string, string>>>());
                            }
                            if (!w6[splittedWord[0]][splittedWord[1]][splittedWord[2]].ContainsKey(splittedWord[3]))
                            {
                                w6[splittedWord[0]][splittedWord[1]][splittedWord[2]].Add(splittedWord[3], new Dictionary<string, Dictionary<string, string>>());
                            }
                            if (!w6[splittedWord[0]][splittedWord[1]][splittedWord[2]][splittedWord[3]].ContainsKey(splittedWord[4]))
                            {
                                w6[splittedWord[0]][splittedWord[1]][splittedWord[2]][splittedWord[3]].Add(splittedWord[4], new Dictionary<string, string>());
                            }
                            w6[splittedWord[0]][splittedWord[1]][splittedWord[2]][splittedWord[3]][splittedWord[4]].AddIfNotExist(splittedWord[5], CUI);
                        }

                        if (l == 7)
                        {
                            if (!w7.ContainsKey(splittedWord[0]))
                            {
                                w7.Add(splittedWord[0], new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>>());
                            }
                            if (!w7[splittedWord[0]].ContainsKey(splittedWord[1]))
                            {
                                w7[splittedWord[0]].Add(splittedWord[1], new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>());
                            }
                            if (!w7[splittedWord[0]][splittedWord[1]].ContainsKey(splittedWord[2]))
                            {
                                w7[splittedWord[0]][splittedWord[1]].Add(splittedWord[2], new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>());
                            }
                            if (!w7[splittedWord[0]][splittedWord[1]][splittedWord[2]].ContainsKey(splittedWord[3]))
                            {
                                w7[splittedWord[0]][splittedWord[1]][splittedWord[2]].Add(splittedWord[3], new Dictionary<string, Dictionary<string, Dictionary<string, string>>>());
                            }
                            if (!w7[splittedWord[0]][splittedWord[1]][splittedWord[2]][splittedWord[3]].ContainsKey(splittedWord[4]))
                            {
                                w7[splittedWord[0]][splittedWord[1]][splittedWord[2]][splittedWord[3]].Add(splittedWord[4], new Dictionary<string, Dictionary<string, string>>());
                            }
                            if (!w7[splittedWord[0]][splittedWord[1]][splittedWord[2]][splittedWord[3]][splittedWord[4]].ContainsKey(splittedWord[5]))
                            {
                                w7[splittedWord[0]][splittedWord[1]][splittedWord[2]][splittedWord[3]][splittedWord[4]].Add(splittedWord[5], new Dictionary<string, string>());
                            }
                            w7[splittedWord[0]][splittedWord[1]][splittedWord[2]][splittedWord[3]][splittedWord[4]][splittedWord[5]].AddIfNotExist(splittedWord[6], CUI);
                        }
                    }
                }
            }

        }

        private void button12_Click(object sender, EventArgs e)
        {

            /*
            HashSet<string> stopWords = new HashSet<string>(sw);

            Dictionary<string, string> w1 = new Dictionary<string, string>();
            Dictionary<string, Dictionary<string, string>> w2 = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, Dictionary<string, Dictionary<string, string>>> w3 = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>> w4 = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>();
            Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>> w5 = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>();
            Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>> w6 = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>>();
            Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>>> w7 = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>>>();
            */

            PrepareConcepts();



            #region ---------- REPLACE BLOCK --------


            string[] files = Directory.GetFiles(@"R:\PubMed\COVID19\noncomm_use_subset\noncomm_use_subset", "*.txt", SearchOption.AllDirectories);
            foreach (var item in files)
            {
                List<string> resLines = new List<string>();

                var srcLines = File.ReadLines(item).ToArray();
                foreach (var lineTxt in srcLines)
                {

                    //string lineTxt = "logistic regression, analysis (solitary ectopic kidney) Precise e.coli, and e. coli, spatial and temporal regulation of gene expression is essential to many biological processes One class of cis-regulatory elements that plays a major role in transcriptional regulation of gene expression is enhancers Enhancers are classically defined as stretches of non-coding DNA that promote transcription of target gene(s) irrespective of genomic context, orientation, and, to a substantial extent, distance as well (Blackwood and Kadonaga, 1998) Enhancers are often cell-type specific, allowing precise spatiotemporal control of gene transcription in different cell types within an organism (Heintzman et al., 2009; Nord et al., 2013)";

                    string[] splittedText = lineTxt.ToLower().Split(new char[] { ' ', ')', '(', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);

                    int sl = splittedText.Length;

                    List<string> res = new List<string>();
                    for (int i = 0; i < sl; i++)
                    {
                        if (stopWords.Contains(splittedText[i]))
                        {
                            res.Add(splittedText[i]);
                            continue;
                        }

                        if (w7.ContainsKey(splittedText[i]) && sl > i + 6)
                        {
                            if (w7[splittedText[i]].ContainsKey(splittedText[i + 1]) && w7[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]].ContainsKey(splittedText[i + 5]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]].ContainsKey(splittedText[i + 6]))
                            {
                                res.Add(w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]][splittedText[i + 6]]);
                                i = i + 6;
                                continue;
                            }
                        }
                        if (w6.ContainsKey(splittedText[i]) && sl > i + 5)
                        {
                            if (w6[splittedText[i]].ContainsKey(splittedText[i + 1]) && w6[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]].ContainsKey(splittedText[i + 5]))
                            {
                                res.Add(w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]]);
                                i = i + 5;
                                continue;
                            }
                        }
                        if (w5.ContainsKey(splittedText[i]) && sl > i + 4)
                        {
                            if (w5[splittedText[i]].ContainsKey(splittedText[i + 1]) && w5[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]))
                            {
                                res.Add(w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]]);
                                i = i + 4;
                                continue;
                            }
                        }
                        if (w4.ContainsKey(splittedText[i]) && sl > i + 3)
                        {
                            if (w4[splittedText[i]].ContainsKey(splittedText[i + 1]) && w4[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w4[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]))
                            {
                                res.Add(w4[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]]);
                                i = i + 3;
                                continue;
                            }
                        }
                        if (w3.ContainsKey(splittedText[i]) && sl > i + 2)
                        {
                            if (w3[splittedText[i]].ContainsKey(splittedText[i + 1]) && w3[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]))
                            {
                                res.Add(w3[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]]);
                                i = i + 2;
                                continue;
                            }
                        }
                        if (w2.ContainsKey(splittedText[i]) && sl > i + 1)
                        {
                            if (w2[splittedText[i]].ContainsKey(splittedText[i + 1]))
                            {
                                res.Add(w2[splittedText[i]][splittedText[i + 1]]);
                                i = i + 1;
                                continue;
                            }
                        }
                        if (w1.ContainsKey(splittedText[i]))
                        {
                            res.Add(w1[splittedText[i]]);
                            continue;
                        }

                        res.Add(splittedText[i]);

                    }

                    string resLine = String.Join(" ", res);
                    resLines.Add(resLine);
                }
                File.WriteAllText(item.Replace(".txt", ".res"), string.Join("\r\n", resLines.ToArray()));
            }
            #endregion ---------- REPLACE BLOCK --------
        }

        private void button13_Click(object sender, EventArgs e)
        {
            //string fn = @"R:\PubMed\COVID19\noncomm_use_subset\noncomm_use_subset";
            string fn = @"R:\PubMed\COVID19_2\document_parses\pmc_json";

            string[] files = Directory.GetFiles(fn, "*.json", SearchOption.AllDirectories);
            //System.IO.Directory.GetFiles(x)

            // Iterate all JSON in folder
            foreach (var item in files)
            {
                string json = File.ReadAllText(item);
                JObject jsonO = JObject.Parse(json);

                string title = jsonO["metadata"]["title"].ToString().ToLower().Replace("fig. ", "fig ").Replace("e.g. ", "e.g ").Replace("u.s. ", "u.s ").Replace("al. ", "al ").Replace("vs. ", "vs ").Replace("i.e. ", "i.e ");

                string abstrac = "";
                if (jsonO["abstract"] != null) abstrac = string.Join(" ", jsonO["abstract"].Select(x => x["text"].ToString()).ToArray()).ToLower().Replace("fig. ", "fig ").Replace("e.g. ", "e.g ").Replace("u.s. ", "u.s ").Replace("al. ", "al ").Replace("vs. ", "vs ").Replace("i.e. ", "i.e ");
                string body_text = string.Join(" ", jsonO["body_text"].Select(x => x["text"].ToString()).ToArray()).ToLower().Replace("fig. ", "fig ").Replace("e.g. ", "e.g ").Replace("u.s. ", "u.s ").Replace("al. ", "al ").Replace("vs. ", "vs ").Replace("i.e. ", "i.e ");

                string[] splittedtitle = title.Split(". ", StringSplitOptions.RemoveEmptyEntries);
                string[] splittedabstrac = abstrac.Split(". ", StringSplitOptions.RemoveEmptyEntries);
                string[] splittedbody_text = body_text.Split(". ", StringSplitOptions.RemoveEmptyEntries);

                string[] result = splittedtitle.Union(splittedabstrac).Union(splittedbody_text).ToArray();

                File.WriteAllText(item.Replace(".json", ".txt"), string.Join("\r\n", result));
            }



        }

        private void button14_Click(object sender, EventArgs e)
        {
            List<string> insert_list = new List<string>();

            //////string fn = @"R:\PubMed\COVID19\noncomm_use_subset\noncomm_use_subset";
            //////string fn = @"R:\PubMed\COVID19\custom_license\custom_license";
            //////string fn = @"R:\PubMed\COVID19\comm_use_subset\comm_use_subset";
            //string fn = @"R:\PubMed\COVID19\biorxiv_medrxiv\biorxiv_medrxiv";

            //string fn = @"R:\PubMed\COVID19_2\document_parses\pdf_json";
            string fn = @"R:\PubMed\COVID19_2\document_parses\pmc_json";

            #region
            /*
            HashSet<string> pmid = new HashSet<string>();

            MySqlConnection _mycn = null;
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT sha FROM covid2.metadata e WHERE SHA=pmcid AND length(sha)>3", _mycn))
            {
                while (dataRdr.Read())
                {
                    string text = dataRdr.GetString(0);

                    pmid.AddIfNotExist(text);
                }
            }
            */
            #endregion


            //R:\PubMed\COVID19_2\document_parses\pdf_json\77ba146a186708d66b9743b410d04cd14b003dbf.json
            string[] files = Directory.GetFiles(fn, "*.json", SearchOption.AllDirectories);

            // Iterate all JSON in folder
            foreach (var item in files)
            {
                string sha = Path.GetFileNameWithoutExtension(item);

                sha = sha.BeforeSafe(".");
                //if (!pmid.Contains(sha)) continue;

                // If loading fails due to any reason (out of mem for example) we can continue from now on -  SELECT MAX(sha) FROM  covid2.sentences
                //int compres = string.Compare(sha, "cc36cdafc856380f3490d1bfd0931d76261e69ee");
                //if (compres<=0) continue;

                //int compres = string.Compare(sha, "PMC3219342");
                //if (compres != 0) continue;

                string json = File.ReadAllText(item);
                JObject jsonO = JObject.Parse(json);

                string title = jsonO["metadata"]["title"].ToString();
                insert_list.Add("('" + sha + "','ti',1, '" + title.Replace("\\`", "'").Replace('′', '\'').Replace('ʹ', '\'').Replace('ʻ', '\'').Replace('ʼ', '\'').Replace('`', '\'').Replace('ˈ', '\'').Replace("\'", "").SQLString() + " ')\r\n");
                MyInsertOverNItems(ref insert_list, "INSERT INTO covid2.sentences(sha,type, num, content)VALUES", 500);

                int n = 0;
                if (jsonO["abstract"] != null)
                {
                    foreach (var im in jsonO["abstract"])
                    {
                        n++;
                        string uu = im["text"].ToString();
                        insert_list.Add("('" + sha + "','ab'," + n + ", '" + uu.Replace("\\`", "'").Replace('′', '\'').Replace('ʹ', '\'').Replace('ʻ', '\'').Replace('ʼ', '\'').Replace('`', '\'').Replace('ˈ', '\'').Replace("\'", "").SQLString() + " ')\r\n");
                        MyInsertOverNItems(ref insert_list, "INSERT INTO covid2.sentences(sha,type, num, content)VALUES", 500);
                    }
                }

                n = 0;
                foreach (var im in jsonO["body_text"])
                {
                    n++;
                    string uu = im["text"].ToString();
                    insert_list.Add("('" + sha + "','bd'," + n + ", '" + uu.Replace("\\`", "'").Replace("\\\'", "").Replace('′', '\'').Replace('ʹ', '\'').Replace('ʻ', '\'').Replace('ʼ', '\'').Replace('`', '\'').Replace('ˈ', '\'').Replace("\'", "").SQLString() + " ')\r\n");
                    try
                    {
                        MyInsertOverNItems(ref insert_list, "INSERT INTO covid2.sentences(sha,type, num, content)VALUES", 500);
                    }
                    catch (Exception ex)
                    {
                        string tyty = uu.Replace("\\`", "'").Replace("\\\'", "").Replace('′', '\'').Replace('ʹ', '\'').Replace('ʻ', '\'').Replace('ʼ', '\'').Replace('`', '\'').Replace('ˈ', '\'').SQLString();
                        string ezzz = ex.Message;
                    }

                }


            }

            MyInsertOverNItems(ref insert_list, "INSERT INTO covid2.sentences(sha,type, num, content)VALUES");

            MessageBox.Show("!");

        }

        private void button15_Click(object sender, EventArgs e)
        {
            // Make CUI replacements with positions
            PrepareConcepts();


            List<string> insert_list = new List<string>();


            MySqlConnection mycn = null;
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader($"select id, content from covid2.sentences", mycn))
            {
                while (dataRdr.Read())
                {
                    int id = dataRdr.GetInt32(0);
                    string lineTxt = dataRdr.GetString(1).ToLower();

                    //string lineTxt = "logistic regression, analysis (solitary ectopic kidney) Precise e.coli, and e. coli, spatial and temporal regulation of gene expression is essential to many biological processes. One class of cis-regulatory elements that plays a major role in transcriptional regulation of gene expression is enhancers Enhancers are classically defined as stretches of non-coding DNA that promote transcription of target gene(s) irrespective of genomic context, orientation, and, to a substantial extent, distance as well (Blackwood and Kadonaga, 1998) Enhancers are often cell-type specific, allowing precise spatiotemporal control of gene transcription in different cell types within an organism (Heintzman et al., 2009; Nord et al., 2013) The O-acetyl modified Sia were expressed at low levels of 1-2% of total Sia in these cell 30 lines. We knocked out and over-expressed the sialate O-acetyltransferase gene 31 (CasD1), and knocked out the sialate O-acetylesterase gene (SIAE) using 32 CRISPR/Cas9 editing. Knocking out CasD1 removed 7,9-O-and 9-O-acetyl Sia 33 expression, confirming previous reports. However, over-expression of CasD1 and 34 knockout of SIAE gave only modest increases in 9-O-acetyl levels in cells and no 35 change in 7,9-O-acetyl levels, indicating that there are complex regulations of these 36 modifications. These modifications were essential for influenza C infection, but had no 37 obvious effect on influenza A infection. 38";
                    //string lineTxt = "e. coli, spatial and temporal regulation of gene expression is essential to many biological processes. One class of cis-regulatory elements that plays a major role in transcriptional regulation of gene expression is enhancers Enhancers are classically defined as stretches of non-coding DNA that promote transcription of target gene(s) irrespective of genomic context, orientation, and, to a substantial extent, distance as well (Blackwood and Kadonaga, 1998) Enhancers are often cell-type specific, allowing precise spatiotemporal control of gene transcription in different cell types within an organism (Heintzman et al., 2009; Nord et al., 2013) The O-acetyl modified Sia were expressed at low levels of 1-2% of total Sia in these cell 30 lines. We knocked out and over-expressed the sialate O-acetyltransferase gene 31 (CasD1), and knocked out the sialate O-acetylesterase gene (SIAE) using 32 CRISPR/Cas9 editing. Knocking out CasD1 removed 7,9-O-and 9-O-acetyl Sia 33 expression, confirming previous reports. However, over-expression of CasD1 and 34 knockout of SIAE gave only modest increases in 9-O-acetyl levels in cells and no 35 change in 7,9-O-acetyl levels, indicating that there are complex regulations of these 36 modifications. These modifications were essential for influenza C infection, but had no 37 obvious effect on influenza A infection. 38";
                    //lineTxt = lineTxt.ToLower();

                    string[] splittedText = lineTxt.ToLower().Replace(". ", " ").Split(new char[] { ' ', ')', '(', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);

                    List<(string, int, int)> positions = new List<(string, int, int)>();

                    int posInStr = 0;

                    foreach (var item in splittedText)
                    {
                        int ii = lineTxt.IndexOf(item, posInStr);
                        if (ii == -1)
                        {
                            // ERROR!
                            int yuiyu = 0;
                        }
                        positions.Add((item, ii, ii + item.Length - 1));
                        posInStr = ii + item.Length + 1;

                    }

                    int sl = splittedText.Length;


                    List<(string, int, int)> res = new List<(string, int, int)>();
                    for (int i = 0; i < sl; i++)
                    {

                        if (stopWords.Contains(splittedText[i]))
                        {
                            //res.Add(splittedText[i]);
                            continue;
                        }


                        if (w7.ContainsKey(splittedText[i]) && sl > i + 6)
                        {
                            if (w7[splittedText[i]].ContainsKey(splittedText[i + 1]) && w7[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]].ContainsKey(splittedText[i + 5]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]].ContainsKey(splittedText[i + 6]))
                            {
                                res.Add((w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]][splittedText[i + 6]], positions[i].Item2, positions[i + 6].Item3));
                                i = i + 6;
                                continue;
                            }
                        }
                        if (w6.ContainsKey(splittedText[i]) && sl > i + 5)
                        {
                            if (w6[splittedText[i]].ContainsKey(splittedText[i + 1]) && w6[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]].ContainsKey(splittedText[i + 5]))
                            {
                                res.Add((w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]], positions[i].Item2, positions[i + 5].Item3));
                                i = i + 5;
                                continue;
                            }
                        }
                        if (w5.ContainsKey(splittedText[i]) && sl > i + 4)
                        {
                            if (w5[splittedText[i]].ContainsKey(splittedText[i + 1]) && w5[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]))
                            {
                                res.Add((w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]], positions[i].Item2, positions[i + 4].Item3));
                                i = i + 4;
                                continue;
                            }
                        }
                        if (w4.ContainsKey(splittedText[i]) && sl > i + 3)
                        {
                            if (w4[splittedText[i]].ContainsKey(splittedText[i + 1]) && w4[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w4[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]))
                            {
                                res.Add((w4[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]], positions[i].Item2, positions[i + 3].Item3));
                                i = i + 3;
                                continue;
                            }
                        }
                        if (w3.ContainsKey(splittedText[i]) && sl > i + 2)
                        {
                            if (w3[splittedText[i]].ContainsKey(splittedText[i + 1]) && w3[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]))
                            {
                                res.Add((w3[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]], positions[i].Item2, positions[i + 2].Item3));
                                i = i + 2;
                                continue;
                            }
                        }
                        if (w2.ContainsKey(splittedText[i]) && sl > i + 1)
                        {
                            if (w2[splittedText[i]].ContainsKey(splittedText[i + 1]))
                            {
                                res.Add((w2[splittedText[i]][splittedText[i + 1]], positions[i].Item2, positions[i + 1].Item3));
                                i = i + 1;
                                continue;
                            }
                        }
                        if (w1.ContainsKey(splittedText[i]))
                        {
                            res.Add((w1[splittedText[i]], positions[i].Item2, positions[i].Item3));
                            continue;
                        }

                        //res.Add(splittedText[i]);

                    }

                    // Reconstruct for testing
                    //string lineRes = lineTxt;
                    //for (int i = res.Count - 1; i >= 0; i--)
                    //{
                    //        lineRes = lineRes.ReplaceAt(res[i].Item2, res[i].Item3 - res[i].Item2+1, res[i].Item1);
                    //}

                    foreach (var item in res)
                    {
                        insert_list.Add("(" + id + ",'" + item.Item1 + "'," + item.Item2 + ", " + item.Item3 + ")\r\n");
                        MyInsertOverNItems(ref insert_list, "INSERT INTO covid2.entity(sentenceid,CUI, posbeg, posend)VALUES", 500);
                    }

                }
                MyInsertOverNItems(ref insert_list, "INSERT INTO covid2.entity(sentenceid,CUI, posbeg, posend)VALUES");
            }

            MessageBox.Show("!");
        }


        private void DoOneBlockCOVID(int from, int to)
        {
            List<string> content = new List<string>();
            Dictionary<int, List<Tuple<string, int, int>>> concepts = new Dictionary<int, List<Tuple<string, int, int>>>();



            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader($"SELECT sentenceid, CUI,	PosBeg,	PosEnd FROM   covid2.entity WHERE sentenceid BETWEEN " + from.ToString() + " AND " + to.ToString() + " ORDER BY sentenceid, PosBeg desc"))
            {
                while (dataRdr.Read())
                {
                    int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);
                    string CUI = dataRdr.GetString(1);
                    int PosBeg = dataRdr.GetInt32(2);
                    int PosEnd = dataRdr.GetInt32(3);

                    if (!concepts.ContainsKey(SENTENCE_ID)) concepts.Add(SENTENCE_ID, new List<Tuple<string, int, int>>());
                    concepts[SENTENCE_ID].Add(new Tuple<string, int, int>(CUI, PosBeg, PosEnd));

                }
            }


            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT id, content FROM   covid2.sentences WHERE id BETWEEN " + from.ToString() + " AND " + to.ToString() + ";"))
            {
                while (dataRdr.Read())
                {
                    int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);
                    string lineRes = dataRdr.GetString(1).ToLower();

                    // Replace words with concept
                    if (concepts.ContainsKey(SENTENCE_ID))
                    {
                        int startNext = 999999;
                        foreach (var item in concepts[SENTENCE_ID])
                        {
                            if (startNext >= item.Item3)
                                lineRes = lineRes.ReplaceAt(item.Item2, item.Item3 - item.Item2 + 1, item.Item1);

                            startNext = item.Item2;
                        }
                    }
                    content.Add(lineRes);
                }
            }


            System.IO.File.AppendAllLines(@"D:\PubMed\covid2CUItext.txt", content.ToArray());

        }

        private void button16_Click(object sender, EventArgs e)
        {
            // Restore text with CUI

            int batchsize = (5465310 - 2845815) / 100;

            // 1140309 = SELECT COUNT(*) FROM   covid.sentences
            // SELECT COUNT(*), min(id), max(id) FROM   covid2.sentences # 2618495,2845815, 5465310
            for (int i = 2845815; i < 5465310; i = i + batchsize)
            {
                DoOneBlockCOVID(i, i + batchsize - 1);
                Application.DoEvents();
                label1.Text = i.ToString();
            }

        }

        private void button17_Click(object sender, EventArgs e)
        {

            List<string> CleanedCUIs = new List<string>();
            HashSet<string> AbstractCUIs = new HashSet<string>();
            Dictionary<string, string> CUI2remove = new Dictionary<string, string>();

            //    List<string> l1 = new List<string>();
            //    List<string> l2 = new List<string>();
            //    List<string> l3 = new List<string>();
            //    List<string> l4 = new List<string>();
            //    List<string> l5 = new List<string>();
            //    List<string> ll = new List<string>();

            //    Dictionary<string, long> d = new Dictionary<string, long>();


            //    using (var sr = new StreamReader(@"R:\PubMed\COVID19\unigram_freq.csv"))
            //    {
            //        string line = null;
            //        sr.ReadLine();

            //        while ((line = sr.ReadLine()) != null)
            //        {

            //            long freq = line.After(",").ToInt64();
            //            d.Add(line.Before (",") , freq);
            //        }
            //    }

            //    /*-----------------*/
            //    //1|induced|C0205263|2211797

            //    using (var sr = new StreamReader(@"R:\PubMed\Entity2CUI\Words2CUI.csv"))
            //    {
            //        string line = null;
            //        while ((line = sr.ReadLine()) != null)
            //        {
            //            string[] splittedText = line.Split(new char[] { '|' }, StringSplitOptions.None);

            //            string word = splittedText[1];
            //            CUI.AddIfNotExist(word, line);
            //        }
            //    }
            //    /*-----------------*/

            //    foreach (var item in CUI)
            //    {
            //        if (item.Key.Length == 1) l1.Add(item.Value);
            //        if (item.Key.Length == 2) l2.Add(item.Value);
            //        if (item.Key.Length == 3) l3.Add(item.Value);

            //        if (item.Key.Length == 4 && d.ContainsKey (item.Key)) l4.Add(item.Value);
            //        if (item.Key.Length == 5 && d.ContainsKey(item.Key)) l5.Add(item.Value);
            //        if (item.Key.Length > 5 && d.ContainsKey(item.Key)) ll.Add(item.Value);
            //    }

            //    System.IO.File.AppendAllLines(@"R:\PubMed\Entity2CUI\l1.csv", l1.ToArray());
            //    System.IO.File.AppendAllLines(@"R:\PubMed\Entity2CUI\l2.csv", l2.ToArray());
            //    System.IO.File.AppendAllLines(@"R:\PubMed\Entity2CUI\l3.csv", l3.ToArray());
            //    System.IO.File.AppendAllLines(@"R:\PubMed\Entity2CUI\l4.csv", l4.ToArray());
            //    System.IO.File.AppendAllLines(@"R:\PubMed\Entity2CUI\l5.csv", l5.ToArray());
            //    System.IO.File.AppendAllLines(@"R:\PubMed\Entity2CUI\ll.csv", ll.ToArray());




            using (var sr = new StreamReader(@"R:\PubMed\Entity2CUI\Del_4l.csv"))
            {
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] splittedText = line.Split(new char[] { '|' }, StringSplitOptions.None);

                    string word = splittedText[1];
                    CUI2remove.AddIfNotExist(word, line);
                }
            }


            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(@"SELECT CUI FROM     mrsty WHERE sty      in ('Biomedical Occupation or Discipline',
'Clinical Attribute',
'Conceptual Entity',
'Daily or Recreational Activity',
'Environmental Effect of Humans',
'Family Group',
'Functional Concept',
'Geographic Area',
'Health Care Activity',
'Health Care Related Organization',
'Human',
'Idea or Concept',
'Individual Behavior',
'Intellectual Product',
'Machine Activit',
'Manufactured Object',
'Medical Device',
'Mental Process',
'Natural Phenomenon or Process',
'Occupation or Discipline',
'Occupational Activity',
'Organism Function',
'Organization',
'Physical Object',
'Professional or Occupational Group',
'Professional Society',
'Qualitative Concept',
'Research Activity',
'Social Behavior',
'Spatial Concept',
'Temporal Concept',
'Regulation or Law',
'Educational Activity',
'Event');"))
            {
                while (dataRdr.Read())
                {
                    string C = dataRdr.GetString(0);
                    AbstractCUIs.AddIfNotExist(C);
                }
            }



            using (var sr = new StreamReader(@"R:\PubMed\Entity2CUI\Words2CUI.csv"))
            {
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] splittedText = line.Split(new char[] { '|' }, StringSplitOptions.None);

                    string word = splittedText[1];
                    string CUI = splittedText[2];
                    if (!CUI2remove.ContainsKey(word) && !AbstractCUIs.Contains(CUI)) CleanedCUIs.Add(line);
                }
            }

            System.IO.File.AppendAllLines(@"R:\PubMed\Entity2CUI\CleanWords2CUI.csv", CleanedCUIs.ToArray());

        }

        private void button18_Click(object sender, EventArgs e)
        {
            HashSet<string> codons = new HashSet<string>();
            List<string> CleanedCUIs = new List<string>();

            using (var sr = new StreamReader(@"R:\PubMed\Entity2CUI\Triplets.txt"))
            {
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    codons.AddIfNotExist(line.ToLower());
                }
            }

            using (var sr = new StreamReader(@"R:\PubMed\Entity2CUI\Words2CUI.csv"))
            {
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] splittedText = line.Split(new char[] { '|' }, StringSplitOptions.None);

                    string word = splittedText[1];
                    string CUI = splittedText[2];
                    if (!codons.Contains(word)) CleanedCUIs.Add(line);
                }
            }

            System.IO.File.AppendAllLines(@"R:\PubMed\Entity2CUI\CleanWords2CUI.csv", CleanedCUIs.ToArray());


        }

        private void button19_Click(object sender, EventArgs e)
        {
            Dictionary<int, List<Tuple<string, int, int>>> concepts = new Dictionary<int, List<Tuple<string, int, int>>>();
            List<string> content = new List<string>();

            int batchsize = 1_000_000;

            PrepareConcepts();


            for (int i = 0; i < 332_724_280; i = i + batchsize)
            {
                DoOneBlockNewCUI(concepts, content, i, i + batchsize - 1);
                Application.DoEvents();
                label1.Text = i.ToString();
            }
        }

        private void DoOneBlockNewCUI(Dictionary<int, List<Tuple<string, int, int>>> concepts, List<string> content, int from, int to)
        {
            concepts.Clear();
            content.Clear();
            List<string> insert_list = new List<string>();

            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT SENTENCE_ID,CUI,START_INDEX,END_INDEX FROM entity WHERE sentence_id BETWEEN " + from.ToString() + " AND " + to.ToString() + "  ORDER BY sentence_id, START_INDEX asc ;"))
            {
                while (dataRdr.Read())
                {
                    int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);
                    string CUI = dataRdr.GetString(1);
                    int START_INDEX = (Int32)dataRdr.GetUInt32(2);
                    int END_INDEX = (Int32)dataRdr.GetUInt32(3);

                    if (!concepts.ContainsKey(SENTENCE_ID)) concepts.Add(SENTENCE_ID, new List<Tuple<string, int, int>>());
                    concepts[SENTENCE_ID].Add(new Tuple<string, int, int>(CUI, START_INDEX, END_INDEX));
                }
            }



            MySqlConnection mycn = null;
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT SENTENCE_ID,SENT_START_INDEX,SENTENCE, PMID FROM sentence WHERE sentence_id BETWEEN " + from.ToString() + " AND " + to.ToString() + ";", mycn))
            {
                while (dataRdr.Read())
                {
                    int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);
                    int SENT_START_INDEX = (Int32)dataRdr.GetUInt32(1);
                    string SENTENCE = dataRdr.GetString(2).ToLower();
                    string PMID = dataRdr.GetString(3).ToLower();

                    // Replace words with concept
                    //if (concepts.ContainsKey(SENTENCE_ID))
                    // {
                    //    int startNext = 999999;
                    //   foreach (var item in concepts[SENTENCE_ID])
                    //   {
                    //     if (startNext >= item.Item3)
                    //        SENTENCE = SENTENCE.ReplaceAt(item.Item2 - SENT_START_INDEX, item.Item3 - item.Item2, item.Item1);

                    //     startNext = item.Item2;
                    //  }
                    //}


                    string[] splittedText = SENTENCE.ToLower().TrimEnd('.').Replace(". ", " ").Split(new char[] { ' ', ')', '(', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);

                    List<(string, int, int)> positions = new List<(string, int, int)>();

                    int posInStr = 0;

                    foreach (var item in splittedText)
                    {
                        int ii = SENTENCE.IndexOf(item, posInStr);
                        if (ii == -1)
                        {
                            // ERROR!
                            int yuiyu = 0;
                        }
                        positions.Add((item, ii, ii + item.Length - 1));
                        posInStr = ii + item.Length + 1;

                    }

                    int sl = splittedText.Length;


                    List<(string, int, int)> res = new List<(string, int, int)>();
                    for (int i = 0; i < sl; i++)
                    {

                        if (stopWords.Contains(splittedText[i]))
                        {
                            //res.Add(splittedText[i]);
                            continue;
                        }


                        if (w7.ContainsKey(splittedText[i]) && sl > i + 6)
                        {
                            if (w7[splittedText[i]].ContainsKey(splittedText[i + 1]) && w7[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]].ContainsKey(splittedText[i + 5]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]].ContainsKey(splittedText[i + 6]))
                            {
                                res.Add((w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]][splittedText[i + 6]], positions[i].Item2, positions[i + 6].Item3));
                                i = i + 6;
                                continue;
                            }
                        }
                        if (w6.ContainsKey(splittedText[i]) && sl > i + 5)
                        {
                            if (w6[splittedText[i]].ContainsKey(splittedText[i + 1]) && w6[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]].ContainsKey(splittedText[i + 5]))
                            {
                                res.Add((w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]], positions[i].Item2, positions[i + 5].Item3));
                                i = i + 5;
                                continue;
                            }
                        }
                        if (w5.ContainsKey(splittedText[i]) && sl > i + 4)
                        {
                            if (w5[splittedText[i]].ContainsKey(splittedText[i + 1]) && w5[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]))
                            {
                                res.Add((w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]], positions[i].Item2, positions[i + 4].Item3));
                                i = i + 4;
                                continue;
                            }
                        }
                        if (w4.ContainsKey(splittedText[i]) && sl > i + 3)
                        {
                            if (w4[splittedText[i]].ContainsKey(splittedText[i + 1]) && w4[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w4[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]))
                            {
                                res.Add((w4[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]], positions[i].Item2, positions[i + 3].Item3));
                                i = i + 3;
                                continue;
                            }
                        }
                        if (w3.ContainsKey(splittedText[i]) && sl > i + 2)
                        {
                            if (w3[splittedText[i]].ContainsKey(splittedText[i + 1]) && w3[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]))
                            {
                                res.Add((w3[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]], positions[i].Item2, positions[i + 2].Item3));
                                i = i + 2;
                                continue;
                            }
                        }
                        if (w2.ContainsKey(splittedText[i]) && sl > i + 1)
                        {
                            if (w2[splittedText[i]].ContainsKey(splittedText[i + 1]))
                            {
                                res.Add((w2[splittedText[i]][splittedText[i + 1]], positions[i].Item2, positions[i + 1].Item3));
                                i = i + 1;
                                continue;
                            }
                        }
                        if (w1.ContainsKey(splittedText[i]))
                        {
                            res.Add((w1[splittedText[i]], positions[i].Item2, positions[i].Item3));
                            continue;
                        }

                        //res.Add(splittedText[i]);

                    }

                    // Reconstruct for testing
                    //string lineRes = lineTxt;
                    //for (int i = res.Count - 1; i >= 0; i--)
                    //{
                    //        lineRes = lineRes.ReplaceAt(res[i].Item2, res[i].Item3 - res[i].Item2+1, res[i].Item1);
                    //}

                    foreach (var item in res)
                    {
                        if (!concepts.ContainsKey(SENTENCE_ID)) continue;

                        bool alreadyHave = false;
                        foreach (var originalCUI in concepts[SENTENCE_ID])
                        {
                            // Have to remove CUI already exists in DB (or other overlapping CUIs)
                            if (originalCUI.Item2 <= (item.Item2 + SENT_START_INDEX) && originalCUI.Item3 >= (item.Item3 + 1 + SENT_START_INDEX))
                            {
                                alreadyHave = true;
                                break;
                            }
                        }

                        if (!alreadyHave)
                        {
                            string wrd = SENTENCE.Substring(item.Item2, (item.Item3 + 1 - item.Item2));
                            // Put ~ before PMID to get my own CUIs
                            //insert_list.Add($"({SENTENCE_ID},'~{PMID}','{item.Item1.SQLString()}','{wrd.SQLString ()}',{item.Item2 + SENT_START_INDEX}, {item.Item3 + 1 + SENT_START_INDEX},'{item.Item1.SQLString()}')\r\n");

                            // Put ` before PMID to get my NEW own CUIs
                            insert_list.Add($"({SENTENCE_ID},'`{PMID}','{item.Item1.SQLString()}','{wrd.SQLString()}',{item.Item2 + SENT_START_INDEX}, {item.Item3 + 1 + SENT_START_INDEX},'{item.Item1.SQLString()}')\r\n");

                            MyInsertOverNItems(ref insert_list, "INSERT INTO entity(  SENTENCE_ID ,PMID ,CUI ,TEXT ,START_INDEX ,END_INDEX ,ClusterCUI)VALUES", 500);
                        }
                    }


                }
                MyInsertOverNItems(ref insert_list, "INSERT INTO entity(  SENTENCE_ID ,PMID ,CUI ,TEXT ,START_INDEX ,END_INDEX ,ClusterCUI)VALUES");

            }


        }

        private void button20_Click(object sender, EventArgs e)
        {
            Dictionary<int, string> lines = new Dictionary<int, string>();
            using (var reader = new StreamReader(@"D:\PubMed\JRank.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split('\t');
                    if (values[0] == "Rank") continue;

                    lines.Add(values[0].ToInt32(), values[1]);
                }
            }

            foreach (var aline in lines)
            {
                var avalues = aline.Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                string lst = "";
                foreach (var item in avalues)
                {
                    string aitem = item.PadLeft(8, '0');
                    lst = lst + ",'" + aitem.Substring(0, 4) + "-" + aitem.Substring(4) + "'";
                }
                lst = lst.Trim(new char[] { ',' });

                string sql = $"SELECT ID FROM journals WHERE   ISSNPrint IN ({lst}) or ISSNOnline IN ({lst})";

                int id = MyCommandExecutorInt(sql);
                if (id > 0)
                {
                    MyCommandExecutorNonQuery($"update journals set Rank={aline.Key} where ID={id}");
                }
            }
            MessageBox.Show("!");

        }

        private void button21_Click(object sender, EventArgs e)
        {
            HashSet<string> supplements = new HashSet<string>();
            Dictionary<string, string> supplementsCUI = new Dictionary<string, string>();

            using (var sr = new StreamReader(@"D:\PubMed\supplements.txt"))
            {
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    supplements.AddIfNotExist(line.ToLower());
                }
            }

            using (var sr = new StreamReader(@"D:\PubMed\2019AB\META\MRCONSO.RRF"))
            {
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] splittedText = line.Split(new char[] { '|' }, StringSplitOptions.None);

                    string CUI = splittedText[0];
                    string Lang = splittedText[1];
                    string Text = splittedText[14].ToLower();

                    if (Lang == "ENG" && supplements.Contains(Text) && !supplementsCUI.ContainsKey(Text))
                        supplementsCUI.AddIfNotExist(Text, CUI);
                }
            }

            var t = supplementsCUI.Select(x => x.Value + "|" + x.Key).ToArray();
            File.WriteAllText(@"D:\PubMed\SupplementsCUI.csv", string.Join("\r\n", t));
            //            System.IO.File.AppendAllLines(@"R:\PubMed\Entity2CUI\CleanWords2CUI.csv", CleanedCUIs.ToArray());


        }

        private Dictionary<string, int> ComputePopularity(object o)
        {
            Dictionary<string, int> res = new Dictionary<string, int>();
            MySqlConnection mycn_ = new MySqlConnection();
            mycn_.ConnectionString = "server=" + host + ";userid=root;password=" + mysqlpwd + ";database=pubmed";
            mycn_.Open();

            int pagesize = 332724280 / 10;
            //pagesize = 400;
            int from = o.ToInt32();
            int to = from + pagesize;

            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader($"SELECT CUI  FROM  entity where sentence_id between {from} and {to}", mycn_))
            {
                Console.WriteLine($"SELECT CUI  FROM  entity where sentence_id between {from} and {to}");
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);

                    if (!res.ContainsKey(CUI))
                    {
                        res.Add(CUI, 1);
                    }
                    else
                    {
                        res[CUI] += 1;
                    }
                }
            }

            mycn_.Close();
            return res;
        }

        private void button22_Click(object sender, EventArgs e)
        {
            Dictionary<string, int> CUIs = new Dictionary<string, int>();
            Dictionary<string, int> CUIsRunning = new Dictionary<string, int>();
            Dictionary<string, int> CUIsToAdd = new Dictionary<string, int>();


            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT CUI, Popularity FROM  cuinamepopularity", mycn))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    int Popularity = (Int32)dataRdr.GetUInt32(1);
                    CUIs.Add(CUI, Popularity);
                }
            }


            Task<Dictionary<string, int>>[] tasks = new Task<Dictionary<string, int>>[10];
            int pagesize = 332724280 / 10;
            //pagesize = 400;

            for (int i = 0; i < 10; i++)
            {
                object rr = (i) * pagesize + 1;
                tasks[i] = new Task<Dictionary<string, int>>(() => ComputePopularity(rr));
            }
            for (int i = 0; i < 10; i++)
                tasks[i].Start();

            Task.WaitAll(tasks);

            for (int i = 0; i < 10; i++)
            {
                foreach (var item in tasks[i].Result)
                {
                    if (!CUIsRunning.ContainsKey(item.Key))
                    {
                        CUIsRunning.Add(item.Key, item.Value);
                    }
                    else
                    {
                        CUIsRunning[item.Key] += item.Value;
                    }

                }
            }


            Dictionary<string, int> diffCUIs = new Dictionary<string, int>();
            // Compare current DB with table cuinamepopularity
            foreach (var item in CUIsRunning)
            {
                if (CUIs.ContainsKey(item.Key) && CUIs[item.Key] != item.Value) diffCUIs.Add(item.Key, item.Value);
                if (!CUIs.ContainsKey(item.Key)) CUIsToAdd.Add(item.Key, item.Value);
            }

            var t = diffCUIs.Select(x => x.Key + "|" + x.Value).ToArray();
            File.WriteAllText(@"D:\PubMed\diffCUInamepopularity.csv", string.Join("\r\n", t));

            var tt = CUIsToAdd.Select(x => x.Key + "|" + x.Value).ToArray();
            File.WriteAllText(@"D:\PubMed\diffCUInamepopularityToAdd.csv", string.Join("\r\n", tt));

            //SELECT CUI FROM entity WHERE sentence_id BETWEEN 1219999  AND 1219999 + 100


        }

        private void button23_Click(object sender, EventArgs e)
        {

            List<string> exec_list = new List<string>();

            using (var sr = new StreamReader(@"diffCUInamepopularity.csv"))
            {
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] splittedText = line.Split(new char[] { '|' }, StringSplitOptions.None);

                    string CUI = splittedText[0];
                    string Popularity = splittedText[1];

                    exec_list.Add($"update CUInamepopularity set Popularity={Popularity} where CUI='{CUI}';");

                    MyExecOverNItems(ref exec_list, "\r\n", 500);

                }
            }
            MyExecOverNItems(ref exec_list, "\r\n");

            exec_list.Clear();

            using (var sr = new StreamReader(@"SupplementsCUI.csv"))
            {
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] splittedText = line.Split(new char[] { '|' }, StringSplitOptions.None);

                    string CUI = splittedText[0];

                    exec_list.Add($"update CUInamepopularity set STY='Vitamins and Supplements' where CUI='{CUI}';");

                    MyExecOverNItems(ref exec_list, "\r\n", 500);

                }
            }
            MyExecOverNItems(ref exec_list, "\r\n");

            MessageBox.Show("!");
            // CUINamePopularity.txt
            // diffCUInamepopularity.csv
            // SupplementsCUI.csv
        }

        private void button24_Click(object sender, EventArgs e)
        {
            List<string> list = new List<string>();


            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT SENTENCE_ID, PMID, CUI, TEXT, START_INDEX, END_INDEX, ClusterCUI  FROM entity WHERE pmid LIKE '`%'", mycn))
            {
                while (dataRdr.Read())
                {
                    uint SENTENCE_ID = dataRdr.GetUInt32(0);
                    string PMID = dataRdr.GetString(1).Replace("`", "");
                    string CUI = dataRdr.GetString(2);
                    string TEXT = dataRdr.GetString(3);

                    uint START_INDEX = dataRdr.GetUInt32(4);
                    uint END_INDEX = dataRdr.GetUInt32(5);

                    string ClusterCUI = dataRdr.GetString(6);


                    list.Add($"insert into entity(SENTENCE_ID, PMID, CUI, TEXT, START_INDEX, END_INDEX, ClusterCUI) values({SENTENCE_ID}, '{PMID}', '{CUI}', '{TEXT.SQLString()}', {START_INDEX}, {END_INDEX}, '{ClusterCUI}');");

                }
            }


            File.WriteAllText(@"D:\PubMed\MYCUI_1_8mln.sql", string.Join("\r\n", list));

        }

        private void button25_Click(object sender, EventArgs e)
        {
            //Testilka
            string myCharCollection = "[acute disseminated form of C0023381 (C0023381)].";

            myCharCollection = new string(myCharCollection.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());


            // ZeroRPC  TO RUN IT -- C:\Anaconda3\envs\ttt\python.exe D:\PubMed\WillAInode.js\server\py\server.py
            Client c = new Client();
            c.Connect("tcp://" + host + ":4345");
            //c.Connect("tcp://localhost:4345");

            string CUI = "C0240066";

            string res = "";
            //string res = c.Invoke<string>("allresdc", "{ \"codes\":\"[\\\"c0687152\\\"]\",\"coeffvec\":\"false\",\"action\":\"allresd\",\"system\":\"LEXE\",\"fingerprint\":\"\"}");
            if (searchType == "'Disease or Syndrome'" || searchType == "'Sign or Symptom','Finding'")
                res = c.Invoke<string>("allresdc", "{ \"codes\":\"[\\\"" + CUI.ToLower() + "\\\"]\",\"coeffvec\":\"false\",\"action\":\"allresd\",\"system\":\"LEXE\",\"fingerprint\":\"\"}");
            else
                res = c.Invoke<string>("specificresdc", "{ \"codes\":\"[\\\"" + CUI.ToLower() + "\\\"]\",\"vid\":\"" + searchType.Trim('\'') + "\",\"action\":\"specificresdc\",\"system\":\"LEXE\",\"fingerprint\":\"\"}");

            JObject j_data;
            try
            {
                j_data = JObject.Parse(res);
            }
            catch (Exception)
            {
                return;
            }

        }

        private void dataGridView2_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
        }

        private void dataGridView3_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            dataGridView3.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;
            if (clusterCUIs.Contains(dataGridView3.Rows[e.RowIndex].Cells[0].Value.ToString()))
            {
                dataGridView3.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.GreenYellow;
            }
            e.Handled = false;
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) { button7_Click(null, null); }
        }

        private void dataGridView1_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (clusterMainCUIs.Contains(dataGridView1.Rows[e.RowIndex].Cells[0].Value.ToString()))
            {
                dataGridView1.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.SandyBrown;
            }
            e.Handled = false;
        }

        private void dataGridView2_CurrentCellChanged(object sender, EventArgs e)
        {
        }

        private void dataGridView2_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            dataGridView2.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;
            if (clusterCUIs.Contains(dataGridView2.Rows[e.RowIndex].Cells[0].Value.ToString()))
            {
                dataGridView2.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.GreenYellow;
            }
            //e.Handled = false;
        }

        private void button26_Click(object sender, EventArgs e)
        {
            HashSet<string> CUIs = new HashSet<string>();
            List<string> abstracts_CUI_FT = new List<string>();
            List<string> abstracts_CUI_CD = new List<string>();
            Regex regex = new Regex(@"\b\w+\b");
            int i = 0;

            using (var reader = new StreamReader(@"D:\PubMed\abstracts_CUI_3commas.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    i++;
                    //line = "C0003862/C0231528 xczxc, erwerwe C0231522.";

                    CUIs.Clear();
                    foreach (var item in regex.Matches(line))
                    {
                        string u = item.ToString();
                        if (u.StartsWith("C") && u.Length == 8) CUIs.AddIfNotExist(u);
                    }

                    if (CUIs.Count > 1)
                    {
                        // Iterate all CUIs
                        foreach (string CUI in CUIs)
                        {
                            HashSet<string> CUIs2 = new HashSet<string>(CUIs);
                            CUIs2.Remove(CUI);
                            abstracts_CUI_FT.Add($"__label__{CUI} {string.Join(" ", CUIs2.ToArray())}");
                            if (CUI == "C0014118" && $"__label__{CUI} {string.Join(" ", CUIs2.ToArray())}" == "__label__C0014118 C0000833 C1264606 C0007222")
                            {
                                int tyuty = 0;
                            }
                            //abstracts_CUI_CD.Add($"{CUI},{string.Join(" ", CUIs2.ToArray())}");
                        }

                    }
                    //__label__2 this is also my text


                    // lines.Add(values[0].ToInt32(), values[1]);

                    if (abstracts_CUI_FT.Count() > 10000)
                    {
                        //System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_FT.txt", abstracts_CUI_FT.ToArray());
                        System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_FTv2.txt", abstracts_CUI_FT.ToArray());
                        //System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_CD.txt", abstracts_CUI_CD.ToArray());
                        abstracts_CUI_FT.Clear();
                        abstracts_CUI_CD.Clear();
                        Application.DoEvents();
                        label1.Text = i.ToString();
                        GC.Collect();
                    }
                }
            }


        }

        private void button27_Click(object sender, EventArgs e)
        {
            //HashSet<string> CUIs = new HashSet<string>();
            Dictionary<string, int> CUIs = new Dictionary<string, int>();
            List<string> abstracts_CUI_FT = new List<string>();
            int i = 0;

            #region Mandatory dis
            CUIs.AddIfNotExist("C0000727", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0000729", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0000731", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0000737", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0000833", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0001175", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0001206", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0001327", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0001361", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0002170", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0002395", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0002726", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0002736", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0002871", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0002878", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0002895", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0002962", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0002965", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0003123", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0003504", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0003564", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0003615", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0003811", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0003862", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0003869", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0003873", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0003962", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0004030", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0004096", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0004134", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0004238", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0004245", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0004364", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0004763", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0005681", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0005686", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0005697", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0005779", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0006105", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0006267", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0006318", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0006386", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0006840", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0007222", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0007570", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0007787", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0007789", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0008031", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0008350", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0008677", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0009088", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0009319", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0009443", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0009450", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0009806", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0010054", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0010068", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0010200", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0010201", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0010346", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0010380", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0010481", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0010674", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0010930", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0011053", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0011124", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0011168", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0011615", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0011644", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0011847", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0011884", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0011991", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0012569", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0012833", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0013080", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0013390", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0013404", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0013405", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0013428", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0013456", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0014013", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0014118", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0014175", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0014356", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0014544", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0014553", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0014724", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0014733", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0014743", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0014745", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0015230", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0015300", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0015468", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0015469", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0015644", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0015672", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0015967", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0016204", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0016867", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0017168", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0017574", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0017601", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0017658", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0017672", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0017677", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018021", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018099", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018213", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018418", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018681", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018775", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018777", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018784", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018801", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018802", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018808", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018824", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018834", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018926", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018932", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018965", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018979", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018989", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019018", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019079", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019112", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019151", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019163", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019209", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019322", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019572", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019693", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019825", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020179", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020438", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020453", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020456", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020461", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020505", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020514", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020538", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020550", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020578", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020615", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020619", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020621", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020649", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020672", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0020676", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0021345", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0021364", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0021390", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0021400", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0022104", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0022107", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0022281", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0022336", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0022346", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0022658", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0022661", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0022735", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0022806", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0023212", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0023218", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0023533", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0023882", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0023885", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0023890", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0023895", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024031", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024117", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024141", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024198", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024205", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024421", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024530", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024534", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024535", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024894", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0024902", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0025289", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0026650", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0026769", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0026821", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0026826", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0026827", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0026837", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0026838", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0026961", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0027051", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0027121", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0027145", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0027404", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0027424", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0027497", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0027498", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0027709", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0027873", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0028081", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0028643", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0028738", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0028754", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0028961", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0029118", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0029132", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0029453", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0029456", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0029899", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0030196", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0030246", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0030252", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0030293", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0030554", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0030567", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0030794", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0030920", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0031036", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0031117", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0031256", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0031350", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0032227", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0032231", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0032285", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0032326", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0032460", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0032533", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0032617", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0033103", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0033377", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0033575", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0033687", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0033771", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0033774", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0033775", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0033778", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0033860", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0033893", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0034150", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0034155", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0034186", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0034359", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0034642", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0034735", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0035258", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0035309", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0035439", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0035457", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036202", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036416", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036420", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036421", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036572", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036689", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036690", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036877", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036973", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0037278", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0037384", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0037763", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0037822", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0038002", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0038013", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0038238", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0038450", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0038454", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0038990", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0038999", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0039144", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0039231", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0039239", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0039263", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0039483", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0040264", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0040425", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0040592", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0040822", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0040997", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0041408", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0041667", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0041834", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0041948", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0041976", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042029", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042109", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042256", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042267", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042345", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042384", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042571", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042594", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042769", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042790", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042798", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042963", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0043094", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0043144", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0080270", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0080271", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0080272", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0080273", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0080274", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085166", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085413", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085580", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085593", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085594", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085602", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085605", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085631", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085635", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085636", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085642", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085659", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085677", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085679", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085681", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085786", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0086132", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0086588", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149520", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149521", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149651", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149721", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149725", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149745", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149871", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149882", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149931", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151313", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151479", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151744", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151747", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151786", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151824", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151827", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151905", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151908", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0152031", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0152113", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0152149", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0152165", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0152171", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0152191", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0152227", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0152230", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0154208", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0154723", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0155533", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0155540", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0155626", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0156156", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0156404", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0162119", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0162298", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0162309", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0162316", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0162429", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0162830", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0184567", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0206042", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0206586", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0221082", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0221150", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0221170", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0221232", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0221248", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0221260", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0221270", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0221512", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0227791", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231218", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231230", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231471", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231528", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231530", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231686", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231698", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231835", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231875", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231918", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0232292", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0232367", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0232461", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0232462", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0232487", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0232492", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0232495", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0232711", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0232937", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0233407", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0233565", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0233715", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234132", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234146", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234178", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234233", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234254", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234518", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234632", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234866", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234925", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234987", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0235055", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0235267", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0235522", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0235546", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0235618", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0235896", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0236018", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0237326", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0238813", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0238819", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239064", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239093", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239134", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239161", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239295", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239340", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239431", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239549", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239573", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239574", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239783", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239842", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0240194", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0240311", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0240701", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0241157", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0241165", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0241181", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0241240", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0241254", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0241379", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0241451", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0241633", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0241693", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0242301", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0242383", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0242429", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0242528", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0242770", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0242979", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0262397", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0262630", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0263725", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0264499", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0264551", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0265040", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0265144", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0265191", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0267937", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0268381", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0268712", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0268731", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0268732", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0268733", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0268842", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0269230", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0270774", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0271185", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0271188", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0271431", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0274137", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0275778", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0276651", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0276835", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0277899", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0277942", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0277977", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0278034", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0278147", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0278151", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0279778", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0281856", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0282488", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0311395", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0332601", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0338489", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0339164", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0340288", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0340708", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0340978", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0342122", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0343401", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0344232", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0375548", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0376175", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0392041", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0392681", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0392703", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0393642", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0398349", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0400966", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0401151", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0406326", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0406547", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0423006", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0423153", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0423178", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0423636", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0423791", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0423798", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0424551", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0424810", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0425449", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0425687", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0426579", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0427008", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0427149", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0427306", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0428977", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0439053", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0456909", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0458235", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0458254", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0473237", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0474738", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0476486", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0494475", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0497156", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0497247", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0497364", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0497406", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0520573", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0520679", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0520886", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0520966", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0521170", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0522224", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0524851", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0541875", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0549164", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0560024", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0562491", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0563277", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0566620", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0566984", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0574066", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0574941", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0581126", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0581879", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0600142", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0686735", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0700590", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0702166", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0730283", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0730285", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0740394", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0742038", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0742343", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0742963", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0743973", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0746365", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0746674", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0748540", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0748706", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0750151", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0750901", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0751079", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0751295", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0751494", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0751495", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0752149", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0809999", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0815316", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0848416", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0848717", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0849747", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0849963", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0850060", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0850149", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0858634", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0858734", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0860603", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0863094", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0878544", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0948089", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0948441", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1258104", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1260880", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1261430", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1271100", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1273957", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1274053", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1275684", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1276061", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1282952", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1285577", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1291708", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1295654", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1304119", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1304408", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1306710", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1313969", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1319471", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1320474", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1321756", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1384606", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1397014", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1446377", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1456255", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1535939", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1536066", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1536220", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0001344", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0001675", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0001792", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0005758", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0006444", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0007859", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0008059", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0008100", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0009763", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0009768", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0013144", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0013595", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0015674", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0018621", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019061", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019345", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019360", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0019559", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0022650", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0023222", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0025266", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0026393", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0026946", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0029408", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0030757", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0031157", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036396", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0036508", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0037284", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0037383", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0038218", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0038363", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0038463", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0039614", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0040259", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0041912", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0041952", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0042548", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0043210", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0085624", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0086227", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0087178", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149512", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149514", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149516", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0149875", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151451", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151594", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0151911", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0155825", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0158369", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0162287", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0162557", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0178782", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0205653", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0205847", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0221244", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0221629", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0231911", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0232493", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234230", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0234428", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0235266", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0237849", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0238598", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0238650", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0238656", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0239375", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0240941", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0241126", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0260267", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0264274", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0267026", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0271429", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0272386", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0277463", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0277851", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0337664", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0343024", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0347950", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0423618", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0423641", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0425251", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0438638", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0455610", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0457097", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0457949", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0497365", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0518445", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0549201", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0554021", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0555957", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0576707", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0581330", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0682053", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0740304", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0740456", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0745043", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0745977", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0746724", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0747731", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0848251", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0849852", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0853945", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0857248", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0870221", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0870604", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0917799", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0919833", CUIs.Count + 1);
            CUIs.AddIfNotExist("C0948842", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1261327", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1271070", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1510475", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1527347", CUIs.Count + 1);
            CUIs.AddIfNotExist("C1563135", CUIs.Count + 1);
            #endregion

            using (var reader = new StreamReader(@"D:\PubMed\CUIDiseasesOnly.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string dis = line.BeforeSafe("\t");
                    CUIs.AddIfNotExist(dis, CUIs.Count + 1);
                    i++;
                    if (CUIs.Count >= 1000) break; // Top 1000 Diseases
                }
            }

            var t = CUIs.Select(x => x.Key + "," + x.Value).ToArray();
            //File.WriteAllText(@"D:\PubMed\abstracts_CUI_ComaSep_Dis1000Dict.csv", string.Join("\r\n", t));

            i = 0;
            using (var reader = new StreamReader(@"D:\PubMed\abstracts_CUI_FT.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string lbl = line.BeforeSafe(" ").Replace("__label__", "");

                    if (CUIs.ContainsKey(lbl))
                    {
                        //abstracts_CUI_FT.Add(line.Substring(18) + "," + CUIs[lbl]);
                        abstracts_CUI_FT.Add(line);

                        string Y = line.After(" ");
                        List<string> yy = Y.Split(" ").ToList();
                        if (yy.Count > 1)
                        {
                            yy.Reverse();
                            abstracts_CUI_FT.Add($"__label__{lbl} {string.Join(" ", yy.ToArray())}");
                        }
                    }
                    i++;

                    if (abstracts_CUI_FT.Count() > 10000)
                    {
                        //System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_ComaSep_Dis1000.txt", abstracts_CUI_FT.ToArray());
                        System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_FT_Dis1000.txt", abstracts_CUI_FT.ToArray());
                        abstracts_CUI_FT.Clear();
                        Application.DoEvents();
                        label1.Text = i.ToString();
                        GC.Collect();
                    }
                }
                //System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_ComaSep_Dis1000.txt", abstracts_CUI_FT.ToArray());
                System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_FT_Dis1000.txt", abstracts_CUI_FT.ToArray());
            }
        }

        private void button28_Click(object sender, EventArgs e)
        {
            HashSet<string> CUIs = new HashSet<string>();
            HashSet<string> disallowedCUIs = new HashSet<string>();

            Dictionary<string, List<string>> Lists = new Dictionary<string, List<string>>();

            Lists.Add("finding", new List<string>());
            Lists.Add("diagnostic procedure", new List<string>());
            Lists.Add("therapeutic or preventive procedure", new List<string>());
            Lists.Add("vitamins and supplements", new List<string>());
            Lists.Add("disease or syndrome", new List<string>());
            Lists.Add("sign or symptom", new List<string>());

            //From google xls document
            #region Mandatory dis
            CUIs.AddIfNotExist("C0000727");
            CUIs.AddIfNotExist("C0000729");
            CUIs.AddIfNotExist("C0000731");
            CUIs.AddIfNotExist("C0000737");
            CUIs.AddIfNotExist("C0000833");
            CUIs.AddIfNotExist("C0001175");
            CUIs.AddIfNotExist("C0001206");
            CUIs.AddIfNotExist("C0001327");
            CUIs.AddIfNotExist("C0001361");
            CUIs.AddIfNotExist("C0002170");
            CUIs.AddIfNotExist("C0002395");
            CUIs.AddIfNotExist("C0002726");
            CUIs.AddIfNotExist("C0002736");
            CUIs.AddIfNotExist("C0002871");
            CUIs.AddIfNotExist("C0002878");
            CUIs.AddIfNotExist("C0002895");
            CUIs.AddIfNotExist("C0002962");
            CUIs.AddIfNotExist("C0002965");
            CUIs.AddIfNotExist("C0003123");
            CUIs.AddIfNotExist("C0003504");
            CUIs.AddIfNotExist("C0003564");
            CUIs.AddIfNotExist("C0003615");
            CUIs.AddIfNotExist("C0003811");
            CUIs.AddIfNotExist("C0003862");
            CUIs.AddIfNotExist("C0003869");
            CUIs.AddIfNotExist("C0003873");
            CUIs.AddIfNotExist("C0003962");
            CUIs.AddIfNotExist("C0004030");
            CUIs.AddIfNotExist("C0004096");
            CUIs.AddIfNotExist("C0004134");
            CUIs.AddIfNotExist("C0004238");
            CUIs.AddIfNotExist("C0004245");
            CUIs.AddIfNotExist("C0004364");
            CUIs.AddIfNotExist("C0004763");
            CUIs.AddIfNotExist("C0005681");
            CUIs.AddIfNotExist("C0005686");
            CUIs.AddIfNotExist("C0005697");
            CUIs.AddIfNotExist("C0005779");
            CUIs.AddIfNotExist("C0006105");
            CUIs.AddIfNotExist("C0006267");
            CUIs.AddIfNotExist("C0006318");
            CUIs.AddIfNotExist("C0006386");
            CUIs.AddIfNotExist("C0006840");
            CUIs.AddIfNotExist("C0007222");
            CUIs.AddIfNotExist("C0007570");
            CUIs.AddIfNotExist("C0007787");
            CUIs.AddIfNotExist("C0007789");
            CUIs.AddIfNotExist("C0008031");
            CUIs.AddIfNotExist("C0008350");
            CUIs.AddIfNotExist("C0008677");
            CUIs.AddIfNotExist("C0009088");
            CUIs.AddIfNotExist("C0009319");
            CUIs.AddIfNotExist("C0009443");
            CUIs.AddIfNotExist("C0009450");
            CUIs.AddIfNotExist("C0009806");
            CUIs.AddIfNotExist("C0010054");
            CUIs.AddIfNotExist("C0010068");
            CUIs.AddIfNotExist("C0010200");
            CUIs.AddIfNotExist("C0010201");
            CUIs.AddIfNotExist("C0010346");
            CUIs.AddIfNotExist("C0010380");
            CUIs.AddIfNotExist("C0010481");
            CUIs.AddIfNotExist("C0010674");
            CUIs.AddIfNotExist("C0010930");
            CUIs.AddIfNotExist("C0011053");
            CUIs.AddIfNotExist("C0011124");
            CUIs.AddIfNotExist("C0011168");
            CUIs.AddIfNotExist("C0011615");
            CUIs.AddIfNotExist("C0011644");
            CUIs.AddIfNotExist("C0011847");
            CUIs.AddIfNotExist("C0011884");
            CUIs.AddIfNotExist("C0011991");
            CUIs.AddIfNotExist("C0012569");
            CUIs.AddIfNotExist("C0012833");
            CUIs.AddIfNotExist("C0013080");
            CUIs.AddIfNotExist("C0013390");
            CUIs.AddIfNotExist("C0013404");
            CUIs.AddIfNotExist("C0013405");
            CUIs.AddIfNotExist("C0013428");
            CUIs.AddIfNotExist("C0013456");
            CUIs.AddIfNotExist("C0014013");
            CUIs.AddIfNotExist("C0014118");
            CUIs.AddIfNotExist("C0014175");
            CUIs.AddIfNotExist("C0014356");
            CUIs.AddIfNotExist("C0014544");
            CUIs.AddIfNotExist("C0014553");
            CUIs.AddIfNotExist("C0014724");
            CUIs.AddIfNotExist("C0014733");
            CUIs.AddIfNotExist("C0014743");
            CUIs.AddIfNotExist("C0014745");
            CUIs.AddIfNotExist("C0015230");
            CUIs.AddIfNotExist("C0015300");
            CUIs.AddIfNotExist("C0015468");
            CUIs.AddIfNotExist("C0015469");
            CUIs.AddIfNotExist("C0015644");
            CUIs.AddIfNotExist("C0015672");
            CUIs.AddIfNotExist("C0015967");
            CUIs.AddIfNotExist("C0016204");
            CUIs.AddIfNotExist("C0016867");
            CUIs.AddIfNotExist("C0017168");
            CUIs.AddIfNotExist("C0017574");
            CUIs.AddIfNotExist("C0017601");
            CUIs.AddIfNotExist("C0017658");
            CUIs.AddIfNotExist("C0017672");
            CUIs.AddIfNotExist("C0017677");
            CUIs.AddIfNotExist("C0018021");
            CUIs.AddIfNotExist("C0018099");
            CUIs.AddIfNotExist("C0018213");
            CUIs.AddIfNotExist("C0018418");
            CUIs.AddIfNotExist("C0018681");
            CUIs.AddIfNotExist("C0018775");
            CUIs.AddIfNotExist("C0018777");
            CUIs.AddIfNotExist("C0018784");
            CUIs.AddIfNotExist("C0018801");
            CUIs.AddIfNotExist("C0018802");
            CUIs.AddIfNotExist("C0018808");
            CUIs.AddIfNotExist("C0018824");
            CUIs.AddIfNotExist("C0018834");
            CUIs.AddIfNotExist("C0018926");
            CUIs.AddIfNotExist("C0018932");
            CUIs.AddIfNotExist("C0018965");
            CUIs.AddIfNotExist("C0018979");
            CUIs.AddIfNotExist("C0018989");
            CUIs.AddIfNotExist("C0019018");
            CUIs.AddIfNotExist("C0019079");
            CUIs.AddIfNotExist("C0019112");
            CUIs.AddIfNotExist("C0019151");
            CUIs.AddIfNotExist("C0019163");
            CUIs.AddIfNotExist("C0019209");
            CUIs.AddIfNotExist("C0019322");
            CUIs.AddIfNotExist("C0019572");
            CUIs.AddIfNotExist("C0019693");
            CUIs.AddIfNotExist("C0019825");
            CUIs.AddIfNotExist("C0020179");
            CUIs.AddIfNotExist("C0020438");
            CUIs.AddIfNotExist("C0020453");
            CUIs.AddIfNotExist("C0020456");
            CUIs.AddIfNotExist("C0020461");
            CUIs.AddIfNotExist("C0020505");
            CUIs.AddIfNotExist("C0020514");
            CUIs.AddIfNotExist("C0020538");
            CUIs.AddIfNotExist("C0020550");
            CUIs.AddIfNotExist("C0020578");
            CUIs.AddIfNotExist("C0020615");
            CUIs.AddIfNotExist("C0020619");
            CUIs.AddIfNotExist("C0020621");
            CUIs.AddIfNotExist("C0020649");
            CUIs.AddIfNotExist("C0020672");
            CUIs.AddIfNotExist("C0020676");
            CUIs.AddIfNotExist("C0021345");
            CUIs.AddIfNotExist("C0021364");
            CUIs.AddIfNotExist("C0021390");
            CUIs.AddIfNotExist("C0021400");
            CUIs.AddIfNotExist("C0022104");
            CUIs.AddIfNotExist("C0022107");
            CUIs.AddIfNotExist("C0022281");
            CUIs.AddIfNotExist("C0022336");
            CUIs.AddIfNotExist("C0022346");
            CUIs.AddIfNotExist("C0022658");
            CUIs.AddIfNotExist("C0022661");
            CUIs.AddIfNotExist("C0022735");
            CUIs.AddIfNotExist("C0022806");
            CUIs.AddIfNotExist("C0023212");
            CUIs.AddIfNotExist("C0023218");
            CUIs.AddIfNotExist("C0023533");
            CUIs.AddIfNotExist("C0023882");
            CUIs.AddIfNotExist("C0023885");
            CUIs.AddIfNotExist("C0023890");
            CUIs.AddIfNotExist("C0023895");
            CUIs.AddIfNotExist("C0024031");
            CUIs.AddIfNotExist("C0024117");
            CUIs.AddIfNotExist("C0024141");
            CUIs.AddIfNotExist("C0024198");
            CUIs.AddIfNotExist("C0024205");
            CUIs.AddIfNotExist("C0024421");
            CUIs.AddIfNotExist("C0024530");
            CUIs.AddIfNotExist("C0024534");
            CUIs.AddIfNotExist("C0024535");
            CUIs.AddIfNotExist("C0024894");
            CUIs.AddIfNotExist("C0024902");
            CUIs.AddIfNotExist("C0025289");
            CUIs.AddIfNotExist("C0026650");
            CUIs.AddIfNotExist("C0026769");
            CUIs.AddIfNotExist("C0026821");
            CUIs.AddIfNotExist("C0026826");
            CUIs.AddIfNotExist("C0026827");
            CUIs.AddIfNotExist("C0026837");
            CUIs.AddIfNotExist("C0026838");
            CUIs.AddIfNotExist("C0026961");
            CUIs.AddIfNotExist("C0027051");
            CUIs.AddIfNotExist("C0027121");
            CUIs.AddIfNotExist("C0027145");
            CUIs.AddIfNotExist("C0027404");
            CUIs.AddIfNotExist("C0027424");
            CUIs.AddIfNotExist("C0027497");
            CUIs.AddIfNotExist("C0027498");
            CUIs.AddIfNotExist("C0027709");
            CUIs.AddIfNotExist("C0027873");
            CUIs.AddIfNotExist("C0028081");
            CUIs.AddIfNotExist("C0028643");
            CUIs.AddIfNotExist("C0028738");
            CUIs.AddIfNotExist("C0028754");
            CUIs.AddIfNotExist("C0028961");
            CUIs.AddIfNotExist("C0029118");
            CUIs.AddIfNotExist("C0029132");
            CUIs.AddIfNotExist("C0029453");
            CUIs.AddIfNotExist("C0029456");
            CUIs.AddIfNotExist("C0029899");
            CUIs.AddIfNotExist("C0030196");
            CUIs.AddIfNotExist("C0030246");
            CUIs.AddIfNotExist("C0030252");
            CUIs.AddIfNotExist("C0030293");
            CUIs.AddIfNotExist("C0030554");
            CUIs.AddIfNotExist("C0030567");
            CUIs.AddIfNotExist("C0030794");
            CUIs.AddIfNotExist("C0030920");
            CUIs.AddIfNotExist("C0031036");
            CUIs.AddIfNotExist("C0031117");
            CUIs.AddIfNotExist("C0031256");
            CUIs.AddIfNotExist("C0031350");
            CUIs.AddIfNotExist("C0032227");
            CUIs.AddIfNotExist("C0032231");
            CUIs.AddIfNotExist("C0032285");
            CUIs.AddIfNotExist("C0032326");
            CUIs.AddIfNotExist("C0032460");
            CUIs.AddIfNotExist("C0032533");
            CUIs.AddIfNotExist("C0032617");
            CUIs.AddIfNotExist("C0033103");
            CUIs.AddIfNotExist("C0033377");
            CUIs.AddIfNotExist("C0033575");
            CUIs.AddIfNotExist("C0033687");
            CUIs.AddIfNotExist("C0033771");
            CUIs.AddIfNotExist("C0033774");
            CUIs.AddIfNotExist("C0033775");
            CUIs.AddIfNotExist("C0033778");
            CUIs.AddIfNotExist("C0033860");
            CUIs.AddIfNotExist("C0033893");
            CUIs.AddIfNotExist("C0034150");
            CUIs.AddIfNotExist("C0034155");
            CUIs.AddIfNotExist("C0034186");
            CUIs.AddIfNotExist("C0034359");
            CUIs.AddIfNotExist("C0034642");
            CUIs.AddIfNotExist("C0034735");
            CUIs.AddIfNotExist("C0035258");
            CUIs.AddIfNotExist("C0035309");
            CUIs.AddIfNotExist("C0035439");
            CUIs.AddIfNotExist("C0035457");
            CUIs.AddIfNotExist("C0036202");
            CUIs.AddIfNotExist("C0036416");
            CUIs.AddIfNotExist("C0036420");
            CUIs.AddIfNotExist("C0036421");
            CUIs.AddIfNotExist("C0036572");
            CUIs.AddIfNotExist("C0036689");
            CUIs.AddIfNotExist("C0036690");
            CUIs.AddIfNotExist("C0036877");
            CUIs.AddIfNotExist("C0036973");
            CUIs.AddIfNotExist("C0037278");
            CUIs.AddIfNotExist("C0037384");
            CUIs.AddIfNotExist("C0037763");
            CUIs.AddIfNotExist("C0037822");
            CUIs.AddIfNotExist("C0038002");
            CUIs.AddIfNotExist("C0038013");
            CUIs.AddIfNotExist("C0038238");
            CUIs.AddIfNotExist("C0038450");
            CUIs.AddIfNotExist("C0038454");
            CUIs.AddIfNotExist("C0038990");
            CUIs.AddIfNotExist("C0038999");
            CUIs.AddIfNotExist("C0039144");
            CUIs.AddIfNotExist("C0039231");
            CUIs.AddIfNotExist("C0039239");
            CUIs.AddIfNotExist("C0039263");
            CUIs.AddIfNotExist("C0039483");
            CUIs.AddIfNotExist("C0040264");
            CUIs.AddIfNotExist("C0040425");
            CUIs.AddIfNotExist("C0040592");
            CUIs.AddIfNotExist("C0040822");
            CUIs.AddIfNotExist("C0040997");
            CUIs.AddIfNotExist("C0041408");
            CUIs.AddIfNotExist("C0041667");
            CUIs.AddIfNotExist("C0041834");
            CUIs.AddIfNotExist("C0041948");
            CUIs.AddIfNotExist("C0041976");
            CUIs.AddIfNotExist("C0042029");
            CUIs.AddIfNotExist("C0042109");
            CUIs.AddIfNotExist("C0042256");
            CUIs.AddIfNotExist("C0042267");
            CUIs.AddIfNotExist("C0042345");
            CUIs.AddIfNotExist("C0042384");
            CUIs.AddIfNotExist("C0042571");
            CUIs.AddIfNotExist("C0042594");
            CUIs.AddIfNotExist("C0042769");
            CUIs.AddIfNotExist("C0042790");
            CUIs.AddIfNotExist("C0042798");
            CUIs.AddIfNotExist("C0042963");
            CUIs.AddIfNotExist("C0043094");
            CUIs.AddIfNotExist("C0043144");
            CUIs.AddIfNotExist("C0080270");
            CUIs.AddIfNotExist("C0080271");
            CUIs.AddIfNotExist("C0080272");
            CUIs.AddIfNotExist("C0080273");
            CUIs.AddIfNotExist("C0080274");
            CUIs.AddIfNotExist("C0085166");
            CUIs.AddIfNotExist("C0085413");
            CUIs.AddIfNotExist("C0085580");
            CUIs.AddIfNotExist("C0085593");
            CUIs.AddIfNotExist("C0085594");
            CUIs.AddIfNotExist("C0085602");
            CUIs.AddIfNotExist("C0085605");
            CUIs.AddIfNotExist("C0085631");
            CUIs.AddIfNotExist("C0085635");
            CUIs.AddIfNotExist("C0085636");
            CUIs.AddIfNotExist("C0085642");
            CUIs.AddIfNotExist("C0085659");
            CUIs.AddIfNotExist("C0085677");
            CUIs.AddIfNotExist("C0085679");
            CUIs.AddIfNotExist("C0085681");
            CUIs.AddIfNotExist("C0085786");
            CUIs.AddIfNotExist("C0086132");
            CUIs.AddIfNotExist("C0086588");
            CUIs.AddIfNotExist("C0149520");
            CUIs.AddIfNotExist("C0149521");
            CUIs.AddIfNotExist("C0149651");
            CUIs.AddIfNotExist("C0149721");
            CUIs.AddIfNotExist("C0149725");
            CUIs.AddIfNotExist("C0149745");
            CUIs.AddIfNotExist("C0149871");
            CUIs.AddIfNotExist("C0149882");
            CUIs.AddIfNotExist("C0149931");
            CUIs.AddIfNotExist("C0151313");
            CUIs.AddIfNotExist("C0151479");
            CUIs.AddIfNotExist("C0151744");
            CUIs.AddIfNotExist("C0151747");
            CUIs.AddIfNotExist("C0151786");
            CUIs.AddIfNotExist("C0151824");
            CUIs.AddIfNotExist("C0151827");
            CUIs.AddIfNotExist("C0151905");
            CUIs.AddIfNotExist("C0151908");
            CUIs.AddIfNotExist("C0152031");
            CUIs.AddIfNotExist("C0152113");
            CUIs.AddIfNotExist("C0152149");
            CUIs.AddIfNotExist("C0152165");
            CUIs.AddIfNotExist("C0152171");
            CUIs.AddIfNotExist("C0152191");
            CUIs.AddIfNotExist("C0152227");
            CUIs.AddIfNotExist("C0152230");
            CUIs.AddIfNotExist("C0154208");
            CUIs.AddIfNotExist("C0154723");
            CUIs.AddIfNotExist("C0155533");
            CUIs.AddIfNotExist("C0155540");
            CUIs.AddIfNotExist("C0155626");
            CUIs.AddIfNotExist("C0156156");
            CUIs.AddIfNotExist("C0156404");
            CUIs.AddIfNotExist("C0162119");
            CUIs.AddIfNotExist("C0162298");
            CUIs.AddIfNotExist("C0162309");
            CUIs.AddIfNotExist("C0162316");
            CUIs.AddIfNotExist("C0162429");
            CUIs.AddIfNotExist("C0162830");
            CUIs.AddIfNotExist("C0184567");
            CUIs.AddIfNotExist("C0206042");
            CUIs.AddIfNotExist("C0206586");
            CUIs.AddIfNotExist("C0221082");
            CUIs.AddIfNotExist("C0221150");
            CUIs.AddIfNotExist("C0221170");
            CUIs.AddIfNotExist("C0221232");
            CUIs.AddIfNotExist("C0221248");
            CUIs.AddIfNotExist("C0221260");
            CUIs.AddIfNotExist("C0221270");
            CUIs.AddIfNotExist("C0221512");
            CUIs.AddIfNotExist("C0227791");
            CUIs.AddIfNotExist("C0231218");
            CUIs.AddIfNotExist("C0231230");
            CUIs.AddIfNotExist("C0231471");
            CUIs.AddIfNotExist("C0231528");
            CUIs.AddIfNotExist("C0231530");
            CUIs.AddIfNotExist("C0231686");
            CUIs.AddIfNotExist("C0231698");
            CUIs.AddIfNotExist("C0231835");
            CUIs.AddIfNotExist("C0231875");
            CUIs.AddIfNotExist("C0231918");
            CUIs.AddIfNotExist("C0232292");
            CUIs.AddIfNotExist("C0232367");
            CUIs.AddIfNotExist("C0232461");
            CUIs.AddIfNotExist("C0232462");
            CUIs.AddIfNotExist("C0232487");
            CUIs.AddIfNotExist("C0232492");
            CUIs.AddIfNotExist("C0232495");
            CUIs.AddIfNotExist("C0232711");
            CUIs.AddIfNotExist("C0232937");
            CUIs.AddIfNotExist("C0233407");
            CUIs.AddIfNotExist("C0233565");
            CUIs.AddIfNotExist("C0233715");
            CUIs.AddIfNotExist("C0234132");
            CUIs.AddIfNotExist("C0234146");
            CUIs.AddIfNotExist("C0234178");
            CUIs.AddIfNotExist("C0234233");
            CUIs.AddIfNotExist("C0234254");
            CUIs.AddIfNotExist("C0234518");
            CUIs.AddIfNotExist("C0234632");
            CUIs.AddIfNotExist("C0234866");
            CUIs.AddIfNotExist("C0234925");
            CUIs.AddIfNotExist("C0234987");
            CUIs.AddIfNotExist("C0235055");
            CUIs.AddIfNotExist("C0235267");
            CUIs.AddIfNotExist("C0235522");
            CUIs.AddIfNotExist("C0235546");
            CUIs.AddIfNotExist("C0235618");
            CUIs.AddIfNotExist("C0235896");
            CUIs.AddIfNotExist("C0236018");
            CUIs.AddIfNotExist("C0237326");
            CUIs.AddIfNotExist("C0238813");
            CUIs.AddIfNotExist("C0238819");
            CUIs.AddIfNotExist("C0239064");
            CUIs.AddIfNotExist("C0239093");
            CUIs.AddIfNotExist("C0239134");
            CUIs.AddIfNotExist("C0239161");
            CUIs.AddIfNotExist("C0239295");
            CUIs.AddIfNotExist("C0239340");
            CUIs.AddIfNotExist("C0239431");
            CUIs.AddIfNotExist("C0239549");
            CUIs.AddIfNotExist("C0239573");
            CUIs.AddIfNotExist("C0239574");
            CUIs.AddIfNotExist("C0239783");
            CUIs.AddIfNotExist("C0239842");
            CUIs.AddIfNotExist("C0240194");
            CUIs.AddIfNotExist("C0240311");
            CUIs.AddIfNotExist("C0240701");
            CUIs.AddIfNotExist("C0241157");
            CUIs.AddIfNotExist("C0241165");
            CUIs.AddIfNotExist("C0241181");
            CUIs.AddIfNotExist("C0241240");
            CUIs.AddIfNotExist("C0241254");
            CUIs.AddIfNotExist("C0241379");
            CUIs.AddIfNotExist("C0241451");
            CUIs.AddIfNotExist("C0241633");
            CUIs.AddIfNotExist("C0241693");
            CUIs.AddIfNotExist("C0242301");
            CUIs.AddIfNotExist("C0242383");
            CUIs.AddIfNotExist("C0242429");
            CUIs.AddIfNotExist("C0242528");
            CUIs.AddIfNotExist("C0242770");
            CUIs.AddIfNotExist("C0242979");
            CUIs.AddIfNotExist("C0262397");
            CUIs.AddIfNotExist("C0262630");
            CUIs.AddIfNotExist("C0263725");
            CUIs.AddIfNotExist("C0264499");
            CUIs.AddIfNotExist("C0264551");
            CUIs.AddIfNotExist("C0265040");
            CUIs.AddIfNotExist("C0265144");
            CUIs.AddIfNotExist("C0265191");
            CUIs.AddIfNotExist("C0267937");
            CUIs.AddIfNotExist("C0268381");
            CUIs.AddIfNotExist("C0268712");
            CUIs.AddIfNotExist("C0268731");
            CUIs.AddIfNotExist("C0268732");
            CUIs.AddIfNotExist("C0268733");
            CUIs.AddIfNotExist("C0268842");
            CUIs.AddIfNotExist("C0269230");
            CUIs.AddIfNotExist("C0270774");
            CUIs.AddIfNotExist("C0271185");
            CUIs.AddIfNotExist("C0271188");
            CUIs.AddIfNotExist("C0271431");
            CUIs.AddIfNotExist("C0274137");
            CUIs.AddIfNotExist("C0275778");
            CUIs.AddIfNotExist("C0276651");
            CUIs.AddIfNotExist("C0276835");
            CUIs.AddIfNotExist("C0277899");
            CUIs.AddIfNotExist("C0277942");
            CUIs.AddIfNotExist("C0277977");
            CUIs.AddIfNotExist("C0278034");
            CUIs.AddIfNotExist("C0278147");
            CUIs.AddIfNotExist("C0278151");
            CUIs.AddIfNotExist("C0279778");
            CUIs.AddIfNotExist("C0281856");
            CUIs.AddIfNotExist("C0282488");
            CUIs.AddIfNotExist("C0311395");
            CUIs.AddIfNotExist("C0332601");
            CUIs.AddIfNotExist("C0338489");
            CUIs.AddIfNotExist("C0339164");
            CUIs.AddIfNotExist("C0340288");
            CUIs.AddIfNotExist("C0340708");
            CUIs.AddIfNotExist("C0340978");
            CUIs.AddIfNotExist("C0342122");
            CUIs.AddIfNotExist("C0343401");
            CUIs.AddIfNotExist("C0344232");
            CUIs.AddIfNotExist("C0375548");
            CUIs.AddIfNotExist("C0376175");
            CUIs.AddIfNotExist("C0392041");
            CUIs.AddIfNotExist("C0392681");
            CUIs.AddIfNotExist("C0392703");
            CUIs.AddIfNotExist("C0393642");
            CUIs.AddIfNotExist("C0398349");
            CUIs.AddIfNotExist("C0400966");
            CUIs.AddIfNotExist("C0401151");
            CUIs.AddIfNotExist("C0406326");
            CUIs.AddIfNotExist("C0406547");
            CUIs.AddIfNotExist("C0423006");
            CUIs.AddIfNotExist("C0423153");
            CUIs.AddIfNotExist("C0423178");
            CUIs.AddIfNotExist("C0423636");
            CUIs.AddIfNotExist("C0423791");
            CUIs.AddIfNotExist("C0423798");
            CUIs.AddIfNotExist("C0424551");
            CUIs.AddIfNotExist("C0424810");
            CUIs.AddIfNotExist("C0425449");
            CUIs.AddIfNotExist("C0425687");
            CUIs.AddIfNotExist("C0426579");
            CUIs.AddIfNotExist("C0427008");
            CUIs.AddIfNotExist("C0427149");
            CUIs.AddIfNotExist("C0427306");
            CUIs.AddIfNotExist("C0428977");
            CUIs.AddIfNotExist("C0439053");
            CUIs.AddIfNotExist("C0456909");
            CUIs.AddIfNotExist("C0458235");
            CUIs.AddIfNotExist("C0458254");
            CUIs.AddIfNotExist("C0473237");
            CUIs.AddIfNotExist("C0474738");
            CUIs.AddIfNotExist("C0476486");
            CUIs.AddIfNotExist("C0494475");
            CUIs.AddIfNotExist("C0497156");
            CUIs.AddIfNotExist("C0497247");
            CUIs.AddIfNotExist("C0497364");
            CUIs.AddIfNotExist("C0497406");
            CUIs.AddIfNotExist("C0520573");
            CUIs.AddIfNotExist("C0520679");
            CUIs.AddIfNotExist("C0520886");
            CUIs.AddIfNotExist("C0520966");
            CUIs.AddIfNotExist("C0521170");
            CUIs.AddIfNotExist("C0522224");
            CUIs.AddIfNotExist("C0524851");
            CUIs.AddIfNotExist("C0541875");
            CUIs.AddIfNotExist("C0549164");
            CUIs.AddIfNotExist("C0560024");
            CUIs.AddIfNotExist("C0562491");
            CUIs.AddIfNotExist("C0563277");
            CUIs.AddIfNotExist("C0566620");
            CUIs.AddIfNotExist("C0566984");
            CUIs.AddIfNotExist("C0574066");
            CUIs.AddIfNotExist("C0574941");
            CUIs.AddIfNotExist("C0581126");
            CUIs.AddIfNotExist("C0581879");
            CUIs.AddIfNotExist("C0600142");
            CUIs.AddIfNotExist("C0686735");
            CUIs.AddIfNotExist("C0700590");
            CUIs.AddIfNotExist("C0702166");
            CUIs.AddIfNotExist("C0730283");
            CUIs.AddIfNotExist("C0730285");
            CUIs.AddIfNotExist("C0740394");
            CUIs.AddIfNotExist("C0742038");
            CUIs.AddIfNotExist("C0742343");
            CUIs.AddIfNotExist("C0742963");
            CUIs.AddIfNotExist("C0743973");
            CUIs.AddIfNotExist("C0746365");
            CUIs.AddIfNotExist("C0746674");
            CUIs.AddIfNotExist("C0748540");
            CUIs.AddIfNotExist("C0748706");
            CUIs.AddIfNotExist("C0750151");
            CUIs.AddIfNotExist("C0750901");
            CUIs.AddIfNotExist("C0751079");
            CUIs.AddIfNotExist("C0751295");
            CUIs.AddIfNotExist("C0751494");
            CUIs.AddIfNotExist("C0751495");
            CUIs.AddIfNotExist("C0752149");
            CUIs.AddIfNotExist("C0809999");
            CUIs.AddIfNotExist("C0815316");
            CUIs.AddIfNotExist("C0848416");
            CUIs.AddIfNotExist("C0848717");
            CUIs.AddIfNotExist("C0849747");
            CUIs.AddIfNotExist("C0849963");
            CUIs.AddIfNotExist("C0850060");
            CUIs.AddIfNotExist("C0850149");
            CUIs.AddIfNotExist("C0858634");
            CUIs.AddIfNotExist("C0858734");
            CUIs.AddIfNotExist("C0860603");
            CUIs.AddIfNotExist("C0863094");
            CUIs.AddIfNotExist("C0878544");
            CUIs.AddIfNotExist("C0948089");
            CUIs.AddIfNotExist("C0948441");
            CUIs.AddIfNotExist("C1258104");
            CUIs.AddIfNotExist("C1260880");
            CUIs.AddIfNotExist("C1261430");
            CUIs.AddIfNotExist("C1271100");
            CUIs.AddIfNotExist("C1273957");
            CUIs.AddIfNotExist("C1274053");
            CUIs.AddIfNotExist("C1275684");
            CUIs.AddIfNotExist("C1276061");
            CUIs.AddIfNotExist("C1282952");
            CUIs.AddIfNotExist("C1285577");
            CUIs.AddIfNotExist("C1291708");
            CUIs.AddIfNotExist("C1295654");
            CUIs.AddIfNotExist("C1304119");
            CUIs.AddIfNotExist("C1304408");
            CUIs.AddIfNotExist("C1306710");
            CUIs.AddIfNotExist("C1313969");
            CUIs.AddIfNotExist("C1319471");
            CUIs.AddIfNotExist("C1320474");
            CUIs.AddIfNotExist("C1321756");
            CUIs.AddIfNotExist("C1384606");
            CUIs.AddIfNotExist("C1397014");
            CUIs.AddIfNotExist("C1446377");
            CUIs.AddIfNotExist("C1456255");
            CUIs.AddIfNotExist("C1535939");
            CUIs.AddIfNotExist("C1536066");
            CUIs.AddIfNotExist("C1536220");
            CUIs.AddIfNotExist("C0001344");
            CUIs.AddIfNotExist("C0001675");
            CUIs.AddIfNotExist("C0001792");
            CUIs.AddIfNotExist("C0005758");
            CUIs.AddIfNotExist("C0006444");
            CUIs.AddIfNotExist("C0007859");
            CUIs.AddIfNotExist("C0008059");
            CUIs.AddIfNotExist("C0008100");
            CUIs.AddIfNotExist("C0009763");
            CUIs.AddIfNotExist("C0009768");
            CUIs.AddIfNotExist("C0013144");
            CUIs.AddIfNotExist("C0013595");
            CUIs.AddIfNotExist("C0015674");
            CUIs.AddIfNotExist("C0018621");
            CUIs.AddIfNotExist("C0019061");
            CUIs.AddIfNotExist("C0019345");
            CUIs.AddIfNotExist("C0019360");
            CUIs.AddIfNotExist("C0019559");
            CUIs.AddIfNotExist("C0022650");
            CUIs.AddIfNotExist("C0023222");
            CUIs.AddIfNotExist("C0025266");
            CUIs.AddIfNotExist("C0026393");
            CUIs.AddIfNotExist("C0026946");
            CUIs.AddIfNotExist("C0029408");
            CUIs.AddIfNotExist("C0030757");
            CUIs.AddIfNotExist("C0031157");
            CUIs.AddIfNotExist("C0036396");
            CUIs.AddIfNotExist("C0036508");
            CUIs.AddIfNotExist("C0037284");
            CUIs.AddIfNotExist("C0037383");
            CUIs.AddIfNotExist("C0038218");
            CUIs.AddIfNotExist("C0038363");
            CUIs.AddIfNotExist("C0038463");
            CUIs.AddIfNotExist("C0039614");
            CUIs.AddIfNotExist("C0040259");
            CUIs.AddIfNotExist("C0041912");
            CUIs.AddIfNotExist("C0041952");
            CUIs.AddIfNotExist("C0042548");
            CUIs.AddIfNotExist("C0043210");
            CUIs.AddIfNotExist("C0085624");
            CUIs.AddIfNotExist("C0086227");
            CUIs.AddIfNotExist("C0087178");
            CUIs.AddIfNotExist("C0149512");
            CUIs.AddIfNotExist("C0149514");
            CUIs.AddIfNotExist("C0149516");
            CUIs.AddIfNotExist("C0149875");
            CUIs.AddIfNotExist("C0151451");
            CUIs.AddIfNotExist("C0151594");
            CUIs.AddIfNotExist("C0151911");
            CUIs.AddIfNotExist("C0155825");
            CUIs.AddIfNotExist("C0158369");
            CUIs.AddIfNotExist("C0162287");
            CUIs.AddIfNotExist("C0162557");
            CUIs.AddIfNotExist("C0178782");
            CUIs.AddIfNotExist("C0205653");
            CUIs.AddIfNotExist("C0205847");
            CUIs.AddIfNotExist("C0221244");
            CUIs.AddIfNotExist("C0221629");
            CUIs.AddIfNotExist("C0231911");
            CUIs.AddIfNotExist("C0232493");
            CUIs.AddIfNotExist("C0234230");
            CUIs.AddIfNotExist("C0234428");
            CUIs.AddIfNotExist("C0235266");
            CUIs.AddIfNotExist("C0237849");
            CUIs.AddIfNotExist("C0238598");
            CUIs.AddIfNotExist("C0238650");
            CUIs.AddIfNotExist("C0238656");
            CUIs.AddIfNotExist("C0239375");
            CUIs.AddIfNotExist("C0240941");
            CUIs.AddIfNotExist("C0241126");
            CUIs.AddIfNotExist("C0260267");
            CUIs.AddIfNotExist("C0264274");
            CUIs.AddIfNotExist("C0267026");
            CUIs.AddIfNotExist("C0271429");
            CUIs.AddIfNotExist("C0272386");
            CUIs.AddIfNotExist("C0277463");
            CUIs.AddIfNotExist("C0277851");
            CUIs.AddIfNotExist("C0337664");
            CUIs.AddIfNotExist("C0343024");
            CUIs.AddIfNotExist("C0347950");
            CUIs.AddIfNotExist("C0423618");
            CUIs.AddIfNotExist("C0423641");
            CUIs.AddIfNotExist("C0425251");
            CUIs.AddIfNotExist("C0438638");
            CUIs.AddIfNotExist("C0455610");
            CUIs.AddIfNotExist("C0457097");
            CUIs.AddIfNotExist("C0457949");
            CUIs.AddIfNotExist("C0497365");
            CUIs.AddIfNotExist("C0518445");
            CUIs.AddIfNotExist("C0549201");
            CUIs.AddIfNotExist("C0554021");
            CUIs.AddIfNotExist("C0555957");
            CUIs.AddIfNotExist("C0576707");
            CUIs.AddIfNotExist("C0581330");
            CUIs.AddIfNotExist("C0682053");
            CUIs.AddIfNotExist("C0740304");
            CUIs.AddIfNotExist("C0740456");
            CUIs.AddIfNotExist("C0745043");
            CUIs.AddIfNotExist("C0745977");
            CUIs.AddIfNotExist("C0746724");
            CUIs.AddIfNotExist("C0747731");
            CUIs.AddIfNotExist("C0848251");
            CUIs.AddIfNotExist("C0849852");
            CUIs.AddIfNotExist("C0853945");
            CUIs.AddIfNotExist("C0857248");
            CUIs.AddIfNotExist("C0870221");
            CUIs.AddIfNotExist("C0870604");
            CUIs.AddIfNotExist("C0917799");
            CUIs.AddIfNotExist("C0919833");
            CUIs.AddIfNotExist("C0948842");
            CUIs.AddIfNotExist("C1261327");
            CUIs.AddIfNotExist("C1271070");
            CUIs.AddIfNotExist("C1510475");
            CUIs.AddIfNotExist("C1527347");
            CUIs.AddIfNotExist("C1563135");
            CUIs.AddIfNotExist("");
            #endregion


            // Add age and sex
            CUIs.AddIfNotExist("C0021289");// Neonate 0 - 1 month
            CUIs.AddIfNotExist("C0021270");// Infant 1 month - 3 year
            CUIs.AddIfNotExist("C0682053");// Toddler 1 - 3 year
            CUIs.AddIfNotExist("C0008100");// Preschool Children 3 - 5
            CUIs.AddIfNotExist("C0260267");// School children 6 - 16
            CUIs.AddIfNotExist("C0008059");// Children 2 - 12
            CUIs.AddIfNotExist("C0205653");// Adolescent 12 - 18
            CUIs.AddIfNotExist("C0087178");// Youth 18 - 21
            CUIs.AddIfNotExist("C0238598");// Young adult 21 - 30
            CUIs.AddIfNotExist("C0001675");// Adult 21 - 59
            CUIs.AddIfNotExist("C0205847");// Middle Ages 45 - 59
            CUIs.AddIfNotExist("C0001792");// Older adults
            CUIs.AddIfNotExist("C0079377");// Frail Elders > 60
            CUIs.AddIfNotExist("C0001792");// Aged 65 and Over
            CUIs.AddIfNotExist("C0028829");// Octogenarian > 80
            CUIs.AddIfNotExist("C0001795");// aged over 80
            CUIs.AddIfNotExist("C0028296");// Nonagenarians > 90
            CUIs.AddIfNotExist("C0007667");// Centenarian > 100
            CUIs.AddIfNotExist("C0870604");// Girl
            CUIs.AddIfNotExist("C0043210");// Woman
            CUIs.AddIfNotExist("C0870221");// Boys
            CUIs.AddIfNotExist("C0025266");// Men
            CUIs.AddIfNotExist("C0032961");// pregnancy


            // Remove general concepts.
            disallowedCUIs.Add("C0039082"); // Syndrome
            disallowedCUIs.Add("C0012634"); // Disease
            disallowedCUIs.Add("C0221198"); // Lesion
            disallowedCUIs.Add("C0205082"); // Severe
            disallowedCUIs.Add("C1457887"); // Symptoms
            disallowedCUIs.Add("C0205160"); // Negative
            disallowedCUIs.Add("C0221444"); // clinical syndromes

            MySqlConnection mylclcn = null;

            string sSQL = "SELECT CUI,Name, sty,popularity FROM cuinamepopularity  where (sty IN       ('Finding','Diagnostic PROCEDURE','Therapeutic or Preventive PROCEDURE','Vitamins and Supplements','Disease or Syndrome','Sign or Symptom') and popularity>=500 and 1=1 ORDER BY popularity desc; ";
            sSQL = sSQL.Replace("1=1", $" not CUI in ('{ string.Join("','", disallowedCUIs.ToArray()) }')) or CUI in ('{string.Join("','", CUIs.ToArray())}') ");

            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1);
                    string sty = dataRdr.GetString(2).ToLower();
                    int pop = dataRdr.GetInt32(3);

                    if (Lists.ContainsKey(sty)) Lists[sty].Add($"{CUI}\t{Name}\t{pop}");

                    // Do not take more than 1000 diseases 
                    if (sty == "disease or syndrome" && Lists["disease or syndrome"].Count > 1000) continue;

                    CUIs.AddIfNotExist(CUI);

                }
            }

            foreach (var item in Lists)
            {
                System.IO.File.WriteAllLines($@"D:\PubMed\top_{item.Key.Replace(' ', '_')}.txt", item.Value.ToArray());
            }

            List<string> res = new List<string>();
            using (var reader = new StreamReader(@"D:\PubMed\only_CUI_noclu.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] C = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                    C = C.Where(x => CUIs.Contains(x)).ToArray();
                    if (C.Length > 1) res.Add(string.Join(" ", C));
                }
            }
            System.IO.File.WriteAllLines(@"D:\PubMed\only_CUI_noclu_top.txt", res.ToArray());

        }

        private void button29_Click(object sender, EventArgs e)
        {
            MySqlConnection mylclcn = null;
            List<string> res = new List<string>();
            //List<string> resSQL = new List<string>();
            HashSet<string> uniquePhrases = new HashSet<string>();
            //HashSet<string> sym_findings_CUIs = new HashSet<string>();

            ReadAllTopCSVs();

            //string sSQL = "SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND ms.STY IN       ('Finding','Sign or Symptom') AND lat='ENG' and popularity>=500";
            //string sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND ms.STY IN       ('Finding','Sign or Symptom')  AND lat='ENG' AND  p.CUI in ('{string.Join("','", sym_findings_CUIs.ToArray())}')";

            var allCUIs_withClusters = allCUIs.Concat(clusters.Keys).Distinct().ToList();
            string sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND lat='ENG' AND  p.CUI in ('{string.Join("','", allCUIs_withClusters.ToArray())}')";

            //string sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND lat='ENG' AND  p.CUI in ('C0002736')";


            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1).ToLower();


                    // Clusterise to MainCUI
                    if (clusters.ContainsKey(CUI)) CUI = clusters[CUI];

                    //Normalize
                    Name = Name.Replace("[d]", "");
                    Name = Name.Replace("(disorder)", "");
                    Name = Name.Replace("(finding)", "");
                    Name = Name.Replace("(situation)", "");
                    Name = Name.Replace("(diagnosis)", "");
                    Name = Name.Replace("(context-dependent category)", "");
                    Name = Name.Replace("(symptom)", "");
                    Name = Name.Replace("(physical finding)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    
                   //  Name = new string(Name.Select(ch => !char.IsPunctuation(ch) ? ch : ' ').ToArray());
                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(CUI + Name))
                    {
                        uniquePhrases.Add(CUI + Name);
                        res.Add($"__label__{CUI} {Name}");
                        //resSQL.Add($"'{CUI}','{Name.SQLString ()}'");

                        if (Name.ContainsWholeWord("hurt"))
                        {
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("hurts"))
                        {
                            Name = Name.ReplaceWholeWord("hurts", "aches");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("pain"))
                        {
                            Name = Name.ReplaceWholeWord("pain", "hurt");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                    }
                }
            }


            using (var reader = new StreamReader(@"D:\PubMed\top_manual_descriptions.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");

                    string CUI = lbl[0];

                    // Clusterise to MainCUI
                    if (clusters.ContainsKey(CUI)) CUI = clusters[CUI];

                    string Name = lbl[1].ToLower();
                    //Normalize
                    Name = Name.Replace("[d]", "");
                    Name = Name.Replace("(disorder)", "");
                    Name = Name.Replace("(finding)", "");
                    Name = Name.Replace("(situation)", "");
                    Name = Name.Replace("(diagnosis)", "");
                    Name = Name.Replace("(context-dependent category)", "");
                    Name = Name.Replace("(symptom)", "");
                    Name = Name.Replace("(physical finding)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(Name))
                    {
                        uniquePhrases.Add(Name);
                        res.Add($"__label__{CUI} {Name}");
                        //resSQL.Add($"'{CUI}','{Name.SQLString()}'");

                        if (Name.ContainsWholeWord("hurt"))
                        {
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("hurts"))
                        {
                            Name = Name.ReplaceWholeWord("hurts", "aches");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("pain"))
                        {
                            Name = Name.ReplaceWholeWord("pain", "hurt");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                    }
                }

            }

            BackOldFile(@"D:\PubMed\ft_symptoms_recognition.txt");

            System.IO.File.WriteAllLines(@"D:\PubMed\ft_symptoms_recognition.txt", res.ToArray());


            BackOldFile(@"D:\PubMed\allcuis.sql");
            System.IO.File.WriteAllLines(@"D:\PubMed\allcuis.sql", new string[] { "delete from allcuis where 1 = 1;" });
            System.IO.File.AppendAllLines(@"D:\PubMed\allcuis.sql", allCUIs_withClusters.Select ( x => $"insert into allcuis(CUI)values('{x}');") .ToArray());

        }

        private void button30_Click(object sender, EventArgs e)
        {
            MySqlConnection mylclcn = null;
            List<string> res = new List<string>();
            HashSet<string> uniquePhrases = new HashSet<string>();

            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT sentence FROM sentence", mylclcn))
            {
                while (dataRdr.Read())
                {
                    string txt = dataRdr.GetString(0).ToLower();

                    //Normalize
                    txt = new string(txt.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    txt = txt.Replace(" and ", " ");
                    txt = txt.Replace("  ", " ");
                    txt = txt.Replace("  ", " ");
                    txt = txt.Replace("  ", " ").Trim();

                    res.Add(txt);
                    if (res.Count > 10000)
                    {
                        System.IO.File.AppendAllLines(@"D:\PubMed\clean_abstracts.txt", res.ToArray());
                        res.Clear();
                    }
                }
            }

            System.IO.File.AppendAllLines(@"D:\PubMed\clean_abstracts.txt", res.ToArray());


        }

        private void button31_Click(object sender, EventArgs e)
        {
            HashSet<string> allCUIs = new HashSet<string>();

            HashSet<string> sym_findings_CUIs = new HashSet<string>();
            HashSet<string> dis_CUIs = new HashSet<string>();
            HashSet<string> additional_CUIs = new HashSet<string>();

            /*
             * 
             * clusters.csv
             * 
             * scr only_CUI_noclu_top.txt
             * result only_CUI_noclu_top.txt
             * 
             * top_disease_or_syndrome.txt
top_finding.txt
top_sign_or_symptom.txt
top_additional.txt
             * */
            #region Read CSVs

            // Read clusters
            clusters.Clear();
            using (var reader = new StreamReader(@"D:\PubMed\clusters.csv"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");

                    if (!clusters.ContainsKey(lbl[0]))
                    {
                        clusters.Add(lbl[0], lbl[1]);
                    }
                }
            }

            using (var reader = new StreamReader(@"D:\PubMed\top_disease_or_syndrome.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");

                    dis_CUIs.AddIfNotExist(lbl[0]);
                    allCUIs.AddIfNotExist(lbl[0]);
                }
            }
            using (var reader = new StreamReader(@"D:\PubMed\top_finding.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");


                    sym_findings_CUIs.AddIfNotExist(lbl[0]);
                    allCUIs.AddIfNotExist(lbl[0]);
                }
            }
            using (var reader = new StreamReader(@"D:\PubMed\top_sign_or_symptom.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");

                    sym_findings_CUIs.AddIfNotExist(lbl[0]);
                    allCUIs.AddIfNotExist(lbl[0]);
                }
            }
            using (var reader = new StreamReader(@"D:\PubMed\top_additional.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");

                    additional_CUIs.AddIfNotExist(lbl[0]);
                    allCUIs.AddIfNotExist(lbl[0]);

                }
            }
            #endregion


            /*
             * Check CUI existance
            HashSet<string> d = new HashSet<string>();
            using (var reader = new StreamReader(@"D:\PubMed\d.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine().Trim();

                    if (!sym_findings_CUIs.Contains (line) && !dis_CUIs.Contains(line)) d.AddIfNotExist(line);

                }
            }

            string l = String.Join("','", d.ToArray());
            return;
            */

            // Create text for embedding
            /*
            List<string> res = new List<string>();
            using (var reader = new StreamReader(@"D:\PubMed\only_CUI_noclu.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] C = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                    C = C.Where(x => allCUIs.Contains(x)).ToArray();
                    if (C.Length > 1)
                    {
                        //Try to clusterise findings and symptoms
                        if (C.Any(x => sym_findings_CUIs.Contains(x) && clusters.ContainsKey(x)))
                        {

                            for (int i = 0; i < C.Length; i++)
                            {
                                // Replace Clusterizable sym/findings with Master sym/finding
                                if (sym_findings_CUIs.Contains(C[i]) && clusters.ContainsKey(C[i])) C[i] = clusters[C[i]];
                            }

                        }

                        res.Add(string.Join(" ", C));
                    }
                }
            }
            System.IO.File.WriteAllLines(@"D:\PubMed\only_CUI_noclu_top.txt", res.ToArray());
            */


            List<string> abstracts_CUI_ft_dis_top = new List<string>();
            // Create training set
            //System.IO.File.Delete(@"D:\PubMed\abstracts_CUI_ft_dis_top.txt");
            System.IO.File.Delete(@"D:\PubMed\abstracts_CUI_ft_dis_topv2.txt");
            using (var reader = new StreamReader(@"D:\PubMed\abstracts_CUI_FT.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string lbl = line.BeforeSafe(" ").Replace("__label__", "");

                    if (dis_CUIs.Contains(lbl))
                    {
                        string Y = line.After(" ");
                        string[] C = Y.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                        C = C.Where(x => allCUIs.Contains(x)).ToArray();
                        if (C.Length > 1)
                        {
                            //Try to clusterise findings and symptoms
                            if (C.Any(x => sym_findings_CUIs.Contains(x) && clusters.ContainsKey(x)))
                            {

                                for (int i = 0; i < C.Length; i++)
                                {
                                    // Replace Clusterizable sym/findings with Master sym/finding
                                    if (sym_findings_CUIs.Contains(C[i]) && clusters.ContainsKey(C[i])) C[i] = clusters[C[i]];
                                }

                            }

                            abstracts_CUI_ft_dis_top.Add($"__label__{lbl} {string.Join(" ", C)}");
                            //if ($"__label__{lbl} {string.Join(" ", C)}" == "__label__C0014118 C0000833 C0007222")
                            //{
                            //    int yu = 0;
                            //}

                        }


                        if (abstracts_CUI_ft_dis_top.Count() > 10000)
                        {
                            //System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_ft_dis_top.txt", abstracts_CUI_ft_dis_top.ToArray());
                            System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_ft_dis_topv2.txt", abstracts_CUI_ft_dis_top.ToArray());
                            abstracts_CUI_ft_dis_top.Clear();
                            Application.DoEvents();
                            GC.Collect();
                        }
                    }
                }
                //System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_ft_dis_top.txt", abstracts_CUI_ft_dis_top.ToArray());
                System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_CUI_ft_dis_topv2.txt", abstracts_CUI_ft_dis_top.ToArray());
            }
        }

        private void DoOnesmBlock(int page)
        {
            int from = page * batchsize;
            int to = (page + 1) * batchsize;

            Console.WriteLine($"Page {page} From {from} to {to}");

            Dictionary<int, List<Tuple<string, int, int, string, string>>> concepts = new Dictionary<int, List<Tuple<string, int, int, string, string>>>();
            List<string> content = new List<string>();
            List<string> onlyCUI = new List<string>();

            MySqlConnection mylclcn = null;
            try
            {
                using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT SENTENCE_ID,       CUI             ,START_INDEX,END_INDEX, PMID FROM entity WHERE sentence_id BETWEEN " + from.ToString() + " AND " + to.ToString() + "  ORDER BY sentence_id;", mylclcn))
                {
                    while (dataRdr.Read())
                    {

                        int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);
                        string CUI = dataRdr.GetString(1);
                        int START_INDEX = (Int32)dataRdr.GetUInt32(2);
                        int END_INDEX = (Int32)dataRdr.GetUInt32(3);
                        string PMID = dataRdr.GetString(4);

                        // Use code clustering - not DB clustering
                        // WITHOUT CLUSTERS comment line below
                        string cluCUI = CUI;
                        if (clusters.ContainsKey(CUI)) cluCUI = clusters[CUI]; // Replace with MainCUI

                        if (!allCUIs.Contains(CUI) && !allCUIs.Contains(cluCUI)) continue;

                        if (!concepts.ContainsKey(SENTENCE_ID)) concepts.Add(SENTENCE_ID, new List<Tuple<string, int, int, string, string>>());
                        concepts[SENTENCE_ID].Add(new Tuple<string, int, int, string, string>(CUI, START_INDEX, END_INDEX, cluCUI, PMID));

                    }
                }
                List<string> insert_list = new List<string>();


                // Remove toxicity and 7 commas
                MySqlConnection mylclcn2 = null;
                using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT SENTENCE_ID,       SENTENCE   FROM SENTENCE where sentence_id in (" + string.Join(",", concepts.Keys.ToArray()) + ");", mylclcn2))
                {
                    while (dataRdr.Read())
                    {

                        int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);
                        string SENTENCE = dataRdr.GetString(1).ToLower();


                        //if (SENTENCE.Contains("frequent events") || SENTENCE.Contains("adverse") || SENTENCE.Contains("toxicity") || SENTENCE.Contains("induced") || SENTENCE.Contains("side effects") || countCommas(SENTENCE) >= 7)
                        if (SENTENCE.Contains("event") || SENTENCE.Contains("adverse") || SENTENCE.Contains("common") || SENTENCE.Contains("toxicity") || SENTENCE.Contains("induced") || SENTENCE.Contains("reaction") || SENTENCE.Contains("effect") || SENTENCE.Contains("side") || countCommas(SENTENCE) >= 7)
                        {
                            concepts.Remove(SENTENCE_ID);
                        }
                    }
                }

                foreach (var item in concepts)
                {
                    if (item.Value.Count > 1)
                    {
                        foreach (var itementity in item.Value)
                        {
                            insert_list.Add("(" + item.Key + ",'" + itementity.Item5 + "','" + itementity.Item1 + "'," + itementity.Item2 + "," + itementity.Item3 + ",'" + itementity.Item4 + "')");
                            MyInsertOverNItems(mylclcn, ref insert_list, " insert into pubmed.smentity(  SENTENCE_ID, PMID,  CUI ,  START_INDEX,  END_INDEX,  ClusterCUI)values", 10000);
                        }
                    }
                }
                MyInsertOverNItems(mylclcn, ref insert_list, " insert into pubmed.smentity(  SENTENCE_ID, PMID,  CUI ,  START_INDEX,  END_INDEX,  ClusterCUI)values");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Err1 Page {page}");
            }
            finally
            {
                if (mylclcn != null && mycn.State == ConnectionState.Open) mylclcn.Close();
            }


        }

        private void button32_Click(object sender, EventArgs e)
        {
            /*
* 
* clusters.csv
* 
* scr only_CUI_noclu_top.txt
* result only_CUI_noclu_top.txt
* 
* top_disease_or_syndrome.txt
top_finding.txt
top_sign_or_symptom.txt
top_additional.txt
* */
            ReadAllTopCSVs();

            batchsize = 100_000;
            maxSentence_id = 332_724_280;

            //batchsize = 50000;
            //maxSentence_id = 6010000;
            int pages = (Int32)Math.Ceiling((double)maxSentence_id / (double)batchsize);

            Task task = Task.Factory.StartNew(delegate
            {
                try
                {
                    Parallel.For(0, pages + 1, new ParallelOptions { MaxDegreeOfParallelism = 11 }, i =>
                    {
                        DoOnesmBlock(i);
                    });

                }
                catch (AggregateException ae)
                {
                    var ignoredExceptions = new List<Exception>();
                    // This is where you can choose which exceptions to handle.
                    foreach (var ex in ae.Flatten().InnerExceptions)
                    {
                        if (ex is ArgumentException)
                            Console.WriteLine(ex.Message);
                        else
                            ignoredExceptions.Add(ex);
                    }
                    if (ignoredExceptions.Count > 0) throw new AggregateException(ignoredExceptions);
                }

            });

            // MessageBox.Show("Ready!");
        }

        private void ReadAllTopCSVs()
        {
            #region Read CSVs

            // Read clusters
            clusters.Clear();
            using (var reader = new StreamReader(@"D:\PubMed\clusters.csv"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");

                    if (!clusters.ContainsKey(lbl[0]))
                    {
                        clusters.Add(lbl[0], lbl[1]);
                    }
                }
            }

            using (var reader = new StreamReader(@"D:\PubMed\top_disease_or_syndrome.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string lbl = line.Substring(0, 8);
                    //string[] lbl = line.Split("\t");

                    dis_CUIs.AddIfNotExist(lbl);
                    allCUIs.AddIfNotExist(lbl);
                }
            }
            using (var reader = new StreamReader(@"D:\PubMed\top_finding.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    //string[] lbl = line.Split("\t");
                    string lbl = line.Substring(0, 8);

                    sym_findings_CUIs.AddIfNotExist(lbl);
                    allCUIs.AddIfNotExist(lbl);
                }
            }
            using (var reader = new StreamReader(@"D:\PubMed\top_sign_or_symptom.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    //string[] lbl = line.Split("\t");
                    string lbl = line.Substring(0, 8);

                    sym_findings_CUIs.AddIfNotExist(lbl);
                    allCUIs.AddIfNotExist(lbl);
                }
            }
            using (var reader = new StreamReader(@"D:\PubMed\top_additional.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    //string[] lbl = line.Split("\t");
                    string lbl = line.Substring(0, 8);

                    additional_CUIs.AddIfNotExist(lbl);
                    allCUIs.AddIfNotExist(lbl);

                }
            }
            #endregion

            Dictionary<string, string> CUINamePopularity = new Dictionary<string, string>();
            using (var reader = new StreamReader(@"D:\PubMed\CUINamePopularity.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("|");

                    string CUI = lbl[0].Trim('"');
                    string sty = lbl[2].Trim('"');

                    CUINamePopularity.AddIfNotExist(CUI, sty);

                }
            }


            string List8 = @"D:\PubMed\_List8_and_Validation.txt"; //
            var lines = File.ReadAllLines(List8);

            foreach (string line in lines)
            {
                string CUI = line.Substring(0, 8);
                if (CUI.Length == 8 && CUI.StartsWith("C"))
                    dis_CUIs_ShortList.AddIfNotExist(CUI);
            }

            int i = 0;

            // Add any CUI that exists in (Болезни_коды симптомов) and add to corresponding lists
            //using (var reader = new StreamReader(@"D:\PubMed\top_validation.txt"))
            //{
            //    while (!reader.EndOfStream)
            //    {
            //        var line = reader.ReadLine();

            //        MatchCollection matches = Regex.Matches(line, @"\b[\w']*\b");

            //        var words = from m in matches.Cast<Match>()
            //                    where !string.IsNullOrEmpty(m.Value)
            //                    select m.Value;

            //        foreach (var wrd in words.ToArray())
            //        {
            //            if (wrd.Substring(0, 1) == "C" && wrd.Length == 8)
            //            {
            //                if (CUINamePopularity.ContainsKey(wrd))
            //                {
            //                    if (!dis_CUIs.Contains (wrd) && CUINamePopularity[wrd] == "Disease or Syndrome")
            //                        dis_CUIs.AddIfNotExist(wrd);
            //                    if (!sym_findings_CUIs.Contains(wrd) && (CUINamePopularity[wrd] == "Sign or Symptom" || CUINamePopularity[wrd] == "Finding"))
            //                        sym_findings_CUIs.AddIfNotExist(wrd);
            //                }

            //                validationCUIs.AddIfNotExist(wrd);
            //                allCUIs.AddIfNotExist(wrd);
            //            }
            //        }

            //    }
            //}

        }

        private void WriteOneBatch(Dictionary<int, List<string>> concepts, Dictionary<string, int> f1)
        {
            List<string> txt = new List<string>();
            List<string> ft = new List<string>();

            //List<string> txtwoenum = new List<string>();
            //List<string> ftwoenum = new List<string>();

            string MMDD = DateTime.Now.ToString("MMdd");


            // Remove toxicity and 7 commas
            MySqlConnection mylclcn3 = null;
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT SENTENCE_ID,       SENTENCE   FROM SENTENCE where sentence_id in (" + string.Join(",", concepts.Keys.ToArray()) + ");", mylclcn3))
            {
                while (dataRdr.Read())
                {

                    int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);
                    string SENTENCE = dataRdr.GetString(1).ToLower();


                    //if (SENTENCE.Contains("frequent events") || SENTENCE.Contains("adverse") || SENTENCE.Contains("toxicity") || SENTENCE.Contains("induced") || SENTENCE.Contains("side effects") || countCommas(SENTENCE) >= 7)
                    if (SENTENCE.Contains("event") || SENTENCE.Contains("adverse") || SENTENCE.Contains("common") || SENTENCE.Contains("toxicity") || SENTENCE.Contains("induced") || SENTENCE.Contains("reaction") || SENTENCE.Contains("effect") || SENTENCE.Contains("side") || countCommas(SENTENCE) >= 7)
                    {
                        concepts.Remove(SENTENCE_ID);
                    }
                }
            }

            foreach (var sentence in concepts)
            {
                if (sentence.Value.Count < 2) continue;

                txt.Add(string.Join(" ", sentence.Value.ToArray()));
                //if (sentence.Value.Count <4) txtwoenum.Add(string.Join(" ", sentence.Value.ToArray()));

                // FT  - find dis and make __label__ string
                foreach (var CUI in sentence.Value)
                {
                    if (dis_CUIs_ShortList.Contains(CUI) || (clusters.ContainsKey(CUI) && dis_CUIs_ShortList.Contains(clusters[CUI])))
                    {
                        HashSet<string> CUIs2 = new HashSet<string>(sentence.Value);
                        CUIs2.Remove(CUI);
                        if (CUIs2.Count != 0)
                        {
                            ///if (f1.ContainsKey (CUI))
                            ft.Add($"__label__{CUI} {string.Join(" ", CUIs2.ToArray())}");

                            //if (sentence.Value.Count < 4) ftwoenum.Add($"__label__{CUI} {string.Join(" ", CUIs2.ToArray())}");
                        }


                    }
                }
            }

            System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_scluCUI_FT_" + MMDD + ".txt", ft.ToArray());
            //System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_scluCUI_EM_" + MMDD + ".txt", txt.ToArray());

            //System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_nocluCUI_FT_" + MMDD + "_woenum.txt", ftwoenum.ToArray());
            //System.IO.File.AppendAllLines(@"D:\PubMed\abstracts_nocluCUI_EM_" + MMDD + "_woenum.txt", txtwoenum.ToArray());
        }

        private void button33_Click(object sender, EventArgs e)
        {
            string MMDD = DateTime.Now.ToString("MMdd");
            int clusterizeDis = 0;
            int clusterizeSym = 1;

            System.IO.File.Delete(@"D:\PubMed\abstracts_scluCUI_FT_" + MMDD + ".txt");
            //System.IO.File.Delete(@"D:\PubMed\abstracts_scluCUI_EM_" + MMDD + ".txt");


            // string file1 = @"D:\PubMed\abstracts_CUI_ft_dis_top.txt";

            Dictionary<string, int> f1 = new Dictionary<string, int>();

            //using (var reader = new StreamReader(file1))
            //{
            //    while (!reader.EndOfStream)
            //    {
            //        var line = reader.ReadLine();
            //        string l = line.BeforeSafe(" ").Replace("__label__", "");
            //        if (f1.ContainsKey(l)) f1[l]++;
            //        else f1.Add(l, 0);
            //    }
            //}

            //System.IO.File.Delete(@"D:\PubMed\abstracts_nocluCUI_FT_" + MMDD + "_woenum.txt");
            //System.IO.File.Delete(@"D:\PubMed\abstracts_nocluCUI_EM_" + MMDD + "_woenum.txt");
            ReadAllTopCSVs();
            Dictionary<int, List<string>> concepts = new Dictionary<int, List<string>>();

            HashSet<int> ttt = new HashSet<int>();
            int cnt = 0;


            //string sent2removefile = @"D:\PubMed\sents2remove.txt";
            //var logFile = File.ReadAllLines(sent2removefile);
            //var logList = new List<string>(logFile);
            //HashSet<string> sent2remove = new HashSet<string>(logList);

            MySqlConnection mylclcn = null;
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader("SELECT SENTENCE_ID,   CUI   FROM smentity  ORDER BY sentence_id;", mylclcn))
            {
                while (dataRdr.Read())
                {

                    int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);
                    string CUI = dataRdr.GetString(1);

                    // !!!!!!!!!!!!!!! Todo temporary measure !!!!!!!!!!!!!!!
                    ////////if (sent2remove.Contains(SENTENCE_ID.ToString())) continue; // Убрать предложения где искомые toxicities etc.   встречаются в соседнем предложении minus 1.7 mln sentences

                    // Use code clustering - not DB clustering
                    if (clusterizeDis == 1 && clusters.ContainsKey(CUI) && (dis_CUIs.Contains(clusters[CUI]) || dis_CUIs.Contains(CUI))) CUI = clusters[CUI];// Replace with MainCUI
                    if (clusterizeSym == 1 && clusters.ContainsKey(CUI) && (sym_findings_CUIs.Contains(clusters[CUI]) || sym_findings_CUIs.Contains(CUI))) CUI = clusters[CUI];// Replace with MainCUI


                    if (!concepts.ContainsKey(SENTENCE_ID)) concepts.Add(SENTENCE_ID, new List<string>());
                    concepts[SENTENCE_ID].Add(CUI);

                    /*
                    if (CUI == "C0014118")
                    {
                        cnt++;
                        ttt.AddIfNotExist(SENTENCE_ID);
                    }
                    */

                    if (concepts.Count >= 10000)
                    {
                        WriteOneBatch(concepts, f1);
                        concepts.Clear();
                    }
                }
                WriteOneBatch(concepts, f1);
            }

            MessageBox.Show("Ready!");
        }

        private void button34_Click(object sender, EventArgs e)
        {
            List<string> toremove = new List<string>();
            ReadAllTopCSVs();
            string sSQL = "SELECT DISTINCT CUI FROM  pubmed.smentity   UNION   SELECT DISTINCT ClusterCUI FROM pubmed.smentity";
            MySqlConnection mylclcn = null;
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {

                    string CUI = dataRdr.GetString(0);

                    if (!clusters.ContainsKey(CUI) && !allCUIs.Contains(CUI)) toremove.Add(CUI);

                }

            }
            // first time - where CUI in 
            // second time - where ClusterCUI in 
            string SQLRemove = "delete from pubmed.smentity where CUI in ('" + string.Join("','", toremove.ToArray()) + "')";
        }

        private void button35_Click(object sender, EventArgs e)
        {
            string file2check = @"D:\PubMed\dssymvalidation.lst";

            HashSet<string> all = new HashSet<string>();
            HashSet<string> absent = new HashSet<string>();

            string sSQL = "SELECT DISTINCT CUI FROM  pubmed.smentity   UNION   SELECT DISTINCT ClusterCUI FROM pubmed.smentity";
            MySqlConnection mylclcn = null;
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {

                    string CUI = dataRdr.GetString(0);

                    all.AddIfNotExist(CUI);

                }

            }

            using (var reader = new StreamReader(file2check))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (!all.Contains(line)) absent.AddIfNotExist(line);
                }
            }

            string y = "select * from cuinamepopularity where cui in ('" + string.Join("','", all.ToArray()) + "')";
        }

        private void button36_Click(object sender, EventArgs e)
        {
            string file2 = @"D:\PubMed\abstracts_cluCUI_FT_0728.txt";
            //string file2 = @"D:\PubMed\abstracts_clushortCUI_FT_0728.txt";
            string file1 = @"D:\PubMed\abstracts_CUI_ft_dis_top.txt";

            Dictionary<string, int> f1 = new Dictionary<string, int>();
            Dictionary<string, int> f2 = new Dictionary<string, int>();
            Dictionary<string, int> diff = new Dictionary<string, int>();

            using (var reader = new StreamReader(file1))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string l = line.BeforeSafe(" ").Replace("__label__", "");
                    if (f1.ContainsKey(l)) f1[l]++;
                    else f1.Add(l, 0);
                }
            }
            using (var reader = new StreamReader(file2))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string l = line.BeforeSafe(" ").Replace("__label__", "");
                    if (f2.ContainsKey(l)) f2[l]++;
                    else f2.Add(l, 0);
                }
            }
            var sorted1 = f1.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
            var sorted2 = f2.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

            foreach (var item in f2.Keys)
            {
                if (!f1.ContainsKey(item)) diff.Add(item, f2[item]);
            }

            string y = "select * from cuinamepopularity where cui in ('" + string.Join("','", diff.Keys.ToArray()) + "')";
            int ytuy = 0;
        }

        private void button37_Click(object sender, EventArgs e)
        {
            MySqlConnection mylclcn = null;
            List<string> res = new List<string>();
            HashSet<string> uniquePhrases = new HashSet<string>();
            //HashSet<string> sym_findings_CUIs = new HashSet<string>();

            ReadAllTopCSVs();


            var allCUIs_withClusters = allCUIs.Concat(clusters.Keys).Distinct().ToList();
            string sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND lat='ENG' AND  p.CUI in ('{string.Join("','", allCUIs_withClusters.ToArray())}')";

            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1).ToLower();

                    // Clusterise to MainCUI Except for disease
                    if (
                        clusters.ContainsKey(CUI) &&
                        !((dis_CUIs.Contains(clusters[CUI]) || dis_CUIs.Contains(CUI)))
                        ) CUI = clusters[CUI];

                    //Normalize
                    Name = Name.Replace("[d]", "");
                    Name = Name.Replace("(disorder)", "");
                    Name = Name.Replace("(finding)", "");
                    Name = Name.Replace("(situation)", "");
                    Name = Name.Replace("(diagnosis)", "");
                    Name = Name.Replace("(context-dependent category)", "");
                    Name = Name.Replace("(symptom)", "");
                    Name = Name.Replace("(physical finding)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(Name))
                    {
                        uniquePhrases.Add(Name);
                        res.Add($"{countSpaces(Name) + 1}|{Name}|{CUI}|99");
                    }
                }
            }


            using (var reader = new StreamReader(@"D:\PubMed\top_manual_descriptions.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");

                    string CUI = lbl[0];

                    // Clusterise to MainCUI Except for disease
                    if (
                        clusters.ContainsKey(CUI) &&
                        !((dis_CUIs.Contains(clusters[CUI]) || dis_CUIs.Contains(CUI)))
                        ) CUI = clusters[CUI];


                    string Name = lbl[1].ToLower();
                    //Normalize
                    Name = Name.Replace("[d]", "");
                    Name = Name.Replace("(disorder)", "");
                    Name = Name.Replace("(finding)", "");
                    Name = Name.Replace("(situation)", "");
                    Name = Name.Replace("(diagnosis)", "");
                    Name = Name.Replace("(context-dependent category)", "");
                    Name = Name.Replace("(symptom)", "");
                    Name = Name.Replace("(physical finding)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(Name))
                    {
                        uniquePhrases.Add(Name);
                        res.Add($"{countSpaces(Name) + 1}|{Name}|{CUI}|99");
                    }
                }

            }


            System.IO.File.WriteAllLines(@"R:\PubMed\Entity2CUI\Words2CUITop.csv", res.ToArray());


        }

        private void button38_Click(object sender, EventArgs e)
        {
            List<string> sentences = new List<string>();
            List<string> sentence = new List<string>();

            string lineCurr = "";
            string lineRest = "";

            using (StreamReader sr = new StreamReader(@"D:\PubMed\PDFs_Download_Extracted\out.res"))
            {
                while (sr.Peek() >= 0)
                {
                    string lineTxt = sr.ReadLine().ToLower().Trim();

                lbl:

                    bool endOfSentence = lineTxt.EndsWith(".");
                    bool sentenceReady = false;

                    int point = lineTxt.IndexOf(". ");
                    if (point >= 0)
                    {
                        lineCurr = (lineCurr + " " + lineTxt.Substring(0, point)).Trim(); // Add line before ". " to current sentence
                        lineRest = lineTxt.Substring(point + 2).Trim();
                        sentenceReady = true;
                    }
                    if (point == -1 && endOfSentence)
                    {
                        lineCurr = (lineCurr + " " + lineTxt).Trim();
                        lineRest = "";
                        sentenceReady = true;
                    }
                    if (point == -1 && !endOfSentence)
                    {
                        lineRest = (lineRest + " " + lineTxt).Trim();
                    }
                    if (sentenceReady)
                    {
                        lineCurr = (new string(lineCurr.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray())).Trim();
                        sentences.Add(lineCurr);

                        if (sentences.Count() > 10000)
                        {
                            System.IO.File.AppendAllLines(@"D:\PubMed\PDFs_Download_Extracted\outRes.res", sentences.ToArray());
                            sentences.Clear();
                            Application.DoEvents();
                            GC.Collect();
                        }
                    }
                    lineCurr = lineRest;
                    if (lineTxt.IndexOf(". ") >= 1 || lineTxt.EndsWith(".")) // if line still contains full sentence
                    {
                        lineTxt = lineCurr;
                        lineCurr = "";
                        lineRest = "";
                        goto lbl;
                    }
                }
            }

            System.IO.File.AppendAllLines(@"D:\PubMed\PDFs_Download_Extracted\outRes.res", sentences.ToArray());

            MessageBox.Show("!");

        }

        private void button39_Click(object sender, EventArgs e)
        {

            string file = @"D:\PubMed\Loader\Loader\Loader\bin\Debug\z.txt";

            Dictionary<string, string> z = new Dictionary<string, string>();
            HashSet<string> all_CUIs = new HashSet<string>();

            using (StreamReader sr = new StreamReader(file))
            {
                while (sr.Peek() >= 0)
                {
                    string lineTxt = sr.ReadLine().Trim();


                    if (lineTxt.Length > 8)
                    {
                        string CUI = lineTxt.Substring(lineTxt.Length - 8);

                        if (CUI.StartsWith("C"))
                        {
                            string text = lineTxt.Substring(0, lineTxt.Length - 8).Trim();
                            z.AddIfNotExist(CUI, text);
                            all_CUIs.AddIfNotExist(CUI);
                        }
                    }

                }
            }



            Dictionary<string, string> cui2name = new Dictionary<string, string>();
            Dictionary<string, string> cui2sty = new Dictionary<string, string>();
            Dictionary<string, HashSet<string>> cui2commonnames = new Dictionary<string, HashSet<string>>();


            string sSQL = $"SELECT mc.CUI, mc.STR, ms.STY FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND lat='ENG' AND  p.CUI in ('{string.Join("','", all_CUIs.ToArray())}')";
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1);
                    string STY = dataRdr.GetString(2);

                    cui2name.AddIfNotExist(CUI, Name.ToLower());
                    cui2sty.AddIfNotExist(CUI, STY);
                }
            }


            sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc WHERE  lat='ENG' AND  mc.CUI in ('{string.Join("','", all_CUIs.ToArray())}')";

            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1);

                    cui2commonnames.AddIfNotExist(CUI, new HashSet<string>());
                    cui2commonnames[CUI].AddIfNotExist(Name.ToLower());
                }
            }


            List<string> lst = new List<string>();

            foreach (var item in z)
            {
                try
                {
                    string synonims = string.Join("\t", cui2commonnames[item.Key].ToArray());
                    string re = $"{item.Key}\t{item.Value}\t{cui2name[item.Key]}\t{cui2sty[item.Key]}\t{synonims}";
                    lst.Add(re);
                }
                catch { }

            }

            System.IO.File.WriteAllLines(@"D:\PubMed\Loader\Loader\Loader\bin\Debug\zRes.res", lst.ToArray());


        }

        private void button40_Click(object sender, EventArgs e)
        {
            List<string> sentences = new List<string>();

            // Make CUI replacements with positions
            PrepareConcepts(@"R:\PubMed\Entity2CUI\Words2CUITop.csv");


            using (StreamReader sr = new StreamReader(@"D:\PubMed\PDFs_Download_Extracted\outRes.res"))
            {
                while (sr.Peek() >= 0)
                {

                    string lineTxt = sr.ReadLine().ToLower().Trim();

                    if (lineTxt.Contains("event") || lineTxt.Contains("adverse") || lineTxt.Contains("common") || lineTxt.Contains("toxic") || lineTxt.Contains("induced") || lineTxt.Contains("reaction") || lineTxt.Contains("effect") || lineTxt.Contains("side") || countCommas(lineTxt) >= 7)
                    {
                        continue;
                    }

                    string[] splittedText = lineTxt.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    //List<(string, int, int)> positions = new List<(string, int, int)>();

                    int posInStr = 0;

                    //foreach (var item in splittedText)
                    //{
                    //    int ii = lineTxt.IndexOf(item, posInStr);

                    //    positions.Add((item, ii, ii + item.Length - 1));
                    //    posInStr = ii + item.Length + 1;

                    //}

                    int sl = splittedText.Length;


                    HashSet<string> res = new HashSet<string>();
                    for (int i = 0; i < sl; i++)
                    {

                        if (stopWords.Contains(splittedText[i]))
                        {
                            //res.Add(splittedText[i]);
                            continue;
                        }


                        if (w7.ContainsKey(splittedText[i]) && sl > i + 6)
                        {
                            if (w7[splittedText[i]].ContainsKey(splittedText[i + 1]) && w7[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]].ContainsKey(splittedText[i + 5]) && w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]].ContainsKey(splittedText[i + 6]))
                            {
                                res.AddIfNotExist(w7[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]][splittedText[i + 6]]);
                                i = i + 6;
                                continue;
                            }
                        }
                        if (w6.ContainsKey(splittedText[i]) && sl > i + 5)
                        {
                            if (w6[splittedText[i]].ContainsKey(splittedText[i + 1]) && w6[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]) && w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]].ContainsKey(splittedText[i + 5]))
                            {
                                res.AddIfNotExist(w6[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]][splittedText[i + 5]]);
                                i = i + 5;
                                continue;
                            }
                        }
                        if (w5.ContainsKey(splittedText[i]) && sl > i + 4)
                        {
                            if (w5[splittedText[i]].ContainsKey(splittedText[i + 1]) && w5[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]) && w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]].ContainsKey(splittedText[i + 4]))
                            {
                                res.AddIfNotExist(w5[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]][splittedText[i + 4]]);
                                i = i + 4;
                                continue;
                            }
                        }
                        if (w4.ContainsKey(splittedText[i]) && sl > i + 3)
                        {
                            if (w4[splittedText[i]].ContainsKey(splittedText[i + 1]) && w4[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]) && w4[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]].ContainsKey(splittedText[i + 3]))
                            {
                                res.AddIfNotExist(w4[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]][splittedText[i + 3]]);
                                i = i + 3;
                                continue;
                            }
                        }
                        if (w3.ContainsKey(splittedText[i]) && sl > i + 2)
                        {
                            if (w3[splittedText[i]].ContainsKey(splittedText[i + 1]) && w3[splittedText[i]][splittedText[i + 1]].ContainsKey(splittedText[i + 2]))
                            {
                                res.AddIfNotExist(w3[splittedText[i]][splittedText[i + 1]][splittedText[i + 2]]);
                                i = i + 2;
                                continue;
                            }
                        }
                        if (w2.ContainsKey(splittedText[i]) && sl > i + 1)
                        {
                            if (w2[splittedText[i]].ContainsKey(splittedText[i + 1]))
                            {
                                res.AddIfNotExist(w2[splittedText[i]][splittedText[i + 1]]);
                                i = i + 1;
                                continue;
                            }
                        }
                        if (w1.ContainsKey(splittedText[i]))
                        {
                            res.AddIfNotExist(w1[splittedText[i]]);
                            continue;
                        }

                        //res.Add(splittedText[i]);

                    }

                    if (res.Count > 1)
                    {
                        sentences.Add(string.Join(" ", res.ToArray()));
                    }


                    if (sentences.Count() > 10000)
                    {
                        System.IO.File.AppendAllLines(@"D:\PubMed\PDFs_Download_Extracted\outCUIRes.res", sentences.ToArray());
                        sentences.Clear();
                        Application.DoEvents();
                        GC.Collect();
                    }
                }
                System.IO.File.AppendAllLines(@"D:\PubMed\PDFs_Download_Extracted\outCUIRes.res", sentences.ToArray());
            }

            MessageBox.Show("!");
        }

        private void WriteOneBatchFullText(Dictionary<int, List<string>> concepts)
        {
            List<string> ft = new List<string>();


            string MMDD = DateTime.Now.ToString("MMdd");

            foreach (var sentence in concepts)
            {
                if (sentence.Value.Count < 2) continue;

                // FT  - find dis and make __label__ string
                foreach (var CUI in sentence.Value)
                {
                    if (dis_CUIs.Contains(CUI) || (clusters.ContainsKey(CUI) && dis_CUIs.Contains(clusters[CUI])))
                    {
                        HashSet<string> CUIs2 = new HashSet<string>(sentence.Value);
                        CUIs2.Remove(CUI);
                        if (CUIs2.Count != 0)
                        {
                            ///if (f1.ContainsKey (CUI))
                            ft.Add($"__label__{CUI} {string.Join(" ", CUIs2.ToArray())}");

                        }


                    }
                }
            }

            System.IO.File.AppendAllLines(@"D:\PubMed\fulltext_scluCUI_50_FT_" + MMDD + ".txt", ft.ToArray());
        }

        private void button41_Click(object sender, EventArgs e)
        {
            string MMDD = DateTime.Now.ToString("MMdd");
            int clusterizeDis = 0;
            int clusterizeSym = 1;

            System.IO.File.Delete(@"D:\PubMed\fulltext_scluCUI_50_FT_" + MMDD + ".txt");


            ReadAllTopCSVs();
            Dictionary<int, List<string>> concepts = new Dictionary<int, List<string>>();

            int SENTENCE_ID = 1;

            using (StreamReader sr = new StreamReader(@"D:\PubMed\PDFs_Download_Extracted\outCUIRes.res"))
            {
                while (sr.Peek() >= 0)
                {

                    SENTENCE_ID++;
                    string CUIs = sr.ReadLine();

                    string[] splittedText = CUIs.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string CUI in splittedText)
                    {
                        string _CUI = CUI;
                        // Use code clustering - not DB clustering
                        if (clusterizeDis == 1 && clusters.ContainsKey(_CUI) && (dis_CUIs.Contains(clusters[_CUI]) || dis_CUIs.Contains(_CUI))) _CUI = clusters[_CUI];// Replace with MainCUI
                        if (clusterizeSym == 1 && clusters.ContainsKey(_CUI) && (sym_findings_CUIs.Contains(clusters[_CUI]) || sym_findings_CUIs.Contains(_CUI))) _CUI = clusters[_CUI];// Replace with MainCUI

                        if (!allCUIs.Contains(_CUI) && !clusters.ContainsKey(_CUI)) continue; // Skip CUI that does not exist in allCUIs or member of a cluster

                        if (!concepts.ContainsKey(SENTENCE_ID)) concepts.Add(SENTENCE_ID, new List<string>());
                        concepts[SENTENCE_ID].Add(_CUI);

                    }

                    if (concepts.Count >= 10000)
                    {
                        WriteOneBatchFullText(concepts);
                        concepts.Clear();
                    }
                }
                WriteOneBatchFullText(concepts);
            }

        }

        private void button42_Click(object sender, EventArgs e)
        {
            List<string> sents2remove = new List<string>();

            string sSQL = $"SELECT distinct SENTENCE_ID from smentity";
            MySqlConnection mylclcn = null;

            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {
                    int SENTENCE_ID = (Int32)dataRdr.GetUInt32(0);

                    DataTable dt = MyCommandExecutor("SELECT GROUP_CONCAT(s.SENTENCE SEPARATOR ', ')  from  sentence s, (SELECT PMID, number from  sentence  WHERE SENTENCE_ID = " + SENTENCE_ID.ToString() + ") t WHERE s.PMID=t.PMID AND s.NUMBER BETWEEN t.number-1 AND t.number+1;");
                    string lineTxt = dt.Rows[0][0].ToString().ToLower();

                    if (lineTxt.Contains("cancer") || lineTxt.Contains("event") || lineTxt.Contains("adverse") || lineTxt.Contains("common") || lineTxt.Contains("toxi") || lineTxt.Contains("induced") || lineTxt.Contains("reaction") || lineTxt.Contains("effect") || lineTxt.Contains("side"))
                    {
                        sents2remove.Add(SENTENCE_ID.ToString());
                    }


                    if (sents2remove.Count() > 10000)
                    {
                        System.IO.File.AppendAllLines(@"D:\PubMed\sents2remove.txt", sents2remove.ToArray());
                        sents2remove.Clear();
                        Application.DoEvents();
                        GC.Collect();
                    }
                }
            }

            System.IO.File.AppendAllLines(@"D:\PubMed\sents2remove.txt", sents2remove.ToArray());

        }

        private void button43_Click(object sender, EventArgs e)
        {
            //string baseDir= AppDomain.CurrentDomain.BaseDirectory;
            string baseDir = @"D:\PubMed\";

            string commonFile = @"_CommonNames.txt"; //D:\PubMed\

            string[] lines = File.ReadAllLines(baseDir + commonFile);
            foreach (string line in lines)
            {
                string[] ar = line.Split("\t");

                DataTable dt = MyCommandExecutor("select * from translations where ISCOMMON=1 and CUI ='" + ar[0] + "'");
                if (dt.Rows.Count == 0)
                {
                    MyCommandExecutor($"INSERT INTO translations( CUI,STR, LANG,ISCOMMON) VALUES ('{ar[0]}','{ar[1].SQLString()}','ENG',1)");
                }

            }

            List<string> cmds = new List<string>();

            string List8 = @"_List8.txt"; //D:\PubMed\
            lines = File.ReadAllLines(baseDir + List8);

            if (lines.Length > 0)
            {
                string cmd = "delete from diseaseSpec;\r\n";
                //MyCommandExecutor(cmd);
                cmds.Add(cmd);
            }

            foreach (string line in lines)
            {
                string[] ar = line.Split("\t", StringSplitOptions.None);
                if (ar[0].Length == 8 && ar[0].StartsWith("C"))
                {
                    string cmd = $"INSERT INTO diseaseSpec( CUI, Urgency, PrefCUI, ReqCUI, Gender,Recommendation,S1,S2,S3) VALUES ('{ar[0].SQLString()}','{ar[3].SQLString()}','{ar[4].SQLString()}','{ar[6].SQLString()}','{ar[13].SQLString()}','{ar[14].SQLString()}','{ar[8].SQLString()}','{ar[10].SQLString()}','{ar[12].SQLString()}');\r\n";

                    //MyCommandExecutor(cmd);
                    cmds.Add(cmd);
                }



            }

            System.IO.File.WriteAllLines(@"D:\PubMed\diseasespec3.sql", cmds.ToArray());


        }

        private void button44_Click(object sender, EventArgs e)
        {
            string List8 = @"D:\PubMed\_List8.txt"; //
            var lines = File.ReadAllLines(List8);
            HashSet<string> l8 = new HashSet<string>();

            foreach (string line in lines)
            {
                string CUI = line.Substring(0, 8);
                l8.AddIfNotExist(CUI);
            }


            List<string> woValidation = new List<string>();
            string MMDD = DateTime.Now.ToString("MMdd");

            string f = @"D:\PubMed\abstracts_scluCUI_FT_"+ MMDD + ".txt";
            HashSet<string> CUIs = new HashSet<string>();


            using (StreamReader sr = new StreamReader(f))
            {
                while (sr.Peek() >= 0)
                {
                    string line = sr.ReadLine();

                    string CIU = line.Substring(9, 8);
                    if (l8.Contains(CIU))
                        woValidation.Add(line);
                }
            }
            System.IO.File.WriteAllLines(@"D:\PubMed\abstracts_scluCUI_FT_" + MMDD + "_wovalidation.txt", woValidation.ToArray());

        }

        private string GetCell(ExcelWorksheet ws, int rowNumber, int colNumber)
        {
            string res = "";

            object obj = ws.Cells[rowNumber, colNumber].Value;
            if (obj != null)
            {
                res = obj.ToString();
            }

            return res;
        }

        private void BackOldFile(string file)
        {
            if (File.Exists(file))
            {
                var dt = File.GetCreationTime(file);
                var newName = file.Replace(".", "_" + dt.ToString("yyyyMMdd") + ".");
                if (File.Exists(newName)) File.Delete(newName);
                File.Move(file, newName);
            }
        }

        private void button45_Click(object sender, EventArgs e)
        {
            HashSet<string> AllCIUs = new HashSet<string>();

            Dictionary<string, string> cui2sty = new Dictionary<string, string>();
            Dictionary<string, string> cui2name = new Dictionary<string, string>();
            Dictionary<string, string> name2cui = new Dictionary<string, string>();
            Dictionary<string, string> SimpleName2cui = new Dictionary<string, string>();
            Dictionary<string, string> UniqueSimpleNames = new Dictionary<string, string>();


            string sSQL = $"SELECT mc.CUI, mc.STR, ms.STY FROM  mrconso mc, mrsty ms, cuinamepopularity p  WHERE mc.CUI=p.CUI AND  mc.CUI=ms.CUI AND lat='ENG'";

            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1);
                    string Sty = dataRdr.GetString(2);

                    cui2sty.AddIfNotExist(CUI, Sty);
                    cui2name.AddIfNotExist(CUI, Name.ToLower());
                    name2cui.AddIfNotExist(Name.ToLower(), CUI);
                }
            }

            var GoogleFile = new FileInfo(@"D:\PubMed\Diseases and symptoms.xlsx");

            List<string> cmds = new List<string>();
            cmds.Add("delete from diseaseSpec;");


            // Open and read the XLSX file.
            using (var package = new ExcelPackage(GoogleFile))
            {

                // Get the work book in the file
                ExcelWorkbook workBook = package.Workbook;
                string CUIsfromNamesAdded = "";

                if (workBook != null)
                {
                    if (workBook.Worksheets.Count > 0)
                    {

                        // Get worksheet
                        ExcelWorksheet currentWorksheet = null;
                        foreach (ExcelWorksheet sheet in workBook.Worksheets)
                        {
                            // Лист8
                            if ((sheet.Hidden == eWorkSheetHidden.Visible) && (sheet.Name.StartsWith("Лист8")))
                            {
                                currentWorksheet = sheet;
                                for (int rowNumber = 2; rowNumber <= currentWorksheet.Dimension.End.Row; rowNumber++)
                                {
                                    for (int colNumber = 1; colNumber <= currentWorksheet.Dimension.End.Column; colNumber++)
                                    {
                                        string fn = GetCell(currentWorksheet, rowNumber, colNumber).Trim();

                                        // Get all CUIs
                                        if (colNumber.InList(1, 5, 7, 9, 11, 14) && fn.Trim() != "")
                                        {
                                            AllCIUs.AddIfNotExist(fn);
                                        }

                                        // Manual names
                                        if (colNumber == 3)
                                        {
                                            string CUI = GetCell(currentWorksheet, rowNumber, 1).Trim();
                                            if (fn != "" && CUI.Length == 8 && CUI.StartsWith("C"))
                                            {
                                                SimpleName2cui.AddIfNotExist(fn.ToLower(), CUI);
                                                UniqueSimpleNames.AddIfNotExist(CUI, fn.ToLower());
                                            }
                                        }
                                        if (colNumber == 6)
                                        {
                                            string CUI = GetCell(currentWorksheet, rowNumber, 5).Trim();
                                            if (fn != "" && CUI.Length == 8 && CUI.StartsWith("C"))
                                            {
                                                SimpleName2cui.AddIfNotExist(fn.ToLower(), CUI);
                                                UniqueSimpleNames.AddIfNotExist(CUI, fn.ToLower());
                                            }
                                        }
                                        if (colNumber == 8)
                                        {
                                            string CUI = GetCell(currentWorksheet, rowNumber, 9).Trim();
                                            if (fn != "" && CUI.Length == 8 && CUI.StartsWith("C"))
                                            {
                                                SimpleName2cui.AddIfNotExist(fn.ToLower(), CUI);
                                                UniqueSimpleNames.AddIfNotExist(CUI, fn.ToLower());
                                            }
                                        }
                                        if (colNumber == 10)
                                        {
                                            string CUI = GetCell(currentWorksheet, rowNumber, 11).Trim();
                                            if (fn != "" && CUI.Length == 8 && CUI.StartsWith("C"))
                                            {
                                                SimpleName2cui.AddIfNotExist(fn.ToLower(), CUI);
                                                UniqueSimpleNames.AddIfNotExist(CUI, fn.ToLower());
                                            }
                                        }
                                        if (colNumber == 12)
                                        {
                                            string CUI = GetCell(currentWorksheet, rowNumber, 13).Trim();
                                            if (fn != "" && CUI.Length == 8 && CUI.StartsWith("C"))
                                            {
                                                SimpleName2cui.AddIfNotExist(fn.ToLower(), CUI);
                                                UniqueSimpleNames.AddIfNotExist(CUI, fn.ToLower());
                                            }
                                        }

                                        // Get symptoms names to convert to CUIs
                                        if (colNumber.InList(8, 10, 12) && GetCell(currentWorksheet, rowNumber, colNumber + 1).Trim() == "")
                                        {
                                            if (name2cui.ContainsKey(fn.ToLower()))
                                            {
                                                currentWorksheet.Cells[rowNumber, colNumber + 1].Value = name2cui[fn.ToLower()];
                                                CUIsfromNamesAdded = CUIsfromNamesAdded + " " + fn;
                                            }
                                        }

                                    }


                                    // diseaseSpec Go by lines
                                    {
                                        string CUI = GetCell(currentWorksheet, rowNumber, 1).Trim();
                                        string Urgency = GetCell(currentWorksheet, rowNumber, 4).Trim();
                                        string PrefCUI = GetCell(currentWorksheet, rowNumber, 5).Trim();
                                        string ReqCUI = GetCell(currentWorksheet, rowNumber, 7).Trim();
                                        string Gender = GetCell(currentWorksheet, rowNumber, 14).Trim();
                                        string Recommendation = GetCell(currentWorksheet, rowNumber, 15).Trim();
                                        string S1 = GetCell(currentWorksheet, rowNumber, 9).Trim();
                                        string S2 = GetCell(currentWorksheet, rowNumber, 11).Trim();
                                        string S3 = GetCell(currentWorksheet, rowNumber, 13).Trim();
                                        string SName1 = GetCell(currentWorksheet, rowNumber, 8).Trim();
                                        string SName2 = GetCell(currentWorksheet, rowNumber, 10).Trim();
                                        string SName3 = GetCell(currentWorksheet, rowNumber, 12).Trim();

                                        if (CUI.Length == 8 && CUI.StartsWith("C"))
                                        {
                                            string cmd = $"INSERT INTO diseaseSpec( CUI, Urgency, PrefCUI, ReqCUI, Gender,Recommendation,S1,S2,S3, SName1,SName2,SName3) VALUES ('{CUI.SQLString()}','{Urgency.SQLString()}','{PrefCUI.SQLString()}','{ReqCUI.SQLString()}','{Gender.SQLString()}','{Recommendation.SQLString()}','{S1.SQLString()}','{S2.SQLString()}','{S3.SQLString()}','{SName1.SQLString()}','{SName2.SQLString()}','{SName3.SQLString()}');\r\n";
                                            cmds.Add(cmd);
                                        }



                                    }

                                }

                            } // Лист8

                            if ((sheet.Hidden == eWorkSheetHidden.Visible) && (sheet.Name.StartsWith("Top Findings")))
                            {
                                currentWorksheet = sheet;
                                for (int rowNumber = 1; rowNumber <= currentWorksheet.Dimension.End.Row; rowNumber++)
                                {
                                    string CUI = GetCell(currentWorksheet, rowNumber, 1).Trim();
                                    if (CUI.Length == 8 && CUI.StartsWith("C"))
                                    {
                                        AllCIUs.AddIfNotExist(CUI);

                                        string simpleName = GetCell(currentWorksheet, rowNumber, 4).Trim().ToLower();
                                        if (simpleName != "")
                                        {
                                            SimpleName2cui.AddIfNotExist(simpleName, CUI);
                                            UniqueSimpleNames.AddIfNotExist(CUI, simpleName);
                                        }
                                    }
                                }

                            }  // Top Findings

                            if ((sheet.Hidden == eWorkSheetHidden.Visible) && (sheet.Name.StartsWith("Top Symptoms")))
                            {
                                currentWorksheet = sheet;
                                for (int rowNumber = 1; rowNumber <= currentWorksheet.Dimension.End.Row; rowNumber++)
                                {
                                    string CUI = GetCell(currentWorksheet, rowNumber, 1).Trim();
                                    if (CUI.Length == 8 && CUI.StartsWith("C"))
                                    {
                                        AllCIUs.AddIfNotExist(CUI);

                                        string simpleName = GetCell(currentWorksheet, rowNumber, 4).Trim().ToLower();
                                        if (simpleName != "")
                                        {
                                            SimpleName2cui.AddIfNotExist(simpleName, CUI);
                                            UniqueSimpleNames.AddIfNotExist(CUI, simpleName);
                                        }
                                    }
                                }

                            }  // Top Symptoms


                            if ((sheet.Hidden == eWorkSheetHidden.Visible) && (sheet.Name.ToLower().StartsWith("top sign")))
                            {
                                currentWorksheet = sheet;
                                for (int rowNumber = 1; rowNumber <= currentWorksheet.Dimension.End.Row; rowNumber++)
                                {
                                    string CUI = GetCell(currentWorksheet, rowNumber, 1).Trim();
                                    if (CUI.Length == 8 && CUI.StartsWith("C"))
                                    {
                                        AllCIUs.AddIfNotExist(CUI);

                                        string simpleName = GetCell(currentWorksheet, rowNumber, 4).Trim().ToLower();
                                        if (simpleName != "")
                                        {
                                            SimpleName2cui.AddIfNotExist(simpleName, CUI);
                                            //UniqueSimpleNames.AddIfNotExist(CUI, simpleName);
                                        }

                                        simpleName = GetCell(currentWorksheet, rowNumber, 5).Trim().ToLower();
                                        if (simpleName != "")
                                        {
                                            SimpleName2cui.AddIfNotExist(simpleName, CUI);
                                            //UniqueSimpleNames.AddIfNotExist(CUI, simpleName);
                                        }

                                        simpleName = GetCell(currentWorksheet, rowNumber, 6).Trim().ToLower();
                                        if (simpleName != "")
                                        {
                                            SimpleName2cui.AddIfNotExist(simpleName, CUI);
                                            //UniqueSimpleNames.AddIfNotExist(CUI, simpleName);
                                        }
                                    }
                                }

                            }  // top sign

                            if ((sheet.Hidden == eWorkSheetHidden.Visible) && (sheet.Name.StartsWith("Болезни_коды симптомов")))
                            {
                                currentWorksheet = sheet;
                                for (int rowNumber = 2; rowNumber <= currentWorksheet.Dimension.End.Row; rowNumber++)
                                {
                                    for (int colNumber = 1; colNumber <= currentWorksheet.Dimension.End.Column; colNumber++)
                                    {
                                        string CUI = GetCell(currentWorksheet, rowNumber, colNumber).Trim();

                                        // Get all CUIs
                                        if (colNumber.InList(5, 6, 7, 8, 9, 10, 11, 12, 13, 14) && CUI.Length == 8 && CUI.StartsWith("C"))
                                        {
                                            AllCIUs.AddIfNotExist(CUI);
                                        }
                                    }
                                }

                            }  // Болезни_коды симптомов
                        }
                    }
                } // workBook

                if (CUIsfromNamesAdded.Trim() != "")
                {
                    var fs = new FileStream(@"D:\PubMed\Diseases and symptoms_New.xlsx", FileMode.Create, FileAccess.Write, FileShare.None);

                    package.SaveAs(fs);
                    fs.Flush();
                    fs.Close();

                    MessageBox.Show(CUIsfromNamesAdded);
                }
            }

            // Update top_manual_descriptions
            BackOldFile(@"D:\PubMed\top_manual_descriptions.txt");
            File.WriteAllLines(@"D:\PubMed\top_manual_descriptions.txt", SimpleName2cui.Select(x => x.Value + "\t" + x.Key).ToArray());

            // Update CommonNames.sql
            BackOldFile(@"D:\PubMed\CommonNames.sql");
            File.WriteAllText(@"D:\PubMed\CommonNames.sql", "delete from translations where LANG='ENG' and ISCOMMON=1;\r\n");
            File.AppendAllLines(@"D:\PubMed\CommonNames.sql", UniqueSimpleNames.Select(x => $"INSERT INTO translations(CUI, STR, LANG, ISCOMMON) VALUES('{x.Key.SQLString()}', '{x.Value.SQLString()}', 'ENG', 1);").ToArray());


            // Update top_disease_or_syndrome top_finding.txt
            BackOldFile(@"D:\PubMed\top_disease_or_syndrome.txt");
            BackOldFile(@"D:\PubMed\top_finding.txt");
            BackOldFile(@"D:\PubMed\top_sign_or_symptom.txt");

            Dictionary<string, string> dis = new Dictionary<string, string>();
            Dictionary<string, string> find = new Dictionary<string, string>();
            Dictionary<string, string> sym = new Dictionary<string, string>();
            foreach (string CUI in AllCIUs)
            {
                if (cui2sty.ContainsKey(CUI))
                {
                    if (cui2sty[CUI] == "Disease or Syndrome") dis.AddIfNotExist(CUI, cui2name[CUI]);
                    if (cui2sty[CUI] == "Finding") find.AddIfNotExist(CUI, cui2name[CUI]);
                    if (cui2sty[CUI] == "Sign or Symptom") sym.AddIfNotExist(CUI, cui2name[CUI]);
                }
            }

            File.WriteAllLines(@"D:\PubMed\top_disease_or_syndrome.txt", dis.Select(x => x.Key + "\t" + x.Value).ToArray());
            File.WriteAllLines(@"D:\PubMed\top_finding.txt", find.Select(x => x.Key + "\t" + x.Value).ToArray());
            File.WriteAllLines(@"D:\PubMed\top_sign_or_symptom.txt", sym.Select(x => x.Key + "\t" + x.Value).ToArray());


            // Press Get Ft __lbls for Symptoms
            button29_Click(null, null);

            System.IO.File.WriteAllLines(@"D:\PubMed\diseasespec.sql", cmds.ToArray());


        }

        private void button43_Click_1(object sender, EventArgs e)
        {

        }

        private void button46_Click(object sender, EventArgs e)
        {
            List<string> res = new List<string>();
            HashSet<string> uniquePhrases = new HashSet<string>();
            MySqlConnection mylclcn = null;
            string sSQL;


            sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND lat='ENG' AND  p.STY ='Vitamins and Supplements'";

            res.Clear();
            uniquePhrases.Clear();
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1).ToLower();

                    //Normalize

                    Name = Name.Replace("(substance)", "");
                    Name = Name.Replace("(organism)", "");
                    Name = Name.Replace("(vaginal)", "");
                    Name = Name.Replace("(dietary)", "");
                    Name = Name.Replace("(discontinued)", "");
                    Name = Name.Replace("(eukaryote)", "");
                    Name = Name.Replace("(medication)", "");
                    Name = Name.Replace("(plant)", "");
                    Name = Name.Replace("(fungus)", "");
                    Name = Name.Replace("(product)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(CUI + Name))
                    {
                        uniquePhrases.Add(CUI + Name);
                        res.Add($"__label__{CUI} {Name}");
                        //resSQL.Add($"'{CUI}','{Name.SQLString ()}'");
                    }
                }
            }
            BackOldFile(@"D:\PubMed\ft_vitamins.txt");
            System.IO.File.WriteAllLines(@"D:\PubMed\ft_vitamins.txt", res.ToArray());


            sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND lat='ENG' AND  p.STY ='Gene or Genome'";

            res.Clear();
            uniquePhrases.Clear();
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1).ToLower();

                    //Normalize

                    Name = Name.Replace("(substance)", "");
                    Name = Name.Replace("(organism)", "");
                    Name = Name.Replace("(vaginal)", "");
                    Name = Name.Replace("(dietary)", "");
                    Name = Name.Replace("(discontinued)", "");
                    Name = Name.Replace("(eukaryote)", "");
                    Name = Name.Replace("(medication)", "");
                    Name = Name.Replace("(plant)", "");
                    Name = Name.Replace("(fungus)", "");
                    Name = Name.Replace("(product)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(CUI + Name))
                    {
                        uniquePhrases.Add(CUI + Name);
                        res.Add($"__label__{CUI} {Name}");
                        //resSQL.Add($"'{CUI}','{Name.SQLString ()}'");
                    }
                }
            }
            BackOldFile(@"D:\PubMed\ft_genes.txt");
            System.IO.File.WriteAllLines(@"D:\PubMed\ft_genes.txt", res.ToArray());


            
            sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI = ms.CUI AND mc.CUI = p.CUI AND lat = 'ENG' AND p.STY IN('Pharmacologic Substance','Clinical Drug')";

            res.Clear();
            uniquePhrases.Clear();
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1).ToLower();

                    //Normalize

                    Name = Name.Replace("(substance)", "");
                    Name = Name.Replace("(organism)", "");
                    Name = Name.Replace("(vaginal)", "");
                    Name = Name.Replace("(dietary)", "");
                    Name = Name.Replace("(discontinued)", "");
                    Name = Name.Replace("(eukaryote)", "");
                    Name = Name.Replace("(medication)", "");
                    Name = Name.Replace("(plant)", "");
                    Name = Name.Replace("(fungus)", "");
                    Name = Name.Replace("(product)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(CUI + Name))
                    {
                        uniquePhrases.Add(CUI + Name);
                        res.Add($"__label__{CUI} {Name}");
                        //resSQL.Add($"'{CUI}','{Name.SQLString ()}'");
                    }
                }
            }
            BackOldFile(@"D:\PubMed\ft_drugs.txt");
            System.IO.File.WriteAllLines(@"D:\PubMed\ft_drugs.txt", res.ToArray());

        }

        private void button47_Click(object sender, EventArgs e)
        {
            MySqlConnection mylclcn = null;
            List<string> res = new List<string>();
            HashSet<string> uniquePhrases = new HashSet<string>();

            List<string> austrailanCUIs = new List<string>();

            ReadAllTopCSVs();

            using (var reader = new StreamReader(@"D:\PubMed\austrailanCUIs.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string lbl = line.Substring(0, 8);

                    austrailanCUIs.AddIfNotExist(lbl);
                }
            }

            var allCUIs_withClusters = allCUIs.Concat(clusters.Keys).Concat (austrailanCUIs).Distinct().ToList();
            string sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND lat='ENG' AND  p.CUI in ('{string.Join("','", allCUIs_withClusters.ToArray())}')";


            //string sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND lat='ENG' AND  p.CUI in ('{string.Join("','", austrailanCUIs.ToArray())}')";

            //string sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND lat='ENG' AND  p.CUI in ('C0002736')";


            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1).ToLower();

                    // Clusterise to MainCUI
                    if (clusters.ContainsKey(CUI)) CUI = clusters[CUI];

                    //Normalize
                    Name = Name.Replace("[d]", "");
                    Name = Name.Replace("(disorder)", "");
                    Name = Name.Replace("(finding)", "");
                    Name = Name.Replace("(situation)", "");
                    Name = Name.Replace("(diagnosis)", "");
                    Name = Name.Replace("(context-dependent category)", "");
                    Name = Name.Replace("(symptom)", "");
                    Name = Name.Replace("(physical finding)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(CUI + Name))
                    {
                        uniquePhrases.Add(CUI + Name);
                        res.Add($"__label__{CUI} {Name}");
                        //resSQL.Add($"'{CUI}','{Name.SQLString ()}'");

                        if (Name.ContainsWholeWord("hurt"))
                        {
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("hurts"))
                        {
                            Name = Name.ReplaceWholeWord("hurts", "aches");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("pain"))
                        {
                            Name = Name.ReplaceWholeWord("pain", "hurt");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                    }
                }
            }

            using (var reader = new StreamReader(@"D:\PubMed\top_manual_descriptions.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");

                    string CUI = lbl[0];

                    // Clusterise to MainCUI
                    if (clusters.ContainsKey(CUI)) CUI = clusters[CUI];

                    string Name = lbl[1].ToLower();
                    //Normalize
                    Name = Name.Replace("[d]", "");
                    Name = Name.Replace("(disorder)", "");
                    Name = Name.Replace("(finding)", "");
                    Name = Name.Replace("(situation)", "");
                    Name = Name.Replace("(diagnosis)", "");
                    Name = Name.Replace("(context-dependent category)", "");
                    Name = Name.Replace("(symptom)", "");
                    Name = Name.Replace("(physical finding)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(Name))
                    {
                        uniquePhrases.Add(Name);
                        res.Add($"__label__{CUI} {Name}");

                        if (Name.ContainsWholeWord("hurt"))
                        {
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("hurts"))
                        {
                            Name = Name.ReplaceWholeWord("hurts", "aches");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("pain"))
                        {
                            Name = Name.ReplaceWholeWord("pain", "hurt");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }

                    }
                }

            }


            BackOldFile(@"D:\PubMed\ft_symptom_recognition_validation.txt");
            System.IO.File.WriteAllLines(@"D:\PubMed\ft_symptom_recognition_validation.txt", res.ToArray());

            /*--------------------------------------*/

            uniquePhrases.Clear();
            res.Clear();



            sSQL = $"SELECT mc.CUI, mc.STR FROM  mrconso mc, mrsty ms, cuinamepopularity p WHERE mc.CUI=ms.CUI AND mc.CUI=p.CUI AND lat='ENG' AND  (p.CUI in ('{string.Join("','", allCUIs_withClusters.ToArray())}')  or p.CUI in (SELECT  CUI  FROM mrsty WHERE sty in ('Age Group', 'Amino Acid, Peptide, or Protein', 'Antibiotic', 'Biologically Active Substance', 'Chemical', 'Classification', 'Clinical Attribute', 'Clinical Drug', 'Diagnostic Procedure', 'Disease or Syndrome', 'Finding', 'Gene or Genome', 'Hormone', 'Immunologic Factor', 'Laboratory Procedure', 'Laboratory or Test Result', 'Nucleic Acid, Nucleoside, or Nucleotide', 'Nucleotide Sequence', 'Organic Chemical', 'Pharmacologic Substance', 'Sign or Symptom', 'Vitamin', 'Vitamins and Supplements')) )";

            //('Age Group', 'Amino Acid, Peptide, or Protein', 'Antibiotic', 'Biologically Active Substance', 'Chemical', 'Classification', 'Clinical Attribute', 'Clinical Drug', 'Diagnostic Procedure', 'Disease or Syndrome', 'Finding', 'Gene or Genome', 'Hormone', 'Immunologic Factor', 'Laboratory Procedure', 'Laboratory or Test Result', 'Nucleic Acid, Nucleoside, or Nucleotide', 'Nucleotide Sequence', 'Organic Chemical', 'Pharmacologic Substance', 'Sign or Symptom', 'Vitamin', 'Vitamins and Supplements')
            using (MySqlDataReader dataRdr = MyCommandExecutorDataReader(sSQL, mylclcn))
            {
                while (dataRdr.Read())
                {
                    string CUI = dataRdr.GetString(0);
                    string Name = dataRdr.GetString(1).ToLower();

                    // Clusterise to MainCUI
                    if (clusters.ContainsKey(CUI)) CUI = clusters[CUI];

                    //Normalize
                    Name = Name.Replace("[d]", "");
                    Name = Name.Replace("(disorder)", "");
                    Name = Name.Replace("(finding)", "");
                    Name = Name.Replace("(situation)", "");
                    Name = Name.Replace("(diagnosis)", "");
                    Name = Name.Replace("(context-dependent category)", "");
                    Name = Name.Replace("(symptom)", "");
                    Name = Name.Replace("(physical finding)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(CUI + Name))
                    {
                        uniquePhrases.Add(CUI + Name);
                        res.Add($"__label__{CUI} {Name}");
                        //resSQL.Add($"'{CUI}','{Name.SQLString ()}'");

                        if (Name.ContainsWholeWord("hurt"))
                        {
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("hurts"))
                        {
                            Name = Name.ReplaceWholeWord("hurts", "aches");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("pain"))
                        {
                            Name = Name.ReplaceWholeWord("pain", "hurt");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                    }
                }
            }

            using (var reader = new StreamReader(@"D:\PubMed\top_manual_descriptions.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    string[] lbl = line.Split("\t");

                    string CUI = lbl[0];

                    // Clusterise to MainCUI
                    if (clusters.ContainsKey(CUI)) CUI = clusters[CUI];

                    string Name = lbl[1].ToLower();
                    //Normalize
                    Name = Name.Replace("[d]", "");
                    Name = Name.Replace("(disorder)", "");
                    Name = Name.Replace("(finding)", "");
                    Name = Name.Replace("(situation)", "");
                    Name = Name.Replace("(diagnosis)", "");
                    Name = Name.Replace("(context-dependent category)", "");
                    Name = Name.Replace("(symptom)", "");
                    Name = Name.Replace("(physical finding)", "");
                    Name = Name.Replace("[ambiguous]", "");

                    Name = Name.ReplaceWholeWord("nos", "");

                    Name = new string(Name.Select(ch => (char.IsPunctuation(ch) && ch != '\'') ? ' ' : ch).ToArray());
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ");
                    Name = Name.Replace("  ", " ").Trim();

                    if (!uniquePhrases.Contains(Name))
                    {
                        uniquePhrases.Add(Name);
                        res.Add($"__label__{CUI} {Name}");

                        if (Name.ContainsWholeWord("hurt"))
                        {
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("hurts"))
                        {
                            Name = Name.ReplaceWholeWord("hurts", "aches");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }
                        if (Name.ContainsWholeWord("pain"))
                        {
                            Name = Name.ReplaceWholeWord("pain", "hurt");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                            Name = Name.ReplaceWholeWord("hurt", "ache");
                            uniquePhrases.Add(Name);
                            res.Add($"__label__{CUI} {Name}");
                        }

                    }
                }

            }

            BackOldFile(@"D:\PubMed\ft_recognition_large.txt");
            System.IO.File.WriteAllLines(@"D:\PubMed\ft_recognition_large.txt", res.ToArray());


        }
    }
}