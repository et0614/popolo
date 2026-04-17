using Popolo.Core.Exceptions;
using Popolo.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Popolo.Core.Tests.Utilities
{
  /// <summary>BoundaryInterpolator のテスト</summary>
  public class BoundaryInterpolatorTests
  {

    #region コンストラクタのテスト

    /// <summary>ソートされていない日時配列で PopoloArgumentException が発生する</summary>
    [Fact]
    public void Constructor_UnsortedDateTimes_ThrowsPopoloArgumentException()
    {
      var dateTimes = new[]
      {
                new DateTime(2024, 1, 1),
                new DateTime(2024, 1, 3),
                new DateTime(2024, 1, 2), //逆順
            };
      Assert.Throws<PopoloArgumentException>(
          () => new BoundaryInterpolator(dateTimes));
    }

    /// <summary>データ数不一致で PopoloArgumentException が発生する</summary>
    [Fact]
    public void Constructor_MismatchedSeriesLength_ThrowsPopoloArgumentException()
    {
      var dateTimes = new[]
      {
                new DateTime(2024, 1, 1),
                new DateTime(2024, 1, 2),
                new DateTime(2024, 1, 3),
            };
      var values = new System.Collections.Generic.List<double[]>
            {
                new double[] { 1.0, 2.0 } //長さが3ではなく2
            };
      Assert.Throws<PopoloArgumentException>(
          () => new BoundaryInterpolator(dateTimes, values));
    }

    #endregion

    #region 補間のテスト

    /// <summary>ノード点では補間値がデータ値と一致する</summary>
    [Fact]
    public void Interpolate_AtNode_ReturnsExactValue()
    {
      var dateTimes = new[]
      {
                new DateTime(2024, 1,  1),
                new DateTime(2024, 4,  1),
                new DateTime(2024, 7,  1),
                new DateTime(2024, 10, 1),
            };
      var values = new double[] { 5.0, 15.0, 25.0, 15.0 };
      var interp = new BoundaryInterpolator(dateTimes);
      interp.AddSeries(values);

      for (int i = 0; i < dateTimes.Length; i++)
        Assert.Equal(values[i], interp.Interpolate(dateTimes[i], 0), precision: 6);
    }

    /// <summary>範囲外（先頭より前）は先頭の値を返す</summary>
    [Fact]
    public void Interpolate_BeforeFirstNode_ReturnsFirstValue()
    {
      var dateTimes = new[]
      {
                new DateTime(2024, 3, 1),
                new DateTime(2024, 6, 1),
                new DateTime(2024, 9, 1),
            };
      var values = new double[] { 10.0, 20.0, 15.0 };
      var interp = new BoundaryInterpolator(dateTimes);
      interp.AddSeries(values);

      double result = interp.Interpolate(new DateTime(2024, 1, 1), 0);
      Assert.Equal(10.0, result, precision: 6);
    }

    /// <summary>範囲外（末尾より後）は末尾の値を返す</summary>
    [Fact]
    public void Interpolate_AfterLastNode_ReturnsLastValue()
    {
      var dateTimes = new[]
      {
                new DateTime(2024, 3, 1),
                new DateTime(2024, 6, 1),
                new DateTime(2024, 9, 1),
            };
      var values = new double[] { 10.0, 20.0, 15.0 };
      var interp = new BoundaryInterpolator(dateTimes);
      interp.AddSeries(values);

      double result = interp.Interpolate(new DateTime(2024, 12, 1), 0);
      Assert.Equal(15.0, result, precision: 6);
    }

    /// <summary>単調増加データでは補間値も単調増加（PCHIPの単調性保証）</summary>
    [Fact]
    public void Interpolate_MonotonicData_RemainsMonotonic()
    {
      var dateTimes = new[]
      {
                new DateTime(2024, 1, 1),
                new DateTime(2024, 4, 1),
                new DateTime(2024, 7, 1),
                new DateTime(2024, 10, 1),
            };
      var values = new double[] { 0.0, 10.0, 20.0, 30.0 };
      var interp = new BoundaryInterpolator(dateTimes);
      interp.AddSeries(values);

      //1日刻みで補間して単調増加を確認
      double prev = double.MinValue;
      for (int d = 0; d < 365; d++)
      {
        double val = interp.Interpolate(new DateTime(2024, 1, 1).AddDays(d), 0);
        Assert.True(val >= prev - 1e-9);
        prev = val;
      }
    }

    /// <summary>InterpolateAllSeriesは全シリーズを同時に補間する</summary>
    [Fact]
    public void InterpolateAllSeries_ReturnsAllSeries()
    {
      var dateTimes = new[]
      {
                new DateTime(2024, 1, 1),
                new DateTime(2024, 7, 1),
                new DateTime(2025, 1, 1),
            };
      var interp = new BoundaryInterpolator(dateTimes);
      interp.AddSeries(new double[] { 0.0, 10.0, 0.0 });
      interp.AddSeries(new double[] { 100.0, 50.0, 100.0 });

      var target = new DateTime(2024, 4, 1);
      double[] all = interp.InterpolateAllSeries(target);

      Assert.Equal(2, all.Length);
      Assert.Equal(interp.Interpolate(target, 0), all[0], precision: 10);
      Assert.Equal(interp.Interpolate(target, 1), all[1], precision: 10);
    }

    #endregion

    #region AddSeriesのテスト

    /// <summary>長さ不一致のシリーズ追加で PopoloArgumentException が発生する</summary>
    [Fact]
    public void AddSeries_WrongLength_ThrowsPopoloArgumentException()
    {
      var dateTimes = new[]
      {
                new DateTime(2024, 1, 1),
                new DateTime(2024, 7, 1),
                new DateTime(2025, 1, 1),
            };
      var interp = new BoundaryInterpolator(dateTimes);
      Assert.Throws<PopoloArgumentException>(
          () => interp.AddSeries(new double[] { 1.0, 2.0 })); //長さ2（3が必要）
    }

    /// <summary>AddSeries後にNumberOfSeriesが増加する</summary>
    [Fact]
    public void AddSeries_IncreasesNumberOfSeries()
    {
      var dateTimes = new[]
      {
                new DateTime(2024, 1, 1),
                new DateTime(2024, 7, 1),
            };
      var interp = new BoundaryInterpolator(dateTimes);
      Assert.Equal(0, interp.SeriesCount);
      interp.AddSeries(new double[] { 1.0, 2.0 });
      Assert.Equal(1, interp.SeriesCount);
      interp.AddSeries(new double[] { 3.0, 4.0 });
      Assert.Equal(2, interp.SeriesCount);
    }

    #endregion

  }
}
