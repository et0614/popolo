/* ValidationMessage.cs
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
