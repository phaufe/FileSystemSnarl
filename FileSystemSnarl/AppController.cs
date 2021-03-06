﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FileSystemSnarl.Snarl;
using System.IO;

namespace FileSystemSnarl
{
    public class AppController
    {
        public static AppController Current;
        public Version installedVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        public string FormattedVersionString
        {
            get
            {
                if (installedVersion != null)
                {
                    return getNiceVersionString(installedVersion.ToString());
                }
                else
                {
                    return "";
                }
            }
        }

        public Snarl.SnarlInterface snarl { get; set; }
        public SnarlNetwork.SnarlNetwork snarlNetwork { get; set; }
        private Dictionary<int, string> snarlSentNotifications { get; set; }

        public string IconPath { get; set; }

        private string lastFilename = "";
        private string lastType = "";
        private DateTime lastNotification;

        private System.IO.FileSystemWatcher watcher;
        public bool isRunning { get; set; }

        public MainWindow mainWindow { get; set; }

        enum SnarlActions
        {
            OpeninExplorer,
            Execute,
            Delete,
            CopyPathToClipboard,
            CopyFilenameToClipboard
        }

        #region Startup and initialization

        public static void Start()
        {
            if (Current == null)
            {
                Current = new AppController();
                Current.openMainWindow();
            }
        }

        private AppController()
        {
            snarlSentNotifications = new Dictionary<int, string>();
            snarl = new Snarl.SnarlInterface();
            snarl.CallbackEvent += new SnarlInterface.CallbackEventHandler(snarl_CallbackEvent);
            snarl.GlobalSnarlEvent +=new SnarlInterface.GlobalEventHandler(snarl_GlobalSnarlEvent);

            watcher = new System.IO.FileSystemWatcher();
            lastNotification = DateTime.Now;

            IconPath = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\FileSystemSnarl.ico";
            if (!System.IO.File.Exists(IconPath))
            {
                throw new Exception("Corrupt installation - missing icon for tray area and Snarl notifications which should be at " + IconPath);
            }

        }



        public void openMainWindow() {
            
            mainWindow = new MainWindow();
            mainWindow.Show();

            if (Properties.Settings.Default.activateOnStartup && mainWindow.startButton.IsEnabled)
            {
                startWatching();
            }

            if (Properties.Settings.Default.minimizeToTrayOnStartup)
            {
                mainWindow.hideNow(null,null);
            }
        }

        ~AppController() {
            if (isRunning)
            {
                if (Properties.Settings.Default.snarlLocal && snarl != null)
                {
                    snarl.Unregister();
                }
                else if (!Properties.Settings.Default.snarlLocal && snarlNetwork != null)
                {
                    snarlNetwork.unregister(Properties.Settings.Default.snpIp, Properties.Settings.Default.snpPort, "FileSystemSnarl");
                }

            }
            Properties.Settings.Default.Save();

        }
        
        #endregion

        public void startWatching()
        {
            if (!isRunning)
            {
                if (!Directory.Exists(Properties.Settings.Default.folder))
                {
                    System.Windows.MessageBox.Show("Folder " + Properties.Settings.Default.folder + " does not exist");
                    return;
                }
                isRunning = true;
                mainWindow.startButton.Content = "Stop watching";
                mainWindow.startButton.Background = System.Windows.Media.Brushes.Red;

                mainWindow.radioButtonUseLocalNotifications.IsEnabled = false;
                mainWindow.radioButtonUseSnpNotifications.IsEnabled = false;
                mainWindow.targetIP.IsEnabled = false;
                mainWindow.targetPort.IsEnabled = false;

                mainWindow.textBoxPath.IsEnabled = false;
                mainWindow.checkBoxIncludeSubdirectories.IsEnabled = false;
                mainWindow.textBoxFilter.IsEnabled = false;

                mainWindow.checkBoxAttributes.IsEnabled = false;
                mainWindow.checkBoxChanged.IsEnabled = false;
                mainWindow.checkBoxCreated.IsEnabled = false;
                mainWindow.checkBoxDeleted.IsEnabled = false;
                mainWindow.checkBoxLastAccess.IsEnabled = false;
                mainWindow.checkBoxLastWrite.IsEnabled = false;
                mainWindow.checkBoxRenamed.IsEnabled = false;
                mainWindow.checkBoxSize.IsEnabled = false;
                mainWindow.checkBoxDirectoryName.IsEnabled = false;
                mainWindow.checkBoxFilename.IsEnabled = false;

                mainWindow.chooseFolder.IsEnabled = false;

                RegisterWithSnarl();

                watcher.Path = Properties.Settings.Default.folder;

                NotifyFilters myFilter = new NotifyFilters();
                NotifyFilters emptyFilter = new NotifyFilters();

                if (Properties.Settings.Default.fAttributes)
                {
                    myFilter = NotifyFilters.Attributes;
                }
                if (Properties.Settings.Default.fDirname)
                {
                    myFilter = myFilter | NotifyFilters.DirectoryName;
                }
                if (Properties.Settings.Default.fFilename)
                {
                    myFilter = myFilter | NotifyFilters.FileName;
                }
                if (Properties.Settings.Default.fLastAccess)
                {
                    myFilter = myFilter | NotifyFilters.LastAccess;
                }
                if (Properties.Settings.Default.fLastWrite)
                {
                    myFilter = myFilter | NotifyFilters.LastWrite;
                }
                if (Properties.Settings.Default.fSize)
                {
                    myFilter = myFilter | NotifyFilters.Size;
                }

                if (myFilter != emptyFilter)
                {
                    watcher.NotifyFilter = myFilter;
                }
                else
                {
                    watcher.NotifyFilter = NotifyFilters.FileName;

                }

                watcher.Filter = Properties.Settings.Default.filter;

                watcher.IncludeSubdirectories = Properties.Settings.Default.includeSubdirs;

                // Add event handlers.
                if (Properties.Settings.Default.fChanged)
                {
                    watcher.Changed += new FileSystemEventHandler(OnChanged);
                }
                if (Properties.Settings.Default.fCreated)
                {
                    watcher.Created += new FileSystemEventHandler(OnChanged);
                }
                if (Properties.Settings.Default.fDeleted)
                {
                    watcher.Deleted += new FileSystemEventHandler(OnChanged);
                }
                if (Properties.Settings.Default.fRenamed)
                {
                    watcher.Renamed += new RenamedEventHandler(OnRenamed);
                }

                watcher.EnableRaisingEvents = true;

            }
            else
            {
                isRunning = false;
                if (Properties.Settings.Default.snarlLocal)
                {
                    snarl.Unregister();
                }
                else
                {
                    snarlNetwork.unregister(Properties.Settings.Default.snpIp, Properties.Settings.Default.snpPort, "FileSystemSnarl");
                }

                watcher.EnableRaisingEvents = false;
                mainWindow.startButton.Content = "Start forwarding";
                mainWindow.startButton.Background = System.Windows.Media.Brushes.GreenYellow;
                mainWindow.radioButtonUseLocalNotifications.IsEnabled = true;
                mainWindow.radioButtonUseSnpNotifications.IsEnabled = true;
                mainWindow.targetIP.IsEnabled = true;
                mainWindow.targetPort.IsEnabled = true;

                mainWindow.textBoxPath.IsEnabled = true;
                mainWindow.checkBoxIncludeSubdirectories.IsEnabled = true;
                mainWindow.textBoxFilter.IsEnabled = true;

                mainWindow.checkBoxAttributes.IsEnabled = true;
                mainWindow.checkBoxChanged.IsEnabled = true;
                mainWindow.checkBoxCreated.IsEnabled = true;
                mainWindow.checkBoxDeleted.IsEnabled = true;
                mainWindow.checkBoxLastAccess.IsEnabled = true;
                mainWindow.checkBoxLastWrite.IsEnabled = true;
                mainWindow.checkBoxRenamed.IsEnabled = true;
                mainWindow.checkBoxSize.IsEnabled = true;
                mainWindow.checkBoxDirectoryName.IsEnabled = true;
                mainWindow.checkBoxFilename.IsEnabled = true;

                mainWindow.chooseFolder.IsEnabled = true;
            }
        }

        #region event handler

        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            if (e.Name.Length >= 5)
            {
                if (e.Name.Substring(0, 5) == "RECYC")
                {
                    return;
                }
            }

            string alertClass = "File has been " + e.ChangeType.ToString().ToLower();
            string title = "";
            string body = "";


            // some events are fired multiple times for the same change so we have a small delay between two of them to be sure not to have multiple notifications
            if (AppController.Current.lastFilename == e.Name && AppController.Current.lastType == e.ChangeType.ToString())
            {
                DateTime waitTime = DateTime.Now.AddSeconds(-3);
                if (AppController.Current.lastNotification > waitTime)
                {
                    // we just had this one...
                    return;
                }
               
            }


            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Changed:
                    title = parseNotificationText(e, Properties.Settings.Default.templateChangedTitle);
                    body = parseNotificationText(e, Properties.Settings.Default.templateChangedBody);
                    break;

                case WatcherChangeTypes.Created:
                    title = parseNotificationText(e, Properties.Settings.Default.templateCreatedTitle);
                    body = parseNotificationText(e, Properties.Settings.Default.templateCreatedBody);
                    break;

                case WatcherChangeTypes.Deleted:
                    title = parseNotificationText(e, Properties.Settings.Default.templateDeletedTitle);
                    body = parseNotificationText(e, Properties.Settings.Default.templateDeletedBody);
                    break;
            }

            if (Properties.Settings.Default.snarlLocal)
            {
                int msgToken = AppController.Current.snarl.Notify(alertClass, title, body, Properties.Settings.Default.displayTime, AppController.Current.IconPath, null);
                if (msgToken != 0 && !AppController.Current.snarlSentNotifications.ContainsKey(msgToken) && e.ChangeType != WatcherChangeTypes.Deleted)
                {
                    memorizeTokenAndPath(msgToken, e.FullPath);
                }
            }
            else
            {
                AppController.Current.snarlNetwork.notify(Properties.Settings.Default.snpIp, Properties.Settings.Default.snpPort, "FileSystemSnarl", alertClass, title, body, Properties.Settings.Default.displayTime.ToString());
            }
            AppController.Current.lastType = e.ChangeType.ToString();
            AppController.Current.lastFilename = e.Name;
            AppController.Current.lastNotification = DateTime.Now;

        }

        private static void memorizeTokenAndPath(int msgToken, string fullpath)
        {
            try
            {
                AppController.Current.snarlSentNotifications.Add(msgToken, fullpath);
                AppController.Current.snarl.AddAction(msgToken, "Open in Explorer", "@" + (int)SnarlActions.OpeninExplorer);
                AppController.Current.snarl.AddAction(msgToken, "Execute", "@" + (int)SnarlActions.Execute);
                AppController.Current.snarl.AddAction(msgToken, "Delete immediately", "@" + (int)SnarlActions.Delete);
                AppController.Current.snarl.AddAction(msgToken, "Copy full path to clipboard", "@" + (int)SnarlActions.CopyPathToClipboard);
                AppController.Current.snarl.AddAction(msgToken, "Copy filename to clipboard", "@" + (int)SnarlActions.CopyFilenameToClipboard);
            }
            catch
            {
                // just in case...
            }

        }

        private static string parseNotificationText(FileSystemEventArgs e, string template)
        {
            string returnValue = template;

            returnValue = returnValue.Replace("%NEWLINE%", "\n");
            returnValue = returnValue.Replace("%FILENAME%", e.Name);
            returnValue = returnValue.Replace("%FULLPATH%", e.FullPath);
            returnValue = returnValue.Replace("%EVENTTYPE%", e.ChangeType.ToString());
            if (returnValue.Contains("%FILECONTENT%") && e.ChangeType != WatcherChangeTypes.Deleted)
            {
                try
                {
                   // FileInfo f = new FileInfo(e.FullPath);
                  //  long s1 = f.Length;
                    string[] fileContents = File.ReadAllLines(e.FullPath, Encoding.ASCII);
                    string fileContent = String.Join("\n", fileContents);
                    if (fileContent.Length > Properties.Settings.Default.templateFullContentMaxLength)
                    {
                        fileContent = fileContent.Substring(0, Properties.Settings.Default.templateFullContentMaxLength) + "...";
                    }
                    returnValue = returnValue.Replace("%FILECONTENT%", fileContent);
                }
                catch
                {
                    returnValue = returnValue.Replace("%FILECONTENT%","UNREADABLE DATA");
                }
            }

            return returnValue;
        }

        private static string parseNotificationText(RenamedEventArgs e, string template)
        {
            string returnValue = parseNotificationText(e as FileSystemEventArgs, template);
            returnValue = returnValue.Replace("%OLDNAME%", e.OldName);
            returnValue = returnValue.Replace("%OLDFULLPATH%", e.OldFullPath);
            returnValue = returnValue.Replace("%NEWNAME%", e.Name);
            returnValue = returnValue.Replace("%NEWFULLPATH%", e.FullPath);

            return returnValue;
        }

        static void OnRenamed(object source, RenamedEventArgs e)
        {
            string title = parseNotificationText(e, Properties.Settings.Default.templateRenamedTitle);
            string body = parseNotificationText(e, Properties.Settings.Default.templateRenamedBody);

            if (Properties.Settings.Default.snarlLocal)
            {
                int msgToken = AppController.Current.snarl.Notify("File has been renamed", title, body, Properties.Settings.Default.displayTime, AppController.Current.IconPath, null);
                if (msgToken != 0 && !AppController.Current.snarlSentNotifications.ContainsKey(msgToken) && e.ChangeType != WatcherChangeTypes.Deleted)
                {
                    memorizeTokenAndPath(msgToken, e.FullPath);
                }
            }
            else
            {
                AppController.Current.snarlNetwork.notify(Properties.Settings.Default.snpIp, Properties.Settings.Default.snpPort, "FileSystemSnarl", "File has been renamed", title, body, Properties.Settings.Default.displayTime.ToString());
            }
        }

        #endregion

        public void RegisterWithSnarl()
        {
            if (Properties.Settings.Default.snarlLocal)
            {
                if (snarl == null)
                {
                    snarl = new SnarlInterface();
                    snarl.CallbackEvent += new SnarlInterface.CallbackEventHandler(snarl_CallbackEvent);
                    snarl.GlobalSnarlEvent += new SnarlInterface.GlobalEventHandler(snarl_GlobalSnarlEvent);
                }
                snarl.RegisterWithEvents("FileSystemSnarl", "FileSystemSnarl", IconPath,"fgvh54546bg54", IntPtr.Zero, null);

                snarl.AddClass("File has been created", "File has been created");
                snarl.AddClass("File has been changed", "File has been changed");
                snarl.AddClass("File has been renamed", "File has been renamed");
                snarl.AddClass("File has been deleted", "File has been deleted");
            }
            else
            {
                snarlNetwork = new SnarlNetwork.SnarlNetwork(Properties.Settings.Default.snpIp, Properties.Settings.Default.snpPort);
                snarlNetwork.register(Properties.Settings.Default.snpIp, Properties.Settings.Default.snpPort, "FileSystemSnarl");
                snarlNetwork.addClass(Properties.Settings.Default.snpIp, Properties.Settings.Default.snpPort, "FileSystemSnarl", "File has been created", "File has been created");
                snarlNetwork.addClass(Properties.Settings.Default.snpIp, Properties.Settings.Default.snpPort, "FileSystemSnarl", "File has been changed", "File has been changed");
                snarlNetwork.addClass(Properties.Settings.Default.snpIp, Properties.Settings.Default.snpPort, "FileSystemSnarl", "File has been renamed", "File has been renamed");
                snarlNetwork.addClass(Properties.Settings.Default.snpIp, Properties.Settings.Default.snpPort, "FileSystemSnarl", "File has been deleted", "File has been deleted");
            }
        }

        void snarl_GlobalSnarlEvent(SnarlInterface sender, SnarlInterface.GlobalEventArgs args)
        {
            if (args.GlobalEvent == SnarlInterface.GlobalEvent.SnarlLaunched && isRunning)
            {
                RegisterWithSnarl();
            }
        }
        void snarl_CallbackEvent(SnarlInterface sender, SnarlInterface.CallbackEventArgs args)
        {
            switch (args.SnarlEvent)
            {
                case SnarlInterface.SnarlStatus.NotifyAction:
                    HandleActionCallback(args.Parameter, args.MessageToken);
                    break;

                case SnarlInterface.SnarlStatus.CallbackInvoked:
                case SnarlInterface.SnarlStatus.CallbackClosed:
                case SnarlInterface.SnarlStatus.CallbackMiddleClick:
                case SnarlInterface.SnarlStatus.CallbackTimedOut:
                    if (snarlSentNotifications.ContainsKey(args.MessageToken))
                    {
                        try
                        {
                            snarlSentNotifications.Remove(args.MessageToken);
                        }
                        catch { }
                    }
                    break;
            }
        }

        private void HandleActionCallback(UInt16 actionData, int msgToken)
        {
            if (snarlSentNotifications.ContainsKey(msgToken))
            {
                switch ((SnarlActions)actionData)
                {
                    case SnarlActions.OpeninExplorer:
                            string argument = @"/select, " + snarlSentNotifications[msgToken];
                            try
                            {
                                System.Diagnostics.Process.Start("explorer.exe", argument);
                            }
                            catch { }
                        break;

                    case SnarlActions.Execute:
                        try
                        {
                            System.Diagnostics.Process.Start(snarlSentNotifications[msgToken]);
                        }
                        catch { }
                        break;

                    case SnarlActions.Delete:
                        try
                        {
                            File.Delete(snarlSentNotifications[msgToken]);
                        }
                        catch { }
                        break;

                    case SnarlActions.CopyPathToClipboard:
                        try
                        {
                            System.Windows.Forms.Clipboard.SetText(snarlSentNotifications[msgToken]);
                        }
                        catch { }
                        break;

                    case SnarlActions.CopyFilenameToClipboard:
                        try
                        {
                            string filename = Path.GetFileName(snarlSentNotifications[msgToken]);
                            System.Windows.Forms.Clipboard.SetText(filename);
                        }
                        catch { }
                        break;
                }
            }
        }



        #region Helper stuff

        public static string getNiceVersionString(string fullString)
        {
            while (fullString.Length > 3 && fullString.EndsWith(".0"))
            {
                fullString = fullString.Substring(0, fullString.Length - 2);
            }
            return fullString;
        }

        #endregion

      
    }
}
