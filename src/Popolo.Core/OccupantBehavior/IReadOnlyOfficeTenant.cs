/* IReadOnlyOfficeTenant.cs
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

namespace Popolo.Core.OccupantBehavior
{
  /// <summary>Represents a read-only view of an office tenant.</summary>
  public interface IReadOnlyOfficeTenant
  {
    /// <summary>Gets the industry category.</summary>
    OfficeTenant.CategoryOfIndustry Industry { get; }

    /// <summary>Gets the floor area [m²].</summary>
    double FloorArea { get; }

    /// <summary>Gets the array of office workers.</summary>
    IReadOnlyOfficeWorker[] OfficeWorkers { get; }

    /// <summary>Gets the number of workers currently in the office.</summary>
    uint StayWorkerNumber { get; }

    /// <summary>Gets the total number of workers.</summary>
    uint OfficeWorkerNumber { get; }

    /// <summary>Gets the regular holiday days of the week.</summary>
    OfficeTenant.DaysOfWeek Holidays { get; }

    /// <summary>Gets the business start hour.</summary>
    int StartHour { get; }

    /// <summary>Gets the business start minute.</summary>
    int StartMinute { get; }

    /// <summary>Gets the business end hour.</summary>
    int EndHour { get; }

    /// <summary>Gets the business end minute.</summary>
    int EndMinute { get; }

    /// <summary>Gets the lunch break start hour.</summary>
    int LunchStartHour { get; }

    /// <summary>Gets the lunch break start minute.</summary>
    int LunchStartMinute { get; }

    /// <summary>Gets the lunch break end hour.</summary>
    int LunchEndHour { get; }

    /// <summary>Gets the lunch break end minute.</summary>
    int LunchEndMinute { get; }

    /// <summary>Gets the number of workers matching the specified criteria [persons].</summary>
    /// <param name="isMale">True for male; false for female.</param>
    /// <param name="isPermanent">True for non-permanent employee.</param>
    /// <param name="job">Job category.</param>
    /// <returns>Number of workers [persons].</returns>
    uint GetNumber(bool isMale, bool isPermanent, OfficeTenant.CategoryOfJob job);

    /// <summary>Determines whether the specified date is a holiday.</summary>
    /// <param name="dTime">Date to check.</param>
    /// <returns>True if the specified date is a holiday.</returns>
    bool IsHoliday(DateTime dTime);

    /// <summary>Determines whether the specified time is within business hours.</summary>
    /// <param name="dTime">Current date and time.</param>
    /// <returns>True if within business hours.</returns>
    bool IsBuisinessHours(DateTime dTime);
  }
}
