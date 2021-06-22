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
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Rendering;

namespace Radegast.Rendering
{
    public class RenderTerrain : SceneObject
    {
        RadegastInstance Instance;
        GridClient Client => Instance.Client;

        public bool Modified = true;
        float[,] heightTable = new float[256, 256];
        Face terrainFace;
        uint[] terrainIndices;
        ColorVertex[] terrainVertices;
        int terrainTexture = -1;
        bool fetchingTerrainTexture = false;
        Bitmap terrainImage = null;
        int terrainVBO = -1;
        int terrainIndexVBO = -1;
        bool terrainVBOFailed = false;
        bool terrainInProgress = false;
        bool terrainTextureNeedsUpdate = false;
        float terrainTimeSinceUpdate = RenderSettings.MinimumTimeBetweenTerrainUpdated + 1f; // Update terrain om first run
        MeshmerizerR renderer;
        Simulator sim => Instance.Client.Network.CurrentSim;

        public RenderTerrain(RadegastInstance instance)
        {
            Instance = instance;
            renderer = new MeshmerizerR();
        }

        public void ResetTerrain()
        {
            ResetTerrain(true);
        }

        public void ResetTerrain(bool removeImage)
        {
            if (terrainImage != null)
            {
                terrainImage.Dispose();
                terrainImage = null;
            }

            if (terrainVBO != -1)
            {
                Compat.DeleteBuffer(terrainVBO);
                terrainVBO = -1;
            }

            if (terrainIndexVBO != -1)
            {
                Compat.DeleteBuffer(terrainIndexVBO);
                terrainIndexVBO = -1;
            }

            if (removeImage)
            {
                if (terrainTexture != -1)
                {
                    GL.DeleteTexture(terrainTexture);
                    terrainTexture = -1;
                }
            }

            fetchingTerrainTexture = false;
            Modified = true;
        }

        private void UpdateTerrain()
        {
            if (sim == null || sim.Terrain == null) return;

            ThreadPool.QueueUserWorkItem(sync =>
            {
                int step = 1;

                for (int x = 0; x < 256; x += step)
                {
                    for (int y = 0; y < 256; y += step)
                    {
                        float z = 0;
                        int patchNr = ((int)x / 16) * 16 + (int)y / 16;
                        if (sim.Terrain[patchNr] != null
                            && sim.Terrain[patchNr].Data != null)
                        {
                            float[] data = sim.Terrain[patchNr].Data;
                            z = data[(int)x % 16 * 16 + (int)y % 16];
                        }
                        heightTable[x, y] = z;
                    }
                }

                terrainFace = renderer.TerrainMesh(heightTable, 0f, 255f, 0f, 255f);
                terrainVertices = new ColorVertex[terrainFace.Vertices.Count];
                for (int i = 0; i < terrainFace.Vertices.Count; i++)
                {
                    byte[] part = Utils.IntToBytes(i);
                    terrainVertices[i] = new ColorVertex()
                    {
                        Vertex = terrainFace.Vertices[i],
                        Color = new Color4b()
                        {
                            R = part[0],
                            G = part[1],
                            B = part[2],
                            A = 253 // terrain picking
                        }
                    };
                }
                terrainIndices = new uint[terrainFace.Indices.Count];
                for (int i = 0; i < terrainIndices.Length; i++)
                {
                    terrainIndices[i] = terrainFace.Indices[i];
                }
                terrainInProgress = false;
                Modified = false;
                terrainTextureNeedsUpdate = true;
                terrainTimeSinceUpdate = 0f;
            });
        }

        void UpdateTerrainTexture()
        {
            if (!fetchingTerrainTexture)
            {
                fetchingTerrainTexture = true;
                ThreadPool.QueueUserWorkItem(sync =>
                {
                    Simulator sim = Client.Network.CurrentSim;
                    terrainImage = TerrainSplat.Splat(Instance, heightTable,
                        new UUID[] { sim.TerrainDetail0, sim.TerrainDetail1, sim.TerrainDetail2, sim.TerrainDetail3 },
                        new float[] { sim.TerrainStartHeight00, sim.TerrainStartHeight01, sim.TerrainStartHeight10, sim.TerrainStartHeight11 },
                        new float[] { sim.TerrainHeightRange00, sim.TerrainHeightRange01, sim.TerrainHeightRange10, sim.TerrainHeightRange11 });

                    fetchingTerrainTexture = false;
                    terrainTextureNeedsUpdate = false;
                });
            }
        }

        public bool TryGetVertex(int indeex, out ColorVertex picked)
        {
            if (indeex < terrainVertices.Length)
            {
                picked = terrainVertices[indeex];
                return true;
            }
            picked = new ColorVertex();
            return false;
        }

        public override void Render(RenderPass pass, int pickingID, SceneWindow scene, float time)
        {
            terrainTimeSinceUpdate += time;

            if (Modified && terrainTimeSinceUpdate > RenderSettings.MinimumTimeBetweenTerrainUpdated)
            {
                if (!terrainInProgress)
                {
                    terrainInProgress = true;
                    ResetTerrain(false);
                    UpdateTerrain();
                }
            }

            if (terrainTextureNeedsUpdate)
            {
                UpdateTerrainTexture();
            }

            if (terrainIndices == null || terrainVertices == null) return;

            GL.Color3(1f, 1f, 1f);
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.EnableClientState(ArrayCap.NormalArray);
            if (pass == RenderPass.Picking)
            {
                GL.EnableClientState(ArrayCap.ColorArray);
                GL.ShadeModel(ShadingModel.Flat);
            }

            if (terrainImage != null)
            {
                if (terrainTexture != -1)
                {
                    GL.DeleteTexture(terrainTexture);
                }

                terrainTexture = RHelp.GLLoadImage(terrainImage, false);
                terrainImage.Dispose();
                terrainImage = null;
            }

            if (pass != RenderPass.Picking && terrainTexture != -1)
            {
                GL.Enable(EnableCap.Texture2D);
                GL.BindTexture(TextureTarget.Texture2D, terrainTexture);
            }

            if (!RenderSettings.UseVBO || terrainVBOFailed)
            {
                unsafe
                {
                    fixed (float* normalPtr = &terrainVertices[0].Vertex.Normal.X)
                    fixed (float* texPtr = &terrainVertices[0].Vertex.TexCoord.X)
                    fixed (byte* colorPtr = &terrainVertices[0].Color.R)
                    {
                        GL.NormalPointer(NormalPointerType.Float, ColorVertex.Size, (IntPtr)normalPtr);
                        GL.TexCoordPointer(2, TexCoordPointerType.Float, ColorVertex.Size, (IntPtr)texPtr);
                        GL.VertexPointer(3, VertexPointerType.Float, ColorVertex.Size, terrainVertices);
                        if (pass == RenderPass.Picking)
                        {
                            GL.ColorPointer(4, ColorPointerType.UnsignedByte, ColorVertex.Size, (IntPtr)colorPtr);
                        }
                        GL.DrawElements(PrimitiveType.Triangles, terrainIndices.Length, DrawElementsType.UnsignedInt, terrainIndices);
                    }
                }
            }
            else
            {
                if (terrainVBO == -1)
                {
                    Compat.GenBuffers(out terrainVBO);
                    Compat.BindBuffer(BufferTarget.ArrayBuffer, terrainVBO);
                    Compat.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(terrainVertices.Length * ColorVertex.Size), terrainVertices, BufferUsageHint.StaticDraw);
                    if (Compat.BufferSize(BufferTarget.ArrayBuffer) != terrainVertices.Length * ColorVertex.Size)
                    {
                        terrainVBOFailed = true;
                        Compat.BindBuffer(BufferTarget.ArrayBuffer, 0);
                        terrainVBO = -1;
                    }
                }
                else
                {
                    Compat.BindBuffer(BufferTarget.ArrayBuffer, terrainVBO);
                }

                if (terrainIndexVBO == -1)
                {
                    Compat.GenBuffers(out terrainIndexVBO);
                    Compat.BindBuffer(BufferTarget.ElementArrayBuffer, terrainIndexVBO);
                    Compat.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(terrainIndices.Length * sizeof(uint)), terrainIndices, BufferUsageHint.StaticDraw);
                    if (Compat.BufferSize(BufferTarget.ElementArrayBuffer) != terrainIndices.Length * sizeof(uint))
                    {
                        terrainVBOFailed = true;
                        Compat.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
                        terrainIndexVBO = -1;
                    }
                }
                else
                {
                    Compat.BindBuffer(BufferTarget.ElementArrayBuffer, terrainIndexVBO);
                }

                if (!terrainVBOFailed)
                {
                    GL.NormalPointer(NormalPointerType.Float, ColorVertex.Size, (IntPtr)12);
                    GL.TexCoordPointer(2, TexCoordPointerType.Float, ColorVertex.Size, (IntPtr)(24));
                    if (pass == RenderPass.Picking)
                    {
                        GL.ColorPointer(4, ColorPointerType.UnsignedByte, ColorVertex.Size, (IntPtr)32);
                    }
                    GL.VertexPointer(3, VertexPointerType.Float, ColorVertex.Size, (IntPtr)(0));

                    GL.DrawElements(PrimitiveType.Triangles, terrainIndices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);
                }

                Compat.BindBuffer(BufferTarget.ArrayBuffer, 0);
                Compat.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            }

            if (pass == RenderPass.Picking)
            {
                GL.DisableClientState(ArrayCap.ColorArray);
                GL.ShadeModel(ShadingModel.Smooth);
            }
            else
            {
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }
            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.TextureCoordArray);
            GL.DisableClientState(ArrayCap.NormalArray);
        }
    }
}
