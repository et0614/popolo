/* Section7Runner.cs
 *
 * ANSI/ASHRAE Standard 140-2023 Section 7
 * (Building Thermal Envelope and Fabric Load Tests, Class I) のランナー。
 *
 * E2-B スコープ: 既存 Buildings.TestCase で対応する 40 ケースを全実行
 *   (60-100, 195-470, 800-810, 600FF/650FF/900FF/950FF, 960, 990)
 * E2-C スコープ: 結果を Std140_TF_Output.xlsx の所定セルへ自動転記
 *   (年間負荷 r70-115, 自由温度 r130-133, 入射/透過/天空温度 (Case600) r155-178,
 *    月別負荷 (Case600/900) r190-201)
 *
 * 気象: 725650TY.csv (TMY3 形式, Std 140-2023 7.2.1.1.1.1)
 * Site : Denver, CO  39.833°N / 104.65°W / 1650 m / TZ -7  (Std 140-2023 Annex A1)
 */

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Physics;
using Popolo.IO.Climate.Weather;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BESTEST_2023
{
  /// <summary>Std 140-2023 Section 7 (Building Thermal Envelope and Fabric Load) ランナー。</summary>
  internal static class Section7Runner
  {
    #region 定数 (Std 140-2023 Annex A1, Table A1-1)

    /// <summary>サイト情報 (Sun 位置計算用)。</summary>
    private record SiteInfo(double Latitude, double Longitude, double StdLongitude, double Elevation);

    /// <summary>Std 140-2023 Annex A1 サイト (DIA, Denver Intl AP)。</summary>
    private static readonly SiteInfo Std140_2023Site
        = new SiteInfo(39.833, -104.65, -105.0, 1650.0);

    /// <summary>
    /// 旧 BESTEST DRYCOLD.TMY 用のサイト (旧 BESTEST/Program.cs 由来)。
    /// 緯度=39°08' N, 経度=104°09' W, 標準時子午線 105°W, 標高 1609 m。
    /// </summary>
    private static readonly SiteInfo LegacyBestestSite
        = new SiteInfo(39.0 + 8.0 / 60.0, -(104.0 - 9.0 / 60.0), -105.0, 1609.0);

    private const string WEATHER_FILE = "725650TY.csv";

    private static readonly string[] MonthAbbrev =
        { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    /// <summary>実行対象ケース (53 ケース、Std 140-2023 §7.2 全件)。</summary>
    private static readonly Buildings.TestCase[] AllCases = new[]
    {
      // In-depth Tests with BangBang Tstat (§7.2.3)
      Buildings.TestCase.C195, Buildings.TestCase.C200, Buildings.TestCase.C210,
      Buildings.TestCase.C215, Buildings.TestCase.C220, Buildings.TestCase.C230,
      Buildings.TestCase.C240, Buildings.TestCase.C250,
      Buildings.TestCase.C270, Buildings.TestCase.C280, Buildings.TestCase.C290,
      Buildings.TestCase.C300, Buildings.TestCase.C310, Buildings.TestCase.C320,
      // In-depth Tests with DeadBand Tstat (§7.2.3)
      Buildings.TestCase.C395, Buildings.TestCase.C400, Buildings.TestCase.C410,
      Buildings.TestCase.C420, Buildings.TestCase.C430, Buildings.TestCase.C440,
      // Constant Surface Coefficients (§7.2.3.21-23, new in 2020)
      Buildings.TestCase.C450, Buildings.TestCase.C460, Buildings.TestCase.C470,
      Buildings.TestCase.C800, Buildings.TestCase.C810,
      // Basic Tests with Windows, Low-Mass (§7.2.1, §7.2.2.1)
      Buildings.TestCase.C600, Buildings.TestCase.C610, Buildings.TestCase.C620,
      Buildings.TestCase.C630, Buildings.TestCase.C640, Buildings.TestCase.C650,
      // Low-Mass Window/Insulation/Tstat variants (new in 2020)
      Buildings.TestCase.C660, Buildings.TestCase.C670,
      Buildings.TestCase.C680, Buildings.TestCase.C685, Buildings.TestCase.C695,
      // Basic Tests with Windows, High-Mass (§7.2.2.2)
      Buildings.TestCase.C900, Buildings.TestCase.C910, Buildings.TestCase.C920,
      Buildings.TestCase.C930, Buildings.TestCase.C940, Buildings.TestCase.C950,
      // High-Mass Insulation/Tstat variants (new in 2020)
      Buildings.TestCase.C980, Buildings.TestCase.C985, Buildings.TestCase.C995,
      // SunSpace + Ground Coupling
      Buildings.TestCase.C960, Buildings.TestCase.C990,
      // Free-Float Tests (§7.2.2.3)
      Buildings.TestCase.C600FF, Buildings.TestCase.C650FF,
      Buildings.TestCase.C680FF,
      Buildings.TestCase.C900FF, Buildings.TestCase.C950FF,
      Buildings.TestCase.C980FF,
    };

    /// <summary>非自由温度ケース → Std140_TF_Output.xlsx シート"A"の年間負荷行。</summary>
    private static readonly Dictionary<Buildings.TestCase, uint> AnnualLoadRow
        = new Dictionary<Buildings.TestCase, uint>
        {
          // Low-Mass Basic
          { Buildings.TestCase.C600, 70 }, { Buildings.TestCase.C610, 71 },
          { Buildings.TestCase.C620, 72 }, { Buildings.TestCase.C630, 73 },
          { Buildings.TestCase.C640, 74 }, { Buildings.TestCase.C650, 75 },
          { Buildings.TestCase.C660, 76 }, { Buildings.TestCase.C670, 77 },
          { Buildings.TestCase.C680, 78 }, { Buildings.TestCase.C685, 79 },
          { Buildings.TestCase.C695, 80 },
          // High-Mass Basic
          { Buildings.TestCase.C900, 81 }, { Buildings.TestCase.C910, 82 },
          { Buildings.TestCase.C920, 83 }, { Buildings.TestCase.C930, 84 },
          { Buildings.TestCase.C940, 85 }, { Buildings.TestCase.C950, 86 },
          { Buildings.TestCase.C960, 87 },
          { Buildings.TestCase.C980, 88 }, { Buildings.TestCase.C985, 89 },
          { Buildings.TestCase.C995, 90 },
          // In-Depth (BangBang)
          { Buildings.TestCase.C195, 91 }, { Buildings.TestCase.C200, 92 },
          { Buildings.TestCase.C210, 93 }, { Buildings.TestCase.C215, 94 },
          { Buildings.TestCase.C220, 95 }, { Buildings.TestCase.C230, 96 },
          { Buildings.TestCase.C240, 97 }, { Buildings.TestCase.C250, 98 },
          { Buildings.TestCase.C270, 99 }, { Buildings.TestCase.C280, 100 },
          { Buildings.TestCase.C290, 101 }, { Buildings.TestCase.C300, 102 },
          { Buildings.TestCase.C310, 103 }, { Buildings.TestCase.C320, 104 },
          // In-Depth (DeadBand)
          { Buildings.TestCase.C395, 105 }, { Buildings.TestCase.C400, 106 },
          { Buildings.TestCase.C410, 107 }, { Buildings.TestCase.C420, 108 },
          { Buildings.TestCase.C430, 109 }, { Buildings.TestCase.C440, 110 },
          // Surface Coefficients
          { Buildings.TestCase.C450, 111 }, { Buildings.TestCase.C460, 112 },
          { Buildings.TestCase.C470, 113 },
          // High-Conductance Mass
          { Buildings.TestCase.C800, 114 }, { Buildings.TestCase.C810, 115 },
          // C990 はテンプレに行が無い (旧 ASHRAE 拡張)。出力はスキップ。
        };

    /// <summary>自由温度ケース → Std140_TF_Output.xlsx シート"A"の自由温度行。</summary>
    private static readonly Dictionary<Buildings.TestCase, uint> FreeFloatRow
        = new Dictionary<Buildings.TestCase, uint>
        {
          { Buildings.TestCase.C600FF, 130 },
          { Buildings.TestCase.C900FF, 131 },
          { Buildings.TestCase.C650FF, 132 },
          { Buildings.TestCase.C950FF, 133 },
          { Buildings.TestCase.C680FF, 134 },
          { Buildings.TestCase.C980FF, 135 },
        };

    /// <summary>§7.3.8 特定日毎時値: Feb 1 zone load の列マッピング (rows 262-285)。</summary>
    private static readonly Dictionary<Buildings.TestCase, string> Feb1LoadCol
        = new Dictionary<Buildings.TestCase, string>
        {
          { Buildings.TestCase.C600, "C" }, { Buildings.TestCase.C640, "D" },
          { Buildings.TestCase.C660, "E" }, { Buildings.TestCase.C670, "F" },
          { Buildings.TestCase.C680, "G" }, { Buildings.TestCase.C685, "H" },
          { Buildings.TestCase.C695, "I" }, { Buildings.TestCase.C900, "J" },
          { Buildings.TestCase.C940, "K" }, { Buildings.TestCase.C980, "L" },
          { Buildings.TestCase.C985, "M" }, { Buildings.TestCase.C995, "N" },
        };

    /// <summary>§7.3.8 特定日毎時値: Jul 14 zone load の列マッピング (rows 262-285)。</summary>
    private static readonly Dictionary<Buildings.TestCase, string> Jul14LoadCol
        = new Dictionary<Buildings.TestCase, string>
        {
          { Buildings.TestCase.C600, "O" }, { Buildings.TestCase.C660, "P" },
          { Buildings.TestCase.C670, "Q" }, { Buildings.TestCase.C680, "R" },
          { Buildings.TestCase.C685, "S" }, { Buildings.TestCase.C695, "T" },
          { Buildings.TestCase.C900, "U" }, { Buildings.TestCase.C980, "V" },
          { Buildings.TestCase.C985, "W" }, { Buildings.TestCase.C995, "X" },
        };

    /// <summary>§7.3.8 Feb 1 室内温度 (controlled, rows 262-285): 640/940 のみ。</summary>
    private static readonly Dictionary<Buildings.TestCase, string> Feb1ZoneTempCol
        = new Dictionary<Buildings.TestCase, string>
        {
          { Buildings.TestCase.C640, "Y" }, { Buildings.TestCase.C940, "Z" },
        };

    /// <summary>§7.3.8 自由温度ケースの特定日室温 (rows 294-317)。</summary>
    private static readonly Dictionary<Buildings.TestCase, (string col, bool isJul14)> FFTempDayCol
        = new Dictionary<Buildings.TestCase, (string, bool)>
        {
          { Buildings.TestCase.C600FF, ("C", false) },   // Feb 1
          { Buildings.TestCase.C900FF, ("D", false) },   // Feb 1
          { Buildings.TestCase.C650FF, ("E", true)  },   // Jul 14
          { Buildings.TestCase.C950FF, ("F", true)  },   // Jul 14
          { Buildings.TestCase.C680FF, ("G", false) },   // Feb 1
          { Buildings.TestCase.C980FF, ("H", false) },   // Feb 1
        };

    #endregion

    #region 結果型

    /// <summary>1 ケース分の集計結果。</summary>
    private class CaseResult
    {
      public Buildings.TestCase Case;
      public bool IsFreeFloat;

      // §7.3.1: 年間負荷
      public double AnnualHeating_MWh;
      public double AnnualCooling_MWh;
      public double PeakHeating_kW;       public DateTime PeakHeatingTime;
      public double PeakCooling_kW;       public DateTime PeakCoolingTime;

      // §7.3.6: 自由温度ゾーン
      public double ZoneTempMean_C;
      public double ZoneTempMin_C;        public DateTime ZoneTempMinTime;
      public double ZoneTempMax_C;        public DateTime ZoneTempMaxTime;

      // §7.3.2.3: 天空温度 (Case 600)
      public double SkyTempMean_C;
      public double SkyTempMin_C;         public DateTime SkyTempMinTime;
      public double SkyTempMax_C;         public DateTime SkyTempMaxTime;

      // §7.3.2.1: 年間入射日射 (Case 600)
      public double IncidentH_kWhm2, IncidentN_kWhm2, IncidentE_kWhm2,
                    IncidentS_kWhm2, IncidentW_kWhm2;

      // §7.3.2.2: 年間透過日射 南窓 (Case 600/660/670)
      public double TransmittedSouth_kWhm2;

      // §7.3.2.4: 月別負荷 (Case 600/900)
      public double[] MonthlyHeating_kWh = new double[12];
      public double[] MonthlyCooling_kWh = new double[12];
      public double[] MonthlyPeakHeating_kW = new double[12];
      public DateTime[] MonthlyPeakHeatingTime = new DateTime[12];
      public double[] MonthlyPeakCooling_kW = new double[12];
      public DateTime[] MonthlyPeakCoolingTime = new DateTime[12];

      // §7.3.8: 特定日毎時値 (24 時間配列、index 0=hour 1, ..., 23=hour 24)
      public double[] Feb1HeatSupply_W = new double[24];   // + heating, - cooling
      public double[] Jul14HeatSupply_W = new double[24];
      public double[] Feb1ZoneTemp_C = new double[24];
      public double[] Jul14ZoneTemp_C = new double[24];
      public double[] Feb1SkyTemp_C = new double[24];
      public double[] May4SkyTemp_C = new double[24];
      public double[] Jul14SkyTemp_C = new double[24];
      public double[] May4IncH_Wm2 = new double[24];
      public double[] May4IncS_Wm2 = new double[24];
      public double[] May4IncW_Wm2 = new double[24];
      public double[] Jul14IncH_Wm2 = new double[24];
      public double[] Jul14IncS_Wm2 = new double[24];
      public double[] Jul14IncW_Wm2 = new double[24];
      public double[] Feb1TransSouth_Wm2 = new double[24];
      public double[] May4TransSouth_Wm2 = new double[24];
      public double[] Jul14TransSouth_Wm2 = new double[24];

      // §7.3.7: Case 900FF only — 1°C 温度ビンヒストグラム (-50°C ~ +97°C, 148 bins)
      public int[] TempBinHours = new int[148];
    }

    #endregion

    #region 公開エントリ

    /// <summary>Section 7 全ケースを実行 + xlsx 転記。</summary>
    public static void Run(string weatherDir, string resultsDir)
    {
      Directory.CreateDirectory(resultsDir);

      string weatherPath = Path.Combine(weatherDir, WEATHER_FILE);
      if (!File.Exists(weatherPath))
      {
        Console.WriteLine($"Section 7 skipped: {weatherPath} not found");
        return;
      }

      var reader = new Tmy3WeatherReader();
      var opts = new WeatherReadOptions { EstimateAtmosphericRadiation = true };
      WeatherData wd = reader.Read(weatherPath, opts);
      Ground ground = Ground.FromWeatherData(wd);   // C990 用

      var site = Std140_2023Site;
      Console.WriteLine($"=== Section 7 (Building Thermal Envelope) ===");
      Console.WriteLine($"  Weather: {weatherPath}");
      Console.WriteLine($"  Site   : {site.Latitude}°/{site.Longitude}°, TZ -7.0, Alt {site.Elevation} m");
      Console.WriteLine($"  Records: {wd.Count} | Cases: {AllCases.Length}");
      Console.WriteLine();

      var allResults = new List<CaseResult>();
      foreach (var tCase in AllCases)
      {
        allResults.Add(RunCase(tCase, wd, ground, resultsDir, site));
      }

      // Std140_TF_Output.xlsx に転記
      string templatePath = Path.Combine(resultsDir, "Std140_TF_Output.xlsx");
      string filledPath   = Path.Combine(resultsDir, "Std140_TF_Output_filled.xlsx");
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

      // Std140_TF_Results.xlsx (参照8ツール集計) との自動突合
      string refPath = Path.Combine(resultsDir, "Std140_TF_Results.xlsx");
      if (File.Exists(refPath))
      {
        Console.WriteLine();
        CompareWithReference(allResults, refPath);
      }
    }

    /// <summary>
    /// 診断モード: 旧 BESTEST の DRYCOLD.TMY (TMY1) を新 Section 7 ランナーで実行。
    /// 旧版 Popolo 結果との直接比較で「移植による論理差」を切り分けるため。
    /// </summary>
    /// <param name="legacyWeatherPath">DRYCOLD.TMY の絶対 / 相対パス。</param>
    /// <param name="resultsDir">結果 CSV の出力先。</param>
    /// <param name="legacyXlsxPath">旧版結果が入った classic_bestest_result.xlsx (任意)。
    /// 指定があれば自動で col J と突合プリントする。</param>
    public static void RunWithLegacyWeather(
        string legacyWeatherPath, string resultsDir, string? legacyXlsxPath = null)
    {
      Directory.CreateDirectory(resultsDir);

      if (!File.Exists(legacyWeatherPath))
      {
        Console.WriteLine($"Diagnostic skipped: {legacyWeatherPath} not found");
        return;
      }

      // 旧版 BESTEST/Program.cs と同じオプションで読み込む
      var reader = new Tmy1WeatherReader();
      var opts = new WeatherReadOptions
      {
        Station = new WeatherStationInfo("BestestStation",
            LegacyBestestSite.Latitude, 360.0 - 104.0 + 9.0 / 60.0, LegacyBestestSite.Elevation),
        CompleteRadiationComponentsByGeometry = true,
      };
      WeatherData wd = reader.Read(legacyWeatherPath, opts);
      Ground ground = Ground.FromWeatherData(wd);

      Console.WriteLine();
      Console.WriteLine($"=== Section 7 [LEGACY WEATHER DIAGNOSTIC] ===");
      Console.WriteLine($"  Weather: {legacyWeatherPath} (TMY1, DRYCOLD)");
      Console.WriteLine($"  Site   : {LegacyBestestSite.Latitude:F4}°N / {LegacyBestestSite.Longitude:F4}°E,"
                        + $" Alt {LegacyBestestSite.Elevation} m  (旧 BESTEST 由来)");
      Console.WriteLine($"  Records: {wd.Count}");
      Console.WriteLine();

      // 旧 BESTEST にあったケースのみ実行 (新 2023 ケースは旧結果との比較不可)
      var legacyCases = new[]
      {
        Buildings.TestCase.C195, Buildings.TestCase.C200, Buildings.TestCase.C210,
        Buildings.TestCase.C215, Buildings.TestCase.C220, Buildings.TestCase.C230,
        Buildings.TestCase.C240, Buildings.TestCase.C250,
        Buildings.TestCase.C270, Buildings.TestCase.C280, Buildings.TestCase.C290,
        Buildings.TestCase.C300, Buildings.TestCase.C310, Buildings.TestCase.C320,
        Buildings.TestCase.C395, Buildings.TestCase.C400, Buildings.TestCase.C410,
        Buildings.TestCase.C420, Buildings.TestCase.C430, Buildings.TestCase.C440,
        Buildings.TestCase.C600, Buildings.TestCase.C610, Buildings.TestCase.C620,
        Buildings.TestCase.C630, Buildings.TestCase.C640, Buildings.TestCase.C650,
        Buildings.TestCase.C800, Buildings.TestCase.C810,
        Buildings.TestCase.C900, Buildings.TestCase.C910, Buildings.TestCase.C920,
        Buildings.TestCase.C930, Buildings.TestCase.C940, Buildings.TestCase.C950,
        Buildings.TestCase.C960, Buildings.TestCase.C990,
        Buildings.TestCase.C600FF, Buildings.TestCase.C650FF,
        Buildings.TestCase.C900FF, Buildings.TestCase.C950FF,
      };

      var allResults = new List<CaseResult>();
      foreach (var tCase in legacyCases)
        allResults.Add(RunCase(tCase, wd, ground, resultsDir, LegacyBestestSite));

      // CSV 出力 (旧版結果との目視比較用)
      string outCsv = Path.Combine(resultsDir, "TF_LegacyWeatherDiagnostic.csv");
      using (var sw = new StreamWriter(outCsv, false, new UTF8Encoding(false)))
      {
        sw.WriteLine("Case,Heat_MWh,Cool_MWh,PeakHeat_kW,PeakCool_kW,Tmean_C");
        foreach (var r in allResults)
        {
          sw.WriteLine($"{r.Case},{r.AnnualHeating_MWh:F4},{r.AnnualCooling_MWh:F4},"
              + $"{r.PeakHeating_kW:F3},{r.PeakCooling_kW:F3},{r.ZoneTempMean_C:F2}");
        }
      }
      Console.WriteLine($"  -> {outCsv}");

      // classic_bestest_result.xlsx との自動突合
      if (legacyXlsxPath != null && File.Exists(legacyXlsxPath))
      {
        Console.WriteLine();
        CompareWithLegacyResults(allResults, legacyXlsxPath);
      }
    }

    #endregion

    #region 1ケース実行

    /// <summary>1 ケース分: 8760 時間シミュレーション + 集計値プリント + 毎時 CSV。</summary>
    private static CaseResult RunCase(
        Buildings.TestCase tCase, WeatherData wd, Ground ground, string resultsDir,
        SiteInfo site)
    {
      string caseName = tCase.ToString();

      // 制御モード
      bool isBangBang   = (tCase & Buildings.TestCase.ControlBangBang) == tCase;
      bool isDeadBand   = (tCase & Buildings.TestCase.ControlDeadBand) == tCase;
      bool isSetBack    = (tCase & Buildings.TestCase.ControlSetBack)  == tCase;
      bool isVenting    = (tCase & Buildings.TestCase.ControlVenting) == tCase;
      bool isFreeFloat  = (tCase & Buildings.TestCase.ControlNone)    == tCase;
      bool isTight20    = (tCase & Buildings.TestCase.ControlTight20) == tCase;
      bool isC650Style  = tCase == Buildings.TestCase.C650 || tCase == Buildings.TestCase.C950;
      bool isC960       = tCase == Buildings.TestCase.C960;
      bool isC990       = tCase == Buildings.TestCase.C990;
      // ConstIntCoeffs (C450/C460): 室内側 h を仕様固定値とするため per-hour 切替を無効化
      // ConstExtCoeffs (C450/C470): 屋外側 h を仕様固定値とするため動的更新軸を無効化
      bool isConstIntCoeffs = (tCase & Buildings.TestCase.ConstIntCoeffs) == tCase;
      bool isConstExtCoeffs = (tCase & Buildings.TestCase.ConstExtCoeffs) == tCase;

      // 建物作成
      Buildings.MakeBuilding(tCase, out MultiRoom mRoom, out Zone[] zones,
                             out Wall[] walls, out Window[] windows);
      var bModel = new BuildingThermalModel(new[] { mRoom });
      // 動的表面熱伝達係数 (Std 140-2023 §7.2.1.9.3 (b) / §7.2.1.10.3 (b) 経路)
      //  - 室内側 h_r: 表面温度の面積加重平均で再線形化
      //  - 屋外側 h_r: 外気温で再線形化
      //  - 屋外側 h_c: 風速依存 (MoWiTT)
      bModel.DynamicIndoorRadiativeCoefficient = !isConstIntCoeffs;
      bModel.DynamicOutdoorRadiativeCoefficient = !isConstExtCoeffs;
      bModel.DynamicOutdoorConvectiveCoefficient = !isConstExtCoeffs;
      var sun = new Sun(site.Latitude, site.Longitude, site.StdLongitude);

      // 5 面のサーフェス (§7.3.2.1, Case 600 用)
      var surfH = new Incline(0.0,             0.0);
      var surfN = new Incline(Math.PI,         0.5 * Math.PI);
      var surfE = new Incline(-0.5 * Math.PI,  0.5 * Math.PI);
      var surfS = new Incline(0.0,             0.5 * Math.PI);
      var surfW = new Incline( 0.5 * Math.PI,  0.5 * Math.PI);

      // 集計用バッファ
      double sumHeating_W = 0, sumCooling_W = 0;
      double peakHeating_W = 0, peakCooling_W = 0;
      DateTime peakHeatingTime = default, peakCoolingTime = default;
      double sumZoneTemp = 0;
      double minZoneTemp = double.MaxValue, maxZoneTemp = double.MinValue;
      DateTime minZoneTime = default, maxZoneTime = default;
      double sumSky = 0;
      double minSky = double.MaxValue, maxSky = double.MinValue;
      DateTime minSkyTime = default, maxSkyTime = default;
      int skyCount = 0;
      double sumIncH = 0, sumIncN = 0, sumIncE = 0, sumIncS = 0, sumIncW = 0;
      double sumTransSouth = 0;
      var sumMonH = new double[12];
      var sumMonC = new double[12];
      var peakMonH = new double[12];
      var peakMonC = new double[12];
      var peakMonHTime = new DateTime[12];
      var peakMonCTime = new DateTime[12];

      // 特定日毎時値 (§7.3.8)
      var feb1HS  = new double[24]; var jul14HS = new double[24];
      var feb1ZT  = new double[24]; var jul14ZT = new double[24];
      var feb1Sky = new double[24]; var may4Sky = new double[24]; var jul14Sky = new double[24];
      var may4IH  = new double[24]; var may4IS  = new double[24]; var may4IW = new double[24];
      var jul14IH = new double[24]; var jul14IS = new double[24]; var jul14IW = new double[24];
      var feb1Tr  = new double[24]; var may4Tr  = new double[24]; var jul14Tr = new double[24];
      // Case 900FF 温度ビン (§7.3.7)
      var tempBin = new int[148];

      // 毎時 CSV
      string csvPath = Path.Combine(resultsDir, $"TF_{caseName}.csv");
      var ci = CultureInfo.InvariantCulture;
      using (var sw = new StreamWriter(csvPath, false, new UTF8Encoding(false)))
      {
        sw.WriteLine("Hour,DateTimeEnd,DryBulb_C,Tzone_C,HeatSupply_W,Heating_kW,Cooling_kW,Tsky_C,IncH,IncN,IncE,IncS,IncW,TransSouth");

        // ========================================================================
        // Warmup (周期定常): Std140-2023 §5.1.1.7.2 に従い Jan 1 hour 1 から記録開始する前に
        // preconditioning を実施。先頭 24 時間 (Jan 1 の 1 日サイクル) を実気象で N 日反復して
        // 壁体内部温度を実シミュレーション初日の周期定常状態へ収束させる。
        // 同 §の informative 注記: 「初期化は年間ピーク暖房・1月暖房負荷に最も影響する」。
        // ========================================================================
        const int WARMUP_DAYS = 7;
        int hoursPerDay = Math.Min(24, wd.Count);
        for (int day = 0; day < WARMUP_DAYS; day++)
        {
          for (int h = 0; h < hoursPerDay; h++)
          {
            WeatherRecord recH = EnrichRecord(wd.Records[h]);
            DateTime simTimeH = recH.Time.AddMinutes(30);
            double iDnH = recH.Has(WeatherField.DirectNormalRadiation)     ? recH.DirectNormalRadiation     : 0.0;
            double iHolH= recH.Has(WeatherField.GlobalHorizontalRadiation) ? recH.GlobalHorizontalRadiation : 0.0;
            double iSkyH= recH.Has(WeatherField.DiffuseHorizontalRadiation)? recH.DiffuseHorizontalRadiation: 0.0;
            bModel.UpdateOutdoorCondition(simTimeH, sun, recH);

            if (isC990)
            {
              double gdbt1H = ground.GetTemperature(simTimeH.DayOfYear, 0.675);
              double gdbt2H = ground.GetTemperature(simTimeH.DayOfYear, 1.350);
              bModel.SetGroundTemperature(0, 0, true, gdbt2H);
              bModel.SetGroundTemperature(0, 3, true, gdbt1H);
              bModel.SetGroundTemperature(0, 5, true, gdbt1H);
              bModel.SetGroundTemperature(0, 7, true, gdbt1H);
              bModel.SetGroundTemperature(0, 8, true, gdbt1H);
            }

            if (isVenting)
            {
              if (simTimeH.Hour < 7 || 18 <= simTimeH.Hour)
                bModel.SetVentilationRate(0, 0,
                    (mRoom.Zones[0].AirMass + 1400 * Buildings.AIR_DNS) / 3600.0);
              else
                bModel.SetVentilationRate(0, 0, mRoom.Zones[0].AirMass * 0.5 / 3600.0);
            }

            sun.Update(simTimeH);
            DateTime sRiseH = sun.GetSunRiseTime();
            DateTime sSetH  = sun.GetSunSetTime();
            if (sRiseH.Hour == sun.CurrentDateTime.Hour)
              sun.Update(simTimeH.AddMinutes(0.5 * sRiseH.Minute));
            else if (sSetH.Hour == sun.CurrentDateTime.Hour)
              sun.Update(simTimeH.AddMinutes(-30 + 0.5 * sSetH.Minute));
            sun.DirectNormalRadiation     = iDnH;
            sun.DiffuseHorizontalRadiation= iSkyH;
            sun.GlobalHorizontalRadiation = iHolH;

            // 自然室温を予測してから本番と同じ制御ロジックを適用 (FreeFloat は何もしない)
            bModel.ControlHeatSupply(0, 0, 0);
            bModel.ForecastHeatTransfer();
            if      (isBangBang) bModel.ControlDryBulbTemperature(0, 0, 20);
            else if (isTight20)  bModel.ControlDryBulbTemperature(0, 0, 20);
            else if (isDeadBand)
            {
              if      (zones[0].Temperature < 20) bModel.ControlDryBulbTemperature(0, 0, 20);
              else if (zones[0].Temperature > 27) bModel.ControlDryBulbTemperature(0, 0, 27);
            }
            else if (isSetBack)
            {
              if (zones[0].Temperature > 27) bModel.ControlDryBulbTemperature(0, 0, 27);
              else if ((7 <= simTimeH.Hour && simTimeH.Hour < 23) && zones[0].Temperature < 20)
                bModel.ControlDryBulbTemperature(0, 0, 20);
              else if (zones[0].Temperature < 10)
                bModel.ControlDryBulbTemperature(0, 0, 10);
            }
            else if (isC650Style)
            {
              if ((7 <= simTimeH.Hour && simTimeH.Hour < 18) && zones[0].Temperature > 27)
                bModel.ControlDryBulbTemperature(0, 0, 27);
            }
            bModel.ForecastHeatTransfer();
            bModel.FixState();
          }
        }

        for (int i = 0; i < wd.Count; i++)
        {
          WeatherRecord rec = EnrichRecord(wd.Records[i]);
          DateTime simTime = rec.Time.AddMinutes(30);  // 中点
          double dbt = rec.DryBulbTemperature;
          double iDn = rec.Has(WeatherField.DirectNormalRadiation)     ? rec.DirectNormalRadiation     : 0.0;
          double iHol= rec.Has(WeatherField.GlobalHorizontalRadiation) ? rec.GlobalHorizontalRadiation : 0.0;
          double iSky= rec.Has(WeatherField.DiffuseHorizontalRadiation)? rec.DiffuseHorizontalRadiation: 0.0;

          bModel.UpdateOutdoorCondition(simTime, sun, rec);

          // 地中温度: C990 のみ Kusuda モデルで深さ別に設定。
          // 非 C990 (Cases 195/600 系) は raised floor = 外気温 (Std140-2023 §7.2.1.5.1) で
          // 床は SetOutsideWall 化されているので地面温度設定は不要。
          if (isC990)
          {
            double gdbt1 = ground.GetTemperature(simTime.DayOfYear, 0.675);
            double gdbt2 = ground.GetTemperature(simTime.DayOfYear, 1.350);
            bModel.SetGroundTemperature(0, 0, true, gdbt2);   // 床
            bModel.SetGroundTemperature(0, 3, true, gdbt1);   // 北壁地下
            bModel.SetGroundTemperature(0, 5, true, gdbt1);   // 東壁地下
            bModel.SetGroundTemperature(0, 7, true, gdbt1);   // 西壁地下
            bModel.SetGroundTemperature(0, 8, true, gdbt1);   // 南壁地下
          }

          // 換気量制御 (Venting ケース 650/950/650FF/950FF: 夜間ブースト, 7-18 時は通常)
          if (isVenting)
          {
            if (simTime.Hour < 7 || 18 <= simTime.Hour)
              bModel.SetVentilationRate(0, 0,
                  (mRoom.Zones[0].AirMass + 1400 * Buildings.AIR_DNS) / 3600.0);
            else
              bModel.SetVentilationRate(0, 0, mRoom.Zones[0].AirMass * 0.5 / 3600.0);
          }

          // 太陽位置
          sun.Update(simTime);
          DateTime sRise = sun.GetSunRiseTime();
          DateTime sSet  = sun.GetSunSetTime();
          if (sRise.Hour == sun.CurrentDateTime.Hour)
            sun.Update(simTime.AddMinutes(0.5 * sRise.Minute));
          else if (sSet.Hour == sun.CurrentDateTime.Hour)
            sun.Update(simTime.AddMinutes(-30 + 0.5 * sSet.Minute));
          sun.DirectNormalRadiation     = iDn;
          sun.DiffuseHorizontalRadiation= iSky;
          sun.GlobalHorizontalRadiation = iHol;

          // 5 面入射 (Case 600 でのみ最終出力に使用するが、全ケースで計算しておく)
          double incH = surfH.GetSolarIrradiance(sun, mRoom.Albedo);
          double incN = surfN.GetSolarIrradiance(sun, mRoom.Albedo);
          double incE = surfE.GetSolarIrradiance(sun, mRoom.Albedo);
          double incS = surfS.GetSolarIrradiance(sun, mRoom.Albedo);
          double incW = surfW.GetSolarIrradiance(sun, mRoom.Albedo);
          sumIncH += incH; sumIncN += incN; sumIncE += incE; sumIncS += incS; sumIncW += incW;

          // 透過日射 (南窓を持つケースのみ)
          // 注: bModel.ForecastHeatTransfer() より前に計算するため、ここで明示的に
          // 窓の入射角依存光学特性を最新化しておく (= 直達透過率 DirectSolarIncidentTransmittance を
          // 現在の太陽位置に対応した値にする)。これを忘れると前ステップ末の値が使われ、
          // 日の出直後の hour で前ステップ「太陽地平線下」の T=0 が掛かり、直達透過が消失する。
          double transS = 0;
          if (windows.Length > 0)
          {
            windows[0].UpdateOpticalProperties(sun);
            double directIrr  = windows[0].OutsideIncline.GetDirectSolarIrradiance(sun);
            double diffuseIrr = windows[0].OutsideIncline.GetDiffuseSolarIrradiance(sun, mRoom.Albedo);
            double shadeFactor = windows[0].SunShade != null
                ? (1.0 - windows[0].SunShade.GetShadowRatio(sun)) : 1.0;
            transS = directIrr  * windows[0].DirectSolarIncidentTransmittance  * shadeFactor
                   + diffuseIrr * windows[0].DiffuseSolarIncidentTransmittance;
          }
          sumTransSouth += transS;

          // (Warmup はメインループ前に 24h サイクル × 7 日反復で実施済み)

          // 室内側対流熱伝達率を熱流向きで切替
          // Cases 450/460 (ConstIntCoeffs): MakeBuilding で一定値に設定済み、毎時更新しない
          /*const double kcLow  = 6.13 - 5.13;
          const double kcHigh = 9.26 - 5.13;
          if (isConstIntCoeffs) {  }
          else if (isC960)
          {
            // 床1・2 (ストラティフィケーション: 床面が低温なら滞留)
            walls[0].ConvectiveCoefficientB =
                walls[0].SurfaceTemperatureB < zones[0].Temperature ? kcLow : kcHigh;
            walls[1].ConvectiveCoefficientB =
                walls[1].SurfaceTemperatureB < zones[1].Temperature ? kcLow : kcHigh;
            // 屋根1・2 (屋根面が低温なら下降流: kcHigh)
            walls[2].ConvectiveCoefficientB =
                walls[2].SurfaceTemperatureB < zones[0].Temperature ? kcHigh : kcLow;
            walls[3].ConvectiveCoefficientB =
                walls[3].SurfaceTemperatureB < zones[1].Temperature ? kcHigh : kcLow;
          }
          else
          {
            walls[0].ConvectiveCoefficientB =
                walls[0].SurfaceTemperatureB < zones[0].Temperature ? kcLow : kcHigh;
            walls[1].ConvectiveCoefficientB =
                walls[1].SurfaceTemperatureB < zones[0].Temperature ? kcHigh : kcLow;
          }*/

          // 自然室温の予測
          bModel.ControlHeatSupply(0, 0, 0);
          bModel.ForecastHeatTransfer();

          // 制御適用
          if      (isBangBang) bModel.ControlDryBulbTemperature(0, 0, 20);
          else if (isTight20)  bModel.ControlDryBulbTemperature(0, 0, 20);  // "20,20" Tstat
          else if (isDeadBand)
          {
            if      (zones[0].Temperature < 20) bModel.ControlDryBulbTemperature(0, 0, 20);
            else if (zones[0].Temperature > 27) bModel.ControlDryBulbTemperature(0, 0, 27);
          }
          else if (isSetBack)
          {
            if (zones[0].Temperature > 27) bModel.ControlDryBulbTemperature(0, 0, 27);
            else if ((7 <= simTime.Hour && simTime.Hour < 23) && zones[0].Temperature < 20)
              bModel.ControlDryBulbTemperature(0, 0, 20);
            else if (zones[0].Temperature < 10)
              bModel.ControlDryBulbTemperature(0, 0, 10);
          }
          else if (isC650Style)
          {
            // C650/C950: 7:00-18:00 のみ 27°C 上限制御 (冷房); 夜間は自然冷却 (Venting と組合せ)
            if ((7 <= simTime.Hour && simTime.Hour < 18) && zones[0].Temperature > 27)
              bModel.ControlDryBulbTemperature(0, 0, 27);
          }
          // isFreeFloat: 何もしない (HeatSupply=0 のまま自然冷却)

          // 状態確定
          bModel.ForecastHeatTransfer();
          bModel.FixState();

          // 集計
          double heatSupply_W = zones[0].HeatSupply;
          double tZone        = zones[0].Temperature;
          double heating_W = Math.Max(0,  heatSupply_W);
          double cooling_W = Math.Max(0, -heatSupply_W);
          DateTime hourEnd = rec.Time.AddHours(1);

          sumHeating_W += heating_W; sumCooling_W += cooling_W;
          if (heating_W > peakHeating_W) { peakHeating_W = heating_W; peakHeatingTime = hourEnd; }
          if (cooling_W > peakCooling_W) { peakCooling_W = cooling_W; peakCoolingTime = hourEnd; }
          sumZoneTemp += tZone;
          if (tZone < minZoneTemp) { minZoneTemp = tZone; minZoneTime = hourEnd; }
          if (tZone > maxZoneTemp) { maxZoneTemp = tZone; maxZoneTime = hourEnd; }

          int monthIdx = rec.Time.Month - 1;
          sumMonH[monthIdx] += heating_W; sumMonC[monthIdx] += cooling_W;
          if (heating_W > peakMonH[monthIdx]) { peakMonH[monthIdx] = heating_W; peakMonHTime[monthIdx] = hourEnd; }
          if (cooling_W > peakMonC[monthIdx]) { peakMonC[monthIdx] = cooling_W; peakMonCTime[monthIdx] = hourEnd; }

          double tsky = double.NaN;
          if (rec.Has(WeatherField.AtmosphericRadiation))
          {
            tsky = Sky.GetSkyTemperature(rec.AtmosphericRadiation);
            sumSky += tsky; skyCount++;
            if (tsky < minSky) { minSky = tsky; minSkyTime = hourEnd; }
            if (tsky > maxSky) { maxSky = tsky; maxSkyTime = hourEnd; }
          }

          // §7.3.8 特定日毎時値の捕捉
          // BESTEST 慣習: hour 1 (00:00-01:00) → rec.Time.Hour=0 → idx 0
          //               hour 24 (23:00-24:00) → rec.Time.Hour=23 → idx 23
          int hourIdx = rec.Time.Hour;
          int month = rec.Time.Month;
          int day = rec.Time.Day;
          if (month == 2 && day == 1)
          {
            feb1HS[hourIdx]  = heatSupply_W;
            feb1ZT[hourIdx]  = tZone;
            feb1Sky[hourIdx] = tsky;
            feb1Tr[hourIdx]  = transS;
          }
          else if (month == 5 && day == 4)
          {
            may4Sky[hourIdx] = tsky;
            may4Tr[hourIdx]  = transS;
            may4IH[hourIdx]  = incH;
            may4IS[hourIdx]  = incS;
            may4IW[hourIdx]  = incW;
          }
          else if (month == 7 && day == 14)
          {
            jul14HS[hourIdx]  = heatSupply_W;
            jul14ZT[hourIdx]  = tZone;
            jul14Sky[hourIdx] = tsky;
            jul14Tr[hourIdx]  = transS;
            jul14IH[hourIdx]  = incH;
            jul14IS[hourIdx]  = incS;
            jul14IW[hourIdx]  = incW;
          }

          // §7.3.7 Case 900FF 温度ビンヒストグラム (-50 ~ +97)
          if (tCase == Buildings.TestCase.C900FF)
          {
            int binIdx = (int)Math.Floor(tZone) + 50;
            if (binIdx < 0) binIdx = 0;
            else if (binIdx > 147) binIdx = 147;
            tempBin[binIdx]++;
          }

          // CSV 行
          sw.Write((i + 1).ToString(ci)); sw.Write(',');
          sw.Write(hourEnd.ToString("yyyy-MM-dd HH:mm", ci)); sw.Write(',');
          sw.Write(dbt.ToString("F2", ci)); sw.Write(',');
          sw.Write(tZone.ToString("F2", ci)); sw.Write(',');
          sw.Write(heatSupply_W.ToString("F2", ci)); sw.Write(',');
          sw.Write((heating_W * 1e-3).ToString("F4", ci)); sw.Write(',');
          sw.Write((cooling_W * 1e-3).ToString("F4", ci)); sw.Write(',');
          sw.Write(double.IsNaN(tsky) ? "" : tsky.ToString("F2", ci)); sw.Write(',');
          sw.Write(incH.ToString("F2", ci)); sw.Write(',');
          sw.Write(incN.ToString("F2", ci)); sw.Write(',');
          sw.Write(incE.ToString("F2", ci)); sw.Write(',');
          sw.Write(incS.ToString("F2", ci)); sw.Write(',');
          sw.Write(incW.ToString("F2", ci)); sw.Write(',');
          sw.Write(transS.ToString("F2", ci)); sw.WriteLine();
        }
      }

      // 結果オブジェクト
      var result = new CaseResult { Case = tCase, IsFreeFloat = isFreeFloat };
      result.AnnualHeating_MWh = sumHeating_W / 1e6;
      result.AnnualCooling_MWh = sumCooling_W / 1e6;
      result.PeakHeating_kW = peakHeating_W / 1000.0; result.PeakHeatingTime = peakHeatingTime;
      result.PeakCooling_kW = peakCooling_W / 1000.0; result.PeakCoolingTime = peakCoolingTime;
      result.ZoneTempMean_C = sumZoneTemp / wd.Count;
      result.ZoneTempMin_C = minZoneTemp; result.ZoneTempMinTime = minZoneTime;
      result.ZoneTempMax_C = maxZoneTemp; result.ZoneTempMaxTime = maxZoneTime;
      result.IncidentH_kWhm2 = sumIncH / 1000.0;
      result.IncidentN_kWhm2 = sumIncN / 1000.0;
      result.IncidentE_kWhm2 = sumIncE / 1000.0;
      result.IncidentS_kWhm2 = sumIncS / 1000.0;
      result.IncidentW_kWhm2 = sumIncW / 1000.0;
      result.TransmittedSouth_kWhm2 = sumTransSouth / 1000.0;
      if (skyCount > 0)
      {
        result.SkyTempMean_C = sumSky / skyCount;
        result.SkyTempMin_C  = minSky; result.SkyTempMinTime = minSkyTime;
        result.SkyTempMax_C  = maxSky; result.SkyTempMaxTime = maxSkyTime;
      }
      for (int m = 0; m < 12; m++)
      {
        result.MonthlyHeating_kWh[m] = sumMonH[m] / 1000.0;
        result.MonthlyCooling_kWh[m] = sumMonC[m] / 1000.0;
        result.MonthlyPeakHeating_kW[m] = peakMonH[m] / 1000.0;
        result.MonthlyPeakHeatingTime[m] = peakMonHTime[m];
        result.MonthlyPeakCooling_kW[m] = peakMonC[m] / 1000.0;
        result.MonthlyPeakCoolingTime[m] = peakMonCTime[m];
      }
      // 特定日毎時値・温度ビン
      Array.Copy(feb1HS,  result.Feb1HeatSupply_W,  24);
      Array.Copy(jul14HS, result.Jul14HeatSupply_W, 24);
      Array.Copy(feb1ZT,  result.Feb1ZoneTemp_C,    24);
      Array.Copy(jul14ZT, result.Jul14ZoneTemp_C,   24);
      Array.Copy(feb1Sky, result.Feb1SkyTemp_C,     24);
      Array.Copy(may4Sky, result.May4SkyTemp_C,     24);
      Array.Copy(jul14Sky,result.Jul14SkyTemp_C,    24);
      Array.Copy(may4IH,  result.May4IncH_Wm2,      24);
      Array.Copy(may4IS,  result.May4IncS_Wm2,      24);
      Array.Copy(may4IW,  result.May4IncW_Wm2,      24);
      Array.Copy(jul14IH, result.Jul14IncH_Wm2,     24);
      Array.Copy(jul14IS, result.Jul14IncS_Wm2,     24);
      Array.Copy(jul14IW, result.Jul14IncW_Wm2,     24);
      Array.Copy(feb1Tr,  result.Feb1TransSouth_Wm2,24);
      Array.Copy(may4Tr,  result.May4TransSouth_Wm2,24);
      Array.Copy(jul14Tr, result.Jul14TransSouth_Wm2,24);
      Array.Copy(tempBin, result.TempBinHours,      148);

      // コンソール 1 行サマリ (FF と非 FF で書式を変える)
      if (isFreeFloat)
      {
        Console.WriteLine($"  {caseName,-7} (FF)  Tmean={result.ZoneTempMean_C,6:F2}°C  "
                          + $"Tmin={result.ZoneTempMin_C,6:F2}°C @{FormatHourEnd(result.ZoneTempMinTime),-13}  "
                          + $"Tmax={result.ZoneTempMax_C,6:F2}°C @{FormatHourEnd(result.ZoneTempMaxTime)}");
      }
      else
      {
        Console.WriteLine($"  {caseName,-7}       H={result.AnnualHeating_MWh,7:F3} MWh  "
                          + $"C={result.AnnualCooling_MWh,7:F3} MWh  "
                          + $"pH={result.PeakHeating_kW,5:F2} kW @{FormatHourEnd(result.PeakHeatingTime),-13}  "
                          + $"pC={result.PeakCooling_kW,5:F2} kW @{FormatHourEnd(result.PeakCoolingTime)}");
      }

      return result;
    }

    /// <summary>
    /// レコードに <see cref="WeatherField.AtmosphericRadiation"/> が無い場合 (TMY1 など)、
    /// 雲量と水蒸気圧から夜間放射量を推定して <c>R_atm = σ·T_air⁴ − NR</c> を補完する。
    /// <see cref="Buildings.NO_NOC_RAD"/> が <c>true</c> のときは <c>NR=0</c> 相当の <c>R_atm = σ·T_air⁴</c> を入れる。
    /// それ以外のフィールドは元レコードからそのまま転記される (将来 WeatherRecord に
    /// 追加されたフィールドも自動的に伝搬される)。
    /// </summary>
    private static WeatherRecord EnrichRecord(WeatherRecord rec)
    {
      if (!Buildings.NO_NOC_RAD && rec.Has(WeatherField.AtmosphericRadiation)) return rec;

      double dbt = rec.DryBulbTemperature;
      double Tk = PhysicsConstants.ToKelvin(dbt);
      double sigmaT4 = PhysicsConstants.StefanBoltzmannConstant * Tk * Tk * Tk * Tk;
      double rAtm;
      if (Buildings.NO_NOC_RAD)
      {
        rAtm = sigmaT4;  // NR = 0
      }
      else
      {
        double ahd = rec.HumidityRatio * 1e-3;
        double wvp = MoistAir.GetWaterVaporPartialPressureFromHumidityRatio(ahd, rec.AtmosphericPressure);
        double nr = Sky.GetNocturnalRadiation(dbt, (int)(10 * rec.CloudCover), wvp);
        rAtm = sigmaT4 - nr;
      }

      var b = new WeatherRecordBuilder()
          .SetTime(rec.Time)
          .SetSourceTime(rec.SourceTime)
          .SetDryBulbTemperature(rec.DryBulbTemperature)
          .SetHumidityRatio(rec.HumidityRatio)
          .SetAtmosphericPressure(rec.AtmosphericPressure)
          .SetAtmosphericRadiation(rAtm)
          .MarkEstimated(WeatherField.AtmosphericRadiation);
      if (rec.Has(WeatherField.GlobalHorizontalRadiation))  b.SetGlobalHorizontalRadiation(rec.GlobalHorizontalRadiation);
      if (rec.Has(WeatherField.DirectNormalRadiation))      b.SetDirectNormalRadiation(rec.DirectNormalRadiation);
      if (rec.Has(WeatherField.DiffuseHorizontalRadiation)) b.SetDiffuseHorizontalRadiation(rec.DiffuseHorizontalRadiation);
      if (rec.Has(WeatherField.WindSpeed))                  b.SetWindSpeed(rec.WindSpeed);
      if (rec.Has(WeatherField.WindDirection))              b.SetWindDirection(rec.WindDirection);
      if (rec.Has(WeatherField.Precipitation))              b.SetPrecipitation(rec.Precipitation);
      if (rec.Has(WeatherField.CloudCover))                 b.SetCloudCover(rec.CloudCover);
      if (rec.Has(WeatherField.OpaqueCloudCover))           b.SetOpaqueCloudCover(rec.OpaqueCloudCover);
      if (rec.Has(WeatherField.CeilingHeight))              b.SetCeilingHeight(rec.CeilingHeight);
      return b.ToRecord();
    }

    private static string FormatHourEnd(DateTime hourEnd)
    {
      if (hourEnd == default) return "—";
      DateTime baseDay = hourEnd.Hour == 0 && hourEnd.Minute == 0 ? hourEnd.AddSeconds(-1) : hourEnd;
      int hourOfDay = hourEnd.Hour == 0 ? 24 : hourEnd.Hour;
      return string.Format(CultureInfo.InvariantCulture, "{0:MMM dd} h{1:00}", baseDay, hourOfDay);
    }

    private static (string month, int day, int hour) DecomposeHourEnd(DateTime hourEnd)
    {
      DateTime baseDay = hourEnd.Hour == 0 && hourEnd.Minute == 0 ? hourEnd.AddSeconds(-1) : hourEnd;
      int hourOfDay = hourEnd.Hour == 0 ? 24 : hourEnd.Hour;
      return (MonthAbbrev[baseDay.Month - 1], baseDay.Day, hourOfDay);
    }

    #endregion

    #region xlsx テンプレ書込み

    /// <summary>
    /// Std140_TF_Output.xlsx の "A" シートの所定セルに各ケースの集計値を書き込む。
    /// 既存のレイアウト・他セルは保持。
    /// </summary>
    private static void FillStd140Template(
        string templatePath, string outPath, List<CaseResult> results)
    {
      File.Copy(templatePath, outPath, overwrite: true);
      using var doc = SpreadsheetDocument.Open(outPath, isEditable: true);
      var wbp = doc.WorkbookPart!;

      var sheetA = wbp.Workbook.Descendants<Sheet>().FirstOrDefault(s => s.Name?.Value == "A");
      if (sheetA == null)
      {
        Console.WriteLine("  WARN: sheet 'A' not found in template");
        return;
      }
      var wsp = (WorksheetPart)wbp.GetPartById(sheetA.Id!.Value!);
      var sd = wsp.Worksheet.GetFirstChild<SheetData>()!;

      int written = 0;
      foreach (var r in results)
      {
        WriteCaseResults(sd, r);
        written++;
      }

      var calc = wbp.Workbook.CalculationProperties;
      if (calc == null) { calc = new CalculationProperties(); wbp.Workbook.AppendChild(calc); }
      calc.CalculationMode = new EnumValue<CalculateModeValues>(CalculateModeValues.Auto);
      calc.FullCalculationOnLoad = true;

      // 数式セルの一部を値で上書きしているため、テンプレート由来の calcChain.xml は
      // 古い参照を含み Excel 起動時に「内容に問題が見つかりました」警告を出す。
      // 削除しておけば Excel が次回保存時に再生成する (任意 part)。
      if (wbp.CalculationChainPart != null)
        wbp.DeletePart(wbp.CalculationChainPart);

      wsp.Worksheet.Save();
      wbp.Workbook.Save();
      Console.WriteLine($"  {written} case(s) written to '{outPath}'");
    }

    /// <summary>1ケース分の結果セルを書き込む。</summary>
    private static void WriteCaseResults(SheetData sd, CaseResult r)
    {
      // §7.3.1: 年間負荷 (非 FF ケース行)
      if (!r.IsFreeFloat && AnnualLoadRow.TryGetValue(r.Case, out uint row))
      {
        SetNumericCell(sd, $"C{row}", r.AnnualHeating_MWh, "F4");
        SetNumericCell(sd, $"D{row}", r.AnnualCooling_MWh, "F4");
        SetNumericCell(sd, $"E{row}", r.PeakHeating_kW, "F3");
        if (r.PeakHeating_kW > 0)
        {
          var ph = DecomposeHourEnd(r.PeakHeatingTime);
          SetStringCell (sd, $"F{row}", ph.month);
          SetNumericCell(sd, $"G{row}", ph.day,  "0");
          SetNumericCell(sd, $"H{row}", ph.hour, "0");
        }
        SetNumericCell(sd, $"I{row}", r.PeakCooling_kW, "F3");
        if (r.PeakCooling_kW > 0)
        {
          var pc = DecomposeHourEnd(r.PeakCoolingTime);
          SetStringCell (sd, $"J{row}", pc.month);
          SetNumericCell(sd, $"K{row}", pc.day,  "0");
          SetNumericCell(sd, $"L{row}", pc.hour, "0");
        }
      }

      // §7.3.6: 自由温度ゾーン (FF ケース行)
      if (r.IsFreeFloat && FreeFloatRow.TryGetValue(r.Case, out uint ffRow))
      {
        SetNumericCell(sd, $"C{ffRow}", r.ZoneTempMean_C, "F2");
        SetNumericCell(sd, $"D{ffRow}", r.ZoneTempMin_C,  "F2");
        var nm = DecomposeHourEnd(r.ZoneTempMinTime);
        SetStringCell (sd, $"E{ffRow}", nm.month);
        SetNumericCell(sd, $"F{ffRow}", nm.day,  "0");
        SetNumericCell(sd, $"G{ffRow}", nm.hour, "0");
        SetNumericCell(sd, $"H{ffRow}", r.ZoneTempMax_C, "F2");
        var xm = DecomposeHourEnd(r.ZoneTempMaxTime);
        SetStringCell (sd, $"I{ffRow}", xm.month);
        SetNumericCell(sd, $"J{ffRow}", xm.day,  "0");
        SetNumericCell(sd, $"K{ffRow}", xm.hour, "0");
      }

      // §7.3.2.1: 年間入射日射 (Case 600)
      if (r.Case == Buildings.TestCase.C600)
      {
        SetNumericCell(sd, "C155", r.IncidentH_kWhm2, "F2");
        SetNumericCell(sd, "C156", r.IncidentN_kWhm2, "F2");
        SetNumericCell(sd, "C157", r.IncidentE_kWhm2, "F2");
        SetNumericCell(sd, "C158", r.IncidentS_kWhm2, "F2");
        SetNumericCell(sd, "C159", r.IncidentW_kWhm2, "F2");
      }

      // §7.3.2.2: 年間透過日射 (テンプレ Std140_TF_Output.xlsx の対応行)
      //   r163: 600/South, r164: 660/South, r165: 670/South, r166: 620/West
      //   r170: 610/South (shaded), r171: 630/West (shaded)
      if (r.Case == Buildings.TestCase.C600)
        SetNumericCell(sd, "C163", r.TransmittedSouth_kWhm2, "F2");
      if (r.Case == Buildings.TestCase.C660)
        SetNumericCell(sd, "C164", r.TransmittedSouth_kWhm2, "F2");
      if (r.Case == Buildings.TestCase.C670)
        SetNumericCell(sd, "C165", r.TransmittedSouth_kWhm2, "F2");
      if (r.Case == Buildings.TestCase.C620)
        SetNumericCell(sd, "C166", r.TransmittedSouth_kWhm2, "F2");
      if (r.Case == Buildings.TestCase.C610)
        SetNumericCell(sd, "C170", r.TransmittedSouth_kWhm2, "F2");
      if (r.Case == Buildings.TestCase.C630)
        SetNumericCell(sd, "C171", r.TransmittedSouth_kWhm2, "F2");

      // §7.3.2.3: 天空温度 (Case 600)
      if (r.Case == Buildings.TestCase.C600)
      {
        SetNumericCell(sd, "C178", r.SkyTempMean_C, "F2");
        SetNumericCell(sd, "D178", r.SkyTempMin_C,  "F2");
        var sn = DecomposeHourEnd(r.SkyTempMinTime);
        SetStringCell (sd, "E178", sn.month);
        SetNumericCell(sd, "F178", sn.day,  "0");
        SetNumericCell(sd, "G178", sn.hour, "0");
        SetNumericCell(sd, "H178", r.SkyTempMax_C, "F2");
        var sx = DecomposeHourEnd(r.SkyTempMaxTime);
        SetStringCell (sd, "I178", sx.month);
        SetNumericCell(sd, "J178", sx.day,  "0");
        SetNumericCell(sd, "K178", sx.hour, "0");
      }

      // §7.3.2.4: 月別負荷 (Case 600 / 900)
      if (r.Case == Buildings.TestCase.C600 || r.Case == Buildings.TestCase.C900)
      {
        bool is600 = r.Case == Buildings.TestCase.C600;
        for (int m = 0; m < 12; m++)
        {
          uint mrow = (uint)(190 + m);
          string cTot  = is600 ? "C" : "K";
          string cCool = is600 ? "D" : "L";
          string cPkH  = is600 ? "E" : "M";
          string cPkHd = is600 ? "F" : "N";
          string cPkHh = is600 ? "G" : "O";
          string cPkC  = is600 ? "H" : "P";
          string cPkCd = is600 ? "I" : "Q";
          string cPkCh = is600 ? "J" : "R";

          SetNumericCell(sd, $"{cTot}{mrow}",  r.MonthlyHeating_kWh[m], "F2");
          SetNumericCell(sd, $"{cCool}{mrow}", r.MonthlyCooling_kWh[m], "F2");
          SetNumericCell(sd, $"{cPkH}{mrow}",  r.MonthlyPeakHeating_kW[m], "F3");
          if (r.MonthlyPeakHeating_kW[m] > 0)
          {
            var ph = DecomposeHourEnd(r.MonthlyPeakHeatingTime[m]);
            SetNumericCell(sd, $"{cPkHd}{mrow}", ph.day,  "0");
            SetNumericCell(sd, $"{cPkHh}{mrow}", ph.hour, "0");
          }
          SetNumericCell(sd, $"{cPkC}{mrow}",  r.MonthlyPeakCooling_kW[m], "F3");
          if (r.MonthlyPeakCooling_kW[m] > 0)
          {
            var pc = DecomposeHourEnd(r.MonthlyPeakCoolingTime[m]);
            SetNumericCell(sd, $"{cPkCd}{mrow}", pc.day,  "0");
            SetNumericCell(sd, $"{cPkCh}{mrow}", pc.hour, "0");
          }
        }
      }

      // §7.3.8: 特定日毎時値 (rows 230-253, 262-285, 294-317)
      WriteSpecificDayHourly(sd, r);

      // §7.3.7: Case 900FF 温度ビンヒストグラム (rows 330-477)
      if (r.Case == Buildings.TestCase.C900FF)
      {
        for (int b = 0; b < 148; b++)
          SetNumericCell(sd, $"C{330 + b}", r.TempBinHours[b], "0");
      }
    }

    /// <summary>
    /// §7.3.8 特定日毎時値の書込み。テンプレ (Std140_TF_Output.xlsx シート"A") の構成:
    ///   rows 230-253: Solar/Sky/Trans (cases 600/660/670)
    ///   rows 262-285: Zone Loads + Zone Air Temps (cases 600/640/660-695/900/940/980-995)
    ///   rows 294-317: FF Zone Temperatures (cases 600FF/650FF/680FF/900FF/950FF/980FF)
    /// </summary>
    private static void WriteSpecificDayHourly(SheetData sd, CaseResult r)
    {
      // Solar/Sky/Trans 行 (Case 600/660/670 のみ)
      if (r.Case == Buildings.TestCase.C600)
      {
        for (int h = 0; h < 24; h++)
        {
          uint row = (uint)(230 + h);
          SetNumericCell(sd, $"C{row}", r.May4IncH_Wm2[h],   "F2");
          SetNumericCell(sd, $"D{row}", r.May4IncS_Wm2[h],   "F2");
          SetNumericCell(sd, $"E{row}", r.May4IncW_Wm2[h],   "F2");
          SetNumericCell(sd, $"F{row}", r.Jul14IncH_Wm2[h],  "F2");
          SetNumericCell(sd, $"G{row}", r.Jul14IncS_Wm2[h],  "F2");
          SetNumericCell(sd, $"H{row}", r.Jul14IncW_Wm2[h],  "F2");
          SetNumericCell(sd, $"I{row}", r.Feb1SkyTemp_C[h],  "F2");
          SetNumericCell(sd, $"J{row}", r.May4SkyTemp_C[h],  "F2");
          SetNumericCell(sd, $"K{row}", r.Jul14SkyTemp_C[h], "F2");
          SetNumericCell(sd, $"L{row}", r.Feb1TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"O{row}", r.May4TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"R{row}", r.Jul14TransSouth_Wm2[h], "F2");
        }
      }
      else if (r.Case == Buildings.TestCase.C660)
      {
        for (int h = 0; h < 24; h++)
        {
          uint row = (uint)(230 + h);
          SetNumericCell(sd, $"M{row}", r.Feb1TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"P{row}", r.May4TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"S{row}", r.Jul14TransSouth_Wm2[h], "F2");
        }
      }
      else if (r.Case == Buildings.TestCase.C670)
      {
        for (int h = 0; h < 24; h++)
        {
          uint row = (uint)(230 + h);
          SetNumericCell(sd, $"N{row}", r.Feb1TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"Q{row}", r.May4TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"T{row}", r.Jul14TransSouth_Wm2[h], "F2");
        }
      }

      // Zone Loads (rows 262-285): 1時間=power×1h なので W → kWh は ×1e-3。
      // 仕様: 暖房=正、冷房=負 (1 セルに heatSupply 直接、negative がそのまま冷房)
      if (Feb1LoadCol.TryGetValue(r.Case, out string? col1))
      {
        for (int h = 0; h < 24; h++)
          SetNumericCell(sd, $"{col1}{262 + h}", r.Feb1HeatSupply_W[h] * 1e-3, "F4");
      }
      if (Jul14LoadCol.TryGetValue(r.Case, out string? col2))
      {
        for (int h = 0; h < 24; h++)
          SetNumericCell(sd, $"{col2}{262 + h}", r.Jul14HeatSupply_W[h] * 1e-3, "F4");
      }

      // Feb 1 Zone Air Temp (controlled cases 640/940, rows 262-285 col Y/Z)
      if (Feb1ZoneTempCol.TryGetValue(r.Case, out string? col3))
      {
        for (int h = 0; h < 24; h++)
          SetNumericCell(sd, $"{col3}{262 + h}", r.Feb1ZoneTemp_C[h], "F2");
      }

      // FF Zone Temperatures (rows 294-317)
      if (FFTempDayCol.TryGetValue(r.Case, out var ffCol))
      {
        double[] src = ffCol.isJul14 ? r.Jul14ZoneTemp_C : r.Feb1ZoneTemp_C;
        for (int h = 0; h < 24; h++)
          SetNumericCell(sd, $"{ffCol.col}{294 + h}", src[h], "F2");
      }
    }

    #endregion

    #region 参照値突合 (Std140_TF_Results.xlsx)

    /// <summary>1 ケース・1 メトリックの参照範囲 (Min/Max/Mean of 6 ref tools)。</summary>
    private struct RefRange
    {
      public double Min;
      public double Max;
      public double Mean;
      public bool HasValue;
    }

    /// <summary>
    /// Std140_TF_Results.xlsx (Informative Annex B8) の参照ツール6本分集計と
    /// Popolo の結果を突合し、envelope 内/外と Mean からの偏差をプリント。
    /// </summary>
    private static void CompareWithReference(List<CaseResult> results, string xlsxPath)
    {
      Console.WriteLine($"=== Reference comparison vs {Path.GetFileName(xlsxPath)} ===");
      Console.WriteLine($"  (envelope = [Min, Max] of 6 ref tools: BSIMAC/CSE/DeST/EnergyPlus/ESP-r/TRNSYS)");

      Dictionary<int, RefRange> heating, cooling, peakH, peakC;
      using (var doc = SpreadsheetDocument.Open(xlsxPath, false))
      {
        var wbp = doc.WorkbookPart!;
        var sst = wbp.SharedStringTablePart?.SharedStringTable;
        SheetData? sd1 = GetSheetData(wbp, "Tables 1");
        SheetData? sd2 = GetSheetData(wbp, "Tables 2");
        if (sd1 == null || sd2 == null)
        {
          Console.WriteLine("  WARN: 'Tables 1' or 'Tables 2' sheet not found in reference xlsx");
          return;
        }
        // Tables 1: B8-1 (heating, header r7), B8-2 (cooling, header r59)
        // 列レイアウト: 各ツール 1 列 → Min/Max/Mean = I/J/K
        heating = ParseRefTable(sd1, sst, headerRow: 7,  minCol: "I",  maxCol: "J",  meanCol: "K");
        cooling = ParseRefTable(sd1, sst, headerRow: 59, minCol: "I",  maxCol: "J",  meanCol: "K");
        // Tables 2: B8-3 (peak h, r7), B8-4 (peak c, r59)
        // 列レイアウト: 各ツール 4 列 (kW/Mo/Day/Hr) → Min/Max/Mean = AA/AB/AC
        peakH = ParseRefTable(sd2, sst, headerRow: 7,  minCol: "AA", maxCol: "AB", meanCol: "AC");
        peakC = ParseRefTable(sd2, sst, headerRow: 59, minCol: "AA", maxCol: "AB", meanCol: "AC");
      }

      PrintComparison("Annual Heating Load [MWh]", results,
          r => !r.IsFreeFloat, r => r.AnnualHeating_MWh, heating);
      PrintComparison("Annual Cooling Load [MWh]", results,
          r => !r.IsFreeFloat, r => r.AnnualCooling_MWh, cooling);
      PrintComparison("Annual Peak Heating [kW]", results,
          r => !r.IsFreeFloat && r.PeakHeating_kW > 0, r => r.PeakHeating_kW, peakH);
      PrintComparison("Annual Peak Cooling [kW]", results,
          r => !r.IsFreeFloat && r.PeakCooling_kW > 0, r => r.PeakCooling_kW, peakC);
    }

    /// <summary>
    /// 表をケース番号で辞書化。データは <paramref name="headerRow"/>+4 から始まり、
    /// Min/Max/Mean は <paramref name="minCol"/>/<paramref name="maxCol"/>/<paramref name="meanCol"/> の列にあるとする。
    /// </summary>
    private static Dictionary<int, RefRange> ParseRefTable(
        SheetData sd, SharedStringTable? sst, uint headerRow,
        string minCol, string maxCol, string meanCol)
    {
      var dict = new Dictionary<int, RefRange>();
      uint dataStart = headerRow + 4;
      for (uint r = dataStart; r < dataStart + 50; r++)
      {
        var row = sd.Elements<Row>().FirstOrDefault(x => x.RowIndex?.Value == r);
        if (row == null) continue;
        string desc = GetCellText(row, $"B{r}", sst);
        if (string.IsNullOrEmpty(desc)) break;
        if (desc.StartsWith("Table ") || desc.StartsWith("**") || desc.StartsWith("Simulation "))
          break;

        int sp = desc.IndexOf(' ');
        if (sp <= 0) continue;
        if (!int.TryParse(desc.Substring(0, sp), out int caseNum)) continue;

        double min  = ParseDouble(GetCellText(row, $"{minCol}{r}",  sst));
        double max  = ParseDouble(GetCellText(row, $"{maxCol}{r}",  sst));
        double mean = ParseDouble(GetCellText(row, $"{meanCol}{r}", sst));
        bool has = !double.IsNaN(min) && !double.IsNaN(max) && !double.IsNaN(mean);

        dict[caseNum] = new RefRange { Min = min, Max = max, Mean = mean, HasValue = has };
      }
      return dict;
    }

    private static void PrintComparison(string title,
        List<CaseResult> results,
        Func<CaseResult, bool> includePredicate,
        Func<CaseResult, double> selector,
        Dictionary<int, RefRange> refDict)
    {
      Console.WriteLine();
      Console.WriteLine($"  ── {title} ──");
      Console.WriteLine($"  {"Case",-7} {"Popolo",10} {"Ref Min",10} {"Ref Max",10} {"Ref Mean",10}  {"Δ%(Mean)",10}  Status");

      int inEnv = 0, above = 0, below = 0, noRef = 0;
      foreach (var r in results)
      {
        if (!includePredicate(r)) continue;
        int? cn = GetCaseNumber(r.Case);
        if (cn == null) continue;
        double pop = selector(r);
        if (!refDict.TryGetValue(cn.Value, out RefRange rr) || !rr.HasValue)
        {
          Console.WriteLine($"  {r.Case,-7} {pop,10:F3}    --       --        --          --     no-ref");
          noRef++;
          continue;
        }
        double devPct = rr.Mean != 0 ? (pop - rr.Mean) / Math.Abs(rr.Mean) * 100.0 : 0;
        string status;
        if      (pop < rr.Min) { status = "BELOW";    below++; }
        else if (pop > rr.Max) { status = "ABOVE";    above++; }
        else                   { status = "in env";   inEnv++; }
        Console.WriteLine($"  {r.Case,-7} {pop,10:F3} {rr.Min,10:F3} {rr.Max,10:F3} {rr.Mean,10:F3}  {devPct,9:+0.0;-0.0;0.0}%  {status}");
      }
      Console.WriteLine($"  Summary: {inEnv} in env, {above} above, {below} below, {noRef} no-ref");
    }

    /// <summary>
    /// 診断用: classic_bestest_result.xlsx (旧版 BESTEST 結果) の "検証" シートから
    /// 旧 Popolo 値 (col J) を読み出し、新ランナーの結果と突合プリント。
    /// 旧版 BESTEST の構造:
    ///   r5-r40   : Annual Heating  (col A=case#, col J=Popolo)
    ///   r44-r79  : Annual Cooling
    ///   r84-r119 : Peak Heating
    ///   r124-r159: Peak Cooling
    /// </summary>
    private static void CompareWithLegacyResults(List<CaseResult> results, string xlsxPath)
    {
      Console.WriteLine($"=== Legacy Popolo comparison vs {Path.GetFileName(xlsxPath)} ===");
      Console.WriteLine($"  (col J of '検証' sheet = legacy Popolo with DRYCOLD.TMY)");

      Dictionary<int, double> lH, lC, lpH, lpC;
      using (var doc = SpreadsheetDocument.Open(xlsxPath, false))
      {
        var wbp = doc.WorkbookPart!;
        var sst = wbp.SharedStringTablePart?.SharedStringTable;
        SheetData? sd = GetSheetData(wbp, "検証");
        if (sd == null) { Console.WriteLine("  WARN: '検証' sheet not found"); return; }
        lH  = ParseLegacyTable(sd, sst,   5,  40);
        lC  = ParseLegacyTable(sd, sst,  44,  79);
        lpH = ParseLegacyTable(sd, sst,  84, 119);
        lpC = ParseLegacyTable(sd, sst, 124, 159);
      }

      PrintLegacyComp("Annual Heating [MWh]", results, r => !r.IsFreeFloat,
                      r => r.AnnualHeating_MWh, lH);
      PrintLegacyComp("Annual Cooling [MWh]", results, r => !r.IsFreeFloat,
                      r => r.AnnualCooling_MWh, lC);
      PrintLegacyComp("Peak Heating [kW]", results, r => !r.IsFreeFloat && r.PeakHeating_kW > 0,
                      r => r.PeakHeating_kW, lpH);
      PrintLegacyComp("Peak Cooling [kW]", results, r => !r.IsFreeFloat && r.PeakCooling_kW > 0,
                      r => r.PeakCooling_kW, lpC);
    }

    /// <summary>"検証" シート内 1 表 (case# 列 A, Popolo 値 列 J) をパース。</summary>
    private static Dictionary<int, double> ParseLegacyTable(
        SheetData sd, SharedStringTable? sst, uint rowStart, uint rowEnd)
    {
      var dict = new Dictionary<int, double>();
      for (uint r = rowStart; r <= rowEnd; r++)
      {
        var row = sd.Elements<Row>().FirstOrDefault(x => x.RowIndex?.Value == r);
        if (row == null) continue;
        string aStr = GetCellText(row, $"A{r}", sst);
        string jStr = GetCellText(row, $"J{r}", sst);
        if (string.IsNullOrEmpty(aStr) || string.IsNullOrEmpty(jStr)) continue;
        if (!int.TryParse(aStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int caseNum)) continue;
        double val = ParseDouble(jStr);
        if (!double.IsNaN(val)) dict[caseNum] = val;
      }
      return dict;
    }

    private static void PrintLegacyComp(string title,
        List<CaseResult> results,
        Func<CaseResult, bool> includePredicate,
        Func<CaseResult, double> selector,
        Dictionary<int, double> legacyDict)
    {
      Console.WriteLine();
      Console.WriteLine($"  ── {title} ──");
      Console.WriteLine($"  {"Case",-7} {"Legacy",10} {"NewLegSite",12} {"Diff",10}  {"%diff",8}");
      var diffs = new List<double>();
      foreach (var r in results)
      {
        if (!includePredicate(r)) continue;
        int? cn = GetCaseNumber(r.Case);
        if (cn == null) continue;
        double pop = selector(r);
        if (!legacyDict.TryGetValue(cn.Value, out double L))
        {
          Console.WriteLine($"  {r.Case,-7} {"--",10} {pop,12:F3}    --        no-legacy");
          continue;
        }
        double diff = pop - L;
        double pct = Math.Abs(L) > 1e-6 ? diff / L * 100 : 0;
        diffs.Add(Math.Abs(pct));
        string flag = Math.Abs(pct) > 5 ? " ⚠" : "";
        Console.WriteLine($"  {r.Case,-7} {L,10:F3} {pop,12:F3} {diff,+10:F3}  {pct,+7:F1}%{flag}");
      }
      if (diffs.Count > 0)
        Console.WriteLine($"  --- avg|%|: {diffs.Average():F2}%, max|%|: {diffs.Max():F2}%, n={diffs.Count}");
    }

    /// <summary>
    /// Buildings.TestCase enum (例: C600, C600FF) から Std140 ケース番号 (600) を取得。
    /// 末尾 "FF" は除去して数値部分のみ返す。
    /// </summary>
    private static int? GetCaseNumber(Buildings.TestCase c)
    {
      string name = c.ToString();
      if (name.StartsWith("C")) name = name.Substring(1);
      if (name.EndsWith("FF")) name = name.Substring(0, name.Length - 2);
      return int.TryParse(name, out int n) ? n : null;
    }

    #endregion

    #region OpenXml 読込ヘルパー (参照値突合用)

    private static SheetData? GetSheetData(WorkbookPart wbp, string sheetName)
    {
      var sheet = wbp.Workbook.Descendants<Sheet>().FirstOrDefault(s => s.Name?.Value == sheetName);
      if (sheet == null) return null;
      var wsp = (WorksheetPart)wbp.GetPartById(sheet.Id!.Value!);
      return wsp.Worksheet.GetFirstChild<SheetData>();
    }

    private static string GetCellText(Row row, string cellRef, SharedStringTable? sst)
    {
      var c = row.Elements<Cell>().FirstOrDefault(x => x.CellReference?.Value == cellRef);
      if (c == null) return "";
      // SharedString 参照
      if (c.DataType?.Value == CellValues.SharedString && sst != null)
      {
        if (int.TryParse(c.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx)
            && idx >= 0 && idx < sst.ChildElements.Count)
          return sst.ChildElements[idx].InnerText;
      }
      // InlineString
      if (c.DataType?.Value == CellValues.InlineString)
        return c.InnerText;
      // Boolean / Number / etc
      return c.CellValue?.Text ?? "";
    }

    private static double ParseDouble(string text)
    {
      if (string.IsNullOrEmpty(text)) return double.NaN;
      return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
          ? v : double.NaN;
    }

    #endregion

    #region OpenXml セル書込みヘルパー

    private static (string col, uint row) SplitCellRef(string cellRef)
    {
      int i = 0;
      while (i < cellRef.Length && char.IsLetter(cellRef[i])) i++;
      return (cellRef.Substring(0, i), uint.Parse(cellRef.Substring(i)));
    }

    private static int ColIndex(string col)
    {
      int n = 0;
      foreach (char c in col) n = n * 26 + (c - 'A' + 1);
      return n - 1;
    }

    private static Cell GetOrCreateCell(SheetData sd, string cellRef)
    {
      var (colLetter, rowIdx) = SplitCellRef(cellRef);

      Row? row = sd.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == rowIdx);
      if (row == null)
      {
        row = new Row { RowIndex = rowIdx };
        var insertBefore = sd.Elements<Row>().FirstOrDefault(r => r.RowIndex != null && r.RowIndex.Value > rowIdx);
        if (insertBefore != null) sd.InsertBefore(row, insertBefore);
        else sd.AppendChild(row);
      }

      Cell? cell = row.Elements<Cell>().FirstOrDefault(c => c.CellReference?.Value == cellRef);
      if (cell == null)
      {
        cell = new Cell { CellReference = cellRef };
        int targetCol = ColIndex(colLetter);
        var insertBefore = row.Elements<Cell>().FirstOrDefault(c =>
        {
          if (c.CellReference?.Value == null) return false;
          var (cc, _) = SplitCellRef(c.CellReference.Value);
          return ColIndex(cc) > targetCol;
        });
        if (insertBefore != null) row.InsertBefore(cell, insertBefore);
        else row.AppendChild(cell);
      }
      return cell;
    }

    private static void SetNumericCell(SheetData sd, string cellRef, double value, string fmt)
    {
      if (double.IsNaN(value)) return;
      var cell = GetOrCreateCell(sd, cellRef);
      cell.RemoveAllChildren();
      cell.DataType = null;
      cell.CellValue = new CellValue(value.ToString(fmt, CultureInfo.InvariantCulture));
    }

    private static void SetNumericCell(SheetData sd, string cellRef, int value, string fmt)
        => SetNumericCell(sd, cellRef, (double)value, fmt);

    private static void SetStringCell(SheetData sd, string cellRef, string value)
    {
      var cell = GetOrCreateCell(sd, cellRef);
      cell.RemoveAllChildren();
      cell.CellValue = null;
      cell.DataType = CellValues.InlineString;
      cell.AppendChild(new InlineString(new Text(value)));
    }

    #endregion
  }
}
