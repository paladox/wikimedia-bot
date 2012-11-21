﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Threading;

namespace wmib
{
    public class RegularModule : Module
    {
        private bool save = false;

        public override bool Construct()
        {
            LoadData();
            Version = "1.0.4";
            base.Create("SEEN", true);
            return true;
        }

        public override void Hook_ACTN(config.channel channel, User invoker, string message)
        {
            WriteStatus(invoker.Nick, invoker.Host, channel.Name, item.Action.Talk);
        }

        public override void Hook_PRIV(config.channel channel, User invoker, string message)
        {
            WriteStatus(invoker.Nick, invoker.Host, channel.Name, item.Action.Talk);
            if (message.StartsWith("@seen "))
            {
                if (GetConfig(channel, "Seen.Enabled", false))
                {
                    string parameter = "";
                    if (message.Contains(" "))
                    {
                        parameter = message.Substring(message.IndexOf(" ") + 1);
                    }
                    if (parameter != "")
                    {
                        RetrieveStatus(parameter, channel, invoker.Nick);
                        return;
                    }
                }
            }

            if (message.StartsWith("@seenrx "))
            {
                if (channel.Users.isApproved(invoker.Nick, invoker.Host, "trust"))
                {
                    if (GetConfig(channel, "Seen.Enabled", false))
                    {
                        string parameter = "";
                        if (message.Contains(" "))
                        {
                            parameter = message.Substring(message.IndexOf(" ") + 1);
                        }
                        if (parameter != "")
                        {
                            RegEx(parameter, channel, invoker.Nick);
                            return;
                        }
                    }
                    return;
                }
                if (!channel.suppress_warnings)
                {
                    core.irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", channel.Language), channel.Name, IRC.priority.low);
                }
                return;
            }

            if (message == "@seen-off")
            {
                if (channel.Users.isApproved(invoker.Nick, invoker.Host, "admin"))
                {
                    if (!GetConfig(channel, "Seen.Enabled", false))
                    {
                        core.irc._SlowQueue.DeliverMessage(messages.get("seen-e2", channel.Language), channel.Name);
                        return;
                    }
                    else
                    {
                        core.irc._SlowQueue.DeliverMessage(messages.get("seen-off", channel.Language), channel.Name, IRC.priority.high);
                        SetConfig(channel, "Seen.Enabled", false);
                        channel.SaveConfig();
                        return;
                    }
                }
                if (!channel.suppress_warnings)
                {
                    core.irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", channel.Language), channel.Name, IRC.priority.low);
                }
                return;
            }

            if (message == "@seen-on")
            {
                if (channel.Users.isApproved(invoker.Nick, invoker.Host, "admin"))
                {
                    if (GetConfig(channel, "Seen.Enabled", false))
                    {
                        core.irc._SlowQueue.DeliverMessage(messages.get("seen-oe", channel.Language), channel.Name);
                        return;
                    }
                    SetConfig(channel, "Seen.Enabled", true);
                    channel.SaveConfig();
                    core.irc._SlowQueue.DeliverMessage(messages.get("seen-on", channel.Language), channel.Name, IRC.priority.high);
                    return;
                }
                if (!channel.suppress_warnings)
                {
                    core.irc._SlowQueue.DeliverMessage(messages.get("PermissionDenied", channel.Language), channel.Name, IRC.priority.low);
                }
                return;
            }
        }

        public override void Hook_Join(config.channel channel, User user)
        {
            WriteStatus(user.Nick, user.Host, channel.Name, item.Action.Join);
        }

        public override void Hook_Kick(config.channel channel, User source, User user)
        {
            WriteStatus(user.Nick, user.Host, channel.Name, item.Action.Kick);
        }

        public override void Hook_Part(config.channel channel, User user)
        {
            WriteStatus(user.Nick, user.Host, channel.Name, item.Action.Part);
        }

        public override void Hook_Quit(User user)
        {
            WriteStatus(user.Nick, user.Host, "N/A", item.Action.Exit);
        }

        public class ChannelRequest
        {
            public config.channel channel;
            public string nick;
            public string source;
            public bool rg;
            public ChannelRequest(string _nick, string _source, config.channel Channel, bool regexp)
            {
                rg = regexp;
                nick = _nick;
                channel = Channel;
                source = _source;
            }
        }

        public class item
        {
            public string nick;
            public string hostname;
            public string lastplace;
            public DateTime LastSeen;
            public Action LastAc;
            public enum Action
            {
                Join,
                Part,
                Talk,
                Kick,
                Exit
            }

            public item(string Nick, string Host, string LastPlace, Action action, string Date = null)
            {
                nick = Nick;
                hostname = Host;
                lastplace = LastPlace;
                if (Date != null)
                {
                    LastSeen = DateTime.FromBinary(long.Parse(Date));
                }
                LastAc = action;
                if (Date == null)
                {
                    LastSeen = DateTime.Now;
                }
            }
        }

        public static List<ChannelRequest> requests = new List<ChannelRequest>();
        public Thread SearchThread;
        public Thread SearchHostThread;
        public bool Working = false;

        public string temp_nick;
        public config.channel chan;
        public string temp_source;

        public override void Load()
        {
            try
            {
                while (true)
                {
                    if (save)
                    {
                        save = false;
                        Save();
                    }
                    Thread.Sleep(20000);
                }
            }
            catch (ThreadAbortException)
            {
                Save();
                if (SearchThread.ThreadState == ThreadState.Running)
                {
                    SearchThread.Abort();
                }
                return;
            }
            catch (Exception fail)
            {
                core.handleException(fail);
            }
        }

        public List<item> global = new List<item>();

        public void WriteStatus(string nick, string host, string place, item.Action action)
        {
            item user = null;
            lock (global)
            {
                foreach (item xx in global)
                {
                    if (nick.ToUpper() == xx.nick.ToUpper())
                    {
                        user = xx;
                        break;
                    }
                }
                if (user == null)
                {
                    user = new item(nick, host, place, action);
                    global.Add(user);
                }
                else
                {
                    user.nick = nick;
                    user.LastAc = action;
                    user.LastSeen = DateTime.Now;
                    user.hostname = host;
                    user.lastplace = place;
                }
            }
            save = true;
        }

        public void Search()
        {
            try
            {
                if (misc.IsValidRegex(temp_nick))
                {
                    System.Text.RegularExpressions.Regex ex = new System.Text.RegularExpressions.Regex(temp_nick);
                    string response = "I have never seen " + temp_nick;
                    bool found = false;
                    bool multiple = false;
                    string results = "";
                    int cn = 0;
                    string action = "quiting the network";
                    foreach (item xx in global)
                    {
                        if (ex.IsMatch(xx.nick))
                        {
                            if (found)
                            {
                                cn++;
                                if (cn < 6)
                                {
                                    results += xx.nick + ", ";
                                }
                                multiple = true;
                                continue;
                            }
                            found = true;
                            config.channel last;
                            switch (xx.LastAc)
                            {
                                case item.Action.Join:
                                    action = "joining the channel";
                                    last = core.getChannel(xx.lastplace);
                                    if (last != null)
                                    {
                                        if (last.containsUser(xx.nick))
                                        {
                                            action += ", they are still in the channel";
                                        }
                                        else
                                        {
                                            action += ", but they are not in the channel now and I don't know why, in";
                                        }
                                    }
                                    break;
                                case item.Action.Kick:
                                    action = "kicked from the channel";
                                    break;
                                case item.Action.Part:
                                    action = "leaving the channel";
                                    break;
                                case item.Action.Talk:
                                    action = "talking in the channel";
                                    last = core.getChannel(xx.lastplace);
                                    if (last != null)
                                    {
                                        if (last.containsUser(xx.nick))
                                        {
                                            action += ", they are still in the channel";
                                        }
                                        else
                                        {
                                            action += ", but they are not in the channel now and I don't know why, in";
                                        }
                                    }
                                    break;
                            }
                            TimeSpan span2 = DateTime.Now - xx.LastSeen;
                            response = "Last time I saw " + xx.nick + " they were " + action + " " + xx.lastplace + " at " + xx.LastSeen.ToString() + " (" + span2.ToString() + " ago)";
                        }
                    }
                    if (temp_nick.ToUpper() == temp_source.ToUpper())
                    {
                        response = "are you really looking for yourself?";
                        core.irc._SlowQueue.DeliverMessage(temp_source + ": " + response, chan.Name);
                        Working = false;
                        return;
                    }
                    if (temp_nick.ToUpper() == config.username.ToUpper())
                    {
                        response = "I am right here";
                        core.irc._SlowQueue.DeliverMessage(temp_source + ": " + response, chan.Name);
                        Working = false;
                        return;
                    }
                    if (chan.containsUser(temp_nick))
                    {
                        response = temp_nick + " is in here, right now";
                        found = true;
                    }
                    if (multiple)
                    {
                        if (results.Length > 2)
                        {
                            results = results.Substring(0, results.Length - 2);
                        }
                        if (cn > 5)
                        {
                            results = results + " and " + (cn - 5).ToString() + " more results";
                        }
                        response += " (multiple results were found: " + results + ")";
                    }
                    core.irc._SlowQueue.DeliverMessage(temp_source + ": " + response, chan.Name);
                    Working = false;
                    return;
                }
                core.irc._SlowQueue.DeliverMessage(messages.get("Error1", chan.Language), chan.Name);
                Working = false;
            }
            catch (ThreadAbortException)
            {
                return;
            }
            catch (Exception fail)
            {
                core.handleException(fail);
                Working = false;
            }
        }

        public void StartRegex()
        {
            try
            {
                while (true)
                {
                    if (requests.Count > 0)
                    {
                        List<ChannelRequest> Requests = new List<ChannelRequest>();
                        lock (requests)
                        {
                            Requests.AddRange(requests);
                            requests.Clear();
                        }
                        foreach (ChannelRequest ch in Requests)
                        {
                            if (ch.rg)
                            {
                                RegEx2(ch.nick, ch.channel, ch.source);
                                continue;
                            }
                            RetrieveStatus2(ch.nick, ch.channel, ch.source);
                        }
                        Requests.Clear();
                    }
                    Thread.Sleep(100);
                }
            }
            catch (ThreadAbortException)
            {
                return;
            }
        }

        public void RegEx2(string nick, config.channel channel, string source)
        {
            try
            {
                temp_nick = nick;
                temp_source = source;
                chan = channel;
                SearchThread = new Thread(Search);
                SearchThread.Start();
                Working = true;
                int curr = 0;
                while (Working)
                {
                    Thread.Sleep(10);
                    curr++;
                    if (curr > 80)
                    {
                        SearchThread.Abort();
                        core.irc._SlowQueue.DeliverMessage("This search took too much time, please optimize query", channel.Name);
                        Working = false;
                        break;
                    }
                }
            }
            catch (Exception fail)
            {
                core.handleException(fail);
            }
        }

        public void RegEx(string nick, config.channel channel, string source)
        {
            lock (requests)
            {
                requests.Add(new ChannelRequest(nick, source, channel, true));
            }
        }

        public void RetrieveStatus(string nick, config.channel channel, string source)
        {
            lock (requests)
            {
                requests.Add(new ChannelRequest(nick, source, channel, false));
            }
        }

        public void RetrieveStatus2(string nick, config.channel channel, string source)
        {
            string response = "I have never seen " + nick;
            bool found = false;
            string action = "quiting the network";
            foreach (item xx in global)
            {
                if (nick.ToUpper() == xx.nick.ToUpper())
                {
                    found = true;
                    config.channel last;
                    switch (xx.LastAc)
                    {
                        case item.Action.Join:
                            action = "joining the channel";
                            last = core.getChannel(xx.lastplace);
                            if (last != null)
                            {
                                if (last.containsUser(nick))
                                {
                                    action += ", they are still in the channel";
                                }
                                else
                                {
                                    action += ", but they are not in the channel now and I don't know why, in";
                                }
                            }
                            break;
                        case item.Action.Kick:
                            action = "kicked from the channel";
                            break;
                        case item.Action.Part:
                            action = "leaving the channel";
                            break;
                        case item.Action.Talk:
                            action = "talking in the channel";
                            last = core.getChannel(xx.lastplace);
                            if (last != null)
                            {
                                if (last.containsUser(nick))
                                {
                                    action += ", they are still in the channel";
                                }
                                else
                                {
                                    action += ", but they are not in the channel now and I don't know why, in";
                                }
                            }
                            break;
                    }
                    TimeSpan span = DateTime.Now - xx.LastSeen;
                    response = "Last time I saw " + nick + " they were " + action + " " + xx.lastplace + " at " + xx.LastSeen.ToString() + " (" + span.ToString() + " ago)";
                    break;
                }
            }
            if (nick.ToUpper() == source.ToUpper())
            {
                response = "are you really looking for yourself?";
                core.irc._SlowQueue.DeliverMessage(source + ": " + response, channel.Name, IRC.priority.normal);
                return;
            }
            if (nick.ToUpper() == config.username.ToUpper())
            {
                response = "I am right here";
                core.irc._SlowQueue.DeliverMessage(source + ": " + response, channel.Name, IRC.priority.normal);
                return;
            }
            if (channel.containsUser(nick))
            {
                response = nick + " is in here, right now";
                found = true;
            }
            if (!found)
            {
                foreach (config.channel Item in config.channels)
                {
                    if (Item.containsUser(nick))
                    {
                        response = nick + " is in " + Item.Name + " right now";
                        break;
                    }
                }
            }
            core.irc._SlowQueue.DeliverMessage(source + ": " + response, channel.Name, IRC.priority.normal);
        }

        public void Save()
        {
            XmlDocument stat = new XmlDocument();
            XmlNode xmlnode = stat.CreateElement("channel_stat");
            lock (global)
            {
                foreach (item curr in global)
                {
                    XmlAttribute name = stat.CreateAttribute("nick");
                    name.Value = curr.nick;
                    XmlAttribute host = stat.CreateAttribute("hostname");
                    host.Value = curr.hostname.ToString();
                    XmlAttribute last = stat.CreateAttribute("lastplace");
                    last.Value = curr.lastplace;
                    XmlAttribute action = stat.CreateAttribute("action");
                    XmlAttribute date = stat.CreateAttribute("date");
                    date.Value = curr.LastSeen.ToBinary().ToString();
                    action.Value = "Exit";
                    switch (curr.LastAc)
                    {
                        case item.Action.Join:
                            action.Value = "Join";
                            break;
                        case item.Action.Part:
                            action.Value = "Part";
                            break;
                        case item.Action.Kick:
                            action.Value = "Kick";
                            break;
                        case item.Action.Talk:
                            action.Value = "Talk";
                            break;
                    }
                    XmlNode db = stat.CreateElement("user");
                    db.Attributes.Append(name);
                    db.Attributes.Append(host);
                    db.Attributes.Append(last);
                    db.Attributes.Append(action);
                    db.Attributes.Append(date);
                    xmlnode.AppendChild(db);
                }
            }
            stat.AppendChild(xmlnode);
            if (System.IO.File.Exists(variables.config + System.IO.Path.DirectorySeparatorChar + "seen.db"))
            {
                core.backupData(variables.config + System.IO.Path.DirectorySeparatorChar + "seen.db");
            }
            stat.Save(variables.config + System.IO.Path.DirectorySeparatorChar + "seen.db");
            if (System.IO.File.Exists(config.tempName(variables.config + System.IO.Path.DirectorySeparatorChar + "seen.db")))
            {
                System.IO.File.Delete(config.tempName(variables.config + System.IO.Path.DirectorySeparatorChar + "seen.db"));
            }
        }

        public void LoadData()
        {
            SearchHostThread = new Thread(StartRegex);
            SearchHostThread.Start();
            try
            {
                core.recoverFile(variables.config + System.IO.Path.DirectorySeparatorChar + "seen.db");
                if (System.IO.File.Exists(variables.config + System.IO.Path.DirectorySeparatorChar + "seen.db"))
                {
                    lock (global)
                    {
                        global = new List<item>();
                        XmlDocument stat = new XmlDocument();
                        stat.Load(variables.config + System.IO.Path.DirectorySeparatorChar + "seen.db");
                        if (stat.ChildNodes[0].ChildNodes.Count > 0)
                        {
                            foreach (XmlNode curr in stat.ChildNodes[0].ChildNodes)
                            {
                                try
                                {
                                    string user = curr.Attributes[0].Value;
                                    item.Action action = item.Action.Exit;
                                    switch (curr.Attributes[3].Value)
                                    {
                                        case "Join":
                                            action = item.Action.Join;
                                            break;
                                        case "Part":
                                            action = item.Action.Part;
                                            break;
                                        case "Talk":
                                            action = item.Action.Talk;
                                            break;
                                        case "Kick":
                                            action = item.Action.Kick;
                                            break;
                                    }
                                    item User = new item(user, curr.Attributes[1].Value, curr.Attributes[2].Value, action, curr.Attributes[4].Value);
                                    global.Add(User);
                                }
                                catch (Exception fail)
                                {
                                    core.handleException(fail);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception f)
            {
                core.handleException(f);
            }
        }
    }
}