﻿/**
 * Radegast Metaverse Client
 * Copyright(c) 2009-2014, Radegast Development Team
 * Copyright(c) 2016-2020, Sjofn, LLC
 * All rights reserved.
 *  
 * Radegast is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.If not, see<https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace Radegast
{
    public class CurrentOutfitFolder : IDisposable
    {
        #region Fields
        GridClient Client;
        RadegastInstance Instance;
        bool InitiCOF = false;
        bool AppearanceSent = false;
        bool COFReady = false;
        bool InitialUpdateDone = false;
        public Dictionary<UUID, InventoryItem> Content = new Dictionary<UUID, InventoryItem>();
        public InventoryFolder COF;

        #endregion Fields

        #region Construction and disposal
        public CurrentOutfitFolder(RadegastInstance instance)
        {
            Instance = instance;
            Client = instance.Client;
            Instance.ClientChanged += new EventHandler<ClientChangedEventArgs>(instance_ClientChanged);
            RegisterClientEvents(Client);
        }

        public void Dispose()
        {
            UnregisterClientEvents(Client);
            Instance.ClientChanged -= new EventHandler<ClientChangedEventArgs>(instance_ClientChanged);
        }
        #endregion Construction and disposal

        #region Event handling
        void instance_ClientChanged(object sender, ClientChangedEventArgs e)
        {
            UnregisterClientEvents(Client);
            Client = e.Client;
            RegisterClientEvents(Client);
        }

        void RegisterClientEvents(GridClient client)
        {
            client.Network.EventQueueRunning += new EventHandler<EventQueueRunningEventArgs>(Network_EventQueueRunning);
            client.Inventory.FolderUpdated += new EventHandler<FolderUpdatedEventArgs>(Inventory_FolderUpdated);
            client.Inventory.ItemReceived += new EventHandler<ItemReceivedEventArgs>(Inventory_ItemReceived);
            client.Appearance.AppearanceSet += new EventHandler<AppearanceSetEventArgs>(Appearance_AppearanceSet);
            client.Objects.KillObject += new EventHandler<KillObjectEventArgs>(Objects_KillObject);
        }

        void UnregisterClientEvents(GridClient client)
        {
            client.Network.EventQueueRunning -= new EventHandler<EventQueueRunningEventArgs>(Network_EventQueueRunning);
            client.Inventory.FolderUpdated -= new EventHandler<FolderUpdatedEventArgs>(Inventory_FolderUpdated);
            client.Inventory.ItemReceived -= new EventHandler<ItemReceivedEventArgs>(Inventory_ItemReceived);
            client.Appearance.AppearanceSet -= new EventHandler<AppearanceSetEventArgs>(Appearance_AppearanceSet);
            client.Objects.KillObject -= new EventHandler<KillObjectEventArgs>(Objects_KillObject);
            lock (Content) Content.Clear();
            InitiCOF = false;
            AppearanceSent = false;
            COFReady = false;
            InitialUpdateDone = false;
        }

        void Appearance_AppearanceSet(object sender, AppearanceSetEventArgs e)
        {
            AppearanceSent = true;
            if (COFReady)
            {
                InitialUpdate();
            }
        }

        void Inventory_ItemReceived(object sender, ItemReceivedEventArgs e)
        {
            var links = ContentLinks();
            bool partOfCOF = links.Any(cofItem => cofItem.AssetUUID == e.Item.UUID);

            if (partOfCOF)
            {
                lock (Content)
                {
                    Content[e.Item.UUID] = e.Item;
                }
            }

            if (Content.Count != links.Count) return;
            COFReady = true;
            if (AppearanceSent)
            {
                InitialUpdate();
            }
            lock (Content)
            {
                foreach (InventoryItem link in Content.Values)
                {
                    if (link.InventoryType == InventoryType.Wearable)
                    {
                        InventoryWearable w = (InventoryWearable)link;
                        InventoryItem lk = links.Find(l => l.AssetUUID == w.UUID);
                        // Logger.DebugLog(string.Format("\nName: {0}\nDescription: {1}\nType: {2} - {3}", w.Name, lk == null ? "" : lk.Description, w.Flags.ToString(), w.WearableType.ToString())); ;
                    }
                }

            }
        }

        object FolderSync = new object();

        void Inventory_FolderUpdated(object sender, FolderUpdatedEventArgs e)
        {
            if (COF == null) return;

            if (e.FolderID == COF.UUID && e.Success)
            {
                COF = (InventoryFolder)Client.Inventory.Store[COF.UUID];
                lock (FolderSync)
                {
                    lock (Content) Content.Clear();


                    List<UUID> items = new List<UUID>();
                    List<UUID> owners = new List<UUID>();

                    foreach (var link in ContentLinks())
                    {
                        //if (Client.Inventory.Store.Contains(link.AssetUUID))
                        //{
                        //    continue;
                        //}
                        items.Add(link.AssetUUID);
                        owners.Add(Client.Self.AgentID);
                    }

                    if (items.Count > 0)
                    {
                        Client.Inventory.RequestFetchInventory(items, owners);
                    }
                }
            }
        }

        void Objects_KillObject(object sender, KillObjectEventArgs e)
        {
            if (Client.Network.CurrentSim != e.Simulator) return;

            Primitive prim = null;
            if (Client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(e.ObjectLocalID, out prim))
            {
                UUID invItem = GetAttachmentItem(prim);
                if (invItem != UUID.Zero)
                {
                    RemoveLink(invItem);
                }
            }
        }

        void Network_EventQueueRunning(object sender, EventQueueRunningEventArgs e)
        {
            if (e.Simulator == Client.Network.CurrentSim && !InitiCOF)
            {
                InitiCOF = true;
                InitCOF();
            }
        }
        #endregion Event handling

        #region Private methods
        void RequestDescendants(UUID folderID)
        {
            Client.Inventory.RequestFolderContents(folderID, Client.Self.AgentID, true, true, InventorySortOrder.ByDate);
        }

        void InitCOF()
        {
            var rootContent = Client.Inventory.Store.GetContents(Client.Inventory.Store.RootFolder.UUID);
            foreach (InventoryBase baseItem in rootContent)
            {
                if (baseItem is InventoryFolder 
                    && ((InventoryFolder)baseItem).PreferredType == FolderType.CurrentOutfit)
                {
                    COF = (InventoryFolder)baseItem;
                    break;
                }
            }

            if (COF == null)
            {
                CreateCOF();
            }
            else
            {
                RequestDescendants(COF.UUID);
            }
        }

        void CreateCOF()
        {
            UUID cofID = Client.Inventory.CreateFolder(Client.Inventory.Store.RootFolder.UUID, "Current Outfit", FolderType.CurrentOutfit);
            if (Client.Inventory.Store.Items.ContainsKey(cofID) && Client.Inventory.Store.Items[cofID].Data is InventoryFolder)
            {
                COF = (InventoryFolder)Client.Inventory.Store.Items[cofID].Data;
                COFReady = true;
                if (AppearanceSent)
                {
                    InitialUpdate();
                }
            }
        }

        void InitialUpdate()
        {
            if (InitialUpdateDone) return;
            InitialUpdateDone = true;
            lock (Content)
            {
                var myAtt = Client.Network.CurrentSim.ObjectsPrimitives.FindAll(p => p.ParentID == Client.Self.LocalID);

                foreach (InventoryItem item in Content.Values)
                {
                    if (item is InventoryObject || item is InventoryAttachment)
                    {
                        if (!IsAttached(myAtt, item))
                        {
                            Client.Appearance.Attach(item, AttachmentPoint.Default, false);
                        }
                    }
                }
            }
        }
        #endregion Private methods

        #region Public methods
        /// <summary>
        /// Get COF contents
        /// </summary>
        /// <returns>List if InventoryItems that can be part of appearance (attachments, wearables)</returns>
        public List<InventoryItem> ContentLinks()
        {
            var ret = new List<InventoryItem>();
            if (COF == null) return ret;

            Client.Inventory.Store.GetContents(COF)
                .FindAll(b => CanBeWorn(b) && ((InventoryItem)b).AssetType == AssetType.Link)
                .ForEach(item => ret.Add((InventoryItem)item));

            return ret;
        }

        /// <summary>
        /// Get inventory ID of a prim
        /// </summary>
        /// <param name="prim">Prim to check</param>
        /// <returns>Inventory ID of the object. UUID.Zero if not found</returns>
        public static UUID GetAttachmentItem(Primitive prim)
        {
            if (prim.NameValues == null) return UUID.Zero;

            for (int i = 0; i < prim.NameValues.Length; i++)
            {
                if (prim.NameValues[i].Name == "AttachItemID")
                {
                    return (UUID)prim.NameValues[i].Value.ToString();
                }
            }
            return UUID.Zero;
        }

        /// <summary>
        /// Is an inventory item currently attached
        /// </summary>
        /// <param name="attachments">List of root prims that are attached to our avatar</param>
        /// <param name="item">Inventory item to check</param>
        /// <returns>True if the inventory item is attached to avatar</returns>
        public static bool IsAttached(List<Primitive> attachments, InventoryItem item)
        {
            foreach (var prim in attachments)
            {
                if (GetAttachmentItem(prim) == item.UUID)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if inventory item of Wearable type is worn
        /// </summary>
        /// <param name="currentlyWorn">Current outfit</param>
        /// <param name="item">Item to check</param>
        /// <returns>True if the item is worn</returns>
        public static bool IsWorn(List<AppearanceManager.WearableData> currentlyWorn, InventoryItem item)
        {
            return currentlyWorn.Any(worn => worn.ItemID == item.UUID);
        }

        /// <summary>
        /// Can this inventory type be worn
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if the inventory item can be worn</returns>
        public static bool CanBeWorn(InventoryBase item)
        {
            return item is InventoryWearable || item is InventoryAttachment || item is InventoryObject;
        }

        /// <summary>
        /// Attach an inventory item
        /// </summary>
        /// <param name="item">Item to be attached</param>
        /// <param name="point">Attachment point</param>
        /// <param name="replace">Replace existing attachment at that point first?</param>
        public void Attach(InventoryItem item, AttachmentPoint point, bool replace)
        {
            Client.Appearance.Attach(item, point, replace);
            AddLink(item);
        }

        /// <summary>
        /// Creates a new COF link
        /// </summary>
        /// <param name="item">Original item to be linked from COF</param>
        public void AddLink(InventoryItem item)
        {
            if (item.InventoryType == InventoryType.Wearable && !IsBodyPart(item))
            {
                var w = (InventoryWearable)item;
                int layer = 0;
                string desc = $"@{(int) w.WearableType}{layer:00}";
                AddLink(item, desc);
            }
            else
            {
                AddLink(item, string.Empty);
            }
        }

        /// <summary>
        /// Creates a new COF link
        /// </summary>
        /// <param name="item">Original item to be linked from COF</param>
        /// <param name="newDescription">Description for the link</param>
        public void AddLink(InventoryItem item, string newDescription)
        {
            if (COF == null) return;

            bool linkExists = null != ContentLinks().Find(itemLink => itemLink.AssetUUID == item.UUID);

            if (!linkExists)
            {
                Client.Inventory.CreateLink(COF.UUID, item.UUID, item.Name, newDescription, 
                    AssetType.Link, item.InventoryType, UUID.Random(), 
                    (success, newItem) =>
                {
                    if (success)
                    {
                        Client.Inventory.RequestFetchInventory(newItem.UUID, newItem.OwnerID);
                    }
                });
            }
        }

        /// <summary>
        /// Remove a link to specified inventory item
        /// </summary>
        /// <param name="itemID">ID of the target inventory item for which we want link to be removed</param>
        public void RemoveLink(UUID itemID)
        {
            RemoveLink(new List<UUID>(1) { itemID });
        }

        /// <summary>
        /// Remove a link to specified inventory item
        /// </summary>
        /// <param name="itemIDs">List of IDs of the target inventory item for which we want link to be removed</param>
        public void RemoveLink(List<UUID> itemIDs)
        {
            if (COF == null) return;

            var toRemove = new List<UUID>();

            foreach (var itemID in itemIDs)
            {
                var links = ContentLinks().FindAll(itemLink => itemLink.AssetUUID == itemID);
                links.ForEach(item => toRemove.Add(item.UUID));
            }

            Client.Inventory.Remove(toRemove, null);
        }

        /// <summary>
        /// Remove attachment
        /// </summary>
        /// <param name="item">>Inventory item to be detached</param>
        public void Detach(InventoryItem item)
        {
            var realItem = RealInventoryItem(item);
            if (!Instance.RLV.AllowDetach(realItem)) return;

            Client.Appearance.Detach(item);
            RemoveLink(item.UUID);
        }

        public List<InventoryItem> GetWornAt(WearableType type)
        {
            var ret = new List<InventoryItem>();
            ContentLinks().ForEach(link =>
            {
                var item = RealInventoryItem(link);
                if (!(item is InventoryWearable wearable)) return;

                if (wearable.WearableType == type)
                {
                    ret.Add(wearable);
                }
            });

            return ret;
        }

        /// <summary>
        /// Resolves inventory links and returns a real inventory item that
        /// the link is pointing to
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public InventoryItem RealInventoryItem(InventoryItem item)
        {
            if (item.IsLink() && Client.Inventory.Store.Contains(item.AssetUUID) && Client.Inventory.Store[item.AssetUUID] is InventoryItem)
            {
                return (InventoryItem)Client.Inventory.Store[item.AssetUUID];
            }

            return item;
        }

        /// <summary>
        /// Replaces the current outfit and updates COF links accordingly
        /// </summary>
        /// <param name="newOutfit">List of new wearables and attachments that comprise the new outfit</param>
        public void ReplaceOutfit(List<InventoryItem> newOutfit)
        {
            // Resolve inventory links
            var outfit = new List<InventoryItem>();
            foreach (var item in newOutfit)
            {
                outfit.Add(RealInventoryItem(item));
            }

            // Remove links to all exiting items
            var toRemove = new List<UUID>();
            ContentLinks().ForEach(item =>
            {
                if (IsBodyPart(item))
                {
                    WearableType linkType = ((InventoryWearable)RealInventoryItem(item)).WearableType;
                    bool hasBodyPart = newOutfit.Select(RealInventoryItem).Where(IsBodyPart).Any(newItem =>
                        ((InventoryWearable) newItem).WearableType == linkType);

                    if (hasBodyPart)
                    {
                        toRemove.Add(item.UUID);
                    }
                }
                else
                {
                    toRemove.Add(item.UUID);
                }
            });

            Client.Inventory.Remove(toRemove, null);

            // Add links to new items
            var newItems = outfit.FindAll(CanBeWorn);
            foreach (var item in newItems)
            {
                AddLink(item);
            }

            Client.Appearance.ReplaceOutfit(outfit, false);
            ThreadPool.QueueUserWorkItem(sync =>
            {
                Thread.Sleep(2000);
                Client.Appearance.RequestSetAppearance(true);
            });
        }

        /// <summary>
        /// Add items to current outfit
        /// </summary>
        /// <param name="item">Item to add</param>
        /// <param name="replace">Should existing wearable of the same type be removed</param>
        public void AddToOutfit(InventoryItem item, bool replace)
        {
            AddToOutfit(new List<InventoryItem>(1) { item }, replace);
        }

        /// <summary>
        /// Add items to current outfit
        /// </summary>
        /// <param name="items">List of items to add</param>
        /// <param name="replace">Should existing wearable of the same type be removed</param>
        public void AddToOutfit(List<InventoryItem> items, bool replace)
        {
            var current = ContentLinks();
            var toRemove = new List<UUID>();

            // Resolve inventory links and remove wearables of the same type from COF
            var outfit = new List<InventoryItem>();

            foreach (var item in items)
            {
                var realItem = RealInventoryItem(item);
                if (replace && realItem is InventoryWearable wearable)
                {
                    foreach (var link in current)
                    {
                        var currentItem = RealInventoryItem(link);
                        if (link.AssetUUID == item.UUID)
                        {
                            toRemove.Add(link.UUID);
                        }
                        else
                        {
                            var w = currentItem as InventoryWearable;
                            if (w?.WearableType == wearable.WearableType)
                            {
                                toRemove.Add(link.UUID);
                            }
                        }
                    }
                }

                outfit.Add(realItem);
            }
            Client.Inventory.Remove(toRemove, null);

            // Add links to new items
            var newItems = outfit.FindAll(CanBeWorn);
            foreach (var item in newItems)
            {
                AddLink(item);
            }

            Client.Appearance.AddToOutfit(outfit, replace);
            ThreadPool.QueueUserWorkItem(sync =>
            {
                Thread.Sleep(2000);
                Client.Appearance.RequestSetAppearance(true);
            });
        }

        /// <summary>
        /// Remove an item from the current outfit
        /// </summary>
        /// <param name="item">Item to remove</param>
        public void RemoveFromOutfit(InventoryItem item)
        {
            RemoveFromOutfit(new List<InventoryItem>(1) { item });
        }

        /// <summary>
        /// Remove specified items from the current outfit
        /// </summary>
        /// <param name="items">List of items to remove</param>
        public void RemoveFromOutfit(List<InventoryItem> items)
        {
            // Resolve inventory links
            var outfit = items.Select(RealInventoryItem).Where(realItem => Instance.RLV.AllowDetach(realItem)).ToList();

            // Remove links to all items that were removed
            var toRemove = outfit.FindAll(item => CanBeWorn(item) && !IsBodyPart(item)).Select(item => item.UUID).ToList();
            RemoveLink(toRemove);

            Client.Appearance.RemoveFromOutfit(outfit);
        }

        public bool IsBodyPart(InventoryItem item)
        {
            var realItem = RealInventoryItem(item);
            if (!(realItem is InventoryWearable wearable)) return false;

            var t = wearable.WearableType;
            return t == WearableType.Shape ||
                   t == WearableType.Skin ||
                   t == WearableType.Eyes ||
                   t == WearableType.Hair;
        }

        /// <summary>
        /// Force rebaking textures
        /// </summary>
        public void RebakeTextures()
        {
            Client.Appearance.RequestSetAppearance(true);
        }

        #endregion Public methods
    }
}
