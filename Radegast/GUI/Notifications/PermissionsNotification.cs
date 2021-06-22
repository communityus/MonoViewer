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
    public partial class ntfPermissions : Notification
    {
        private UUID taskID;
        private UUID itemID;
        private string objectName;
        private string objectOwner;
        private ScriptPermission questions;
        private RadegastInstance instance;
        private Simulator simulator;


        public ntfPermissions(RadegastInstance instance, Simulator simulator, UUID taskID, UUID itemID, string objectName, string objectOwner, ScriptPermission questions)
            : base(NotificationType.PermissionsRequest)
        {
            InitializeComponent();

            this.instance = instance;
            this.simulator = simulator;
            this.taskID = taskID;
            this.itemID = itemID;
            this.objectName = objectName;
            this.objectOwner = objectOwner;
            this.questions = questions;

            txtMessage.BackColor = instance.MainForm.NotificationBackground;
            txtMessage.Text = "Object " + objectName + " owned by " + objectOwner + " is asking permission to " + questions + ". Do you accept?";

            // Fire off event
            NotificationEventArgs args = new NotificationEventArgs(instance) {Text = txtMessage.Text};
            args.Buttons.Add(btnYes);
            args.Buttons.Add(btnNo);
            args.Buttons.Add(btnMute);
            FireNotificationCallback(args);

            GUI.GuiHelpers.ApplyGuiFixes(this);
        }

        private void btnYes_Click(object sender, EventArgs e)
        {
            instance.Client.Self.ScriptQuestionReply(simulator, itemID, taskID, questions);
            instance.MainForm.RemoveNotification(this);
        }

        private void btnNo_Click(object sender, EventArgs e)
        {
            instance.Client.Self.ScriptQuestionReply(simulator, itemID, taskID, 0);
            instance.MainForm.RemoveNotification(this);
        }

        private void btnMute_Click(object sender, EventArgs e)
        {
            instance.Client.Self.UpdateMuteListEntry(MuteType.Object, taskID, objectName);
            instance.MainForm.RemoveNotification(this);
        }
    }
}
