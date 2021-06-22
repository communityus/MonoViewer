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
#if (COGBOT_LIBOMV || USE_STHREADS)
using ThreadPoolUtil;
using Thread = ThreadPoolUtil.Thread;
using ThreadPool = ThreadPoolUtil.ThreadPool;
using Monitor = ThreadPoolUtil.Monitor;
#endif
using System.Threading;
using System.Windows.Forms;
using System.IO;
using OpenMetaverse;

namespace Radegast
{
	public partial class ExportConsole : RadegastTabControl
	{
		#region Private Variables
		uint uLocalID;
		List<UUID> Textures = new List<UUID>();
		GridClient Client;
		PrimExporter Exporter;
		#endregion
		
		#region Constructor
		public ExportConsole(GridClient client, uint localID)
		{
			InitializeComponent();
			uLocalID = localID;
			Client = client;
			GatherInfo();
			Exporter = new PrimExporter(client);

			GUI.GuiHelpers.ApplyGuiFixes(this);
		}
		#endregion
		
		#region Private Methods
		void LogMessage(string format, params object[] args)
		{
			if (InvokeRequired)
			{
				if (IsHandleCreated || !instance.MonoRuntime)
					BeginInvoke(new MethodInvoker(() => LogMessage(format, args)));
				return;
			}
			txtLog.AppendText(String.Format(format + "\r\n",args));
			txtLog.SelectionStart = txtLog.TextLength;
			txtLog.ScrollToCaret();
		}
		
		void GatherInfo()
		{
		    uint localid;
			
			var exportPrim = Client.Network.CurrentSim.ObjectsPrimitives.Find(
			    prim => prim.LocalID == uLocalID
			);
			
			if (exportPrim != null)
			{
				if (exportPrim.ParentID != 0)
					localid = exportPrim.ParentID;
				else
					localid = exportPrim.LocalID;
				
				List<Primitive> prims = Client.Network.CurrentSim.ObjectsPrimitives.FindAll(
                    prim => (prim.LocalID == localid || prim.ParentID == localid)
                );
				
				foreach (Primitive prim in prims)
				{
				    if (prim.Textures.DefaultTexture.TextureID != Primitive.TextureEntry.WHITE_TEXTURE &&
				        !Textures.Contains(prim.Textures.DefaultTexture.TextureID))
				    {
				        var texture = new UUID(prim.Textures.DefaultTexture.TextureID);
				        Textures.Add(texture);
						
				        foreach (Primitive.TextureEntryFace face in prim.Textures.FaceTextures)
				        {
				            if (face != null &&
				                face.TextureID != Primitive.TextureEntry.WHITE_TEXTURE &&
				                !Textures.Contains(face.TextureID))
				            {
				                texture = new UUID(face.TextureID);
				                Textures.Add(texture);
				            }
				        }
						
				        if (prim.Sculpt != null && prim.Sculpt.SculptTexture != UUID.Zero && !Textures.Contains(prim.Sculpt.SculptTexture))
				        {
				            texture = new UUID(prim.Sculpt.SculptTexture);
				            Textures.Add(texture);
				        }
				    }
				}
				objectName.Text = exportPrim.Properties.Name;
				objectUUID.Text = exportPrim.ID.ToString();
				primCount.Text = prims.Count.ToString();
				textureCount.Text = Textures.Count.ToString();
			}
		}
		
		void ValidatePath(string fname)
		{
		    string path = Path.GetDirectoryName(fname);
		    btnExport.Enabled = Directory.Exists(path);
		}
		#endregion
		
		#region Event Handlers
		void TxtFileNameTextChanged(object sender, EventArgs e)
		{
			ValidatePath(txtFileName.Text);
		}
		
		void BtnBrowseClick(object sender, EventArgs e)
		{
            SaveFileDialog dlg = new SaveFileDialog {Title = "Export object file", Filter = "XML File (*.xml)|*.xml"};
            DialogResult res = dlg.ShowDialog();
			
			if (res == DialogResult.OK)
			{
				txtFileName.Text = dlg.FileName;
				ValidatePath(dlg.FileName);
			}
		}
		
		void BtnExportClick(object sender, EventArgs e)
		{
			Enabled = false;
			Exporter.LogMessage = LogMessage;

            Thread t = new Thread(delegate()
            {
                try
                {
                    Exporter.ExportToFile(txtFileName.Text, uLocalID);
                    LogMessage("Export Successful.");
                    if (InvokeRequired)
                    {
                        BeginInvoke(new MethodInvoker(() => Enabled = true));
                    }
                }
                catch (Exception ex)
                {
                    LogMessage("Export failed.  Reason: {0}", ex.Message);
                }
            }) {IsBackground = true};

            t.Start();
		}
		#endregion
	}
}
