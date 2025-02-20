// 
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

namespace Radegast.Netcom
{
    public class StartLocationParser
    {
        private string location;

        public StartLocationParser(string location)
        {
            this.location = location ?? throw new Exception("Location cannot be null.");
        }

        private string GetSim(string location)
        {
            if (!location.Contains("/")) return location;

            string[] locSplit = location.Split('/');
            return locSplit[0];
        }

        private int GetX(string location)
        {
            if (!location.Contains("/")) return 128;

            string[] locSplit = location.Split('/');

            int returnResult;
            bool stringToInt = int.TryParse(locSplit[1], out returnResult);

            if (stringToInt)
                return returnResult;
            else
                return 128;
        }

        private int GetY(string location)
        {
            if (!location.Contains("/")) return 128;

            string[] locSplit = location.Split('/');

            if (locSplit.Length > 2)
            {
                int returnResult;
                bool stringToInt = int.TryParse(locSplit[2], out returnResult);

                if (stringToInt)
                    return returnResult;
            }

            return 128;
        }

        private int GetZ(string location)
        {
            if (!location.Contains("/")) return 0;

            string[] locSplit = location.Split('/');

            if (locSplit.Length > 3)
            {
                int returnResult;
                bool stringToInt = int.TryParse(locSplit[3], out returnResult);

                if (stringToInt)
                    return returnResult;
            }

            return 0;
        }

        public string Sim => GetSim(location);

        public int X => GetX(location);

        public int Y => GetY(location);

        public int Z => GetZ(location);
    }
}
