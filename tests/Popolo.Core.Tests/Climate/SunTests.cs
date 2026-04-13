/* SunTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using System;
using Xunit;
using Popolo.Core.Climate;

namespace Popolo.Core.Tests.Climate
{
    /// <summary>Sun のテスト</summary>
    /// <remarks>
    /// 期待値の根拠：
    /// - 宿谷昌則「数値計算で学ぶ光と熱の建築環境学」丸善, 1993
    /// - 宇田川光弘「パソコンによる空気調和計算法」1986
    /// - 東京（北緯35.67°、東経139.75°、標準経度135°）を基準地点とする
    /// </remarks>
    public class SunTests
    {
        //東京の位置情報
        private const double TokyoLatitude = 35.67;
        private const double TokyoLongitude = 139.75;
        private const double TokyoStandardLongitude = 135.0;

        #region 定数のテスト

        /// <summary>太陽定数が正しい値を持つ</summary>
        [Fact]
        public void SolarConstant_HasCorrectValue()
        {
            Assert.Equal(1367.0, Sun.SolarConstant, precision: 1);
        }

        /// <summary>発光効率が正しい値を持つ</summary>
        [Fact]
        public void SolarLuminousEfficacy_HasCorrectValue()
        {
            Assert.Equal(93.9, Sun.SolarLuminousEfficacy, precision: 1);
        }

        #endregion

        #region コンストラクタのテスト

        /// <summary>緯度・経度・標準経度で初期化できる</summary>
        [Fact]
        public void Constructor_WithLatLon_SetsProperties()
        {
            var sun = new Sun(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude);
            Assert.Equal(TokyoLatitude, sun.Latitude, precision: 6);
            Assert.Equal(TokyoLongitude, sun.Longitude, precision: 6);
            Assert.Equal(TokyoStandardLongitude, sun.StandardLongitude, precision: 6);
        }

        /// <summary>都市名で初期化できる</summary>
        [Fact]
        public void Constructor_WithCity_SetsTokyoLocation()
        {
            var sun = new Sun(Sun.City.Tokyo);
            Assert.InRange(sun.Latitude, 35.0, 36.0);
            Assert.InRange(sun.Longitude, 139.0, 141.0);
            Assert.Equal(135.0, sun.StandardLongitude, precision: 1);
        }

        /// <summary>度分秒で初期化した結果が度数法と一致する</summary>
        [Fact]
        public void Constructor_WithDMS_EqualsDecimalDegrees()
        {
            //東京：北緯35°40'、東経139°45'、標準経度135°
            var sunDMS = new Sun(35, 40, 0, 139, 45, 0, 135, 0, 0);
            var sunDeg = new Sun(35 + 40.0 / 60, 139 + 45.0 / 60, 135);
            Assert.Equal(sunDeg.Latitude, sunDMS.Latitude, precision: 4);
            Assert.Equal(sunDeg.Longitude, sunDMS.Longitude, precision: 4);
        }

        #endregion

        #region 太陽位置のテスト

        /// <summary>夏至の正午付近で太陽高度が高い（東京）</summary>
        [Fact]
        public void GetSunPosition_SummerSolsticeNoon_HighAltitude()
        {
            //夏至（6月22日）の正午付近
            var dTime = new DateTime(2024, 6, 22, 12, 0, 0);
            Sun.GetSunPosition(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude,
                dTime, out double altitude, out double orientation);

            //東京の夏至の南中高度 ≒ 90 - 35.67 + 23.44 ≒ 77.8°
            Assert.InRange(altitude * 180 / Math.PI, 70.0, 82.0);
        }

        /// <summary>冬至の正午付近で太陽高度が低い（東京）</summary>
        [Fact]
        public void GetSunPosition_WinterSolsticeNoon_LowAltitude()
        {
            var dTime = new DateTime(2024, 12, 22, 12, 0, 0);
            Sun.GetSunPosition(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude,
                dTime, out double altitude, out double orientation);

            //東京の冬至の南中高度 ≒ 90 - 35.67 - 23.44 ≒ 30.9°
            Assert.InRange(altitude * 180 / Math.PI, 25.0, 36.0);
        }

        /// <summary>夏至の方が冬至より太陽高度が高い</summary>
        [Fact]
        public void GetSunPosition_SummerAltitude_GreaterThanWinter()
        {
            var summer = new DateTime(2024, 6, 22, 12, 0, 0);
            var winter = new DateTime(2024, 12, 22, 12, 0, 0);

            Sun.GetSunPosition(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude,
                summer, out double altSummer, out _);
            Sun.GetSunPosition(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude,
                winter, out double altWinter, out _);

            Assert.True(altSummer > altWinter);
        }

        /// <summary>日の出前は太陽高度が0</summary>
        [Fact]
        public void GetSunPosition_BeforeSunrise_AltitudeIsZero()
        {
            var dTime = new DateTime(2024, 6, 22, 2, 0, 0); //深夜2時
            Sun.GetSunPosition(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude,
                dTime, out double altitude, out double orientation);

            Assert.Equal(0, altitude, precision: 6);
            Assert.Equal(0, orientation, precision: 6);
        }

        /// <summary>Update後にCurrentDateTimeが更新される</summary>
        [Fact]
        public void Update_SetsCurrentDateTime()
        {
            var sun = new Sun(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude);
            var dTime = new DateTime(2024, 6, 22, 12, 0, 0);
            sun.Update(dTime);

            Assert.Equal(dTime, sun.CurrentDateTime);
            Assert.True(sun.Altitude > 0);
        }

        #endregion

        #region 日の出・日没時刻のテスト

        /// <summary>夏至の日の出は冬至より早い（東京）</summary>
        [Fact]
        public void GetSunRiseTime_SummerEarlierThanWinter()
        {
            var summer = new DateTime(2024, 6, 22);
            var winter = new DateTime(2024, 12, 22);

            var riseSummer = Sun.GetSunRiseTime(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude, summer);
            var riseWinter = Sun.GetSunRiseTime(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude, winter);

            Assert.True(riseSummer.TimeOfDay < riseWinter.TimeOfDay);
        }

        /// <summary>夏至の日没は冬至より遅い（東京）</summary>
        [Fact]
        public void GetSunSetTime_SummerLaterThanWinter()
        {
            var summer = new DateTime(2024, 6, 22);
            var winter = new DateTime(2024, 12, 22);

            var setSummer = Sun.GetSunSetTime(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude, summer);
            var setWinter = Sun.GetSunSetTime(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude, winter);

            Assert.True(setSummer.TimeOfDay > setWinter.TimeOfDay);
        }

        /// <summary>日の出は日没より前</summary>
        [Fact]
        public void GetSunRiseTime_IsBeforeSunSet()
        {
            var dTime = new DateTime(2024, 6, 22);
            var rise = Sun.GetSunRiseTime(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude, dTime);
            var set  = Sun.GetSunSetTime(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude, dTime);

            Assert.True(rise < set);
        }

        /// <summary>インスタンスメソッドと静的メソッドの日の出時刻が一致する</summary>
        [Fact]
        public void GetSunRiseTime_InstanceAndStaticReturnSameValue()
        {
            var dTime = new DateTime(2024, 6, 22, 12, 0, 0);
            var sun = new Sun(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude);
            sun.Update(dTime);

            var riseInstance = sun.GetSunRiseTime();
            var riseStatic = Sun.GetSunRiseTime(
                TokyoLatitude, TokyoLongitude, TokyoStandardLongitude, dTime);

            Assert.Equal(riseInstance, riseStatic);
        }

        #endregion

        #region 大気圏外日射量のテスト

        /// <summary>大気圏外日射量は太陽定数の±3.3%以内に収まる</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(172)] //夏至付近
        [InlineData(355)] //冬至付近
        public void GetExtraterrestrialRadiation_WithinSolarConstantRange(int dayOfYear)
        {
            double io = Sun.GetExtraterrestrialRadiation(dayOfYear);
            Assert.InRange(io,
                Sun.SolarConstant * (1 - 0.033),
                Sun.SolarConstant * (1 + 0.033));
        }

        /// <summary>冬至付近（近日点）で大気圏外日射量が最大、夏至付近（遠日点）で最小</summary>
        [Fact]
        public void GetExtraterrestrialRadiation_PerihelionLargerThanAphelion()
        {
            double ioWinter = Sun.GetExtraterrestrialRadiation(1);   //1月1日（近日点付近）
            double ioSummer = Sun.GetExtraterrestrialRadiation(172);  //夏至付近（遠日点）
            Assert.True(ioWinter > ioSummer);
        }

        #endregion

        #region 日射相互変換のテスト

        /// <summary>直達・天空・全天日射の相互変換が整合する</summary>
        [Theory]
        [InlineData(500, 150, 0.5)]  //晴天
        [InlineData(200, 180, 0.3)]  //曇天
        public void RadiationConversions_AreConsistent(
            double dni, double dhi, double altitudeRad)
        {
            double ghi = Sun.GetGlobalHorizontalRadiation(dhi, dni, altitudeRad);
            double dniRecovered = Sun.GetDirectNormalRadiation(ghi, dhi, altitudeRad);
            double dhiRecovered = Sun.GetDiffuseHorizontalRadiation(dni, ghi, altitudeRad);

            Assert.Equal(dni, dniRecovered, precision: 4);
            Assert.Equal(dhi, dhiRecovered, precision: 4);
        }

        #endregion

        #region 直散分離のテスト

        /// <summary>直散分離後の直達・天空日射量の和が全天日射量と一致する</summary>
        [Theory]
        [InlineData(Sun.SeparationMethod.Erbs)]
        [InlineData(Sun.SeparationMethod.Udagawa)]
        [InlineData(Sun.SeparationMethod.Miki)]
        [InlineData(Sun.SeparationMethod.Watanabe)]
        [InlineData(Sun.SeparationMethod.Berlage)]
        public void SeparateGlobalHorizontalRadiation_SumEqualsGHI(
            Sun.SeparationMethod method)
        {
            //東京, 2024年6月22日正午（晴天想定）
            var dTime = new DateTime(2024, 6, 22, 12, 0, 0);
            double ghi = 700; // W/m²

            Sun.SeparateGlobalHorizontalRadiation(ghi,
                TokyoLatitude, TokyoLongitude, TokyoStandardLongitude,
                dTime, method,
                out double dni, out double dhi);

            double altitude = Sun.GetSunAltitude(
                TokyoLatitude, TokyoLongitude, TokyoStandardLongitude, dTime);
            double ghiRecovered = dni * Math.Sin(altitude) + dhi;

            Assert.Equal(ghi, ghiRecovered, precision: 0);
        }

        /// <summary>夜間は直達・天空日射量がともに0</summary>
        [Fact]
        public void SeparateGlobalHorizontalRadiation_NightTime_ReturnsZero()
        {
            var dTime = new DateTime(2024, 6, 22, 2, 0, 0); //深夜2時
            Sun.SeparateGlobalHorizontalRadiation(100,
                TokyoLatitude, TokyoLongitude, TokyoStandardLongitude,
                dTime, Sun.SeparationMethod.Erbs,
                out double dni, out double dhi);

            Assert.Equal(0, dni, precision: 6);
            Assert.Equal(0, dhi, precision: 6);
        }

        #endregion

        #region 日射量のプロパティテスト

        /// <summary>DirectNormalRadiationに負の値を設定しても0になる</summary>
        [Fact]
        public void DirectNormalRadiation_NegativeValue_ClampedToZero()
        {
            var sun = new Sun(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude);
            sun.DirectNormalRadiation = -100;
            Assert.Equal(0, sun.DirectNormalRadiation, precision: 6);
        }

        /// <summary>SeparateGlobalHorizontalRadiationでインスタンス状態が更新される</summary>
        [Fact]
        public void SeparateGlobalHorizontalRadiation_UpdatesInstanceState()
        {
            var sun = new Sun(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude);
            sun.Update(new DateTime(2024, 6, 22, 12, 0, 0));

            double ghi = 700;
            sun.SeparateGlobalHorizontalRadiation(ghi, Sun.SeparationMethod.Erbs);

            Assert.Equal(ghi, sun.GlobalHorizontalRadiation, precision: 6);
            Assert.True(sun.DirectNormalRadiation >= 0);
            Assert.True(sun.DiffuseHorizontalRadiation >= 0);
        }

        #endregion
    }
}
