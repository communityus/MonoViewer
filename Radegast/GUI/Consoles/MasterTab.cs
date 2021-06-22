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
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
#if (COGBOT_LIBOMV || USE_STHREADS)
using ThreadPoolUtil;
using Thread = ThreadPoolUtil.Thread;
using ThreadPool = ThreadPoolUtil.ThreadPool;
using Monitor = ThreadPoolUtil.Monitor;
#endif
using System.Threading;

using Radegast.Netcom;
using OpenMetaverse;

namespace Radegast
{
    public partial class MasterTab : UserControl
    {
        private RadegastInstance instance;
        private GridClient client => instance.Client;
        private RadegastNetcom netcom => instance.Netcom;
        private Avatar avatar;
        public UUID selectedID;
        public Primitive selectedPrim;

        public MasterTab(RadegastInstance instance, Avatar avatar)
        {
            InitializeComponent();
            Disposed += new EventHandler(MasterTab_Disposed);

            if (!instance.advancedDebugging)
            {
                saveBtn.Visible = false;
                texturesBtn.Visible = false;
            }

            this.instance = instance;
            this.avatar = avatar;
            
            // Callbacks
            client.Avatars.ViewerEffectPointAt += new EventHandler<ViewerEffectPointAtEventArgs>(Avatars_ViewerEffectPointAt);
            client.Objects.ObjectProperties += new EventHandler<ObjectPropertiesEventArgs>(Objects_ObjectProperties);

            GUI.GuiHelpers.ApplyGuiFixes(this);
        }

        void MasterTab_Disposed(object sender, EventArgs e)
        {
            client.Avatars.ViewerEffectPointAt -= new EventHandler<ViewerEffectPointAtEventArgs>(Avatars_ViewerEffectPointAt);
            client.Objects.ObjectProperties -= new EventHandler<ObjectPropertiesEventArgs>(Objects_ObjectProperties);
        }

        void Objects_ObjectProperties(object sender, ObjectPropertiesEventArgs e)
        {
            if (selectedPrim != null) {
                if (selectedPrim.ID == e.Properties.ObjectID) {
                    selectedPrim.Properties = e.Properties;
                    UpdateDisplay();
                }
            }
        }

        void UpdateDisplay()
        {
            if (InvokeRequired) {
                Invoke(new MethodInvoker(delegate()
                {
                    UpdateDisplay();
                }));
                return;
            }
            lastPrimName.Text = selectedPrim.Properties.Name;
            lastPrimLocalID.Text = selectedPrim.LocalID.ToString();
        }

        void UpdateLLUUID()
        {
            if (InvokeRequired) {
                Invoke(new MethodInvoker(delegate()
                {
                    UpdateLLUUID();
                }));
                return;
            }
            lastPrimLLUUID.Text = selectedID.ToString();
            lastPrimLocalID.Text = selectedPrim.LocalID.ToString();
            sitBitn.Enabled = true;
            if (selectedPrim.ParentID != 0) {
                objInfoBtn.Enabled = true;
            } else {
                objInfoBtn.Enabled = false;
            }
            touchBtn.Enabled = true;
            if ((selectedPrim.Flags & PrimFlags.Money) != 0)
            {
                payBtn.Enabled = true;
            }
            else
            {
                payBtn.Enabled = false;
            }
            saveBtn.Enabled = true;
            if (selectedPrim.Textures != null) {
                texturesBtn.Enabled = true;
            }
            btnPoint.Enabled = true;
        }

        void Avatars_ViewerEffectPointAt(object sender, ViewerEffectPointAtEventArgs e)
        {
            if (e.SourceID == avatar.ID && e.TargetID != UUID.Zero) {
                selectedID = e.TargetID;
                selectedPrim = client.Network.CurrentSim.ObjectsPrimitives.Find(
                    prim => prim.ID == selectedID
                );
                if (selectedPrim != null) {
                    client.Objects.SelectObject(client.Network.CurrentSim, selectedPrim.LocalID);
                    UpdateLLUUID();
                }
            }
        }

        private void objInfoBtn_Click(object sender, EventArgs e)
        {
            selectedPrim = client.Network.CurrentSim.ObjectsPrimitives.Find(
                prim => prim.LocalID == selectedPrim.ParentID
            );
            selectedID = selectedPrim.ID;
            UpdateLLUUID();
            client.Objects.SelectObject(client.Network.CurrentSim, selectedPrim.LocalID);
        }

        private void sitBitn_Click(object sender, EventArgs e)
        {
            if (selectedPrim != null) {
                instance.State.SetSitting(true, selectedPrim.ID);
            }
        }

        private void standBtn_Click(object sender, EventArgs e)
        {
            instance.State.SetSitting(false, UUID.Zero);
        }

        private void touchBtn_Click(object sender, EventArgs e)
        {
            client.Self.Touch(selectedPrim.LocalID);
        }

        private void payBtn_Click(object sender, EventArgs e)
        {
            (new frmPay(instance, selectedPrim.ID, selectedPrim.Properties.Name, true)).ShowDialog();
        }

        private void texturesBtn_Click(object sender, EventArgs e)
        {
            pnlImages.Controls.Clear();

            List<UUID> textures = new List<UUID> {selectedPrim.Textures.DefaultTexture.TextureID};

            foreach (var te in selectedPrim.Textures.FaceTextures)
            {
                if (te != null && (!textures.Contains(te.TextureID))) {
                    textures.Add(te.TextureID);
                }
            }

            int nTextures = 0;

            foreach (UUID textureID in textures) {
                SLImageHandler img = new SLImageHandler(instance, textureID, "Texture " + (nTextures + 1));
                img.Location = new Point(0, nTextures++ * img.Height);
                img.Dock = DockStyle.Top;
                img.Height = 450;
                pnlImages.Controls.Add(img);
//                nTextures++;
            }

            if (selectedPrim.Sculpt != null && selectedPrim.Sculpt.SculptTexture != UUID.Zero)
            {
                SLImageHandler img = new SLImageHandler(instance, selectedPrim.Sculpt.SculptTexture, "Sculp Texture");
                img.Location = new Point(0, nTextures * img.Height);
                img.Dock = DockStyle.Top;
                img.Height = 450;
                pnlImages.Controls.Add(img);
            }


        }

        private void saveBtn_Click(object sender, EventArgs e)
        {
            if (Form.ActiveForm == null) return;
            WindowWrapper mainWindow = new WindowWrapper(Form.ActiveForm.Handle);
            SaveFileDialog dlg = new SaveFileDialog
            {
                AddExtension = true,
                RestoreDirectory = true,
                Title = "Save object as...",
                Filter = "XML file (*.xml)|*.xml"
            };
            DialogResult res = dlg.ShowDialog();

            if (res == DialogResult.OK)
            {
                Thread t = new Thread(delegate()
                {
                    try
                    {
                        PrimSerializer s = new PrimSerializer(client);
                        string primsXmls = s.GetSerializedPrims(client.Network.CurrentSim, selectedPrim.LocalID);
                        System.IO.File.WriteAllText(dlg.FileName, primsXmls);
                        s.CleanUp();
                        s = null;
                        MessageBox.Show(mainWindow, "Successfully saved " + dlg.FileName, "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception excp)
                    {
                        MessageBox.Show(mainWindow, excp.Message, "Saving failed", MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }) {IsBackground = true};
                t.Start();
            }
        }
        private void loadBtn_Click(object sender, EventArgs e)
        {
            PrimDeserializer.ImportFromFile(client);
        }

        private void btnPoint_Click(object sender, EventArgs e)
        {
            if (instance.State.IsPointing)
            {
                instance.State.UnSetPointing();
                btnPoint.Text = "Point at";
            }
            else
            {
                instance.State.SetPointing(selectedPrim, 3);
                btnPoint.Text = "Unpoint";
            }
        }


    }

    public class WindowWrapper : IWin32Window
    {
        public WindowWrapper(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }

}
