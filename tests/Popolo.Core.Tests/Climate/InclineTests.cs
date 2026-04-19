/* InclineTests.cs
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

namespace Popolo.Core.Tests.Climate
{
    /// <summary>Incline のテスト</summary>
    public class InclineTests
    {
        private const double Pi = Math.PI;
        private const double HalfPi = Math.PI / 2.0;

        #region コンストラクタのテスト

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

        #region 形態係数のテスト

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

        #region 直達日射入射率のテスト

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

        #region 傾斜面日射量のテスト

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

        #region MakeReverseInclineのテスト

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

        #region CopyのテストI

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
    }
}
