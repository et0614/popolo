/* VRFNEDOTestDemo.cs
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
using System.Globalization;

using Popolo.Core.HVAC.VRF;
using Popolo.Core.Physics;

namespace Popolo.Samples.Demos.Core
{
  /// <summary>
  /// Runs the NEDO test-case comparison for a Daikin VRF system.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Builds a Daikin-22.4 kW outdoor unit paired with four 5.6 kW indoor
  /// units and runs two suites of cases:
  /// </para>
  /// <list type="number">
  ///   <item><description>
  ///     Cooling mode (NOM, ML, MLMT, and NEDO-measured patterns 22–35) at
  ///     JIS rated outdoor conditions (35 °C / 24 °C WB) and mid-temperature
  ///     conditions (29 °C / 19 °C WB) for the MLMT/27/28/29 cases.
  ///   </description></item>
  ///   <item><description>
  ///     Heating mode (NOM, ML, and NEDO-measured patterns 8–21) at JIS
  ///     rated outdoor conditions (7 °C / 6 °C WB) and cold-climate
  ///     conditions (2 °C / 1 °C WB) for the 13–15 cases.
  ///   </description></item>
  /// </list>
  /// <para>
  /// For each case, one CSV line is written to standard output with columns:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>Cooling: <c>case, loadSum, totalHeat, PLR, Pin [MPa], Pout [MPa], Welec [kW], SHR</c></description></item>
  ///   <item><description>Heating: <c>case, loadSum, totalHeat, PLR, Pin [MPa], Pout [MPa], Welec [kW], SHR, defrostLoad [kW]</c></description></item>
  /// </list>
  /// <para>
  /// <b>Usage:</b>
  /// </para>
  /// <code>
  /// dotnet run --project samples/Popolo.Samples -- vrf-nedo-test [--use-jis]
  /// </code>
  /// <para>
  /// Without arguments, the demo uses NEDO-measured performance values for
  /// the outdoor unit. Passing <c>--use-jis</c> uses the catalogue
  /// <see cref="VRFInitializer"/> data (Daikin VRV-X 22.4 kW) instead.
  /// </para>
  /// </remarks>
  public sealed class VRFNEDOTestDemo : IDemo
  {

    #region IDemo 実装

    public string Name => "vrf-nedo-test";
    public string Category => "Core";
    public string Description => "Run NEDO test cases on a Daikin 22.4 kW VRF system and print cooling/heating results.";

    public int Run(string[] args)
    {
      bool useJIS = false;
      foreach (string a in args)
      {
        if (a is "--use-jis" or "-j") useJIS = true;
        else if (a is "help" or "--help" or "-h")
        {
          Console.Error.WriteLine($"Usage: {Name} [--use-jis]");
          Console.Error.WriteLine("  --use-jis    Use catalogue (Daikin VRV-X) outdoor unit data.");
          Console.Error.WriteLine("               Without this flag, NEDO-measured performance values are used.");
          return 0;
        }
      }

      Execute(useJIS);
      return 0;
    }

    #endregion

    #region 本体ロジック

    /// <summary>Runs the NEDO test cases and writes CSV lines to standard output.</summary>
    /// <param name="useJIS">
    /// When true, builds the outdoor unit from catalogue data via <see cref="VRFInitializer"/>.
    /// When false, constructs it directly from NEDO-measured performance values.
    /// </param>
    public static void Execute(bool useJIS)
    {
      //冷媒物性計算インスタンス作成
      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);

      //室外機作成:ダイキン22.8kWをベースにNEDO試験情報を追加
      VRFSystem vrfSystem;
      if (useJIS)
      {
        vrfSystem = VRFInitializer.MakeOutdoorUnit(
          VRFInitializer.OutdoorUnitModel.Daikin_VRVX,
          VRFInitializer.CoolingCapacity.C22_4,
          indoorUnitHeight: 0, useWaterSpray: false);
      }
      else
      {
        VRFUnit iHex = VRFInitializer.MakeIndoorUnit_Daikin(
          VRFInitializer.IndoorUnitType.CeilingRoundFlow_S,
          VRFInitializer.CoolingCapacity.C11_2);
        vrfSystem = new VRFSystem(r410a,
          218 * 1.2 / 60d, 0.26 * 2, -21.04, 6.89, -9.54, 2.63, -10.58, 1.66,  // NEDO実試験 冷房
          218 * 1.2 / 60d, 0.26 * 2,  23.32, 7.49, 12.41, 3.88,                // NEDO実試験 暖房
          nominalPipeLength: 7.5,
          coolingLongPipeLength: 100, coolingPipeCorrectionFactor: 0.88,
          heatingLongPipeLength: 100, heatingPipeCorrectionFactor: 1.00,
          iHex: iHex);
        vrfSystem.MinimumPartialLoadRatio = 0.18;
      }

      //室内機リスト (4台 × 5.6 kW)
      VRFUnit[] iHexes = new VRFUnit[]
      {
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
      };
      vrfSystem.AddIndoorUnit(iHexes);
      vrfSystem.Cooling.MinEvaporatingTemperature = 5;
      vrfSystem.Cooling.MaxEvaporatingTemperature = 20;
      if (vrfSystem.Heating != null)
      {
        vrfSystem.Heating.MinCondensingTemperature = 30;
      }
      vrfSystem.ControlThermoOffWithSensibleHeat = false; //全熱基準でサーモを制御（処理負荷を完全に合わせるため）

      RunCoolingSuite(vrfSystem, iHexes);
      Console.WriteLine();
      RunHeatingSuite(vrfSystem, iHexes);
      Console.WriteLine();
    }

    #endregion

    #region 冷房テストケース

    /// <summary>Runs the cooling-mode test suite and prints one CSV line per case.</summary>
    private static void RunCoolingSuite(VRFSystem vrfSystem, VRFUnit[] iHexes)
    {
      Console.WriteLine("Cooling mode test");

      for (int i = 0; i < 4; i++)
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
      vrfSystem.CurrentMode = VRFSystem.Mode.Cooling;

      Dictionary<string, double[]> pLoadsC = CoolingLoads();
      Dictionary<string, double[]> iDBTC = CoolingInletDryBulb();
      Dictionary<string, double[]> iWBTC = CoolingInletWetBulb();

      foreach (string key in pLoadsC.Keys)
      {
        //外気条件
        bool midcnd = key is "27" or "28" or "29" or "MLMT"; //中温条件
        double oaDbt = midcnd ? 29.0 : 35.0;
        double oaWbt = midcnd ? 19.0 : 24.0;
        vrfSystem.OutdoorAirDryBulbTemperature = oaDbt;
        vrfSystem.OutdoorAirHumidityRatio =
          MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
            oaDbt, oaWbt, PhysicsConstants.StandardAtmosphericPressure);

        //室内機条件の設定
        double loadSum = 0;
        for (int i = 0; i < 4; i++)
        {
          loadSum += pLoadsC[key][i];
          double dbt_i = iDBTC[key][i];
          double hrt_i = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
            dbt_i, iWBTC[key][i], PhysicsConstants.StandardAtmosphericPressure);
          vrfSystem.SetIndoorUnitInletAirState(i, dbt_i, hrt_i);
          //給気温度
          iHexes[i].CurrentMode = pLoadsC[key][i] == 0 ? VRFUnit.Mode.ThermoOff : VRFUnit.Mode.Cooling;
          iHexes[i].SolveHeatLoad(-pLoadsC[key][i], iHexes[i].NominalAirFlowRate, dbt_i, hrt_i, false);
          vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[i].OutletAirTemperature);
          vrfSystem.SetIndoorUnitSetpointHumidityRatio(i, iHexes[i].OutletAirHumidityRatio);
        }
        vrfSystem.UpdateState();

        //集計
        double ttlHeat = 0;
        double ssbHeat = 0;
        for (int i = 0; i < 4; i++)
        {
          ttlHeat -= iHexes[i].HeatTransfer;
          ssbHeat -= iHexes[i].SensibleHeatTransfer;
        }

        WriteCsv(
          key, loadSum, ttlHeat,
          vrfSystem.PartialLoadRatio,
          vrfSystem.CompressorInletPressure,
          vrfSystem.CompressorOutletPressure,
          vrfSystem.CompressorElectricity,
          ssbHeat / ttlHeat,
          defrostLoad: null);
      }
    }

    #endregion

    #region 暖房テストケース

    /// <summary>Runs the heating-mode test suite and prints one CSV line per case.</summary>
    private static void RunHeatingSuite(VRFSystem vrfSystem, VRFUnit[] iHexes)
    {
      Console.WriteLine("Heating mode test");

      for (int i = 0; i < 4; i++)
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
      vrfSystem.CurrentMode = VRFSystem.Mode.Heating;

      Dictionary<string, double[]> pLoadsH = HeatingLoads();
      Dictionary<string, double[]> iDBTH = HeatingInletDryBulb();
      Dictionary<string, double[]> iWBTH = HeatingInletWetBulb();

      foreach (string key in pLoadsH.Keys)
      {
        //外気条件
        bool cldWin = key is "13" or "13.5" or "14" or "15"; //厳寒条件
        double oaDbt = cldWin ? 2.0 : 7.0;
        double oaWbt = cldWin ? 1.0 : 6.0;
        vrfSystem.OutdoorAirDryBulbTemperature = oaDbt;
        vrfSystem.OutdoorAirHumidityRatio =
          MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
            oaDbt, oaWbt, PhysicsConstants.StandardAtmosphericPressure);

        //室内機条件の設定
        double loadSum = 0;
        for (int i = 0; i < 4; i++)
        {
          loadSum += pLoadsH[key][i];
          double dbt_i = iDBTH[key][i];
          double hrt_i = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
            dbt_i, iWBTH[key][i], PhysicsConstants.StandardAtmosphericPressure);
          vrfSystem.SetIndoorUnitInletAirState(i, dbt_i, hrt_i);
          //給気温度
          iHexes[i].CurrentMode = pLoadsH[key][i] == 0 ? VRFUnit.Mode.ThermoOff : VRFUnit.Mode.Heating;
          iHexes[i].SolveHeatLoad(pLoadsH[key][i], iHexes[i].NominalAirFlowRate, dbt_i, hrt_i, false);
          vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[i].OutletAirTemperature);
          vrfSystem.SetIndoorUnitSetpointHumidityRatio(i, iHexes[i].OutletAirHumidityRatio);
        }
        vrfSystem.UpdateState();

        //集計
        double ttlHeat = 0;
        double ssbHeat = 0;
        for (int i = 0; i < 4; i++)
        {
          ttlHeat += iHexes[i].HeatTransfer;
          ssbHeat += iHexes[i].SensibleHeatTransfer;
        }

        //暖房モードでは Heating が必ず non-null でなければ実行できない
        double defrostLoad = vrfSystem.Heating!.OutdoorUnit.DefrostLoad;

        WriteCsv(
          key, loadSum, ttlHeat,
          vrfSystem.PartialLoadRatio,
          vrfSystem.CompressorInletPressure,
          vrfSystem.CompressorOutletPressure,
          vrfSystem.CompressorElectricity,
          ssbHeat / ttlHeat,
          defrostLoad: defrostLoad);
      }
    }

    #endregion

    #region 出力ヘルパー

    /// <summary>
    /// Writes one CSV result line.
    /// Pressures are converted from [kPa] to [MPa] by dividing by 1000.
    /// <paramref name="defrostLoad"/> is appended only for heating cases.
    /// </summary>
    private static void WriteCsv(
      string key, double loadSum, double totalHeat,
      double partialLoadRatio,
      double compressorInletPressure,  // [kPa]
      double compressorOutletPressure, // [kPa]
      double compressorElectricity,    // [kW]
      double sensibleHeatRatio,
      double? defrostLoad)
    {
      var ci = CultureInfo.InvariantCulture;
      string line =
        key + "," +
        loadSum.ToString("F2", ci) + "," +
        totalHeat.ToString("F2", ci) + "," +
        partialLoadRatio.ToString("F3", ci) + "," +
        (0.001 * compressorInletPressure).ToString("F3", ci) + "," +
        (0.001 * compressorOutletPressure).ToString("F3", ci) + "," +
        compressorElectricity.ToString("F2", ci) + "," +
        sensibleHeatRatio.ToString("F3", ci);

      if (defrostLoad.HasValue)
      {
        line += "," + defrostLoad.Value.ToString(ci);
      }

      Console.WriteLine(line);
    }

    #endregion

    #region 冷房テストケース データ

    private static Dictionary<string, double[]> CoolingLoads() => new()
    {
      { "NOM",  new[] { 5.6, 5.6, 5.6, 5.6 } },
      { "ML",   new[] { 2.8, 2.8, 2.8, 2.8 } },
      { "MLMT", new[] { 2.8, 2.8, 2.8, 2.8 } },
      { "22_1", new[] { 5.20, 5.28, 5.26, 5.30 } },
      { "22_2", new[] { 5.12, 5.37, 5.26, 5.17 } },
      { "24",   new[] { 2.43, 2.47, 2.44, 2.21 } },
      { "28",   new[] { 3.12, 2.50, 2.48, 2.49 } },
      { "34",   new[] { 5.23, 1.60, 1.29, 1.53 } },
      { "35",   new[] { 2.77, 2.68, 2.64, 2.74 } },
      { "23",   new[] { 3.97, 3.91, 3.96, 3.97 } },
      { "25",   new[] { 1.11, 1.08, 1.07, 1.16 } },
      { "25_2", new[] { 1.05, 1.04, 1.18, 1.12 } },
      { "26",   new[] { 0.59, 0.54, 0.52, 0.60 } },
      { "26_2", new[] { 0.45, 0.45, 0.54, 0.51 } },
      { "27",   new[] { 5.15, 5.27, 5.12, 5.22 } },
      { "29",   new[] { 1.09, 1.08, 1.07, 1.07 } },
      { "30",   new[] { 5.39, 5.46, 5.43, 5.49 } },
      { "31",   new[] { 2.80, 2.72, 2.80, 2.85 } },
      { "32",   new[] { 1.38, 1.40, 1.44, 1.45 } },
      { "33",   new[] { 2.06, 2.10, 3.83, 2.13 } },
    };

    private static Dictionary<string, double[]> CoolingInletDryBulb() => new()
    {
      { "NOM",  new[] { 27.0, 27.0, 27.0, 27.0 } },
      { "ML",   new[] { 27.0, 27.0, 27.0, 27.0 } },
      { "MLMT", new[] { 27.0, 27.0, 27.0, 27.0 } },
      { "22_1", new[] { 27.58, 27.61, 28.11, 27.94 } },
      { "22_2", new[] { 26.55, 26.22, 26.75, 26.73 } },
      { "24",   new[] { 26.28, 26.13, 26.43, 26.71 } },
      { "28",   new[] { 25.88, 26.39, 26.53, 26.42 } },
      { "34",   new[] { 27.83, 26.04, 25.44, 26.06 } },
      { "35",   new[] { 22.23, 26.12, 25.69, 22.09 } },
      { "23",   new[] { 26.54, 26.52, 26.74, 26.68 } },
      { "25",   new[] { 25.91, 25.96, 25.48, 25.90 } },
      { "25_2", new[] { 26.84, 26.89, 26.29, 26.94 } },
      { "26",   new[] { 25.72, 25.76, 25.35, 25.82 } },
      { "26_2", new[] { 26.69, 26.71, 26.28, 26.76 } },
      { "27",   new[] { 26.61, 26.49, 26.73, 26.82 } },
      { "29",   new[] { 25.84, 25.94, 25.47, 25.94 } },
      { "30",   new[] { 25.50, 25.36, 25.74, 25.74 } },
      { "31",   new[] { 21.69, 21.58, 21.45, 21.56 } },
      { "32",   new[] { 20.73, 20.86, 20.48, 20.76 } },
      { "33",   new[] { 26.08, 26.04, 26.70, 26.06 } },
    };

    private static Dictionary<string, double[]> CoolingInletWetBulb() => new()
    {
      { "NOM",  new[] { 19.0, 19.0, 19.0, 19.0 } },
      { "ML",   new[] { 19.0, 19.0, 19.0, 19.0 } },
      { "MLMT", new[] { 19.0, 19.0, 19.0, 19.0 } },
      { "22_1", new[] { 19.11, 19.15, 19.60, 19.24 } },
      { "22_2", new[] { 18.61, 18.42, 18.67, 18.61 } },
      { "24",   new[] { 18.69, 18.57, 18.64, 18.85 } },
      { "28",   new[] { 18.00, 18.85, 18.87, 18.85 } },
      { "34",   new[] { 19.34, 18.54, 18.15, 18.57 } },
      { "35",   new[] { 15.50, 18.57, 18.25, 15.35 } },
      { "23",   new[] { 18.80, 18.78, 18.89, 18.82 } },
      { "25",   new[] { 18.70, 18.68, 18.28, 18.56 } },
      { "25_2", new[] { 19.27, 19.25, 18.85, 19.29 } },
      { "26",   new[] { 18.47, 18.40, 18.20, 18.49 } },
      { "26_2", new[] { 19.01, 19.02, 18.65, 18.96 } },
      { "27",   new[] { 18.64, 18.56, 18.64, 18.69 } },
      { "29",   new[] { 18.64, 18.57, 18.30, 18.63 } },
      { "30",   new[] { 17.30, 17.21, 17.31, 17.34 } },
      { "31",   new[] { 15.15, 15.02, 14.86, 14.94 } },
      { "32",   new[] { 14.54, 14.51, 14.30, 14.48 } },
      { "33",   new[] { 18.62, 18.62, 18.83, 18.56 } },
    };

    #endregion

    #region 暖房テストケース データ

    private static Dictionary<string, double[]> HeatingLoads() => new()
    {
      { "NOM",  new[] { 6.25, 6.25, 6.25, 6.25 } },
      { "ML",   new[] { 2.83, 2.83, 2.83, 2.83 } },
      { "8_1",  new[] { 5.79, 5.73, 5.89, 5.90 } },
      { "8_2",  new[] { 5.85, 5.70, 5.85, 5.88 } },
      { "10",   new[] { 3.14, 3.09, 3.11, 3.07 } },
      { "20",   new[] { 2.03, 2.02, 5.96, 2.00 } },
      { "21",   new[] { 3.12, 3.12, 3.07, 3.10 } },
      { "9",    new[] { 4.54, 4.56, 4.58, 4.52 } },
      { "11",   new[] { 1.52, 1.52, 1.48, 1.54 } },
      { "12",   new[] { 0.89, 0.81, 0.84, 0.82 } },
      { "13",   new[] { 4.56, 4.64, 4.47, 4.50 } },
      { "13.5", new[] { 3.96, 3.94, 3.97, 3.97 } },
      { "14",   new[] { 3.15, 3.12, 3.23, 3.12 } },
      { "15",   new[] { 1.51, 1.50, 1.47, 1.45 } },
      { "16",   new[] { 5.75, 5.62, 5.71, 5.71 } },
      { "17",   new[] { 3.42, 3.17, 3.32, 3.24 } },
      { "18",   new[] { 1.68, 1.82, 1.68, 1.81 } },
      { "19",   new[] { 2.47, 2.52, 4.54, 2.56 } },
    };

    private static Dictionary<string, double[]> HeatingInletDryBulb() => new()
    {
      { "NOM",  new[] { 20.00, 20.00, 20.00, 20.00 } },
      { "ML",   new[] { 20.00, 20.00, 20.00, 20.00 } },
      { "8_1",  new[] { 19.62, 19.48, 19.08, 19.34 } },
      { "8_2",  new[] { 19.82, 19.89, 19.77, 20.04 } },
      { "10",   new[] { 19.22, 19.14, 19.09, 19.53 } },
      { "20",   new[] { 21.77, 21.34, 19.76, 21.52 } },
      { "21",   new[] { 18.53, 24.11, 23.94, 19.28 } },
      { "9",    new[] { 18.88, 19.08, 19.25, 19.75 } },
      { "11",   new[] { 20.41, 20.31, 20.26, 20.14 } },
      { "12",   new[] { 19.32, 19.29, 19.24, 19.79 } },
      { "13",   new[] { 13.13, 12.91, 13.03, 13.15 } },
      { "13.5", new[] { 19.51, 19.51, 19.35, 19.66 } },
      { "14",   new[] { 19.22, 19.32, 19.02, 19.65 } },
      { "15",   new[] { 18.94, 19.13, 18.90, 19.32 } },
      { "16",   new[] { 23.80, 23.83, 23.46, 23.68 } },
      { "17",   new[] { 23.73, 23.82, 23.73, 24.21 } },
      { "18",   new[] { 24.12, 24.04, 24.04, 24.26 } },
      { "19",   new[] { 19.68, 19.56, 19.23, 19.80 } },
    };

    private static Dictionary<string, double[]> HeatingInletWetBulb() => new()
    {
      { "NOM",  new[] { 16.00, 16.00, 16.00, 16.00 } },
      { "ML",   new[] { 16.00, 16.00, 16.00, 16.00 } },
      { "8_1",  new[] { 15.34, 15.25, 14.89, 15.07 } },
      { "8_2",  new[] { 15.41, 15.55, 15.40, 15.59 } },
      { "10",   new[] { 14.47, 14.49, 14.36, 14.68 } },
      { "20",   new[] { 16.22, 15.91, 15.39, 15.93 } },
      { "21",   new[] { 14.00, 18.78, 18.60, 14.48 } },
      { "9",    new[] { 14.53, 14.70, 14.72, 15.06 } },
      { "11",   new[] { 15.15, 15.07, 14.94, 14.82 } },
      { "12",   new[] { 14.19, 14.15, 14.08, 14.47 } },
      { "13",   new[] { 10.74, 10.57, 10.75, 10.82 } },
      { "13.5", new[] { 14.88, 14.95, 14.74, 14.94 } },
      { "14",   new[] { 14.44, 14.55, 14.26, 14.72 } },
      { "15",   new[] { 14.00, 14.16, 13.94, 14.25 } },
      { "16",   new[] { 18.93, 19.06, 18.65, 18.82 } },
      { "17",   new[] { 18.36, 18.55, 18.38, 18.73 } },
      { "18",   new[] { 18.45, 18.34, 18.36, 18.44 } },
      { "19",   new[] { 14.71, 14.64, 14.75, 14.72 } },
    };

    #endregion

  }
}
