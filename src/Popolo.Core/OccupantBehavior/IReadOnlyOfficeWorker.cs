/* IReadOnlyOfficeWorker.cs
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
