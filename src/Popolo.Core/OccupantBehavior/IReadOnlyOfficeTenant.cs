/* IReadOnlyOfficeTenant.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
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
    uint StayWorkerCount { get; }

    /// <summary>Gets the total number of workers.</summary>
    uint OfficeWorkerCount { get; }

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
    uint GetWorkerCount(bool isMale, bool isPermanent, OfficeTenant.CategoryOfJob job);

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
