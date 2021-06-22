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
using System.Linq;
using System.Windows.Forms;
using OpenMetaverse;

namespace Radegast
{
    public partial class ntfInventoryOffer : Notification
    {
        private RadegastInstance instance;
        private GridClient client => instance.Client;
        private InstantMessage msg;
        private AssetType type = AssetType.Unknown;
        private UUID objectID = UUID.Zero;
        UUID destinationFolderID;

        public ntfInventoryOffer(RadegastInstance instance, InstantMessage msg)
            : base (NotificationType.InventoryOffer)
        {
            InitializeComponent();
            Disposed += new EventHandler(ntfInventoryOffer_Disposed);

            this.instance = instance;
            this.msg = msg;

            instance.Names.NameUpdated += new EventHandler<UUIDNameReplyEventArgs>(Avatars_UUIDNameReply);

            if (msg.BinaryBucket.Length > 0)
            {
                type = (AssetType)msg.BinaryBucket[0];
                destinationFolderID = client.Inventory.FindFolderForType(type);

                if (msg.BinaryBucket.Length == 17)
                {
                    objectID = new UUID(msg.BinaryBucket, 1);
                }

                if (msg.Dialog == InstantMessageDialog.InventoryOffered)
                {
                    txtInfo.Text = string.Format("{0} has offered you {1} \"{2}\".", msg.FromAgentName, type, msg.Message);
                }
                else if (msg.Dialog == InstantMessageDialog.TaskInventoryOffered)
                {
                    txtInfo.Text = objectOfferText();
                }

                // Fire off event
                NotificationEventArgs args = new NotificationEventArgs(instance) {Text = txtInfo.Text};
                args.Buttons.Add(btnAccept);
                args.Buttons.Add(btnDiscard);
                args.Buttons.Add(btnIgnore);
                FireNotificationCallback(args);
            }
            else
            {
                Logger.Log("Wrong format of the item offered", Helpers.LogLevel.Warning, client);
            }

            GUI.GuiHelpers.ApplyGuiFixes(this);
        }

        void ntfInventoryOffer_Disposed(object sender, EventArgs e)
        {
            instance.Names.NameUpdated -= new EventHandler<UUIDNameReplyEventArgs>(Avatars_UUIDNameReply);
        }

        void Avatars_UUIDNameReply(object sender, UUIDNameReplyEventArgs e)
        {
            if (e.Names.Keys.Contains(msg.FromAgentID))
            {
                instance.Names.NameUpdated -= new EventHandler<UUIDNameReplyEventArgs>(Avatars_UUIDNameReply);
                BeginInvoke(new MethodInvoker(() => txtInfo.Text = objectOfferText()));
            }
        }

        private string objectOfferText()
        {
            return string.Format("Object \"{0}\" owned by {1} has offered you {2}", msg.FromAgentName, instance.Names.Get(msg.FromAgentID), msg.Message); 
        }

        private void SendReply(InstantMessageDialog dialog, byte[] bucket)
        {
            client.Self.InstantMessage(client.Self.Name, msg.FromAgentID, string.Empty, msg.IMSessionID, dialog, InstantMessageOnline.Offline, client.Self.SimPosition, client.Network.CurrentSim.RegionID, bucket);
            
            if (dialog == InstantMessageDialog.InventoryAccepted)
            {
                client.Inventory.RequestFetchInventory(objectID, client.Self.AgentID);
            }
        }

        private void btnAccept_Click(object sender, EventArgs e)
        {
            if (type == AssetType.Unknown) return;

            if (msg.Dialog == InstantMessageDialog.InventoryOffered)
            {
                SendReply(InstantMessageDialog.InventoryAccepted, destinationFolderID.GetBytes());
            }
            else if (msg.Dialog == InstantMessageDialog.TaskInventoryOffered)
            {
                SendReply(InstantMessageDialog.TaskInventoryAccepted, destinationFolderID.GetBytes());
            }
            instance.MainForm.RemoveNotification(this);
        }

        private void btnDiscard_Click(object sender, EventArgs e)
        {
            if (type == AssetType.Unknown) return;

            if (msg.Dialog == InstantMessageDialog.InventoryOffered)
            {
                SendReply(InstantMessageDialog.InventoryDeclined, Utils.EmptyBytes);
                try
                {
                    client.Inventory.Move(
                        client.Inventory.Store.Items[objectID].Data,
                        (InventoryFolder)client.Inventory.Store.Items[client.Inventory.FindFolderForType(FolderType.Trash)].Data);
                }
                catch (Exception) { }
            }
            else if (msg.Dialog == InstantMessageDialog.TaskInventoryOffered)
            {
                SendReply(InstantMessageDialog.TaskInventoryDeclined, Utils.EmptyBytes);
            }
            instance.MainForm.RemoveNotification(this);
        }

        private void btnIgnore_Click(object sender, EventArgs e)
        {
            instance.MainForm.RemoveNotification(this);
        }

        private void txtInfo_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            instance.MainForm.ProcessLink(e.LinkText, true);
        }
    }
}
