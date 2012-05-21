﻿//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.

// Created by Petr Bena

using System;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;


namespace wmib
{
    public class variables
    {
        /// <summary>
        /// Configuration directory
        /// </summary>
        public static readonly string config = "configuration";
		public static readonly string prefix_logdir = "log";
    }
    public class misc
    {
        public static bool IsValidRegex(string pattern)
        {
            if (pattern == null) return false;

            try
            {
                Regex.Match("", pattern);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }
    }

    public class core
    {
        public static Thread dumphtmt;
        public static Thread rc;
        public static bool disabled;
        public static IRC irc;
        private static List<user> User = new List<user>();

        public class user
        {
            /// <summary>
            /// Regex
            /// </summary>
            public string name;
            /// <summary>
            /// Level
            /// </summary>
            public string level;
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="level"></param>
            /// <param name="name"></param>
            public user(string level, string name)
            {
                this.level = level;
                this.name = name;
            }
        }

        public class RegexCheck
        {
            public string value;
            public string regex;
            public bool searching;
            public bool result = false;
            public RegexCheck(string Regex, string Data)
            {
                result = false;
                value = Data;
                regex = Regex;
            }
            private void Run()
            {
                Regex c = new Regex(regex);
                result = c.Match(value).Success;
                searching = false;
            }
            public int IsMatch()
            {
                Thread quick = new Thread(Run);
                searching = true;
                quick.Start();
                int check = 0;
                while (searching)
                {
                    check++;
                    Thread.Sleep(10);
                    if (check > 50)
                    {
                        quick.Abort();
                        return 2;
                    }
                }
                if (result)
                {
                    return 1;
                }
                return 0;
            }
        }

        /// <summary>
        /// Encode a data before saving it to a file
        /// </summary>
        /// <param name="text">Text</param>
        /// <returns></returns>
        public static string encode(string text)
        {
            return text.Replace(config.separator, "<separator>");
        }

        /// <summary>
        /// Decode
        /// </summary>
        /// <param name="text">String</param>
        /// <returns></returns>
        public static string decode(string text)
        {
            return text.Replace("<separator>", config.separator);
        }

        /// <summary>
        /// Exceptions :o
        /// </summary>
        /// <param name="ex">Exception pointer</param>
        /// <param name="chan">Channel name</param>
        public static void handleException(Exception ex, string chan = "")
        {
            try
            {
                if (config.debugchan != null)
                {
                    irc._SlowQueue.DeliverMessage("DEBUG Exception: " + ex.Message + " I feel crushed, uh :|", config.debugchan);
                }
                Program.Log(ex.Message + ex.Source + ex.StackTrace);
            }
            catch (Exception) // exception happened while we tried to handle another one, ignore that (probably issue with logging)
            { }
        }

        /// <summary>
        /// Get a channel object
        /// </summary>
        /// <param name="name">Name</param>
        /// <returns></returns>
        public static config.channel getChannel(string name)
        {
            foreach (config.channel current in config.channels)
            {
                if (current.name.ToLower() == name.ToLower())
                {
                    return current;
                }
            }
            return null;
        }

        /// <summary>
        /// Change rights of user
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="channel">Channel</param>
        /// <param name="user">User</param>
        /// <param name="host">Host</param>
        /// <returns></returns>
        public static int modifyRights(string message, config.channel channel, string user, string host)
        {
            try
            {
                if (message.StartsWith("@trustadd"))
                {
                    string[] rights_info = message.Split(' ');
                    if (channel.Users.isApproved(user, host, "trustadd"))
                    {
                        if (rights_info.Length < 3)
                        {
                            irc.Message(messages.get("Trust1", channel.ln), channel.name);
                            return 0;
                        }
                        if (!(rights_info[2] == "admin" || rights_info[2] == "trusted"))
                        {
                            irc.Message(messages.get("Unknown1", channel.ln), channel.name);
                            return 2;
                        }
                        if (rights_info[2] == "admin")
                        {
                            if (!channel.Users.isApproved(user, host, "admin"))
                            {
                                irc.Message(messages.get("PermissionDenied", channel.ln), channel.name);
                                return 2;
                            }
                        }
                        if (channel.Users.addUser(rights_info[2], rights_info[1]))
                        {
                            irc.Message(messages.get("UserSc", channel.ln) + rights_info[1], channel.name);
                            return 0;
                        }
                    }
                    else
                    {
                        irc._SlowQueue.DeliverMessage(messages.get("Authorization" ,channel.ln), channel.name);
                        return 0;
                    }
                }
                if (message.StartsWith("@trusted"))
                {
                    channel.Users.listAll();
                    return 0;
                }
                if (message.StartsWith("@trustdel"))
                {
                    string[] rights_info = message.Split(' ');
                    if (rights_info.Length > 1)
                    {
                        string x = rights_info[1];
                        if (channel.Users.isApproved(user, host, "trustdel"))
                        {
                            channel.Users.delUser(channel.Users.getUser(user + "!@" + host), rights_info[1]);
                            return 0;
                        }
                        else
                        {
                            irc._SlowQueue.DeliverMessage(messages.get("Authorization", channel.ln), channel.name);
                            return 0;
                        }
                    }
                    irc.Message(messages.get("InvalidUser", channel.ln), channel.name);
                }
            }
            catch (Exception b)
            {
                handleException(b, channel.name);
            }
            return 0;
        }

        /// <summary>
        /// Called on action
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="Channel">Channel</param>
        /// <param name="host">Host</param>
        /// <param name="nick">Nick</param>
        /// <returns></returns>
        public static bool getAction(string message, string Channel, string host, string nick)
        {
            config.channel curr = getChannel(Channel);
            Logs.chanLog(message, curr, nick, host, false);
            return false;
        }

        public static bool validFile(string name)
        {
            return !(name.Contains(" ") || name.Contains("?") || name.Contains("|") || name.Contains("/")
                || name.Contains("\\") || name.Contains(">") || name.Contains("<") || name.Contains("*"));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chan">Channel</param>
        /// <param name="user">User</param>
        /// <param name="host">Host</param>
        /// <param name="message">Message</param>
        public static void addChannel(config.channel chan, string user, string host, string message)
        {
            try
            {
                if (message.StartsWith("@add"))
                {
                    if (chan.Users.isApproved(user, host, "admin"))
                    {
                        if (message.Contains(" "))
                        {
                            string channel = message.Substring(message.IndexOf(" ") + 1);
                            if (!validFile(channel) || (channel.Contains("#") == false))
                            {
                                irc._SlowQueue.DeliverMessage(messages.get("InvalidName", chan.ln), chan.name);
                                return;
                            }
                            foreach (config.channel cu in config.channels)
                            {
                                if (channel == cu.name)
                                {
									irc._SlowQueue.DeliverMessage (messages.get("ChannelIn", chan.ln), chan.name);
                                    return;
                                }
                            }
							bool existing = config.channel.channelExist(channel);
                            config.channels.Add(new config.channel(channel));
                            config.Save();
                            irc.wd.WriteLine("JOIN " + channel);
                            irc.wd.Flush();
                            Thread.Sleep(100);
                            config.channel Chan = getChannel(channel);
							if (!existing)
							{
                            	Chan.Users.addUser("admin", IRCTrust.normalize(user) + "!.*@" + host);
							}
                            return;
                        }
                        irc.Message(messages.get("InvalidName", chan.ln), chan.name);
                        return;
                    }
                    irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                    return;
                }
            }
            catch (Exception b)
            {
                handleException(b);
            }
        }

        /// <summary>
        /// Part a channel
        /// </summary>
        /// <param name="chan">Channel object</param>
        /// <param name="user">User</param>
        /// <param name="host">Host</param>
        /// <param name="message">Message</param>
        public static void partChannel(config.channel chan, string user, string host, string message)
        {
            try
            {
                if (message == "@drop")
                {
                    if (chan.Users.isApproved(user, host, "admin"))
                    {
                        irc.wd.WriteLine("PART " + chan.name);
                        Thread.Sleep(100);
                        chan.feed = false;
                        irc.wd.Flush();
                        try
                        {
                            if (Directory.Exists(chan.log))
                            {
                                Directory.Delete(chan.log, true);
                            }
                        }
                        catch (Exception)
                        { }
                        try
                        {
                            File.Delete(variables.config + "/" + chan.name + ".setting");
                            File.Delete(chan.Users.File);
                            if (File.Exists(variables.config + "/" + chan.name + ".list"))
                            {
                                File.Delete(variables.config + "/" + chan.name + ".list");
                            }
                        }
                        catch (Exception) { }
                        config.channels.Remove(chan);
                        config.Save();
                        return;
                    }
                    irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                    return;
                }
                if (message == "@part")
                {
                    if (chan.Users.isApproved(user, host, "admin"))
                    {
                        irc.wd.WriteLine("PART " + chan.name);
                        chan.feed = false;
                        Thread.Sleep(100);
                        irc.wd.Flush();
                        config.channels.Remove(chan);
                        config.Save();
                        return;
                    }
                    irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                    return;
                }
            }
            catch (Exception x)
            {
                handleException(x);
            }
        }

        /// <summary>
        /// Display admin command
        /// </summary>
        /// <param name="chan">Channel</param>
        /// <param name="user">User name</param>
        /// <param name="host">Host</param>
        /// <param name="message">Message</param>
        public static void admin(config.channel chan, string user, string host, string message)
        {
            if (message == "@reload")
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    chan.LoadConfig();
                    chan.Keys = new dictionary(chan.keydb, chan.name);
                    irc.Message(messages.get("Config", chan.ln), chan.name);
                    return;
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }
            if (message == "@refresh")
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    irc._Queue.Abort();
                    irc._SlowQueue.newmessages.Clear();
                    irc._Queue = new System.Threading.Thread(new System.Threading.ThreadStart(irc._SlowQueue.Run));
                    irc._SlowQueue.messages.Clear();
                    irc._Queue.Start();
                    irc.Message(messages.get("MessageQueueWasReloaded", chan.ln), chan.name);
                    return;
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }

            if (message == "@recentchanges-on")
            {
                if (chan.Users.isApproved(user, host, "recentchanges-manage"))
                {
                    if (chan.feed)
                    {
                        irc.Message(messages.get("Feed1", chan.ln), chan.name);
                        return;
                    }
                    else
                    {
                        irc.Message(messages.get("Feed2", chan.ln), chan.name);
                        chan.feed = true;
                        chan.SaveConfig();
                        config.Save();
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }

            if (message.StartsWith("@recentchanges+"))
            {
                if (chan.Users.isApproved(user, host, "recentchanges-manage"))
                {
                    if (chan.feed)
                    {
                        if (!message.Contains(" "))
                        {
                            irc._SlowQueue.DeliverMessage(messages.get("InvalidWiki", chan.ln), chan.name);
                            return;
                        }
                        string channel = message.Substring(message.IndexOf(" ") + 1);
                        if (RecentChanges.InsertChannel(chan, channel))
                        {
                            irc.Message(messages.get("Wiki+", chan.ln), chan.name);
                        }
                        return;
                    }
                    else
                    {
                        irc.Message(messages.get("Feed3", chan.ln), chan.name);
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }

            if (message.StartsWith("@recentchanges- "))
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    if (chan.feed)
                    {
                        if (!message.Contains(" "))
                        {
                            irc._SlowQueue.DeliverMessage(messages.get("InvalidWiki", chan.ln), chan.name);
                            return;
                        }
                        string channel = message.Substring(message.IndexOf(" ") + 1);
                        if (RecentChanges.DeleteChannel(chan, channel))
                        {
                            irc.Message(messages.get("Wiki-", chan.ln), chan.name);
                        }
                        return;
                    }
                    else
                    {
                        irc.Message(messages.get("Feed3", chan.ln), chan.name);
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }

            if (message.StartsWith("@RC+ "))
            {
                if (chan.Users.isApproved(user, host, "trust"))
                {
                    if (chan.feed)
                    {
                        string[] a = message.Split(' ');
                        if (a.Length < 3)
                        {
                            irc._SlowQueue.DeliverMessage(messages.get("Feed4", chan.ln) + user + messages.get("Feed5", chan.ln), chan.name);
                            return;
                        }
                        string wiki = a[1];
                        string Page = a[2];
                        chan.RC.insertString(wiki, Page);
                        return;
                    }
                    else
                    {
                        irc._SlowQueue.DeliverMessage(messages.get("Feed3", chan.ln), chan.name);
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }

            if (message.StartsWith("@language"))
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    string parameter = "";
                    if (message.Contains(" "))
                    {
                        parameter = message.Substring(message.IndexOf(" ") + 1).ToLower();
                    }
                    if (parameter != "")
                    {
                        if (messages.exist(parameter))
                        {
                            chan.ln = parameter;
                            irc._SlowQueue.DeliverMessage(messages.get("Language", chan.ln), chan.name);
                            return;
                        }
						irc._SlowQueue.DeliverMessage(messages.get("InvalidCode", chan.ln), chan.name);
                        return;
                    }
                    else
                    {
                        irc._SlowQueue.DeliverMessage(messages.get("LanguageInfo", chan.ln), chan.name);
                        return;
                    }
                }
                else
                {
                    irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                    return;
                }
            }

            if (message.StartsWith("@help"))
            {
                string parameter = "";
                if (message.Contains(" "))
                {
                    parameter = message.Substring(message.IndexOf(" ") + 1);
                }
                if (parameter != "")
                {
                    ShowHelp(parameter, chan.name);
                    return;
                }
                else
                {
                    irc._SlowQueue.DeliverMessage("Type @commands for list of commands. This bot is running http://meta.wikimedia.org/wiki/WM-Bot version " + config.version + " source code licensed under GPL and located at https://github.com/benapetr/wikimedia-bot", chan.name);
                    return;
                }
            }

            if (message.StartsWith("@RC-"))
            {
                if (chan.Users.isApproved(user, host, "trust"))
                {
                    if (chan.feed)
                    {
                        string[] a = message.Split(' ');
                        if (a.Length < 3)
                        {
                            irc._SlowQueue.DeliverMessage(messages.get("Feed7", chan.ln) + user + messages.get("Feed8", chan.ln), chan.name);
                            return;
                        }
                        string wiki = a[1];
                        string Page = a[2];
                        chan.RC.removeString(wiki, Page);
                        return;
                    }
                    else
                    {
                        irc._SlowQueue.DeliverMessage(messages.get("Feed3", chan.ln), chan.name);
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }
			
			if (message == "@suppress-off")
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    if (!chan.suppress)
                    {
                        irc.Message(messages.get("Silence1", chan.ln), chan.name);
                        return;
                    }
                    else
                    {
						chan.suppress = false;
                        irc.Message(messages.get("Silence2", chan.ln), chan.name);
                        chan.SaveConfig();
                        config.Save();
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }
			
			if (message == "@suppress-on")
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    if (chan.suppress)
                    {
                        //Message("Channel had already quiet mode disabled", chan.name);
                        return;
                    }
                    else
                    {
                        irc.Message(messages.get( "SilenceBegin", chan.ln ), chan.name);
                        chan.suppress = true;
                        chan.SaveConfig();
                        config.Save();
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }
			
            if (message == "@recentchanges-off")
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    if (!chan.feed)
                    {
                        irc.Message(messages.get("Feed6", chan.ln), chan.name);
                        return;
                    }
                    else
                    {
                        irc.Message(messages.get("Feed7", chan.ln), chan.name);
                        chan.feed = false;
                        chan.SaveConfig();
                        config.Save();
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }

            if (message == "@logon")
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    if (chan.logged)
                    {
                        irc.Message(messages.get("ChannelLogged",   chan.ln), chan.name);
                        return;
                    }
                    else
                    {
                        irc.Message(messages.get("LoggingOn", chan.ln), chan.name);
                        chan.logged = true;
                        chan.SaveConfig();
                        config.Save();
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }

            if (message == "@whoami")
            {
                user current = chan.Users.getUser(user + "!@" + host);
                if (current.level == "null")
                {
                    irc._SlowQueue.DeliverMessage(messages.get("Unknown", chan.ln), chan.name);
                    return;
                }
                irc.Message(messages.get("usr1",chan.ln) + current.level + messages.get ("usr2", chan.ln) + current.name, chan.name);
                return;
            }

            if (message == "@logoff")
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    if (!chan.logged)
                    {
                        irc.Message(messages.get("LogsE1", chan.ln), chan.name);
                        return;
                    }
                    else
                    {
                        chan.logged = false;
                        config.Save();
                        chan.SaveConfig();
                        irc.Message(messages.get("NotLogged", chan.ln), chan.name);
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }
            if (message == "@channellist")
            {
                string channels = "";
                foreach (config.channel a in config.channels)
                {
                    channels = channels + a.name + ", ";
                }
                irc._SlowQueue.DeliverMessage(messages.get("List", chan.ln) + channels, chan.name);
                return;
            }
            if (message == "@infobot-off")
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    if (!chan.info)
                    {
                        irc.Message(messages.get("infobot1", chan.ln ), chan.name);
                        return;
                    }
                    else
                    {
                        irc.Message(messages.get("infobot2", chan.ln), chan.name);
                        chan.info = false;
                        chan.SaveConfig();
                        config.Save();
                        return;
                    }
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", chan.ln), chan.name);
                return;
            }
            if (message == "@infobot-on")
            {
                if (chan.Users.isApproved(user, host, "admin"))
                {
                    if (!chan.logged)
                    {
                        irc.Message(messages.get( "infobot3", chan.ln ), chan.name);
                        return;
                    }
                    chan.info = true;
                    config.Save();
                    chan.SaveConfig();
                    irc.Message(messages.get("infobot4", chan.ln), chan.name);
                    return;
                }
                irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied" ,chan.ln), chan.name);
                return;
            }
            if (message == "@commands")
            {
                irc._SlowQueue.DeliverMessage("Commands: channellist, trusted, trustadd, trustdel, infobot-off, refresh, infobot-on, drop, whoami, add, reload, suppress-off, suppress-on, help, RC-, recentchanges-on, language, recentchanges-off, logon, logoff, recentchanges-, recentchanges+, RC+", chan.name);
                return;
            }
        }

        public static void Connect()
        {
                irc = new IRC(config.network, config.username, config.name, config.name);
                irc.Connect();
                dumphtmt = new Thread(HtmlDump.Start);
                dumphtmt.Start();
                rc = new Thread(RecentChanges.Start);
                rc.Start();
        }

        /// <summary>
        /// Called when someone post a message to server
        /// </summary>
        /// <param name="channel">Channel</param>
        /// <param name="nick">Nick</param>
        /// <param name="host">Host</param>
        /// <param name="message">Message</param>
        /// <returns></returns>
        public static bool getMessage(string channel, string nick, string host, string message)
        {
            config.channel curr = getChannel(channel);
            if (curr != null)
            {
                Logs.chanLog(message, curr, nick, host);
                if (message.StartsWith("!") && curr.info)
                {
                    curr.Keys.print(message, nick, curr, host);
                }
                if (message.StartsWith("@"))
                {
                    if (curr.info)
                    {
                        curr.Keys.Find(message, curr);
                        curr.Keys.RSearch(message, curr);
                    }
                    modifyRights(message, curr, nick, host);
                    addChannel(curr, nick, host, message);
                    admin(curr, nick, host, message);
                    partChannel(curr, nick, host, message);
                }
            }

            return false;
        }

        private static void showInfo(string name, string info, string channel)
        {
            irc._SlowQueue.DeliverMessage("Info for " + name + ": " + info, channel);
        }

        private static bool ShowHelp(string parameter, config.channel channel)
        {
			if (parameter.StartsWith ("@"))
			{
				parameter = parameter.Substring (1);
			}
            switch (parameter.ToLower())
            {
                case "trustdel":
                case "refresh":
                case "infobot-on":
                case "infobot-off":
                case "channellist":
                case "trusted":
                case "trustadd":
                case "drop":
                case "part":
				case "language":
                case "whoami":
				case "suppress-on":
                case "add":
                case "reload":
                case "logon":
                case "logoff":
                case "recentchanges-on":
                case "recentchanges-off":
                case "recentchanges-":
                case "recentchanges+":
                case "rc-":
                case "rc+":
				case "suppress-off":
					showInfo(parameter, messages.get (parameter.ToLower (), channel.ln), channel.name);
					return false;
            }
            irc._SlowQueue.DeliverMessage("Unknown command type @commands for a list of all commands I know", channel.name);
            return false;
        }

		public static string getUptime()
		{
			System.TimeSpan uptime = System.DateTime.Now - config.UpTime;
			return uptime.Days.ToString () + " days  " + uptime.Hours.ToString () + " hours since " + config.UpTime.ToString ();
		}
    }
}
