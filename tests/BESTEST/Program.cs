/* Program.cs - BESTEST Validation Runner
 *
 * Runs ASHRAE Standard 140 / BESTEST validation cases and writes
 * per-hour CSV results. Results are then transferred to Result.xlsx
 * for comparison against reference tool bands.
 *
 * Usage:
 *   1. Place DRYCOLD.TMY in the working directory (or WeatherData/).
 *   2. Place Result.xlsx template in the working directory (or ResultTemplate/).
 *   3. Run: dotnet run
 *   4. Open the updated Result.xlsx and verify results.
 *
 * Copyright (C) 2016-2026 E.Togashi
 */

using System;
using System.IO;
using System.Text;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using Popolo.Core.Climate;
using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Physics;

namespace Popolo.Core.Validation.BESTEST
{
  class Program
  {

    #region 定数宣言

    static readonly Incline INC_N = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
    static readonly Incline INC_E = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
    static readonly Incline INC_W = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
    static readonly Incline INC_S = new Incline(Incline.Orientation.S, 0.5 * Math.PI);
    static readonly Incline INC_H = new Incline(Incline.Orientation.N, 0);

    /// <summary>Suppress nocturnal radiation.
    /// BESTEST results are insensitive to nocturnal radiation;
    /// the TMY→nocturnal conversion appears to introduce errors.</summary>
    const bool NO_NOC_RAD = true;

    /// <summary>Air density at BESTEST conditions [kg/m³].</summary>
    const double AIR_DNS = PhysicsConstants.NominalMoistAirDensity * 0.822;

    #endregion

    #region 列挙型定義

    /// <summary>BESTEST test case identifiers.</summary>
    [Flags]
    public enum TestCase : long
    {
      None = 0,
      C195 = 1,
      C200 = C195 * 2,
      C210 = C200 * 2,
      C215 = C210 * 2,
      C220 = C215 * 2,
      C230 = C220 * 2,
      C240 = C230 * 2,
      C250 = C240 * 2,
      C270 = C250 * 2,
      C280 = C270 * 2,
      C290 = C280 * 2,
      C300 = C290 * 2,
      C310 = C300 * 2,
      C320 = C310 * 2,
      C395 = C320 * 2,
      C400 = C395 * 2,
      C410 = C400 * 2,
      C420 = C410 * 2,
      C430 = C420 * 2,
      C440 = C430 * 2,
      C600 = C440 * 2,
      C610 = C600 * 2,
      C620 = C610 * 2,
      C630 = C620 * 2,
      C640 = C630 * 2,
      C650 = C640 * 2,
      C800 = C650 * 2,
      C810 = C800 * 2,
      C900 = C810 * 2,
      C910 = C900 * 2,
      C920 = C910 * 2,
      C930 = C920 * 2,
      C940 = C930 * 2,
      C950 = C940 * 2,
      C960 = C950 * 2,
      C990 = C960 * 2,
      C600FF = C990 * 2,
      C650FF = C600FF * 2,
      C900FF = C650FF * 2,
      C950FF = C900FF * 2,
      C900_J1_1 = C950FF * 2,
      C900_J1_2 = C900_J1_1 * 2,
      C900_J2 = C900_J1_2 * 2,
      C900_J3 = C900_J2 * 2,

      ControlBangBang = C195 | C200 | C210 | C215 | C220 | C230 | C240 | C250 | C270 | C280 | C290 | C300 | C310,
      ControlDeadBand = C320 | C395 | C400 | C410 | C420 | C430 | C440 | C600 | C610 | C620 | C630 | C800 | C810 | C900 | C910 | C920 | C930 | C960 | C990 | C900_J1_1 | C900_J1_2 | C900_J3,
      ControlSetBack = C640 | C940,
      ControlVenting = C650 | C950 | C650FF | C950FF,
      ControlNone = C600FF | C650FF | C900FF | C950FF,
      HeavyWeight = C800 | C810 | C900 | C910 | C920 | C930 | C940 | C950 | C900FF | C950FF | C990 | C900_J1_1 | C900_J1_2 | C900_J2 | C900_J3,
      HasHeatGain = C240 | C420 | C430 | C440 | C600 | C610 | C620 | C630 | C640 | C650 | C800 | C810 | C900 | C910 | C920 | C930 | C940 | C950 | C990 | C600FF | C650FF | C900FF | C950FF | C900_J1_1 | C900_J1_2,
      HasOpaqueWindow = C200 | C210 | C215 | C220 | C230 | C240 | C250 | C400 | C410 | C420 | C430 | C800,
      NoInfiltration = C195 | C200 | C210 | C215 | C220 | C240 | C250 | C270 | C280 | C290 | C300 | C310 | C320 | C395 | C400,
      LowIntIREmissivity = C195 | C200 | C210,
      LowExtIREmissivity = C195 | C200 | C215,
      LowIntSWEmissivity = C280 | C440 | C810,
      HighIntSWEmissivity = C270 | C290 | C300 | C310 | C320,
      LowExtSWEmissivity = C195 | C200 | C210 | C215 | C220 | C230 | C240 | C270 | C280 | C290 | C300 | C310 | C320 | C395 | C400 | C410 | C420,
      NoWindow = C195 | C395,
      HasHighConductanceWall = C210 | C215 | C220 | C230 | C240 | C250 | C400 | C410 | C420 | C430 | C440 | C800,
      HasEWWindow = C300 | C310 | C620 | C630 | C920 | C930,
      HasSunShade = C290 | C310 | C610 | C630 | C910 | C930 | C990,
      JapaneseInsulation = C900_J1_2 | C900_J2 | C900_J3,
    }

    #endregion

    #region メインメソッド

    static void Main(string[] args)
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

      // TMYデータからBESTEST用CSVを生成
      string tmyPath = FindFile("DRYCOLD.TMY", "WeatherData");
      string templatePath = FindFile("Result.xlsx", "ResultTemplate");
      string str;
      using (StreamReader sr = new StreamReader(tmyPath))
        str = sr.ReadToEnd();
      //string csv = WeatherConverter.TMY1toCSV(str);
      //using (StreamWriter sw = new StreamWriter("DRYCOLD.csv", false, Encoding.GetEncoding("Shift_JIS")))
      //  sw.WriteLine(csv);
      //MakeBESTestWeatherFile("DRYCOLD.csv", "BESTestWeather.csv");

      // テスト実行
      Directory.CreateDirectory("Result");

      // 実行するケースをここで選択（コメントアウトで除外）
      //Test(TestCase.C600, "Result\\C600.csv");
      
      Test(TestCase.C195,      "Result\\C195.csv");
      Test(TestCase.C200,      "Result\\C200.csv");
      Test(TestCase.C210,      "Result\\C210.csv");
      Test(TestCase.C215,      "Result\\C215.csv");
      Test(TestCase.C220,      "Result\\C220.csv");
      Test(TestCase.C230,      "Result\\C230.csv");
      Test(TestCase.C240,      "Result\\C240.csv");
      Test(TestCase.C250,      "Result\\C250.csv");
      Test(TestCase.C270,      "Result\\C270.csv");
      Test(TestCase.C280,      "Result\\C280.csv");
      Test(TestCase.C290,      "Result\\C290.csv");
      Test(TestCase.C300,      "Result\\C300.csv");
      Test(TestCase.C310,      "Result\\C310.csv");
      Test(TestCase.C320,      "Result\\C320.csv");
      Test(TestCase.C395,      "Result\\C395.csv");
      Test(TestCase.C400,      "Result\\C400.csv");
      Test(TestCase.C410,      "Result\\C410.csv");
      Test(TestCase.C420,      "Result\\C420.csv");
      Test(TestCase.C430,      "Result\\C430.csv");
      Test(TestCase.C440,      "Result\\C440.csv");
      Test(TestCase.C600,      "Result\\C600.csv");
      Test(TestCase.C610,      "Result\\C610.csv");
      Test(TestCase.C620,      "Result\\C620.csv");
      Test(TestCase.C630,      "Result\\C630.csv");
      Test(TestCase.C640,      "Result\\C640.csv");
      Test(TestCase.C650,      "Result\\C650.csv");
      Test(TestCase.C800,      "Result\\C800.csv");
      Test(TestCase.C810,      "Result\\C810.csv");
      Test(TestCase.C900,      "Result\\C900.csv");
      Test(TestCase.C910,      "Result\\C910.csv");
      Test(TestCase.C920,      "Result\\C920.csv");
      Test(TestCase.C930,      "Result\\C930.csv");
      Test(TestCase.C940,      "Result\\C940.csv");
      Test(TestCase.C950,      "Result\\C950.csv");
      Test(TestCase.C600FF,    "Result\\C600FF.csv");
      Test(TestCase.C650FF,    "Result\\C650FF.csv");
      Test(TestCase.C900FF,    "Result\\C900FF.csv");
      Test(TestCase.C950FF,    "Result\\C950FF.csv");
      Test(TestCase.C960,      "Result\\C960.csv");
      Test(TestCase.C990,      "Result\\C990.csv");
      Test(TestCase.C900_J1_1, "Result\\C900_J1_1.csv");
      Test(TestCase.C900_J1_2, "Result\\C900_J1_2.csv");
      Test(TestCase.C900_J2,   "Result\\C900_J2.csv");
      Test(TestCase.C900_J3,   "Result\\C900_J3.csv");
      

      // 結果をExcelに転記
      if (File.Exists(templatePath))
        MakeBESTResultExcelSheet(templatePath);
    }

    #endregion

    #region ユーティリティ

    /// <summary>ファイルをカレントディレクトリまたはサブディレクトリから検索する。</summary>
    private static string FindFile(string fileName, string subDir)
    {
      string direct = Path.Combine(Directory.GetCurrentDirectory(), fileName);
      if (File.Exists(direct)) return direct;
      string inSub = Path.Combine(Directory.GetCurrentDirectory(), subDir, fileName);
      if (File.Exists(inSub)) return inSub;
      throw new FileNotFoundException($"{fileName} not found. Place it in the working directory or {subDir}/.", fileName);
    }

    #endregion

    #region 年間計算処理

    public static void Test(TestCase testCase, string outputFilePath)
    {
      Console.Write("Testing Case " + testCase.ToString() + "...");

      bool isBangBang = (testCase & TestCase.ControlBangBang) == testCase;
      bool isDeadBand = (testCase & TestCase.ControlDeadBand) == testCase;
      bool isSetBack = (testCase & TestCase.ControlSetBack) == testCase;
      bool isVenting27 = (testCase & TestCase.ControlVenting) == testCase;

      // 傾斜面（ロギング用）
      Incline[] incs = new Incline[]
      {
        new Incline(Incline.Orientation.N, 0),
        new Incline(Incline.Orientation.N, 0.5 * Math.PI),
        new Incline(Incline.Orientation.E, 0.5 * Math.PI),
        new Incline(Incline.Orientation.W, 0.5 * Math.PI),
        new Incline(Incline.Orientation.S, 0.5 * Math.PI),
      };

      // モデルを作成
      MultiRooms mRoom;
      Zone[] zones;
      Wall[] walls;
      Window[] windows;
      Sun sun = new Sun(39 + 8d / 60d, 360 - 104 + 9d / 60d, 360 - 105);
      MakeBuilding(testCase, out mRoom, out zones, out walls, out windows);
      BuildingThermalModel bModel = new BuildingThermalModel(new MultiRooms[] { mRoom });

      using (StreamReader sReader = new StreamReader("BESTestWeather.csv"))
      using (StreamWriter sWriter = new StreamWriter(outputFilePath, false, Encoding.GetEncoding("Shift_JIS")))
      {
        // ヘッダ行
        sWriter.Write("日付");
        for (int i = 0; i < Math.Min(5, incs.Length); i++) sWriter.Write(",傾斜面" + i);
        if (testCase == TestCase.C990) sWriter.Write(",");
        sWriter.Write(",室乾球温度[C],顕熱負荷[W],窓1透過日射[W/m2],窓2透過日射[W/m2]");
        if (testCase == TestCase.C960) sWriter.Write(",SunZone室温[C],SunZone顕熱負荷[W]");
        sWriter.WriteLine();

        string sBuff;
        string[] strs;
        sReader.ReadLine(); // ヘッダスキップ
        DateTime dt = new DateTime(1999, 1, 1, 0, 30, 0);
        double prevDBT = 0;
        double prevAHD = 0;
        bool isStarting = true;

        while ((sBuff = sReader.ReadLine()) != null)
        {
          sWriter.Write(dt.ToString());

          strs = sBuff.Split(',');
          double dbt = (double.Parse(strs[2]) + prevDBT) / 2d;
          double ahd = (double.Parse(strs[3]) + prevAHD) / 2d;
          double iDn = double.Parse(strs[4]);
          double iHol = double.Parse(strs[5]);
          double iSky = double.Parse(strs[6]);
          double nr = double.Parse(strs[8]);
          double gdbt1 = double.Parse(strs[10]); // 地中温度（0.675 m）
          double gdbt2 = double.Parse(strs[11]); // 地中温度（1.350 m）
          prevDBT = double.Parse(strs[2]);
          prevAHD = double.Parse(strs[3]);

          // 外気条件更新
          bModel.UpdateOutdoorCondition(dt, sun, dbt, ahd, nr);
          if (testCase == TestCase.C990)
          {
            bModel.SetGroundTemperature(0, 0, true, gdbt2);
            bModel.SetGroundTemperature(0, 3, true, gdbt1);
            bModel.SetGroundTemperature(0, 5, true, gdbt1);
            bModel.SetGroundTemperature(0, 7, true, gdbt1);
            bModel.SetGroundTemperature(0, 8, true, gdbt1);
          }
          else bModel.SetGroundTemperature(0, 0, true, 10);

          // 換気量制御
          if (isVenting27)
          {
            if (dt.Hour < 7 || 18 <= dt.Hour)
              bModel.SetVentilationRate(0, 0, (bModel.MultiRoom[0].Zones[0].AirMass + 1400 * AIR_DNS) / 3600d);
            else
              bModel.SetVentilationRate(0, 0, bModel.MultiRoom[0].Zones[0].AirMass * 0.5 / 3600d);
          }

          // 太陽の情報を更新
          sun.Update(dt);
          DateTime sRise = sun.GetSunRiseTime();
          DateTime sSet = sun.GetSunSetTime();
          if (sRise.Hour == sun.CurrentDateTime.Hour) sun.Update(dt.AddMinutes(30 + 0.5 * sRise.Minute));
          else if (sSet.Hour == sun.CurrentDateTime.Hour) sun.Update(dt.AddMinutes(0.5 * sSet.Minute));
          sun.DirectNormalRadiation = iDn;
          sun.DiffuseHorizontalRadiation = iSky;
          sun.GlobalHorizontalRadiation = iHol;

          // 傾斜面日射のログ
          for (int i = 0; i < Math.Min(5, incs.Length); i++)
            sWriter.Write("," + incs[i].GetSolarIrradiance(sun, mRoom.Albedo));

          // 予備計算（起動時の24時間暖機運転）
          if (isStarting)
          {
            bModel.ControlDryBulbTemperature(0, 0, 20);
            if (testCase == TestCase.C960) bModel.ControlHeatSupply(0, 1, 0); // SunZoneはFreeFloat
            for (int i = 0; i < 24; i++)
            {
              bModel.ForecastHeatTransfer();
              bModel.FixState();
            }
            isStarting = false;
          }

          // 熱流の向きに応じた室内側対流熱伝達率の更新
          // （SunspaceとGroundCouplingは非対応）
          if (walls[0].Temperatures[walls[0].NodeCount - 1] < zones[0].Temperature)
            walls[0].ConvectiveCoefficientB = 6.13 - 5.13;
          else
            walls[0].ConvectiveCoefficientB = 9.26 - 5.13;
          if (walls[1].Temperatures[walls[1].NodeCount - 1] < zones[0].Temperature)
            walls[1].ConvectiveCoefficientB = 9.26 - 5.13;
          else
            walls[1].ConvectiveCoefficientB = 6.13 - 5.13;

          // 室温制御
          bModel.ControlHeatSupply(0, 0, 0); // まず自然室温を予測
          bModel.ForecastHeatTransfer();
          if (isBangBang)
          {
            bModel.ControlDryBulbTemperature(0, 0, 20);
          }
          else if (isDeadBand)
          {
            if (zones[0].Temperature < 20) bModel.ControlDryBulbTemperature(0, 0, 20);
            else if (27 < zones[0].Temperature) bModel.ControlDryBulbTemperature(0, 0, 27);
          }
          else if (isSetBack)
          {
            if (27 < zones[0].Temperature)
              bModel.ControlDryBulbTemperature(0, 0, 27);
            else if ((7 <= dt.Hour && dt.Hour < 23) && zones[0].Temperature < 20)
              bModel.ControlDryBulbTemperature(0, 0, 20);
            else if (zones[0].Temperature < 10)
              bModel.ControlDryBulbTemperature(0, 0, 10);
          }
          else if (testCase == TestCase.C650 || testCase == TestCase.C950)
          {
            if ((7 <= dt.Hour && dt.Hour < 18) && 27 < zones[0].Temperature)
              bModel.ControlDryBulbTemperature(0, 0, 27);
          }
          else if (testCase == TestCase.C900_J2)
          {
            if (8 <= dt.Hour && dt.Hour < 17)
            {
              if (zones[0].Temperature < 20) bModel.ControlDryBulbTemperature(0, 0, 20);
              else if (27 < zones[0].Temperature) bModel.ControlDryBulbTemperature(0, 0, 27);
            }
          }

          // 状態確定
          bModel.ForecastHeatTransfer();
          bModel.FixState();

          // CSV書き出し
          sWriter.Write("," + zones[0].Temperature + "," + zones[0].HeatSupply);
          if (0 < windows.Length)
          {
            double rad1 =
              windows[0].OutsideIncline.GetDirectSolarIrradiance(sun)
                * windows[0].DirectSolarIncidentTransmittance
                * (1 - windows[0].SunShade.GetShadowRate(sun))
              + windows[0].OutsideIncline.GetDiffuseSolarIrradiance(sun, mRoom.Albedo)
                * windows[0].DiffuseSolarIncidentTransmittance;
            double rad2 =
              windows[1].OutsideIncline.GetDirectSolarIrradiance(sun)
                * windows[1].DirectSolarIncidentTransmittance
                * (1 - windows[1].SunShade.GetShadowRate(sun))
              + windows[1].OutsideIncline.GetDiffuseSolarIrradiance(sun, mRoom.Albedo)
                * windows[1].DiffuseSolarIncidentTransmittance;
            sWriter.Write("," + rad1 + "," + rad2);
          }
          else sWriter.Write(",-,-");

          if (testCase == TestCase.C960)
            sWriter.Write("," + zones[1].Temperature + "," + zones[1].HeatSupply);

          sWriter.WriteLine();
          dt = dt.AddHours(1);
        }
      }
      Console.WriteLine("Done.");
    }

    #endregion

    #region 気象データ変換処理

    /// <summary>TMY1形式のCSVをBESTEST用気象ファイルに変換する。</summary>
    public static void MakeBESTestWeatherFile(string inputFile, string outputFile)
    {
      string[] sl;
      using (StreamReader sr = new StreamReader(inputFile))
        sl = sr.ReadToEnd().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

      using (StreamWriter sw = new StreamWriter(outputFile, false, Encoding.GetEncoding("Shift_JIS")))
      {
        sw.WriteLine("日付,時刻,乾球温度[C],絶対湿度[kg/kg],法線面直達日射[W/m2],水平面全天日射[W/m2],水平面拡散日射[W/m2],雲量[-],夜間放射[W/m2],太陽高度[radian],地中温度(0.675m)[C],地中温度(1.35m)[C],地中温度(2.35m)[C]");

        Sun sun = new Sun(39 + 8d / 60d, 360 - 104 + 9d / 60d, 360 - 105);
        DateTime dTime;

        // 年平均気温・年較差・最大平均気温日を計算
        double dbt;
        double dbtAve = 0;
        double dbtMin = 100;
        double dbtMax = -100;
        double dbtMinSum = 0;
        double dbtMaxSum = 0;
        double maxDay = 1;
        double daySum = 0;
        double maxDaySum = 0;
        double[] maxAve = new double[12];
        double[] minAve = new double[12];
        dTime = new DateTime(1999, 1, 1, 0, 0, 0);
        int days = 0;

        for (int i = 1; i < sl.Length; i++)
        {
          string[] strs = sl[i].Split(',');
          dbt = double.Parse(strs[11]);
          dbtAve += dbt;
          dbtMin = Math.Min(dbt, dbtMin);
          dbtMax = Math.Max(dbt, dbtMax);
          daySum += dbt;

          if (dTime.Hour == 23)
          {
            days++;
            dbtMaxSum += dbtMax;
            dbtMinSum += dbtMin;
            dbtMax = -100;
            dbtMin = 100;
            if (maxDaySum < daySum) { maxDaySum = daySum; maxDay = (i / 24) + 1; }
            daySum = 0;
          }
          dTime = dTime.AddHours(1);

          if (dTime.Day == 1 && dTime.Hour == 0)
          {
            int mIdx = dTime.Month == 1 ? 11 : dTime.Month - 2;
            maxAve[mIdx] = dbtMaxSum / days;
            minAve[mIdx] = dbtMinSum / days;
            dbtMaxSum = dbtMinSum = 0;
            days = 0;
          }
        }
        dbtAve /= 8760d;
        foreach (double v in maxAve) dbtMax = Math.Max(v, dbtMax);
        foreach (double v in minAve) dbtMin = Math.Min(v, dbtMin);

        // 気象データ行を書き出す
        for (int i = 1; i < sl.Length; i++)
        {
          string[] strs = sl[i].Split(',');
          dTime = new DateTime(
            int.Parse(strs[0]), int.Parse(strs[1]), int.Parse(strs[2]),
            int.Parse(strs[3]), int.Parse(strs[4]), 0);
          sun.Update(dTime.AddMinutes(30));
          sw.Write(dTime.ToShortDateString() + "," + dTime.ToShortTimeString() + ",");

          dbt = double.Parse(strs[11]);
          sw.Write(dbt + ",");

          double dpt = double.Parse(strs[12]);
          double atm = double.Parse(strs[10]);
          double ahd = MoistAir.GetSaturationHumidityRatioFromDryBulbTemperature(dpt, atm);
          sw.Write(ahd + ",");

          sun.DirectNormalRadiation = strs[5] == "" ? 0 : double.Parse(strs[5]);
          sun.GlobalHorizontalRadiation = strs[7] == "" ? 0 : double.Parse(strs[7]);
          sw.Write(sun.DirectNormalRadiation + ",");
          sw.Write(sun.GlobalHorizontalRadiation + ",");

          // 日の出・日没の補正
          DateTime sRise = sun.GetSunRiseTime();
          DateTime sSet = sun.GetSunSetTime();
          if (sRise.Hour == sun.CurrentDateTime.Hour) sun.Update(dTime.AddMinutes(30 + 0.5 * sRise.Minute));
          if (sSet.Hour == sun.CurrentDateTime.Hour) sun.Update(dTime.AddMinutes(0.5 * sSet.Minute));
          sw.Write(Math.Max(0, Sun.GetDiffuseHorizontalRadiation(
            sun.DirectNormalRadiation, sun.GlobalHorizontalRadiation, sun.Altitude)) + ",");

          int cc = (int)(double.Parse(strs[15]) * 10);
          if (cc > 10) cc = 10;
          sw.Write(cc + ",");

          double wbp = MoistAir.GetWaterVaporPartialPressureFromHumidityRatio(ahd, atm);
          double nr = NO_NOC_RAD ? 0 : Sky.GetNocturnalRadiation(dbt, cc, wbp);
          sw.Write(nr + ",");

          sw.Write(sun.Altitude + ",");

          // 地中温度[C]（Kusuda-Achenbach式）
          double nd = dTime.DayOfYear + dTime.Hour / 24d + (dTime.Minute + 30d) / 24d / 60d;
          sw.Write(GroundTemp(dbtAve, dbtMax, dbtMin, nd, maxDay, 0.675) + ",");
          sw.Write(GroundTemp(dbtAve, dbtMax, dbtMin, nd, maxDay, 1.35) + ",");
          sw.Write(GroundTemp(dbtAve, dbtMax, dbtMin, nd, maxDay, 2.65) + ",");
          sw.WriteLine();
        }
      }
    }

    /// <summary>Kusuda-Achenbach式で地中温度[°C]を計算する。</summary>
    private static double GroundTemp(double ave, double max, double min, double dayOfYear, double maxDay, double depth)
    {
      return ave + 0.5 * (max - min)
        * Math.Exp(-0.526 * depth)
        * Math.Cos((dayOfYear - maxDay - 30.556 * depth) * 0.017214);
    }

    #endregion

    #region 建物モデル作成処理

    private static void MakeBuilding(TestCase tCase,
      out MultiRooms mRoom, out Zone[] zones, out Wall[] walls, out Window[] windows)
    {
      if (tCase == TestCase.C960)
      {
        MakeSunZoneBuilding(out mRoom, out zones, out walls, out windows);
        return;
      }
      if (tCase == TestCase.C990)
      {
        MakeGroundCouplingBuilding(out mRoom, out zones, out walls, out windows);
        return;
      }

      bool hasEWWindow = (tCase & TestCase.HasEWWindow) == tCase;
      bool hasSunShade = (tCase & TestCase.HasSunShade) == tCase;
      bool hasHeatGain = (tCase & TestCase.HasHeatGain) == tCase;
      bool hasOpaqueWindow = (tCase & TestCase.HasOpaqueWindow) == tCase;
      bool isLowIntIREmissivity = (tCase & TestCase.LowIntIREmissivity) == tCase;
      bool isLowExtIREmissivity = (tCase & TestCase.LowExtIREmissivity) == tCase;
      bool isLowIntSWEmissivity = (tCase & TestCase.LowIntSWEmissivity) == tCase;
      bool isHighIntSWEmissivity = (tCase & TestCase.HighIntSWEmissivity) == tCase;
      bool noInfiltration = (tCase & TestCase.NoInfiltration) == tCase;
      bool isLowExtSWEmissivity = (tCase & TestCase.LowExtSWEmissivity) == tCase;
      bool isHeavyWeight = (tCase & TestCase.HeavyWeight) == tCase;
      bool noWindow = (tCase & TestCase.NoWindow) == tCase;

      // 放射率・吸収率・表面熱伝達率
      double extswAbsorptance, extlwEmissivity, intswAbsorptance, intlwEmissivity, aowal, aowin;
      if (isLowExtIREmissivity) { extlwEmissivity = 0.1; aowal = 25.2; aowin = 16.9; }
      else { extlwEmissivity = 0.9; aowal = 29.3; aowin = 21.0; }
      if (tCase == TestCase.C250) extswAbsorptance = 0.9;
      else if (isLowExtSWEmissivity) extswAbsorptance = 0.1;
      else extswAbsorptance = 0.6;
      intlwEmissivity = isLowIntIREmissivity ? 0.1 : 0.9;
      if (isLowIntSWEmissivity) intswAbsorptance = 0.1;
      else if (isHighIntSWEmissivity) intswAbsorptance = 0.9;
      else intswAbsorptance = 0.6;

      // ゾーン
      zones = new Zone[1];
      zones[0] = new Zone("Zn1", 8 * 6 * 2.7 * AIR_DNS);
      zones[0].InitializeAirState(20, 0);
      if (hasHeatGain) zones[0].AddHeatGain(new SimpleHeatGain(200 * 0.4, 200 * 0.6, 0));
      if (tCase == TestCase.C230) zones[0].VentilationRate = zones[0].AirMass / 3600d;
      else if (noInfiltration) zones[0].VentilationRate = 0;
      else zones[0].VentilationRate = zones[0].AirMass * 0.5 / 3600d;

      // 壁
      WallLayer[] exwL, flwL, rfwL;
      MakeWallLayer(tCase, out exwL, out flwL, out rfwL);
      walls = new Wall[6];
      walls[0] = new Wall(48, flwL);  // 床
      walls[1] = new Wall(48, rfwL);  // 屋根
      walls[2] = new Wall(8 * 2.7, exwL);  // 北外壁
      walls[3] = new Wall(hasEWWindow ? 6 * 2.7 - 6 : 6 * 2.7, exwL); // 東外壁
      walls[4] = new Wall(hasEWWindow ? 6 * 2.7 - 6 : 6 * 2.7, exwL); // 西外壁
      walls[5] = new Wall(
        (noWindow || hasEWWindow) ? 8 * 2.7 : 8 * 2.7 - 6d - 6d, exwL); // 南外壁

      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ConvectiveCoefficientF = aowal;
        walls[i].RadiativeCoefficientF = 0;
        walls[i].Initialize(25);
        walls[i].ShortWaveAbsorptanceF = extswAbsorptance;
        walls[i].LongWaveEmissivityF = extlwEmissivity;
        walls[i].ShortWaveAbsorptanceB = intswAbsorptance;
        walls[i].LongWaveEmissivityB = intlwEmissivity;
        walls[i].ConvectiveCoefficientB = 3.16;
      }

      // 窓
      if (!noWindow)
      {
        windows = new Window[2];
        Incline[] inc = hasEWWindow
          ? new[] { new Incline(Incline.Orientation.E, 0.5 * Math.PI),
                    new Incline(Incline.Orientation.W, 0.5 * Math.PI) }
          : new[] { new Incline(Incline.Orientation.S, 0.5 * Math.PI),
                    new Incline(Incline.Orientation.S, 0.5 * Math.PI) };

        for (int i = 0; i < 2; i++)
        {
          windows[i] = hasOpaqueWindow
            ? new Window(6, new[] { 0.0, 0.0 }, new[] { 1 - extswAbsorptance, 0.0 }, inc[i])
            : new Window(6,
                new[] { 0.861563, 0.861563 }, new[] { 0.043362, 0.043362 },
                new[] { 0.861563, 0.861563 }, new[] { 0.043362, 0.043362 },
                inc[i]);

          windows[i].SetGlassResistance(0, 0.003);
          windows[i].SetGlassResistance(1, 0.003);
          windows[i].SetAirGapResistance(0, 0.1588);
          windows[i].LongWaveEmissivityF = extlwEmissivity;
          windows[i].ConvectiveCoefficientF = aowin;
          windows[i].RadiativeCoefficientF = 0;
          windows[i].ConvectiveCoefficientB = 3.16;
          SetBESTESTWindowAngleDependence(windows[i]);

          if (tCase == TestCase.C900_J3)
            windows[i].SetShadingDevice(2,
              new SimpleShadingDevice(SimpleShadingDevice.PredefinedDevices.BrightVenetianBlind));
        }

        if (hasSunShade)
        {
          if (hasEWWindow)
          {
            windows[0].SunShade = SunShade.MakeGridSunShade(3, 2, 1, 0, 0, 0, 0, inc[0]);
            windows[1].SunShade = SunShade.MakeGridSunShade(3, 2, 1, 0, 0, 0, 0, inc[1]);
          }
          else
          {
            windows[0].SunShade = SunShade.MakeHorizontalSunShade(3, 2, 1, 4.5, 0.5, 0.5, inc[0]);
            windows[1].SunShade = SunShade.MakeHorizontalSunShade(3, 2, 1, 0.5, 4.5, 0.5, inc[1]);
          }
        }
      }
      else windows = new Window[0];

      // 多数室
      mRoom = new MultiRooms(1, zones, walls, windows);
      mRoom.TimeStep = 3600;
      mRoom.Albedo = 0.2;
      mRoom.AddZone(0, 0);

      // 屋外表面設定
      mRoom.SetGroundWall(0, true, 10000); // 床：断熱材の向こう側は土壌温度に固定
      mRoom.SetOutsideWall(1, true, INC_H);
      mRoom.SetOutsideWall(2, true, INC_N);
      mRoom.SetOutsideWall(3, true, INC_E);
      mRoom.SetOutsideWall(4, true, INC_W);
      mRoom.SetOutsideWall(5, true, INC_S);

      // 壁・窓をゾーンに追加
      for (int i = 0; i < walls.Length; i++) mRoom.AddWall(0, i, false);
      for (int i = 0; i < windows.Length; i++)
      {
        mRoom.AddWindow(0, i);
        // BESTESTでは全日射がまず床に当たると仮定
        mRoom.SetSWDistributionRateToFloor(i, 0, false, 1.0);
      }
    }

    private static void MakeSunZoneBuilding(
      out MultiRooms mRoom, out Zone[] zones, out Wall[] walls, out Window[] windows)
    {
      const double aowal = 29.3;
      const double aowin = 21.0;
      const double extswEmissivity = 0.6;
      const double extlwEmissivity = 0.9;
      const double intswEmissivity = 0.6;
      const double intlwEmissivity = 0.9;

      // ゾーン
      zones = new Zone[2];
      zones[0] = new Zone("BackZone", 8 * 6 * 2.7 * AIR_DNS);
      zones[0].AddHeatGain(new SimpleHeatGain(200 * 0.4, 200 * 0.6, 0));
      zones[0].InitializeAirState(20, 0);
      zones[0].VentilationRate = zones[0].AirMass * 0.5 / 3600;
      zones[1] = new Zone("SunZone", 8 * 2 * 2.7 * AIR_DNS);
      zones[1].InitializeAirState(20, 0);
      zones[1].VentilationRate = zones[1].AirMass * 0.5 / 3600;

      // 壁
      WallLayer[] exwL, flwL, rfwL;
      MakeWallLayer(TestCase.C960, out exwL, out flwL, out rfwL);
      WallLayer[] cwL = new[] { new WallLayer("CommonWall", 0.510, 1400d * 1000d / 1000d, 0.2) };

      walls = new Wall[11];
      walls[0] = new Wall(48, flwL); // 床1
      walls[1] = new Wall(16, flwL); // 床2
      walls[2] = new Wall(48, rfwL); // 屋根1
      walls[3] = new Wall(16, rfwL); // 屋根2
      walls[4] = new Wall(8 * 2.7, exwL); // 北外壁
      walls[5] = new Wall(8 * 2.7 - 6 - 6, exwL); // 南外壁
      walls[6] = new Wall(6 * 2.7, exwL); // 東外壁1
      walls[7] = new Wall(2 * 2.7, exwL); // 東外壁2
      walls[8] = new Wall(6 * 2.7, exwL); // 西外壁1
      walls[9] = new Wall(2 * 2.7, exwL); // 西外壁2
      walls[10] = new Wall(8 * 2.7, cwL);  // 共用壁

      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ConvectiveCoefficientF = aowal;
        walls[i].RadiativeCoefficientF = 0;
        walls[i].Initialize(25);
        walls[i].ShortWaveAbsorptanceF = extswEmissivity;
        walls[i].LongWaveEmissivityF = extlwEmissivity;
        walls[i].ShortWaveAbsorptanceB = intswEmissivity;
        walls[i].LongWaveEmissivityB = intlwEmissivity;
        walls[i].ConvectiveCoefficientB = 3.16;
      }
      walls[10].ConvectiveCoefficientF = 3.16;
      walls[10].ShortWaveAbsorptanceF = intswEmissivity;
      walls[10].LongWaveEmissivityF = intlwEmissivity;

      // 窓
      windows = new Window[2];
      for (int i = 0; i < 2; i++)
      {
        windows[i] = new Window(6,
          new[] { 0.861563, 0.861563 }, new[] { 0.043362, 0.043362 }, INC_S);
        windows[i].SetGlassResistance(0, 0.003);
        windows[i].SetGlassResistance(1, 0.003);
        windows[i].SetAirGapResistance(0, 0.1588);
        windows[i].LongWaveEmissivityF = extlwEmissivity;
        windows[i].ConvectiveCoefficientF = aowin;
        windows[i].RadiativeCoefficientF = 0;
        windows[i].ConvectiveCoefficientB = 3.16;
        SetBESTESTWindowAngleDependence(windows[i]);
      }

      // 多数室（2室）
      mRoom = new MultiRooms(2, zones, walls, windows);
      mRoom.TimeStep = 3600;
      mRoom.Albedo = 0.2;
      mRoom.AddZone(0, 0);
      mRoom.AddZone(1, 1);

      // 屋外表面設定
      mRoom.SetGroundWall(0, true, 10000);
      mRoom.SetGroundWall(1, true, 10000);
      mRoom.SetOutsideWall(2, true, INC_H);
      mRoom.SetOutsideWall(3, true, INC_H);
      mRoom.SetOutsideWall(4, true, INC_N);
      mRoom.SetOutsideWall(5, true, INC_S);
      mRoom.SetOutsideWall(6, true, INC_E);
      mRoom.SetOutsideWall(7, true, INC_E);
      mRoom.SetOutsideWall(8, true, INC_W);
      mRoom.SetOutsideWall(9, true, INC_W);

      // SunZoneに窓を追加
      mRoom.AddWindow(1, 0);
      mRoom.AddWindow(1, 1);
      mRoom.SetSWDistributionRateToFloor(0, 1, false, 1.0);
      mRoom.SetSWDistributionRateToFloor(1, 1, false, 1.0);

      // BackZoneに壁を追加
      mRoom.AddWall(0, 0, false);
      mRoom.AddWall(0, 2, false);
      mRoom.AddWall(0, 4, false);
      mRoom.AddWall(0, 6, false);
      mRoom.AddWall(0, 8, false);
      mRoom.AddWall(0, 10, true);

      // SunZoneに壁を追加
      mRoom.AddWall(1, 1, false);
      mRoom.AddWall(1, 3, false);
      mRoom.AddWall(1, 5, false);
      mRoom.AddWall(1, 7, false);
      mRoom.AddWall(1, 9, false);
      mRoom.AddWall(1, 10, false);
    }

    private static void MakeGroundCouplingBuilding(
      out MultiRooms mRoom, out Zone[] zones, out Wall[] walls, out Window[] windows)
    {
      const double aowal = 29.3;
      const double aowin = 21.0;
      const double extswEmissivity = 0.6;
      const double extlwEmissivity = 0.9;
      const double intswEmissivity = 0.6;
      const double intlwEmissivity = 0.9;

      // ゾーン
      zones = new Zone[1];
      zones[0] = new Zone("Zn1", 8 * 6 * 2.7 * AIR_DNS);
      zones[0].AddHeatGain(new SimpleHeatGain(200 * 0.4, 200 * 0.6, 0));
      zones[0].InitializeAirState(20, 0);
      zones[0].VentilationRate = zones[0].AirMass * 0.5 / 3600;

      // 壁
      WallLayer[] exwL, flwL, rfwL;
      MakeWallLayer(TestCase.C990, out exwL, out flwL, out rfwL);
      // 床は土壌層を追加
      flwL = new WallLayer[]
      {
        new WallLayer("Ground",          1.3,   800d * 1500d / 1000d, 2.0),
        new WallLayer("Concrete Slab",   1.130, 1400d * 1000d / 1000d, 0.08 / 3),
        new WallLayer("Concrete Slab",   1.130, 1400d * 1000d / 1000d, 0.08 / 3),
        new WallLayer("Concrete Slab",   1.130, 1400d * 1000d / 1000d, 0.08 / 3),
      };

      walls = new Wall[9];
      walls[0] = new Wall(48, flwL); // 床
      walls[1] = new Wall(48, rfwL); // 屋根
      walls[2] = new Wall(8 * 1.35, exwL); // 北外壁（地上）
      walls[3] = new Wall(8 * 1.35, flwL); // 北外壁（地下）
      walls[4] = new Wall(6 * 1.35, exwL); // 東外壁（地上）
      walls[5] = new Wall(6 * 1.35, flwL); // 東外壁（地下）
      walls[6] = new Wall(6 * 1.35, exwL); // 西外壁（地上）
      walls[7] = new Wall(6 * 1.35, flwL); // 西外壁（地下）
      walls[8] = new Wall(8 * 1.35, flwL); // 南外壁（地下）

      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ConvectiveCoefficientF = aowal;
        walls[i].RadiativeCoefficientF = 0;
        walls[i].Initialize(25);
        walls[i].ShortWaveAbsorptanceF = extswEmissivity;
        walls[i].LongWaveEmissivityF = extlwEmissivity;
        walls[i].ShortWaveAbsorptanceB = intswEmissivity;
        walls[i].LongWaveEmissivityB = intlwEmissivity;
        walls[i].ConvectiveCoefficientB = 3.16;
      }

      // 窓
      windows = new Window[2];
      for (int i = 0; i < 2; i++)
      {
        windows[i] = new Window(5.4,
          new[] { 0.861563, 0.861563 }, new[] { 0.043362, 0.043362 }, INC_S);
        windows[i].SetGlassResistance(0, 0.003);
        windows[i].SetGlassResistance(1, 0.003);
        windows[i].SetAirGapResistance(0, 0.1588);
        windows[i].LongWaveEmissivityF = extlwEmissivity;
        windows[i].ConvectiveCoefficientF = aowin;
        windows[i].RadiativeCoefficientF = 0;
        windows[i].ConvectiveCoefficientB = 3.16;
        SetBESTESTWindowAngleDependence(windows[i]);
      }

      // 多数室
      mRoom = new MultiRooms(1, zones, walls, windows);
      mRoom.TimeStep = 3600;
      mRoom.Albedo = 0.2;
      mRoom.AddZone(0, 0);

      // 屋外表面設定
      mRoom.SetGroundWall(0, true, 10000);
      mRoom.SetOutsideWall(1, true, INC_H);
      mRoom.SetOutsideWall(2, true, INC_N);
      mRoom.SetGroundWall(3, true, 10000);
      mRoom.SetOutsideWall(4, true, INC_E);
      mRoom.SetGroundWall(5, true, 10000);
      mRoom.SetOutsideWall(6, true, INC_W);
      mRoom.SetGroundWall(7, true, 10000);
      mRoom.SetGroundWall(8, true, 10000);

      // 窓と壁をゾーンに追加
      mRoom.AddWindow(0, 0);
      mRoom.AddWindow(0, 1);
      mRoom.SetSWDistributionRateToFloor(0, 0, false, 1.0);
      mRoom.SetSWDistributionRateToFloor(1, 0, false, 1.0);
      for (int i = 0; i < walls.Length; i++) mRoom.AddWall(0, i, false);
    }

    #endregion

    #region 壁層作成処理

    private static void MakeWallLayer(TestCase tCase,
      out WallLayer[] exterior, out WallLayer[] floor, out WallLayer[] roof)
    {
      bool isLightWeight = !((tCase & TestCase.HeavyWeight) == tCase);

      if (isLightWeight)
      {
        exterior = new WallLayer[]
        {
          new WallLayer("Wood Siding",      0.140, 530d  * 900d  / 1000d, 0.009),
          new WallLayer("Fibreglas quilt",  0.040, 12d   * 840d  / 1000d, 0.066),
          new WallLayer("Plasterboard",     0.160, 950d  * 840d  / 1000d, 0.012),
        };
        floor = new WallLayer[]
        {
          new WallLayer("Insulation1",      0.040, 0.0001,                0.500),
          new WallLayer("Insulation2",      0.040, 0.0001,                0.5003),
          new WallLayer("Timber flooring",  0.140, 650d  * 1200d / 1000d, 0.025),
        };
        roof = new WallLayer[]
        {
          new WallLayer("Roofdeck",         0.140, 530d  * 900d  / 1000d, 0.019),
          new WallLayer("Fibreglas quilt",  0.040, 12d   * 840d  / 1000d, 0.1118),
          new WallLayer("Plasterboard",     0.160, 950d  * 840d  / 1000d, 0.010),
        };
        return;
      }

      // 重量系
      if ((tCase & TestCase.JapaneseInsulation) == tCase)
      {
        exterior = new WallLayer[]
        {
          new WallLayer("タイル",               1.30,  2400d * 833d  / 1000d, 0.010),
          new WallLayer("セメント・モルタル",   1.50,  2000d * 800d  / 1000d, 0.025),
          new WallLayer("コンクリート",         1.60,  2300d * 870d  / 1000d, 0.150),
          new WallLayer("断熱材",               0.04,  25d   * 1320d / 1000d, 0.025),
          new AirGapLayer("空気層", false, 0.01),
          new WallLayer("せっこうボード",       0.220, 750d  * 1107d / 1000d, 0.008),
        };
        floor = new WallLayer[]
        {
          new WallLayer("Insulation1",     0.040, 0.0001,                0.500),
          new WallLayer("Insulation2",     0.040, 0.0001,                0.5003),
          new WallLayer("Timber flooring", 0.140, 650d * 1200d / 1000d,  0.025),
        };
        roof = new WallLayer[]
        {
          new WallLayer("コンクリート",         1.60,  2300d * 870d  / 1000d, 0.060),
          new WallLayer("断熱材",               0.04,  25d   * 1320d / 1000d, 0.050),
          new WallLayer("セメント・モルタル",   1.50,  2000d * 800d  / 1000d, 0.015),
          new WallLayer("アスファルト類",       0.110, 1000d * 920d  / 1000d, 0.005),
          new WallLayer("セメント・モルタル",   1.50,  2000d * 800d  / 1000d, 0.015),
          new WallLayer("コンクリート",         1.60,  2300d * 870d  / 1000d, 0.150),
          new AirGapLayer("空気層", false, 0.01),
          new WallLayer("せっこうボード",       0.220, 750d  * 1107d / 1000d, 0.010),
          new WallLayer("岩綿吸音板",           0.064, 350d  * 829d  / 1000d, 0.012),
        };
        return;
      }

      // 重量系・標準
      if (tCase == TestCase.C900_J1_1)
      {
        exterior = new WallLayer[]
        {
          new WallLayer("Wood Siding",     0.140, 530d  * 900d  / 1000d, 0.009),
          new WallLayer("Concrete Block",  0.510, 1400d * 1000d / 1000d, 0.1 / 3),
          new WallLayer("Concrete Block",  0.510, 1400d * 1000d / 1000d, 0.1 / 3),
          new WallLayer("Concrete Block",  0.510, 1400d * 1000d / 1000d, 0.1 / 3),
          new WallLayer("Foam Insulation", 0.040, 10d   * 1400d / 1000d, 0.0615),
        };
      }
      else
      {
        exterior = new WallLayer[]
        {
          new WallLayer("Wood Siding",     0.140, 530d  * 900d  / 1000d, 0.009),
          new WallLayer("Foam Insulation", 0.040, 10d   * 1400d / 1000d, 0.0615),
          new WallLayer("Concrete Block",  0.510, 1400d * 1000d / 1000d, 0.1 / 3),
          new WallLayer("Concrete Block",  0.510, 1400d * 1000d / 1000d, 0.1 / 3),
          new WallLayer("Concrete Block",  0.510, 1400d * 1000d / 1000d, 0.1 / 3),
        };
      }
      floor = new WallLayer[]
      {
        new WallLayer("Insulation1",     0.040, 0.001,                 0.500),
        new WallLayer("Insulation2",     0.040, 0.001,                 0.5003),
        new WallLayer("Concrete Slab",   1.130, 1400d * 1000d / 1000d, 0.08 / 3),
        new WallLayer("Concrete Slab",   1.130, 1400d * 1000d / 1000d, 0.08 / 3),
        new WallLayer("Concrete Slab",   1.130, 1400d * 1000d / 1000d, 0.08 / 3),
      };
      roof = new WallLayer[]
      {
        new WallLayer("Roofdeck",        0.140, 530d  * 900d  / 1000d, 0.019),
        new WallLayer("Fibreglas quilt", 0.040, 12d   * 840d  / 1000d, 0.1118),
        new WallLayer("Plasterboard",    0.160, 950d  * 840d  / 1000d, 0.010),
      };
    }

    #endregion

    #region 窓入射角特性設定

    /// <summary>BESTESTの近似係数を窓の両層に設定する。</summary>
    private static void SetBESTESTWindowAngleDependence(Window win)
    {
      double[] tau = new[] { 3.382, -3.103, -1.759, 4.345, -1.865 };
      double[] rho = new[] { 5.612395, -14.295921, 19.782858, -14.202927, 4.104435 };
      win.SetAngleDependence(0, tau, tau, rho, rho);
      win.SetAngleDependence(1, tau, tau, rho, rho);
    }

    #endregion

    #region Excelファイル作成処理

    private static void MakeBESTResultExcelSheet(string templatePath)
    {
      const string folderName = "Result";
      if (!Directory.Exists(folderName)) return;

      // テンプレートをResultフォルダにコピー
      string destPath = Path.Combine(folderName, "Result.xlsx");
      if (!File.Exists(destPath)) File.Copy(templatePath, destPath);

      using (SpreadsheetDocument doc = SpreadsheetDocument.Open(
               destPath, true, new OpenSettings { AutoSave = false }))
      {
        WorkbookPart wbp = doc.WorkbookPart!;
        string[] csvFiles = Directory.GetFiles(folderName, "*.csv");

        foreach (string fn in csvFiles)
        {
          Console.WriteLine("Converting " + fn);
          using (StreamReader sr = new StreamReader(fn))
          {
            sr.ReadLine(); // ヘッダスキップ
            string sheetName = Path.GetFileNameWithoutExtension(fn);
            bool isC600 = sheetName is "C600" or "C610" or "C620" or "C630";
            bool isC960 = sheetName == "C960";

            Sheet? sheet = wbp.Workbook.GetFirstChild<Sheets>()!
              .Elements<Sheet>().FirstOrDefault(s => s.Name == sheetName);
            if (sheet == null) { Console.WriteLine($"  Sheet '{sheetName}' not found. Skipped."); continue; }

            WorksheetPart wsp = (WorksheetPart)wbp.GetPartById(sheet.Id!.Value!);
            Worksheet ws = wsp.Worksheet;

            for (int i = 0; i < 8760; i++)
            {
              string[] ss = sr.ReadLine()!.Split(',');
              if (ss[6] != "-")
              {
                GetCell(ws, "B", (uint)(2 + i)).CellValue = new CellValue(ss[6]);
                GetCell(ws, "C", (uint)(2 + i)).CellValue = new CellValue(ss[7]);
              }
              if (isC600)
              {
                GetCell(ws, "I", (uint)(2 + i)).CellValue = new CellValue(ss[1]);
                GetCell(ws, "J", (uint)(2 + i)).CellValue = new CellValue(ss[2]);
                GetCell(ws, "K", (uint)(2 + i)).CellValue = new CellValue(ss[3]);
                GetCell(ws, "L", (uint)(2 + i)).CellValue = new CellValue(ss[4]);
                GetCell(ws, "M", (uint)(2 + i)).CellValue = new CellValue(ss[5]);
                GetCell(ws, "N", (uint)(2 + i)).CellValue = new CellValue(ss[8]);
                GetCell(ws, "O", (uint)(2 + i)).CellValue = new CellValue(ss[9]);
              }
              else if (isC960)
              {
                GetCell(ws, "I", (uint)(2 + i)).CellValue = new CellValue(ss[10]);
              }
            }
            ws.Save();
          }
          File.Delete(fn);
        }

        // 開いたとき再計算させる
        CalculationProperties cp = doc.WorkbookPart!.Workbook.CalculationProperties!;
        cp.CalculationMode = new EnumValue<CalculateModeValues>(CalculateModeValues.Auto);
        cp.FullCalculationOnLoad = true;
        doc.Save();
      }
    }

    private static Cell GetCell(Worksheet worksheet, string col, uint row)
    {
      string cellRef = col + row;
      Row? r = worksheet.GetFirstChild<SheetData>()!
        .Elements<Row>().FirstOrDefault(x => x.RowIndex == row);
      return r!.Elements<Cell>().First(c => c.CellReference!.Value == cellRef);
    }

    #endregion

  }
}