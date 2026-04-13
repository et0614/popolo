/* PhotovoltaicPanelTests.cs
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
  /// STC条件（日射1000W/m²、気温25°C、風速1m/s相当）では温度補正係数≒1 となるため、
  /// 出力 ≒ PeakPower × InverterEfficiency が成り立つ。
  /// </remarks>
  public class PhotovoltaicPanelTests
  {

    #region コンストラクタのテスト

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
      Assert.Equal(0.9, panel.InverterEfficiency, precision: 6);
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
          3000, PhotovoltaicPanel.MountType.MountMode,
          PhotovoltaicPanel.MaterialType.Amorphous, incline);

      Assert.Equal(incline.HorizontalAngle, panel.Incline.HorizontalAngle, precision: 6);
      Assert.Equal(incline.VerticalAngle, panel.Incline.VerticalAngle, precision: 6);
    }

    #endregion

    #region InverterEfficiencyのテスト

    /// <summary>InverterEfficiencyは0〜1にクランプされる</summary>
    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(0.85, 0.85)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.5, 1.0)]
    public void InverterEfficiency_Clamped(double input, double expected)
    {
      var panel = new PhotovoltaicPanel(
          5000, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);
      panel.InverterEfficiency = input;
      Assert.Equal(expected, panel.InverterEfficiency, precision: 6);
    }

    #endregion

    #region GetPowerのテスト

    /// <summary>
    /// STC条件（日射1000W/m²、25°C）では出力がPeakPower×InverterEfficiency に近い
    /// </summary>
    /// <remarks>
    /// 風速5m/sでもパネル温度は43〜48°Cまで上昇するため（湯川ら 1996）、
    /// 出力は定格値の88〜98%程度になる。STC条件は日射・気温の規定であり
    /// 風速は規定されないため、実運用では温度損失が発生する。
    /// </remarks>
    [Theory]
    [InlineData(PhotovoltaicPanel.MountType.RoofMount, PhotovoltaicPanel.MaterialType.Crystal)]
    [InlineData(PhotovoltaicPanel.MountType.RoofIntegrated, PhotovoltaicPanel.MaterialType.Crystal)]
    [InlineData(PhotovoltaicPanel.MountType.MountMode, PhotovoltaicPanel.MaterialType.Amorphous)]
    public void GetPower_AtSTC_CloseToRatedOutput(
        PhotovoltaicPanel.MountType mount, PhotovoltaicPanel.MaterialType material)
    {
      double peakPower = 5000;
      var panel = new PhotovoltaicPanel(
          peakPower, mount, material,
          Incline.Orientation.S, 30);

      //STC: 日射1000W/m²、気温25°C、風速5m/s
      //パネル温度は43〜48°Cになるため出力は定格の88〜98%程度
      double power = panel.GetPower(25, 5, 1000);
      double rated = peakPower * panel.InverterEfficiency;

      Assert.InRange(power, rated * 0.88, rated * 1.00);
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
          peakPower, PhotovoltaicPanel.MountType.MountMode,
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
          25, 2, 800, panel.PeakPower, panel.InverterEfficiency,
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

    #region IReadOnlyPhotovoltaicPanelのテスト

    /// <summary>IReadOnlyPhotovoltaicPanelとして参照できる</summary>
    [Fact]
    public void Panel_ImplementsIReadOnlyPhotovoltaicPanel()
    {
      var panel = new PhotovoltaicPanel(
          5000, PhotovoltaicPanel.MountType.RoofMount,
          PhotovoltaicPanel.MaterialType.Crystal, Incline.Orientation.S, 30);

      IReadOnlyPhotovoltaicPanel readOnly = panel;
      Assert.Equal(5000, readOnly.PeakPower, precision: 6);
      Assert.Equal(0.9, readOnly.InverterEfficiency, precision: 6);
    }

    #endregion

  }
}
