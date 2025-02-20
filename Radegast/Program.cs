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
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using CommandLine;
using CommandLine.Text;
#if (COGBOT_LIBOMV || USE_STHREADS)
using ThreadPoolUtil;
using Thread = ThreadPoolUtil.Thread;
using ThreadPool = ThreadPoolUtil.ThreadPool;
using Monitor = ThreadPoolUtil.Monitor;
#endif
using System.Threading;
using OpenTK.Graphics.OpenGL;

namespace Radegast
{
    public class CommandLineOptions
    {
        [Option('u', "username", HelpText = "Username, use quotes to supply \"First Last\"")]
        public string Username { get; set; } = string.Empty;

        [Option('p', "password", HelpText = "Account password")]
        public string Password { get; set; } = string.Empty;

        [Option('a', "autologin", HelpText = "Automatically login with provided user credentials")]
        public bool AutoLogin { get; set; } = false;

        [Option('g', "grid", HelpText = "Grid ID to login into, try --list-grids to see IDs used for this parameter")]
        public string Grid { get; set; } = string.Empty;

        [Option('l', "location", HelpText = "Login location: last, home or region name. Region name can also be in format regionname/x/y/z")]
        public string Location { get; set; } = string.Empty;

        [Option("list-grids", HelpText = "Lists grid IDs used for --grid option")]
        public bool ListGrids { get; set; } = false;

        [Option("loginuri", HelpText = "Use this URI to login (don't use with --grid)")]
        public string LoginUri { get; set; } = string.Empty;

        [Option("no-sound", HelpText = "Disable sound")]
        public bool DisableSound { get; set; } = false;


        public HelpText GetHeader()
        {
            HelpText header = new HelpText(Properties.Resources.RadegastTitle)
            {
                AdditionalNewLineAfterOption = true,
                Copyright = new CopyrightInfo("Radegast Development Team, Cinderblocks Design", 2009, 2019)
            };
            header.AddPreOptionsLine("https://radegast.life/");
            return header;
        }
    }

    public static class MainProgram
    {
        /// <summary>
        /// Parsed command line options
        /// </summary>
        public static CommandLineOptions s_CommandLineOpts;

        static void RunRadegast(CommandLineOptions args)
        {
            s_CommandLineOpts = args;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Increase the number of IOCP threads available. Mono defaults to a tragically low number
            int workerThreads, iocpThreads;
            ThreadPool.GetMaxThreads(out workerThreads, out iocpThreads);

            if (workerThreads < 500 || iocpThreads < 1000)
            {
                if (workerThreads < 500) workerThreads = 500;
                if (iocpThreads < 1000) iocpThreads = 1000;
                ThreadPool.SetMaxThreads(workerThreads, iocpThreads);
            }

            // Change current working directory to Radegast install dir
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) 
                                          ?? throw new InvalidOperationException());

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // See if we only wanted to display list of grids
            if (s_CommandLineOpts.ListGrids)
            {
                Console.WriteLine(s_CommandLineOpts.GetHeader());
                Console.WriteLine();
                GridManager grids = new GridManager();
                Console.WriteLine("Use Grid ID as the parameter for --grid");
                Console.WriteLine("{0,-25} - {1}", "Grid ID", "Grid Name");
                Console.WriteLine(@"========================================================");

                for (int i = 0; i < grids.Count; i++)
                {
                    Console.WriteLine(@"{0,-25} - {1}", grids[i].ID, grids[i].Name);
                }

                Environment.Exit(0);
            }

            // Create main Radegast instance
            RadegastInstance instance = RadegastInstance.GlobalInstance;
            Application.Run(instance.MainForm);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var instance = RadegastInstance.GlobalInstance;
            instance.Client.Network.Logout();
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                var parser = new Parser(x => x.HelpWriter = null);
                var result = parser.ParseArguments<CommandLineOptions>(args);
                result.WithParsed(RunRadegast)
                    .WithNotParsed(errs =>
                    {
                        var helpText = HelpText.AutoBuild(result, h =>
                        {
                            h.AdditionalNewLineAfterOption = false;
                            return h;
                        }, e => e);
                        Console.WriteLine(helpText);
                        Console.WriteLine("Use Grid ID as the parameter for --grid");
                        Console.WriteLine("{0,-25} - {1}", "Grid ID", "Grid Name");
                        Console.WriteLine();
                    });
            }
            catch (Exception e)
            {
                if (System.Diagnostics.Debugger.IsAttached){ throw; }

                string errMsg = "Unhandled " + e + ": " +
                                e.Message + Environment.NewLine +
                                e.StackTrace + Environment.NewLine;

                OpenMetaverse.Logger.Log(errMsg, OpenMetaverse.Helpers.LogLevel.Error);

                string dlgMsg = "Radegast has encountered an unrecoverable error." + Environment.NewLine +
                                "Would you like to send the error report to help improve Radegast?";

                var res = MessageBox.Show(dlgMsg, @"Unrecoverable error", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);

                if (res == DialogResult.Yes)
                {
                    var reporter = new ErrorReporter("http://api.radegast.org/svc/error_report");
                    reporter.SendExceptionReport(e);
                }

                Environment.Exit(1);
            }
        }
    }
}
