/* SunTests.cs
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

        #region Constant tests

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

        #region Constructor tests

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

        #region Solar position tests

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

        #region Sunrise and sunset time tests

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

        #region Extraterrestrial solar radiation tests

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

        #region Solar radiation conversion tests

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

        #region Direct/diffuse separation tests

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

        #region Solar radiation property tests

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

        #region Regression tests (DNI derivation near/below the horizon)

        /// <summary>
        /// 夜間（太陽高度 = 0 の番兵値）に GHI・DHI から DNI を求めても NaN にならず 0 になる
        /// （旧実装は 0/0 = NaN を返し、セッターの Math.Max(0, NaN) が NaN を保持していた）
        /// </summary>
        [Theory]
        [InlineData(0.0, 0.0)]    //0/0 → NaN だった
        [InlineData(50.0, 20.0)]  //正/0 → +∞ だった
        public void SetDirectNormalRadiation_SunBelowHorizon_ReturnsZero(double ghi, double dhi)
        {
            var sun = new Sun(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude);
            sun.Update(new DateTime(2024, 6, 22, 2, 0, 0)); //深夜2時
            Assert.Equal(0.0, sun.Altitude);

            sun.SetDirectNormalRadiation(ghi, dhi);

            Assert.Equal(0.0, sun.DirectNormalRadiation);
            Assert.Equal(ghi, sun.GlobalHorizontalRadiation);
            Assert.Equal(dhi, sun.DiffuseHorizontalRadiation);
        }

        /// <summary>静的メソッドも太陽高度 0 では DNI = 0 を返す（NaN/∞ にならない）</summary>
        [Fact]
        public void GetDirectNormalRadiation_AltitudeZero_ReturnsZero()
        {
            Assert.Equal(0.0, Sun.GetDirectNormalRadiation(0.0, 0.0, 0.0));
            Assert.Equal(0.0, Sun.GetDirectNormalRadiation(50.0, 20.0, 0.0));
            Assert.Equal(0.0, Sun.GetDirectNormalRadiation(50.0, 20.0, -0.1));
        }

        /// <summary>
        /// sin(高度) が閾値 0.02（≈1.15°）未満では DNI の逆算を行わず 0 を返す
        /// （WeatherCompleter の MinEffectiveSinH と同じ閾値）
        /// </summary>
        [Fact]
        public void GetDirectNormalRadiation_BelowMinimumAltitude_ReturnsZero()
        {
            double altitude = 0.5 * Math.PI / 180.0; //0.5°
            Assert.Equal(0.0, Sun.GetDirectNormalRadiation(30.0, 20.0, altitude));
        }

        /// <summary>
        /// 低高度で (GHI − DHI) / sin(h) が物理的上限を超える場合、
        /// 大気圏外法線面日射量（年最大値）で頭打ちになる
        /// </summary>
        [Fact]
        public void GetDirectNormalRadiation_LowAltitude_IsCappedAtExtraterrestrial()
        {
            double altitude = 2.0 * Math.PI / 180.0; //2°：sin ≈ 0.035
            double dni = Sun.GetDirectNormalRadiation(120.0, 20.0, altitude); //旧実装では ≈ 2865 W/m²
            Assert.True(double.IsFinite(dni));
            Assert.InRange(dni, 0.0, Sun.SolarConstant * 1.033 + 1e-9);
        }

        /// <summary>インスタンスメソッドでは当日の大気圏外法線面日射量で頭打ちになる</summary>
        [Fact]
        public void SetDirectNormalRadiation_LowAltitude_IsCappedAtDailyExtraterrestrial()
        {
            var sun = new Sun(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude);
            //日の出直後の低高度時刻を探す
            DateTime t = new DateTime(2024, 6, 22, 4, 0, 0);
            while (!(0.03 < Math.Sin(sun.Altitude) && Math.Sin(sun.Altitude) < 0.05))
            {
                t = t.AddMinutes(1);
                sun.Update(t);
            }
            sun.SetDirectNormalRadiation(200.0, 20.0);
            Assert.Equal(sun.GetExtraterrestrialRadiation(), sun.DirectNormalRadiation, precision: 9);
        }

        /// <summary>通常の太陽高度では従来の (GHI − DHI) / sin(h) と一致する（挙動不変）</summary>
        [Theory]
        [InlineData(700.0, 150.0, 1.0)]
        [InlineData(300.0, 100.0, 0.2)]
        [InlineData(40.0, 30.0, 0.03)]
        public void GetDirectNormalRadiation_NormalAltitude_Unchanged(
            double ghi, double dhi, double altitude)
        {
            Assert.Equal((ghi - dhi) / Math.Sin(altitude),
                Sun.GetDirectNormalRadiation(ghi, dhi, altitude));
        }

        /// <summary>日射量セッターは NaN・無限大を拒否する</summary>
        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void RadiationSetters_NonFiniteValue_Throws(double value)
        {
            var sun = new Sun(TokyoLatitude, TokyoLongitude, TokyoStandardLongitude);
            Assert.Throws<Popolo.Core.Exceptions.PopoloArgumentException>(
                () => sun.DirectNormalRadiation = value);
            Assert.Throws<Popolo.Core.Exceptions.PopoloArgumentException>(
                () => sun.DiffuseHorizontalRadiation = value);
            Assert.Throws<Popolo.Core.Exceptions.PopoloArgumentException>(
                () => sun.GlobalHorizontalRadiation = value);
        }

        #endregion

        #region Regression tests (separation fallback returns DNI)

        /// <summary>
        /// 大気透過率 = 1 でも推定値が観測 GHI に届かないフォールバック分岐でも、
        /// 出力は法線面直達日射（DNI）であり DNI·sin(h) + DHI = GHI が成り立つ
        /// （旧実装は水平面直達 DNI·sin(h) を返していた）
        /// </summary>
        [Theory]
        [InlineData(Sun.SeparationMethod.Berlage)]
        [InlineData(Sun.SeparationMethod.Watanabe)]
        public void SeparateGlobalHorizontalRadiation_Fallback_ReturnsNormalDirect(
            Sun.SeparationMethod method)
        {
            //東京, 夏至の正午。大気透過率 1 の推定値（≈ Io·sin h）を超える GHI を与える
            var dTime = new DateTime(2024, 6, 22, 12, 0, 0);
            double altitude = Sun.GetSunAltitude(
                TokyoLatitude, TokyoLongitude, TokyoStandardLongitude, dTime);
            double ghi = 1.05 * Sun.GetExtraterrestrialRadiation(dTime.DayOfYear) * Math.Sin(altitude);

            Sun.SeparateGlobalHorizontalRadiation(ghi,
                TokyoLatitude, TokyoLongitude, TokyoStandardLongitude,
                dTime, method, out double dni, out double dhi);

            Assert.Equal(ghi, dni * Math.Sin(altitude) + dhi, precision: 6);
        }

        #endregion

        #region Regression tests (city table)

        /// <summary>全都市で経度と標準子午線の差が 30° 未満、かつ標準子午線は 15° の倍数</summary>
        [Fact]
        public void CityTable_StandardMeridian_IsConsistentWithLongitude()
        {
            foreach (Sun.City city in Enum.GetValues(typeof(Sun.City)))
            {
                var sun = new Sun(city);
                Assert.True(Math.Abs(sun.Longitude - sun.StandardLongitude) < 30.0,
                    $"{city}: longitude {sun.Longitude}, standard meridian {sun.StandardLongitude}");
                Assert.True(Math.Abs(sun.StandardLongitude % 15.0) < 1e-9,
                    $"{city}: standard meridian {sun.StandardLongitude} is not a multiple of 15°");
                Assert.InRange(sun.Latitude, -90.0, 90.0);
                Assert.InRange(sun.Longitude, -180.0, 180.0);
            }
        }

        /// <summary>代表都市の標準子午線がタイムゾーン（UTC オフセット × 15°）と一致する</summary>
        [Theory]
        [InlineData(Sun.City.Bangkok, 105.0)]    //UTC+7（旧値 -75 は誤り）
        [InlineData(Sun.City.Tokyo, 135.0)]      //UTC+9
        [InlineData(Sun.City.Jakarta, 105.0)]    //UTC+7
        [InlineData(Sun.City.Singapore, 120.0)]  //UTC+8
        [InlineData(Sun.City.Beijing, 120.0)]    //UTC+8
        [InlineData(Sun.City.London, 0.0)]       //UTC+0
        [InlineData(Sun.City.Paris, 15.0)]       //UTC+1
        [InlineData(Sun.City.Cairo, 30.0)]       //UTC+2
        [InlineData(Sun.City.Moscow, 45.0)]      //UTC+3
        [InlineData(Sun.City.Sydney, 150.0)]     //UTC+10
        [InlineData(Sun.City.Auckland, 180.0)]   //UTC+12
        [InlineData(Sun.City.Bogota, -75.0)]     //UTC-5
        [InlineData(Sun.City.MexicoCity, -90.0)] //UTC-6
        [InlineData(Sun.City.SaoPaulo, -45.0)]   //UTC-3
        public void CityTable_StandardMeridian_MatchesTimeZone(Sun.City city, double expected)
        {
            Assert.Equal(expected, new Sun(city).StandardLongitude);
        }

        #endregion
    }
}
