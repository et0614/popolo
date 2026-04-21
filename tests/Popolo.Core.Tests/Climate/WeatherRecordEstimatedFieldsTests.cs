/* WeatherRecordEstimatedFieldsTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 */

using System;
using Xunit;

using Popolo.Core.Climate.Weather;

namespace Popolo.Core.Tests.Climate
{
  /// <summary>
  /// Unit tests for the tri-state field provenance introduced on
  /// <see cref="WeatherRecord"/> (recorded / estimated / missing).
  /// </summary>
  public class WeatherRecordEstimatedFieldsTests
  {
    private static WeatherRecord BuildMinimalRecord(Action<WeatherRecordBuilder> configure)
    {
      var builder = new WeatherRecordBuilder().SetTime(new DateTime(2026, 1, 1, 0, 0, 0));
      configure(builder);
      return builder.ToRecord();
    }

    [Fact]
    public void Set_MarksFieldAsRecorded()
    {
      var rec = BuildMinimalRecord(b => b.SetDryBulbTemperature(10.0));

      Assert.True(rec.Has(WeatherField.DryBulbTemperature));
      Assert.Equal(WeatherField.DryBulbTemperature, rec.RecordedFields);
      Assert.Equal(WeatherField.None, rec.EstimatedFields);
      Assert.False(rec.IsEstimated(WeatherField.DryBulbTemperature));
    }

    [Fact]
    public void MarkEstimated_MovesFieldFromRecordedToEstimated()
    {
      var rec = BuildMinimalRecord(b => b
          .SetDryBulbTemperature(10.0)
          .MarkEstimated(WeatherField.DryBulbTemperature));

      // 値を持つ (Has) ことは変わらないが、estimated に分類される
      Assert.True(rec.Has(WeatherField.DryBulbTemperature));
      Assert.Equal(WeatherField.None, rec.RecordedFields);
      Assert.Equal(WeatherField.DryBulbTemperature, rec.EstimatedFields);
      Assert.True(rec.IsEstimated(WeatherField.DryBulbTemperature));
    }

    [Fact]
    public void AvailableFields_EqualsUnionOfRecordedAndEstimated()
    {
      var rec = BuildMinimalRecord(b => b
          .SetDryBulbTemperature(10.0)                    // recorded
          .SetGlobalHorizontalRadiation(500.0)            // recorded
          .SetDiffuseHorizontalRadiation(100.0)           // recorded → 直後に estimated 化
          .MarkEstimated(WeatherField.DiffuseHorizontalRadiation));

      var expected = WeatherField.DryBulbTemperature
                   | WeatherField.GlobalHorizontalRadiation
                   | WeatherField.DiffuseHorizontalRadiation;

      Assert.Equal(expected, rec.AvailableFields);
      Assert.Equal(expected, rec.RecordedFields | rec.EstimatedFields);
    }

    [Fact]
    public void RecordedAndEstimated_AreMutuallyExclusive()
    {
      var rec = BuildMinimalRecord(b => b
          .SetDryBulbTemperature(10.0)
          .SetHumidityRatio(5.0)
          .MarkEstimated(WeatherField.HumidityRatio));

      Assert.Equal(WeatherField.None, rec.RecordedFields & rec.EstimatedFields);
    }

    [Fact]
    public void MarkEstimated_MultipleFlagsAtOnce()
    {
      var rec = BuildMinimalRecord(b => b
          .SetDirectNormalRadiation(700.0)
          .SetDiffuseHorizontalRadiation(120.0)
          .MarkEstimated(WeatherField.DirectNormalRadiation | WeatherField.DiffuseHorizontalRadiation));

      Assert.True(rec.IsEstimated(WeatherField.DirectNormalRadiation));
      Assert.True(rec.IsEstimated(WeatherField.DiffuseHorizontalRadiation));
      Assert.True(rec.IsEstimated(
          WeatherField.DirectNormalRadiation | WeatherField.DiffuseHorizontalRadiation));
    }

    [Fact]
    public void IsEstimated_ReturnsFalseForMissingField()
    {
      var rec = BuildMinimalRecord(b => b.SetDryBulbTemperature(10.0));

      // 記録されていないフィールドは estimated でもない
      Assert.False(rec.IsEstimated(WeatherField.HumidityRatio));
    }

    [Fact]
    public void Reset_ClearsBothMasks()
    {
      var builder = new WeatherRecordBuilder()
          .SetTime(new DateTime(2026, 1, 1))
          .SetDryBulbTemperature(10.0)
          .SetHumidityRatio(5.0)
          .MarkEstimated(WeatherField.HumidityRatio);
      builder.Reset();
      builder.SetTime(new DateTime(2026, 1, 1, 1, 0, 0));

      var rec = builder.ToRecord();
      Assert.Equal(WeatherField.None, rec.AvailableFields);
      Assert.Equal(WeatherField.None, rec.RecordedFields);
      Assert.Equal(WeatherField.None, rec.EstimatedFields);
    }
  }
}
