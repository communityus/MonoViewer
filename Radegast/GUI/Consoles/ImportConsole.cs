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
using OpenMetaverse.StructuredData;

namespace Radegast
{
	public partial class ImportConsole : RadegastTabControl
	{
		#region Private Varaibles
		PrimImporter Importer;
		GridClient Client;
		List<UUID> FailedUploads = new List<UUID>();
		DateTime start;
		string sFileName;
		#endregion
		
		#region Constructors
		public ImportConsole(GridClient client)
		{
			InitializeComponent();
			Client = client;
            Importer = new PrimImporter(client) {LogMessage = LogMessage};
            sFileName = "";
			objectName.Text = "";
			primCount.Text = "";
			textureCount.Text = "";

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
		
		void ValidateFileName()
		{
			string fileName = txtFileName.Text;
			if (File.Exists(fileName))
			{
				txtLog.Clear();
				LogMessage("Loading {0}...",fileName);
				string xml = File.ReadAllText(fileName);
				List<Primitive> prims = Helpers.OSDToPrimList(OSDParser.DeserializeLLSDXml(xml));
				int count = prims.Count;
				string name = "";
				string desc = "";
				Importer.Textures = new Dictionary<UUID, UUID>();
				Importer.SculptTextures = new Dictionary<UUID, UUID>();
				LogMessage("Parsing Object Data...");
				foreach(Primitive prim in prims)
				{
					if (prim.ParentID == 0)
					{
						name = prim.Properties.Name;
						desc = prim.Properties.Description;
					}
					
					if (prim.Textures.DefaultTexture.TextureID != Primitive.TextureEntry.WHITE_TEXTURE &&
					    !Importer.Textures.ContainsKey(prim.Textures.DefaultTexture.TextureID))
						Importer.Textures.Add(prim.Textures.DefaultTexture.TextureID,UUID.Zero);
					
					foreach (var tex in prim.Textures.FaceTextures)
                    {
                        if (tex != null &&
                            tex.TextureID != Primitive.TextureEntry.WHITE_TEXTURE &&
                            !Importer.Textures.ContainsKey(tex.TextureID))
                            Importer.Textures.Add(tex.TextureID,UUID.Zero);
                    }
					
					if (prim.Sculpt != null && prim.Sculpt.SculptTexture != UUID.Zero && !Importer.Textures.ContainsKey(prim.Sculpt.SculptTexture))
						Importer.Textures.Add(prim.Sculpt.SculptTexture,UUID.Zero);
					if (prim.Sculpt != null && prim.Sculpt.SculptTexture != UUID.Zero && !Importer.SculptTextures.ContainsKey(prim.Sculpt.SculptTexture))
						Importer.SculptTextures.Add(prim.Sculpt.SculptTexture,UUID.Zero);
					
				}
				objectName.Text = name;
				primCount.Text = prims.Count.ToString();
				textureCount.Text = Importer.Textures.Count.ToString();
				LogMessage("Reading complete, Ready to import...");
			}
		}
		
		UUID FindOrMakeInventoryFolder(string name)
		{
			List<InventoryBase> folders = Client.Inventory.FolderContents(Client.Inventory.FindFolderForType(AssetType.Texture),Client.Self.AgentID,true,false,InventorySortOrder.ByName,15000);
			UUID dir = UUID.Zero;
			foreach(InventoryBase item in folders)
			{
				if (item.Name == name)
					dir = item.UUID;
			}
			
			if (dir == UUID.Zero)
				dir = Client.Inventory.CreateFolder(Client.Inventory.FindFolderForType(AssetType.Texture),name);
			return dir;
		}
		
		void UploadImages()
		{
			ImageUploader upldr = new ImageUploader(Client);
			string path = Path.Combine(Path.GetDirectoryName(txtFileName.Text),Path.GetFileNameWithoutExtension(txtFileName.Text));
			
			UUID uploaddir = FindOrMakeInventoryFolder("Import_" + Path.GetFileNameWithoutExtension(txtFileName.Text));
            FailedUploads.Clear();
			LogMessage("Begining Uploading of Textures...");
			
			List<UUID> textures = new List<UUID>();
			if (Importer.TextureUse == PrimImporter.TextureSet.NewUUID)
				textures = Importer.Textures.Keys.ToList<UUID>();
			else if (Importer.TextureUse == PrimImporter.TextureSet.SculptUUID)
				textures = Importer.SculptTextures.Keys.ToList<UUID>();
			
			foreach (UUID texture in textures)
			{
				if (texture == UUID.Zero)
					continue;
				string file = Path.Combine(path,texture + ".jp2");
				if (!File.Exists(file))
				{
					LogMessage("Failed to find texture {0}",texture.ToString());
					continue;
				}
				LogMessage("Uploading texture {0}...",texture.ToString());
				bool ret = upldr.UploadImage(file,"Import of " + Path.GetFileNameWithoutExtension(txtFileName.Text),uploaddir);
				if (ret)
				{
					LogMessage("Uploaded texture {0} with new UUID: {1}\r\nUpload took {2} seconds",
					           texture.ToString(), upldr.TextureID.ToString(),upldr.Duration);
					if (Importer.TextureUse == PrimImporter.TextureSet.NewUUID)
						Importer.Textures[texture] = upldr.TextureID;
					else if (Importer.TextureUse == PrimImporter.TextureSet.SculptUUID)
						Importer.SculptTextures[texture] = upldr.TextureID;
				}
				else
				{
					LogMessage("Upload of texture {0} failed, reason: {1}",texture.ToString(), upldr.Status);
					FailedUploads.Add(texture);
				}
			}
		}
		
		void UploadImagesRetry()
		{
			ImageUploader upldr = new ImageUploader(Client);
			string path = Path.Combine(Path.GetDirectoryName(txtFileName.Text),Path.GetFileNameWithoutExtension(txtFileName.Text));
			
			UUID uploaddir = FindOrMakeInventoryFolder("Import_" + Path.GetFileNameWithoutExtension(txtFileName.Text));
			FailedUploads = new List<UUID>();
			LogMessage("Retrying Uploading of failed Textures...");
			
			List<UUID> textures = new List<UUID>(FailedUploads);
			FailedUploads = new List<UUID>();
			
			foreach(UUID texture in textures)
			{
				string file = Path.Combine(path,texture + ".jp2");
				LogMessage("Uploading texture {0}...",texture.ToString());
				bool ret = upldr.UploadImage(file,"Import of " + Path.GetFileNameWithoutExtension(txtFileName.Text),uploaddir);
				if (ret)
				{
					LogMessage("Uploaded texture {0} with new UUID: {1}\r\nUpload took {2} seconds",
					           texture.ToString(), upldr.TextureID.ToString(), upldr.Duration);
					if (Importer.TextureUse == PrimImporter.TextureSet.NewUUID)
						Importer.Textures[texture] = upldr.TextureID;
					else if (Importer.TextureUse == PrimImporter.TextureSet.SculptUUID)
						Importer.SculptTextures[texture] = upldr.TextureID;
				}
				else
				{
					LogMessage("Upload of texture {0} failed, reason: {1}",texture.ToString(),upldr.Status);
					FailedUploads.Add(texture);
				}
			}
		}
		
		void EnableWindow()
		{
			Enabled = true;
		}
		#endregion
		
		#region Event Handlers
		void ChckRezAtLocCheckedChanged(object sender, EventArgs e)
		{
			txtX.Enabled = chckRezAtLoc.Checked;
			txtY.Enabled = chckRezAtLoc.Checked;
			txtZ.Enabled = chckRezAtLoc.Checked;
		}
		
		void BtnBrowseClick(object sender, EventArgs e)
		{
		    if (Form.ActiveForm != null)
		    {
		        WindowWrapper mainWindow = new WindowWrapper(Form.ActiveForm.Handle);
		    }
		    OpenFileDialog dlg = new OpenFileDialog
		    {
		        Title = "Open object file",
		        Filter = "XML file (*.xml)|*.xml",
		        Multiselect = false
		    };
		    DialogResult res = dlg.ShowDialog();
			
			if (res != DialogResult.OK)
				return;
			txtFileName.Text = dlg.FileName;
			ValidateFileName();
		}
		
		void TxtFileNameLeave(object sender, EventArgs e)
		{
			if (sFileName != txtFileName.Text)
			{
				sFileName = txtFileName.Text;
				ValidateFileName();
			}
		}
		
		void BtnUploadClick(object sender, EventArgs e)
		{
			Enabled = false;
			if (cmbImageOptions.SelectedIndex == -1)
			{
				MessageBox.Show("You must select an Image Option before you can import an object.","Import Object Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
				Enabled = true;
				return;
			}
			switch(cmbImageOptions.SelectedIndex)
			{
				case 0:
					Importer.TextureUse = PrimImporter.TextureSet.OriginalUUID;
					break;
				case 1:
					Importer.TextureUse = PrimImporter.TextureSet.NewUUID;
					break;
				case 2:
					Importer.TextureUse = PrimImporter.TextureSet.SculptUUID;
					break;
				case 3:
					Importer.TextureUse = PrimImporter.TextureSet.WhiteUUID;
					break;
			}
			if (chckRezAtLoc.Checked)
			{
				float x = 0.0f;
				float y = 0.0f;
				float z = 0.0f;
				if (!float.TryParse(txtX.Text,out x))
				{
					MessageBox.Show("X Coordinate needs to be a Float position!  Example: 1.500","Import Object Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
					Enabled = true;
					return;
				}
				if (!float.TryParse(txtY.Text,out y))
				{
					MessageBox.Show("Y Coordinate needs to be a Float position!  Example: 1.500","Import Object Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
					Enabled = true;
					return;
				}
				if (!float.TryParse(txtZ.Text,out z))
				{
					MessageBox.Show("Z Coordinate needs to be a Float position!  Example: 1.500","Import Object Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
					Enabled = true;
					return;
				}
				Importer.RezAt = new Vector3(x,y,z);
			}
			else
			{
				Importer.RezAt = Client.Self.SimPosition;
				Importer.RezAt.Z += 3.5f;
			}

            Thread t = new Thread(delegate()
            {
                try
                {
                    start = DateTime.Now;
                    // First upload Images that will be needed by the Importer, if required by user.
                    if (Importer.TextureUse == PrimImporter.TextureSet.NewUUID ||
                        Importer.TextureUse == PrimImporter.TextureSet.SculptUUID)
                        UploadImages();

                    // Check to see if there are any failed uploads.
                    if (FailedUploads.Count > 0)
                    {
                        DialogResult res = MessageBox.Show(
                            $"Failed to upload {FailedUploads.Count} textures, which to try again?",
                            "Import - Upload Texture Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                        if (res == DialogResult.Yes)
                            UploadImagesRetry();

                        if (FailedUploads.Count != 0)
                        {
                            MessageBox.Show(
                                $"Failed to upload {FailedUploads.Count} textures on second try, aborting!",
                                "Import - Upload Texture Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            LogMessage(
                                "Failed to import object, due to texture error, review the log for further information");
                            return;
                        }
                    }

                    LogMessage("Texture Upload completed");
                    LogMessage("Importing Prims...");
                    // If we get here, then we successfully uploaded the textures, continue with the upload of the Prims.
                    Importer.ImportFromFile(txtFileName.Text);
                    LogMessage("Import successful.");
                    LogMessage("Total Time: {0}", DateTime.Now.Subtract(start));
                }
                catch (Exception ex)
                {
                    LogMessage("Import failed. Reason: {0}", ex.Message);
                    MessageBox.Show(ex.Message, "Importing failed.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                BeginInvoke(new MethodInvoker(() => EnableWindow()));
            }) {IsBackground = true};
            t.Start();
		}
		#endregion
	}
}
