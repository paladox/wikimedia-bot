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
using System.IO;

namespace wmib
{
    public class dictionary
    {
        /// <summary>
        /// Data file
        /// </summary>
        public string datafile = "";

        // if we need to update dump
        public bool update = true;

        /// <summary>
        /// Locked
        /// </summary>
        public bool locked = false;

        public class item
        {
            /// <summary>
            /// Text
            /// </summary>
            public string text;

            /// <summary>
            /// Key
            /// </summary>
            public string key;

            public string user;

            public string locked;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="Key">Key</param>
            /// <param name="Text">Text of the key</param>
            /// <param name="User">User who created the key</param>
            /// <param name="Lock">If key is locked or not</param>
            public item(string Key, string Text, string User, string Lock = "false")
            {
                text = Text;
                key = Key;
                locked = Lock;
                user = User;
            }
        }

        public class staticalias
        {
            /// <summary>
            /// Name
            /// </summary>
            public string Name;

            /// <summary>
            /// Key
            /// </summary>
            public string Key;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="name">Alias</param>
            /// <param name="key">Key</param>
            public staticalias(string name, string key)
            {
                Name = name;
                Key = key;
            }
        }

        /// <summary>
        /// List of all items in class
        /// </summary>
        public List<item> text = new List<item>();

        /// <summary>
        /// List of all aliases we want to use
        /// </summary>
        public List<staticalias> Alias = new List<staticalias>();

        /// <summary>
        /// Channel name
        /// </summary>
        public string Channel;

        private bool running;

        private string search_key;

        /// <summary>
        /// Load it
        /// </summary>
        public void Load()
        {
            text.Clear();
            if (!File.Exists(datafile))
            {
                // Create db
                File.WriteAllText(datafile, "");
            }

            string[] db = File.ReadAllLines(datafile);
            foreach (string x in db)
            {
                if (x.Contains(config.separator))
                {
                    string[] info = x.Split(Char.Parse(config.separator));
                    string type = info[2];
                    string value = info[1];
                    string name = info[0];
                    if (type == "key")
                    {
                        string locked = info[3];
                        text.Add(new item(name, value, "", locked));
                    }
                    else
                    {
                        Alias.Add(new staticalias(name, value));
                    }
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="database"></param>
        /// <param name="channel"></param>
        public dictionary(string database, string channel)
        {
            datafile = database;
            Channel = channel;
            Load();
        }

        /// <summary>
        /// Save to a file
        /// </summary>
        public void Save()
        {
            update = true;
            try
            {
                File.WriteAllText(datafile, "");
                foreach (staticalias key in Alias)
                {
                    File.AppendAllText(datafile,
                                       key.Name + config.separator + key.Key + config.separator + "alias" + "\n");
                }
                foreach (item key in text)
                {
                    File.AppendAllText(datafile,
                                       key.key + config.separator + key.text + config.separator + "key" +
                                       config.separator + key.locked + config.separator + key.user + "\n");
                }
            }
            catch (Exception b)
            {
                core.handleException(b, Channel);
            }
        }

        /// <summary>
        /// Get value of key
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public string getValue(string key)
        {
            foreach (item data in text)
            {
                if (data.key == key)
                {
                    return core.decode(data.text);
                }
            }
            return "";
        }

        /// <summary>
        /// Print a value to channel if found this message doesn't need to be a valid command
        /// </summary>
        /// <param name="name">Name</param>
        /// <param name="user">User</param>
        /// <param name="chan">Channel</param>
        /// <param name="host">Host name</param>
        /// <returns></returns>
        public bool print(string name, string user, config.channel chan, string host)
        {
            if (!name.StartsWith("!"))
            {
                return true;
            }
            name = name.Substring(1);
            if (name.Contains(" "))
            {
                string[] parm = name.Split(' ');
                if (parm[1] == "is")
                {
                    if (chan.Users.isApproved(user, host, "info"))
                    {
                        if (parm.Length < 3)
                        {
                            core.irc.Message(messages.get("key", chan.ln), Channel);
                            return true;
                        }
                        string key = name.Substring(name.IndexOf(" is") + 4);
                        setKey(key, parm[0], "");
                    }
                    else
                    {
                        core.irc._SlowQueue.DeliverMessage(messages.get("Authorization", chan.ln), Channel);
                    }
                    return false;
                }
                if (parm[1] == "alias")
                {
                    if (chan.Users.isApproved(user, host, "info"))
                    {
                        if (parm.Length < 3)
                        {
                            core.irc.Message(messages.get("key", chan.ln), Channel);
                            return true;
                        }
                        this.aliasKey(name.Substring(name.IndexOf(" alias") + 7), parm[0], "");
                    }
                    else
                    {
                        core.irc._SlowQueue.DeliverMessage(messages.get("Authorization", chan.ln), Channel);
                    }
                    return false;
                }
                if (parm[1] == "unalias")
                {

                    if (chan.Users.isApproved(user, host, "info"))
                    {
                        foreach (staticalias b in Alias)
                        {
                            if (b.Name == parm[0])
                            {
                                Alias.Remove(b);
                                core.irc.Message(messages.get("AliasRemoved", chan.ln), Channel);
                                Save();
                                return false;
                            }
                        }
                        return false;
                    }
                    core.irc._SlowQueue.DeliverMessage(messages.get("Authorization", chan.ln), Channel);
                    return false;
                }
                if (parm[1] == "del")
                {
                    if (chan.Users.isApproved(user, host, "info"))
                    {
                        rmKey(parm[0], "");
                    }
                    else
                    {
                        core.irc._SlowQueue.DeliverMessage(messages.get("Authorization", chan.ln), Channel);
                    }
                    return false;
                }
            }
            string User = "";
            if (name.Contains("|"))
            {
                User = name.Substring(name.IndexOf("|") + 1);
                if (User.StartsWith( " " ))
                {
                    while (User.StartsWith(" "))
                    {
                        User = User.Substring(1);
                    }
                }
                name = name.Substring(0, name.IndexOf("|"));
            }
            string[] p = name.Split(' ');
            int parameters = p.Length;
            string keyv = getValue(p[0]);
            if (keyv != "")
            {
                if (parameters > 1)
                {
                    int curr = 1;
                    while (parameters > curr)
                    {
                        keyv = keyv.Replace("$" + curr.ToString(), p[curr]);
                        curr++;
                    }
                }
                if (User == "")
                {
                    core.irc._SlowQueue.DeliverMessage(keyv, Channel);
                }
                else
                {
                    core.irc._SlowQueue.DeliverMessage(User + ": " + keyv, Channel);
                }
                return true;
            }
            foreach (staticalias b in Alias)
            {
                if (b.Name == p[0])
                {
                    keyv = getValue(b.Key);
                    if (keyv != "")
                    {
                        if (parameters > 1)
                        {
                            int curr = 1;
                            while (parameters > curr)
                            {
                                keyv = keyv.Replace("$" + curr.ToString(), p[curr]);
                                curr++;
                            }
                        }
                        if (User == "")
                        {
                            core.irc._SlowQueue.DeliverMessage(keyv, Channel);
                        }
                        else
                        {
                            core.irc._SlowQueue.DeliverMessage(User + ": " + keyv, Channel);
                        }
                        return true;
                    }
                }
            }
            return true;
        }

        private void StartSearch()
        {
            Regex value = new Regex(search_key, RegexOptions.Compiled);
            config.channel _channel = core.getChannel(Channel);
            string results = "";
            int count = 0;
            foreach (item data in text)
            {
                if (data.key == search_key || value.Match(data.text).Success)
                {
                    count++;
                    results = results + data.key + ", ";
                }
            }
            if (results == "")
            {
                core.irc._SlowQueue.DeliverMessage(messages.get("ResultsWereNotFound", _channel.ln), Channel);
            }
            else
            {
                core.irc._SlowQueue.DeliverMessage(messages.get("Results", _channel.ln) + "(" + messages.get("ResultsFound", _channel.ln) + count.ToString() + "): " + results, Channel);
            }
            running = false;
        }

        /// <summary>
        /// Search
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="Chan"></param>
        public void RSearch(string key, config.channel Chan)
        {
            if (!key.StartsWith("@regsearch"))
            {
                return;
            }
            if (!misc.IsValidRegex(key))
            {
                core.irc.Message(messages.get("Error1", Chan.ln), Chan.name);
                return;
            }
            if (key.Length < 11)
            {
                core.irc.Message(messages.get("Search1", Chan.ln), Chan.name);
                return;
            }
            search_key = key.Substring(11);
            running = true;
            Thread th = new Thread(StartSearch);
            th.Start();
            int check = 1;
            while (running)
            {
                check++;
                Thread.Sleep(100);
                if (check > 8)
                {
                    th.Abort();
                    core.irc.Message(messages.get("Error2", Chan.ln), Channel);
                    running = false;
                    return;
                }
            }
        }

        public void Find(string key, config.channel Chan)
        {
            if (!key.StartsWith("@search"))
            {
                return;
            }
            if (key.Length < 9)
            {
                core.irc.Message(messages.get("Error1", Chan.ln), Chan.name);
                return;
            }
            key = key.Substring(8);
            int count = 0;
            string results = "";
            foreach (item data in text)
            {
                if (data.key == key || data.text.Contains(key))
                {
                    results = results + data.key + ", ";
                    count++;
                }
            }
            if (results == "")
            {
                core.irc._SlowQueue.DeliverMessage(messages.get("ResultsWereNotFound", Chan.ln), Chan.name);
            }
            else
            {
                core.irc._SlowQueue.DeliverMessage(messages.get("Results", Chan.ln) + "(" + messages.get("ResultsFound", Chan.ln) + count.ToString() + "): " + results, Chan.name);
            }
        }

        /// <summary>
        /// Save a new key
        /// </summary>
        /// <param name="Text">Text</param>
        /// <param name="key">Key</param>
        /// <param name="user">User who created it</param>
        public void setKey(string Text, string key, string user)
        {
            while (locked)
            {
                Thread.Sleep(200);
            }
			config.channel ch = core.getChannel (Channel);
            try
            {
                foreach (item data in text)
                {
                    if (data.key == key)
                    {
                        core.irc._SlowQueue.DeliverMessage(messages.get("Error3", ch.ln), Channel);
                        return;
                    }
                }
                text.Add(new item(key, core.encode(Text), user, "false"));
                core.irc.Message("Key was added!", Channel);
                Save();
            }
            catch (Exception b)
            {
                core.handleException(b, Channel);
            }
        }

        /// <summary>
        /// Alias
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="al">Alias</param>
        /// <param name="user">User</param>
        public void aliasKey(string key, string al, string user)
        {
            foreach (staticalias stakey in this.Alias)
            {
                if (stakey.Name == al)
                {
                    core.irc._SlowQueue.DeliverMessage("Alias is already existing!", Channel);
                    return;
                }
            }
            this.Alias.Add(new staticalias(al, key));
            core.irc._SlowQueue.DeliverMessage("Successfully created", Channel);
            Save();
        }

        public void rmKey(string key, string user)
        {
            while (locked)
            {
                Thread.Sleep(200);
            }
            foreach (item keys in text)
            {
                if (keys.key == key)
                {
                    text.Remove(keys);
                    core.irc._SlowQueue.DeliverMessage("Successfully removed " + key, Channel);
                    Save();
                    return;
                }
            }
            core.irc._SlowQueue.DeliverMessage("Unable to find the specified key in db", Channel);
        }
    }

}
