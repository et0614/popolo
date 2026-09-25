/* TimestampConvention.cs
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

using Popolo.Core.Climate.Weather;

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// How the timestamp of a <see cref="WeatherRecord"/> relates to the
  /// observation interval it represents.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Hourly weather archives such as EPW, TMY1, HASP, and WEA2 label their
  /// records with the <b>end</b> of the observation interval (e.g. the hour
  /// "1" represents the period <c>[00:00, 01:00)</c>). Popolo's readers,
  /// however, normalise that labelling on import — the file's hour-1 record
  /// is stored with <see cref="WeatherRecord.Time"/> equal to <c>00:00</c>,
  /// so the stored timestamp is already the <b>start</b> of the interval.
  /// </para>
  /// <para>
  /// Consequently, <see cref="StartOfInterval"/> is the default and matches
  /// every built-in reader. Use <see cref="EndOfInterval"/> only for custom
  /// data pipelines that stamp records with the end of the interval
  /// (e.g. raw hourly data piped in from an external tool).
  /// </para>
  /// <para>
  /// This enum tells <see cref="WeatherCompleter"/> how to map a
  /// <see cref="WeatherRecord.Time"/> to the (start, end) of its sampling
  /// interval, so it can integrate solar geometry properly. The interval
  /// length comes from <see cref="WeatherData.NominalInterval"/>; when the
  /// dataset does not declare one, the convention is ignored and the record
  /// timestamp is used as an instantaneous evaluation point.
  /// </para>
  /// </remarks>
  public enum TimestampConvention
  {
    /// <summary>
    /// The record time marks the start of the interval, so the interval is
    /// <c>[time, time + Δt)</c>. This matches the DateTime values produced
    /// by every built-in reader (EPW, TMY1, HASP, WEA2, Exa) and is the
    /// default.
    /// </summary>
    StartOfInterval,

    /// <summary>
    /// The record time marks the end of the observation interval, so the
    /// interval is <c>(time − Δt, time]</c>. Use this only when records are
    /// stamped with the end of the integration period — unusual in Popolo
    /// since the built-in readers normalise end-of-hour file labels to
    /// start-of-interval DateTime values on import.
    /// </summary>
    EndOfInterval,

    /// <summary>
    /// The record time is the midpoint of the interval, so the interval is
    /// <c>[time − Δt/2, time + Δt/2)</c>.
    /// </summary>
    Midpoint,

    /// <summary>
    /// The record is an instantaneous value at <c>time</c>, not the result
    /// of integration. No interval averaging is performed; solar geometry is
    /// evaluated at the record time directly.
    /// </summary>
    Instant,
  }
}
