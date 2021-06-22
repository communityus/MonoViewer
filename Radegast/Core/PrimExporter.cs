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
using System.IO;
using System.Linq;
#if (COGBOT_LIBOMV || USE_STHREADS)
using ThreadPoolUtil;
using Thread = ThreadPoolUtil.Thread;
using ThreadPool = ThreadPoolUtil.ThreadPool;
using Monitor = ThreadPoolUtil.Monitor;
#endif
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Assets;

namespace Radegast
{
	/// <summary>
	/// Description of PrimExporter.
	/// </summary>
	public class PrimExporter
	{
		List<UUID> Textures = new List<UUID>();
		AutoResetEvent GotPermissionsEvent = new AutoResetEvent(false);
		Primitive.ObjectProperties Properties;
		bool GotPermissions = false;
		UUID SelectedObject = UUID.Zero;
		
		Dictionary<UUID, Primitive> PrimsWaiting = new Dictionary<UUID, Primitive>();
		AutoResetEvent AllPropertiesReceived = new AutoResetEvent(false);
		GridClient Client;
		string ExportDirectory;
		private uint uLocalID;
		public delegate void LogMessageDelegate(string format, params object[] args);
		public LogMessageDelegate LogMessage;
		
		public PrimExporter(GridClient client)
		{
			Client = client;
			Client.Objects.ObjectPropertiesFamily += new EventHandler<ObjectPropertiesFamilyEventArgs>(Objects_OnObjectPropertiesFamily);
			Client.Objects.ObjectProperties += new EventHandler<ObjectPropertiesEventArgs>(Objects_OnObjectProperties);
		}
		
		public void CleanUp()
		{
			Client.Objects.ObjectPropertiesFamily -= new EventHandler<ObjectPropertiesFamilyEventArgs>(Objects_OnObjectPropertiesFamily);
			Client.Objects.ObjectProperties += new EventHandler<ObjectPropertiesEventArgs>(Objects_OnObjectProperties);
		}
		
		public void ExportToFile(string filename, uint localID)
		{
		    uint localid;
			
			ExportDirectory = Path.Combine(Path.GetDirectoryName(filename),Path.GetFileNameWithoutExtension(filename));
			Directory.CreateDirectory(ExportDirectory);
			
			var exportPrim = Client.Network.CurrentSim.ObjectsPrimitives.Find(
			    prim => prim.LocalID == localID
			);
			
			if (exportPrim != null)
			{
				localid = exportPrim.ParentID != 0 ? exportPrim.ParentID : exportPrim.LocalID;
				
				uLocalID = localid;
				// Check for export permission first
				Client.Objects.RequestObjectPropertiesFamily(Client.Network.CurrentSim, exportPrim.ID);
				GotPermissionsEvent.WaitOne(1000 * 10, false);
				
				if (!GotPermissions)
				{
					throw new Exception("Couldn't fetch permissions for the requested object, try again");
				}
				else
				{
					GotPermissions = false;

					// Must be Owner and Creator of the item to export, per Linden Lab's TOS
					if (!(Properties.CreatorID == Client.Self.AgentID &&
					     Properties.OwnerID == Client.Self.AgentID))
					{
						string msg = "That object is owned by {0}, Created by {1} we don't have permission to export it. Your UUID: {2}";
						throw new Exception(String.Format(msg,Properties.OwnerID,Properties.CreatorID,Client.Self.AgentID));
					}
					
					List<Primitive> prims = Client.Network.CurrentSim.ObjectsPrimitives.FindAll(
                        prim => (prim.LocalID == localid || prim.ParentID == localid)
                    );
					
					bool complete = RequestObjectProperties(prims, 250);
					
					string output = OSDParser.SerializeLLSDXmlString(Helpers.PrimListToOSD(prims));
					File.WriteAllText(filename,output);
					
					List<ImageRequest> textureRequests = new List<ImageRequest>();
					
					lock(Textures)
					{
						foreach (var prim in prims)
                        {
                            UUID texture;
							
                            if (prim.Textures.DefaultTexture.TextureID != Primitive.TextureEntry.WHITE_TEXTURE &&
                                !Textures.Contains(prim.Textures.DefaultTexture.TextureID))
                            {
                                texture = new UUID(prim.Textures.DefaultTexture.TextureID);
                                Textures.Add(texture);
                            }
							
                            foreach (var tex in prim.Textures.FaceTextures)
                            {
                                if (tex != null &&
                                    tex.TextureID != Primitive.TextureEntry.WHITE_TEXTURE &&
                                    !Textures.Contains(tex.TextureID))
                                {
                                    texture = new UUID(tex.TextureID);
                                    Textures.Add(texture);
                                }
                            }
							
                            if (prim.Sculpt != null && prim.Sculpt.SculptTexture != UUID.Zero && !Textures.Contains(prim.Sculpt.SculptTexture))
                            {
                                texture = new UUID(prim.Sculpt.SculptTexture);
                                Textures.Add(texture);
                            }
                        }
						
						FindImagesInInventory();

                        textureRequests.AddRange(Textures.Select(t => new ImageRequest(t, ImageType.Normal, 1013000.0f, 0)));

                        foreach (ImageRequest request in textureRequests)
						{
							Client.Assets.RequestImage(request.ImageID, request.Type, Assets_OnImageReceived);
						}
					}
				}
			}
			else
			{
				throw new Exception("Couldn't find id " + localID + " in the " +
				                    Client.Network.CurrentSim.ObjectsPrimitives.Count + 
				                    " objects currently indexed in the current simulator.");
			}
		}
		
		private List<InventoryNode> TraverseDir(InventoryNode node)
		{
			List<InventoryNode> nodes = new List<InventoryNode>(node.Nodes.Values);
			List<InventoryNode> textures = new List<InventoryNode>();
			foreach(InventoryNode n in nodes)
			{
				if (n.Data is InventoryFolder)
				{
					List<InventoryNode> nn = TraverseDir(n);
					foreach(InventoryNode i in nn)
						textures.Add(i);
				}
				else
				{
					InventoryItem item = (InventoryItem)n.Data;
					if (item.InventoryType == InventoryType.Texture)
						textures.Add(n);
				}
			}
			return textures;
		}
		
		private void FindImagesInInventory()
		{
			List<InventoryNode> nodes = TraverseDir(Client.Inventory.Store.RootNode);
			List<UUID> oldTextures = new List<UUID>(Textures);
			Textures = new List<UUID>();
			
			foreach (InventoryNode n in nodes)
			{
				InventoryItem texture = (InventoryItem)n.Data;
				if (oldTextures.Contains(texture.AssetUUID))
				{
					LogMessage("Found Texture {0}: {1}",texture.AssetUUID.ToString(),texture.Name);
					PermissionMask fullPerm = PermissionMask.Modify | PermissionMask.Copy | PermissionMask.Transfer;
					if ((texture.Permissions.OwnerMask & fullPerm) == fullPerm)
					{
						Textures.Add(texture.AssetUUID);
						LogMessage("Texture {0} will be exported",texture.Name);
						oldTextures.Remove(texture.AssetUUID);
					}
					else
						LogMessage("Texture {0} is not full perm, will not export.",texture.Name);
				}
			}
			foreach (UUID texture in oldTextures)
				LogMessage("Failed to find {0}, will not export",texture.ToString());
		}
		
		private bool RequestObjectProperties(List<Primitive> objects, int msPerRequest)
		{
			uint[] localids = new uint[objects.Count];
			
			lock (PrimsWaiting)
			{
				PrimsWaiting.Clear();
				
				for (int i = 0; i < objects.Count; ++i)
				{
					localids[i] = objects[i].LocalID;
					PrimsWaiting.Add(objects[i].ID,objects[i]);
				}
			}
			
			Client.Objects.SelectObjects(Client.Network.CurrentSim, localids);
			
			return AllPropertiesReceived.WaitOne(2000 + msPerRequest * objects.Count, false);
		}
		
		void Objects_OnObjectPropertiesFamily(object sender, ObjectPropertiesFamilyEventArgs e)
		{
			Properties = new Primitive.ObjectProperties();
			Properties.SetFamilyProperties(e.Properties);
			if (e.Properties.CreatorID == UUID.Zero)
			{
				Client.Objects.SelectObject(Client.Network.CurrentSim,uLocalID);
			}
			else
			{
				GotPermissions = true;
				GotPermissionsEvent.Set();
			}
		}
		
		void Objects_OnObjectProperties(object sender, ObjectPropertiesEventArgs e)
		{
			if (e.Properties.ObjectID == Properties.ObjectID)
			{
				if (e.Properties.CreatorID != UUID.Zero)
				{
					Properties.CreatorID = e.Properties.CreatorID;
					Properties.Permissions = e.Properties.Permissions;
					GotPermissions = true;
					GotPermissionsEvent.Set();
				}
			}
			lock (PrimsWaiting)
			{
				PrimsWaiting.Remove(e.Properties.ObjectID);
				
				if (PrimsWaiting.Count == 0)
					AllPropertiesReceived.Set();
			}
		}
		
		void Assets_OnImageReceived(TextureRequestState state, AssetTexture asset)
		{
			if (state == TextureRequestState.Finished && Textures.Contains(asset.AssetID))
			{
				lock (Textures)
					Textures.Remove(asset.AssetID);
				
				if (state == TextureRequestState.Finished)
				{
					try
					{
						File.WriteAllBytes(Path.Combine(ExportDirectory,asset.AssetID + ".jp2"), asset.AssetData);
						LogMessage("Successfully downloaded texture {0}",asset.AssetID.ToString());
					}
					catch (Exception ex)
					{
						LogMessage("Failed to download texture {0}\r\nReason: {1}",asset.AssetID.ToString(),ex.Message);
					}
				}
				else
				{
					LogMessage("Failed to download texture {0}\r\nReason: {1}",asset.AssetID.ToString(),state);
				}
				lock (Textures)
				{
					if (Textures.Count == 0)
						LogMessage("Texture Download complete!");
				}
			}
		}
	}
}
