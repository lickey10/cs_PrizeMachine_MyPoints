using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Collections;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Automation;
using System.Threading;
using System.Reflection;
using System.Management;
using System.Linq;

namespace SCTV
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1301:AvoidDuplicateAccelerators")]
    public partial class MainForm : Form
    {
        private bool loggedIn = false;
        public static string[] blockedTerms;
        public static string[] foundBlockedTerms;
        public static string[] foundBlockedSites;
        public static string blockedTermsPath = "config\\BlockedTerms.txt";
        public static string foundBlockedTermsPath = "config\\FoundBlockedTerms.txt";
        public static string[] blockedSites;
        public static string blockedSitesPath = "config\\BlockedSites.txt";
        public static string foundBlockedSitesPath = "config\\foundBlockedSites.txt";
        public static string loginInfoPath = "config\\LoginInfo.txt";
        public bool adminLock = false;//locks down browser until unlocked by a parent
        public int loggedInTime = 0;
        public bool checkForms = true;
        public bool MonitorActivity = false; //determines whether safesurf monitors page contents, forms, sites, etc...
        int loginMaxTime = 20;//20 minutes
        TabCtlEx tabControlEx = new TabCtlEx();

        bool showVolumeControl = false;
        bool showAddressBar = true;

        private DateTime startTime;
        private string userName;
        string documentString = "";
        public string documentStringLoaded = "";
        bool enterTheContest = false;
        int counterCashstravaganza = 0;
        int counterUnclaimedPrizes = 0;
        string[] videos = null;
        int currentVideoIndex = -1;
        ArrayList videosList = new ArrayList();
        string currentVideoNumberString = "";
        string currentVideoNumberStringMyPoints = "";
        int secondsCounter = 0;
        ExtendedWebBrowser hideMeBrowser;
        ExtendedWebBrowser myPointsBrowser;
        bool foundCategory = false;
        bool foundVideo = false;
        bool watchingVideo = false;
        //bool foundCategoryMyPoints = false;
        //bool foundVideoMyPoints = false;
        //bool watchingVideoMyPoints = false;
        int errorCount = 0;
        int errorCountMax = 5;
        string prevVideoNumberString = "";
        string[] categories = null;
        int currentCategoryIndexMyPoints = 0;
        int currentCategoryIndex = 0;
        IntPtr hWnd = IntPtr.Zero;
        string goToUrlString = "";
        double refreshSpeed = 1;//this is times the seconds that are set for a timer
        HtmlElement elementToClick = null;
        string[] myPointsCategories = null;
        RefreshUtilities.RefreshUtilities refreshUtilities;
        RefreshUtilities.FirstRun firstRun = new RefreshUtilities.FirstRun();
        bool foundNextVideo = false;
        ArrayList playlist = new ArrayList();
        int isWatchedTries = 0;//number of times we have checked to see if the current video is watched
        bool lookForVideos = false;
        int playlistCount = 0;
        string startURL = "https://www.mypoints.com/videos";
        string refererURL = "https://www.mypoints.com?rb=40968701";
        //bool firstTimeAppHasRun = true;
        Random rnd = new Random();
        DateTime dtPlaylistCounted = DateTime.Now;

        //[DllImport("User32.dll")]
        //static extern bool SetForegroundWindow(IntPtr hWnd);

        //[DllImport("user32.dll")]
        //static extern bool IsIconic(IntPtr hWnd);

        //[DllImport("user32.dll")]
        //static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        public Uri URL
        {
            set { _windowManager.ActiveBrowser.Url = value; }
        }

        public bool ShowMenuStrip
        {
            set { this.menuStrip.Visible = value; }
        }

        public FormBorderStyle FormBorder
        {
            set { this.FormBorderStyle = value; }
        }

        public bool ShowLoginButton
        {
            set { LoginToolStripButton.Visible = value; }
        }
        
        public HtmlDocument SetDocument
        {
            set
            {
                if (value.Url.ToString().ToLower().Contains("https://www.mypoints.com/videos/video/"))
                {
                    string currentVideoNumberString = findValue(value.Url.ToString(), "https://www.mypoints.com/videos/video/", "/");

                    if (currentVideoNumberString.Trim().Length == 0)
                        currentVideoNumberString = prevVideoNumberString;

                    if (isCurrentVideoWatched(value, currentVideoNumberString) || isWatchedTries > 8)
                    {
                        if (!foundNextVideo || isWatchedTries > 8)
                            foundNextVideo = getNextVideo(value.Body.InnerHtml);
                    }

                    if (!refreshUtilities.IsActive)
                    {
                        lblStatus.Text = "Watching video";
                        refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 4, 0, lblRefreshTimer, myPointsBrowser);
                    }
                }
                else if (value.Url.ToString().ToLower().Contains("https://www.mypoints.com/videos/category/") && !lookForVideos)
                {
                    if (!foundVideo)
                        foundVideo = iterateVideoCards(value.Body.InnerHtml);

                    if (!foundVideo)
                    {
                        lblStatus.Text = "Waiting to look for playlist again";
                        refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 3, lblRefreshTimer, myPointsBrowser);
                    }
                }
                else if (!foundCategory && (myPointsBrowser.Url.ToString().ToLower() == "https://www.mypoints.com/videos" || myPointsBrowser.Url.ToString().ToLower() == "https://www.mypoints.com/videos/"))//watch home page - find a playlist to start on
                    _windowManager_DocumentCompleted(value, null);
                else if (lookForVideos)
                {
                    getNextVideo(value.Body.InnerHtml);
                    lookForVideos = false;
                }
                else
                {
                    lblStatus.Text = "Waiting...";
                    refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 4, 0, lblRefreshTimer, myPointsBrowser);
                }
            }
        }

        //[DllImport("user32.dll")]
        //public static extern IntPtr FindWindow(string strClassName, string strWindowName);

            //[DllImport("user32.dll", SetLastError = true)]
            //public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

            //public static void Run(IntPtr windowHandle)
            //{
            //    uint appID = Application. .GetAppIDByWindow(windowHandle);
            //    BFS.Audio.SetApplicationMute(appID, !BFS.Audio.GetApplicationMute(appID));
            //}

        public MainForm()
        {
            InitializeComponent();

            try
            {
                //Process myPointsVideos = Process.Start("chrome.exe", "https://www.mypoints.com/videos");

                //SetForegroundWindow(myPointsVideos.MainWindowHandle);//bring window to front



                //// open in Internet Explorer
                //Process.Start("iexplore", @"http://www.stackoverflow.net/");

                //// open in Firefox
                //Process.Start("firefox", @"http://www.stackoverflow.net/");
                
                firstRun.FirstTimeAppHasRun = firstRun.IsThisFirstRunOnThisPC();

                if (firstRun.FirstTimeAppHasRun)
                {
                    startURL = refererURL;

                    StartInstructions startInstructions = new StartInstructions();
                    startInstructions.Show(this);
                }

                useLatestIE();
                
                tabControlEx.Name = "tabControlEx";
                tabControlEx.SelectedIndex = 0;
                tabControlEx.Visible = false;
                tabControlEx.OnClose += new TabCtlEx.OnHeaderCloseDelegate(tabEx_OnClose);
                tabControlEx.VisibleChanged += new System.EventHandler(this.tabControlEx_VisibleChanged);

                this.panel1.Controls.Add(tabControlEx);
                tabControlEx.Dock = DockStyle.Fill;

                _windowManager = new WindowManager(tabControlEx);
                _windowManager.CommandStateChanged += new EventHandler<CommandStateEventArgs>(_windowManager_CommandStateChanged);
                _windowManager.StatusTextChanged += new EventHandler<TextChangedEventArgs>(_windowManager_StatusTextChanged);
                _windowManager.DocumentCompleted += _windowManager_DocumentCompleted;
                //_windowManager.ActiveBrowser.Navigating += ActiveBrowser_Navigating;
                //_windowManager.ActiveBrowser.ScriptErrorsSuppressed = true;
                _windowManager.ShowAddressBar = showAddressBar;

                //startTime = DateTime.Now;
                //userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

                //initFormsConfigs();


                ////load blocked terms
                //loadBlockedTerms(blockedTermsPath);

                ////load blocked sites
                //loadBlockedSites(blockedSitesPath);

                ////load found blocked terms
                //loadFoundBlockedTerms(foundBlockedTermsPath);

                ////load found blocked sites
                //loadFoundBlockedSites(foundBlockedSitesPath);


                //getDefaultBrowser();

            }
            catch (Exception ex)
            {
                //Application.Restart();
                throw;
            }
        }
                
        // Starting the app here...
        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                // Open a new browser window

                //hideMeBrowser = _windowManager.New(false);
                //hideMeBrowser.Url = new Uri("https://us.hideproxy.me/index.php");

                myPointsBrowser = this._windowManager.New();
                myPointsBrowser.Url = new Uri(startURL);
                myPointsBrowser.ScriptErrorsSuppressed = true;
                myPointsBrowser.ObjectForScripting = new MyScript();
                myPointsBrowser.Navigating += MyPointsBrowser_Navigating;
                
                refreshUtilities = new RefreshUtilities.RefreshUtilities();
                refreshUtilities.ClickComplete += RefreshUtilities_ClickComplete;
                refreshUtilities.GoToUrlComplete += RefreshUtilities_GoToUrlComplete;
                refreshUtilities.CallMethodComplete += RefreshUtilities_CallMethodComplete;
                refreshUtilities.Error += RefreshUtilities_Error;
            }
            catch (Exception ex)
            {
                //Application.Restart();
                throw;
            }            
        }

        private void RefreshUtilities_Error(object sender, EventArgs e)
        {
            RestartApp();
        }
        
        private void MyPointsBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            hideScriptErrors();
        }

        private void RefreshUtilities_CallMethodComplete(object sender, EventArgs e)
        {
            if(((RefreshUtilities.TimerInfo)sender).MethodToCall == "RestartApp")
                RestartApp();
        }

        private void RefreshUtilities_GoToUrlComplete(object sender, EventArgs e)
        {
            if (chbAutoRefresh.Checked)
            {
                if (sender != null && sender is RefreshUtilities.TimerInfo && ((RefreshUtilities.TimerInfo)sender).Browser is ExtendedWebBrowser)
                {
                    ExtendedWebBrowser tempBrowser = (ExtendedWebBrowser)((RefreshUtilities.TimerInfo)sender).Browser;

                    if (tempBrowser.IsBusy)
                        tempBrowser.Stop();

                    tempBrowser.Url = new Uri(((RefreshUtilities.TimerInfo)sender).UrlToGoTo);
                    
                    foundNextVideo = false;
                }

                //refreshUtilities.CallMethod("RestartApp", 500, lblRefreshTimer);
            }
        }

        private void RefreshUtilities_ClickComplete(object sender, EventArgs e)
        {
            if (chbAutoRefresh.Checked)
            {
                if (foundVideo && myPointsBrowser.Url.ToString().ToLower().Contains("javascript"))//go to page url again
                {
                    foundVideo = false;
                    
                    if (!foundNextVideo)
                    {
                        lookForVideos = true;
                        lblStatus.Text = "Looking for next video";
                        refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 3, 0, lblRefreshTimer, myPointsBrowser);
                    }
                }
                else if (foundVideo && !watchingVideo && myPointsBrowser.Url.ToString().ToLower().Contains("https://www.mypoints.com/videos/category/"))//video playlist
                {
                    //lblWatched.Text = "Not Watched";
                    //foundCategory = false;
                    //foundVideo = false;
                    playlist.Clear();

                    if (documentString.Trim().Length > 0)
                    {
                        foundVideo = iterateVideoCards(documentString);

                        if (!foundVideo)
                        {
                            if (dtPlaylistCounted != null)
                            {
                                TimeSpan elapsedTime = DateTime.Now - dtPlaylistCounted;

                                if (elapsedTime.Minutes > 20)
                                {
                                    RestartApp();

                                    return;
                                }
                            }

                            lblStatus.Text = "Looking for next playlist";
                            refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 5, 0, true, lblRefreshTimer, myPointsBrowser);
                        }
                    }
                }

                //refreshUtilities.CallMethod("RestartApp", 500, lblRefreshTimer);
            }
        }

        private void ActiveBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            documentString = "";
        }
        
        private void _windowManager_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            try
            {
                string originalPageURL = "";
                documentString = _windowManager.ActiveBrowser.DocumentText;

                //my points
                if (myPointsBrowser != null && myPointsBrowser.Url != null && chbAutoRefresh.Checked)
                {
                    if (sender is string && e == null)//this is from the late load
                        documentString = (string)sender;

                    if (sender is HtmlDocument)
                    {
                        documentString = ((HtmlDocument)sender).Body.InnerHtml;

                        originalPageURL = ((HtmlDocument)sender).Url.ToString();

                        currentVideoNumberStringMyPoints = findValue(originalPageURL, "https://www.mypoints.com/videos/video/", "/");
                    }
                    else
                    {
                        originalPageURL = "";

                        currentVideoNumberStringMyPoints = findValue(myPointsBrowser.Url.ToString(), "https://www.mypoints.com/videos/video/", "/");
                    }

                    if (!foundCategory && (myPointsBrowser.Url.ToString().ToLower() == "https://www.mypoints.com/videos" || myPointsBrowser.Url.ToString().ToLower() == "https://www.mypoints.com/videos/"))//watch home page - find a playlist to start on
                    {
                        foundNextVideo = false;
                        foundVideo = false;
                        playlist.Clear();

                        if (documentString.Trim().Length > 0)
                        {
                            foundCategory = findNextCategory(documentString);
                            documentString = "";

                            if (!foundCategory)
                            {
                                lblStatus.Text = "Looking for next category";
                                refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 2, 0, lblRefreshTimer, myPointsBrowser);
                            }
                        }
                    }
                    else if (!foundVideo && !watchingVideo && myPointsBrowser.Url.ToString().ToLower().Contains("https://www.mypoints.com/videos/category/"))//video playlist
                    {
                        lblWatched.Text = "Not Watched";
                        foundCategory = false;
                        playlist.Clear();
                        
                        if (documentString.Trim().Length > 0)
                        {
                            foundVideo = iterateVideoCards(documentString);
                            
                            if (!foundVideo)
                            {
                                if (dtPlaylistCounted != null)
                                {
                                    TimeSpan elapsedTime = DateTime.Now - dtPlaylistCounted;

                                    if (elapsedTime.Minutes > 20)
                                    {
                                        RestartApp();

                                        return;
                                    }
                                }

                                lblStatus.Text = "Looking for next playlist";
                                refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 5, 0, true, lblRefreshTimer, myPointsBrowser);
                            }
                        }
                    }
                    else if (currentVideoNumberStringMyPoints.Trim().Length > 0 && prevVideoNumberString != currentVideoNumberStringMyPoints && myPointsBrowser != null && (myPointsBrowser.Url.ToString().ToLower().Contains("https://www.mypoints.com/videos/video/") || originalPageURL.ToLower().Contains("https://www.mypoints.com/videos/video/")))//we are watching a video
                    {
                        foundVideo = false;
                        foundNextVideo = false;
                        lblWatched.Text = "Not Watched";
                        isWatchedTries = 0;

                        if (firstRun.FirstTimeAppHasRun)
                        {
                            firstRun.FirstTimeAppHasRun = false;
                            firstRun.IsThisFirstRunOnThisPC();
                        }

                        prevVideoNumberString = currentVideoNumberStringMyPoints;

                        //playlist.Clear();
                        lblStatus.Text = "Waiting for page and video to load";
                        //check to see if this video is watched after a given time
                        refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 27, true, lblRefreshTimer, myPointsBrowser);
                    }
                    else if ((myPointsBrowser.Url.ToString() == "javascript: window.external.CallServerSideCode();" && myPointsBrowser.Url.ToString().ToLower().Contains("https://www.mypoints.com/videos/video/") || originalPageURL.ToLower().Contains("https://www.mypoints.com/videos/video/")) && !refreshUtilities.IsActive)
                    {
                        prevVideoNumberString = "";

                    }
                    else if ((myPointsBrowser.Url.ToString() == "javascript: window.external.CallServerSideCode();" && myPointsBrowser.Url.ToString().ToLower().Contains("https://www.mypoints.com/videos/category/") || originalPageURL.ToLower().Contains("https://www.mypoints.com/videos/category")))
                    {
                        if (!foundNextVideo && documentString.Trim().Length > 0)
                            foundNextVideo = getNextVideo(documentString);
                    }
                    else if(!myPointsBrowser.Document.Url.ToString().ToLower().Contains(refererURL.ToLower()))//don't start timer on sign up page
                    {
                        refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 1, 0, lblRefreshTimer, myPointsBrowser);
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private bool isCurrentVideoWatched(HtmlDocument pageDocument, string currentVideoNumber)
        {
            HtmlElementCollection elc = pageDocument.GetElementsByTagName("div");

            foreach (HtmlElement el in elc)
            {
                if (el.Id == "video_"+ currentVideoNumber)//this is current video check to see if it is watched
                {
                    string classString = findValue(el.OuterHtml.ToLower(), "class=\"video-tile", "\"");
                    //if (el.OuterHtml.ToLower().Contains("class=\"video-tile watching watched"))//this video is watched
                    //if (classString.Trim().Length > 0 && classString.Contains("watching") && classString.Contains("watched"))//this video is watched
                    if (classString.Trim().Length > 0 && classString.Contains("watched"))//this video is watched
                    {
                        string urlToRemove = "";

                        foreach(string url in playlist)
                        {
                            if(url.Contains("/"+ currentVideoNumber +"/"))
                            {
                                urlToRemove = url;
                                break;
                            }
                        }

                        playlist.Remove(urlToRemove);
                        //playlist.Remove(pageDocument.Url.ToString());//remove the currently playing video from the playlist

                        lblWatched.Text = "Watched";
                        isWatchedTries = 0;
                        foundNextVideo = false;
                        return true;
                    }
                    if (el.OuterHtml.ToLower().Contains("class=\"video-tile\""))//this video is not watched
                    {
                        lblWatched.Text = "Not Watched";
                        isWatchedTries++;
                        return false;
                    }
                }
            }

            //lblWatched.Text = "Not Watched";
            isWatchedTries++;
            return false;
        }
        
        private bool findNextCategory(string pageContent)
        {
            try
            {
                lblStatus.Text = "Looking for next Category";

                if (myPointsCategories == null || myPointsCategories.Length == 0)
                {
                    string splitString = "<li";

                    splitString = "<option ";
                    currentCategoryIndexMyPoints = 0;

                    //get category string
                    string tempPageContent = findValue(pageContent, "Videos Home</option>", "</select>", false).Trim();
                    tempPageContent = tempPageContent.Replace("<option disabled=\"\">─────────────</option>", "");

                    myPointsCategories = tempPageContent.Split(new string[] { splitString }, StringSplitOptions.RemoveEmptyEntries);
                }

                if (myPointsCategories.Length > 0)
                {
                    //randomize category selection
                    int rndCategoryIndex = rnd.Next(1, myPointsCategories.Length - 1);

                    string category = myPointsCategories[rndCategoryIndex];

                    //get url and go
                    string categoryValue = findValue(category, " value=\"", "\"").Trim();
                    string categoryName = findValue(category, "\">", "</").Trim();
                    categoryName = categoryName.Replace("&amp;", "-");
                    categoryName = categoryName.Replace(" & ", "-");
                    categoryName = categoryName.Replace("&", "-");
                    categoryName = categoryName.Replace(" ", "");

                    if (categoryValue.Trim().Length > 0)
                    {
                        foundCategory = true;
                        watchingVideo = false;
                        foundVideo = false;

                        string newURL = "https://www.mypoints.com/videos/category/" + categoryValue + "/" + categoryName.ToLower();

                        refreshUtilities.GoToURL(newURL, true, lblRefreshTimer, myPointsBrowser);
                        lblStatus.Text = "Going to next Category";

                        return true;
                    }
                }
                //else
                //    refreshUtilities.GoToURL("https://www.mypoints.com/videos", 10, lblRefreshTimer, myPointsBrowser);

                return false;
            }
            catch (Exception ex)
            {
                throw;
            }            
        }

        private bool iterateVideoCards(string pageContent)
        {
            try
            {
                string splitString = "<section class=";
                string videoURL = "";
                bool watched = true;
                string watchedString = "";
                string selectedVideoIDString = "";
                string videoPointString = "";
                double videoPoints = 0;
                string numVideosString = "";
                double numVideos = 0;
                double currentSelectedVideoPoints = 0;
                double tempSelectedVideoPoints = 0;

                foundCategory = false;

                lblStatus.Text = "Looking for Playlist";

                //find best choice video
                //then find the html object and click it

                if (pageContent.Contains("<div id=\"playlistsContainer\">"))
                {
                    string tempContent = pageContent.Substring(pageContent.IndexOf("<div id=\"playlistsContainer\">"));

                    string[] cards = tempContent.Split(new string[] { splitString }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string card in cards)
                    {
                        numVideosString = findValue(card, "<span class=\"pull-left\">", " videos");
                        double.TryParse(numVideosString, out numVideos);

                        videoPointString = findValue(card, "data-earn=\"", " PT");
                        double.TryParse(videoPointString, out videoPoints);

                        tempSelectedVideoPoints = videoPoints / numVideos;

                        watched = card.ToLower().Contains("<div class=\"watched\"></div>");

                        if (tempSelectedVideoPoints > currentSelectedVideoPoints && !watched)
                        {
                            selectedVideoIDString = findValue(card, " id=\"", "\"");
                            currentSelectedVideoPoints = tempSelectedVideoPoints;
                        }
                    }

                    if (selectedVideoIDString.Trim().Length > 0)
                    {
                        HtmlElementCollection elc = this.myPointsBrowser.Document.GetElementsByTagName("a");

                        foreach (HtmlElement el in elc)
                        {
                            if (el.GetAttribute("id").Equals(selectedVideoIDString))//this is selected video - click it
                            {
                                lblStatus.Text = "Going to new Playlist";

                                refreshUtilities.ClickElement(el, true, lblRefreshTimer);
                                return true;
                            }
                        }
                    }
                    else//didn't find any unwatched playlists - find new category
                    {
                        foundVideo = false;
                        watchingVideo = false;
                        lblStatus.Text = "Going to Videos main page";
                        refreshUtilities.GoToURL("https://www.mypoints.com/videos", 2, true, lblRefreshTimer, myPointsBrowser);

                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                //Application.Restart();
                throw;
            }            
        }

        private bool getNextVideo(string pageContent)
        {
            try
            {
                string splitString = "url:";
                string videoURL = "";
                string currentVideo = "";
                string videoNumber = "";
                foundCategory = false;

                //check to see if current video is watched
                bool watched = false;
                string tempWatchedString = "";
                lblStatus.Text = "Looking for next Video";

                if (playlist.Count == 0 && pageContent.ToLower().Contains("id=\"videostrayinner\""))
                {
                    splitString = "<div class=\"video-slide\">";
                    videos = pageContent.Split(new string[] { splitString }, StringSplitOptions.RemoveEmptyEntries);
                    //bool foundNextVideo = false;
                    string videoTitle = "";

                    foreach (string video in videos)
                    {
                        //https://www.mypoints.com/videos/video/303541799/oldest-yoga-teacher

                        videoURL = findValue(video, "data-value=\"", "\"");
                        videoTitle = findValue(video, "title=\"", "\"");
                        videoTitle = videoTitle.Replace(" ", "-");
                        videoTitle = videoTitle.Replace(":", "-");
                        videoTitle = videoTitle.Replace("'", "");
                        videoTitle = videoTitle.Replace(",", "-");
                        videoTitle = videoTitle.Replace("?", "-");
                        videoTitle = videoTitle.Replace("\"", "-");
                        videoTitle = videoTitle.Replace("&", "-");
                        videoTitle = videoTitle.Replace("$", "-");
                        videoTitle = videoTitle.Replace("@", "-");
                        videoTitle = videoTitle.Replace("*", "-");
                        videoTitle = videoTitle.Replace("%", "-");
                        videoTitle = videoTitle.Replace("!", "-");
                        videoTitle = videoTitle.Replace("#", "-");
                        videoTitle = videoTitle.Replace("_", "-");
                        videoTitle = videoTitle.Replace(".", "");
                        videoTitle = videoTitle.Replace("`", "");
                        videoTitle = videoTitle.Replace("+", "-");
                        videoTitle = videoTitle.Replace("=", "-");
                        videoTitle = videoTitle.Replace("!", "-");
                        videoTitle = videoTitle.Replace("~", "-");
                        videoTitle = videoTitle.Replace(">", "-");
                        videoTitle = videoTitle.Replace("<", "-");

                        while (videoTitle.Contains("--"))
                            videoTitle = videoTitle.Replace("--", "-");

                        while (videoTitle.StartsWith("-"))
                            videoTitle = videoTitle.Substring(1);

                        while (videoTitle.EndsWith("-"))
                            videoTitle = videoTitle.Substring(0, videoTitle.Length - 1);

                        tempWatchedString = findValue(video.ToLower(), "class=\"video-tile", "\"");

                        watched = tempWatchedString.ToLower().Contains("watched");

                        if (videoURL.Trim().Length > 0 && videoTitle.Trim().Length > 0 && !watched)
                        {
                            videoURL = "https://www.mypoints.com/videos/video/" + videoURL + "/" + videoTitle;

                            if (!myPointsBrowser.Url.ToString().ToLower().Contains(videoTitle.ToLower()))//keep out the current video
                                playlist.Add(videoURL);
                        }
                    }
                }
                
                if (playlist.Count > 0 && !foundNextVideo)
                {
                    refreshUtilities.GoToURL((string)playlist[0],8,true,lblRefreshTimer,myPointsBrowser);
                    isWatchedTries = 0;
                    lblStatus.Text = "Going to next video";

                    return true;
                }
                else
                {
                    if(!foundCategory)
                        foundCategory = findNextCategory(pageContent);

                    if (videos != null && (videos.Length > 1 && playlist.Count == 0))
                    {
                        playlistCount++;
                        lblPlaylistCount.Text = playlistCount.ToString();
                        dtPlaylistCounted = DateTime.Now;

                        if (playlistCount > 5)
                        {
                            RestartApp();
                        }
                        //else
                        //    refreshUtilities.CallMethod("RestartApp", 500, lblRefreshTimer);
                    }

                    return foundCategory;
                }
            }
            catch (Exception ex)
            {
                //Application.Restart();
                throw;
            }       
        }

        private bool getNextVideo(HtmlDocument pageDocument, string currentVideoNumber)
        {
            try
            {
                bool foundCurrentVideo = false;
                HtmlElementCollection elc = pageDocument.GetElementsByTagName("div");

                foreach (HtmlElement el in elc)
                {
                    if (foundCurrentVideo && el.Id != null && el.Id.Contains("video_"))
                    {
                        getNextVideo(el.OuterHtml + el.InnerHtml);

                        return true;
                    }

                    if (el.Id == "video_" + currentVideoNumber)//this is current video
                    {
                        foundCurrentVideo = true;

                        break;
                    }
                }

                return false;


                //string splitString = "url:";
                //string videoURL = "";
                //string currentVideo = "";
                //string videoNumber = "";

                ////check to see if current video is watched
                //bool watched = false;
                //string tempVideoString = "";

                //if (playlist.Count == 0 && pageDocument.ToLower().Contains("id=\"videostrayinner\""))
                //{
                //    splitString = "<div class=\"video-slide\">";
                //    videos = pageDocument.Split(new string[] { splitString }, StringSplitOptions.RemoveEmptyEntries);
                //    bool foundNextVideo = false;
                //    string videoTitle = "";

                //    foreach (string video in videos)
                //    {
                //        //https://www.mypoints.com/videos/video/303541799/oldest-yoga-teacher

                //        videoURL = findValue(video, "data-value=\"", "\"");
                //        videoTitle = findValue(video, "title=\"", "\"");
                //        videoTitle = videoTitle.Replace(" ", "-");

                //        videoURL = "https://www.mypoints.com/videos/video/" + videoURL + "/" + videoTitle;

                //        if (!myPointsBrowser.Url.ToString().ToLower().Contains(videoTitle.ToLower()))//keep out the current video
                //            playlist.Add(videoURL);
                //    }

                //    //if (playlist.Count > 0)
                //    //    playlist.RemoveAt(0);//remove the first one.  it's the one we are watching now
                //}

                //if (playlist.Count > 0)
                //{
                //    refreshUtilities.Cancel();
                //    refreshUtilities.GoToURL((string)playlist[0], 30, 10, lblRefreshTimer, myPointsBrowser);

                //    playlist.RemoveAt(0);
                //}
                ////else
                ////{
                ////    findNextCategory(pageContent);
                ////}

                //return false;
            }
            catch (Exception ex)
            {
                //Application.Restart();
                throw;
            }
        }

        public void hideScriptErrors()
        {
            bool hide = true;

            //FieldInfo fiComWebBrowser = typeof(WebBrowser).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
            //if (fiComWebBrowser == null) return;
            //object objComWebBrowser = fiComWebBrowser.GetValue(wb);

            //if (objComWebBrowser == null) return;

            myPointsBrowser.axIWebBrowser2.GetType().InvokeMember(
            "Silent", BindingFlags.SetProperty, null, myPointsBrowser.axIWebBrowser2, new object[] { hide });

            //objComWebBrowser.GetType().InvokeMember(
            //"Silent", BindingFlags.SetProperty, null, objComWebBrowser, new object[] { Hide });
        }

        private void clickButton()
        {
            //int iHandle = NativeWin32.FindWindow(null, "Security Alert");
            //NativeWin32.SetForegroundWindow(iHandle);
            //System.Windows.Forms.SendKeys.Send("Y%");
        }

        protected void RestartApp()
        {
            try
            {
                if ((components != null))
                {
                    components.Dispose();
                }
                base.Dispose(true);
            }
            catch (Exception ex)
            {
                
            }
            

            Application.Restart();
        }

        private void initFormsConfigs()
        {
            SettingsHelper helper = SettingsHelper.Current;

            checkForms = helper.CheckForms;
        }

        private void useLatestIE()
        {
            try
            {
                string AppName = Application.ProductName;// My.Application.Info.AssemblyName
                int VersionCode = 0;
                string Version = "";
                object ieVersion = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Internet Explorer").GetValue("svcUpdateVersion");

                if (ieVersion == null)
                    ieVersion = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Internet Explorer").GetValue("Version");

                if (ieVersion != null)
                {
                    Version = ieVersion.ToString().Substring(0, ieVersion.ToString().IndexOf("."));
                    switch (Version)
                    {
                        case "7":
                            VersionCode = 7000;
                            break;
                        case "8":
                            VersionCode = 8888;
                            break;
                        case "9":
                            VersionCode = 9999;
                            break;
                        case "10":
                            VersionCode = 10001;
                            break;
                        default:
                            if (int.Parse(Version) >= 11)
                                VersionCode = 11001;
                            //else
                                //Tools.WriteToFile(Tools.errorFile, "useLatestIE error: IE Version not supported");
                            break;
                    }
                }
                else
                {
                    //Tools.WriteToFile(Tools.errorFile, "useLatestIE error: Registry error");
                }

                //'Check if the right emulation is set
                //'if not, Set Emulation to highest level possible on the user machine
                string Root = "HKEY_CURRENT_USER\\";
                string Key = "Software\\Microsoft\\Internet Explorer\\Main\\FeatureControl\\FEATURE_BROWSER_EMULATION";
                
                object CurrentSetting = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Key).GetValue(AppName + ".exe");

                if (CurrentSetting == null || int.Parse(CurrentSetting.ToString()) != VersionCode)
                {
                    Microsoft.Win32.Registry.SetValue(Root + Key, AppName + ".exe", VersionCode);
                    Microsoft.Win32.Registry.SetValue(Root + Key, AppName + ".vshost.exe", VersionCode);
                }
            }
            catch (Exception ex)
            {
               // Tools.WriteToFile(Tools.errorFile, "useLatestIE error: "+ ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        // Update the status text
        void _windowManager_StatusTextChanged(object sender, TextChangedEventArgs e)
        {
            this.toolStripStatusLabel.Text = e.Text;
        }

        // Enable / disable buttons
        void _windowManager_CommandStateChanged(object sender, CommandStateEventArgs e)
        {
            this.forwardToolStripButton.Enabled = ((e.BrowserCommands & BrowserCommands.Forward) == BrowserCommands.Forward);
            this.backToolStripButton.Enabled = ((e.BrowserCommands & BrowserCommands.Back) == BrowserCommands.Back);
            this.printPreviewToolStripButton.Enabled = ((e.BrowserCommands & BrowserCommands.PrintPreview) == BrowserCommands.PrintPreview);
            this.printPreviewToolStripMenuItem.Enabled = ((e.BrowserCommands & BrowserCommands.PrintPreview) == BrowserCommands.PrintPreview);
            this.printToolStripButton.Enabled = ((e.BrowserCommands & BrowserCommands.Print) == BrowserCommands.Print);
            this.printToolStripMenuItem.Enabled = ((e.BrowserCommands & BrowserCommands.Print) == BrowserCommands.Print);
            this.homeToolStripButton.Enabled = ((e.BrowserCommands & BrowserCommands.Home) == BrowserCommands.Home);
            this.searchToolStripButton.Enabled = ((e.BrowserCommands & BrowserCommands.Search) == BrowserCommands.Search);
            this.refreshToolStripButton.Enabled = ((e.BrowserCommands & BrowserCommands.Reload) == BrowserCommands.Reload);
            this.stopToolStripButton.Enabled = ((e.BrowserCommands & BrowserCommands.Stop) == BrowserCommands.Stop);
        }

        #region Tools menu
        // Executed when the user clicks on Tools -> Options
        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OptionsForm of = new OptionsForm())
            {
                of.ShowDialog(this);
            }
        }

        // Tools -> Show script errors
        private void scriptErrorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ScriptErrorManager.Instance.ShowWindow();
        }

        private void UpdateLoginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (Login login = new Login())
            {
                login.Update = true;
                login.ShowDialog(this);
            }
        }

        private void modifyBlockedTermsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //display terms
            tcAdmin.Visible = true;
            tcAdmin.BringToFront();

            tcAdmin.SelectedTab = tcAdmin.TabPages["tpChangeLoginInfo"];
        }

        private void modifyBlockedSitesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tcAdmin.Visible = true;
            tcAdmin.BringToFront();
            tcAdmin.SelectedTab = tcAdmin.TabPages["tpBlockedSites"];
        }

        private void foundBlockedTermsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tcAdmin.Visible = true;
            tcAdmin.BringToFront();
            tcAdmin.SelectedTab = tcAdmin.TabPages["tpFoundBlockedTerms"];
        }

        private void foundBlockedSitesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tcAdmin.Visible = true;
            tcAdmin.BringToFront();
            tcAdmin.SelectedTab = tcAdmin.TabPages["tpFoundBlockedSites"];
        }
        #endregion

        #region File Menu

        // File -> Print
        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Print();
        }

        // File -> Print Preview
        private void printPreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PrintPreview();
        }

        // File -> Exit
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // File -> Open URL
        private void openUrlToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenUrlForm ouf = new OpenUrlForm())
            {
                if (ouf.ShowDialog() == DialogResult.OK)
                {
                    ExtendedWebBrowser brw = _windowManager.New(false);
                    brw.Navigate(ouf.Url);
                }
            }
        }

        // File -> Open File
        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = Properties.Resources.OpenFileDialogFilter;
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Uri url = new Uri(ofd.FileName);
                    WindowManager.Open(url);
                }
            }
        }
        #endregion

        #region Help Menu

        // Executed when the user clicks on Help -> About
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About();
        }

        /// <summary>
        /// Shows the AboutForm
        /// </summary>
        private void About()
        {
            using (AboutForm af = new AboutForm())
            {
                af.ShowDialog(this);
            }
        }

        #endregion

        /// <summary>
        /// The WindowManager class
        /// </summary>
        public WindowManager _windowManager;

        // This is handy when all the tabs are closed.
        private void tabControlEx_VisibleChanged(object sender, EventArgs e)
        {
            if (tabControlEx.Visible)
            {
                this.panel1.BackColor = SystemColors.Control;
            }
            else
                this.panel1.BackColor = SystemColors.AppWorkspace;
        }

        #region Printing & Print Preview
        private void Print()
        {
            ExtendedWebBrowser brw = _windowManager.ActiveBrowser;
            if (brw != null)
                brw.ShowPrintDialog();
        }

        private void PrintPreview()
        {
            ExtendedWebBrowser brw = _windowManager.ActiveBrowser;
            if (brw != null)
                brw.ShowPrintPreviewDialog();
        }
        #endregion

        #region Toolstrip buttons
        private void closeWindowToolStripButton_Click(object sender, EventArgs e)
        {
            this._windowManager.New();
        }

        private void closeToolStripButton_Click(object sender, EventArgs e)
        {
            //closes browser window
            //this._windowManager.Close();

            //closes admin tabPages
            tcAdmin.Visible = false;
        }

        private void tabEx_OnClose(object sender, CloseEventArgs e)
        {
            //this.userControl11.Controls.Remove(this.userControl11.TabPages[e.TabIndex]);

            //closes browser window
            this._windowManager.Close();
        }

        private void printToolStripButton_Click(object sender, EventArgs e)
        {
            Print();
        }

        private void printPreviewToolStripButton_Click(object sender, EventArgs e)
        {
            PrintPreview();
        }

        private void backToolStripButton_Click(object sender, EventArgs e)
        {
            if (_windowManager.ActiveBrowser != null && _windowManager.ActiveBrowser.CanGoBack)
                _windowManager.ActiveBrowser.GoBack();
        }

        private void forwardToolStripButton_Click(object sender, EventArgs e)
        {
            if (_windowManager.ActiveBrowser != null && _windowManager.ActiveBrowser.CanGoForward)
                _windowManager.ActiveBrowser.GoForward();
        }

        private void stopToolStripButton_Click(object sender, EventArgs e)
        {
            if (_windowManager.ActiveBrowser != null)
            {
                _windowManager.ActiveBrowser.Stop();
            }
            stopToolStripButton.Enabled = false;
        }

        private void refreshToolStripButton_Click(object sender, EventArgs e)
        {
            if (_windowManager.ActiveBrowser != null)
            {
                _windowManager.ActiveBrowser.Refresh(WebBrowserRefreshOption.Normal);
            }
        }

        private void homeToolStripButton_Click(object sender, EventArgs e)
        {
            if (_windowManager.ActiveBrowser != null)
                _windowManager.ActiveBrowser.GoHome();
        }

        private void searchToolStripButton_Click(object sender, EventArgs e)
        {
            if (_windowManager.ActiveBrowser != null)
                _windowManager.ActiveBrowser.GoSearch();
        }

        #endregion

        public WindowManager WindowManager
        {
            get { return _windowManager; }
        }

        /// <summary>
        /// load blocked terms from file
        /// </summary>
        /// <param name="path"></param>
        public void loadBlockedTerms(string path)
        {
            blockedTerms = File.ReadAllLines(path);

            if (!validateBlockedTerms())
            {
                //decrypt terms
                blockedTerms = Encryption.Decrypt(blockedTerms);
            }

            if (!validateBlockedTerms())
            {
                //log that terms have been tampered with
                log(blockedTermsPath, "Blocked Terms file has been tampered with.  Reinstall SafeSurf");
                //block all pages
                adminLock = true;
            }

            dgBlockedTerms.Dock = DockStyle.Fill;
            dgBlockedTerms.Anchor = AnchorStyles.Right;
            dgBlockedTerms.Anchor = AnchorStyles.Bottom;
            dgBlockedTerms.Anchor = AnchorStyles.Left;
            dgBlockedTerms.Anchor = AnchorStyles.Top;
            dgBlockedTerms.Columns.Add("Terms", "Terms");
            dgBlockedTerms.Refresh();

            foreach (string term in blockedTerms)
            {
                dgBlockedTerms.Rows.Add(new string[] { term });
            }
        }

        private void loadBlockedSites(string path)
        {
            blockedSites = File.ReadAllLines(path);

            if (!validateBlockedSites())
            {
                //decrypt terms
                blockedSites = Encryption.Decrypt(blockedSites);
            }

            if (!validateBlockedSites())
            {
                //log that terms have been tampered with
                log(blockedSitesPath, "Blocked Sites file has been tampered with.  Reinstall SafeSurf");
                //block all pages
                adminLock = true;
            }

            dgBlockedSites.Dock = DockStyle.Fill;
            dgBlockedSites.Anchor = AnchorStyles.Right;
            dgBlockedSites.Anchor = AnchorStyles.Bottom;
            dgBlockedSites.Anchor = AnchorStyles.Left;
            dgBlockedSites.Anchor = AnchorStyles.Top;
            dgBlockedSites.Columns.Add("Sites", "Sites");

            foreach (string site in blockedSites)
            {
                dgBlockedSites.Rows.Add(new string[] { site });
            }
        }

        public void loadFoundBlockedTerms(string path)
        {
            string fBlockedTerms = "";

            if (File.Exists(path))
                foundBlockedTerms = File.ReadAllLines(path);

            if (foundBlockedTerms != null && foundBlockedTerms.Length > 0)
            {
                //if (!validateFoundBlockedTerms())
                //{
                //decrypt terms
                foundBlockedTerms = Encryption.Decrypt(foundBlockedTerms);
                //}

                if (!validateBlockedTerms())
                {
                    //log that terms have been tampered with
                    log(foundBlockedTermsPath, "Found Blocked Terms file has been tampered with.");
                    //block all pages
                    adminLock = true;
                }

                lbFoundBlockedTerms.DataSource = foundBlockedTerms;
            }
        }

        public void loadFoundBlockedSites(string path)
        {
            if (File.Exists(path))
                foundBlockedSites = File.ReadAllLines(path);

            if (foundBlockedSites != null && foundBlockedSites.Length > 0)
            {

                //if (!validateBlockedTerms())
                //{
                //decrypt terms
                foundBlockedSites = Encryption.Decrypt(foundBlockedSites);
                //}

                //if (!validateBlockedTerms())
                //{
                //    //log that terms have been tampered with
                //    log(blockedTermsPath, "Blocked Terms file has been tampered with.  Reinstall SafeSurf");
                //    //block all pages
                //    adminLock = true;
                //}

                lbFoundBlockedSites.DataSource = foundBlockedSites;
            }
        }

        private bool validateBlockedTerms()
        {
            bool isValid = false;

            foreach (string term in blockedTerms)
            {
                if (term.ToLower() == "fuck")
                {
                    isValid = true;
                    break;
                }
            }

            return isValid;
        }

        private bool validateBlockedSites()
        {
            bool isValid = false;

            foreach (string site in blockedSites)
            {
                if (site.ToLower() == "pussy.org")
                {
                    isValid = true;
                    break;
                }
            }

            return isValid;
        }

        private bool validateFoundBlockedTerms()
        {
            bool isValid = true;

            //foreach (string term in foundBlockedTerms)
            //{
            //    if (term.ToLower().Contains("fuck"))
            //    {
            //        isValid = true;
            //        break;
            //    }
            //}

            return isValid;
        }

        #region datagridview events
        private void dgBlockedTerms_RowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            //make sure values are valid
            //DataGridView dg = (DataGridView)sender;

        }

        private void dgBlockedTerms_RowValidated(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                //update blocked terms file
                ArrayList terms = new ArrayList();
                string value = "";
                DataGridView dg = (DataGridView)sender;
                foreach (DataGridViewRow row in dg.Rows)
                {
                    value = Convert.ToString(row.Cells["Terms"].Value);
                    if (value != null && value.Trim().Length > 0)
                        terms.Add(value);
                }

                blockedTerms = (string[])terms.ToArray(typeof(string));

                //encrypt
                blockedTerms = Encryption.Encrypt(blockedTerms);

                //save blockedTerms
                File.WriteAllLines(blockedTermsPath, blockedTerms);
            }
            catch (Exception ex)
            {

            }
        }
        #endregion

        private void logHeader(string path)
        {
            if (startTime.CompareTo(File.GetLastWriteTime(path)) == 1)
            {
                StringBuilder content = new StringBuilder();

                content.AppendLine();
                content.AppendLine("User: " + userName + "  Start Time: " + startTime);

                File.AppendAllText(path, Encryption.Encrypt(content.ToString()));
            }
        }

        public void log(string path, string content)
        {
            logHeader(path);

            File.AppendAllText(path, content);
        }

        public void log(string path, string[] content)
        {
            logHeader(path);

            File.WriteAllLines(path, content);
            //File.WriteAllText(path, content);
        }

        private void tcAdmin_VisibleChanged(object sender, EventArgs e)
        {
            closeToolStripButton.Visible = true;
        }
        
        private void btnChangePassword_Click(object sender, EventArgs e)
        {
            string[] loginInfo = { "username:" + txtNewUserName.Text.Trim(), "password:" + txtNewPassword.Text.Trim() };
            loginInfo = Encryption.Encrypt(loginInfo);
            File.WriteAllLines(MainForm.loginInfoPath, loginInfo);
            lblLoginInfoUpdated.Visible = true;
        }

        private void tpChangeLoginInfo_Leave(object sender, EventArgs e)
        {
            lblLoginInfoUpdated.Visible = false;
        }

        private string getDefaultBrowser()
        {
            //original value on classesroot
            //"C:\Program Files\Internet Explorer\IEXPLORE.EXE" -nohome

            string browser = string.Empty;
            RegistryKey key = null;
            try
            {
                key = Registry.ClassesRoot.OpenSubKey(@"HTTP\shell\open\command",true);

                //trim off quotes
                //browser = key.GetValue(null).ToString().Replace("\"", "");
                //if (!browser.EndsWith(".exe"))
                //{
                //    //get rid of everything after the ".exe"
                //    browser = browser.Substring(0, browser.ToLower().LastIndexOf(".exe") + 4);
                //}

                browser = key.GetValue(null).ToString();
                
                //key.SetValue(null, (string)@browser);

                string safeSurfBrowser = "\""+ Application.ExecutablePath +"\"";

                key.SetValue(null, (string)@safeSurfBrowser);
            }
            finally
            {
                if (key != null) key.Close();
            }
            return browser;
        }

        private void JustinRecordtoolStripButton_Click(object sender, EventArgs e)
        {
            //need to get channel name from url
            string[] urlSegments = _windowManager.ActiveBrowser.Url.Segments;

            if (urlSegments[1].ToLower() != "directory")//this is a channel
            {
                string channelName = urlSegments[1];
                DialogResult result = MessageBox.Show("Are you sure you want to download from " + channelName, "Download " + channelName, MessageBoxButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    //pop up justin downloader and start downloading
                    //SCTVJustinTV.Downloader downloader = new SCTVJustinTV.Downloader(channelName, "12", Application.StartupPath + "\\JustinDownloads\\");
                    //SCTVJustinTV.Downloader downloader = new SCTVJustinTV.Downloader();
                    //downloader.Channel = channelName;
                    //downloader.Show();
                }
            }
            else
                MessageBox.Show("You must be watching the channel you want to record");
        }

        private void toolStripButtonFavorites_Click(object sender, EventArgs e)
        {
            string url = "";

            //check for url
            if (_windowManager.ActiveBrowser != null && _windowManager.ActiveBrowser.Url.PathAndQuery.Length > 0)
            {
                url = _windowManager.ActiveBrowser.Url.PathAndQuery;

                //add to onlineMedia.xml
                //SCTVObjects.MediaHandler.AddOnlineMedia(_windowManager.ActiveBrowser.Url.Host, _windowManager.ActiveBrowser.Url.PathAndQuery, "Online", "Favorites", "", "");
            }
            else
                MessageBox.Show("You must browse to a website to add it to your favorites");
        }
        
        private string findValue(string stringToParse, string startPattern, string endPattern)
        {
            return findValue(stringToParse, startPattern, endPattern, false);
        }

        private string findValue(string stringToParse, string startPattern, string endPattern, bool returnSearchPatterns)
        {
            int start = 0;
            int end = 0;
            string foundValue = "";

            try
            {
                start = stringToParse.IndexOf(startPattern);

                if (start > -1)
                {
                    if (!returnSearchPatterns)
                        stringToParse = stringToParse.Substring(start + startPattern.Length);
                    else
                        stringToParse = stringToParse.Substring(start);

                    end = stringToParse.IndexOf(endPattern);

                    if (end > 0)
                    {
                        if (returnSearchPatterns)
                            foundValue = stringToParse.Substring(0, end + endPattern.Length);
                        else
                            foundValue = stringToParse.Substring(0, end);
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
                //Tools.WriteToFile(ex);
            }

            return foundValue;
        }
        
        private void btnFindCategories_Click(object sender, EventArgs e)
        {
            foundCategory = findNextCategory(myPointsBrowser.DocumentText);

            //if(!foundCategory)
            //    refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 1, 0, lblRefreshTimer, myPointsBrowser);
        }

        private void btnLateLoad_Click(object sender, EventArgs e)
        {
            myPointsBrowser.Navigate("javascript: window.external.CallServerSideCode();");
        }

        private void btnGetPlaylist_Click(object sender, EventArgs e)
        {
            foundVideo = iterateVideoCards(myPointsBrowser.DocumentText);
            //if (!foundNextVideo)
            //    foundNextVideo = getNextVideo(documentString);

            if (!foundVideo)
                refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 1, 0, lblRefreshTimer, myPointsBrowser);
        }

        private void btnMoreVideos_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.instagc.com/1790146");
        }

        private void btnBitcoin_Click(object sender, EventArgs e)
        {
            Process.Start("https://bitvideo.club/index?ref=lickey");
        }

        private void chbAutoRefresh_CheckedChanged(object sender, EventArgs e)
        {
            if (!chbAutoRefresh.Checked)
            {
                refreshUtilities.Cancel();

                lblRefreshTimer.Text = "0 seconds";
            }
            else
                refreshUtilities.GoToURL("javascript: window.external.CallServerSideCode();", 1, 0, lblRefreshTimer, myPointsBrowser);
        }

        private void btnSurveys_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.mypoints.com/surveys");
        }

        private void btnWatchVideos_Click(object sender, EventArgs e)
        {
            chbAutoRefresh.Checked = true;
            myPointsBrowser.Url = new Uri("https://www.mypoints.com/videos");
        }

        private void btnRestart_Click(object sender, EventArgs e)
        {
            Application.Restart();
        }
    }

    [ComVisible(true)]
    public class MyScript
    {
        public void CallServerSideCode()
        {
            try
            {
                MainForm currentForm = ((MainForm)Application.OpenForms[0]);

                var doc = currentForm._windowManager.ActiveBrowser.Document;

                //var renderedHtml = doc.GetElementsByTagName("HTML")[0].OuterHtml;

                //currentForm.SetDocumentString = renderedHtml;
                currentForm.SetDocument = doc;
            }
            catch (Exception ex)
            {
                //Application.Restart();
            }
            
            //string temp = renderedHtml;

            //if (temp.Length > 500)
            //{
            //    if (temp.ToLower().Contains("your account"))
            //        temp = temp;

            //    currentForm.SetDocumentString = temp;
            //}
        }

        //public void CallServerSideCodeString()
        //{
        //    try
        //    {
        //        MainForm currentForm = ((MainForm)Application.OpenForms[0]);

        //        var doc = currentForm._windowManager.ActiveBrowser.Document;

        //        var renderedHtml = doc.GetElementsByTagName("HTML")[0].OuterHtml;

        //        currentForm.SetDocumentString = renderedHtml;
        //        //currentForm.SetDocument = doc;
        //    }
        //    catch (Exception ex)
        //    {
        //        //Application.Restart();
        //    }
        //}
    }
}