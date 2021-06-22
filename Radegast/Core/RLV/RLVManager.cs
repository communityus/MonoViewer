﻿// 
// Radegast Metaverse Client
// Copyright (c) 2009-2014, Radegast Development Team
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice,
//       this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of the application "Radegast", nor the names of its
//       contributors may be used to endorse or promote products derived from
//       this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// $Id$
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Radegast
{
    public class RLVManager : IDisposable
    {
        #region Events
        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<RLVEventArgs> m_RLVRuleChanged;

        /// <summary>Raises the RLVRuleChanged event</summary>
        /// <param name="e">An RLVRuleChangedEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnRLVRuleChanged(RLVEventArgs e)
        {
            EventHandler<RLVEventArgs> handler = m_RLVRuleChanged;
            try
            {
                handler?.Invoke(this, e);
            }
            catch (Exception) { }

        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_RLVRuleChangedLock = new object();

        /// <summary>Triggered when an RLVRuleChangedUpdate packet is received,
        /// telling us what our avatar is currently wearing
        /// <see cref="RequestRLVRuleChanged"/> request.</summary>
        public event EventHandler<RLVEventArgs> RLVRuleChanged
        {
            add { lock (m_RLVRuleChangedLock) { m_RLVRuleChanged += value; } }
            remove { lock (m_RLVRuleChangedLock) { m_RLVRuleChanged -= value; } }
        }
        #endregion

        #region Helper classes, methods, structs and enums
        public struct RLVWearable
        {
            public string Name { get; set; }
            public WearableType Type { get; set; }
        }

        public struct RLVAttachment
        {
            public string Name { get; set; }
            public AttachmentPoint Point { get; set; }
        }

        public static readonly List<RLVWearable> RLVWearables;
        public static readonly List<RLVAttachment> RLVAttachments;
        
        static RLVManager()
        {
            RLVWearables = new List<RLVWearable>();
            foreach (WearableType wear in Enum.GetValues(typeof(WearableType)))
            {
                if (wear.Equals(WearableType.Invalid)) continue;
                string wearstr = Utils.EnumToText(wear);
                RLVWearables.Add(new RLVWearable() {Name = wearstr.ToLower(), Type = wear});
            }

            RLVAttachments = new List<RLVAttachment>
            {
                new RLVAttachment() {Name = "none", Point = AttachmentPoint.Default}
            };
            foreach (AttachmentPoint pt in Enum.GetValues(typeof(AttachmentPoint)))
            {
                string ptstr = Utils.EnumToText(pt);
                if (pt.Equals(AttachmentPoint.Default) || ptstr.StartsWith("HUD")) continue;
                RLVAttachments.Add(new RLVAttachment {Name = ptstr.ToLower(), Point = pt});
            }
        }

        public static WearableType WearableFromString(string type)
        {
            var found = RLVWearables.FindAll(w => w.Name == type);
            return found.Count == 1 ? found[0].Type : WearableType.Invalid;
        }

        public static AttachmentPoint AttachmentPointFromString(string point)
        {
            var found = RLVAttachments.FindAll(a => a.Name == point);
            return found.Count == 1 ? found[0].Point : AttachmentPoint.Default;
        }

        #endregion Helper classes, structs and enums

        public bool Enabled
        {
            get
            {
                if (instance.GlobalSettings["rlv_enabled"].Type == OSDType.Unknown)
                {
                    instance.GlobalSettings["rlv_enabled"] = new OSDBoolean(false);
                }

                return instance.GlobalSettings["rlv_enabled"].AsBoolean();
            }

            set
            {
                if (Enabled != instance.GlobalSettings["rlv_enabled"].AsBoolean())
                {
                    instance.GlobalSettings["rlv_enabled"] = new OSDBoolean(value);
                    OnRLVRuleChanged(new RLVEventArgs(null));
                }

                if (value)
                    StartTimer();
                else
                    StopTimer();
            }
        }

        public bool EnabledDebugCommands
        {
            get
            {
                if (instance.GlobalSettings["rlv_debugcommands"].Type == OSDType.Unknown)
                {
                    instance.GlobalSettings["rlv_debugcommands"] = new OSDBoolean(false);
                }

                return instance.GlobalSettings["rlv_debugcommands"].AsBoolean();
            }

            set
            {
                if (EnabledDebugCommands != instance.GlobalSettings["rlv_debugcommands"].AsBoolean())
                {
                    instance.GlobalSettings["rlv_debugcommands"] = new OSDBoolean(value);
                }
            }
        }

        readonly RadegastInstance instance;
        GridClient client => instance.Client;
        readonly Regex rlv_regex = new Regex(@"(?<behaviour>[^:=]+)(:(?<option>[^=]*))?=(?<param>\w+)", RegexOptions.Compiled);
        readonly List<RLVRule> rules = new List<RLVRule>();
        System.Timers.Timer CleanupTimer;

        public RLVManager(RadegastInstance instance)
        {
            this.instance = instance;

            if (Enabled)
            {
                StartTimer();
            }
        }

        public void Dispose()
        {
            StopTimer();
        }

        void StartTimer()
        {
            StopTimer();
            CleanupTimer = new System.Timers.Timer()
            {
                Enabled = true,
                Interval = 120 * 1000 // two minutes
            };

            CleanupTimer.Elapsed += new System.Timers.ElapsedEventHandler(CleanupTimer_Elapsed);
        }

        void CleanupTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            List<UUID> objecs = new List<UUID>();
            lock (rules)
            {
                foreach (var rule in rules)
                {
                    if (!objecs.Contains(rule.Sender))
                        objecs.Add(rule.Sender);
                }
            }

            foreach (UUID obj in objecs)
            {
                if (client.Network.CurrentSim.ObjectsPrimitives.Find(p => p.ID == obj) == null)
                {
                    Clear(obj);
                }
            }
        }

        void StopTimer()
        {
            if (CleanupTimer != null)
            {
                CleanupTimer.Enabled = false;
                CleanupTimer.Dispose();
                CleanupTimer = null;
            }
        }

        public bool TryProcessCMD(ChatEventArgs e)
        {
            if (!Enabled || !e.Message.StartsWith("@")) return false;

            if (e.Message == "@clear")
            {
                Clear(e.SourceID);
                return true;
            }

            foreach (var cmd in e.Message.Substring(1).Split(','))
            {
                var m = rlv_regex.Match(cmd);
                if (!m.Success) continue;

                var rule = new RLVRule
                {
                    Behaviour = m.Groups["behaviour"].ToString().ToLower(),
                    Option = m.Groups["option"].ToString().ToLower(),
                    Param = m.Groups["param"].ToString().ToLower(),
                    Sender = e.SourceID,
                    SenderName = e.FromName
                };

                Logger.DebugLog(rule.ToString());

                if (rule.Param == "rem") rule.Param = "y";
                if (rule.Param == "add") rule.Param = "n";

                switch (rule.Param)
                {
                    case "n":
                        lock (rules)
                        {
                            var existing = rules.Find(r =>
                                r.Behaviour == rule.Behaviour &&
                                r.Sender == rule.Sender &&
                                r.Option == rule.Option);

                            if (existing != null)
                            {
                                rules.Remove(existing);
                            }
                            rules.Add(rule);
                            OnRLVRuleChanged(new RLVEventArgs(rule));
                        }
                        continue;
                    case "y":
                        lock (rules)
                        {
                            if (rule.Option == "")
                            {
                                rules.RemoveAll(r => r.Behaviour == rule.Behaviour && r.Sender == rule.Sender);
                            }
                            else
                            {
                                rules.RemoveAll(r => r.Behaviour == rule.Behaviour && r.Sender == rule.Sender && r.Option == rule.Option);
                            }
                        }

                        OnRLVRuleChanged(new RLVEventArgs(rule));
                        continue;
                }


                switch (rule.Behaviour)
                {
                    case "version":
                        int chan = 0;
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            Respond(chan, "RestrainedLife viewer v1.23 (" + Properties.Resources.RadegastTitle + " " + Assembly.GetExecutingAssembly().GetName().Version + ")");
                        }
                        break;

                    case "versionnew":
                        chan = 0;
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            Respond(chan, "RestrainedLove viewer v1.23 (" + Properties.Resources.RadegastTitle + " " + Assembly.GetExecutingAssembly().GetName().Version + ")");
                        }
                        break;


                    case "versionnum":
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            Respond(chan, "1230100");
                        }
                        break;

                    case "getgroup":
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            UUID gid = client.Self.ActiveGroup;
                            if (instance.Groups.ContainsKey(gid))
                            {
                                Respond(chan, instance.Groups[gid].Name);
                            }
                        }
                        break;

                    case "setgroup":
                        {
                            if (rule.Param == "force")
                            {
                                foreach (var g in instance.Groups.Values)
                                {
                                    if (g.Name.ToLower() == rule.Option)
                                    {
                                        client.Groups.ActivateGroup(g.ID);
                                    }
                                }
                            }
                        }
                        break;

                    case "getsitid":
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            Avatar me;
                            if (client.Network.CurrentSim.ObjectsAvatars.TryGetValue(client.Self.LocalID, out me))
                            {
                                if (me.ParentID != 0)
                                {
                                    Primitive seat;
                                    if (client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(me.ParentID, out seat))
                                    {
                                        Respond(chan, seat.ID.ToString());
                                        break;
                                    }
                                }
                            }
                            Respond(chan, UUID.Zero.ToString());
                        }
                        break;

                    case "getstatusall":
                    case "getstatus":
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            string sep = "/";
                            string filter = "";

                            if (!string.IsNullOrEmpty(rule.Option))
                            {
                                var parts = rule.Option.Split(';');
                                if (parts.Length > 1 && parts[1].Length > 0)
                                {
                                    sep = parts[1].Substring(0, 1);
                                }
                                if (parts.Length > 0 && parts[0].Length > 0)
                                {
                                    filter = parts[0].ToLower();
                                }
                            }

                            lock (rules)
                            {
                                string res = "";
                                rules
                                    .FindAll(r => (rule.Behaviour == "getstatusall" || r.Sender == rule.Sender) && r.Behaviour.Contains(filter))
                                    .ForEach(objRule =>
                                {
                                    res += sep + objRule.Behaviour;
                                    if (!string.IsNullOrEmpty(objRule.Option))
                                    {
                                        res += ":" + objRule.Option;
                                    }
                                });
                                Respond(chan, res);
                            }
                        }
                        break;

                    case "sit":
                        UUID sitTarget = UUID.Zero;

                        if (rule.Param == "force" && UUID.TryParse(rule.Option, out sitTarget) && sitTarget != UUID.Zero)
                        {
                            instance.State.SetSitting(true, sitTarget);
                        }
                        break;

                    case "unsit":
                        if (rule.Param == "force")
                        {
                            instance.State.SetSitting(false, UUID.Zero);
                        }
                        break;

                    case "setrot":
                        double rot = 0.0;

                        if (rule.Param == "force" && double.TryParse(rule.Option, System.Globalization.NumberStyles.Float, Utils.EnUsCulture, out rot))
                        {
                            client.Self.Movement.UpdateFromHeading(Math.PI / 2d - rot, true);
                        }
                        break;

                    case "tpto":
                        var coord = rule.Option.Split('/');

                        try
                        {
                            float gx = float.Parse(coord[0], Utils.EnUsCulture);
                            float gy = float.Parse(coord[1], Utils.EnUsCulture);
                            float z = float.Parse(coord[2], Utils.EnUsCulture);
                            float x = 0, y = 0;

                            instance.TabConsole.DisplayNotificationInChat("Starting teleport...");
                            ulong h = Helpers.GlobalPosToRegionHandle(gx, gy, out x, out y);
                            client.Self.RequestTeleport(h, new Vector3(x, y, z));
                        }
                        catch (Exception) { }

                        break;

                    #region #RLV folder and outfit manipulation
                    case "getoutfit":
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            var wearables = client.Appearance.GetWearablesByType();
                            string res = "";

                            // Do we have a specific wearable to check, ie @getoutfit:socks=99
                            if (!string.IsNullOrEmpty(rule.Option))
                            {
                                res = wearables.ContainsKey(WearableFromString(rule.Option)) ? "1" : "0";
                            }
                            else
                            {
                                foreach (var t in RLVWearables)
                                {
                                    if (wearables.ContainsKey(t.Type))
                                    {
                                        res += "1";
                                    }
                                    else
                                    {
                                        res += "0";
                                    }
                                }
                            }
                            Respond(chan, res);
                        }
                        break;

                    case "getattach":
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            string res = "";
                            var attachments = client.Network.CurrentSim.ObjectsPrimitives.FindAll(p => p.ParentID == client.Self.LocalID);
                            if (attachments.Count > 0)
                            {
                                var myPoints = new List<AttachmentPoint>(attachments.Count);
                                foreach (var p in attachments)
                                {
                                    if (!myPoints.Contains(p.PrimData.AttachmentPoint))
                                    {
                                        myPoints.Add(p.PrimData.AttachmentPoint);
                                    }
                                }

                                // Do we want to check one single attachment
                                if (!string.IsNullOrEmpty(rule.Option))
                                {
                                    res = myPoints.Contains(AttachmentPointFromString(rule.Option)) ? "1" : "0";
                                }
                                else
                                {
                                    foreach (var a in RLVAttachments)
                                    {
                                        if (myPoints.Contains(a.Point))
                                        {
                                            res += "1";
                                        }
                                        else
                                        {
                                            res += "0";
                                        }
                                    }
                                }


                            }
                            Respond(chan, res);
                        }
                        break;

                    case "remattach":
                    case "detach":
                        if (rule.Param == "force")
                        {
                            if (!string.IsNullOrEmpty(rule.Option))
                            {
                                var point = RLVAttachments.Find(a => a.Name == rule.Option);
                                if (point.Name == rule.Option)
                                {
                                    var attachment = client.Network.CurrentSim.ObjectsPrimitives.Find(p => p.ParentID == client.Self.LocalID && p.PrimData.AttachmentPoint == point.Point);
                                    if (attachment != null && client.Inventory.Store.Items.ContainsKey(CurrentOutfitFolder.GetAttachmentItem(attachment)))
                                    {
                                        instance.COF.Detach((InventoryItem)client.Inventory.Store.Items[CurrentOutfitFolder.GetAttachmentItem(attachment)].Data);
                                    }
                                }
                                else
                                {
                                    InventoryNode folder = FindFolder(rule.Option);
                                    if (folder != null)
                                    {
                                        var outfit = (from item in folder.Nodes.Values where CurrentOutfitFolder.CanBeWorn(item.Data) select (InventoryItem) (item.Data)).ToList();
                                        instance.COF.RemoveFromOutfit(outfit);
                                    }
                                }
                            }
                            else
                            {
                                client.Network.CurrentSim.ObjectsPrimitives.FindAll(p => p.ParentID == client.Self.LocalID).ForEach(attachment =>
                                {
                                    if (client.Inventory.Store.Items.ContainsKey(CurrentOutfitFolder.GetAttachmentItem(attachment)))
                                    {
                                        instance.COF.Detach((InventoryItem)client.Inventory.Store.Items[CurrentOutfitFolder.GetAttachmentItem(attachment)].Data);
                                    }
                                });
                            }
                        }
                        break;
                    case "detachall":
                        if (rule.Param == "force")
                        {
                            if (!string.IsNullOrEmpty(rule.Option))
                            {
                                InventoryNode folder = FindFolder(rule.Option);
                                if (folder != null)
                                {
                                    List<InventoryItem> allItems = new List<InventoryItem>();
                                    AllSubfolderWearables(folder, ref allItems);
                                    List<InventoryItem> allSubfolderWorn = new List<InventoryItem>();
                                    foreach (var n in allItems)
                                    {
                                        if (CurrentOutfitFolder.CanBeWorn(n))
                                        {
                                            allSubfolderWorn.Add(n);
                                        }
                                    }
                                    instance.COF.RemoveFromOutfit(allSubfolderWorn);
                                }
                            }
                        }
                        break;

                    case "remoutfit":
                        if (rule.Param == "force")
                        {
                            if (!string.IsNullOrEmpty(rule.Option))
                            {
                                var w = RLVWearables.Find(a => a.Name == rule.Option);
                                if (w.Name == rule.Option)
                                {
                                    var items = instance.COF.GetWornAt(w.Type);
                                    instance.COF.RemoveFromOutfit(items);
                                }
                            }
                        }
                        break;

                    case "attach":
                    case "attachoverorreplace":
                    case "attachover":
                    case "attachall":
                    case "attachallover":
                        if (rule.Param == "force")
                        {
                            if (!string.IsNullOrEmpty(rule.Option))
                            {
                                InventoryNode folder = FindFolder(rule.Option);
                                if (folder != null)
                                {
                                    List<InventoryItem> outfit = new List<InventoryItem>();
                                    if(rule.Behaviour == "attachall" || rule.Behaviour == "attachallover")
                                    {
                                        GetAllItems(folder, true, ref outfit);
                                    }
                                    else
                                    {
                                        GetAllItems(folder, false, ref outfit);
                                    }

                                    if (rule.Behaviour == "attachover" || rule.Behaviour == "attachallover")
                                    {
                                        instance.COF.AddToOutfit(outfit, false);
                                    }
                                    else
                                    {
                                        instance.COF.AddToOutfit(outfit, true);
                                    }
                                }
                            }
                        }
                        break;

                    case "getinv":
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            string res = string.Empty;
                            InventoryNode folder = FindFolder(rule.Option);
                            if (folder != null)
                            {
                                res = folder.Nodes.Values.Where(
                                    f => f.Data is InventoryFolder && !f.Data.Name.StartsWith(".")).Aggregate(
                                        res, (current, f) => current + (f.Data.Name + ","));
                            }
                            
                            Respond(chan, res.TrimEnd(','));
                        }
                        break;

                    case "getinvworn":
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            string res = string.Empty;
                            InventoryNode root = FindFolder(rule.Option);
                            if (root != null)
                            {
                                res += "|" + GetWornIndicator(root) + ",";
                                res = root.Nodes.Values.Where(
                                    n => n.Data is InventoryFolder && !n.Data.Name.StartsWith(".")).Aggregate(
                                        res, (current, n) => current + (n.Data.Name + "|" + GetWornIndicator(n) + ","));
                            }

                            Respond(chan, res.TrimEnd(','));
                        }
                        break;

                    case "findfolder":
                    case "findfolders":
                        if (int.TryParse(rule.Param, out chan) && chan > 0)
                        {
                            StringBuilder response = new StringBuilder();

                            string[] keywordsArray = rule.Option.Split(new string[] {"&&"}, StringSplitOptions.None);
                            if (keywordsArray.Any())
                            {
                                List<InventoryNode> matching_nodes = FindFoldersKeyword(keywordsArray);
                                if(matching_nodes.Any())
                                {
                                    if(rule.Behaviour == "findfolder")
                                    {
                                        InventoryNode bestCandidate = null;
                                        int bestCandidateSlashCount = -1;
                                        foreach (var match in matching_nodes)
                                        {
                                            string fullPath = FindFullInventoryPath(match, "");
                                            int numSlashes = fullPath.Count(ch => ch == '/');
                                            if(bestCandidate == null || numSlashes > bestCandidateSlashCount)
                                            {
                                                bestCandidateSlashCount = numSlashes;
                                                bestCandidate = match;
                                            }
                                        }

                                        string bestCandidatePath = bestCandidate.Data.Name;
                                        if (bestCandidatePath.Substring(0, 5).ToLower() == @"#rlv/")
                                        {
                                            bestCandidatePath = bestCandidatePath.Substring(5);
                                        }
                                        response.Append(bestCandidatePath);
                                    }
                                    else
                                    {
                                        foreach (var node in matching_nodes)
                                        {
                                            string fullPath = FindFullInventoryPath(node, "");
                                            if (fullPath.Length > 4 && fullPath.Substring(0, 5).ToLower() == @"#rlv/")
                                            {
                                                fullPath = fullPath.Substring(5);
                                            }
                                            response.Append(fullPath + ",");
                                        }
                                    }
                                }
                            }

                            Respond(chan, response.ToString().TrimEnd(','));
                        }
                        break;

                    #endregion #RLV folder and outfit manipulation

                }
            }

            return true;
        }

        private void GetAllItems(InventoryNode root, bool recursive, ref List<InventoryItem> items)
        {
            foreach (var item in root.Nodes.Values)
            {
                if (CurrentOutfitFolder.CanBeWorn(item.Data))
                {
                    items.Add((InventoryItem)item.Data);
                }
                if(recursive)
                {
                    foreach (var child in item.Nodes.Values)
                    {
                        GetAllItems(child, true, ref items);
                    }
                }
            }
        }

        private void Respond(int chan, string msg)
        {
#if DEBUG
            instance.TabConsole.DisplayNotificationInChat($"{chan}: {msg}", ChatBufferTextStyle.OwnerSay);
#endif
            client.Self.Chat(msg, chan, ChatType.Normal);
        }


        public void AllSubfolderWearables(InventoryNode root, ref List<InventoryItem> WearableItems)
        {
            foreach (var n in root.Nodes.Values)
            {
                if (!n.Data.Name.StartsWith("."))
                {
                    if (n.Data is InventoryFolder)
                    {
                        AllSubfolderWearables(n, ref WearableItems);
                    }
                    else
                    {
                        WearableItems.Add((InventoryItem)n.Data);
                    }
                }
            }

        }

        protected string GetWornIndicator(InventoryNode node)
        {
            var currentOutfit = new List<AppearanceManager.WearableData>(client.Appearance.GetWearables());
            var currentAttachments = client.Network.CurrentSim.ObjectsPrimitives.FindAll(p => p.ParentID == client.Self.LocalID);
            int myItemsCount = 0;
            int myItemsWornCount = 0;

            foreach (var n in node.Nodes.Values)
            {
                if (CurrentOutfitFolder.CanBeWorn(n.Data) && !n.Data.Name.StartsWith("."))
                {
                    myItemsCount++;
                    if ((n.Data is InventoryWearable && CurrentOutfitFolder.IsWorn(currentOutfit, (InventoryItem)n.Data)) ||
                        CurrentOutfitFolder.IsAttached(currentAttachments, (InventoryItem)n.Data))
                    {
                        myItemsWornCount++;
                    }
                }
            }

            List<InventoryItem> allItems = new List<InventoryItem>();
            foreach (var n in node.Nodes.Values)
            {
                if (n.Data is InventoryFolder && !n.Data.Name.StartsWith("."))
                {
                    AllSubfolderWearables(n, ref allItems);
                }
            }

            int allItemsCount = 0;
            int allItemsWornCount = 0;

            foreach (var n in allItems)
            {
                if (CurrentOutfitFolder.CanBeWorn(n) && !n.Name.StartsWith("."))
                {
                    allItemsCount++;
                    if ((n is InventoryWearable && CurrentOutfitFolder.IsWorn(currentOutfit, n)) ||
                        CurrentOutfitFolder.IsAttached(currentAttachments, n))
                    {
                        allItemsWornCount++;
                    }
                }
            }

            return WornIndicator(myItemsCount, myItemsWornCount) + WornIndicator(allItemsCount, allItemsWornCount);
        }

        protected string WornIndicator(int nrMyItem, int nrWorn)
        {
            string first = "0";

            if (nrMyItem > 0)
            {
                if (nrMyItem == nrWorn)
                {
                    first = "3";
                }
                else if (nrWorn > 0)
                {
                    first = "2";
                }
                else
                {
                    first = "1";
                }
            }

            return first;
        }

        public InventoryNode RLVRootFolder()
        {
            return client.Inventory.Store.RootNode.Nodes.Values.FirstOrDefault(rn => rn.Data.Name == "#RLV" && rn.Data is InventoryFolder);
        }

        public string FindFullInventoryPath(InventoryNode input, string pathConstruct)
        {
            if (input.Parent == null) {
                return pathConstruct.TrimEnd('/');
            } else {
                pathConstruct = input.Data.Name + "/" + pathConstruct;
                return FindFullInventoryPath (input.Parent, pathConstruct);
            }
        }

        public InventoryNode FindFolder(string path)
        {
            var root = RLVRootFolder();
            return root == null ? null : FindFolderInternal(root, "/", "/" + Regex.Replace(path, @"^[/\s]*(.*)[/\s]*", @"$1").ToLower());
        }

        protected InventoryNode FindFolderInternal(InventoryNode currentNode, string currentPath, string desiredPath)
        {
            if (desiredPath == currentPath || desiredPath == (currentPath + "/"))
            {
                return currentNode;
            }
            return currentNode.Nodes.Values.Select(
                n => FindFolderInternal(n, (currentPath == "/" ? currentPath : currentPath + "/") + n.Data.Name.ToLower(), desiredPath))
                .FirstOrDefault(res => res != null);
        }

        public List<InventoryNode> FindFoldersKeyword(string[] keywords)
        {
            List<InventoryNode> matchingNodes = new List<InventoryNode>();

            var root = RLVRootFolder();
            if (root != null)
            {
                FindFoldersKeywordsInternal(root, keywords, new List<string>(), ref matchingNodes);
            }

            return matchingNodes;
        }

        protected void FindFoldersKeywordsInternal(InventoryNode currentNode, string[] keywords, List<string> currentPathParts, ref List<InventoryNode> matchingNodes)
        {
            if (currentNode.Data is InventoryFolder &&
                !currentNode.Data.Name.StartsWith(".") &&
                !currentNode.Data.Name.StartsWith("~") &&
                keywords.All(currentPathParts.Contains))
            {
                matchingNodes.Add(currentNode);
            }

            foreach (var node in currentNode.Nodes.Values)
            {
                if (!(node.Data is InventoryFolder)) continue;
                currentPathParts.Add(node.Data.Name.ToLower());
                FindFoldersKeywordsInternal(node, keywords, currentPathParts, ref matchingNodes);
                currentPathParts.RemoveAt(currentPathParts.Count - 1);
            }
        }

        public void Clear(UUID id)
        {
            lock (rules)
            {
                rules.RemoveAll(r => r.Sender == id);
            }
        }

        public bool RestictionActive(string behaviour)
        {
            if (!Enabled) return false;

            return rules.FindAll(r => r.Behaviour == behaviour && string.IsNullOrEmpty(r.Option)).Count > 0;
        }

        public bool RestictionActive(string behaviour, string exception)
        {
            if (!Enabled) return false;
            var set = rules.FindAll(r => r.Behaviour == behaviour);

            return set.Count > 0 &&
                   set.FindAll(r => r.Option == exception).Count == 0 &&
                   set.FindAll(r => string.IsNullOrEmpty(r.Option)).Count > 0;
        }

        public List<string> GetOptions(string behaviour)
        {
            List<string> ret = new List<string>();

            foreach (var rule in rules.FindAll(r => r.Behaviour == behaviour && !string.IsNullOrEmpty(r.Option)))
            {
                if (!ret.Contains(rule.Option)) ret.Add(rule.Option);
            }

            return ret;
        }

        public bool AllowDetach(AttachmentInfo a)
        {
            if (!Enabled || a == null) return true;

            return rules.FindAll(r => r.Behaviour == "detach" && r.Sender == a.Prim.ID).Count <= 0;
        }

        public bool AllowDetach(InventoryItem item)
        {
            if (!Enabled || item == null) return true;

            List<Primitive> myAtt = client.Network.CurrentSim.ObjectsPrimitives.FindAll(p => p.ParentID == client.Self.LocalID);
            foreach (var att in myAtt)
            {
                if (CurrentOutfitFolder.GetAttachmentItem(att) != item.UUID) continue;
                if (rules.FindAll(r => r.Behaviour == "detach" && r.Sender == att.ID).Count > 0)
                {
                    return false;
                }
                break;
            }
            return true;
        }

        public bool AutoAcceptTP(UUID agent)
        {
            if (!Enabled || agent == UUID.Zero) return false;

            return rules.FindAll(r => r.Behaviour == "accepttp" && (r.Option == "" || r.Option == agent.ToString())).Count > 0;
        }
    }
}
