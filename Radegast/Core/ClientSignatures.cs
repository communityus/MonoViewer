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
// $Id: IMTextManager.cs 361 2009-10-24 15:04:57Z latifer $
//

using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Radegast
{
    public class ClientSignatures
    {
        public static Dictionary<UUID, string> Signatures { get; }

        static ClientSignatures()
        {
            Signatures = new Dictionary<UUID, string>();

            try
            {
                OSDMap clients = (OSDMap)OSDParser.DeserializeLLSDXml(Properties.Resources.client_signatures);

                foreach (KeyValuePair<string, OSD> kvp in clients)
                {
                    UUID sig;
                    
                    if (UUID.TryParse(kvp.Key, out sig))
                    {
                        if (kvp.Value.Type == OSDType.Map)
                        {
                            OSDMap client = (OSDMap)kvp.Value;
                            if (client.ContainsKey("name"))
                            {
                                Signatures.Add(sig, client["name"].AsString());
                            }
                        }
                    }
                }
            }
            catch 
            {
                Logger.Log("Failed to parse client signatures.", Helpers.LogLevel.Warning);
            }
        }
    }
}
