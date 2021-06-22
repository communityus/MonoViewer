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

using System.Threading;
using OpenMetaverse;

namespace Radegast
{

    public class FolderCopy
    {
        RadegastInstance Instance;
        GridClient Client;

        public FolderCopy(RadegastInstance instance)
        {
            Instance = instance;
            Client = Instance.Client;
        }

        public void GetFolders(string folder)
        {
            var f = FindFolder(folder, Client.Inventory.Store.LibraryRootNode);
            if (f == null) return;

            UUID dest = Client.Inventory.FindFolderForType(AssetType.Clothing);
            if (dest == UUID.Zero) return;

            var destFolder = (InventoryFolder)Client.Inventory.Store[dest];

            ThreadPool.QueueUserWorkItem(sync =>
            {
                Instance.TabConsole.DisplayNotificationInChat("Starting copy operation...");
                foreach (var node in f.Nodes.Values)
                {
                    if (node.Data is InventoryFolder)
                    {
                        var s = (InventoryFolder)node.Data;
                        Instance.TabConsole.DisplayNotificationInChat(string.Format("  Copying {0} to {1}", s.Name, destFolder.Name));
                        CopyFolder(destFolder, s);
                    }
                }
                Instance.TabConsole.DisplayNotificationInChat("Done.");
            });
        }

        public void CopyFolder(InventoryFolder dest, InventoryFolder folder)
        {
            UUID newFolderID = Client.Inventory.CreateFolder(dest.UUID, folder.Name, FolderType.None);
            Thread.Sleep(500);
            var items = Client.Inventory.FolderContents(folder.UUID, folder.OwnerID, true, true, InventorySortOrder.ByDate, 45 * 1000);
            AutoResetEvent copied = new AutoResetEvent(false);
            foreach (var item in items)
            {
                if (item is InventoryItem)
                {
                    copied.Reset();
                    Client.Inventory.RequestCopyItem(item.UUID, newFolderID, item.Name, folder.OwnerID, target =>
                    {
                        Instance.TabConsole.DisplayNotificationInChat(string.Format("    * Copied {0} to {1}", item.Name, dest.Name));
                        copied.Set();
                    });
                    copied.WaitOne(15 * 1000, false);
                }
            }
        }

        public InventoryNode FindFolder(string folder, InventoryNode start)
        {
            if (start.Data.Name == folder)
            {
                return start;
            }

            foreach (var node in start.Nodes.Values)
            {
                if (node.Data is InventoryFolder)
                {
                    var n = FindFolder(folder, node);
                    if (n != null)
                    {
                        return n;
                    }
                }
            }

            return null;
        }
    }
}
