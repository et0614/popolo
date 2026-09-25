/* PhotovoltaicPanelTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using Xunit;
using Popolo.Core.Climate;
using Popolo.Core.Energy;

namespace Popolo.Core.Tests.Energy
{
  /// <summary>PhotovoltaicPanel のテスト</summary>
  /// <remarks>
  /// 期待値の根拠：
  /// - JIS C 8907: STC（標準試験条件）は傾斜面日射強度1000W/m²、周囲温度25°C
  /// - 湯川元信ら: パネル温度上昇の推定式（1996）
  ///
  /// インバータはPowerConverter（負荷率依存効率、定格=PeakPower）で表現される。
  /// STC条件では温度補正係数0.88〜0.98、インバータ効率≒0.95のため、
  /// 出力 ≒ PeakPower × (0.84〜0.95) が成り立つ。
  /// </remarks>
  public class PhotovoltaicPanelTests
  {

    #region Constructor tests

    /// <summary>傾斜角コンストラクタでプロパティが正しく設定される</summary>
    [Fact]
    public void Constructor_WithTiltAngle_SetsProperties()
    {
      var panel = new PhotovoltaicPanel(
          5000, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);

      Assert.Equal(5000, panel.PeakPower, precision: 6);
      Assert.Equal(PhotovoltaicPanel.MountType.RoofMount, panel.Mount);
      Assert.Equal(PhotovoltaicPanel.MaterialType.Crystal, panel.Material);
      //インバータの定格電力はパネルのピーク出力で初期化される
      Assert.Equal(5000, panel.Inverter.RatedPower, precision: 6);
      //南向き（HorizontalAngle=0）で設置される
      Assert.Equal(0, panel.Incline.HorizontalAngle, precision: 6);
      //傾斜角30° = π/6 radian
      Assert.Equal(30 * Math.PI / 180, panel.Incline.VerticalAngle, precision: 5);
    }

    /// <summary>Inclineコンストラクタでプロパティが正しく設定される</summary>
    [Fact]
    public void Constructor_WithIncline_SetsIncline()
    {
      var incline = new Incline(Incline.Orientation.W, Math.PI / 4);
      var panel = new PhotovoltaicPanel(
          3000, PhotovoltaicPanel.MountType.GroundMount,
          PhotovoltaicPanel.MaterialType.Amorphous, incline);

      Assert.Equal(incline.HorizontalAngle, panel.Incline.HorizontalAngle, precision: 6);
      Assert.Equal(incline.VerticalAngle, panel.Incline.VerticalAngle, precision: 6);
    }

    #endregion

    #region Inverter tests

    /// <summary>インバータの損失係数を変更すると出力が変化する</summary>
    [Fact]
    public void Inverter_CoefficientsChanged_AffectsPower()
    {
      var panel = new PhotovoltaicPanel(
          5000, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);

      double powerDefault = panel.GetPower(25, 5, 1000);
      //無負荷損係数を増やすと効率が下がる
      panel.Inverter.CoefficientC = 0.10;
      double powerLossy = panel.GetPower(25, 5, 1000);

      Assert.True(powerLossy < powerDefault,
          $"Expected lossy ({powerLossy:F1}W) < default ({powerDefault:F1}W)");
    }

    #endregion

    #region GetPower tests

    /// <summary>
    /// STC条件（日射1000W/m²、25°C）では出力がPeakPower×(0.84〜0.95) に収まる
    /// </summary>
    /// <remarks>
    /// 風速5m/sでもパネル温度は43〜48°Cまで上昇するため（湯川ら 1996）、
    /// DC出力は定格値の88〜98%程度になる。さらにインバータ効率
    /// （負荷率0.9前後で約0.95）が乗るため、AC出力は定格の84〜95%程度になる。
    /// </remarks>
    [Theory]
    [InlineData(PhotovoltaicPanel.MountType.RoofMount, PhotovoltaicPanel.MaterialType.Crystal)]
    [InlineData(PhotovoltaicPanel.MountType.RoofIntegrated, PhotovoltaicPanel.MaterialType.Crystal)]
    [InlineData(PhotovoltaicPanel.MountType.GroundMount, PhotovoltaicPanel.MaterialType.Amorphous)]
    public void GetPower_AtSTC_CloseToRatedOutput(
        PhotovoltaicPanel.MountType mount, PhotovoltaicPanel.MaterialType material)
    {
      double peakPower = 5000;
      var panel = new PhotovoltaicPanel(
          peakPower, mount, material,
          Incline.Orientation.S, 30);

      //STC: 日射1000W/m²、気温25°C、風速5m/s
      double power = panel.GetPower(25, 5, 1000);

      Assert.InRange(power, peakPower * 0.84, peakPower * 0.95);
    }

    /// <summary>日射がゼロの場合は出力がゼロ</summary>
    [Fact]
    public void GetPower_ZeroIrradiance_ReturnsZero()
    {
      var panel = new PhotovoltaicPanel(
          5000, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);

      double power = panel.GetPower(20, 1, 0);
      Assert.Equal(0, power, precision: 6);
    }

    /// <summary>高温ではパネル温度上昇により出力が低下する</summary>
    [Fact]
    public void GetPower_HighTemperature_LowerThanLowTemperature()
    {
      var panel = new PhotovoltaicPanel(
          5000, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);

      double powerCool = panel.GetPower(15, 1, 800);
      double powerHot = panel.GetPower(35, 1, 800);

      Assert.True(powerCool > powerHot,
          $"Expected cool ({powerCool:F1}W) > hot ({powerHot:F1}W)");
    }

    /// <summary>アモルファスは結晶より温度係数が小さい（高温での出力低下が小さい）</summary>
    [Fact]
    public void GetPower_Amorphous_LessTemperatureSensitiveThanCrystal()
    {
      double peakPower = 5000;
      var panelAm = new PhotovoltaicPanel(
          peakPower, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Amorphous, Incline.Orientation.S, 30);
      var panelCr = new PhotovoltaicPanel(
          peakPower, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);

      //高温・強日射条件
      double powerAm = panelAm.GetPower(40, 0.5, 900);
      double powerCr = panelCr.GetPower(40, 0.5, 900);

      //アモルファスの方が温度係数が小さいため出力が高い
      Assert.True(powerAm > powerCr,
          $"Expected amorphous ({powerAm:F1}W) > crystal ({powerCr:F1}W)");
    }

    /// <summary>架台形は屋根材一体形より通気が良くパネル温度が低い（出力が高い）</summary>
    [Fact]
    public void GetPower_MountMode_HigherThanRoofIntegrated()
    {
      double peakPower = 5000;
      var panelMount = new PhotovoltaicPanel(
          peakPower, PhotovoltaicPanel.MountType.GroundMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);
      var panelInteg = new PhotovoltaicPanel(
          peakPower, PhotovoltaicPanel.MountType.RoofIntegrated,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);

      //低風速・高日射条件（通気性の差が出やすい）
      double powerMount = panelMount.GetPower(30, 0.5, 900);
      double powerInteg = panelInteg.GetPower(30, 0.5, 900);

      Assert.True(powerMount > powerInteg,
          $"Expected mount ({powerMount:F1}W) > integrated ({powerInteg:F1}W)");
    }

    /// <summary>静的メソッドとインスタンスメソッドの出力が一致する</summary>
    [Fact]
    public void GetPower_StaticAndInstanceMatch()
    {
      var panel = new PhotovoltaicPanel(
          5000, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);

      double fromInstance = panel.GetPower(25, 2, 800);
      double fromStatic = PhotovoltaicPanel.GetPower(
          25, 2, 800, panel.PeakPower, panel.Inverter,
          panel.Mount, panel.Material);

      Assert.Equal(fromInstance, fromStatic, precision: 6);
    }

    /// <summary>Sunオブジェクトを使ったGetPowerは日射量0で出力0</summary>
    [Fact]
    public void GetPower_WithSun_NightTime_ReturnsZero()
    {
      var panel = new PhotovoltaicPanel(
          5000, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);

      var sun = new Sun(35.67, 139.75, 135.0);
      //深夜2時（日射なし）
      sun.Update(new DateTime(2024, 6, 22, 2, 0, 0));
      sun.DirectNormalRadiation = 0;
      sun.DiffuseHorizontalRadiation = 0;
      sun.GlobalHorizontalRadiation = 0;

      double power = panel.GetPower(20, 1, sun);
      Assert.Equal(0, power, precision: 6);
    }

    #endregion

    #region IReadOnlyPhotovoltaicPanel tests

    /// <summary>IReadOnlyPhotovoltaicPanelとして参照できる</summary>
    [Fact]
    public void Panel_ImplementsIReadOnlyPhotovoltaicPanel()
    {
      var panel = new PhotovoltaicPanel(
          5000, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);

      IReadOnlyPhotovoltaicPanel readOnly = panel;
      Assert.Equal(5000, readOnly.PeakPower, precision: 6);
      Assert.Equal(5000, readOnly.Inverter.RatedPower, precision: 6);
    }

    #endregion

  }
}
