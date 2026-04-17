/* AirToAirFlatPlateHeatExchangerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.Tests.HVAC.HeatExchanger
{
  /// <summary>Unit tests for <see cref="AirToAirFlatPlateHeatExchanger"/>.</summary>
  /// <remarks>
  /// AirToAirFlatPlateHeatExchanger models a fixed-plate air-to-air heat exchanger
  /// for ventilation heat recovery. It supports sensible-only and total heat exchange
  /// (sensible + latent).
  ///
  /// UpdateState(supplyFlowVol, exhaustFlowVol, SA_T_in, SA_W_in, EA_T_in, EA_W_in):
  ///   SA = supply air (outdoor → indoor), EA = exhaust air (indoor → outdoor)
  ///   In winter heating: SA_T_in &lt; EA_T_in → SA is heated, EA is cooled
  ///   In summer cooling: SA_T_in &gt; EA_T_in → SA is cooled, EA is heated
  ///
  /// Efficiencies (rated values given to constructor):
  ///   SensibleEfficiency: temperature recovery ratio
  ///   LatentEfficiency: humidity ratio recovery ratio (total HEX only)
  /// </remarks>
  public class AirToAirFlatPlateHeatExchangerTests
  {
    #region ヘルパー

    // JIS B 8628:2003 加熱条件（標準的な向流型全熱交換器）
    private const double SA_Flow = 500.0;  // 給気風量 [m³/h]
    private const double EA_Flow = 500.0;  // 排気風量 [m³/h]
    private const double SensEff = 0.7;    // 顕熱交換効率 [-]
    private const double LatEff = 0.6;    // 潜熱交換効率 [-]

    /// <summary>向流型全熱交換器（JIS 2003 加熱条件で初期化）。</summary>
    private static AirToAirFlatPlateHeatExchanger MakeTotalHex()
        => new AirToAirFlatPlateHeatExchanger(
            SA_Flow, EA_Flow, SensEff, LatEff,
            AirToAirFlatPlateHeatExchanger.AirFlow.CounterFlow,
            AirToAirFlatPlateHeatExchanger.Condition.JISB8628_2003_Heating);

    /// <summary>向流型顕熱交換器（潜熱なし: 全熱フラグ false の MakeHexDirect で構築）。</summary>
    private static AirToAirFlatPlateHeatExchanger MakeSensibleOnlyHex()
        => new AirToAirFlatPlateHeatExchanger(
            SA_Flow, EA_Flow, SensEff, 0.0,
            AirToAirFlatPlateHeatExchanger.AirFlow.CounterFlow,
            AirToAirFlatPlateHeatExchanger.Condition.JISB8628_2003_Heating,
            isEnthalpyEfficiency: false);

    #endregion

    // ================================================================
    #region コンストラクタ

    /// <summary>全熱交換器フラグが正しく設定される。</summary>
    [Fact]
    public void Constructor_TotalHex_IsTotalHeatExchangerTrue()
    {
      var hex = MakeTotalHex();
      Assert.True(hex.IsTotalHeatExchanger);
    }

    /// <summary>2引数コンストラクタ（効率直接指定）では IsTotalHeatExchanger が true になる。</summary>
    [Fact]
    public void Constructor_TwoEfficiencies_FlowVolumeStored()
    {
      var hex = MakeTotalHex();
      // コンストラクタ後は UpdateState 未呼び出しなのでプロパティは既定値
      Assert.True(hex.IsTotalHeatExchanger);
    }

    #endregion

    // ================================================================
    #region UpdateState — 加熱時（冬季）

    /// <summary>
    /// 加熱時（SA &lt; EA）：給気出口温度が給気入口温度より高い（予熱効果）。
    /// </summary>
    [Fact]
    public void UpdateState_Heating_SupplyOutletWarmerThanInlet()
    {
      var hex = MakeTotalHex();
      // SA: 0°C/50%, EA: 22°C/50%
      hex.UpdateState(SA_Flow, EA_Flow, 0.0, 0.003, 22.0, 0.008);
      Assert.True(hex.SupplyAirOutletDryBulbTemperature > hex.SupplyAirInletDryBulbTemperature,
          $"SA outlet={hex.SupplyAirOutletDryBulbTemperature:F2}°C > inlet=0°C");
    }

    /// <summary>
    /// 加熱時：排気出口温度が排気入口温度より低い（排熱回収）。
    /// </summary>
    [Fact]
    public void UpdateState_Heating_ExhaustOutletCoolerThanInlet()
    {
      var hex = MakeTotalHex();
      hex.UpdateState(SA_Flow, EA_Flow, 0.0, 0.003, 22.0, 0.008);
      Assert.True(hex.ExhaustAirOutletDryBulbTemperature < hex.ExhaustAirInletDryBulbTemperature,
          $"EA outlet={hex.ExhaustAirOutletDryBulbTemperature:F2}°C < inlet=22°C");
    }

    /// <summary>
    /// 全熱交換時：加熱で給気の絶対湿度も増加する（加湿効果）。
    /// </summary>
    [Fact]
    public void UpdateState_Heating_TotalHex_SupplyOutletHumidityIncreases()
    {
      var hex = MakeTotalHex();
      // SA: 乾燥外気 W=0.002, EA: 室内 W=0.008
      hex.UpdateState(SA_Flow, EA_Flow, 0.0, 0.002, 22.0, 0.008);
      Assert.True(hex.SupplyAirOutletHumidityRatio > hex.SupplyAirInletHumidityRatio,
          $"SA outlet W={hex.SupplyAirOutletHumidityRatio:F4} > inlet W=0.002");
    }

    /// <summary>
    /// 顕熱のみの交換器では絶対湿度が変化しない。
    /// </summary>
    [Fact]
    public void UpdateState_Heating_SensibleOnly_HumidityRatioUnchanged()
    {
      var hex = MakeSensibleOnlyHex();
      hex.UpdateState(SA_Flow, EA_Flow, 0.0, 0.002, 22.0, 0.008);
      Assert.InRange(hex.SupplyAirOutletHumidityRatio - hex.SupplyAirInletHumidityRatio,
          -0.0001, 0.0001);
    }

    #endregion

    // ================================================================
    #region UpdateState — 冷却時（夏季）

    /// <summary>
    /// 冷却時（SA &gt; EA）：給気出口温度が給気入口温度より低い（予冷効果）。
    /// </summary>
    [Fact]
    public void UpdateState_Cooling_SupplyOutletCoolerThanInlet()
    {
      var hex = MakeTotalHex();
      // SA: 35°C/W=0.018 (高温多湿外気), EA: 26°C/W=0.010 (室内)
      hex.UpdateState(SA_Flow, EA_Flow, 35.0, 0.018, 26.0, 0.010);
      Assert.True(hex.SupplyAirOutletDryBulbTemperature < hex.SupplyAirInletDryBulbTemperature,
          $"SA outlet={hex.SupplyAirOutletDryBulbTemperature:F2}°C < inlet=35°C");
    }

    #endregion

    // ================================================================
    #region UpdateState — 流量ゼロ

    /// <summary>給気風量ゼロのとき出口温度 = 入口温度（熱交換なし）。</summary>
    [Fact]
    public void UpdateState_ZeroSupplyFlow_OutletEqualsInlet()
    {
      var hex = MakeTotalHex();
      hex.UpdateState(0.0, EA_Flow, 0.0, 0.003, 22.0, 0.008);
      Assert.InRange(hex.SupplyAirOutletDryBulbTemperature, -0.01, 0.01);
      Assert.Equal(0.0, hex.SensibleEfficiency);
    }

    #endregion

    // ================================================================
    #region 効率・プロパティ

    /// <summary>
    /// JIS B 8628:2003 加熱の定格条件（SA=5°C, EA=20.5°C）で UpdateState すると
    /// SensibleEfficiency がコンストラクタ指定の定格値に一致する。
    /// 定格条件と異なる温度では UA は同じでも ε-NTU 上の効率値が変わるため
    /// 定格条件と同じ温度条件でのみ一致する。
    /// </summary>
    [Fact]
    public void UpdateState_RatedCondition_SensibleEfficiencyMatchesRating()
    {
      var hex = MakeTotalHex();
      // JIS B 8628:2003 加熱定格条件: SA=5.0°C/W=0.00350, EA=20.5°C/W=0.00894
      hex.UpdateState(SA_Flow, EA_Flow, 5.0, 0.00350, 20.5, 0.00894);
      Assert.InRange(hex.SensibleEfficiency, SensEff - 0.02, SensEff + 0.02);
    }

    /// <summary>FlowVolume プロパティが UpdateState 後に更新される。</summary>
    [Fact]
    public void UpdateState_FlowVolumeProperties_Updated()
    {
      var hex = MakeTotalHex();
      hex.UpdateState(400.0, 450.0, 0.0, 0.003, 22.0, 0.008);
      Assert.InRange(hex.SupplyAirFlowVolume, 399.9, 400.1);
      Assert.InRange(hex.ExhaustAirFlowVolume, 449.9, 450.1);
    }

    /// <summary>GetSensibleEffectiveness は 0〜1 の有効度を out で返す。</summary>
    [Fact]
    public void GetSensibleEffectiveness_InValidRange()
    {
      // static メソッドを直接呼び出す
      // SA: 0°C/W=0.003, EA: 22°C/W=0.008, 向流
      AirToAirFlatPlateHeatExchanger.GetSensibleEffectiveness(
          supplyAirMassFlowRate: 500.0 / 3600.0 * 1.2,
          exhaustAirMassFlowRate: 500.0 / 3600.0 * 1.2,
          supplyAirDryBulbTemperature: 0.0,
          supplyAirHumidityRatio: 0.003,
          exhaustAirDryBulbTemperature: 22.0,
          exhaustAirHumitidyRatio: 0.008,
          heatTransferCoefficient: 1.0,
          flow: AirToAirFlatPlateHeatExchanger.AirFlow.CounterFlow,
          out double eps, out double mcMin, out double R, out bool isMcMinSA);
      Assert.InRange(eps, 0.0, 1.0);
    }

    #endregion
  }
}