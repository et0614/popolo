/* BuriedPipeTests.cs
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

using Xunit;
using Popolo.Core.Building.Envelope;

namespace Popolo.Core.Tests.Building.Envelope
{
  /// <summary>BuriedPipe のテスト</summary>
  /// <remarks>
  /// 定性的な正しさを検証する。
  ///
  /// テストに使用する代表的な床暖房配管の仕様：
  ///   配管敷設ピッチ   : 0.2 m
  ///   配管総延長       : 10 m
  ///   分岐数           : 1
  ///   内径             : 0.013 m（13A銅管相当）
  ///   外径             : 0.015 m
  ///   配管熱伝導率     : 380 W/(m·K)（銅）
  ///   フィン熱伝導率   : 1.0 W/(m·K)（コンクリート相当）
  ///   フィン熱抵抗     : 0.1 (m²·K)/W
  ///   フィン厚み       : 0.05 m
  /// </remarks>
  public class BuriedPipeTests
  {
    #region テスト用ヘルパー

    /// <summary>代表的な床暖房配管を生成する</summary>
    private static BuriedPipe MakeTypicalPipe()
    {
      return new BuriedPipe(
          pitch: 0.20,   // 配管敷設ピッチ [m]
          length: 10.0,   // 配管総延長 [m]
          branchCount: 1,      // 分岐数
          iDiameter: 0.013,  // 内径 [m]
          oDiameter: 0.015,  // 外径 [m]
          tubeConductivity: 380.0,  // 配管熱伝導率 [W/(m·K)]（銅）
          upperFinConductivity: 1.0,  // 上側フィン熱伝導率 [W/(m·K)]
          lowerFinConductivity: 1.0,  // 下側フィン熱伝導率 [W/(m·K)]
          upperFinResistance: 0.1,   // 上側フィン熱抵抗 [(m²·K)/W]
          lowerFinResistance: 0.1,   // 下側フィン熱抵抗 [(m²·K)/W]
          upperFinThickness: 0.05,  // 上側フィン厚み [m]
          lowerFinThickness: 0.05); // 下側フィン厚み [m]
    }

    #endregion

    #region コンストラクタのテスト

    /// <summary>コンストラクタでプロパティが正しく設定される</summary>
    [Fact]
    public void Constructor_SetsProperties()
    {
      var pipe = MakeTypicalPipe();

      Assert.Equal(0.20, pipe.Pitch, precision: 6);
      Assert.Equal(10.0, pipe.Length, precision: 6);
      Assert.Equal(1, pipe.BranchCount);
      Assert.Equal(0.013, pipe.InnerDiameter, precision: 6);
      Assert.Equal(0.015, pipe.OuterDiameter, precision: 6);
      Assert.Equal(380.0, pipe.ThermalConductivityOfTube, precision: 6);
    }

    /// <summary>フィン効率は0より大きく1以下である</summary>
    [Fact]
    public void Constructor_FinEfficiency_IsInRange()
    {
      var pipe = MakeTypicalPipe();

      Assert.InRange(pipe.UpperFinEfficiency, 0.0, 1.0);
      Assert.InRange(pipe.LowerFinEfficiency, 0.0, 1.0);
    }

    #endregion

    #region 流量のテスト

    /// <summary>流量0では有効度が0</summary>
    [Fact]
    public void SetFlowRate_Zero_EffectivenessIsZero()
    {
      var pipe = MakeTypicalPipe();
      pipe.SetFlowRate(0);

      Assert.Equal(0, pipe.Effectiveness, precision: 6);
    }

    /// <summary>
    /// 流量が増加すると有効度が減少する（NTU法の性質）。
    /// NTU = KA / (m_dot × cp) であり、流量増加でNTUが低下するため有効度が低下する。
    /// ただしKAも流量依存（Re↑→Nu↑→hi↑）するため減少は緩やか。
    /// </summary>
    [Fact]
    public void SetFlowRate_Increasing_EffectivenessDecreases()
    {
      var pipe = MakeTypicalPipe();

      pipe.SetFlowRate(1e-3);
      double e1 = pipe.Effectiveness;

      pipe.SetFlowRate(1e-2);
      double e2 = pipe.Effectiveness;

      pipe.SetFlowRate(1e-1);
      double e3 = pipe.Effectiveness;

      Assert.True(e1 > e2, $"e1={e1:F4} should be > e2={e2:F4}");
      Assert.True(e2 > e3, $"e2={e2:F4} should be > e3={e3:F4}");
    }

    /// <summary>有効度は0より大きく1未満である</summary>
    [Fact]
    public void SetFlowRate_Typical_EffectivenessInRange()
    {
      var pipe = MakeTypicalPipe();
      pipe.SetFlowRate(1e-2); // 代表的な流量

      Assert.InRange(pipe.Effectiveness, 0.0, 1.0);
    }

    /// <summary>流量が十分大きい場合でも有効度は物理的範囲内</summary>
    [Fact]
    public void SetFlowRate_Large_EffectivenessInRange()
    {
      var pipe = MakeTypicalPipe();
      pipe.SetFlowRate(1.0); // 大きな流量

      Assert.InRange(pipe.Effectiveness, 0.0, 1.0);
    }

    /// <summary>流量が小さいほど有効度が1に近い（滞留時間が長く熱交換が進む）</summary>
    [Fact]
    public void SetFlowRate_Small_EffectivenessNearOne()
    {
      var pipe = MakeTypicalPipe();
      pipe.SetFlowRate(1e-5); // 非常に小さい流量

      Assert.InRange(pipe.Effectiveness, 0.99, 1.0);
    }

    #endregion

    #region 水温のテスト

    /// <summary>水温が変化しても有効度は物理的な範囲内</summary>
    [Theory]
    [InlineData(5)]
    [InlineData(25)]
    [InlineData(60)]
    public void SetWaterTemperature_VariousTemperatures_EffectivenessInRange(double temperature)
    {
      var pipe = MakeTypicalPipe();
      pipe.SetFlowRate(1e-4);
      pipe.SetWaterTemperature(temperature);

      Assert.InRange(pipe.Effectiveness, 0.0, 1.0);
    }

    /// <summary>高温では水の動粘性係数が低下するため乱流が強まり有効度が上昇する</summary>
    [Fact]
    public void SetWaterTemperature_HigherTemperature_HigherEffectiveness()
    {
      //水の動粘性係数は温度上昇とともに低下する→Re数上昇→Nu数上昇→有効度上昇
      var pipe = MakeTypicalPipe();
      pipe.SetFlowRate(1e-4);

      pipe.SetWaterTemperature(20);
      double eLow = pipe.Effectiveness;

      pipe.SetWaterTemperature(60);
      double eHigh = pipe.Effectiveness;

      Assert.True(eHigh > eLow,
          $"Effectiveness at 60°C ({eHigh:F4}) should be > at 20°C ({eLow:F4})");
    }

    /// <summary>初期水温のデフォルト値が25°Cである</summary>
    [Fact]
    public void InletWaterTemperature_Default_Is25()
    {
      var pipe = MakeTypicalPipe();
      Assert.Equal(25, pipe.InletWaterTemperature, precision: 6);
    }

    #endregion

    #region フィン効率のテスト

    /// <summary>フィン熱伝導率が高いほどフィン効率が高い</summary>
    [Fact]
    public void UpperFinEfficiency_HigherConductivity_HigherEfficiency()
    {
      //低熱伝導率フィン（断熱材相当）
      var pipeLow = new BuriedPipe(
          0.20, 10.0, 1, 0.013, 0.015, 380.0,
          0.1, 0.1, 0.1, 0.1, 0.05, 0.05);

      //高熱伝導率フィン（金属相当）
      var pipeHigh = new BuriedPipe(
          0.20, 10.0, 1, 0.013, 0.015, 380.0,
          50.0, 50.0, 0.1, 0.1, 0.05, 0.05);

      Assert.True(pipeHigh.UpperFinEfficiency > pipeLow.UpperFinEfficiency,
          $"High conductivity ({pipeHigh.UpperFinEfficiency:F4}) should be > low ({pipeLow.UpperFinEfficiency:F4})");
    }

    #endregion
  }
}