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
using System.Timers;
using System.Collections.Generic;
using OpenMetaverse;

namespace Radegast
{
    public class AutoPilot2
    {
        #region Declarations
        /// <summary>
        /// Delegate to pass the coordinate value of next waypoint to an event-handler
        /// </summary>
        public delegate void WaypointDelegate(Vector3d waypoint);

        /// <summary>
        /// Delegate to pass the coordinate value of next waypoint and the new Status of AutoPilot to an event-handler
        /// </summary>
        public delegate void AutoPilotStatusDelegate(AutoPilotStatus newStatus, Vector3d nextWaypoint);

        /// <summary>
        /// Enum declaration representing the statuses of AutoPilot
        /// </summary>
        public enum AutoPilotStatus
        {
            Idle,
            Paused,
            Moving,
            Cancelled,
            Finished,
            Failed
        }
        #endregion Declarations

        #region Private Variables
        private GridClient Client;
        private Vector3d myGlobalPosition;
        private List<Vector3d> waypoints = new List<Vector3d>();
        private int waypointIndex = 0;
        private double waypointRadius = 2d;
        private Timer ticker = new Timer(500);
        private int stuckTimeout = 10000;
        private int lastDistance = 0;
        private int lastDistanceChanged = -1;
        #endregion Private Variables

        #region Public Variables/Properties
        public bool Loop = false;

        /// <summary>
        /// The Status of the AutoPilot instance
        /// </summary>
        public AutoPilotStatus Status { get; private set; } = AutoPilotStatus.Idle;

        /// <summary>
        /// The Vector3d Waypoints in the AutoPilot instance
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Must have at least 2 Waypoints</exception>
        public List<Vector3d> Waypoints
        {
            get => waypoints;
            set
            {
                if (value.Count > 1)
                {
                    Stop(AutoPilotStatus.Idle);
                    waypoints = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("Waypoints", "Must have at least 2 Waypoints");
                }
            }
        }

        /// <summary>
        /// The previous Vector3d Waypoint along the path. Returns Vector3d.Zero if there is no previous waypoint.
        /// </summary>
        public Vector3d PreviousWaypoint => waypointIndex >= 1 ? waypoints[waypointIndex - 1] : Vector3d.Zero;

        /// <summary>
        /// The next Vector3d Waypoint along the path. Returns Vector3d.Zero if there is no next waypoint.
        /// </summary>
        public Vector3d NextWaypoint => waypointIndex < waypoints.Count ? waypoints[waypointIndex] : Vector3d.Zero;

        /// <summary>
        /// The next Waypoint's index. A new value will immediately take effect if AutoPilot is not Idle
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">NextWaypointIndex must be greater than or equal to 0 and less than the number of Waypoints</exception>
        /// <exception cref="Exception">Must have at least 2 Waypoints</exception>
        public int NextWaypointIndex
        {
            set
            {
                if (waypoints.Count > 1)
                {
                    if (Loop)
                    {
                        waypointIndex = value % waypoints.Count;
                    }
                    else if (value < waypoints.Count && value >= 0)
                    {
                        waypointIndex = value;
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("NextWaypointIndex", "Value must be greater than or equal to 0 and less than the number of Waypoints");
                    }
                    if (Status != AutoPilotStatus.Idle)
                    {
                        Client.Self.AutoPilotCancel();
                        SetStatus(AutoPilotStatus.Moving);
                        Client.Self.AutoPilot(waypoints[waypointIndex].X, waypoints[waypointIndex].Y, waypoints[waypointIndex].Z);
                    }
                }
                else
                {
                    throw new Exception("Must have at least 2 Waypoints");
                }
            }
            get => waypointIndex;
        }

        /// <summary>
        /// The next Waypoint's index
        /// </summary>
        public bool NextWaypointIsFinal => waypointIndex == (waypoints.Count - 1);

        /// <summary>
        /// Returns true if next Waypoint is the Start
        /// </summary>
        public bool NextWaypointIsStart => waypointIndex == 0 && waypoints.Count > 1;

        /// <summary>
        /// The Waypoint detection radius
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">WaypointRadius must be greater than 0</exception>
        public double WaypointRadius
        {
            get => waypointRadius;
            set
            {
                if (value > 0)
                {
                    waypointRadius = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("WaypointRadius", "Value must be greater than 0");
                }
            }
        }

        /// <summary>
        /// The timeout in milliseconds before being considered stuck
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">StuckTimeout must be greater than 0</exception>
        public int StuckTimeout
        {
            get => stuckTimeout;
            set
            {
                if (value > 0)
                {
                    stuckTimeout = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("StuckTimeout", "Value must be greater than 0");
                }
            }
        }
        #endregion Public Variables/Properties

        #region Public Events
        /// <summary>
        /// Event for when agent arrives at a Waypoint
        /// </summary>
        public event WaypointDelegate OnWaypointArrival;

        /// <summary>
        /// Event for when AutoPilot's status changes
        /// </summary>
        public event AutoPilotStatusDelegate OnStatusChange;
        #endregion Public Events

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="client">The GridClient to use</param>
        public AutoPilot2(GridClient client)
        {
            Client = client;
            Client.Objects.TerseObjectUpdate += new EventHandler<TerseObjectUpdateEventArgs>(Objects_TerseObjectUpdate);
            ticker.Elapsed += new ElapsedEventHandler(ticker_Elapsed);
        }

        #region Public Methods
        /// <summary>
        /// Triggers Autopilot to move towards next waypoint along the path
        /// </summary>
        /// <returns>Next Waypoint index</returns>
        public int MoveToNextWaypoint()
        {
            return MoveToNextWaypoint(true);
        }

        /// <summary>
        /// Triggers Autopilot to move towards next waypoint along the path
        /// </summary>
        /// <param name="increment">Increment current Waypoint index</param>
        /// <exception cref="Exception">Must have at least 2 Waypoints</exception>
        /// <returns>Next Waypoint index</returns>
        public int MoveToNextWaypoint(bool increment)
        {
            if (waypoints.Count > 1)
            {
                if (increment)
                {
                    NextWaypointIndex++;
                }
                else
                {
                    Client.Self.AutoPilotCancel();
                    Vector3d nextWaypoint = NextWaypoint;
                    SetStatus(AutoPilotStatus.Moving);
                    Client.Self.AutoPilot(nextWaypoint.X, nextWaypoint.Y, nextWaypoint.Z);
                }
                return waypointIndex;
            }
            else
            {
                throw new Exception("Must have at least 2 Waypoints");
            }
        }

        /// <summary>
        /// Starts AutoPilot from an Idle state. Will trigger a Moving status change.
        /// </summary>
        /// <exception cref="Exception">Must have at least 2 Waypoints</exception>
        /// <exception cref="Exception">Status must be Idle</exception>
        /// <returns>Next Waypoint index</returns>
        public int Start()
        {
            if (waypoints.Count > 1)
            {
                if (Status == AutoPilotStatus.Idle)
                {
                    ticker.Start();
                    return MoveToNextWaypoint(false);
                }
                else
                {
                    throw new Exception("Status must be Idle");
                }
            }
            else
            {
                throw new Exception("Must have at least 2 Waypoints");
            }
        }

        /// <summary>
        /// Restarts AutoPilot. Will trigger a Cancel status change if not already cancelled.
        /// </summary>
        /// <exception cref="Exception">Must have at least 2 Waypoints</exception>
        /// <returns>Next Waypoint index</returns>
        public int Restart()
        {
            if (waypoints.Count > 1)
            {
                Stop();
                lastDistanceChanged = -1;
                return MoveToNextWaypoint(false);
            }
            else
            {
                throw new Exception("Must have at least 2 Waypoints");
            }
        }

        /// <summary>
        /// Pauses AutoPilot from a Moving state. Will trigger a Paused status change.
        /// </summary>
        /// <exception cref="Exception">Status must be Moving</exception>
        /// <returns>Next Waypoint index</returns>
        public int Pause()
        {
            if (Status == AutoPilotStatus.Moving)
            {
                ticker.Stop();
                Client.Self.AutoPilotCancel();
                SetStatus(AutoPilotStatus.Paused);
                return waypointIndex;
            }
            else
            {
                throw new Exception("Status must be Moving");
            }
        }

        /// <summary>
        /// Resumes AutoPilot from a Paused state. Will trigger a Moving status change.
        /// </summary>
        /// <exception cref="Exception">Status must be Paused</exception>
        /// <returns>Next Waypoint index</returns>
        public int Resume()
        {
            if (Status == AutoPilotStatus.Paused)
            {
                ticker.Start();
                return MoveToNextWaypoint(false);
            }
            else
            {
                throw new Exception("Status must be Paused");
            }
        }

        /// <summary>
        /// Stops AutoPilot. Will trigger a Cancel status change if not already cancelled.
        /// </summary>
        public void Stop()
        {
            Stop(AutoPilotStatus.Cancelled);
        }

        /// <summary>
        /// Stops AutoPilot. Will trigger the given status if not already in that state.
        /// </summary>
        /// <param name="newStatus">The new status for AutoPilot. Cannot be Moving</param>
        /// <exception cref="ArgumentOutOfRangeException">newStatus cannot be Moving</exception>
        public void Stop(AutoPilotStatus newStatus)
        {
            if (newStatus != AutoPilotStatus.Moving)
            {
                ticker.Stop();
                Client.Self.AutoPilotCancel();
                SetStatus(newStatus);
                lastDistanceChanged = -1;
                waypointIndex = 0;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(newStatus), "Value cannot be Moving");
            }
        }
        #endregion Public Methods

        #region Private Methods
        /// <summary>
        /// Sets AutoPilot's Status. If newStatus is different from current Status than it will cause OnStatusChange event trigger.
        /// </summary>
        /// <param name="newStatus">The new Status AutoPilot is to be changed to</param>
        /// <returns>True if OnStatusChanged triggered</returns>
        private bool SetStatus(AutoPilotStatus newStatus)
        {
            AutoPilotStatus oldStatus = Status;
            if (oldStatus != newStatus)
            {
                Status = newStatus;
                OnStatusChange?.Invoke(Status, NextWaypoint);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Event Handler for waypoint distance checking
        /// </summary>
        private void Objects_TerseObjectUpdate(object sender, TerseObjectUpdateEventArgs e)
        {
            if (Status == AutoPilotStatus.Moving && e.Update.Avatar && e.Update.LocalID == Client.Self.LocalID)
            {
                uint regionX, regionY;
                Utils.LongToUInts(e.Simulator.Handle, out regionX, out regionY);
                myGlobalPosition = new Vector3d(
                    regionX + e.Update.Position.X,
                    regionY + e.Update.Position.Y,
                    e.Update.Position.Z
                );
                if (Vector3d.Distance(myGlobalPosition, NextWaypoint) <= waypointRadius)
                {
                    if (NextWaypointIsFinal && !Loop)
                    {
                        Stop(AutoPilotStatus.Finished);
                    }
                    else
                    {
                        OnWaypointArrival?.Invoke(NextWaypoint);
                        MoveToNextWaypoint();
                    }
                }
            }
        }

        /// <summary>
        /// Event Handler for Timer which detects if agent is stuck
        /// </summary>
        private void ticker_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Status == AutoPilotStatus.Moving)
            {
                int distance = (int)Vector3d.Distance(myGlobalPosition, NextWaypoint);
                if (distance != lastDistance || lastDistanceChanged < 0)
                {
                    lastDistance = distance;
                    lastDistanceChanged = Environment.TickCount;
                }
                else if (Math.Abs(Environment.TickCount - lastDistanceChanged) > stuckTimeout)
                {
                    Stop(AutoPilotStatus.Failed);
                }
            }
        }
        #endregion Private Methods
    }
}