using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Configuration;

using Newtonsoft.Json;           
using Newtonsoft.Json.Linq;


namespace RiseApi
{
    public partial class fMain : Form
    {
        private DateTime fromDate = DateTime.Now; // 09/12/2021 15:38:09 PM  
        private Int16 addDays = 0;
        private DateTime ToDate;
        //private string paramMeetings = "";
        private const string URL = "https://api.rise-digital.com.au/v2/";
        string sCurrMeetsJs = Application.StartupPath + "\\currMeets.json";
        string sMeetings = "";
        string sState = "";
        dynamic dMeets;
        dynamic dRaces;
        dynamic dRunners;
        string RaceStartType = "";
        int StandingRow = 0;
        string previousMark = "";
        bool bNormalStatus = true;
        bool bStatusFound = false;

        string logFile = "";
        Boolean bAutoRun = false;
        Boolean bExitIfAutoRun = false;
        //dynamic currMeetings;
        //JObject currMeets;
        ActiveMeetings currMeets;

        string xmlFile = "";
        static UInt16 capacity = UInt16.MaxValue;
        StringBuilder xmlLines = new StringBuilder(capacity);

        ActiveMeetings activeMeetings = new ActiveMeetings();

        public class Meeting
        {
            public string meetingCode { get; set; }
            public string lastUpdate { get; set; }
        }


        public class ActiveMeetings
        {
            public ActiveMeetings()
            {
                Meeting = new List<Meeting>();
            }
            public List<Meeting> Meeting { get; set; }
        }
        public fMain()
        {
            InitializeComponent();
        }

        private void addNewMeeting(string meetingCode, string lastUpdate)
        {
            Meeting propertyActiveMeetings = new Meeting();
            propertyActiveMeetings.meetingCode = meetingCode;
            propertyActiveMeetings.lastUpdate = lastUpdate;

            activeMeetings.Meeting.Add(propertyActiveMeetings);
        }
        private void saveJsonMeets()
        {
            //string newMeets = "";
            string json = JsonConvert.SerializeObject(activeMeetings, Formatting.Indented);
            //JsonSerializer serializer = new JsonSerializer();
            //serializer.Serialize(jw, config);
            //write string to file
            File.WriteAllText(sCurrMeetsJs, json);
        }
        private void fMain_Load(object sender, EventArgs e)
        {
            startupRoutine();

        }
        private void startupRoutine()
        {
            addDays = Convert.ToInt16(numUD.Value);
            ToDate = fromDate.AddDays(addDays);
            txtFromDate.Text = fromDate.ToString("yyyy-MM-dd");
            txtToDate.Text = ToDate.ToString("yyyy-MM-dd");
            dTPFrom.CustomFormat = "yyyy-MM-dd";

            this.Text += " - " + Application.ProductVersion;

            txtFolder.Text = ConfigurationManager.AppSettings["XmlOutputFolder"];
            string sAutoRun = ConfigurationManager.AppSettings["AutoRun"];
            string sExitIfAutoRun = ConfigurationManager.AppSettings["ExitIfAutoRun"];

            bExitIfAutoRun = (sExitIfAutoRun == "Y");
            bAutoRun = (sAutoRun == "Y");
            chkAutoRun.Checked = bAutoRun;

            string logFolder = Application.StartupPath + "/Log";
            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }
            string filename = DateTime.Now.ToShortDateString().Replace("/", "-");
            logFile = logFolder + "/Log_" + filename + ".txt";

            if (File.Exists(sCurrMeetsJs))
            {
                //currMeets = JObject.Parse(File.ReadAllText(sCurrMeetsJs));
                string js = File.ReadAllText(sCurrMeetsJs);
                currMeets = JsonConvert.DeserializeObject<ActiveMeetings>(js);
            }
            else
            {
                currMeets = JsonConvert.DeserializeObject<ActiveMeetings>("{\"Meeting\": [] }");
                //currMeets = {"Meeting": [] };
            }
        }

        private void numUD_ValueChanged(object sender, EventArgs e)
        {
            calcToDate();
            

        }

        private void calcToDate()
        {
            addDays = Convert.ToInt16(numUD.Value);
            DateTime ToDate = fromDate.AddDays(addDays);
            txtToDate.Text = ToDate.ToString("yyyy-MM-dd");
        }
        private void button1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnGetMeets_Click(object sender, EventArgs e)
        {

            bNormalStatus = true;
            getMeets(); // 
            
        }

        private void getMeets()
        {
            Cursor.Current = Cursors.WaitCursor;
            lbStatus.Items.Clear();
            addInfo("Request Meetings." + Environment.NewLine);
            string paramMeetings = "";
            if (bNormalStatus)
            {
                paramMeetings = "meetings?earliest=" + txtFromDate.Text + "&latest=" + txtToDate.Text;
            }
            else
            {
                string currDate = DateTime.Now.ToString("yyyy-MM-dd");
                paramMeetings = "meetings?earliest=" + currDate + "&latest=" + currDate;
            }
            
            sMeetings = getFromURL(paramMeetings);

            if (sMeetings != "")
            {
                dMeets = JsonConvert.DeserializeObject<dynamic>(sMeetings);

                processMeetings(dMeets);
                saveJsonMeets();
                writeToFile();
                if (bAutoRun)
                {
                    if (bExitIfAutoRun)
                    {
                        Application.Exit();
                    }
                }
            }
            
            // Set cursor as default arrow
            Cursor.Current = Cursors.Default;
        }

        private void processMeetings(dynamic dMeetings)
        {
            string meetingStage;
            string track;
            string trial;
            string meetingCode="";
            string racesParams = "";
            string lastUpdate = "";
            Int16 count = 0;
            //Boolean stopNow = false;

            Int16 meetingsCount = calcMeetingFieldsAndNoTrial(dMeetings);
            for (Int16 i = 0; i < dMeetings.Count; i++)
            {
                meetingStage = dMeetings[i]["meetingStage"].ToString();
                track = dMeetings[i]["track"].ToString();
                trial = dMeetings[i]["trials"].ToString();
                meetingCode = dMeetings[i]["meetingCode"].ToString();
                lastUpdate = dMeetings[i]["lastUpdatedTime"].ToString();
                //Debug.WriteLine(track + " - " + meetingStage + " - " + trial + " - " + meetingCode);
                //if (meetings[i]["meetingStage"].ToString() != "PARTIAL_RESULTS")
                //{
                //    string abc = meetings[i]["meetingStage"].ToString();
                //    Debug.WriteLine(abc);
                //}
                
                //if (meetingStage  == "FIELDS" && trial =="FALSE" )
                if (IsStatusMet(meetingStage, trial))
                {
                    count++;
                    //addNewMeeting( meetingCode, lastUpdate);
                    Debug.WriteLine(activeMeetings);

                    if (IsSamelastUpdate(meetingCode,lastUpdate) )
                    {
                        addInfo("XML Meeting " + meetingCode + " (" + count.ToString() + "/" + meetingsCount.ToString() + ") - no changes!" + Environment.NewLine);
                    }
                    else
                    {
                        addInfo("XML Meeting " + meetingCode + " (" + count.ToString() + "/" + meetingsCount.ToString() + ")" + Environment.NewLine);

                        xmlMeet(dMeetings[i]);
                        //get races "meetings/AP110921/races"
                        racesParams = "meetings/" + meetingCode + "/races";
                        //Debug.WriteLine(racesParams);
                        addInfo("Request " + meetingCode + " Races" + Environment.NewLine);

                        string sRaces = getFromURL(racesParams);
                        //Debug.WriteLine("RACES= " + sRaces);
                        if (sRaces != "")
                        {
                            dRaces = JsonConvert.DeserializeObject<dynamic>(sRaces);
                            if (!processRaces(dMeetings[i], dRaces))
                            {

                                return;
                            }
                            
                        }
                        xmlEndMeet();
                        WriteToFile();
                        addInfo("End Meeting" + meetingCode + Environment.NewLine);
                    }
                    addNewMeeting(meetingCode, lastUpdate);
                    addInfo("===== End XML Meeting " + meetingCode + " (" + count.ToString() + "/" + meetingsCount.ToString() + ") =====" + Environment.NewLine);
                    //if (count == 3) break;
                }
            }//for i
            if (count > 0)
            {
                txtStatus.Text = "Done";
            }
        }
        private bool processRaces(dynamic Meet,dynamic dRaces)
        {
            //Boolean stopNow = false;
            string raceFieldParams = "";
            string raceCode = "";
            try
            {
                for (Int16 i = 0; i < dRaces.Count; i++)
                {//"/races/{raceCode}/raceAndFields
                    addInfo("XML Race " + (i + 1).ToString() + Environment.NewLine);
                    xmlRace(Meet, dRaces[i]); //<RACE_LIST_HORSE>
                    addInfo("Request Race " + (i + 1).ToString() + ", Fields and Forms" + Environment.NewLine);

                    //if (!stopNow)
                    //{//"/races/{raceCode}/raceAndForm":
                    raceCode = dRaces[i]["raceCode"].ToString();
                    //raceFieldParams = "races/" + raceCode + "/fields";
                    raceFieldParams = "races/" + raceCode + "/raceAndForm";
                    string sRaceAndFields = getFromURL(raceFieldParams);
                    //Debug.WriteLine("RACES - raceAndForm = " + sRaceAndFields);
                    if (sRaceAndFields != "")
                    {
                        dRunners = JsonConvert.DeserializeObject<dynamic>(sRaceAndFields);
                        for (Int16 j = 0; j < dRunners["raceForm"].Count; j++)
                        {
                            txtStatus.Text = Meet["meetingCode"].ToString() + " XML Race " + (i + 1).ToString() + " - Runner No." + (j + 1).ToString() + Environment.NewLine;
                            txtStatus.Refresh();
                            xmlRunner(dRaces[i], dRunners["raceForm"][j]); //Runner/Horse
                            if (dRunners["raceForm"][j]["form"].Count == 0)
                            {
                                appendXmlLineX("<LIST_FORM/>", 5);
                            }
                            else
                            {
                                xmlStartFormList();
                                for (int k = dRunners["raceForm"][j]["form"].Count - 1; k >= 0; k--) //List form
                                {
                                    xmlForm(dRaces[i], dRunners["raceForm"][j], dRunners["raceForm"][j]["form"][k]);
                                }
                                xmlEndFormList();
                            }

                            xmlEndRunner();
                        }
                    }
                    
                    //xmlEndRaceListHorse();
                    xmlEndRace();
                    //    stopNow = true;
                    //    break; //TO BE REMOVED
                    //}
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + "Try again later!");
                return false;
            }
            return true;
        }
        
        //private string test1(string paramMeetings)
        //{
        //    string responseFromServerx = "";

        //    WebClient client = new WebClient();
        //    client.Headers.Add("x-api-key", "7AlLgoLr4m79Lm486FD5q63mh5qylaZf3llxElRU");
        //    //client.Headers.Add("application/json; charset=utf-8");
        //    responseFromServerx = client.DownloadString(URL + paramMeetings);
        //    return responseFromServerx;
        //}
        private string getFromURL(string paramMeetings)
        {
            string responseFromServerx = "";
           
            WebRequest request = WebRequest.Create(URL + paramMeetings);
            request.ContentType = "application/json; charset=utf-8";
            request.Headers.Add("x-api-key", "7AlLgoLr4m79Lm486FD5q63mh5qylaZf3llxElRU");
            //request.Headers.Add("application", "json");
            try
            {
                //Debug.WriteLine("Before Request");
                WebResponse respMeets = request.GetResponse();
                //Debug.WriteLine("After Request");
                //Debug.WriteLine(((HttpWebResponse)respMeets).StatusDescription);
                using (Stream dataStream = respMeets.GetResponseStream())
                {
                    // Open the stream using a StreamReader for easy access.
                    StreamReader reader = new StreamReader(dataStream);
                    // Read the content.
                    responseFromServerx = reader.ReadToEnd();
                    // Display the content.
                    
                }

                // Close the response.
                respMeets.Close();
                return responseFromServerx;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return "";
        }
        //==================================XML=========================
        private void xmlMeet(dynamic meet)
        {//"/venues/{trackId}/{clubId}"
            string tmp = "";
            string param = "/venues/" + meet["trackId"].ToString() + "/" + meet["clubId"].ToString();
            string sVenue = getFromURL(param);
            dynamic dVenue = JsonConvert.DeserializeObject<dynamic>(sVenue);

            //string xyz = dVenue.ToString();
            //Debug.WriteLine(xyz);
            
            xmlFile = txtFolder.Text + "\\" + meet["meetingCode"].ToString() + ".xml";
            deleteFile(xmlFile);
            xmlLines.Clear();
            xmlLines.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xmlLines.AppendLine("<!DOCTYPE MEETING SYSTEM \"racemeetingfull-9.0.dtd\">");
            
            appendXmlLineX("<MEETING>",0);
            appendXmlLine("<MEETING_CODE>",  meet["meetingCode"].ToString(), "</MEETING_CODE>", 1);
            sState = meet["state"].ToString();

            appendXmlLine("<MEETING_STATE>", sState ,"</MEETING_STATE>", 1);

            DateTime dt = DateTime.Parse(meet["scheduledDateAsISO8601"].ToString());
            appendXmlLine("<MEETING_SCHEDULED_DATE>" ,dt.ToString("dd/MMM/yy").ToUpper(), "</MEETING_SCHEDULED_DATE>", 1);

            appendXmlLineX("<MEETING_HOST_CLUB_CLIENT_ID></MEETING_HOST_CLUB_CLIENT_ID>",1);
            appendXmlLineX("<MEETING_TRACK_PROPERTY_ID></MEETING_TRACK_PROPERTY_ID>",1);
            appendXmlLineX("<MEETING_NAME/>",1);
            appendXmlLine("<MEETING_CLUB>", meet["club"].ToString(), "</MEETING_CLUB>", 1);
            appendXmlLine("<MEETING_DATE>", meet["scheduledDate"].ToString(), "</MEETING_DATE>",1);
            appendXmlLineX("<MEETING_TIME/>",1);
            appendXmlLineX("<MEETING_UTC_OFFSET/>",1);
            appendXmlLine("<MEETING_DAY_NIGHT_TWILIGHT>",meet["dayNightTwilight"].ToString(), "</MEETING_DAY_NIGHT_TWILIGHT>",1);
            appendXmlLine("<MEETING_TRACK>" , meet["track"].ToString(), "</MEETING_TRACK>",1);
            appendXmlLine("<MEETING_CIRCUMFERENCE>" , dVenue["track"]["circumference"].ToString() , "</MEETING_CIRCUMFERENCE>",1);
            appendXmlLine("<MEETING_STRAIGHT>" , dVenue["track"]["homeStraightLength"].ToString() , "</MEETING_STRAIGHT>",1);
            appendXmlLine("<MEETING_CLASS>" , meet["meetingClass"].ToString() , "</MEETING_CLASS>",1);
            appendXmlLineX("<MEETING_CLUBTAB_CODE></MEETING_CLUBTAB_CODE>",1);// to be done

            tmp = meet["meetingClass"].ToString();
            appendXmlLine("<MEETING_TAB_CLASS>" , tmp.Replace("-CLASS", "") , "</MEETING_TAB_CLASS>",1);
            appendXmlLine("<MEETING_TRACK_ABBREV_NAME>" , dVenue["club"]["abbreviatedName"].ToString() , "</MEETING_TRACK_ABBREV_NAME>",1);
            appendXmlLine("<MEETING_SPRINT_LANE>" , dVenue["track"]["sprintLane"].ToString() , "</MEETING_SPRINT_LANE>",1);

            //tmp = meet["tab"].ToString();
            appendXmlLine("<MEETING_IS_TAB>" , meet["tab"].ToString() , "</MEETING_IS_TAB>",1);
            appendXmlLine("<MEETING_CLUB_DISPLAY_NAME>" , meet["club"].ToString() , "</MEETING_CLUB_DISPLAY_NAME>",1);
            appendXmlLineX("<MEETING_TRIALS_INCLUDED>0</MEETING_TRIALS_INCLUDED>",1);
            appendXmlLineX("<STATE_AUTHORITY_NAME></STATE_AUTHORITY_NAME>",1);
            appendXmlLineX("<STATE_AUTHORITY_SHORT_NAME></STATE_AUTHORITY_SHORT_NAME>",1); // Could not be found
            appendXmlLineX("<MEETING_SCHEDULED_DT/>",1);
            appendXmlLineX("<MEETING_NOMS_CLOSE_DT/>",1);
            appendXmlLineX("<MEETING_ACCEPTANCE_DT/>",1);
            appendXmlLineX("<MEETING_FIELDS_AVAILABLE_DT/>",1);
            appendXmlLineX("<MEETING_DRIVERS_AVAILABLE_DT/>",1);
            appendXmlLineX("<MEETING_LATE_SCRATCHING_DT/>",1);
            appendXmlLineX("<MEETING_SCHEDULED_TS/>",1);
            appendXmlLineX("<MEETING_NOMS_CLOSE_TS/>",1);
            appendXmlLineX("<MEETING_ACCEPTANCE_TS/>",1);
            appendXmlLineX("<MEETING_FIELDS_AVAILABLE_TS/>",1);
            appendXmlLine("<MEETING_DRIVERS_AVAILABLE_TS>" , meet["driversAvailableTime"].ToString() , "</MEETING_DRIVERS_AVAILABLE_TS>",1);
            appendXmlLine("<MEETING_LATE_SCRATCHING_TS>" , meet["lateScratchingTime"].ToString()  , "</MEETING_LATE_SCRATCHING_TS>",1);
            appendXmlLine("<STARTS_GENERATED>" , meet["numberOfRaces"].ToString() , "</STARTS_GENERATED>",1);
            appendXmlLine("<MEETING_STATUS>" , meet["meetingStatus"].ToString() , "</MEETING_STATUS>",1);
            appendXmlLineX("<MEETING_ORG_NAME/>",1);
            appendXmlLine("<CREATED_DATE>" , DateTime.Now.ToShortDateString() , "</CREATED_DATE>",1);// not found use current pc date
            appendXmlLine("<CREATED_TIMESTAMP>" , DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fffff"), "</CREATED_TIMESTAMP>",1);// not found use current pc date
            appendXmlLineX("<LINK_NAME_USED/>",1);

            appendXmlLineX("<LIST_RACES>",1);
            WriteToFile();
            xmlLines.Clear();
        }
        private void xmlRace(dynamic meet,dynamic race )
        {
            
            appendXmlLineX("<RACE>", 2);
            appendXmlLine("<MEETING_CODE>" , race["meetingCode"].ToString() , "</MEETING_CODE>", 3);
            appendXmlLine("<RACE_CODE>" , race["raceCode"].ToString() , "</RACE_CODE>", 3);
            appendXmlLine("<RACE_STATE>" , sState , "</RACE_STATE>", 3);
            appendXmlLine("<RACE_NUMBER>" , race["raceNumber"].ToString() , "</RACE_NUMBER>", 3);
            appendXmlLine("<RACE_NAME>" , race["name"].ToString() , "</RACE_NAME>", 3);
            appendXmlLine("<RACE_SHORT_RACE_NAME>" , race["nameShort"].ToString() , "</RACE_SHORT_RACE_NAME>", 3);
            appendXmlLine("<RACE_TIME>" , race["plannedStartTimeLocal"].ToString() , "</RACE_TIME>", 3);
            appendXmlLine("<RACE_TS>" , race["plannedStartTimestamp"].ToString() , "</RACE_TS>", 3);
            appendXmlLine("<RACE_DISTANCE>" , race["distance"].ToString() , "</RACE_DISTANCE>", 3);
            RaceStartType = race["startType"].ToString();
            if (RaceStartType=="SS")
            {
                StandingRow = 0;
                previousMark = "";
                //MessageBox.Show("SS " + race["meetingCode"].ToString());
            }
            appendXmlLine("<RACE_START_TYPE>" , RaceStartType , "</RACE_START_TYPE>", 3);
            appendXmlLine("<RACE_STAKES>" , FormattedAmount(race["stakes"].ToString()) , "</RACE_STAKES>", 3);
            appendXmlLineX("<RACE_TROPHY/>" , 3);
            string ageRestriction = race["ageRestriction"].ToString();
            ageRestriction = ageRestriction.Replace(" and older", "+");
            if (ageRestriction.Length > 5){ ageRestriction = ageRestriction.Substring(0, 5); }
            appendXmlLine("<RACE_AGE_RESTRICTION>" , ageRestriction , "</RACE_AGE_RESTRICTION>", 3);
            appendXmlLine("<RACE_CLASS>" , race["raceClass"].ToString() , "</RACE_CLASS>", 3);
            appendXmlLine("<RACE_CLASS_RESTRICTION>" , race["raceClassRestriction"].ToString() , "</RACE_CLASS_RESTRICTION>", 3);
            appendXmlLine("<RACE_GAIT>" , race["gait"].ToString() , "</RACE_GAIT>", 3);
            appendXmlLine("<RACE_SEX_RESTRICTION>" , race["ageSexDescription"].ToString() , "</RACE_SEX_RESTRICTION>", 3);
            appendXmlLine("<RACE_BARRIER_DRAW_TYPE>" , race["barrierDrawType"].ToString() , "</RACE_BARRIER_DRAW_TYPE>", 3);
            appendXmlLine("<RACE_PRIZEMONEY_1_4>" , race["prizemoney14"].ToString() , "</RACE_PRIZEMONEY_1_4>", 3);
            appendXmlLine("<RACE_PRIZEMONEY_ALL>" , race["prizemoneyAll"].ToString() , "</RACE_PRIZEMONEY_ALL>", 3);
            appendXmlLineX("<RACE_TRACK_RECORD/>" , 3);
            appendXmlLine("<RACE_AGE_SEX_TRACK_RECORD>" , race["ageSexTrackRecord"].ToString() , "</RACE_AGE_SEX_TRACK_RECORD>", 3);
            appendXmlLine("<RACE_BET_TYPES>" , race["betTypes"].ToString() , "</RACE_BET_TYPES>", 3);
            appendXmlLine("<RACE_NOTES>" , race["notes"].ToString() , "</RACE_NOTES>", 3);

            string tmp = ageRestriction + " " + race["ageSexDescription"].ToString();
            appendXmlLine("<RACE_AGE_SEX_DESCRIPTION>" , tmp , "</RACE_AGE_SEX_DESCRIPTION>", 3);
            appendXmlLine("<RACE_ATC_CLASS>" , race["raceClassRestriction"].ToString() ,"</RACE_ATC_CLASS>", 3);
            appendXmlLineX("<RACE_BARRIER_DRAW_DESCRIPTION/>", 3);
            appendXmlLineX("<RACE_STAKES_1_4_UNFORMATTED/>", 3); 
            appendXmlLine("<RACE_START_TYPE_WORD>" , race["startTypeWord"].ToString() , "</RACE_START_TYPE_WORD>", 3);
            appendXmlLine("<RACE_NO_ACROSS_FRONT>" , race["numberAcrossFront"].ToString() , "</RACE_NO_ACROSS_FRONT>", 3);
            appendXmlLine("<RACE_DISTANCE_IN_LAPS>" , race["distanceInLaps"].ToString() , "</RACE_DISTANCE_IN_LAPS>", 3);
            appendXmlLine("<RACE_ALSO_ELIGIBLE>" , race["alsoEligible"].ToString() , "</RACE_ALSO_ELIGIBLE>", 3);
            appendXmlLine("<RACE_TOTAL_STARTERS>" , race["fieldSize"].ToString() , "</RACE_TOTAL_STARTERS>", 3);

            tmp = race["discretionaryHandicap"].ToString();
            tmp = (tmp.ToUpper() == "TRUE" ? "Discretionary Handicap1" : "");
            appendXmlLine("<RACE_DISC_HCP>" , tmp , "</RACE_DISC_HCP>", 3);

            appendXmlLine("<RACE_CLAIM_RESTRICTION_TEXT>" , race["claimRestrictionText"].ToString() , "</RACE_CLAIM_RESTRICTION_TEXT>", 3);
            appendXmlLineX("<RACE_DT/>", 3);
            appendXmlLineX("<RACE_VICBRED/>", 3);
            appendXmlLine("<RACE_PRIZEMONEY_1_3>" , race["prizemoney13"].ToString() , "</RACE_PRIZEMONEY_1_3>", 3);
            appendXmlLineX("<RACE_STAKES_1_3_UNFORMATTED/>", 3);
            appendXmlLine("<RACE_BLACK_TYPE>" , race["blackType"].ToString() , "</RACE_BLACK_TYPE>", 3);
            appendXmlLineX("<RACE_ENG_LEGEND/>", 3);
            appendXmlLineX("<RACE_HPR_MIN/>", 3);
            appendXmlLineX("<RACE_HPR_MAX/>", 3);
            appendXmlLineX("<RACE_HPR_WINS/>", 3);
            appendXmlLine("<RACE_MONTE>" , race["monte"].ToString() , "</RACE_MONTE>", 3);
            appendXmlLineX("<RACE_CONDITIONS_DESCRIPTION/>", 3);

            appendXmlLineX("<RACE_LIST_HORSES>", 3);
            WriteToFile();
            xmlLines.Clear();
        }
        //private void xmlRunner(dynamic race, dynamic runner, dynamic dHorseDet)
        private void xmlRunner(dynamic race,dynamic runner)
        {
            
            appendXmlLineX("<HORSE>", 4);
            appendXmlLine("<MEETING_CODE>" , race["meetingCode"].ToString() , "</MEETING_CODE>", 5);
            appendXmlLine("<RACE_CODE>" , race["raceCode"].ToString() , "</RACE_CODE>", 5);
            string tmp = runner["horseId"].ToString();
            if (tmp.Length > 6)
            {
                tmp = tmp.Substring(0, 6);  //51869C7DCE536D1E055005E00000019 ==>51869
            }
            appendXmlLine("<HORSE_ID>" , tmp , "</HORSE_ID>", 5);
            appendXmlLine("<HORSE_STATE>" , sState , "</HORSE_STATE>", 5);
            
            appendXmlLine("<HORSE_BRAND_NO>" , runner["freezebrand"].ToString(), "</HORSE_BRAND_NO>", 5);
            appendXmlLineX("<HORSE_LIP_TATTOO/>", 5);
            appendXmlLineX("<HORSE_REF_BRAND/>", 5);
            appendXmlLineX("<HORSE_CERT_NO/>", 5);
            appendXmlLineX("<HORSE_RAC_NO/>", 5);
            appendXmlLineX("<HORSE_SIRE_HORSE_ID/>", 5);
            appendXmlLineX("<HORSE_DAM_HORSE_ID/>", 5);
            appendXmlLineX("<HORSE_SIRE_OF_DAM_HORSE_ID/>", 5);
            appendXmlLineX("<HORSE_DAM_OF_DAM_HORSE_ID/>", 5);
            appendXmlLineX("<HORSE_BREEDER_CLIENT_ID/>", 5);
            appendXmlLineX("<HORSE_OWNER_LESSEE_CLIENT_ID/>", 5);
            appendXmlLineX("<HORSE_TRAINER_CLIENT_ID/>", 5);
            appendXmlLineX("<HORSE_DRIVER_CLIENT_ID/>", 5);
            appendXmlLine("<HORSE_SADDLECLOTH>" , runner["saddlecloth"].ToString() , "</HORSE_SADDLECLOTH>", 5);
            appendXmlLine("<HORSE_BARRIER>" , runner["barrier"].ToString() , "</HORSE_BARRIER>", 5);
            string row = getRow(runner["barrier"].ToString());
            string pos = getPos(runner["barrier"].ToString());
            appendXmlLine("<HORSE_ROW>" , row , "</HORSE_ROW>", 5);
            appendXmlLine("<HORSE_POSITION>" , pos , "</HORSE_POSITION>", 5);
            tmp = runner["frontRowFlag"].ToString();
            tmp = (tmp.ToUpper () == "TRUE" ? "1":"2");
            appendXmlLine("<HORSE_FRONT_ROW_FLAG>" , tmp , "</HORSE_FRONT_ROW_FLAG>", 5);//guessing
            appendXmlLine("<HORSE_NAME>" , runner["name"].ToString() , "</HORSE_NAME>", 5);
            appendXmlLineX("<HORSE_STATE_BRED_ELIGIBLE/>" , 5);//dont know
            appendXmlLine("<HORSE_CLASS>" , runner["class"].ToString() , "</HORSE_CLASS>", 5);
            //appendXmlLine("<HORSE_CLASS_METRO>" , runner["class"].ToString() , "</HORSE_CLASS_METRO>", 5);
            appendXmlLineX("<HORSE_CLASS_METRO/>",5);
            appendXmlLine("<HORSE_HANDICAP>" , runner["handicap"].ToString() , "</HORSE_HANDICAP>", 5);
            appendXmlLine("<HORSE_ODS_STATUS>" , runner["odStatus"].ToString() , "</HORSE_ODS_STATUS>", 5);
            appendXmlLine("<HORSE_RODS_STATUS>" , runner["odStatus"].ToString() , "</HORSE_RODS_STATUS>", 5);
            appendXmlLine("<HORSE_OWNER>" , runner["owner"].ToString() , "</HORSE_OWNER>", 5);
            appendXmlLine("<HORSE_BREEDER>" , runner["breeder"].ToString() , "</HORSE_BREEDER>", 5);
            appendXmlLine("<HORSE_SIRE>" , runner["sireName"].ToString() , "</HORSE_SIRE>", 5);
            appendXmlLine("<HORSE_DAM>" , runner["damName"].ToString() , "</HORSE_DAM>", 5);
            appendXmlLine("<HORSE_BROODMARE_SIRE>" , runner["broodmareSireName"].ToString() , "</HORSE_BROODMARE_SIRE>", 5);
            appendXmlLine("<HORSE_TRAINER>" , runner["trainerName"].ToString() , "</HORSE_TRAINER>", 5);
            appendXmlLine("<HORSE_TRAINER_SHORT>" , runner["trainerNameShort"].ToString() , "</HORSE_TRAINER_SHORT>", 5);
            appendXmlLine("<HORSE_TRAINER_STABLE>" , runner["trainerStable"].ToString() ,  "</HORSE_TRAINER_STABLE>", 5);
            appendXmlLine("<HORSE_DRIVER>" , runner["driverName"].ToString() , "</HORSE_DRIVER>", 5);
            appendXmlLine("<HORSE_DRIVER_SHORT>" , runner["driverNameShort"].ToString() , "</HORSE_DRIVER_SHORT>", 5);
            appendXmlLineX("<HORSE_RACING_COLOURS_ID/>", 5);
            appendXmlLine("<HORSE_RACING_COLOURS>" , runner["racingColours"].ToString() , "</HORSE_RACING_COLOURS>", 5);// TO BE CHANGED
            appendXmlLine("<HORSE_COLOUR>" , runner["colour"].ToString() , "</HORSE_COLOUR>", 5);
            appendXmlLine("<HORSE_AGE>" , runner["age"].ToString() , "</HORSE_AGE>", 5);
            appendXmlLine("<HORSE_SEX>" , runner["sex"].ToString() , "</HORSE_SEX>", 5);
            appendXmlLine("<HORSE_GAIT>" , runner["gait"].ToString() , "</HORSE_GAIT>", 5);
            tmp = runner["trotterInPacersRace"].ToString();
            tmp = (tmp.ToUpper() == "TRUE" ? "1" : "");
            appendXmlLine("<HORSE_TROTTER_IN_PACERS_RACE>" , tmp.Trim() , "</HORSE_TROTTER_IN_PACERS_RACE>", 5);
            appendXmlLine("<HORSE_LIFETIME_FORM>" , runner["lifetimeFigureForm"].ToString() , "</HORSE_LIFETIME_FORM>", 5);
            appendXmlLine("<HORSE_LAST_SIX_STARTS_FORM>" , runner["lastSixStartsFigureForm"].ToString() , "</HORSE_LAST_SIX_STARTS_FORM>", 5);
            appendXmlLine("<HORSE_THIS_SEASON_FIGURE_FORM>" , runner["thisSeasonFigureForm"].ToString() , "</HORSE_THIS_SEASON_FIGURE_FORM>", 5);
            appendXmlLine("<HORSE_LAST_SEASON_FIGURE_FORM>" , runner["lastSeasonFigureForm"].ToString() , "</HORSE_LAST_SEASON_FIGURE_FORM>", 5);
            appendXmlLine("<HORSE_LIFETIME_STATS>" , runner["lifetimeStats"].ToString() , "</HORSE_LIFETIME_STATS>", 5);
            appendXmlLine("<HORSE_LIFETIME_STATS_EXTENDED>" , runner["lifetimeStatsExtended"].ToString() , "</HORSE_LIFETIME_STATS_EXTENDED>", 5);
            appendXmlLine("<HORSE_LAST_SEASON_STATS>" , runner["lastSeasonStats"].ToString() , "</HORSE_LAST_SEASON_STATS>", 5);
            appendXmlLine("<HORSE_THIS_SEASON_STATS>" , runner["thisSeasonStats"].ToString() , "</HORSE_THIS_SEASON_STATS>", 5);
            appendXmlLine("<HORSE_LIFETIME_STAKES>" , runner["lifetimeStakes"].ToString() , "</HORSE_LIFETIME_STAKES>", 5);
            appendXmlLine("<HORSE_LAST_SEASON_STAKES>" , runner["lastSeasonStakes"].ToString() , "</HORSE_LAST_SEASON_STAKES>", 5);
            appendXmlLine("<HORSE_THIS_SEASON_STAKES>" , runner["thisSeasonStakes"].ToString() , "</HORSE_THIS_SEASON_STAKES>", 5);
            appendXmlLine("<HORSE_CLAIMING_PRICE>" , runner["claimingPrice"].ToString() , "</HORSE_CLAIMING_PRICE>", 5);
            appendXmlLine("<HORSE_EMERGENCY>" , runner["emergency"].ToString() , "</HORSE_EMERGENCY>", 5);
            appendXmlLine("<HORSE_ENGAGEMENTS>" , runner["engagements"].ToString() , "</HORSE_ENGAGEMENTS>", 5);
            appendXmlLineX("<HORSE_BLED_COUNT>0</HORSE_BLED_COUNT>", 5);
            appendXmlLine("<HORSE_DRIVER_CONC_FLAGS>" , runner["driverConcessionFlag"].ToString() , "</HORSE_DRIVER_CONC_FLAGS>", 5);
            appendXmlLine("<HORSE_FORM_COMMENTARY>" , runner["formCommentary"].ToString() , "</HORSE_FORM_COMMENTARY>", 5);
            appendXmlLineX("<HORSE_RATINGS/>", 5);
            appendXmlLineX("<HORSE_STALL/>" , 5);
            appendXmlLineX("<HORSE_STATE_SIRES_FUT_ELIGIBLE/>" , 5);
            appendXmlLineX("<HORSE_STATE_BORN_FUT_ELIGIBLE/>", 5);
            string scr = runner["scratchingFlag"].ToString();
            
            scr = (scr.ToLower()  == "true" ? "SCRATCHED" : "");
            appendXmlLine("<HORSE_STATUS_SCRATCHED>" , scr.Trim() , "</HORSE_STATUS_SCRATCHED>", 5);
            appendXmlLine("<HORSE_LAST_3_WITH_CLASS>" , runner["lastThreeWithClass"].ToString() , "</HORSE_LAST_3_WITH_CLASS>", 5);
            appendXmlLineX("<HORSE_NAME_PROPER/>" , 5);
            appendXmlLineX("<HORSE_SELECTION/>" , 5);
            appendXmlLine("<HORSE_STARTING_MARKET>" , runner["startingMarket"].ToString() , "</HORSE_STARTING_MARKET>", 5);
            appendXmlLine("<HORSE_STARTING_MARKET_DECIMAL>" , runner["startingMarket"].ToString() , "</HORSE_STARTING_MARKET_DECIMAL>", 5);
            appendXmlLine("<HORSE_LAST_SEASON_STATS_EXT>" , runner["lastSeasonStatsExtended"].ToString() , "</HORSE_LAST_SEASON_STATS_EXT>", 5);
            appendXmlLine("<HORSE_THIS_SEASON_STATS_EXT>" , runner["thisSeasonStatsExtended"].ToString() , "</HORSE_THIS_SEASON_STATS_EXT>", 5);
            appendXmlLine("<HORSE_BEST_MR>" , runner["bestMileRate"].ToString() , "</HORSE_BEST_MR>", 5);
            appendXmlLine("<HORSE_RANGE_DIST_BEST_MR>" , runner["rangeDistBestMileRate"].ToString() , "</HORSE_RANGE_DIST_BEST_MR>", 5);
            appendXmlLine("<HORSE_EXACT_DIST_STATS>" , runner["exactDistStats"].ToString() , "</HORSE_EXACT_DIST_STATS>", 5);
            appendXmlLine("<HORSE_RANGE_DIST_STATS>" , runner["rangeDistStats"].ToString() , "</HORSE_RANGE_DIST_STATS>", 5);
            appendXmlLine("<HORSE_TRACK_STATS>" , runner["trackStats"].ToString() , "</HORSE_TRACK_STATS>", 5);
            appendXmlLine("<HORSE_EXACT_DIST_TRACK_STATS>" , runner["exactDistTrackStats"].ToString() , "</HORSE_EXACT_DIST_TRACK_STATS>", 5);
            appendXmlLine("<HORSE_RANGE_DIST_TRACK_STATS>" , runner["rangeDistTrackStats"].ToString() , "</HORSE_RANGE_DIST_TRACK_STATS>", 5);
            appendXmlLine("<HORSE_START_STATS>" , runner["startStats"].ToString() , "</HORSE_START_STATS>", 5);
            appendXmlLine("<HORSE_START_TRACK_STATS>" , runner["startTrackStats"].ToString() , "</HORSE_START_TRACK_STATS>", 5);
            appendXmlLineX("<PREVIOUS_ENGAGEMENTS/>" , 5);
            appendXmlLineX("<FUTURE_ENGAGEMENT_RACE_CODES/>" , 5);
            appendXmlLine("<HORSE_GEAR_ADDED>" , runner["gearAdded"].ToString() , "</HORSE_GEAR_ADDED>", 5);
            appendXmlLine("<HORSE_GEAR_REMOVED>" , runner["gearRemoved"].ToString() , "</HORSE_GEAR_REMOVED>", 5);
            appendXmlLineX("<FIRST_START_DETAILS/>", 5);
            appendXmlLineX("<ENG_CODES/>", 5);
            appendXmlLineX("<HORSE_HPR_POINTS/>", 5);
            appendXmlLineX("<FORM_PREVIEW/>" , 5); //dont know
            appendXmlLineX("<APG_ICON_FLAG/>", 5);//dont know
            appendXmlLine("<HORSE_MONTE_STATS>" , runner["monteStats"].ToString() , "</HORSE_MONTE_STATS>", 5);

            WriteToFile();
            xmlLines.Clear();
        }
        private void xmlStartFormList()
        {
            appendXmlLineX("<LIST_FORM>", 5);
        }

        private void xmlForm(dynamic race, dynamic runner, dynamic form)
        {
            txtStatus.Text = race["meetingCode"].ToString() + " XML Race " + race["raceNumber"].ToString() + " - Runner No." + runner["saddlecloth"].ToString() + " - Form " + form["date"].ToString() + Environment.NewLine;
            txtStatus.Refresh();
            appendXmlLineX("<FORM>", 6);
            appendXmlLine("<MEETING_CODE>" , race["meetingCode"].ToString() , "</MEETING_CODE>", 7);
            appendXmlLine("<RACE_CODE>" , race["raceCode"].ToString() , "</RACE_CODE>", 7);
            string tmp = runner["horseId"].ToString();
            if (tmp.Length > 6)
            {
                tmp = tmp.Substring(0, 6);  //51869C7DCE536D1E055005E00000019 ==>51869
            }
            appendXmlLine("<HORSE_ID>" , tmp , "</HORSE_ID>", 7);
            appendXmlLineX("<FORM_LINE_NUMBER/>" , 7);
            appendXmlLine("<FORM_TYPE>" , form["type"].ToString() , "</FORM_TYPE>", 7);
            appendXmlLine("<FORM_STATE>" , form["state"].ToString() , "</FORM_STATE>", 7); 
            appendXmlLine("<FORM_RACE_CODE>" , form["raceCode"].ToString() , "</FORM_RACE_CODE>", 7);
            appendXmlLineX("<FORM_RACE_NUMBER/>", 7);
            appendXmlLine("<FORM_PLACE>" , form["place"].ToString() , "</FORM_PLACE>", 7);
            appendXmlLine("<FORM_FIELD_SIZE>" , form["fieldSize"].ToString() , "</FORM_FIELD_SIZE>", 7);
            appendXmlLine("<FORM_TRACK>" , form["track"].ToString() , "</FORM_TRACK>", 7);
            appendXmlLine("<FORM_TRACK_CONDITION>" , form["trackCondition"].ToString() , "</FORM_TRACK_CONDITION>", 7);
            appendXmlLine("<FORM_DATE>" , form["date"].ToString() , "</FORM_DATE>", 7);
            appendXmlLine("<FORM_RACE_NAME>" , form["shortRaceName"].ToString() , "</FORM_RACE_NAME>", 7);
            appendXmlLine("<FORM_SHORT_RACE_NAME>" , form["shortRaceName"].ToString() , "</FORM_SHORT_RACE_NAME>", 7);
            appendXmlLine("<FORM_DISTANCE>" , form["distance"].ToString() , "</FORM_DISTANCE>", 7);
            appendXmlLine("<FORM_START_TYPE>" , form["startType"].ToString() , "</FORM_START_TYPE>", 7);
            appendXmlLine("<FORM_HANDICAP>" , form["handicap"].ToString() , "</FORM_HANDICAP>", 7);
            appendXmlLine("<FORM_BARRIER>" , form["barrier"].ToString() , "</FORM_BARRIER>", 7);
            appendXmlLineX("<FORM_ROW/>" , 7);
            appendXmlLineX("<FORM_POSITION/>" , 7);
            appendXmlLine("<FORM_RACE_CLASS_TYPE>" , form["raceClass"].ToString() , "</FORM_RACE_CLASS_TYPE>", 7);//request to change classOfRace to raceClass
            appendXmlLine("<FORM_CLASS_OF_RACE>" , form["raceClass"].ToString() , "</FORM_CLASS_OF_RACE>", 7);//classOfRace is depreciated
            appendXmlLine("<FORM_DRIVER>" , form["driverName"].ToString() , "</FORM_DRIVER>", 7); // driver is depreciated
            appendXmlLine("<FORM_DRIVER_SHORT>" , getNameShort(form["driverName"].ToString()) , "</FORM_DRIVER_SHORT>", 7);
            appendXmlLine("<FORM_BEATEN_MARGIN>" , form["beatenMargin"].ToString() , "</FORM_BEATEN_MARGIN>", 7);
            appendXmlLine("<FORM_RACE_TIME>" , form["raceTime"].ToString() , "</FORM_RACE_TIME>", 7);
            appendXmlLine("<FORM_MILE_RATE>" , form["winnerMileRate"].ToString() , "</FORM_MILE_RATE>", 7);
            appendXmlLineX("<FORM_LAST_MILE/>" , 7);
            appendXmlLine("<FORM_LAST_HALF>" , form["lastHalf"].ToString() , "</FORM_LAST_HALF>", 7);
            appendXmlLine("<FORM_LAST_QUARTER>" , form["lastQuarter"].ToString() , "</FORM_LAST_QUARTER>", 7);
            appendXmlLine("<FORM_THIRD_QUARTER>" , form["thirdQuarter"].ToString() , "</FORM_THIRD_QUARTER>", 7);
            appendXmlLine("<FORM_SECOND_QUARTER>" , form["secondQuarter"].ToString() , "</FORM_SECOND_QUARTER>", 7);
            appendXmlLine("<FORM_FIRST_QUARTER>" , form["firstQuarter"].ToString() , "</FORM_FIRST_QUARTER>", 7);
            appendXmlLine("<FORM_MARGIN_1_2>" , form["marginFirstToSecond"].ToString() , "</FORM_MARGIN_1_2>", 7);
            appendXmlLine("<FORM_MARGIN_2_3>" , form["marginSecondToThird"].ToString() , "</FORM_MARGIN_2_3>", 7);
            appendXmlLine("<FORM_STARTING_PRICE>" , form["startingPrice"].ToString() , "</FORM_STARTING_PRICE>", 7);
            appendXmlLineX("<FORM_FLUCTUATIONS/>" , 7);
            appendXmlLine("<FORM_FAVOURITISM_INDICATOR>" , form["favouritismIndicator"].ToString() , "</FORM_FAVOURITISM_INDICATOR>", 7);
                                                               
            appendXmlLine("<FORM_WINNER_NAME>" , form["winnerName"].ToString() , "</FORM_WINNER_NAME>", 7);
            appendXmlLine("<FORM_WINNER_HANDICAP>" , form["winnerHandicap"].ToString() , "</FORM_WINNER_HANDICAP>", 7);
            appendXmlLine("<FORM_WINNER_BARRIER>" , form["winnerBarrier"].ToString() , "</FORM_WINNER_BARRIER>", 7);
            appendXmlLineX("<FORM_WINNER_ROW/>", 7);
            appendXmlLineX("<FORM_WINNER_ROW_POSITION/>", 7);
            appendXmlLine("<FORM_SECOND_NAME>" , form["secondName"].ToString() , "</FORM_SECOND_NAME>", 7);
            appendXmlLine("<FORM_SECOND_HANDICAP>" , form["secondHandicap"].ToString() , "</FORM_SECOND_HANDICAP>", 7);
            appendXmlLine("<FORM_SECOND_BARRIER>" , form["secondBarrier"].ToString() , "</FORM_SECOND_BARRIER>", 7);
            appendXmlLineX("<FORM_SECOND_ROW/>" , 7);
            appendXmlLineX("<FORM_SECOND_ROW_POSITION/>", 7);
            appendXmlLine("<FORM_THIRD_NAME>" , form["thirdName"].ToString() , "</FORM_THIRD_NAME>", 7);
            appendXmlLine("<FORM_THIRD_HANDICAP>" , form["thirdHandicap"].ToString() , "</FORM_THIRD_HANDICAP>", 7);
            appendXmlLine("<FORM_THIRD_BARRIER>" , form["thirdBarrier"].ToString() , "</FORM_THIRD_BARRIER>", 7);
            appendXmlLineX("<FORM_THIRD_ROW/>", 7);
            appendXmlLineX("<FORM_THIRD_ROW_POSITION/>", 7);
            appendXmlLine("<FORM_STEWARDS_COMMENTS>" , form["stewardsComments"].ToString() , "</FORM_STEWARDS_COMMENTS>", 7);
            appendXmlLine("<FORM_STEWARDS_CODES>" , form["stewardsCommentsShort"].ToString() , "</FORM_STEWARDS_CODES>", 7);
            appendXmlLineX("<FORM_FAV_INDICATOR_SHORT/>" , 7);
            string isTrial = form["isTrial"].ToString();
            isTrial = isTrial == "true" ? "Y" : "";
            appendXmlLine("<FORM_IS_TRIAL>" , isTrial   , "</FORM_IS_TRIAL>", 7);
            appendXmlLine("<FORM_PRIZEMONEY>" , form["prizemoney"].ToString() , "</FORM_PRIZEMONEY>", 7);
            appendXmlLineX("<FORM_POS_AT_MILE/>", 7);
            appendXmlLineX("<FORM_POS_AT_BELL/>", 7);
            appendXmlLineX("<FORM_POS_AT_800/>", 7);
            appendXmlLine("<FORM_CLASS>" , form["raceClassRestriction"].ToString() , "</FORM_CLASS>", 7);
            appendXmlLineX("<FORM_SADDLECLOTH_NUMBER/>" , 7);
            appendXmlLineX("<FORM_PREVIEW/>", 7);
            appendXmlLineX("<FORM_REVIEW/>", 7);
            appendXmlLineX("<FORM_TRACK_LONG/>", 7);
            appendXmlLineX("<FORM_MONTE/>", 7); // + form["monteStats"].ToString() + "</FORM_MONTE>", 7);

            appendXmlLineX("</FORM>", 6);
            WriteToFile();
            xmlLines.Clear();
        }
        private void xmlEndFormList()
        {
            appendXmlLineX("</LIST_FORM>", 5);
        }
        private void xmlEndRunner()
        {
            appendXmlLineX("</HORSE>", 4);
        }
        //private void xmlEndRaceListHorse()
        //{
        //    appendXmlLineX("</RACE_LIST_HORSES>", 3);
        //}
        private void xmlEndRace()
        {
            appendXmlLineX("</RACE_LIST_HORSES>", 3);
            appendXmlLineX("</RACE>", 2);
        }
        private void xmlEndMeet()
        {
            appendXmlLineX("</LIST_RACES>",1);
            appendXmlLineX("</MEETING>",0);
        }
        private string getRow(string barrier)
        {
            //Fr1, Sr1, Sr-
            string retVal = "";

            if (barrier.ToUpper() == "SCR") return retVal;

            if (RaceStartType == "MS")
            {
                if (barrier.Substring(0, 2) == "Fr")
                    retVal = "1";
                else //Sr
                    retVal = "2";
            }
            else ////FT Fr1,10 Fr1,30 Fr1
            {
                string[] s = barrier.Split(' ');
                if (s[0] == "FT")
                {
                    StandingRow = 1;
                    previousMark = "FT";
                    retVal = "1";
                }
                else
                {
                    if((previousMark != s[0]) ||(previousMark == s[0] && s[1].ToUpper() == "SR1") )
                    {
                        StandingRow += 1;
                        previousMark = s[0];
                        
                    }
                    retVal = StandingRow.ToString();
                }
                
            }
            
            return retVal;
        }
        private string getPos(string barrier)
        {//FT Fr1,10 Fr1,30 Fr1
         //Fr1, Sr1, Sr-
            string retVal = "";
            if (barrier.ToUpper() == "SCR") return retVal;

            string[] s = barrier.Split(' ');
            if (s.Length > 1)
            {
                s[0] = s[1];
            }
            bool isNumber = int.TryParse(s[0].Substring(2, 1), out _);
            if (isNumber)
            {
                retVal = s[0].Substring(2, 1);
            }
            return retVal;
        }

        private string getNameShort(string theName)
        {
            string sRetVal = "";
            if (theName != "")
            {
                string[] s = theName.Split(' ');
                switch ( s.Length){
                    case 1:
                        sRetVal = s[0];
                        break;
                    case 2:
                        sRetVal = s[0].Substring(0,1) + " " + s[1];
                        break;
                    case 3:
                        sRetVal = s[0].Substring(0, 1) + " " + s[1].Substring(0, 1) + " " + s[2];
                        break;
                }
            }

            return sRetVal;
        }
        private void appendXmlLineX(String sMsg, Int16 level)
        {
            switch (level)
            {
                case 0:
                    break;
                case 1:
                    sMsg = "  " + sMsg;
                    break;
                case 2:
                    sMsg = "    " + sMsg;
                    break;
                case 3://Race det
                    sMsg = "      " + sMsg;
                    break;
                case 4://Horse
                    sMsg = "        " + sMsg;
                    break;
                case 5://Horse det
                    sMsg = "          " + sMsg;
                    break;
                case 6://Form List
                    sMsg = "            " + sMsg;
                    break;
                case 7://Form
                    sMsg = "              " + sMsg;
                    break;
                default:
                    break;
            }

            xmlLines.AppendLine(sMsg);
        }
        private void appendXmlLine(String StartElem,String sMsg,String CloseElem, Int16 level)
        {
            sMsg = sMsg.Replace("&", "&amp;");
            sMsg = sMsg.Replace("'", "&apos;");
            sMsg = sMsg.Replace("\"", "&quot;");
            sMsg = sMsg.Replace("<", "&lt;");
            sMsg = sMsg.Replace(">", "&gt;");
            sMsg = StartElem + sMsg + CloseElem;

            switch (level)
            {
                case 0:
                    break;
                case 1:
                    sMsg = "  " + sMsg;
                    break;
                case 2:
                    sMsg = "    " + sMsg;
                    break;
                case 3://Race det
                    sMsg = "      " + sMsg;
                    break;
                case 4://Horse
                    sMsg = "        " + sMsg;
                    break;
                case 5://Horse det
                    sMsg = "          " + sMsg;
                    break;
                case 6://Form List
                    sMsg = "            " + sMsg;
                    break;
                case 7://Form
                    sMsg = "              " + sMsg;
                    break;
                default:
                    break;
            }
            
            xmlLines.AppendLine (sMsg);
        }
        private string FormattedAmount(string _amount)
        {
            if (_amount == "") return _amount;
            decimal amount;
            amount = Convert.ToDecimal(_amount);
           
            //return string.Format("{0:C}", amount); //eg $10,000.00
            return string.Format("{0:C0}", amount); // eg $10,000 - no cents
        }
        private void WriteToFile()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(File.Open (xmlFile, FileMode.Append, FileAccess.Write)))
                {

                    sw.Write(xmlLines );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private Boolean IsSamelastUpdate(string meetingCode,string lastUpdate)
        {
            Boolean retVal = false;
            for (Int16 i = 0; i < currMeets.Meeting.Count ; i++)
            {
                if ((currMeets.Meeting[i].meetingCode == meetingCode) && (currMeets.Meeting[i].lastUpdate == lastUpdate))
                {
                    retVal = true;
                    break;
                }
            }


            return retVal;
        }
        //private void saveCurrMeets()
        //{

        //}
        private Int16  calcMeetingFieldsAndNoTrial(dynamic Meetings) 
        {
            Int16 count = 0;
            string meetingStage = "";
            string trial = "";
            for (Int16 i = 0; i < Meetings.Count; i++)
            {
                meetingStage = Meetings[i]["meetingStage"].ToString();
                trial = Meetings[i]["trials"].ToString();
                //if (meetingStage == "FIELDS" && trial == "False")
                if(IsStatusMet (meetingStage ,trial ))
                {
                    count++;
                }

            }
                
                return count;
        }
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            
            folderBrowserDialog1.SelectedPath = txtFolder.Text;
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                txtFolder.Text = folderBrowserDialog1.SelectedPath;
                //if (!fileOpened)
                //{
                //    // No file is opened, bring up openFileDialog in selected path.
                //    openFileDialog1.InitialDirectory = folderName;
                //    openFileDialog1.FileName = null;
                //    openMenuItem.PerformClick();
                //}
            }

            
        }
        private void addInfo(String sMsg)
        {
            lbStatus.Items.Add(sMsg);
            lbStatus.TopIndex = lbStatus.Items.Count - 1;
            lbStatus.Refresh();
        }
        private bool deleteFile(string filePath)
        {
            Boolean bRetVal = true;
            if (System.IO.File.Exists(filePath))
            {  
                try
                {
                    System.IO.File.Delete(filePath);
                    
                }
                catch (System.IO.IOException e)
                {
                    xmlLines.Clear();
                    xmlLines.Append(e.Message);
                    WriteToFile();
                    bRetVal = false;
                }
            }
            return bRetVal;
        }

        private void btLastUpdated_Click(object sender, EventArgs e)
        {
            lbStatus.Items.Clear();
            for (Int16 i = 0; i < currMeets.Meeting.Count ; i++)
            {
                string line = currMeets.Meeting[i].meetingCode + " - " + currMeets.Meeting[i].lastUpdate;
                lbStatus.Items.Add(line);
            }
        }
        private void writeToFile()
        {
            using (StreamWriter sw = File.AppendText(logFile))
            {
                foreach (string item in lbStatus.Items)
                {
                    sw.WriteLine(item);
                }
                sw.Close();
            }
                   

            //if (!File.Exists(logFile))
            //{
            //    File.Create(logFile);
            //    TextWriter tw = new StreamWriter(logFile);
            //    foreach (string item in lbStatus.Items)
            //    {
            //        tw.WriteLine(item);
            //    }
            //    tw.Close();
            //}
            //else if (File.Exists(logFile))
            //{
            //    using (var tw = new StreamWriter(logFile, true))
            //    {
            //        tw.WriteLine();
            //        tw.WriteLine("============================");
            //        tw.WriteLine();
            //        foreach (string item in lbStatus.Items)
            //        {
            //            tw.WriteLine(item);
            //        }
            //        tw.Close();
            //    }
            //}
        }

        private void fMain_Shown(object sender, EventArgs e)
        {
            
            this.Refresh();
            if (bAutoRun)
            {
                getMeets();
            }
        }

        private void dTPFrom_ValueChanged(object sender, EventArgs e)
        {
            //fromDate = dTPFrom.Value;
            //txtFromDate.Text = fromDate.ToString("yyyy-MM-dd");
            fromDate = DateTime.Parse(dTPFrom.Value.ToString()); 
            txtFromDate.Text = dTPFrom.Value.ToString("yyyy-MM-dd");
            calcToDate();
        }

        private void btnCurrPartialResults_Click(object sender, EventArgs e)
        {
            bStatusFound = false;
            bNormalStatus = false;
            getMeets();
            if (!bStatusFound)
            {;
                txtStatus.Text = txtResStatus.Text + " not found!";
            }
        }
        private bool IsStatusMet(string meetingStage, string trial)
        {
            bool retVal = false;
            if(bNormalStatus)
            {
                retVal = (meetingStage.ToUpper() == "FIELDS" && trial.ToUpper() == "FALSE");
            }
            else
            {
               // retVal = (meetingStage == "PARTIAL_RESULTS");
                retVal = (meetingStage == txtResStatus.Text.ToUpper ());
                if (retVal) bStatusFound = true;
            }
            return retVal;
        }
    }
}
