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

using System.Windows.Forms;
using OpenMetaverse;

namespace Radegast
{
    public class RadegastTabControl : UserControl
    {
        public RadegastTab RadegastTab { get; set; }

        protected RadegastInstance instance;
        protected GridClient client => instance.Client;

        public RadegastTabControl() { }

        public RadegastTabControl(RadegastInstance instance)
        {
            this.instance = instance;
        }
    }
}
