/**
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
using OpenMetaverse;

namespace Radegast
{
    public partial class ntfTeleport : Notification
    {
        private RadegastInstance instance;
        private InstantMessage msg;

        public ntfTeleport(RadegastInstance instance, InstantMessage msg)
            : base(NotificationType.Teleport)
        {
            InitializeComponent();
            this.instance = instance;
            this.msg = msg;

            txtHead.BackColor = instance.MainForm.NotificationBackground;
            txtHead.Text = String.Format("{0} has offered to teleport you to their location.", msg.FromAgentName);
            txtMessage.BackColor = instance.MainForm.NotificationBackground;
            txtMessage.Text = msg.Message;
            btnTeleport.Focus();

            // Fire off event
            NotificationEventArgs args = new NotificationEventArgs(instance)
            {
                Text = txtHead.Text + Environment.NewLine + txtMessage.Text
            };
            args.Buttons.Add(btnTeleport);
            args.Buttons.Add(btnCancel);
            FireNotificationCallback(args);

            GUI.GuiHelpers.ApplyGuiFixes(this);
        }

        private void btnTeleport_Click(object sender, EventArgs e)
        {
            instance.Client.Self.TeleportLureRespond(msg.FromAgentID, msg.IMSessionID, true);
            instance.MainForm.RemoveNotification(this);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            instance.Client.Self.TeleportLureRespond(msg.FromAgentID, msg.IMSessionID, false);
            instance.MainForm.RemoveNotification(this);
        }
    }
}
