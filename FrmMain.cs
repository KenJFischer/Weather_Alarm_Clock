using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Net;
using System.Xml;
using System.IO;
using System.Globalization;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;

// Credit to http://csharphelper.com/blog/2018/07/get-a-weather-forecast-from-openweathermap-org-in-c/ for the base weather app
// Credit to https://www.c-sharpcorner.com/blogs/alarm-clock-in-c-sharp for the base alarm clock
// Voice by Google Cloud Shell
// Created by Kenneth Fischer on March 24, 2021

namespace howto_weather_forecast2
{
    public partial class FrmMain : Form
    {
        public FrmMain()
        {
            InitializeComponent();
        }

        private bool isPlaying = false;
        private bool advisoryJacket;
        private bool advisoryUmbrella;
        private SoundPlayer name = new SoundPlayer();
        private System.Timers.Timer timer;
        private string APP_PATH = Application.StartupPath.ToString() + $@"\res\";
        private double avgTemp;
        private int highTemp;
        private int lowTemp;
        private double avgChance;
        private double highChance;
        private double lowChance;
        private double avgCloud;
        private int[] precipTypeFreq = new int[3]; // { occurences of rain, occurences of snow, occurences of other }
        // Get an API key by making a free account at: http://home.openweathermap.org/users/sign_in
        // Place the API key in the res\api.txt file
        private string API_KEY;
        // Query URLs. Replace @LOC@ with the location.
        private string CurrentUrl;
        private string ForecastUrl;
        private const string imgURL1 = "http://openweathermap.org/img/wn/";
        private const string imgURL2 = "@2x.png";
        // Query codes.
        private string[] QueryCodes = { "q", "zip", "id", };

        
        private void Form1_Load(object sender, EventArgs e)
        {
            // Initialize strings
            API_KEY = File.ReadAllText(APP_PATH + "api.txt");
            txtLocation.Text = File.ReadAllText(APP_PATH.Substring(0, APP_PATH.LastIndexOf("res")) + "Default ZIP Code.txt");
            CurrentUrl = "http://api.openweathermap.org/data/2.5/weather?" + "@QUERY@=@LOC@&mode=xml&units=imperial&APPID=" + API_KEY;
            ForecastUrl = "http://api.openweathermap.org/data/2.5/forecast?" + "@QUERY@=@LOC@&mode=xml&units=imperial&APPID=" + API_KEY;

            // Set up alarm timer
            timer = new System.Timers.Timer();
            timer.Interval = 1000;
            timer.Elapsed += timerElapsed;

            // Set time in alarm datetimepicker
            DateTime nowRounded = DateTime.Now;
            nowRounded = nowRounded.AddSeconds(-nowRounded.Second); // Set seconds to 00
            dtpAlarm.Value = nowRounded;

            btnForecast.PerformClick();
            chkSound.Checked = true;
        }



        /************** Set up timer ***********/
        private void timerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            DateTime currentTime = DateTime.Now;
            DateTime alarmTime = dtpAlarm.Value;

            if (currentTime.Hour == alarmTime.Hour && currentTime.Minute == alarmTime.Minute && currentTime.Second == alarmTime.Second)
            {
                timer.Stop();
               
                updateLabel update = updateDataLabel;
                if (lblAlarm.InvokeRequired)
                {
                    Invoke(update, lblAlarm, "Alarm Triggered!");
                }
                SetMonitorState(MonitorState.ON);
                playAlarmSound();
            }
        }

        private void btnAlarm_Click(object sender, EventArgs e)
        {
            timer.Start();
            string time = dtpAlarm.Value.TimeOfDay.ToString();
            if ( time.LastIndexOf('.') >= 0 )
            {
                time = time.Substring(0, time.LastIndexOf('.')); // Truncate the milliseconds
            }
            lblAlarm.Text = "Alarm set for " + time + ".";  
        }




        /************************         Play Voice         ************************/

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(Environment.ExitCode); // Kill all running threads on exit (stop any playing sounds)
        }

        /****** Thread Starters *******/
        private void playAlarmSound()
        {
            Thread t = new Thread(new ThreadStart(playAlarmSoundThreadJoiner));
            t.IsBackground = true;
            t.Start();
        }

        private void playForecast()
        {
            object[] paths = new object[5];
            double[] chances = new double[5];
            for (int row = 0; row < 5; row++)
            {
                paths[row] = getForecastResources(row);
                double.TryParse(lvwForecast.Items[row].SubItems[3].Text, out chances[row]);
            }
            var t = new Thread(() => playForecastThreadJoiner(paths, chances));
            t.IsBackground = true;
            t.Start();
        }

        private void playAdvisory()
        {
            Thread t = new Thread(new ThreadStart(playAdvisoryThreadJoiner));
            t.IsBackground = true;
            t.Start();
        }

        /********* Thread Joiners *********/
        // Use joiners so the main thread is not waiting (the joiner thread waits instead)
        private void playAlarmSoundThreadJoiner() 
        {
            Thread t = new Thread(new ThreadStart(playAlarmSoundThreadHelper));
            t.IsBackground = true;
            t.Start();
            t.Join();
            t.Abort();
            getForecast forecast = getForecastData;
            Invoke(forecast);
        }

        private void playForecastThreadJoiner(object[] paths, double[] chances)
        {
            Thread introThread = new Thread(new ThreadStart(playForecastIntro));
            introThread.IsBackground = true;
            introThread.Start();
            introThread.Join();
            introThread.Abort();

            // Initialize static soundplayers
            SoundPlayer at = new SoundPlayer();
            SoundPlayer tempIntro = new SoundPlayer();
            SoundPlayer cloudIntro = new SoundPlayer();
            SoundPlayer precipIntro = new SoundPlayer();
            SoundPlayer chanceOf = new SoundPlayer();

            // Set resources for static soundplayers
            at.SoundLocation = APP_PATH + "at.wav";
            tempIntro.SoundLocation = APP_PATH + "weatherTempIntro.wav";
            cloudIntro.SoundLocation = APP_PATH + "weatherCloudIntro.wav";
            precipIntro.SoundLocation = APP_PATH + "weatherPrecipIntro.wav";
            chanceOf.SoundLocation = APP_PATH + "chanceOf.wav";

            for (int row = 0; row < 5; row++)
            {
                string[] strPaths = (string[])paths[row];
                var t = new Thread(() => playForecastThreadHelper(strPaths, at, tempIntro, cloudIntro, precipIntro, chanceOf, chances[row]));
                t.IsBackground = true;
                t.Start();
                t.Join();
                t.Abort();
            }

            Thread outroThread = new Thread(new ThreadStart(playForecastOutro));
            outroThread.IsBackground = true;
            outroThread.Start();
            outroThread.Join();
            outroThread.Abort();
        }

        private void playAdvisoryThreadJoiner()
        {
            Thread t = new Thread(new ThreadStart(playAdvisoryThreadHelper));
            t.IsBackground = true;
            t.Start();
            t.Join();
            t.Abort();
        }

        /******* Threads that play sound **********/
        private void playAlarmSoundThreadHelper()
        {
            // Assign resources to soundplayers
            SoundPlayer alarm = new SoundPlayer();
            SoundPlayer goodTime = new SoundPlayer();
            SoundPlayer greeting = new SoundPlayer();
            SoundPlayer jingle = new SoundPlayer();

            // Determine what sounds to play
            string[] paths = getGoodTimePath(DateTime.Now.Hour);
            goodTime.SoundLocation = paths[0];
            greeting.SoundLocation = paths[1];
            alarm.SoundLocation = APP_PATH + "alarm.wav";
            jingle.SoundLocation = APP_PATH + "jingle0.wav";

            alarm.PlaySync(); // play alarm
            jingle.PlaySync(); // Play opening jingle
            goodTime.PlaySync(); // play "Good Morning/afternoon/etc.
            greeting.PlaySync(); // Play time-based greeting
        }

        private void playForecastThreadHelper(string[] paths, SoundPlayer at, SoundPlayer tempIntro,
                SoundPlayer cloudIntro, SoundPlayer precipIntro, SoundPlayer chanceOf, double chance)
        {
            SoundPlayer day = new SoundPlayer();
            SoundPlayer hour = new SoundPlayer();
            SoundPlayer AMPM = new SoundPlayer();          
            SoundPlayer tempQuality = new SoundPlayer();            
            SoundPlayer cloudQuality = new SoundPlayer();            
            SoundPlayer precipQuality = new SoundPlayer();            
            SoundPlayer precipType = new SoundPlayer();
            
            // Set resources for dynamic soundplayers
            day.SoundLocation = paths[0];
            hour.SoundLocation = paths[1];
            AMPM.SoundLocation = paths[2];
            tempQuality.SoundLocation = paths[3];
            cloudQuality.SoundLocation = paths[4];
            precipQuality.SoundLocation = paths[5];
            precipType.SoundLocation = paths[6];

            // Play all soundplayers to create forecast
            at.PlaySync();
            hour.PlaySync();
            AMPM.PlaySync();
            day.PlaySync();
            tempIntro.PlaySync();
            tempQuality.PlaySync();
            cloudIntro.PlaySync();
            cloudQuality.PlaySync();

            // Don't play precipitation if chance == 0
            if (chance > 0)
            {
                precipIntro.PlaySync();
                precipQuality.PlaySync();
                chanceOf.PlaySync();
                precipType.PlaySync();
            }
        }

        private void playForecastIntro()
        {
            SoundPlayer intro = new SoundPlayer();
            intro.SoundLocation = APP_PATH + "weatherIntro.wav";
            intro.PlaySync();
        }

        private void playForecastOutro()
        {
            SoundPlayer outro = new SoundPlayer();
            SoundPlayer jingle1 = new SoundPlayer();
            outro.SoundLocation = APP_PATH + "weatherOutro.wav";
            jingle1.SoundLocation = APP_PATH + "jingle1.wav";

            playAdvisory();
            outro.PlaySync();
            jingle1.PlaySync();
            isPlaying = false;
        }

        private void playAdvisoryThreadHelper()
        {
            SoundPlayer jacket = new SoundPlayer();
            SoundPlayer umbrella = new SoundPlayer();
            if (advisoryJacket)
            {
                jacket.SoundLocation = APP_PATH + "advisoryJacket.wav";
                jacket.PlaySync();
            }
            if (advisoryUmbrella)
            {
                umbrella.SoundLocation = APP_PATH + "advisoryUmbrella.wav";
                umbrella.PlaySync();
            }
        }






        /***************************         Get data for voice          ********************************/
        private string[] getGoodTimePath(int hour)
        {
            // Select good [morning/afternoon/evening/night] and appropriate greeting based on the time
            string[] paths = new string[2];
            if (hour < 4)
            {
                paths[0] = APP_PATH + "goodNight.wav";
                paths[1] = APP_PATH + "greetingNight.wav";
            }
            else if (hour < 12)
            {
                paths[0] = APP_PATH + "goodMorning.wav";
                aths[1] = APP_PATH + "greetingMorning.wav";
            }
            else if (hour < 17)
            {
                paths[0] = APP_PATH + "goodAfternoon.wav";
                paths[1] = APP_PATH + "greetingAfternoon.wav";
            }
            else if (hour < 22)
            {
                paths[0] = APP_PATH + "goodEvening.wav";
                paths[1] = APP_PATH + "greetingEvening.wav";
            }
            else
            {
                paths[0] = APP_PATH + "goodNight.wav";
                paths[1] = APP_PATH + "greetingNight.wav";
            }
            return paths;
        }

        private string[] getForecastResources(int row)
        {
            // Get string paths of all audio files for the first 5 rows (12 hours) of the forecast
            string[] paths = new string[7];

            // Get day
            paths[0] = APP_PATH + lvwForecast.Items[row].SubItems[0].Text.ToLower() + ".wav";
            // Get hour
            paths[1] = APP_PATH + lvwForecast.Items[row].SubItems[1].Text.Substring(0, lvwForecast.Items[row].SubItems[1].Text.IndexOf(":")) + ".wav";
            // Get AMPM
            paths[2] = APP_PATH + lvwForecast.Items[row].SubItems[1].Text.Substring(lvwForecast.Items[row].SubItems[1].Text.IndexOf(" ") + 1).ToLower() + ".wav";
            // Get temp quality
            paths[3] = APP_PATH + getTempQuality(row, 2) + ".wav";
            // Get cloud quality
            paths[4] = APP_PATH + getCloudQuality(row, 5) + ".wav";
            // Get precip quality
            paths[5] = APP_PATH + getPrecipQuality(row, 3) + ".wav";
            // Get precip type
            string type = lvwForecast.Items[row].SubItems[4].Text.ToLower();
            if (type == "none") { type = "rain"; }
            paths[6] = APP_PATH + type + ".wav";

            return paths;
        }

        private string getCloudQuality(int row, int col)
        {
            string[] qualities = { "clear", "partlyCloudy", "mostlyCloudy", "overcast" };
            int quality;
            double cover;
            // Get just the number without the percent sign
            double.TryParse(lvwForecast.Items[row].SubItems[col].Text.Substring(0, lvwForecast.Items[row].SubItems[col].Text.Length - 1), out cover);

            // Determine what quality the precip is
            if (cover < 0.15) { quality = 0; }
            else if (cover < 0.50) { quality = 1; }
            else if (cover < 0.80) { quality = 2; }
            else { quality = 3; }

            return qualities[quality];
        }

        private string getPrecipQuality(int row, int col)
        {
            string[] qualities = { "slight", "low", "medium", "high", "veryHigh" };
            int quality;
            double chance;
            double.TryParse(lvwForecast.Items[row].SubItems[col].Text, out chance);

            // Determine what quality the precip is
            if (chance < 0.15) { quality = 0; }
            else if (chance < 0.25) { quality = 1; }
            else if (chance < 0.50) { quality = 2; }
            else if (chance < 0.75) { quality = 3; }
            else { quality = 4; }

            return qualities[quality];
        }

        private string getTempQuality(int row, int col)
        {
            string[] qualities = { "frigid", "cold", "cool", "mild", "warm", "hot", "scorching" };
            int quality;
            int temp;
            int.TryParse(lvwForecast.Items[row].SubItems[col].Text.Substring(0, lvwForecast.Items[row].SubItems[col].Text.Length - 1), out temp); // Get just the number without the degree symbol

            // Determine what quality the temp is
            if (temp < 20) { quality = 0; }
            else if (temp < 40) { quality = 1; }
            else if (temp < 60) { quality = 2; }
            else if (temp < 75) { quality = 3; }
            else if (temp < 85) { quality = 4; }
            else if (temp < 100) { quality = 5; }
            else { quality = 6; }

            return qualities[quality];
        }






        /**************************    Get 12 hour weather data    ******************************/
        private double getAvgTemp()
        {
            double avg = 0;
            int temp;
            int lowest = 300; // High initial value ensures lowest temp is set properly
            int highest = 0;

            for (int row = 0; row < 5; row++)
            {
                // Get just the number without the degree symbol
                int.TryParse(lvwForecast.Items[row].SubItems[2].Text.Substring(0, lvwForecast.Items[row].SubItems[2].Text.Length - 1), out temp);
                avg += temp;
                if (temp < lowest) { lowest = temp; }
                if (temp > highest) { highest = temp; }
            }

            lowTemp = lowest;
            highTemp = highest;
            return avg / 5;
        }

        private double getAvgChance()
        {
            double avg = 0;
            double chance;
            double lowest = 300; // High initial value ensures lowest temp is set properly
            double highest = 0;

            for (int row = 0; row < 5; row++)
            {
                double.TryParse(lvwForecast.Items[row].SubItems[3].Text, out chance);
                avg += chance;
                if (chance < lowest) { lowest = chance; }
                if (chance > highest) { highest = chance; }
            }

            lowChance = lowest;
            highChance = highest;
            return avg / 5;
        }

        private double getAvgCloud()
        {
            double avg = 0;
            double cover;

            for (int row = 0; row < 5; row++)
            {
                // Get just the number without the percent sign
                double.TryParse(lvwForecast.Items[row].SubItems[5].Text.Substring(0, lvwForecast.Items[row].SubItems[5].Text.Length - 1), out cover);
                avg += cover;
            }

            return avg / 5;
        }

        private int[] getPrecipTypeFreq()
        {
            // Gets the frequency of each type of precip. E.g. if it rains twice and snows three times in 12 hours, freqs will hold {2,3}
            int[] freqs = new int[2];
            string type;
            freqs[0] = 0;
            freqs[1] = 0;
            freqs[2] = 0;

            for (int row = 0; row < 5; row++)
            {
                type = lvwForecast.Items[row].SubItems[4].Text.ToLower();
                if (type == "rain") { freqs[0]++; }
                else if (type == "snow") { freqs[1]++; }
                else { freqs[2]++; } // type is other
            }

            return freqs;
        }

        private void setWeatherData()
        {
            // Sets the temperature data
            lblTempData.Text = "Low Temp:  " + lowTemp + "\nHigh Temp:  " + highTemp + "\nAverage Temp: " + Math.Round(avgTemp)
                + "\nAvg. Chance: " + avgChance + "\nAvg. Cloud Cover: " + avgCloud + "%";
            advisoryJacket = false;
            advisoryUmbrella = false;
            if (avgTemp <= 55 || lowTemp <= 40) { advisoryJacket = true; }
            if (avgChance >= 0.35 || highChance >= 0.7) { advisoryUmbrella = true; }
        }








        /*********************************************       FORECAST RETRIEVAL BELOW       *********************************************/

        // Get a forecast.
        private void btnForecast_Click(object sender, EventArgs e)
        { 
            // Compose the query URL.
            string url = ForecastUrl.Replace("@LOC@", txtLocation.Text);
            url = url.Replace("@QUERY@", QueryCodes[1]); // 1 for ZIP

            // Create a web client.
            using (WebClient client = new WebClient())
            {
                // Get the response string from the URL.
                try
                {
                    DisplayForecast(client.DownloadString(url));
                }
                catch (WebException ex)
                {
                    DisplayError(ex);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unknown error\n" + ex.Message);
                }
            }
        }

        // Display the forecast.
        private void DisplayForecast(string xml)
        {
            // Load the response into an XML document.
            XmlDocument xml_doc = new XmlDocument();
            xml_doc.LoadXml(xml);

            // Get the city, country, latitude, and longitude.
            XmlNode loc_node = xml_doc.SelectSingleNode("weatherdata/location");
            XmlNode geo_node = loc_node.SelectSingleNode("location");

            lvwForecast.Items.Clear();
            char degrees = (char)176;
            int numRows = 0;
            
            foreach (XmlNode time_node in xml_doc.SelectNodes("//time"))
            {
                // Get the time in UTC.
                DateTime time = DateTime.Parse(time_node.Attributes["from"].Value, null, DateTimeStyles.AssumeUniversal);

                // Get the temperature.
                XmlNode temp_node = time_node.SelectSingleNode("temperature");
                string temp = temp_node.Attributes["value"].Value;
                double dblTemp;
                double.TryParse(temp, out dblTemp);
                dblTemp = Math.Round(dblTemp);
                temp = dblTemp.ToString();

                // Get the precip chance and type
                XmlNode precip_node = time_node.SelectSingleNode("precipitation");
                string chance = precip_node.Attributes["probability"].Value;
                string type = "none";
                if (precip_node.Attributes["type"] != null)
                {
                    type = precip_node.Attributes["type"].Value;
                }

                // Get the cloud cover
                XmlNode cloud_node = time_node.SelectSingleNode("clouds");
                string cover = cloud_node.Attributes["all"].Value + "%";

                // Get the condition
                XmlNode symbol_node = time_node.SelectSingleNode("symbol");
                string symbol = symbol_node.Attributes["name"].Value;

                // Put everything into the listview
                ListViewItem item = lvwForecast.Items.Add(time.DayOfWeek.ToString());
                item.SubItems.Add(time.ToShortTimeString());
                item.SubItems.Add(temp + degrees);
                item.SubItems.Add(chance);
                item.SubItems.Add(type);
                item.SubItems.Add(cover);
                item.SubItems.Add(symbol);

                // Gets image for the weather
                if (numRows == 0)
                {
                    string imgID = symbol_node.Attributes["var"].Value;
                    imgWeather.ImageLocation = imgURL1 + imgID + imgURL2;
                }

                numRows++;
            }

            // Get 12 hour weather data
            avgTemp = getAvgTemp();
            avgChance = getAvgChance();
            precipTypeFreq = getPrecipTypeFreq();
            avgCloud = getAvgCloud();
            setWeatherData();
            // Play audio forecast
            if (chkSound.Checked && !isPlaying ) 
            {
                isPlaying = true;
                playForecast();
            }
        }

        // Display an error message.
        private void DisplayError(WebException exception)
        {
            try
            {
                StreamReader reader = new StreamReader(exception.Response.GetResponseStream());
                XmlDocument response_doc = new XmlDocument();
                response_doc.LoadXml(reader.ReadToEnd());
                XmlNode message_node = response_doc.SelectSingleNode("//message");
                MessageBox.Show(message_node.InnerText);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unknown error\n" + ex.Message);
            }
        }






        /*****************    Delegate functs.    *********************/
        delegate void updateLabel(Label lbl, string msg);
        void updateDataLabel(Label lbl, string msg)
        {
            lbl.Text = msg;
        }

        delegate void getForecast();
        void getForecastData()
        {
            isPlaying = false;
            btnForecast.PerformClick();
            isPlaying = true;
        }

        delegate string getData(int row, int col);
        string getLVWData(int row, int col)
        {
            return lvwForecast.Items[row].SubItems[col].Text;
        }

        // Turn on/off the monitor with the alarm
        private void button2_Click(object sender, EventArgs e)
        {
            SetMonitorState(MonitorState.OFF);
        }
        private int SC_MONITORPOWER = 0xF170;
        [DllImport("user32.dll")]
        static extern void mouse_event(Int32 dwFlags, Int32 dx, Int32 dy, Int32 dwData, UIntPtr dwExtraInfo);
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        private uint WM_SYSCOMMAND = 0x0112;
        private const int MOUSEEVENTF_MOVE = 0x0001;
        enum MonitorState
        {
            ON = -1,
            OFF = 2,
            STANDBY = 1
        }
        private void SetMonitorState(MonitorState state)
        {
            Form frm = new Form();
            SendMessage(frm.Handle, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)state);
            for (int i = 0; i < 10; i++)
            {
                // Shake the mouse in case the monitor needs mouse input to wake
                mouse_event(MOUSEEVENTF_MOVE, 0, 5, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_MOVE, 0, -5, 0, UIntPtr.Zero);
            }
            if (state == MonitorState.ON)
            {
                // Give time for the monitor to come on before starting the alarm
                Thread.Sleep(3000);
            }
        }
    }
}
