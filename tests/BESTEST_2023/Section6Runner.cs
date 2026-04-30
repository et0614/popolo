/* Section6Runner.cs
 *
 * ANSI/ASHRAE Standard 140-2023 Section 6 (Weather Drivers Tests) のランナー。
 * 6 ケース (WD100..WD600) を全実行し、Std140_WD_Output.xlsx と同じ 41 列の
 * 8760 行 CSV を 6 本生成 + Std140_WD_Output_filled.xlsx に転記する。
 * 末尾に Std140_WD_Annual_Results.xlsx と突合しやすい年平均サマリ表を出す。
 *
 * 列構成 (Std140_WD_Output.xlsx 準拠):
 *   A     : Time of Year [hr]            ... 1..8760 (区間末ラベル, 瞬時値用)
 *   B-L   : Tdb / RH / Tdp / x / Twb / Wspd / Wdir / P / TotCld / OpqCld / Tsky
 *   M     : Solar Time of Year [hr]      ... 0.5..8759.5 (区間中点, 平均値用)
 *   N-AQ  : 傾斜10面 × Total/Beam/Diffuse [Wh/m²] (1時間平均なので W/m² と同値)
 *
 * Section 6.3.1 末尾の「瞬時値はその時刻、平均値は中点時刻」規則に対応するため、
 * 太陽位置は WeatherRecord.Time (=区間始点) + 30 分 で更新する。
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using Popolo.Core.Climate;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Physics;
using Popolo.IO.Climate.Weather;

namespace BESTEST_2023
{
  /// <summary>Std 140-2023 Section 6 (Weather Drivers Tests) ランナー。</summary>
  internal static class Section6Runner
  {
    #region 型定義

    /// <summary>WD ケース定義 (Std 140-2023 Table 6-1 ～ 6-6)。</summary>
    private record WdCase(
        string Name, string WeatherFile,
        double Latitude, double Longitude,
        double TimeZone, double Elevation, double Albedo);

    private static readonly WdCase[] AllCases =
    {
      new("WD100", "WD100.epw",  39.833, -104.65,  -7.0, 1650.0, 0.0),
      new("WD200", "WD200.epw",  33.633,  -84.433, -5.0,  308.0, 0.0),
      new("WD300", "WD300.epw", -33.393,  -70.786, -4.0,  474.0, 0.0),
      new("WD400", "WD400.epw",  71.286, -156.767, -9.0,   10.0, 0.0),
      new("WD500", "WD500.epw",  28.567,   77.103, +5.5,  236.8, 0.0),
      new("WD600", "WD600.epw",  39.833, -104.65,  -7.0, 1650.0, 0.2),
    };

    /// <summary>1ケース分の年平均集計値（最終サマリ表用）。</summary>
    private struct AnnualStats
    {
      public string Name;
      public double Tdb, Rh, Tdp, X, Twb, Wsp, WdirVec, WdirArithRaw, P, TotCld, OpqCld, Tsky;
      public double[] T, B, D;
    }

    /// <summary>
    /// 1ケース分の実行結果。年平均サマリ <see cref="Stats"/> と、
    /// xlsx テンプレートに転記するための毎時 43 列バッファ <see cref="Rows"/> を持つ。
    /// </summary>
    private record WdRunResult(AnnualStats Stats, double[][] Rows);

    /// <summary>列定義（A..AQ, 計43列）。</summary>
    /// <remarks>添字: 0..42 (= Excel A..AQ)。Std140_WD_Output.xlsx と完全一致させる。</remarks>
    private const int NumCols = 43;
    private const int IdxHourEnd = 0;        // A: Time of Year (hr, integer)
    private const int IdxHumidityRatio = 4;  // E: G8 表示
    private const int IdxSolarHourMid = 12;  // M: Solar Time of Year (hr, F1)

    private static readonly string[] ColumnNames = new[]
    {
      "Time of Year",
      "Dry Bulb Temperature","Relative Humidity","Dewpoint Temperature","Humidity Ratio",
      "Wet Bulb Temperature","Windspeed","Wind Direction","Station Pressure",
      "Total Cloud Cover","Opaque Cloud Cover","Sky Temperature",
      "Solar Time of Year",
      "Total Horizontal Radiation","Beam Horizontal Radiation","Diffuse Horizontal Radiation",
      "Total Radiation on S Azimuth and 90° Slope",
      "Beam Radiation on S Azimuth and 90° Slope",
      "Diffuse Radiation on S Azimuth and 90° Slope",
      "Total Radiation on E Azimuth and 90° Slope",
      "Beam Radiation on E Azimuth and 90° Slope",
      "Diffuse Radiation on E Azimuth and 90° Slope",
      "Total Radiation on N Azimuth and 90° Slope",
      "Beam Radiation on N Azimuth and 90° Slope",
      "Diffuse Radiation on N Azimuth and 90° Slope",
      "Total Radiation on W Azimuth and 90° Slope",
      "Beam Radiation on W Azimuth and 90° Slope",
      "Diffuse Radiation on W Azimuth and 90° Slope",
      "Total Radiation on 45° E of S Azimuth and 90° Slope",
      "Beam Radiation on 45° E of S Azimuth and 90° Slope",
      "Diffuse Radiation on 45° E of S Azimuth and 90° Slope",
      "Total Radiation on 45° W of S Azimuth and 90° Slope",
      "Beam Radiation on 45° W of S Azimuth and 90° Slope",
      "Diffuse Radiation on 45° W of S Azimuth and 90° Slope",
      "Total Radiation on E Azimuth and 30° from H Slope",
      "Beam Radiation on E Azimuth and 30° from H Slope",
      "Diffuse Radiation on E Azimuth and 30° from H Slope",
      "Total Radiation on S Azimuth and 30° from H Slope",
      "Beam Radiation on S Azimuth and 30° from H Slope",
      "Diffuse Radiation on S Azimuth and 30° from H Slope",
      "Total Radiation on W Azimuth and 30° from H Slope",
      "Beam Radiation on W Azimuth and 30° from H Slope",
      "Diffuse Radiation on W Azimuth and 30° from H Slope",
    };

    private static readonly string[] ColumnUnits = new[]
    {
      "hr","C","%","C","kg moisture/kg dry air","C","m/s","degrees from North",
      "mbar","tenths of sky","tenths of sky","C","hr",
      "Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2",
      "Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2",
      "Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2","Wh/m2",
      "Wh/m2","Wh/m2","Wh/m2",
    };

    #endregion

    #region 公開エントリ

    /// <summary>Section 6 全6ケースを実行する。</summary>
    /// <param name="weatherDir">EPW/TMY3 が置かれているディレクトリ。</param>
    /// <param name="resultsDir">CSV と Std140_WD_Output_filled.xlsx の出力先。
    /// テンプレート Std140_WD_Output.xlsx もこのディレクトリにある想定。</param>
    public static void Run(string weatherDir, string resultsDir)
    {
      Directory.CreateDirectory(resultsDir);

      var allResults = new List<WdRunResult>();
      foreach (var c in AllCases)
      {
        string outCsv = Path.Combine(resultsDir, c.Name + ".csv");
        allResults.Add(RunCase(c, weatherDir, outCsv));
      }

      // Std140_WD_Output.xlsx に転記
      string templatePath = Path.Combine(resultsDir, "Std140_WD_Output.xlsx");
      string filledPath   = Path.Combine(resultsDir, "Std140_WD_Output_filled.xlsx");
      if (File.Exists(templatePath))
      {
        Console.WriteLine();
        Console.WriteLine($"Filling template -> {filledPath} ...");
        FillStd140Template(templatePath, filledPath, allResults);
      }
      else
      {
        Console.WriteLine();
        Console.WriteLine($"Template not found: {templatePath} (xlsx output skipped)");
      }

      PrintSummaryTable(allResults.Select(r => r.Stats).ToList());
    }

    #endregion

    #region 1ケース実行

    /// <summary>1ケース分: 毎時 CSV 出力 + 行バッファ蓄積 + 年平均統計。</summary>
    private static WdRunResult RunCase(WdCase c, string weatherDir, string csvPath)
    {
      Console.WriteLine($"=== {c.Name} ===");
      Console.WriteLine($"  Weather: {c.WeatherFile}, Site: {c.Latitude}°/{c.Longitude}°,"
                        + $" TZ {c.TimeZone:+0.0;-0.0;0}, Alt {c.Elevation} m, Albedo {c.Albedo}");

      var opts = new WeatherReadOptions { EstimateAtmosphericRadiation = true };
      string ext = Path.GetExtension(c.WeatherFile).ToLowerInvariant();
      IWeatherDataReader reader = ext == ".tmy3"
          ? new Tmy3WeatherReader()
          : new EpwWeatherReader();

      WeatherData wd = reader.Read(Path.Combine(weatherDir, c.WeatherFile), opts);
      if (wd.Count != 8760)
        Console.WriteLine($"  WARNING: expected 8760 hourly records, got {wd.Count}.");

      var sun = new Sun(c.Latitude, c.Longitude, c.TimeZone * 15.0);
      var surfaces = MakeBESTEST2023Surfaces();

      // 集計用バッファ
      int n = 0;
      double sumTdb = 0, sumRh = 0, sumTdp = 0, sumX = 0, sumTwb = 0;
      double sumWsp = 0, sumP = 0, sumTotCld = 0, sumOpqCld = 0, sumTsky = 0;
      double sumWdirSin = 0, sumWdirCos = 0;
      var sumT = new double[surfaces.Length];
      var sumB = new double[surfaces.Length];
      var sumD = new double[surfaces.Length];

      // 行バッファ (xlsx 転記用)
      var rows = new double[wd.Count][];

      using (var sw = new StreamWriter(csvPath, false, new UTF8Encoding(false)))
      {
        // ヘッダ 2 行
        sw.WriteLine(string.Join(',', ColumnNames));
        sw.WriteLine(string.Join(',', ColumnUnits));

        for (int i = 0; i < wd.Count; i++)
        {
          WeatherRecord r = wd.Records[i];

          // --- 瞬時値系 (col A..L) ---
          double tdb = r.DryBulbTemperature;
          double xKgKg = r.HumidityRatio * 1e-3;
          double pKpa = r.AtmosphericPressure;
          double rh = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(
              tdb, xKgKg, pKpa);
          double tdp = MoistAir.GetDewPointTemperatureFromHumidityRatio(xKgKg, pKpa);
          double twb = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
              tdb, xKgKg, pKpa);
          double wsp = r.Has(WeatherField.WindSpeed) ? r.WindSpeed : 0.0;
          double wdirDeg = r.Has(WeatherField.WindDirection)
              ? WindDirRadianSouthZeroToCompassDeg(r.WindDirection)
              : 0.0;
          double pMbar = pKpa * 10.0;
          double totCld = r.Has(WeatherField.CloudCover) ? r.CloudCover * 10.0 : 0.0;
          double opqCld = r.Has(WeatherField.OpaqueCloudCover) ? r.OpaqueCloudCover * 10.0 : 0.0;
          double tsky = r.Has(WeatherField.AtmosphericRadiation)
              ? Sky.GetSkyTemperature(r.AtmosphericRadiation)
              : double.NaN;

          // --- 平均値系 (col N..AQ): 太陽位置は区間中点で評価 ---
          DateTime midpoint = r.Time.AddMinutes(30);
          sun.Update(midpoint);
          sun.DirectNormalRadiation = r.Has(WeatherField.DirectNormalRadiation)
              ? r.DirectNormalRadiation : 0.0;
          sun.DiffuseHorizontalRadiation = r.Has(WeatherField.DiffuseHorizontalRadiation)
              ? r.DiffuseHorizontalRadiation : 0.0;
          sun.GlobalHorizontalRadiation = r.Has(WeatherField.GlobalHorizontalRadiation)
              ? r.GlobalHorizontalRadiation : 0.0;

          // 行バッファ (43 列)
          var row = new double[NumCols];
          row[0]  = i + 1;       // A: hour-end
          row[1]  = tdb;         // B
          row[2]  = rh;          // C
          row[3]  = tdp;         // D
          row[4]  = xKgKg;       // E
          row[5]  = twb;         // F
          row[6]  = wsp;         // G
          row[7]  = wdirDeg;     // H
          row[8]  = pMbar;       // I
          row[9]  = totCld;      // J
          row[10] = opqCld;      // K
          row[11] = tsky;        // L (NaN if missing)
          row[12] = i + 0.5;     // M: solar midpoint

          for (int s = 0; s < surfaces.Length; s++)
          {
            double beam = surfaces[s].Inc.GetDirectSolarIrradiance(sun);
            double diff = surfaces[s].Inc.GetDiffuseSolarIrradiance(sun, c.Albedo);
            double tot = beam + diff;
            int colBase = 13 + s * 3;       // 13, 16, 19, ..., 40
            row[colBase]     = tot;
            row[colBase + 1] = beam;
            row[colBase + 2] = diff;
            sumT[s] += tot; sumB[s] += beam; sumD[s] += diff;
          }
          rows[i] = row;

          // CSV 行の書き出し
          var sb = new StringBuilder(512);
          for (int col = 0; col < NumCols; col++)
          {
            if (col > 0) sb.Append(',');
            sb.Append(FormatCellText(row[col], col));
          }
          sw.WriteLine(sb.ToString());

          // 集計
          n++;
          sumTdb += tdb; sumRh += rh; sumTdp += tdp; sumX += xKgKg; sumTwb += twb;
          sumWsp += wsp; sumP += pMbar; sumTotCld += totCld; sumOpqCld += opqCld;
          if (!double.IsNaN(tsky)) sumTsky += tsky;
          double wRad = wdirDeg * Math.PI / 180.0;
          sumWdirSin += Math.Sin(wRad);
          sumWdirCos += Math.Cos(wRad);
        }
      }

      Console.WriteLine($"  -> {csvPath} ({n} hourly rows)");

      // 風向 (ベクトル / EPW 生値の算術)
      double wdirVecRad = Math.Atan2(sumWdirSin / n, sumWdirCos / n);
      double wdirVecDeg = wdirVecRad * 180.0 / Math.PI;
      if (wdirVecDeg < 0) wdirVecDeg += 360.0;
      double wdirArithRaw = ext == ".epw"
          ? ComputeArithmeticWindBearingFromEpw(Path.Combine(weatherDir, c.WeatherFile))
          : double.NaN;

      var stats = new AnnualStats
      {
        Name = c.Name,
        Tdb = sumTdb / n, Rh = sumRh / n, Tdp = sumTdp / n,
        X = sumX / n,     Twb = sumTwb / n,
        Wsp = sumWsp / n, WdirVec = wdirVecDeg, WdirArithRaw = wdirArithRaw,
        P = sumP / n, TotCld = sumTotCld / n, OpqCld = sumOpqCld / n, Tsky = sumTsky / n,
        T = ArrayDiv(sumT, n), B = ArrayDiv(sumB, n), D = ArrayDiv(sumD, n),
      };
      return new WdRunResult(stats, rows);
    }

    /// <summary>
    /// EPW field 20 (Wind Direction, deg from N, 0..360 inclusive) を直接
    /// 読み出し、算術平均を返す。Std140_WD_Annual_Results.xlsx の "Actual"
    /// 列と一致させるための補助計算。
    /// </summary>
    private static double ComputeArithmeticWindBearingFromEpw(string epwPath)
    {
      var ci = CultureInfo.InvariantCulture;
      double sum = 0;
      int n = 0;
      using var reader = new StreamReader(epwPath);
      for (int i = 0; i < 8; i++) reader.ReadLine();   // 8行のヘッダをスキップ
      string? line;
      while ((line = reader.ReadLine()) != null)
      {
        if (line.Length == 0) continue;
        var f = line.Split(',');
        if (f.Length < 21) continue;
        if (double.TryParse(f[20], NumberStyles.Float, ci, out double wd)
            && wd >= 0 && wd <= 360)
        {
          sum += wd;
          n++;
        }
      }
      return n > 0 ? sum / n : double.NaN;
    }

    #endregion

    #region サマリ表示

    /// <summary>
    /// 全ケースの年平均をまとめて表示。
    /// Std140_WD_Annual_Results.xlsx の "Annual Average Results" シートと
    /// 行の順序を揃えてあるので、目視突合しやすい。
    /// </summary>
    private static void PrintSummaryTable(List<AnnualStats> all)
    {
      Console.WriteLine();
      Console.WriteLine("=================================================================================================");
      Console.WriteLine("  Annual averages summary -- compare against Std140_WD_Annual_Results.xlsx (column 'Actual' / 8 ref tools)");
      Console.WriteLine("=================================================================================================");

      // ヘッダ
      Console.Write($"  {"Quantity",-44}");
      foreach (var s in all) Console.Write($" {s.Name,12}");
      Console.WriteLine();
      Console.Write(new string('-', 46 + 13 * all.Count));
      Console.WriteLine();

      Print(all, "Dry Bulb Temperature [C]",        s => s.Tdb,     "F4");
      Print(all, "Relative Humidity [%]",           s => s.Rh,      "F4");
      Print(all, "Dew Point Temperature [C]",       s => s.Tdp,     "F4");
      Print(all, "Humidity Ratio [kg/kg]",          s => s.X,       "E4");
      Print(all, "Wet Bulb Temperature [C]",        s => s.Twb,     "F4");
      Print(all, "Wind Speed [m/s]",                s => s.Wsp,     "F4");
      Print(all, "Wind Direction [deg-N, arith raw]", s => s.WdirArithRaw, "F4");
      Print(all, "Wind Direction [deg-N, vec.avg]",   s => s.WdirVec,      "F4");
      Print(all, "Station Pressure [mbar]",         s => s.P,       "F4");
      Print(all, "Total Cloud Cover [tenths]",      s => s.TotCld,  "F4");
      Print(all, "Opaque Cloud Cover [tenths]",     s => s.OpqCld,  "F4");
      Print(all, "Sky Temperature [C]",             s => s.Tsky,    "F4");

      // 傾斜面: Std140_WD_Output.xlsx の列順
      string[] sNames = {
        "Horizontal", "S 90°", "E 90°", "N 90°", "W 90°",
        "45° E of S 90°", "45° W of S 90°", "E 30°", "S 30°", "W 30°",
      };
      for (int i = 0; i < sNames.Length; i++)
      {
        int idx = i;
        Print(all, $"{sNames[i]} Total Radiation [W/m²]",   s => s.T[idx], "F4");
        Print(all, $"{sNames[i]} Beam Radiation [W/m²]",    s => s.B[idx], "F4");
        Print(all, $"{sNames[i]} Diffuse Radiation [W/m²]", s => s.D[idx], "F4");
      }
    }

    private static void Print(List<AnnualStats> all, string label,
        Func<AnnualStats, double> sel, string fmt)
    {
      Console.Write($"  {label,-44}");
      foreach (var s in all)
        Console.Write($" {sel(s).ToString(fmt, CultureInfo.InvariantCulture),12}");
      Console.WriteLine();
    }

    private static double[] ArrayDiv(double[] a, double n)
    {
      var r = new double[a.Length];
      for (int i = 0; i < a.Length; i++) r[i] = a[i] / n;
      return r;
    }

    #endregion

    #region xlsx テンプレ書込み

    /// <summary>
    /// Std140_WD_Output.xlsx テンプレート (Read Me / Program Info / WD100..WD600
    /// の8シート構成) の各 WD### シートに毎時データを行追記し、別名で保存。
    /// テンプレートの 2 行ヘッダ・列幅・他シートはそのまま保持する。
    /// </summary>
    private static void FillStd140Template(
        string templatePath, string outPath, List<WdRunResult> results)
    {
      File.Copy(templatePath, outPath, overwrite: true);
      using var doc = SpreadsheetDocument.Open(outPath, isEditable: true);
      var wbp = doc.WorkbookPart!;
      var sheets = wbp.Workbook.Descendants<Sheet>().ToList();

      foreach (var result in results)
      {
        var sheet = sheets.FirstOrDefault(s => s.Name?.Value == result.Stats.Name);
        if (sheet == null)
        {
          Console.WriteLine($"  WARN: sheet '{result.Stats.Name}' not found in template");
          continue;
        }
        var wsp = (WorksheetPart)wbp.GetPartById(sheet.Id!.Value!);
        AppendDataRows(wsp, result.Rows);
        wsp.Worksheet.Save();
        Console.WriteLine($"  {result.Stats.Name}: {result.Rows.Length} rows appended");
      }

      // 開いたとき再計算 (テンプレ側の集計式が新規データを参照する想定)
      var calc = wbp.Workbook.CalculationProperties;
      if (calc == null)
      {
        calc = new CalculationProperties();
        wbp.Workbook.AppendChild(calc);
      }
      calc.CalculationMode = new EnumValue<CalculateModeValues>(CalculateModeValues.Auto);
      calc.FullCalculationOnLoad = true;
      wbp.Workbook.Save();
    }

    /// <summary>SheetData の末尾に行追記。既存ヘッダ行 (1, 2) は保持。</summary>
    private static void AppendDataRows(WorksheetPart wsp, double[][] rows)
    {
      var ws = wsp.Worksheet;
      var sd = ws.GetFirstChild<SheetData>();
      if (sd == null)
      {
        sd = new SheetData();
        ws.AppendChild(sd);
      }

      uint startRow = sd.Elements<Row>().Any()
          ? sd.Elements<Row>().Max(r => r.RowIndex?.Value ?? 0U) + 1U
          : 1U;

      var ci = CultureInfo.InvariantCulture;
      for (int i = 0; i < rows.Length; i++)
      {
        uint rIdx = startRow + (uint)i;
        var row = new Row { RowIndex = rIdx };
        string rowLabel = rIdx.ToString(ci);
        for (int col = 0; col < rows[i].Length; col++)
        {
          double v = rows[i][col];
          if (double.IsNaN(v)) continue;          // NaN セルはスキップ → Excel 表示は空欄
          row.AppendChild(new Cell
          {
            CellReference = ColLetter(col) + rowLabel,
            CellValue = new CellValue(FormatCellText(v, col)),
            // DataType を省略 = number セル (CellValues.Number)
          });
        }
        sd.AppendChild(row);
      }
    }

    /// <summary>列番号を Excel の列ラベルに変換 (0→"A", 25→"Z", 26→"AA", 42→"AQ")。</summary>
    private static string ColLetter(int col)
    {
      string s = "";
      int n = col;
      while (true)
      {
        s = (char)('A' + n % 26) + s;
        n = n / 26 - 1;
        if (n < 0) break;
      }
      return s;
    }

    /// <summary>列ごとの数値書式 (CSV / xlsx 共通)。</summary>
    private static string FormatCellText(double v, int col)
    {
      if (double.IsNaN(v)) return "";
      var ci = CultureInfo.InvariantCulture;
      return col switch
      {
        IdxHourEnd       => ((int)Math.Round(v)).ToString(ci),
        IdxHumidityRatio => v.ToString("G8", ci),
        IdxSolarHourMid  => v.ToString("F1", ci),
        _                => v.ToString("F2", ci),
      };
    }

    #endregion

    #region 補助関数

    /// <summary>Std140-2023 Table 6-2 の10面（azimuth, tilt）。</summary>
    private static (string Name, Incline Inc)[] MakeBESTEST2023Surfaces()
    {
      double v0 = 0.0;
      double v30 = Math.PI / 6.0;
      double v90 = 0.5 * Math.PI;
      double aS = 0.0;
      double aE = -0.5 * Math.PI;
      double aW = 0.5 * Math.PI;
      double aN = Math.PI;
      double aSE45 = -0.25 * Math.PI;
      double aSW45 = 0.25 * Math.PI;

      return new (string, Incline)[]
      {
        ("Horizontal",                       new Incline(aS,    v0)),
        ("S Azimuth and 90° Slope",          new Incline(aS,    v90)),
        ("E Azimuth and 90° Slope",          new Incline(aE,    v90)),
        ("N Azimuth and 90° Slope",          new Incline(aN,    v90)),
        ("W Azimuth and 90° Slope",          new Incline(aW,    v90)),
        ("45° E of S Azimuth and 90° Slope", new Incline(aSE45, v90)),
        ("45° W of S Azimuth and 90° Slope", new Incline(aSW45, v90)),
        ("E Azimuth and 30° from H Slope",   new Incline(aE,    v30)),
        ("S Azimuth and 30° from H Slope",   new Incline(aS,    v30)),
        ("W Azimuth and 30° from H Slope",   new Incline(aW,    v30)),
      };
    }

    /// <summary>
    /// Popolo の風向規約 (south=0, east 負, west 正, rad) → 北基準・時計回り [deg]。
    /// </summary>
    private static double WindDirRadianSouthZeroToCompassDeg(double radSouthZero)
    {
      double deg = radSouthZero * 180.0 / Math.PI;
      double compass = 180.0 + deg;
      compass %= 360.0;
      if (compass < 0) compass += 360.0;
      return compass;
    }

    #endregion
  }
}
