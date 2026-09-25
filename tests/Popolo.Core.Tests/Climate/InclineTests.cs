/* InclineTests.cs
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
    /// <summary>Incline のテスト</summary>
    public class InclineTests
    {
        private const double Pi = Math.PI;
        private const double HalfPi = Math.PI / 2.0;

        #region Constructor tests

        /// <summary>水平面（傾斜角0）で正しく初期化される</summary>
        [Fact]
        public void Constructor_HorizontalSurface_CorrectProperties()
        {
            var incline = new Incline(0d, 0d);
            Assert.Equal(0, incline.HorizontalAngle, precision: 6);
            Assert.Equal(0, incline.VerticalAngle, precision: 6);
            Assert.Equal(1.0, incline.ConfigurationFactorToSky, precision: 6);
            Assert.Equal(0.0, incline.ConfigurationFactorToGround, precision: 6);
        }

        /// <summary>垂直面（傾斜角π/2）で正しく初期化される</summary>
        [Fact]
        public void Constructor_VerticalSurface_CorrectProperties()
        {
            var incline = new Incline(0d, HalfPi);
            Assert.Equal(HalfPi, incline.VerticalAngle, precision: 6);
            Assert.Equal(0.5, incline.ConfigurationFactorToSky, precision: 6);
            Assert.Equal(0.5, incline.ConfigurationFactorToGround, precision: 6);
        }

        /// <summary>方位角は -π〜π に正規化される</summary>
        [Theory]
        [InlineData(3 * Math.PI, Math.PI)]      //3π → π
        [InlineData(-3 * Math.PI, -Math.PI)]    //-3π → -π
        [InlineData(Math.PI / 2, Math.PI / 2)]  //π/2 → π/2（変化なし）
        public void Constructor_HorizontalAngle_IsNormalized(
            double input, double expected)
        {
            var incline = new Incline(input, HalfPi);
            Assert.Equal(expected, incline.HorizontalAngle, precision: 5);
        }

        /// <summary>16方位コンストラクタで南向き垂直面が正しく初期化される</summary>
        [Fact]
        public void Constructor_WithOrientation_SouthVertical_IsCorrect()
        {
            var incline = new Incline(Incline.Orientation.S, HalfPi);
            Assert.Equal(0, incline.HorizontalAngle, precision: 6);
            Assert.Equal(HalfPi, incline.VerticalAngle, precision: 6);
        }

        /// <summary>コピーコンストラクタで全プロパティが一致する</summary>
        [Fact]
        public void Constructor_Copy_AllPropertiesMatch()
        {
            var src = new Incline(Pi / 4, Pi / 3);
            var dst = new Incline(src);
            Assert.Equal(src.HorizontalAngle, dst.HorizontalAngle, precision: 6);
            Assert.Equal(src.VerticalAngle, dst.VerticalAngle, precision: 6);
            Assert.Equal(src.ConfigurationFactorToSky, dst.ConfigurationFactorToSky, precision: 6);
        }

        #endregion

        #region View factor tests

        /// <summary>形態係数の合計は常に1</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(Math.PI / 6)]
        [InlineData(Math.PI / 2)]
        [InlineData(Math.PI)]
        public void ConfigurationFactor_SumIsOne(double verticalAngle)
        {
            var incline = new Incline(0d, verticalAngle);
            Assert.Equal(1.0,
                incline.ConfigurationFactorToSky + incline.ConfigurationFactorToGround,
                precision: 6);
        }

        /// <summary>GetConfigurationFactorToSkyの静的メソッドとインスタンスプロパティが一致する</summary>
        [Fact]
        public void GetConfigurationFactorToSky_StaticAndInstanceMatch()
        {
            double va = Pi / 4;
            var incline = new Incline(0d, va);
            Assert.Equal(Incline.GetConfigurationFactorToSky(va),
                incline.ConfigurationFactorToSky, precision: 6);
        }

        #endregion

        #region Direct solar incidence tests

        /// <summary>水平面に対して真上からの太陽（高度π/2）のcosθは1</summary>
        [Fact]
        public void GetDirectSolarRadiationRate_VerticalSun_OnHorizontalSurface_IsOne()
        {
            var incline = new Incline(0d, 0d); //水平面
            double rate = incline.GetDirectSolarRadiationRatio(HalfPi, 0);
            Assert.Equal(1.0, rate, precision: 4);
        }

        /// <summary>面の裏側からの太陽のcosθは0（負にならない）</summary>
        [Fact]
        public void GetDirectSolarRadiationRate_SunBehindSurface_IsZero()
        {
            //南向き垂直面、太陽が真北方向から来る場合
            var incline = new Incline(0d, HalfPi); //南向き垂直
            double rate = incline.GetDirectSolarRadiationRatio(0.1, Pi); //太陽が北向き
            Assert.Equal(0, rate, precision: 4);
        }

        /// <summary>日射入射率は0〜1の範囲に収まる</summary>
        [Theory]
        [InlineData(Pi / 6, 0)]
        [InlineData(Pi / 4, Pi / 4)]
        [InlineData(Pi / 3, -Pi / 6)]
        public void GetDirectSolarRadiationRate_IsInRange(double altitude, double orientation)
        {
            var incline = new Incline(0d, HalfPi); //南向き垂直
            double rate = incline.GetDirectSolarRadiationRatio(altitude, orientation);
            Assert.InRange(rate, 0.0, 1.0);
        }

        #endregion

        #region Solar radiation on inclined surface tests

        /// <summary>水平面の全天日射量は太陽の直達と天空の合計と一致する</summary>
        [Fact]
        public void GetSolarIrradiance_HorizontalSurface_EqualsGHI()
        {
            var sun = new Sun(35.67, 139.75, 135.0);
            sun.Update(new DateTime(2024, 6, 22, 12, 0, 0));
            sun.DirectNormalRadiation = 600;
            sun.DiffuseHorizontalRadiation = 150;
            sun.GlobalHorizontalRadiation = sun.DirectNormalRadiation * Math.Sin(sun.Altitude)
                + sun.DiffuseHorizontalRadiation;

            var horizontal = new Incline(0d, 0d); //水平面
            double irradiance = horizontal.GetSolarIrradiance(sun, 0.2);

            //水平面では形態係数→天空=1,地面=0 なので直達+天空日射のみ
            double expected = sun.DirectNormalRadiation * Math.Sin(sun.Altitude)
                + sun.DiffuseHorizontalRadiation;
            Assert.Equal(expected, irradiance, precision: 4);
        }

        /// <summary>傾斜面日射量は直達+拡散の合計と一致する</summary>
        [Fact]
        public void GetSolarIrradiance_EqualsSumOfDirectAndDiffuse()
        {
            var sun = new Sun(35.67, 139.75, 135.0);
            sun.Update(new DateTime(2024, 6, 22, 12, 0, 0));
            sun.DirectNormalRadiation = 600;
            sun.DiffuseHorizontalRadiation = 150;
            sun.GlobalHorizontalRadiation = 700;

            var incline = new Incline(0d, HalfPi); //南向き垂直面
            double direct  = incline.GetDirectSolarIrradiance(sun);
            double diffuse = incline.GetDiffuseSolarIrradiance(sun, 0.2);
            double total   = incline.GetSolarIrradiance(sun, 0.2);

            Assert.Equal(direct + diffuse, total, precision: 6);
        }

        #endregion

        #region MakeReverseIncline tests

        /// <summary>逆向き面の方位角は反対方向</summary>
        [Fact]
        public void MakeReverseIncline_ReturnsOppositeDirection()
        {
            var south = new Incline(0d, HalfPi); //南向き垂直
            var reverse = south.MakeReverseIncline();
            //北向き垂直になるはず（方位角π）
            Assert.InRange(Math.Abs(reverse.HorizontalAngle), Pi - 0.01, Pi + 0.01);
        }

        #endregion

        #region Copy tests

        /// <summary>Copyで全プロパティが正しくコピーされる</summary>
        [Fact]
        public void Copy_AllPropertiesMatch()
        {
            var src = new Incline(Pi / 4, Pi / 3);
            var dst = new Incline(0d, 0d);
            dst.Copy(src);

            Assert.Equal(src.HorizontalAngle, dst.HorizontalAngle, precision: 6);
            Assert.Equal(src.VerticalAngle, dst.VerticalAngle, precision: 6);
            Assert.Equal(src.ConfigurationFactorToSky, dst.ConfigurationFactorToSky, precision: 6);
        }

        #endregion

        #region Regression tests (sun below the horizon)

        /// <summary>
        /// 太陽が地平線下（高度 = 0, 方位 = 0 の番兵値）のとき、南向き垂直面の cosθ は 0。
        /// 旧実装は番兵の方位 0（真南）により cosθ = 1 となり、夜間の DNI をそのまま受けていた
        /// </summary>
        [Fact]
        public void GetDirectSolarIrradiance_SunBelowHorizon_IsZero()
        {
            var sun = new Sun(35.67, 139.75, 135.0);
            sun.Update(new DateTime(2024, 6, 22, 2, 0, 0)); //深夜2時
            sun.DirectNormalRadiation = 100; //時刻ずれ等で夜間に DNI > 0 が入った場合

            var south = new Incline(0d, HalfPi);
            Assert.Equal(0.0, south.GetDirectSolarRadiationRatio(sun));
            Assert.Equal(0.0, south.GetDirectSolarIrradiance(sun));
            Assert.Equal(0.0, south.GetDirectSolarIlluminance(sun));
        }

        /// <summary>太陽が地平線上にあるときの cosθ は従来どおり（角度指定オーバーロードと一致）</summary>
        [Fact]
        public void GetDirectSolarRadiationRatio_SunAboveHorizon_Unchanged()
        {
            var sun = new Sun(35.67, 139.75, 135.0);
            sun.Update(new DateTime(2024, 6, 22, 9, 0, 0));
            var incline = new Incline(-Pi / 4, Pi / 3);
            Assert.Equal(incline.GetDirectSolarRadiationRatio(sun.Altitude, sun.Azimuth),
                incline.GetDirectSolarRadiationRatio(sun));
        }

        #endregion

        #region Regression tests (profile angle)

        /// <summary>
        /// 面座標系で独立に計算したプロファイル角の正接 tan = (s·u) / (s·n)。
        /// x = 南, y = 西, z = 上。n は面の法線、u は法線を含む鉛直面内で面に沿う「上向き」単位ベクトル
        /// </summary>
        private static double ReferenceTangentProfileAngle(
            double surfaceAzimuth, double tilt, double altitude, double azimuth)
        {
            double[] s = { Math.Cos(altitude) * Math.Cos(azimuth),
                           Math.Cos(altitude) * Math.Sin(azimuth), Math.Sin(altitude) };
            double[] n = { Math.Sin(tilt) * Math.Cos(surfaceAzimuth),
                           Math.Sin(tilt) * Math.Sin(surfaceAzimuth), Math.Cos(tilt) };
            double[] u = { -Math.Cos(tilt) * Math.Cos(surfaceAzimuth),
                           -Math.Cos(tilt) * Math.Sin(surfaceAzimuth), Math.Sin(tilt) };
            double su = s[0] * u[0] + s[1] * u[1] + s[2] * u[2];
            double sn = s[0] * n[0] + s[1] * n[1] + s[2] * n[2];
            return su / sn;
        }

        /// <summary>太陽が傾斜面の法線方向にあるときプロファイル角は 0</summary>
        [Theory]
        [InlineData(0.0, 0.6)]
        [InlineData(0.3, 0.6)]
        [InlineData(-1.2, 1.0)]
        [InlineData(2.5, 0.3)]
        public void GetProfileAngle_SunOnSurfaceNormal_IsZero(double surfaceAzimuth, double tilt)
        {
            var incline = new Incline(surfaceAzimuth, tilt);
            double altitude = HalfPi - tilt;
            double azimuth = surfaceAzimuth;
            Assert.Equal(1.0, incline.GetDirectSolarRadiationRatio(altitude, azimuth), precision: 12);
            Assert.Equal(0.0, incline.GetTangentProfileAngle(altitude, azimuth), precision: 12);
            Assert.Equal(0.0, incline.GetProfileAngle(altitude, azimuth), precision: 12);
        }

        /// <summary>
        /// 真南向き傾斜面（傾斜 β）に真南から高度 h の太陽：プロファイル角 = h + β − 90°
        /// （β = 90° の垂直面では太陽高度そのもの）
        /// </summary>
        [Theory]
        [InlineData(Math.PI / 6, 0.5)]
        [InlineData(Math.PI / 4, 0.9)]
        [InlineData(Math.PI / 3, 0.3)]
        [InlineData(Math.PI / 2, 0.7)]
        public void GetProfileAngle_SouthTiltedSurface_SunFromSouth(double tilt, double altitude)
        {
            var incline = new Incline(0d, tilt);
            Assert.Equal(altitude + tilt - HalfPi, incline.GetProfileAngle(altitude, 0.0), precision: 12);
        }

        /// <summary>面と太陽の方位を同じ角度だけ回転させてもプロファイル角は変わらない</summary>
        [Theory]
        [InlineData(-2.0)]
        [InlineData(-0.7)]
        [InlineData(0.0)]
        [InlineData(0.4)]
        [InlineData(1.9)]
        public void GetTangentProfileAngle_IsRotationInvariant(double rotation)
        {
            const double tilt = 0.5, altitude = 0.6, relativeAzimuth = 0.4;
            var reference = new Incline(0d, tilt);
            var rotated = new Incline(rotation, tilt);
            double expected = reference.GetTangentProfileAngle(altitude, relativeAzimuth);
            Assert.Equal(expected,
                rotated.GetTangentProfileAngle(altitude, rotation + relativeAzimuth), precision: 12);
        }

        /// <summary>任意の傾斜面について、ベクトル計算による参照値と一致する</summary>
        [Theory]
        [InlineData(0.0, 0.5, 0.6, 0.4)]
        [InlineData(0.8, 0.3, 0.9, 0.2)]
        [InlineData(-1.1, 1.2, 0.4, -0.9)]
        [InlineData(3.0, 0.7, 0.5, 2.8)]
        [InlineData(0.2, Math.PI / 2, 0.6, -0.3)]
        public void GetTangentProfileAngle_MatchesVectorReference(
            double surfaceAzimuth, double tilt, double altitude, double azimuth)
        {
            var incline = new Incline(surfaceAzimuth, tilt);
            Assert.True(0 < incline.GetDirectSolarRadiationRatio(altitude, azimuth));
            Assert.Equal(ReferenceTangentProfileAngle(surfaceAzimuth, tilt, altitude, azimuth),
                incline.GetTangentProfileAngle(altitude, azimuth), precision: 12);
        }

        /// <summary>垂直面では tan(プロファイル角) = tan(h) / cos(A − α) で、修正前と同一</summary>
        [Theory]
        [InlineData(0.0, 0.5, 0.3)]
        [InlineData(-HalfPi, 0.4, -1.0)]
        [InlineData(HalfPi, 0.8, 1.2)]
        [InlineData(Math.PI / 8 * 3, 0.2, 0.9)]
        public void GetTangentProfileAngle_VerticalWall_Unchanged(
            double surfaceAzimuth, double altitude, double azimuth)
        {
            var incline = new Incline(surfaceAzimuth, HalfPi);
            double expected = Math.Tan(altitude) / Math.Cos(azimuth - incline.HorizontalAngle);
            Assert.Equal(expected, incline.GetTangentProfileAngle(altitude, azimuth), precision: 12);
        }

        #endregion
    }
}
