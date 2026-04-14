/* IReadOnlyOfficeWorker.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using static Popolo.Core.OccupantBehavior.OfficeTenant;

namespace Popolo.Core.OccupantBehavior
{
  /// <summary>Represents a read-only view of an office worker.</summary>
  public interface IReadOnlyOfficeWorker
  {
    /// <summary>Gets a value indicating whether the worker is male.</summary>
    bool IsMale { get; }

    /// <summary>Gets the job category.</summary>
    OfficeWorker.CategoryOfJob Job { get; }

    /// <summary>Gets the age [years].</summary>
    int Age { get; }

    /// <summary>Gets the tenant office to which this worker belongs.</summary>
    IReadOnlyOfficeTenant Office { get; }

    /// <summary>Gets the arrival time at the office.</summary>
    DateTime ArriveTime { get; }

    /// <summary>Gets the departure time from the office.</summary>
    DateTime LeaveTime { get; }

    /// <summary>Gets a value indicating whether the worker is on leave.</summary>
    bool IsLOA { get; }

    /// <summary>Gets a value indicating whether the worker is currently in the office.</summary>
    bool StayInOffice { get; }

    /// <summary>Updates the in-office presence state for the current time step.</summary>
    /// <param name="dTime">Current date and time.</param>
    void UpdateStatus(DateTime dTime);

    /// <summary>Computes the expected number of minutes per day spent in the office.</summary>
    /// <returns>Expected minutes per day spent in the office.</returns>
    double CalculateStayInOfficeMinutes();
  }
}
