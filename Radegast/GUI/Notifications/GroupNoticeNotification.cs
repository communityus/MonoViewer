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

using System.Windows.Forms;
using OpenMetaverse;

namespace Radegast
{
    public partial class ntfGroupNotice : Notification
    {
        private RadegastInstance instance;
        private GridClient client => instance.Client;
        private InstantMessage msg;
        private AssetType type = AssetType.Unknown;
        private UUID destinationFolderID;
        private UUID groupID;
        private Group group;

        public ntfGroupNotice(RadegastInstance instance, InstantMessage msg)
            : base(NotificationType.GroupNotice)
        {
            InitializeComponent();
            Disposed += new System.EventHandler(ntfGroupNotice_Disposed);

            this.instance = instance;
            this.msg = msg;
            client.Groups.GroupProfile += new System.EventHandler<GroupProfileEventArgs>(Groups_GroupProfile);

            if (msg.BinaryBucket.Length > 18 && msg.BinaryBucket[0] != 0)
            {
                type = (AssetType)msg.BinaryBucket[1];
                destinationFolderID = client.Inventory.FindFolderForType(type);
                int icoIndx = InventoryConsole.GetItemImageIndex(type.ToString().ToLower());
                if (icoIndx >= 0)
                {
                    icnItem.Image = frmMain.ResourceImages.Images[icoIndx];
                    icnItem.Visible = true;
                }
                txtItemName.Text = Utils.BytesToString(msg.BinaryBucket, 18, msg.BinaryBucket.Length - 19);
                btnSave.Enabled = true;
                btnSave.Visible = icnItem.Visible = txtItemName.Visible = true;
            }


            if (msg.BinaryBucket.Length >= 18)
            {
                groupID = new UUID(msg.BinaryBucket, 2);
            }
            else
            {
                groupID = msg.FromAgentID;
            }

            int pos = msg.Message.IndexOf('|');
            string title = msg.Message.Substring(0, pos);
            lblTitle.Text = title;
            string text = msg.Message.Replace("\n", System.Environment.NewLine);
            text = text.Remove(0, pos + 1);

            lblSentBy.Text = string.Format("Sent by {0}", msg.FromAgentName);
            txtNotice.Text = text;

            if (instance.Groups.ContainsKey(groupID))
            {
                group = instance.Groups[groupID];
                ShowNotice();
            }
            else
            {
                client.Groups.RequestGroupProfile(groupID);
            }

            GUI.GuiHelpers.ApplyGuiFixes(this);
        }

        private void ShowNotice()
        {
            if (group.ID == UUID.Zero) return;
            
            imgGroup.Init(instance, group.InsigniaID, string.Empty);
            lblSentBy.Text += ", " + group.Name;

            // Fire off event
            NotificationEventArgs args = new NotificationEventArgs(instance)
            {
                Text = string.Format("{0}{1}{2}{3}{4}",
                    lblTitle.Text, System.Environment.NewLine,
                    lblSentBy.Text, System.Environment.NewLine,
                    txtNotice.Text
                )
            };
            if (btnSave.Visible)
            {
                args.Buttons.Add(btnSave);
                args.Text += $"{System.Environment.NewLine}Attachment: {txtItemName.Text}";
            }
            args.Buttons.Add(btnOK);
            FireNotificationCallback(args);
        }

        void ntfGroupNotice_Disposed(object sender, System.EventArgs e)
        {
            client.Groups.GroupProfile -= new System.EventHandler<GroupProfileEventArgs>(Groups_GroupProfile);
        }

        void Groups_GroupProfile(object sender, GroupProfileEventArgs e)
        {
            if (groupID != e.Group.ID) return;

            if (instance.MainForm.InvokeRequired)
            {
                instance.MainForm.BeginInvoke(new MethodInvoker(() => Groups_GroupProfile(sender, e)));
                return;
            }

            group = e.Group;
            ShowNotice();
        }

        private void SendReply(InstantMessageDialog dialog, byte[] bucket)
        {
            client.Self.InstantMessage(client.Self.Name, msg.FromAgentID, string.Empty, msg.IMSessionID, dialog, InstantMessageOnline.Offline, client.Self.SimPosition, client.Network.CurrentSim.RegionID, bucket);
        }

        private void btnOK_Click(object sender, System.EventArgs e)
        {
            instance.MainForm.RemoveNotification(this);
        }

        private void btnSave_Click(object sender, System.EventArgs e)
        {
            SendReply(InstantMessageDialog.GroupNoticeInventoryAccepted, destinationFolderID.GetBytes());
            btnSave.Enabled = false;
            btnOK.Focus();
        }
    }
}
