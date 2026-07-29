/* CsvBoundaryReaderTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using System.IO;
using System.Text;
using Xunit;
using Popolo.Core.Exceptions;
using Popolo.Core.Utilities;
using Popolo.IO.Boundary;

namespace Popolo.IO.Tests.Boundary
{
  /// <summary>Unit tests for <see cref="CsvBoundaryReader"/>.</summary>
  public class CsvBoundaryReaderTests
  {

    #region Helpers

    private static Stream MakeStream(string content)
    {
      return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private const string SAMPLE_CSV =
      "# occupancy and internal heat gain schedule\n" +
      "time,occupants,equipment\n" +
      "\n" +
      "2026-01-01T08:00:00,0,120\n" +
      "2026-01-01T09:00:00,50,480\n" +
      "2026-01-01T10:00:00,50,500\n";

    #endregion

    #region Normal cases

    /// <summary>ヘッダから系列名を取得し、系列数・ノード数が正しい</summary>
    [Fact]
    public void Read_ParsesHeaderAndSize()
    {
      var reader = new CsvBoundaryReader();
      BoundaryInterpolator interp = reader.Read(MakeStream(SAMPLE_CSV), out string[] names);

      Assert.Equal(new[] { "occupants", "equipment" }, names);
      Assert.Equal(2, interp.SeriesCount);
      Assert.Equal(3, interp.NodeCount);
    }

    /// <summary>既定では全系列が線形補間で読み込まれる</summary>
    [Fact]
    public void Read_DefaultsToLinearInterpolation()
    {
      var reader = new CsvBoundaryReader();
      BoundaryInterpolator interp = reader.Read(MakeStream(SAMPLE_CSV), out _);

      Assert.Equal(BoundaryInterpolator.InterpolationMethod.Linear,
        interp.GetInterpolationMethod(0));
      Assert.Equal(BoundaryInterpolator.InterpolationMethod.Linear,
        interp.GetInterpolationMethod(1));

      //8:30の値は両端の平均
      Assert.Equal(25.0,
        interp.Interpolate(new DateTime(2026, 1, 1, 8, 30, 0), 0), precision: 9);
      Assert.Equal(300.0,
        interp.Interpolate(new DateTime(2026, 1, 1, 8, 30, 0), 1), precision: 9);
    }

    /// <summary>ノード点では入力値がそのまま再現される</summary>
    [Fact]
    public void Read_ValuesAtNodesAreExact()
    {
      var reader = new CsvBoundaryReader();
      BoundaryInterpolator interp = reader.Read(MakeStream(SAMPLE_CSV), out _);

      Assert.Equal(0.0, interp.Interpolate(new DateTime(2026, 1, 1, 8, 0, 0), 0), precision: 9);
      Assert.Equal(50.0, interp.Interpolate(new DateTime(2026, 1, 1, 9, 0, 0), 0), precision: 9);
      Assert.Equal(500.0, interp.Interpolate(new DateTime(2026, 1, 1, 10, 0, 0), 1), precision: 9);
    }

    /// <summary>defaultMethod指定で全系列の補間法を切り替えられる</summary>
    [Fact]
    public void Read_WithDefaultMethod_AppliesToAllSeries()
    {
      var reader = new CsvBoundaryReader();
      BoundaryInterpolator interp = reader.Read(MakeStream(SAMPLE_CSV),
        BoundaryInterpolator.InterpolationMethod.StepHold, out _);

      Assert.Equal(BoundaryInterpolator.InterpolationMethod.StepHold,
        interp.GetInterpolationMethod(0));
      //StepHold: 8:30は8:00の値を保持
      Assert.Equal(0.0,
        interp.Interpolate(new DateTime(2026, 1, 1, 8, 30, 0), 0), precision: 9);
    }

    /// <summary>スペース区切りの日時形式も受け付ける</summary>
    [Fact]
    public void Read_AcceptsSpaceSeparatedDateTime()
    {
      const string csv =
        "time,value\n" +
        "2026-01-01 00:00:00,1\n" +
        "2026-01-01 06:00:00,2\n";
      var reader = new CsvBoundaryReader();
      BoundaryInterpolator interp = reader.Read(MakeStream(csv), out _);
      Assert.Equal(2, interp.NodeCount);
    }

    /// <summary>ファイルパス経由でも読み込める</summary>
    [Fact]
    public void Read_FromFilePath()
    {
      string path = Path.Combine(Path.GetTempPath(), $"boundary_{Guid.NewGuid():N}.csv");
      try
      {
        File.WriteAllText(path, SAMPLE_CSV.Replace("\n", Environment.NewLine));
        var reader = new CsvBoundaryReader();
        BoundaryInterpolator interp = reader.Read(path, out string[] names);
        Assert.Equal(2, names.Length);
        Assert.Equal(3, interp.NodeCount);
      }
      finally
      {
        if (File.Exists(path)) File.Delete(path);
      }
    }

    #endregion

    #region Sample CSV

    /// <summary>サンプルCSVはRead可能で、ヘッダ・行数が正しい</summary>
    [Fact]
    public void GetSampleCsv_IsReadable()
    {
      string csv = CsvBoundaryReader.GetSampleCsv();
      var reader = new CsvBoundaryReader();
      BoundaryInterpolator interp = reader.Read(MakeStream(csv), out string[] names);

      Assert.Equal(new[] { "occupants", "lighting", "equipment" }, names);
      Assert.Equal(3, interp.SeriesCount);
      Assert.Equal(25, interp.NodeCount);  //0時から翌0時まで1時間刻み
    }

    /// <summary>サンプルCSVの値がノード点で再現される</summary>
    [Fact]
    public void GetSampleCsv_ValuesAreExactAtNodes()
    {
      string csv = CsvBoundaryReader.GetSampleCsv();
      var reader = new CsvBoundaryReader();
      BoundaryInterpolator interp = reader.Read(MakeStream(csv), out _);

      //9:00 執務時間帯: 50人
      Assert.Equal(50.0,
        interp.Interpolate(new DateTime(2026, 1, 1, 9, 0, 0), 0), precision: 9);
      //深夜: 0人
      Assert.Equal(0.0,
        interp.Interpolate(new DateTime(2026, 1, 1, 3, 0, 0), 0), precision: 9);
    }

    /// <summary>サンプルCSVをファイルに書き出してから読み戻せる</summary>
    [Fact]
    public void GetSampleCsv_FileRoundTrip()
    {
      string path = Path.Combine(Path.GetTempPath(), $"boundary_sample_{Guid.NewGuid():N}.csv");
      try
      {
        File.WriteAllText(path, CsvBoundaryReader.GetSampleCsv());
        var reader = new CsvBoundaryReader();
        BoundaryInterpolator interp = reader.Read(path, out string[] names);
        Assert.Equal(3, names.Length);
        Assert.Equal(25, interp.NodeCount);
      }
      finally
      {
        if (File.Exists(path)) File.Delete(path);
      }
    }

    #endregion

    #region Error handling

    /// <summary>ヘッダのみ・データ行なしで例外が発生する</summary>
    [Fact]
    public void Read_NoDataRows_Throws()
    {
      var reader = new CsvBoundaryReader();
      Assert.Throws<PopoloArgumentException>(
        () => reader.Read(MakeStream("time,value\n"), out _));
    }

    /// <summary>空のストリームで例外が発生する</summary>
    [Fact]
    public void Read_EmptyStream_Throws()
    {
      var reader = new CsvBoundaryReader();
      Assert.Throws<PopoloArgumentException>(
        () => reader.Read(MakeStream("# comment only\n"), out _));
    }

    /// <summary>列数の不一致で例外が発生する</summary>
    [Fact]
    public void Read_WrongColumnCount_Throws()
    {
      const string csv =
        "time,a,b\n" +
        "2026-01-01T00:00:00,1\n";
      var reader = new CsvBoundaryReader();
      var ex = Assert.Throws<PopoloArgumentException>(
        () => reader.Read(MakeStream(csv), out _));
      Assert.Contains("Line 2", ex.Message);
    }

    /// <summary>数値として解釈できない値で例外が発生する</summary>
    [Fact]
    public void Read_InvalidNumber_Throws()
    {
      const string csv =
        "time,a\n" +
        "2026-01-01T00:00:00,abc\n";
      var reader = new CsvBoundaryReader();
      var ex = Assert.Throws<PopoloArgumentException>(
        () => reader.Read(MakeStream(csv), out _));
      Assert.Contains("'abc'", ex.Message);
    }

    /// <summary>日時として解釈できない値で例外が発生する</summary>
    [Fact]
    public void Read_InvalidDateTime_Throws()
    {
      const string csv =
        "time,a\n" +
        "not-a-time,1\n";
      var reader = new CsvBoundaryReader();
      Assert.Throws<PopoloArgumentException>(
        () => reader.Read(MakeStream(csv), out _));
    }

    /// <summary>時刻が昇順でない場合に例外が発生する</summary>
    [Fact]
    public void Read_UnsortedTimes_Throws()
    {
      const string csv =
        "time,a\n" +
        "2026-01-02T00:00:00,1\n" +
        "2026-01-01T00:00:00,2\n";
      var reader = new CsvBoundaryReader();
      Assert.Throws<PopoloArgumentException>(
        () => reader.Read(MakeStream(csv), out _));
    }

    /// <summary>系列列のないヘッダで例外が発生する</summary>
    [Fact]
    public void Read_HeaderWithoutSeries_Throws()
    {
      const string csv =
        "time\n" +
        "2026-01-01T00:00:00\n";
      var reader = new CsvBoundaryReader();
      Assert.Throws<PopoloArgumentException>(
        () => reader.Read(MakeStream(csv), out _));
    }

    /// <summary>null引数で例外が発生する</summary>
    [Fact]
    public void Read_NullArguments_Throw()
    {
      var reader = new CsvBoundaryReader();
      Assert.Throws<PopoloArgumentException>(() => reader.Read((Stream)null!, out _));
      Assert.Throws<PopoloArgumentException>(() => reader.Read("", out _));
    }

    #endregion

  }
}
