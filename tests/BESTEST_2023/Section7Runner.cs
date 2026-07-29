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
  /// <summary>
  /// Std 140-2023 Section 7 (Building Thermal Envelope and Fabric Load) ランナー。
  /// 3 ファイルに partial 分割している:
  ///   Section7Runner.cs                  — 公開エントリ <see cref="Run"/>、1 ケース実行
  ///                                          (<see cref="RunCase"/>)、結果型 / 表マッピング
  ///   Section7Runner.OutputWriter.cs     — Std140_TF_Output.xlsx (Annex B8 informative) への
  ///                                          結果転記
  ///   Section7Runner.ReferenceCompare.cs — Std140_TF_Results.xlsx の 6 参照ツール集計と
  ///                                          Popolo の突合プリント
  /// </summary>
  internal static partial class Section7Runner
  {
    #region Constants (Std 140-2023 Annex A1, Table A1-1)

    /// <summary>サイト情報 (Sun 位置計算用)。</summary>
    private record SiteInfo(double Latitude, double Longitude, double StdLongitude, double Elevation);

    /// <summary>Std 140-2023 Annex A1 サイト (DIA, Denver Intl AP)。</summary>
    private static readonly SiteInfo Std140_2023Site
        = new SiteInfo(39.833, -104.65, -105.0, 1650.0);

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

    /// <summary>非自由温度ケース → Std140_TF_Output.xlsx シート 'YourData' の年間負荷行。</summary>
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

    /// <summary>自由温度ケース → Std140_TF_Output.xlsx シート 'YourData' の自由温度行。</summary>
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

    #region Result types

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

    #region Public entry points

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

      // Std 140-2023 Annex A3 (Normative) Software Acceptance Criteria 判定
      Console.WriteLine();
      var caseValues = new Dictionary<string, (double AH, double AC)>();
      foreach (var r in allResults)
      {
        caseValues[r.Case.ToString()] = (r.AnnualHeating_MWh, r.AnnualCooling_MWh);
      }
      string a3ReportPath = Path.Combine(resultsDir, "A3_AcceptanceReport.txt");
      A3Evaluator.EvaluateAndReport(caseValues, a3ReportPath);
    }

    #endregion

    #region Single case execution

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
      //  - 屋外側 h_c: 風速依存 (MoWiTT, windward/leeward 切替 + 高さ補正)
      //  - 室内側 h_c: 動的化しない。Table B4-1 の静的値はリファレンスシミュレータ群の
      //                TARP 計算結果の年間平均値として導出されており、Popolo が再度 TARP を
      //                適用すると spec 範囲内のケースが多数境界外に移動する (検証済み)。
      bModel.DynamicIndoorRadiativeCoefficient = !isConstIntCoeffs;
      bModel.DynamicOutdoorRadiativeCoefficient = !isConstExtCoeffs;
      bModel.DynamicOutdoorConvectiveCoefficient = !isConstExtCoeffs;

      // Std 140-2023 §7.2.1.1.2: weather station tower top = 10 m, building floor
      // is 10 m below tower top (= ground level). Both meteo and building site are
      // open terrain (Terrain Category 3). 風速の境界層補正に必要な情報を設定。
      bModel.SetWeatherStation(new WeatherStationInfo(
          name: "BESTEST", latitude: site.Latitude, longitude: site.Longitude,
          elevation: site.Elevation,
          anemometerHeight: 10.0,
          stationTerrain: TerrainCategory.OpenTerrain));
      bModel.SetSiteTerrainCategory(TerrainCategory.OpenTerrain);
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
            WeatherRecord recH = wd.Records[h];
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
              // Std 140-2023 §7.2.2.1.5 + Table 7-16: 18:00–07:00 (Hours 19–7) で
              // 0.5 ACH 背景 (§7.2.1.6) に加えて 1700 m³/h vent fan (高度補正済み
              // 体積流量、Buildings.AIR_DNS が高度密度を提供)。日中はベース 0.5 ACH のみ。
              if (simTimeH.Hour < 7 || 18 <= simTimeH.Hour)
                bModel.SetVentilationRate(0, 0,
                    (mRoom.Zones[0].AirMass * 0.5 + 1700 * Buildings.AIR_DNS) / 3600.0);
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
            else if (isDeadBand)
            {
              if      (zones[0].Temperature < 20) bModel.ControlDryBulbTemperature(0, 0, 20);
              else if (zones[0].Temperature > 27) bModel.ControlDryBulbTemperature(0, 0, 27);
            }
            else if (isSetBack)
            {
              if (zones[0].Temperature > 27) bModel.ControlDryBulbTemperature(0, 0, 27);
              else if ((8 <= simTimeH.Hour && simTimeH.Hour < 23) && zones[0].Temperature < 20)
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
          WeatherRecord rec = wd.Records[i];
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
            // 仕様の補足は warmup ループ側コメント参照。
            if (simTime.Hour < 7 || 18 <= simTime.Hour)
              bModel.SetVentilationRate(0, 0,
                  (mRoom.Zones[0].AirMass * 0.5 + 1700 * Buildings.AIR_DNS) / 3600.0);
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
            var inc = windows[0].OutsideIncline;
            double directIrr  = inc.GetDirectSolarIrradiance(sun);
            double diffuseTotal = inc.GetDiffuseSolarIrradiance(sun, mRoom.Albedo);
            // 拡散日射: 実シミュレーション (Window.EmitShortWaveToIndoor) と同じく
            // 天空成分のみ SunShade で減衰させる。
            double groundReflected = mRoom.Albedo * inc.ConfigurationFactorToGround
                                   * sun.GlobalHorizontalRadiation;
            double skyDiffuse = diffuseTotal - groundReflected;
            if (skyDiffuse < 0) skyDiffuse = 0;
            double dirShade = windows[0].SunShade != null
                ? windows[0].SunShade.GetDirectShadingRate(sun, inc) : 0.0;
            double skyShade = windows[0].SunShade != null
                ? windows[0].SunShade.GetSkyDiffuseShadingRate(inc) : 0.0;
            double dirEff = directIrr * (1.0 - dirShade);
            double difEff = skyDiffuse * (1.0 - skyShade) + groundReflected;
            transS = dirEff * windows[0].DirectSolarIncidentTransmittance
                   + difEff * windows[0].DiffuseSolarIncidentTransmittance;
          }
          sumTransSouth += transS;

          // (Warmup はメインループ前に 24h サイクル × 7 日反復で実施済み)

          // 自然室温の予測
          bModel.ControlHeatSupply(0, 0, 0);
          bModel.ForecastHeatTransfer();

          // 制御適用
          if      (isBangBang) bModel.ControlDryBulbTemperature(0, 0, 20);
          else if (isDeadBand)
          {
            if      (zones[0].Temperature < 20) bModel.ControlDryBulbTemperature(0, 0, 20);
            else if (zones[0].Temperature > 27) bModel.ControlDryBulbTemperature(0, 0, 27);
          }
          else if (isSetBack)
          {
            if (zones[0].Temperature > 27) bModel.ControlDryBulbTemperature(0, 0, 27);
            else if ((8 <= simTime.Hour && simTime.Hour < 23) && zones[0].Temperature < 20)
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
  }
}
