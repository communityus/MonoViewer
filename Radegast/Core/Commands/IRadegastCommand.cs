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

namespace Radegast
{
    public delegate void CommandExecuteDelegate(string name, string[] cmdArgs, ConsoleWriteLine WriteLine);
    public delegate void ConsoleWriteLine(string fmt, params object[] args);
    public interface IRadegastCommand: IDisposable
    {
        string Name { get; }
        string Description { get; }
        string Usage { get; }
        void StartCommand(RadegastInstance inst);
        void Execute(string name, string[] cmdArgs, ConsoleWriteLine WriteLine);
    }
}