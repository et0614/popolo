/* HaspWeatherReader.cs
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
using System.Globalization;
using System.IO;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;
using Popolo.Core.Physics;

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Reads weather data in HASP format (Japanese legacy building simulation
  /// data format).
  /// </summary>
  /// <remarks>
  /// <para>
  /// HASP stores 365 days of hourly data as a text file with seven lines per
  /// day: dry-bulb temperature, humidity ratio, direct normal radiation,
  /// diffuse horizontal radiation, nocturnal (net outgoing longwave)
  /// radiation, wind direction, and wind speed. Each line carries 24 hourly
  /// values as 3-character fixed-width fields.
  /// </para>
  /// <para>
  /// HASP carries no year or station information, so this reader uses
  /// <see cref="SyntheticYear"/> (default 1999, matching the legacy behaviour)
  /// for the logical time axis, and leaves <see cref="WeatherData.Station"/>
  /// empty. Callers that need station metadata should set it after reading.
  /// </para>
  /// <para>
  /// Unit conversions applied by this reader:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>Temperature: (raw − 500) × 0.1 [°C]</description></item>
  ///   <item><description>Humidity ratio: raw × 0.1 [g/kg(DA)]</description></item>
  ///   <item><description>Radiation (DNI, DHI): raw × 0.01 [MJ/(m²·h)] → ×1e6/3600 [W/m²]</description></item>
  ///   <item><description>
  ///     Nocturnal radiation: raw × 0.01 [MJ/(m²·h)] → ×1e6/3600 [W/m²], then
  ///     converted to atmospheric (downwelling longwave) radiation via
  ///     <c>AtmosphericRadiation = σ(T+273.15)⁴ − NocturnalRadiation</c>,
  ///     since Popolo uses downwelling longwave for the
  ///     <see cref="WeatherField.AtmosphericRadiation"/> field.
  ///   </description></item>
  ///   <item><description>
  ///     Wind direction: the raw HASP value is a 1..16 compass-point index
  ///     (1 = N, 5 = E, 9 = S, 13 = W). It is converted to radian using
  ///     Popolo's south-origin convention. Values of 0 (calm / unknown) are
  ///     stored as missing.
  ///   </description></item>
  ///   <item><description>Wind speed: raw × 0.1 [m/s]</description></item>
  /// </list>
  /// <para>
  /// This reader does not act on any <see cref="WeatherReadOptions"/> fields;
  /// HASP records all physical quantities directly.
  /// </para>
  /// </remarks>
  public class HaspWeatherReader : IWeatherDataReader
  {
    /// <summary>ステファン・ボルツマン定数 [W/(m²·K⁴)]。内部計算用。</summary>
    private const double Sigma = PhysicsConstants.StefanBoltzmannConstant;

    /// <summary>摂氏→ケルビン変換。内部計算用。</summary>
    private const double CelsiusToKelvin = PhysicsConstants.CelsiusToKelvinOffset;

    /// <summary>
    /// Synthetic year to stamp onto the records. Defaults to 1999, matching
    /// the behaviour of the legacy <c>WeatherConverter.HASPtoCSV</c>.
    /// </summary>
    public int SyntheticYear { get; set; } = 1999;

    /// <inheritdoc />
    public WeatherData Read(string path, WeatherReadOptions? options = null)
    {
      if (string.IsNullOrEmpty(path))
        throw new PopoloArgumentException("path must not be null or empty.", nameof(path));
      using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
      return Read(stream, options);
    }

    /// <inheritdoc />
    public WeatherData Read(Stream stream, WeatherReadOptions? options = null)
    {
      if (stream == null)
        throw new PopoloArgumentException("stream must not be null.", nameof(stream));

      using var reader = new StreamReader(stream, leaveOpen: true);
      string content = reader.ReadToEnd();
      return ParseCore(content);
    }

    private WeatherData ParseCore(string content)
    {
      var ci = CultureInfo.InvariantCulture;

      // HASP は CRLF 区切り、空行は無視
      string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
      if (lines.Length < 365 * 7)
      {
        throw new PopoloArgumentException(
            $"HASP data has {lines.Length} lines; expected at least {365 * 7} (7 lines × 365 days).",
            "stream");
      }

      var data = new WeatherData
      {
        Source = WeatherDataSource.Hasp,
        NominalInterval = TimeSpan.FromHours(1),
      };

      var builder = new WeatherRecordBuilder();
      var startDate = new DateTime(SyntheticYear, 1, 1, 0, 0, 0);

      for (int day = 0; day < 365; day++)
      {
        string dbtLine = lines[day * 7];
        string hrtLine = lines[day * 7 + 1];
        string dnrLine = lines[day * 7 + 2];
        string dhrLine = lines[day * 7 + 3];
        string ncrLine = lines[day * 7 + 4];
        string wdrLine = lines[day * 7 + 5];
        string wspLine = lines[day * 7 + 6];

        for (int hour = 0; hour < 24; hour++)
        {
          int off = hour * 3;

          // 値のパース
          double temperature = (double.Parse(dbtLine.Substring(off, 3), ci) - 500.0) * 0.1;
          double humidityRatio = double.Parse(hrtLine.Substring(off, 3), ci) * 0.1;
          double dniMJ = double.Parse(dnrLine.Substring(off, 3), ci) * 0.01;
          double dhiMJ = double.Parse(dhrLine.Substring(off, 3), ci) * 0.01;
          double ncrMJ = double.Parse(ncrLine.Substring(off, 3), ci) * 0.01;
          int windCompass = int.Parse(wdrLine.Substring(off, 3), ci);
          double windSpeed = double.Parse(wspLine.Substring(off, 3), ci) * 0.1;

          // MJ/(m²·h) → W/m²
          double directNormal = MJhToWm2(dniMJ);
          double diffuseHoriz = MJhToWm2(dhiMJ);
          double nocturnalDownward = MJhToWm2(ncrMJ);

          // 夜間放射(outgoing net) → 大気放射(downwelling)
          double blackBody = BlackBodyRadiation(temperature);
          double atmosphericRadiation = blackBody - nocturnalDownward;

          builder.Reset();
          builder.SetTime(startDate.AddHours(day * 24 + hour));
          builder.SetDryBulbTemperature(temperature);
          builder.SetHumidityRatio(humidityRatio);
          builder.SetDirectNormalRadiation(Math.Max(0.0, directNormal));
          builder.SetDiffuseHorizontalRadiation(Math.Max(0.0, diffuseHoriz));
          builder.SetAtmosphericRadiation(Math.Max(0.0, atmosphericRadiation));
          builder.SetWindSpeed(windSpeed);

          // 風向: 16方位インデックスを radian に変換。0 は calm/未定義扱い。
          if (windCompass >= 1 && windCompass <= 16)
          {
            double bearing = 22.5 * windCompass;
            if (bearing >= 360.0) bearing -= 360.0;
            builder.SetWindDirection(WindDirectionUtil.FromNorthBearingDegrees(bearing));
          }

          data.Add(builder.ToRecord());
        }
      }

      return data;
    }

    private static double MJhToWm2(double mjPerM2Hour) => mjPerM2Hour * 1.0e6 / 3600.0;

    /// <summary>σ(T + 273.15)⁴ [W/m²]。</summary>
    private static double BlackBodyRadiation(double temperatureCelsius)
    {
      double k = temperatureCelsius + CelsiusToKelvin;
      return Sigma * k * k * k * k;
    }
  }
}
