/* WeatherInterpolator.cs
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
using System.Collections.Generic;
using Popolo.Core.Exceptions;
using Popolo.Core.Utilities;

namespace Popolo.Core.Climate.Weather
{
  /// <summary>
  /// A continuous-time view of a <see cref="WeatherData"/> that returns
  /// interpolated field values at arbitrary times.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Each field is interpolated independently from the subset of records that
  /// have the field recorded. Records whose <see cref="WeatherField"/> flag is
  /// not set are simply skipped for that field, so a field with intermittent
  /// missing values still produces a continuous interpolant from the remaining
  /// observations.
  /// </para>
  /// <para>
  /// At times outside the range of valid observations for a given field, the
  /// value at the nearest end of the range is returned (clamping). When a
  /// field has no recorded value anywhere in the dataset, querying that field
  /// raises <see cref="PopoloInvalidOperationException"/>.
  /// </para>
  /// <para>
  /// The default interpolation strategy is
  /// <see cref="InterpolationStrategy.Linear"/> for temperature and humidity,
  /// and <see cref="InterpolationStrategy.Pchip"/> for the four radiation
  /// fields (to preserve monotonicity and avoid negative overshoot).
  /// Strategies can be overridden per field at construction time.
  /// </para>
  /// <para>
  /// This class is not thread-safe. Each thread that performs interpolation
  /// queries should construct its own <see cref="WeatherInterpolator"/>.
  /// </para>
  /// </remarks>
  public class WeatherInterpolator
  {

    #region インスタンス変数・プロパティ

    private readonly IReadOnlyWeatherData _data;

    /// <summary>
    /// サポートする各フィールドの内部表現。
    /// 欠測を除いた観測点と、その時刻配列、補間戦略を保持する。
    /// </summary>
    private readonly FieldChannel _dryBulbTemperature;
    private readonly FieldChannel _humidityRatio;
    private readonly FieldChannel _globalHorizontalRadiation;
    private readonly FieldChannel _directNormalRadiation;
    private readonly FieldChannel _diffuseHorizontalRadiation;
    private readonly FieldChannel _atmosphericRadiation;

    /// <summary>Gets the underlying weather data source.</summary>
    public IReadOnlyWeatherData Data => _data;

    #endregion

    #region コンストラクタ

    /// <summary>
    /// Initializes an interpolator over the specified dataset using the
    /// default per-field strategies.
    /// </summary>
    /// <param name="data">
    /// The weather data to interpolate. Must be non-empty and its records must
    /// be in non-decreasing order of <see cref="WeatherRecord.Time"/>.
    /// </param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="data"/> is null, empty, or has records that
    /// are not in ascending time order.
    /// </exception>
    public WeatherInterpolator(IReadOnlyWeatherData data)
        : this(data, BuildDefaultStrategies()) { }

    /// <summary>
    /// Initializes an interpolator over the specified dataset with explicit
    /// per-field strategies.
    /// </summary>
    /// <param name="data">
    /// The weather data to interpolate. Must be non-empty and its records must
    /// be in non-decreasing order of <see cref="WeatherRecord.Time"/>.
    /// </param>
    /// <param name="strategies">
    /// Per-field interpolation strategy overrides. Fields that are not present
    /// in this dictionary fall back to the default strategy.
    /// </param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="data"/> is null, empty, or has records that
    /// are not in ascending time order.
    /// </exception>
    public WeatherInterpolator(
        IReadOnlyWeatherData data,
        IReadOnlyDictionary<WeatherField, InterpolationStrategy> strategies)
    {
      if (data == null)
        throw new PopoloArgumentException("data must not be null.", nameof(data));
      if (data.Count == 0)
        throw new PopoloArgumentException(
            "data must contain at least one record.", nameof(data));

      // 時刻昇順の検証
      var records = data.Records;
      for (int i = 1; i < records.Count; i++)
      {
        if (records[i].Time < records[i - 1].Time)
        {
          throw new PopoloArgumentException(
              $"Records must be in non-decreasing order of Time. "
              + $"Record[{i - 1}].Time = {records[i - 1].Time:O}, "
              + $"Record[{i}].Time = {records[i].Time:O}.",
              nameof(data));
        }
      }

      _data = data;

      _dryBulbTemperature         = BuildChannel(data, WeatherField.DryBulbTemperature,         strategies);
      _humidityRatio              = BuildChannel(data, WeatherField.HumidityRatio,              strategies);
      _globalHorizontalRadiation  = BuildChannel(data, WeatherField.GlobalHorizontalRadiation,  strategies);
      _directNormalRadiation      = BuildChannel(data, WeatherField.DirectNormalRadiation,      strategies);
      _diffuseHorizontalRadiation = BuildChannel(data, WeatherField.DiffuseHorizontalRadiation, strategies);
      _atmosphericRadiation       = BuildChannel(data, WeatherField.AtmosphericRadiation,       strategies);
    }

    /// <summary>既定の補間戦略マップ。</summary>
    private static Dictionary<WeatherField, InterpolationStrategy> BuildDefaultStrategies()
    {
      return new Dictionary<WeatherField, InterpolationStrategy>
      {
        { WeatherField.DryBulbTemperature,         InterpolationStrategy.Linear },
        { WeatherField.HumidityRatio,              InterpolationStrategy.Linear },
        { WeatherField.GlobalHorizontalRadiation,  InterpolationStrategy.Pchip  },
        { WeatherField.DirectNormalRadiation,      InterpolationStrategy.Pchip  },
        { WeatherField.DiffuseHorizontalRadiation, InterpolationStrategy.Pchip  },
        { WeatherField.AtmosphericRadiation,       InterpolationStrategy.Pchip  },
      };
    }

    private static FieldChannel BuildChannel(
        IReadOnlyWeatherData data,
        WeatherField field,
        IReadOnlyDictionary<WeatherField, InterpolationStrategy> strategies)
    {
      InterpolationStrategy strategy = strategies != null && strategies.TryGetValue(field, out var s)
          ? s
          : DefaultStrategyFor(field);

      // 当該フィールドが記録されているレコードだけを抽出
      var records = data.Records;
      var times = new List<DateTime>(records.Count);
      var values = new List<double>(records.Count);
      for (int i = 0; i < records.Count; i++)
      {
        var r = records[i];
        if (!r.Has(field)) continue;
        times.Add(r.Time);
        values.Add(GetFieldValue(r, field));
      }

      return new FieldChannel(times.ToArray(), values.ToArray(), strategy, field);
    }

    private static InterpolationStrategy DefaultStrategyFor(WeatherField field)
    {
      switch (field)
      {
        case WeatherField.GlobalHorizontalRadiation:
        case WeatherField.DirectNormalRadiation:
        case WeatherField.DiffuseHorizontalRadiation:
        case WeatherField.AtmosphericRadiation:
          return InterpolationStrategy.Pchip;
        default:
          return InterpolationStrategy.Linear;
      }
    }

    private static double GetFieldValue(WeatherRecord r, WeatherField field)
    {
      switch (field)
      {
        case WeatherField.DryBulbTemperature:         return r.DryBulbTemperature;
        case WeatherField.HumidityRatio:              return r.HumidityRatio;
        case WeatherField.GlobalHorizontalRadiation:  return r.GlobalHorizontalRadiation;
        case WeatherField.DirectNormalRadiation:      return r.DirectNormalRadiation;
        case WeatherField.DiffuseHorizontalRadiation: return r.DiffuseHorizontalRadiation;
        case WeatherField.AtmosphericRadiation:       return r.AtmosphericRadiation;
        default:
          throw new PopoloNotImplementedException(
              $"GetFieldValue does not support field '{field}'.");
      }
    }

    #endregion

    #region インスタンスメソッド(フィールド別クエリ)

    /// <summary>
    /// Interpolates the dry-bulb temperature [°C] at the specified time.
    /// </summary>
    /// <exception cref="PopoloInvalidOperationException">
    /// Thrown when the underlying dataset has no record of this field.
    /// </exception>
    public double GetDryBulbTemperature(DateTime time) => _dryBulbTemperature.Interpolate(time);

    /// <summary>Interpolates the absolute humidity ratio [g/kg(DA)] at the specified time.</summary>
    public double GetHumidityRatio(DateTime time) => _humidityRatio.Interpolate(time);

    /// <summary>Interpolates the global horizontal radiation [W/m²] at the specified time.</summary>
    public double GetGlobalHorizontalRadiation(DateTime time)
        => _globalHorizontalRadiation.Interpolate(time);

    /// <summary>Interpolates the direct normal radiation [W/m²] at the specified time.</summary>
    public double GetDirectNormalRadiation(DateTime time)
        => _directNormalRadiation.Interpolate(time);

    /// <summary>Interpolates the diffuse horizontal radiation [W/m²] at the specified time.</summary>
    public double GetDiffuseHorizontalRadiation(DateTime time)
        => _diffuseHorizontalRadiation.Interpolate(time);

    /// <summary>Interpolates the atmospheric (long-wave) radiation [W/m²] at the specified time.</summary>
    public double GetAtmosphericRadiation(DateTime time)
        => _atmosphericRadiation.Interpolate(time);

    /// <summary>
    /// Returns a <see cref="WeatherRecord"/> whose six supported fields
    /// (temperature, humidity ratio, three radiation components, and
    /// atmospheric radiation) are filled in by interpolation at the specified
    /// time. Fields for which the dataset has no recorded values are left
    /// missing.
    /// </summary>
    public WeatherRecord Sample(DateTime time)
    {
      var builder = new WeatherRecordBuilder().SetTime(time);

      if (_dryBulbTemperature.HasAnyData)
        builder.SetDryBulbTemperature(_dryBulbTemperature.Interpolate(time));
      if (_humidityRatio.HasAnyData)
        builder.SetHumidityRatio(_humidityRatio.Interpolate(time));
      if (_globalHorizontalRadiation.HasAnyData)
        builder.SetGlobalHorizontalRadiation(_globalHorizontalRadiation.Interpolate(time));
      if (_directNormalRadiation.HasAnyData)
        builder.SetDirectNormalRadiation(_directNormalRadiation.Interpolate(time));
      if (_diffuseHorizontalRadiation.HasAnyData)
        builder.SetDiffuseHorizontalRadiation(_diffuseHorizontalRadiation.Interpolate(time));
      if (_atmosphericRadiation.HasAnyData)
        builder.SetAtmosphericRadiation(_atmosphericRadiation.Interpolate(time));

      return builder.ToRecord();
    }

    #endregion

    #region 内部補助クラス

    /// <summary>
    /// 単一フィールドに対する時刻配列・値配列・補間戦略のバンドル。
    /// PCHIP の場合は BoundaryInterpolator を内部で遅延生成する。
    /// </summary>
    private sealed class FieldChannel
    {
      private readonly DateTime[] _times;
      private readonly double[] _values;
      private readonly InterpolationStrategy _strategy;
      private readonly WeatherField _field;
      private BoundaryInterpolator? _pchip;

      public bool HasAnyData => _times.Length > 0;

      public FieldChannel(
          DateTime[] times,
          double[] values,
          InterpolationStrategy strategy,
          WeatherField field)
      {
        _times = times;
        _values = values;
        _strategy = strategy;
        _field = field;
      }

      public double Interpolate(DateTime time)
      {
        if (_times.Length == 0)
        {
          throw new PopoloInvalidOperationException(
              $"Cannot interpolate field '{_field}': no recorded values in dataset.");
        }

        // 単一観測点しかない場合は、常にその値を返す
        if (_times.Length == 1) return _values[0];

        // 範囲外クランプ
        if (time <= _times[0]) return _values[0];
        if (time >= _times[_times.Length - 1]) return _values[_values.Length - 1];

        switch (_strategy)
        {
          case InterpolationStrategy.Linear:
            return LinearInterpolate(time);
          case InterpolationStrategy.Pchip:
            return PchipInterpolate(time);
          case InterpolationStrategy.StepHold:
            return StepHoldInterpolate(time);
          default:
            throw new PopoloNotImplementedException(
                $"Interpolation strategy '{_strategy}' is not implemented.");
        }
      }

      private double LinearInterpolate(DateTime time)
      {
        int idx = FindLowerIndex(time);
        long t0 = _times[idx].Ticks;
        long t1 = _times[idx + 1].Ticks;
        double fraction = (double)(time.Ticks - t0) / (t1 - t0);
        return _values[idx] + fraction * (_values[idx + 1] - _values[idx]);
      }

      private double StepHoldInterpolate(DateTime time)
      {
        int idx = FindLowerIndex(time);
        return _values[idx];
      }

      private double PchipInterpolate(DateTime time)
      {
        if (_pchip == null)
        {
          _pchip = new BoundaryInterpolator(_times);
          _pchip.AddSeries(_values);
        }
        return _pchip.Interpolate(time, 0);
      }

      /// <summary>
      /// Returns index i such that <c>_times[i] &lt;= time &lt; _times[i+1]</c>.
      /// Precondition: _times has at least 2 elements and
      /// <c>_times[0] &lt; time &lt; _times[Length-1]</c>.
      /// </summary>
      private int FindLowerIndex(DateTime time)
      {
        int pos = Array.BinarySearch(_times, time);
        if (pos >= 0)
        {
          // 完全一致。後続の区間補間を avoid するため、末尾でない場合はそのまま返す。
          return pos < _times.Length - 1 ? pos : pos - 1;
        }
        int insert = ~pos;
        // insert は time を挿入すべき位置。insert-1 が下限インデックス。
        return insert - 1;
      }
    }

    #endregion

  }
}
