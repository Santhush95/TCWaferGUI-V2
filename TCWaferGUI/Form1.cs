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

namespace TCWaferGUI
{
    public partial class Form1 : Form
    {
        //global variables
        private bool connectedStatus { get; set; }
        private String connectedPort { get; set; }
        private List<int> TCList { get; set; }
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
                TCList = new List<int>();
                TCTable = new DataTable();
                startPort = new SerialPort();
                WaferAvailable();
                //TCAvailable();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                this.Close();
            }
            
        }

        //function - Waffer initializer
        private void WaferAvailable()
        {
            List<int> TCXList = new List<int> { 150 , 300 , 300, 150, 160, 450 };
            List<int> TCYList = new List<int> { 150, 300, 500, 150, 160, 450 };
            List<int> TCTList = new List<int> { 0, 5, 10, 20, 25, 145 };

            Bitmap BMP = new Bitmap(600, 600);
            Graphics GFX = Graphics.FromImage(BMP);
            GFX.FillEllipse(Brushes.Black, 0,0, 600, 600);
            Bitmap BMP2 = new Bitmap(600, 600);
            Graphics GFX2 = Graphics.FromImage(BMP2);
            GFX2.FillEllipse(Brushes.Black, 0, 0, 600, 600);

            WaferMap.Image = BMP;
            WaferMap.BackgroundImage = BMP2;

            for (int i = 0; i < TCXList.Count; i++)
            {
                Color TCColor = RainbowColor(TCTList[i]);
                BMP.SetPixel(TCXList[i], TCYList[i], TCColor);
            }

            for (int x = 0; x < 600; x++)
            {
                for (int y = 0; y < 600; y++)
                {
                    List<int> TCDistList = new List<int>();
                    List<int> TCDistIndexList = new List<int>();
                    Dictionary<int, int> TCDistDic = new Dictionary<int, int>();
                    double TCCenterTempclosest = 0;
                    double TCCenterTemp = 0;

                    bool isTC = false;

                    for (int i = 0; i < TCXList.Count; i++)
                    {
                        if (x == TCXList[i] && y == TCYList[i])
                        {
                            isTC = true;
                        }
                    }

                    if (BMP.GetPixel(x, y).ToArgb() != 0 && isTC == false)
                    {
                        for (int i = 0; i < TCXList.Count; i++)
                        {
                            int tempDist = (int)Math.Sqrt(Math.Pow((x- TCXList[i]), 2) + Math.Pow((y - TCYList[i]), 2));
                            TCDistDic.Add(i, tempDist);

                            double TCTTemp = (1 * (TCTList[i] * ((600 - tempDist) / 600.0)));

                            if (TCCenterTempclosest < TCTTemp)
                            {
                                TCCenterTempclosest = TCTTemp;
                            }
                            else
                            {
                                TCCenterTempclosest += (0.1 * (TCTList[i] * ((600 - tempDist) / 600.0)));
                            }
                        }

                        foreach (KeyValuePair<int, int> item in TCDistDic.OrderBy(key => key.Value))
                        {
                            TCDistIndexList.Add(item.Key);
                            TCDistList.Add(item.Value);
                        }

                        int TCDisDiv = 0;
                        for (int i = 0; i < TCDistList.Count; i++)
                        {
                            int TCDistMult = 1;
                            for (int j = 0; j < TCDistList.Count; j++)
                            {
                                if (i!=j)
                                {
                                    TCDistMult *= TCDistList[j];
                                }
                            }
                            TCDisDiv += TCDistMult;
                            TCCenterTemp += TCTList[TCDistIndexList[i]] * TCDistMult;
                        }
                        TCCenterTemp /= TCDisDiv * 1.0;


                        //TCCenterTemp  = TCTList[TCDistIndexList[0]] * TCDistList[1] * TCDistList[2] * TCDistList[3];
                        //TCCenterTemp  += TCTList[TCDistIndexList[1]] * TCDistList[0] * TCDistList[2] * TCDistList[3];
                        //TCCenterTemp  += TCTList[TCDistIndexList[2]] * TCDistList[0] * TCDistList[1] * TCDistList[3];
                        //TCCenterTemp  += TCTList[TCDistIndexList[3]] * TCDistList[0] * TCDistList[1] * TCDistList[2];
                        //TCCenterTemp  /= (TCDistList[0] * TCDistList[1] * TCDistList[3] + TCDistList[1] * TCDistList[2] * TCDistList[3] + TCDistList[0] * TCDistList[1] * TCDistList[2] + TCDistList[0] * TCDistList[2] * TCDistList[3]) * 1.0;


                        int TCCenterT = Convert.ToInt16(TCCenterTempclosest);
                        if (TCCenterT >= 0 && TCCenterT <= 200)
                        {
                            Color pixelColor = RainbowColor(TCCenterT);
                            BMP.SetPixel(x, y, pixelColor);
                        }
                        else if (TCCenterT > 200)
                        {
                            Color pixelColor = RainbowColor(200);
                            BMP.SetPixel(x, y, pixelColor);
                        }
                        else
                        {
                            Color pixelColor = RainbowColor(0);
                            BMP.SetPixel(x, y, pixelColor);
                        }
                    }      
                }                
            }

            for (int i = 0; i < TCXList.Count; i++)
            {
                GFX.DrawString("TC50", SystemFonts.DefaultFont, Brushes.Black, TCXList[i] + 2, TCYList[i]);
            }

            WaferMap.Image = BMP;
        }

        //Function - find selected TC and list them + dynamically add graph panels
        private void TCAvailable()
        {
            flowLayoutPanel1.Controls.Clear();

            if (TCList.Count > 0)
            {
                foreach (var ctrl in panel4.Controls)
                {
                    ((CheckBox)ctrl).Checked = false;
                    ((CheckBox)ctrl).BackColor = System.Drawing.Color.Transparent;
                    ((CheckBox)ctrl).ForeColor = Color.FromArgb(255, 180, 170, 150);
                    foreach (var TC in TCList)
                    {
                        string TCName = "cb" + TC;
                        if (((CheckBox)ctrl).Name == TCName)
                        {
                            ((CheckBox)ctrl).Checked = true;
                            ((CheckBox)ctrl).BackColor = System.Drawing.Color.White;
                            ((CheckBox)ctrl).ForeColor = System.Drawing.Color.Black;                            
                            break;
                        }
                    }
                }
                foreach (var TC in TCList)
                {
                    PanelGraphCloner(TC, TCTable);
                }

            }
            else
            {
                foreach (var ctrl in panel4.Controls)
                {
                    ((CheckBox)ctrl).Checked = false;
                    ((CheckBox)ctrl).BackColor = System.Drawing.Color.Transparent;
                    ((CheckBox)ctrl).ForeColor = Color.FromArgb(255, 180, 170, 150);
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
            chart.ChartAreas[0].AxisX.Interval = 1;
            chart.Series.Clear();
            for (int i = 1; i < TCTable.Columns.Count; i++)
            {
                if (TCTable.Columns[i].ColumnName == panelNo.ToString())
                {
                    Series series = new Series();
                    series.XValueMember = TCTable.Columns[0].ColumnName;
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

        //Function - Coloring refferenced checkbox by temperature value
        public void ColorfyCB(int TC, double currTemp)
        {
            Control currCB = GetControlByName(panel4, "cb" + (TC));
            Color CBColor = RainbowColor(currTemp);
            currCB.BackColor = Color.FromArgb(255, CBColor.R, CBColor.G, CBColor.B);
            currCB.Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold, Font.Unit, Font.GdiCharSet);

            List<int> TCSurround = TCdictionary(TC);

            foreach (int CBNo in TCSurround)
            {
                Control Checkbox = GetControlByName(panel4, "cb" + CBNo);
                Checkbox.BackColor = Color.FromArgb(100, CBColor.R, CBColor.G, CBColor.B);
            }
        }

        //List of surround checkbox to a given TC 
        public List<int> TCdictionary(int TC)
        {
            List<int> TCSurround = new List<int>();

            IDictionary<int, List<int>> TCDic = new Dictionary<int, List<int>>
            {
                { 1, new List<int>{2,7,6,5} },{ 2, new List<int>{1,6,7,8,3} },{ 3, new List<int>{2,7,8,9} },{ 4, new List<int>{11,12,13,5} },
                { 5, new List<int>{4,12,13,14,6,1} },{ 6, new List<int>{5,13,14,15,7,2,1} },{ 7, new List<int>{1,6,14,15,16,8,3,2} },{ 8, new List<int>{2,7,15,16,17,9,3} },
                { 9, new List<int>{3,8,16,17,18,10} },{ 10, new List<int>{9,17,18,19} },{ 11, new List<int>{20,21,22,12,4} },{ 12, new List<int>{11,21,22,23,13,5,4} },
                { 13, new List<int>{4,12,22,23,24,14,6,5} },{ 14, new List<int>{13,15,5,6,7,23,24,25} },{ 15, new List<int>{14,16,6,7,8,24,25,26} },{ 16, new List<int>{15,17,7,8,9,25,26,27} },
                { 17, new List<int>{8,16,26,27,28,18,10,9} },{ 18, new List<int>{17,19,9,10,27,28,29} },{ 19, new List<int>{18,10,28,29,30} },{ 20, new List<int>{21,11,31,32} },
                { 21, new List<int>{20,31,32,33,22,12,11} },{ 22, new List<int>{21,23,11,12,13,32,33,34} },{ 23, new List<int>{22,24,12,13,14,33,34,35} },{ 24, new List<int>{23,25,13,14,15,34,35,36} },
                { 25, new List<int>{24,26,14,15,16,35,36,37} },{ 26, new List<int>{25,27,15,16,17,36,37,38} },{ 27, new List<int>{26,28,16,17,18,37,38,39} },{ 28, new List<int>{27,29,17,18,19,38,39,40} },
                { 29, new List<int>{28,30,18,19,39,40,41} },{ 30, new List<int>{29,19,40,41} },{ 31, new List<int>{32,20,21,42,43,44} },{ 32, new List<int>{31,33,20,21,22,43,44,45} },
                { 33, new List<int>{32,34,21,22,23,44,45,46} },{ 34, new List<int>{33,35,22,23,24,45,46,47} },{ 35, new List<int>{34,36,23,24,25,46,47,48} },{ 36, new List<int>{35,37,24,25,26,47,48,49} },
                { 37, new List<int>{36,38,25,26,27,48,49,50} },{ 38, new List<int>{37,39,26,27,28,49,50,51} },{ 39, new List<int>{38,40,27,28,29,50,51,52} },{ 40, new List<int>{39,41,28,29,30,51,52,53} },
                { 41, new List<int>{40,29,30,52,53,54} },{ 42, new List<int>{43,31,55,56} },{ 43, new List<int>{42,44,31,32,55,56,57} },{ 44, new List<int>{43,45,31,32,33,56,57,58} },
                { 45, new List<int>{44,46,32,33,34,57,58,59} },{ 46, new List<int>{45,47,33,34,35,58,59,60} },{ 47, new List<int>{46,48,34,35,36,59,60,61} },{ 48, new List<int>{47,49,35,36,37,60,61,62} },
                { 49, new List<int>{48,50,36,37,38,61,62,63} },{ 50, new List<int>{49,51,37,38,39,62,63,64} },{ 51, new List<int>{50,52,38,39,40,63,64,65} },{ 52, new List<int>{51,53,39,40,41,64,65,66} },
                { 53, new List<int>{52,54,40,41,65,66,67} },{ 54, new List<int>{53,41,66,67} },{ 55, new List<int>{56,42,43,68,69} },{ 56, new List<int>{55,57,42,43,44,68,69,70} },
                { 57, new List<int>{56,58,43,44,45,69,70,71} },{ 58, new List<int>{57,59,44,45,46,70,71,72} },{ 59, new List<int>{58,60,45,46,47,71,72,73} },{ 60, new List<int>{59,61,46,47,48,72,73,74} },
                { 61, new List<int>{60,62,47,48,49,73,74,75} },{ 62, new List<int>{61,63,48,49,50,74,75,76} },{ 63, new List<int>{62,64,49,50,51,75,76,77} },{ 64, new List<int>{63,65,50,51,52,76,77,78} },
                { 65, new List<int>{64,66,51,52,53,77,78,79} },{ 66, new List<int>{65,67,52,53,54,78,79,80} },{ 67, new List<int>{66,53,54,79,80} },{ 68, new List<int>{69,55,56,81} },
                { 69, new List<int>{68,70,55,56,57,81,82} },{ 70, new List<int>{69,71,56,57,58,81,82,83} },{ 71, new List<int>{70,72,57,58,59,82,83,84} },{ 72, new List<int>{71,73,58,59,60,83,84,85} },
                { 73, new List<int>{72,74,59,60,61,84,85,86} },{ 74, new List<int>{73,75,60,61,62,85,86,87} },{ 75, new List<int>{74,76,61,62,63,86,87,88} },{ 76, new List<int>{75,77,62,63,64,87,88,89} },
                { 77, new List<int>{76,78,63,64,65,88,89,90} },{ 78, new List<int>{77,79,64,65,66,89,90,91} },{ 79, new List<int>{78,80,65,66,67,90,91} },{ 80, new List<int>{79,66,67,91} },
                { 81, new List<int>{82,68,69,70,92,93} },{ 82, new List<int>{81,83,69,70,71,92,93,94} },{ 83, new List<int>{} },{ 84, new List<int>{} },
                { 85, new List<int>{} },{ 86, new List<int>{85,87,73,74,75,96,97,98} },{ 87, new List<int>{} },{ 88, new List<int>{} },
                { 89, new List<int>{} },{ 90, new List<int>{} },{ 91, new List<int>{} },{ 92, new List<int>{} },
                { 93, new List<int>{} },{ 94, new List<int>{} },{ 95, new List<int>{} },{ 96, new List<int>{} },
                { 97, new List<int>{} },{ 98, new List<int>{} },{ 99, new List<int>{} },{ 100, new List<int>{} },
                { 101, new List<int>{} },{ 102, new List<int>{} },{ 103, new List<int>{} },{ 104, new List<int>{} },
                { 105, new List<int>{} },{ 106, new List<int>{} },{ 107, new List<int>{} },{ 108, new List<int>{} },
                { 109, new List<int>{} },{ 110, new List<int>{} },{ 111, new List<int>{} },{ 112, new List<int>{} },
                { 113, new List<int>{} },{ 114, new List<int>{} },{ 115, new List<int>{114,116,106,107,108,119,120,121} },{ 116, new List<int>{} },
                { 117, new List<int>{} },{ 118, new List<int>{} },{ 119, new List<int>{} },{ 120, new List<int>{} },
                { 121, new List<int>{} },{ 122, new List<int>{} },{ 123, new List<int>{} },{ 124, new List<int>{} },

            };

            return TCSurround = TCDic[TC];
        }

        //Color picker for temperature value between 0-200C
        public Color RainbowColor(double currTemp)
        {
            double minTemp = 0.0;
            double maxTemp = 200.0;
            double midTemp = (maxTemp + minTemp) / 2;
            double currTempD = currTemp;
            if (currTempD >= minTemp && currTempD <= midTemp)
            {
                int r = 0;
                int g = Convert.ToInt32((currTempD/midTemp) * 255);
                int b = Convert.ToInt32(((midTemp - currTemp)/midTemp) * 255);
                return Color.FromArgb(255, r, g, b);
            }
            else if (currTempD <= maxTemp)
            {
                int r = Convert.ToInt32(((currTempD - midTemp )/ midTemp) * 255);
                int g = Convert.ToInt32(((2*midTemp - currTempD)/midTemp) * 255);
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


            if (csvId % 10 == 0)
            {
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += new DoWorkEventHandler(bw_DoWork);
                bw.RunWorkerAsync();
            }
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
                            label2.Text = dataList[1];
                            TCList = dataList[2].Split(',').Select(int.Parse).ToList();
                            TCAvailable();
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
                    TCAvailable();
                    btnStartRecord.Enabled = false;
                }
            }
            catch (Exception ex)
            {

            }
            
        }

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

            }
        }


        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort port = (SerialPort)sender;
            string message = port.ReadLine();
            List<string> dataList = message.Split('|').ToList();
            if (dataList[0] == "TCData")
            {
                StoreData(csvPath, dataList[1], dataList[2]);

                List<double> TCData = dataList[2].Split(',').Select(double.Parse).ToList();
                DataTable TCTable = GetData(csvPath);
                for (int i = 0; i < TCList.Count; i++)
                {                   
                    ColorfyCB(TCList[i], TCData[i]);
                }
            }
        }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            this.Invoke((MethodInvoker)delegate
            {
                foreach (var TC in TCList)
                {
                    Control tempChart = GetControlByName(flowLayoutPanel1, "chart" + TC.ToString());
                    tempChart.Refresh();
                    label10.Text = TC.ToString();
                }
            });
        }

    }
}
