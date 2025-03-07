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
					delegate(Primitive prim)
					{
						return (prim.LocalID == localid || prim.ParentID == localid);
					}
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
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Title = "Export object file";
			dlg.Filter = "XML File (*.xml)|*.xml";
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
			
			Thread t = new Thread(new ThreadStart(delegate()
			{
				try
				{
					Exporter.ExportToFile(txtFileName.Text,uLocalID);
					LogMessage("Export Successful.");
					if (InvokeRequired)
					{
						BeginInvoke(new MethodInvoker(() => Enabled = true));
					}
				}
				catch (Exception ex)
				{
					LogMessage("Export failed.  Reason: {0}",ex.Message);
				}
			}));
			
			t.IsBackground = true;
			t.Start();
		}
		#endregion
	}
}
