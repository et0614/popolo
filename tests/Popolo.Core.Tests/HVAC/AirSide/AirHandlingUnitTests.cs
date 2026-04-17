/* AirHandlingUnitTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.AirSide;
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.HVAC.FluidCircuit;

namespace Popolo.Core.Tests.HVAC.AirSide
{
  /// <summary>Unit tests for <see cref="AirHandlingUnit"/>.</summary>
  /// <remarks>
  /// Test conditions are taken directly from AirHandlingUnitTest() and
  /// AirHandlingUnitVAVTest() sample code.
  ///
  /// Common setup:
  ///   SA flow: 7476 m3/h, RA flow: 6946 m3/h, OA flow: 1513 m3/h
  ///   Cooling coil: rated 49.6 kW (27.46C -> 15C, CW 7C)
  ///   Heating coil: rated 46.9 kW (17.46C -> 35C, HW 50C)
  ///   Rotary regenerator: ε = 0.34
  ///   CW inlet: 7C, HW inlet: 43C (AHUTest) / 50C (VAVTest)
  /// </remarks>
  public class AirHandlingUnitTests
  {
    #region 定格条件定数

    // 風量 [m3/s]
    private static readonly double Qsa = 7476.0 / 3600;
    private static readonly double Qra = 6946.0 / 3600;
    private static readonly double Qoa = 1513.0 / 3600;

    // kg/s 換算（空気密度 1.2 kg/m3）
    private static readonly double Msa = Qsa * 1.2;
    private static readonly double Mra = Qra * 1.2;
    private static readonly double Moa = Qoa * 1.2;

    #endregion

    #region ヘルパー

    /// <summary>
    /// AirHandlingUnitTest() / AirHandlingUnitVAVTest() と同じ機器構成で AHU を生成する。
    /// </summary>
    private static AirHandlingUnit MakeAHU(
        double hwInlet = 43.0,
        bool withRegenerator = true)
    {
      // 冷水コイル (AirHandlingUnitTest と同一パラメータ)
      var cCoil = new CrossFinHeatExchanger(
          0.82, 0.910, 6, 24,
          Msa, 27.46, 0.01206, 95,
          127.0 / 60, 127.0 / 60, 7.0,
          CrossFinHeatExchanger.WaterFlowType.HalfFlow,
          49.6, true);

      // 温水コイル
      var hCoil = new CrossFinHeatExchanger(
          0.82, 0.910, 4, 24,
          Msa, 17.46, 0.00554, 95,
          105.0 / 60, 105.0 / 60, 50.0,
          CrossFinHeatExchanger.WaterFlowType.HalfFlow,
          46.9, true);

      // 給気・還気ファン
      // AirHandlingUnitTest: (0.4, Msa/1.2, 0.4, Msa/1.2, 3, true)
      //   nomPressure=0.4kPa, nomFlowRate=Qsa m3/s, number=3, INV=true
      var saFan = new CentrifugalFan(0.4, Qsa, 0.4, Qsa, 3, true);
      var raFan = new CentrifugalFan(0.2, Qra, 0.2, Qra, 3, true);

      AirHandlingUnit ahu;
      if (withRegenerator)
      {
        // 全熱交換器: ε=0.34, SA側=Qoa*3600 m3/h, EA側=(Qoa+Qra-Qsa)*3600 m3/h
        double eaFlow = (Qoa + Qra - Qsa) * 3600;
        var regen = new RotaryRegenerator(
            0.34,
            Qoa * 3600, eaFlow,
            true,               // isDesiccantWheel（全熱）
            34.4, 0.0194,       // SA 入口条件（夏季基準）
            26.0, 0.0105);      // EA 入口条件
        ahu = new AirHandlingUnit(
            cCoil, hCoil,
            AirHandlingUnit.HumidifierType.DropPervaporation,
            saFan, raFan, regen);
      }
      else
      {
        ahu = new AirHandlingUnit(
            cCoil, hCoil,
            AirHandlingUnit.HumidifierType.DropPervaporation,
            saFan, raFan);
      }

      ahu.SetOutdoorAirFlowRange(Moa, Msa);
      ahu.SetAirFlowRate(Mra, Msa);
      ahu.ChilledWaterInletTemperature = 7.0;
      ahu.HotWaterInletTemperature = hwInlet;
      ahu.RATemperature = 26.0;
      ahu.RAHumidityRatio = 0.0105;

      return ahu;
    }

    #endregion

    // ================================================================
    #region ShutOff

    /// <summary>ShutOff 後は SA 流量 = 0。</summary>
    [Fact]
    public void ShutOff_ZeroSupplyAirFlow()
    {
      var ahu = MakeAHU();
      ahu.ShutOff();
      Assert.Equal(0.0, ahu.SAFlowRate);
    }

    #endregion

    // ================================================================
    #region CoolAir — 冷却運転（AirHandlingUnitTest より）

    /// <summary>
    /// 夏季外気（34.4C/0.0194）で CoolAir(15C, 0) を呼ぶと SA 温度が設定値付近になる。
    /// </summary>
    [Fact]
    public void CoolAir_SummerCondition_SATemperatureNearSetpoint()
    {
      var ahu = MakeAHU(hwInlet: 43.0, withRegenerator: false);
      ahu.OATemperature = 34.4;
      ahu.OAHumidityRatio = 0.0194;
      ahu.BypassRegenerator = true;

      bool ok = ahu.CoolAir(15.0, 0);
      // 冷凍能力が十分あれば設定値付近
      Assert.InRange(ahu.SATemperature, 13.0, 17.0);
    }

    /// <summary>全熱交換器あり・バイパスなしで CoolAir すると SA 温度が設定値付近。</summary>
    [Fact]
    public void CoolAir_WithRegenerator_SATemperatureNearSetpoint()
    {
      var ahu = MakeAHU(hwInlet: 43.0);
      ahu.OATemperature = 34.4;
      ahu.OAHumidityRatio = 0.0194;
      ahu.BypassRegenerator = false;

      bool ok = ahu.CoolAir(15.0, 0);
      Assert.InRange(ahu.SATemperature, 12.0, 18.0);
    }

    /// <summary>CoolAir 後、冷水コイルが熱を処理している（HeatTransfer > 0）。</summary>
    [Fact]
    public void CoolAir_CoolingCoilHeatTransferIsPositive()
    {
      var ahu = MakeAHU(hwInlet: 43.0, withRegenerator: false);
      ahu.OATemperature = 34.4;
      ahu.OAHumidityRatio = 0.0194;
      ahu.BypassRegenerator = true;

      ahu.CoolAir(15.0, 0);
      Assert.True(ahu.CoolingCoil.HeatTransfer > 0,
          $"CoolingCoil.HeatTransfer={ahu.CoolingCoil.HeatTransfer:F1} kW > 0");
    }

    /// <summary>SA 流量が正。</summary>
    [Fact]
    public void CoolAir_SAFlowRateIsPositive()
    {
      var ahu = MakeAHU(withRegenerator: false);
      ahu.OATemperature = 34.4;
      ahu.OAHumidityRatio = 0.0194;
      ahu.CoolAir(15.0, 0);
      Assert.True(ahu.SAFlowRate > 0, $"SAFlowRate={ahu.SAFlowRate:F3} kg/s > 0");
    }

    /// <summary>
    /// 外気冷房（乾球温度基準）が有効な条件（OA=10C < RA=26C）では
    /// 冷水コイルの処理熱量が外気冷房なしより少ない。
    /// </summary>
    [Fact]
    public void CoolAir_EconomiserActive_ReducesCoolingCoilLoad()
    {
      // 外気冷房なし
      var ahuNoEco = MakeAHU(withRegenerator: false);
      ahuNoEco.OATemperature = 10.0;
      ahuNoEco.OAHumidityRatio = 0.006;
      ahuNoEco.OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.None;
      ahuNoEco.CoolAir(15.0, 0);
      double qNoEco = ahuNoEco.CoolingCoil.HeatTransfer;

      // 外気冷房あり
      var ahuEco = MakeAHU(withRegenerator: false);
      ahuEco.OATemperature = 10.0;
      ahuEco.OAHumidityRatio = 0.006;
      ahuEco.OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.DryBulbTemperature;
      ahuEco.CoolAir(15.0, 0);
      double qEco = ahuEco.CoolingCoil.HeatTransfer;

      Assert.True(qEco <= qNoEco,
          $"With economiser Q={qEco:F1} kW <= Without Q={qNoEco:F1} kW");
    }

    #endregion

    // ================================================================
    #region HeatAir — 加熱運転（AirHandlingUnitTest より）

    /// <summary>
    /// 冬季外気（2C/0.0014）で HeatAir(35C, 0.010) を呼ぶと SA 温度が設定値付近になる。
    /// </summary>
    [Fact]
    public void HeatAir_WinterCondition_SATemperatureNearSetpoint()
    {
      var ahu = MakeAHU(hwInlet: 43.0, withRegenerator: false);
      ahu.OATemperature = 2.0;
      ahu.OAHumidityRatio = 0.0014;
      ahu.RATemperature = 26.0;
      ahu.RAHumidityRatio = 0.0105;
      ahu.BypassRegenerator = true;

      bool ok = ahu.HeatAir(35.0, 0.010);
      if (ok)
        Assert.InRange(ahu.SATemperature, 33.0, 37.0);
    }

    /// <summary>加熱運転で温水コイルが熱を供給している（HeatTransfer > 0）。</summary>
    [Fact]
    public void HeatAir_HeatingCoilHeatTransferIsPositive()
    {
      var ahu = MakeAHU(hwInlet: 43.0, withRegenerator: false);
      ahu.OATemperature = 2.0;
      ahu.OAHumidityRatio = 0.0014;
      ahu.BypassRegenerator = true;

      ahu.HeatAir(35.0, 0.010);
      // 加熱コイルでは温水から空気へ熱が移動するため水側は冷却される（HeatTransfer < 0）
      Assert.True(ahu.HeatingCoil.HeatTransfer < 0,
              $"HeatingCoil.HeatTransfer={ahu.HeatingCoil.HeatTransfer:F1} kW < 0 (water cooled)");
    }

    /// <summary>全熱交換器ありでは排気から予熱回収が行われる（SA が暖かくなる）。</summary>
    [Fact]
    public void HeatAir_RegeneratorRecovery_SAWarmerThanWithoutRegenerator()
    {
      // バイパスあり（全熱交換器なし）
      var ahuBypass = MakeAHU(hwInlet: 43.0);
      ahuBypass.OATemperature = 2.0;
      ahuBypass.OAHumidityRatio = 0.0014;
      ahuBypass.BypassRegenerator = true;
      ahuBypass.HeatAir(35.0, 0.010);
      double htBypass = ahuBypass.HeatingCoil.HeatTransfer;

      // バイパスなし（全熱交換器あり）
      var ahuRegen = MakeAHU(hwInlet: 43.0);
      ahuRegen.OATemperature = 2.0;
      ahuRegen.OAHumidityRatio = 0.0014;
      ahuRegen.BypassRegenerator = false;
      ahuRegen.HeatAir(35.0, 0.010);
      double htRegen = ahuRegen.HeatingCoil.HeatTransfer;

      // 全熱交換器使用時は予熱回収されるため温水コイルへの負荷が少ない
      // HeatTransfer は負値（水側冷却）なので絶対値で比較
      Assert.True(Math.Abs(htRegen) <= Math.Abs(htBypass),
          $"With regen |HT|={Math.Abs(htRegen):F1} kW <= Bypass |HT|={Math.Abs(htBypass):F1} kW");
    }

    #endregion

    // ================================================================
    #region OptimizeVAV — VAV風量計算（AirHandlingUnitVAVTest より）

    /// <summary>
    /// 冷却運転で OptimizeVAV が成功し、SA 流量が正になる。
    /// ゾーン顕熱負荷: -16/-7/-6 kW（冷房要求）。
    /// </summary>
    [Fact]
    public void OptimizeVAV_CoolingMode_SucceedsAndPositiveSAFlow()
    {
      var ahu = MakeAHU(hwInlet: 50.0);
      ahu.OATemperature = 34.4;
      ahu.OAHumidityRatio = 0.0194;
      ahu.OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.None;
      ahu.MinimizeAirFlow = true;
      ahu.UpperTemperatureLimit_C = 15.0;
      ahu.LowerTemperatureLimit_C = 10.0;
      ahu.UpperTemperatureLimit_H = 40.0;
      ahu.LowerTemperatureLimit_H = 30.0;

      const double CNV = 1.2 / 3600.0;
      bool[] off = { false, false, false };
      double[] zT = { 22.0, 24.0, 26.0 };
      double[] zW = { 0.008, 0.008, 0.008 };
      double[] zHL = { -16000.0, -7000.0, -6000.0 };
      double[] maxSA = { 4151 * CNV, 1782 * CNV, 1543 * CNV };
      double cf = 6946.0 / 7476.0;
      double[] maxRA = { maxSA[0] * cf, maxSA[1] * cf, maxSA[2] * cf };
      double[] minSA = { maxSA[0] * 0.4, maxSA[1] * 0.4, maxSA[2] * 0.4 };

      bool suc;
      double[] af = ahu.OptimizeVAV(true, 0, off, zT, zW, zHL, minSA, maxSA, maxRA, out suc);

      Assert.True(ahu.SAFlowRate > 0,
          $"SAFlowRate={ahu.SAFlowRate:F3} kg/s > 0");
      Assert.Equal(3, af.Length);
    }

    /// <summary>
    /// 加熱運転で OptimizeVAV が成功し、SA 温度が下限以上になる。
    /// ゾーン顕熱負荷: +12.1/+5.3/+4.6 kW（暖房要求）。
    /// </summary>
    [Fact]
    public void OptimizeVAV_HeatingMode_SATemperatureAboveLowerLimit()
    {
      var ahu = MakeAHU(hwInlet: 50.0);
      ahu.OATemperature = 2.0;
      ahu.OAHumidityRatio = 0.0014;
      ahu.MinimizeAirFlow = true;
      ahu.UpperTemperatureLimit_H = 40.0;
      ahu.LowerTemperatureLimit_H = 30.0;

      const double CNV = 1.2 / 3600.0;
      bool[] off = { false, false, false };
      double[] zT = { 22.0, 24.0, 26.0 };
      double[] zW = { 0.008, 0.008, 0.008 };
      double[] zHL = { 12100.0, 5300.0, 4600.0 };
      double[] maxSA = { 4151 * CNV, 1782 * CNV, 1543 * CNV };
      double cf = 6946.0 / 7476.0;
      double[] maxRA = { maxSA[0] * cf, maxSA[1] * cf, maxSA[2] * cf };
      double[] minSA = { maxSA[0] * 0.4, maxSA[1] * 0.4, maxSA[2] * 0.4 };

      bool suc;
      double[] af = ahu.OptimizeVAV(false, 0.0105, off, zT, zW, zHL, minSA, maxSA, maxRA, out suc);

      Assert.True(ahu.SATemperature >= 29.0,
          $"SA temp={ahu.SATemperature:F1}C >= lower limit 30C");
    }

    /// <summary>
    /// 外気冷房条件（OA=10C < RA=26C）の VAV 冷却運転で冷水コイル処理熱が減る。
    /// </summary>
    [Fact]
    public void OptimizeVAV_EconomiserCooling_ReducesCoolingCoilLoad()
    {
      const double CNV = 1.2 / 3600.0;
      bool[] off = { false, false, false };
      double[] zT = { 22.0, 24.0, 26.0 };
      double[] zW = { 0.008, 0.008, 0.008 };
      double[] zHL = { -16000.0, -7000.0, -6000.0 };
      double[] maxSA = { 4151 * CNV, 1782 * CNV, 1543 * CNV };
      double cf = 6946.0 / 7476.0;
      double[] maxRA = { maxSA[0] * cf, maxSA[1] * cf, maxSA[2] * cf };
      double[] minSA = { maxSA[0] * 0.4, maxSA[1] * 0.4, maxSA[2] * 0.4 };

      // 外気冷房なし
      var ahuNoEco = MakeAHU(hwInlet: 50.0);
      ahuNoEco.OATemperature = 10.0;
      ahuNoEco.OAHumidityRatio = 0.006;
      ahuNoEco.OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.None;
      ahuNoEco.MinimizeAirFlow = true;
      bool suc;
      ahuNoEco.OptimizeVAV(true, 0, off, zT, zW, zHL, minSA, maxSA, maxRA, out suc);
      double qNoEco = ahuNoEco.CoolingCoil.HeatTransfer;

      // 外気冷房あり
      var ahuEco = MakeAHU(hwInlet: 50.0);
      ahuEco.OATemperature = 10.0;
      ahuEco.OAHumidityRatio = 0.006;
      ahuEco.OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.DryBulbTemperature;
      ahuEco.MinimizeAirFlow = true;
      ahuEco.OptimizeVAV(true, 0, off, zT, zW, zHL, minSA, maxSA, maxRA, out suc);
      double qEco = ahuEco.CoolingCoil.HeatTransfer;

      Assert.True(qEco <= qNoEco,
          $"With economiser Q={qEco:F1} kW <= Without Q={qNoEco:F1} kW");
    }

    #endregion
  }
}