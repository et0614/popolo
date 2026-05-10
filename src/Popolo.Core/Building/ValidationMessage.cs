/* ValidationMessage.cs
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

namespace Popolo.Core.Building
{
  /// <summary>Severity classification for a <see cref="ValidationMessage"/>.</summary>
  public enum ValidationSeverity
  {
    /// <summary>
    /// The configuration is internally consistent but a setting is unusual or
    /// suboptimal. The simulation will run but the user may want to investigate.
    /// </summary>
    Warning,

    /// <summary>
    /// The configuration is broken in a way that will produce wrong results or
    /// throw an exception during simulation. The user must fix this before
    /// running the model.
    /// </summary>
    Error,
  }

  /// <summary>
  /// Diagnostic emitted by <see cref="MultiRoom.Validate"/> /
  /// <see cref="BuildingThermalModel.Validate"/>.
  /// </summary>
  public sealed class ValidationMessage
  {
    /// <summary>Severity classification.</summary>
    public ValidationSeverity Severity { get; }

    /// <summary>Human-readable diagnostic text.</summary>
    public string Message { get; }

    /// <summary>Initializes a new instance.</summary>
    public ValidationMessage(ValidationSeverity severity, string message)
    {
      Severity = severity;
      Message = message ?? string.Empty;
    }

    /// <summary>Convenience factory for an Error-severity message.</summary>
    public static ValidationMessage Error(string message)
        => new ValidationMessage(ValidationSeverity.Error, message);

    /// <summary>Convenience factory for a Warning-severity message.</summary>
    public static ValidationMessage Warning(string message)
        => new ValidationMessage(ValidationSeverity.Warning, message);

    /// <summary>Returns "[Severity] Message".</summary>
    public override string ToString() => $"[{Severity}] {Message}";
  }
}
