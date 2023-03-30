using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Xml;

namespace TCWaferGUI
{
    public partial class Form1 : Form
    {
        //global variables
        private bool connectedStatus { get; set; }
        private String connectedPort { get; set; }
        private List<int> TCXList { get; set; } // TC x cordinate list
        private List<int> TCYList { get; set; } // TC y cordinate list
        private List<double> TCTList { get; set; } //current temperature list
        private List<int> TCList { get; set; }
        private List<List<int>> WaferPixelArea { get; set; }
        private String csvPath { get; set; }
        private int csvId { get; set; }
        private DataTable TCTable { get; set; }
        private SerialPort startPort { get; set; }

        //Form Initializing
        public Form1()
        {
            InitializeComponent();         
        }

        //Form load event
        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                TCTable = new DataTable();
                TCList = new List<int> ();
                TCXList = new List<int> ();
                TCYList = new List<int> ();
                TCTList = new List<double> ();              
                startPort = new SerialPort();
                WaferInitilizer();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                this.Close();
            }
            
        }
        //wafer refresher
        private void WaferRefresh()
        {
            Bitmap BMP = new Bitmap(600, 600);
            Graphics GFX = Graphics.FromImage(BMP);
            GFX.FillEllipse(Brushes.Black, 0, 0, 600, 600);

            if (TCList.Count > 0)
            {
                foreach (List<int> pixelxy in WaferPixelArea)
                {
                    List<double> TCDistList = new List<double>();
                    List<int> TCDistIndexList = new List<int>();
                    Dictionary<int, double> TCDistDic = new Dictionary<int, double>();
                    double TCCenterTemp = 0;

                    bool isTC = false;

                    for (int i = 0; i < TCXList.Count; i++)
                    {
                        if (pixelxy[0] == TCXList[i] && pixelxy[1] == TCYList[i])
                        {
                            isTC = true;
                        }
                    }
                    if (isTC == false)
                    {
                        for (int i = 0; i < TCXList.Count; i++)
                        {
                            double tempDist = (double)Math.Sqrt(Math.Pow((pixelxy[0] - TCXList[i]), 2) + Math.Pow((pixelxy[1] - TCYList[i]), 2));
                            TCDistDic.Add(i, tempDist);
                        }

                        //ascending order sorting
                        foreach (KeyValuePair<int, double> item in TCDistDic.OrderBy(key => key.Value))
                        {
                            TCDistIndexList.Add(item.Key);
                            TCDistList.Add(item.Value);
                        }

                        // Pixel temperature calculating equation
                        double TCDisDiv = 0;
                        for (int i = 0; i < TCDistList.Count; i++)
                        {
                            double TCDistMult = 1;
                            for (int j = 0; j < TCDistList.Count; j++)
                            {
                                if (i != j)
                                {
                                    TCDistMult *= TCDistList[j];
                                }
                            }
                            TCDisDiv += TCDistMult;
                            TCCenterTemp += TCTList[TCDistIndexList[i]] * TCDistMult;
                        }
                        TCCenterTemp /= (TCDisDiv * 1.0);

                        //Pixel coloring
                        double TCCenterT = TCCenterTemp;
                        if (TCCenterT >= TCTList.Min() && TCCenterT <= TCTList.Max())
                        {
                            Color pixelColor = RainbowColor(TCCenterT);
                            BMP.SetPixel(pixelxy[0], pixelxy[1], pixelColor);
                        }
                        else if (TCCenterT > TCTList.Max())
                        {
                            Color pixelColor = RainbowColor(TCTList.Max());
                            BMP.SetPixel(pixelxy[0], pixelxy[1], pixelColor);
                        }
                        else
                        {
                            Color pixelColor = RainbowColor(TCTList.Min());
                            BMP.SetPixel(pixelxy[0], pixelxy[1], pixelColor);
                        }
                    } 
                }
                //TC name add
                for (int i = 0; i < TCList.Count; i++)
                {
                    GFX.DrawString("TC" + TCList[i].ToString() + "-" + TCTList[i].ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, TCXList[i]-10, TCYList[i]-5);
                }
                WaferMap.Image = BMP;

                //Gragh refresh
                foreach (int TC in TCList)
                {
                    string chartName = "chart" + TC;
                    Chart chart = (Chart)GetControlByName(flowLayoutPanel1, chartName);
                    if (chart != null)
                    {
                        chart.Invoke(new MethodInvoker(delegate {
                            chart.Series.Clear();
                            chart.DataSource = TCTable;
                            if (TCTable.Rows.Count >= 50)
                            {
                                chart.ChartAreas[0].AxisX.Minimum = TCTable.Rows.Count - 50;
                                chart.ChartAreas[0].AxisX.Maximum = TCTable.Rows.Count;
                            }
                            for (int i = 1; i < TCTable.Columns.Count; i++)
                            {
                                if (TCTable.Columns[i].ColumnName == TC.ToString())
                                {
                                    Series series = new Series();
                                    series.XValueMember = TCTable.Columns[2].ColumnName;
                                    series.YValueMembers = TCTable.Columns[i].ColumnName;
                                    series.ChartType = SeriesChartType.Line;
                                    series.IsVisibleInLegend = true;
                                    series.IsValueShownAsLabel = false;
                                    series.BorderWidth = 3;
                                    series.LegendText = TCTable.Columns[i].ColumnName;
                                    chart.Series.Add(series);
                                    chart.DataBind();
                                }
                                else
                                {

                                }
                            }
                        }));
                    }
                }

                lblMaxTemp.Invoke(new MethodInvoker(delegate { lblMaxTemp.Text = TCTList.Max().ToString() + " C"; }));
                lblMinTemp.Invoke(new MethodInvoker(delegate { lblMinTemp.Text = TCTList.Min().ToString() + " C"; }));
            }
        }

        //Function - find selected TC and list them + dynamically add graph panels
        private void WaferInitilizer()
        {
            flowLayoutPanel1.Controls.Clear();


            if (TCList.Count > 0)
            {
                foreach (var TC in TCList)
                {
                    PanelGraphCloner(TC, TCTable);
                }
            }

            Bitmap BMP2 = new Bitmap(600, 600);
            Graphics GFX2 = Graphics.FromImage(BMP2);
            GFX2.FillEllipse(Brushes.Black, 0, 0, 600, 600);

            WaferMap.BackgroundImage = BMP2;

            WaferPixelArea = new List<List<int>>();
            for (int x = 0; x < 600; x++)
            {
                for (int y = 0; y < 600; y++)
                {
                    if (BMP2.GetPixel(x, y).ToArgb() != 0)
                    {
                        List<int> pixelxy = new List<int> {x, y};
                        WaferPixelArea.Add(pixelxy);
                    }
                }
            }
        }

        //clock - 1s refresh
        private void Timer1_Tick(object Sender, EventArgs e)
        {
            //List<int> TCList = TCAvailable();
            //Random rand = new Random();
            //foreach (int TC in TCList)
            //{
            //    int randTemp = rand.Next(200);
            //    ColorfyCB(TC, randTemp);
            //}
        }

        //Function - Get any controller by name withing a parent control object
        public Control GetControlByName(Control ParentCntl, string NameToSearch)
        {
            if (ParentCntl.Name == NameToSearch)
                return ParentCntl;

            foreach (Control ChildCntl in ParentCntl.Controls)
            {
                Control ResultCntl = GetControlByName(ChildCntl, NameToSearch);
                if (ResultCntl != null)
                    return ResultCntl;
            }
            return null;
        }

        //Function - Cloning sidebar panels
        public void PanelGraphCloner(int panelNo, DataTable TCTable)
        {
            Panel pnl = new Panel();
            pnl.Name = "graphPanel" + panelNo;
            pnl.Size = new Size(graphPanel001.Width, graphPanel001.Height);         
            //flowLayoutPanel1.Controls.SetChildIndex(pnl, 0);
            pnl.BackColor = System.Drawing.Color.Gainsboro;
            
            Label lbl = new Label();
            lbl.Name = "lbl" + panelNo;
            lbl.Text = "TC" + panelNo;
            lbl.Size = new Size(graphPanel001.Width, 15);
            lbl.Location = new Point(3, 3);
            lbl.ForeColor = System.Drawing.Color.Black;
            lbl.BackColor = System.Drawing.Color.Transparent;
            pnl.Controls.Add(lbl);

            Chart chart = new Chart();
            ChartArea ChartArea0 = new ChartArea("default");
            chart.ChartAreas.Add(ChartArea0);
            chart.Name = "chart" + panelNo;
            chart.Size = new Size(graphPanel001.Width-6, graphPanel001.Height-15);
            chart.Location = new Point(3,15);
            chart.DataSource = TCTable;
            chart.ChartAreas[0].AxisX.Interval = 10;
            chart.ChartAreas[0].AxisX.LabelStyle.Angle = -90;
            chart.ChartAreas[0].AxisY.IsStartedFromZero = false;
            chart.ChartAreas[0].AxisY.Interval = 5;
            chart.Series.Clear();
            for (int i = 1; i < TCTable.Columns.Count; i++)
            {
                if (TCTable.Columns[i].ColumnName == panelNo.ToString())
                {
                    Series series = new Series();
                    series.XValueMember = TCTable.Columns[2].ColumnName;
                    series.YValueMembers = TCTable.Columns[i].ColumnName;
                    series.ChartType = SeriesChartType.Line;
                    series.IsVisibleInLegend = true;
                    series.IsValueShownAsLabel = false;
                    series.BorderWidth = 3;
                    series.LegendText = TCTable.Columns[i].ColumnName;
                    chart.Series.Add(series);
                    chart.DataBind();
                }
                else
                {

                }
            }
            pnl.Controls.Add(chart);

            flowLayoutPanel1.Controls.Add(pnl);
        }

        //Color picker for temperature value between 0-200C
        public Color RainbowColor(double currTemp)
        {
            double minTemp = TCTList.Min();
            double maxTemp = TCTList.Max();

            //mapping to 0-1000
            int currTempMaped = Convert.ToInt32(((currTemp - minTemp) / (maxTemp - minTemp)) * 1000);
            
            if (currTempMaped <=500)
            {
                int r = 0;
                int g = Convert.ToInt32((currTempMaped / 500.0) * 255);
                int b = Convert.ToInt32(((500 - currTempMaped) /500.0) * 255);
                return Color.FromArgb(255, r, g, b);
            }
            else if (currTempMaped <= 1000)
            {
                int r = Convert.ToInt32(((currTempMaped - 500 )/ 500.0) * 255);
                int g = Convert.ToInt32(((1000 - currTempMaped)/ 500.0) * 255);
                int b = 0;
                return Color.FromArgb(255, r, g, b);
            }
            else
            {
                return Color.FromArgb(255, 255, 255, 255);
            }

        }

        //CSV file database
        public void StoreData(string csvPath, String TCTime, String TCData)
        {
            string dataString = csvId + "," + DateTime.Now.ToString("yyyyMMddTHHmmss") + "," + TCTime + "," + TCData;

            List<string> result = dataString.Split(',').ToList();
            TCTable.Rows.Add(result.ToArray());

            StreamWriter sw = new StreamWriter(csvPath, true);
            sw.Write(dataString);
            //sw.Write(sw.NewLine);
            sw.Close();
            csvId += 1;
        }

        //CSV file get data
        public DataTable GetData(string csvPath)
        {
            DataTable dt = new DataTable();
            using (StreamReader sr = new StreamReader(csvPath))
            {
                string[] headers = sr.ReadLine().Split(',');
                foreach (string header in headers)
                {
                    dt.Columns.Add(header);
                }
                while (!sr.EndOfStream)
                {
                    string[] rows = sr.ReadLine().Split(',');
                    DataRow dr = dt.NewRow();
                    for (int i = 0; i < headers.Length; i++)
                    {
                        dr[i] = rows[i];
                    }
                    dt.Rows.Add(dr);
                }
                sr.Close();
            }
            return dt;
        }

        //btn connect to tc wafer
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (connectedStatus != true)
                {
                    string[] coms = SerialPort.GetPortNames();
                    foreach (var com in coms)
                    {
                        SerialPort port = new SerialPort(com, 9600, Parity.None, 8, StopBits.One);
                        port.ReadTimeout = 500;
                        port.WriteTimeout = 500;
                        port.Open();
                        port.WriteLine("Connect");
                        string message = port.ReadLine();
                        port.Close();
                        List<string> dataList = message.Split('|').ToList();
                        if (dataList[0] == "Connected")
                        {
                            connectedStatus = true;
                            connectedPort = com;
                            btnConnect.Text = "Disconnect from TC wafer";
                            label7.Text = "Connection Status: Connected";
                            //label2.Text = dataList[1];
                            //TCList = dataList[2].Split('#').Select(int.Parse).ToList();
                            TCTList = dataList[2].Split(',').Select(double.Parse).ToList();
                            if (TCTList.Count == 8)
                            {
                                String XMLPath = Environment.CurrentDirectory + "/Config.xml";
                                XmlDocument xml = new XmlDocument();
                                xml.Load(XMLPath);
                                if (xml.SelectNodes("/TCWaferAnalyzer/TC8_V1") != null)
                                {
                                    XmlNodeList xnList = xml.SelectNodes("/TCWaferAnalyzer/TC8_V1");
                                    label2.Text = xnList[0]["Version"].InnerText;
                                    List<string> TCData = xnList[0]["TC"].InnerText.Split('#').ToList();
                                    TCData.RemoveAt(0);
                                    foreach (var item in TCData)
                                    {
                                        List<int> TCTemp = item.Split(',').Select(int.Parse).ToList();
                                        TCList.Add(TCTemp[0]);
                                        TCXList.Add(TCTemp[1]);
                                        TCYList.Add(TCTemp[2]);
                                    }
                                }
                            }
                            if (TCTList.Count == 2)
                            {
                                String XMLPath = Environment.CurrentDirectory + "/Config.xml";
                                XmlDocument xml = new XmlDocument();
                                xml.Load(XMLPath);
                                if (xml.SelectNodes("/TCWaferAnalyzer/TC2_V1") != null)
                                {
                                    XmlNodeList xnList = xml.SelectNodes("/TCWaferAnalyzer/TC2_V1");
                                    label2.Text = xnList[0]["Version"].InnerText;
                                    List<string> TCData = xnList[0]["TC"].InnerText.Split('#').ToList();
                                    TCData.RemoveAt(0);
                                    foreach (var item in TCData)
                                    {
                                        List<int> TCTemp = item.Split(',').Select(int.Parse).ToList();
                                        TCList.Add(TCTemp[0]);
                                        TCXList.Add(TCTemp[1]);
                                        TCYList.Add(TCTemp[2]);
                                    }
                                }
                            }
                            WaferInitilizer();
                            btnStartRecord.Enabled = true;
                            break;
                        }
                    }
                }
                else
                {
                    if (startPort.PortName != null)
                    {
                        startPort.Close();
                    }
                    connectedStatus = false;
                    connectedPort = null;
                    btnConnect.Text = "Connect to TC wafer";
                    label7.Text = "Connection Status: Not Connected";
                    label2.Text = "TC Wafer Analyzer";
                    TCList.Clear();
                    WaferInitilizer();
                    btnStartRecord.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                this.Close();
            }
            
        }

        //btn start recording data from arduino 
        private void btnStartRecord_Click(object sender, EventArgs e)
        {
            try
            {
                csvPath = Environment.CurrentDirectory + "/Data/" + DateTime.Now.ToString("yyyyMMddTHHmmss") + ".csv";
                csvId = 1;
                StreamWriter sw = new StreamWriter(csvPath, true);
                string dataString = "ID" + "," + "DateTime" + "," + "MilliSeconds" + "," + string.Join(",", TCList.ToArray());
                sw.Write(dataString);
                sw.Write(sw.NewLine);
                sw.Close();

                TCTable = new DataTable();
                TCTable.Columns.Add("ID");
                TCTable.Columns.Add("DateTime");
                TCTable.Columns.Add("MiliSeconds");
                foreach (var TC in TCList)
                {
                    TCTable.Columns.Add(TC.ToString());
                }

                startPort = new SerialPort(connectedPort, 9600, Parity.None, 8, StopBits.One);
                startPort.ReadTimeout = 10000;
                startPort.WriteTimeout = 500;
                startPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                startPort.Open();
                startPort.WriteLine("Start");
            }
            catch (Exception ex)
            {
                this.Close();
            }
        }

        //seperate serial port to listen and store data
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort port = (SerialPort)sender;
            string message = port.ReadLine();
            List<string> dataList = message.Split('|').ToList();
            if (dataList[0] == "TCData")
            {
                StoreData(csvPath, dataList[1], dataList[2]);
                TCTable = GetData(csvPath);

                trackBar1.Invoke(new MethodInvoker(delegate {
                    trackBar1.Maximum = int.Parse(dataList[1]) / 1000;
                    trackBar1.Value = int.Parse(dataList[1]) / 1000;
                }));
                label11.Invoke(new MethodInvoker(delegate {
                    label11.Text = "Time : " + trackBar1.Value + " s";
                }));

                TCTList = dataList[2].Split(',').Select(double.Parse).ToList();
                if (Int32.Parse(dataList[1]) % 1000 == 0)
                {
                    WaferRefresh();
                }               
            }
        }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            //if (csvId % 10 == 0)
            //{
            //    BackgroundWorker bw = new BackgroundWorker();
            //    bw.DoWork += new DoWorkEventHandler(bw_DoWork);
            //    bw.RunWorkerAsync();
            //}

            this.Invoke((MethodInvoker)delegate
            {
                
            });
        }

        //Time slider refresher
        private void trackBar1_MouseUp(object sender, MouseEventArgs e)
        {
            label11.Text = "Time : " + trackBar1.Value + " s";
            int msTime = trackBar1.Value * 1000;

            DataRow foundRow = TCTable.Select(("MilliSeconds=\'" + (msTime + "\'")))[0];
            //int tempIndex = TCTable.Rows.IndexOf(foundRow);\
            if (foundRow != null)
            {
                TCTList.Clear();
                for (int i = 3; i < TCTable.Columns.Count; i++)
                {
                    double TValue = double.Parse(foundRow.ItemArray[i].ToString());
                    TCTList.Add(TValue);
                }
                WaferRefresh();
            }
        }
    }
}
