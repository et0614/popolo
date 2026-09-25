/* ObsoleteAliasTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * Licensed under the Apache License, Version 2.0 - see the accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.Building.Envelope;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.HVAC.Storage;
using Popolo.Core.OccupantBehavior;

// 綴り誤りのため廃止予定とした旧名メンバーの互換性を検証するので、CS0618 を抑止する
#pragma warning disable CS0618

namespace Popolo.Core.Tests
{
    /// <summary>
    /// 4.0 で綴りを訂正したメンバーについて、旧名（[Obsolete]）が新名と同じ値を返し、
    /// 設定も新名に反映されることを確認する。
    /// </summary>
    public class ObsoleteAliasTests
    {
        /// <summary>OfficeTenant.IsBuisinessHours は IsBusinessHours と同じ結果を返す。</summary>
        [Fact]
        public void OfficeTenant_IsBuisinessHours_ForwardsToIsBusinessHours()
        {
            var tenant = new OfficeTenant(OfficeTenant.CategoryOfIndustry.Manufacturing, 200,
                OfficeTenant.DaysOfWeek.Saturday | OfficeTenant.DaysOfWeek.Sunday, 1,
                8, 30, 17, 15, 12, 0, 13, 0);
            foreach (int h in new[] { 7, 9, 12, 17, 18 })
            {
                var t = new DateTime(2025, 2, 4, h, 0, 0);
                Assert.Equal(tenant.IsBusinessHours(t), tenant.IsBuisinessHours(t));
                IReadOnlyOfficeTenant ro = tenant;
                Assert.Equal(tenant.IsBusinessHours(t), ro.IsBuisinessHours(t));
            }
        }

        /// <summary>WaterPipe.OutletWaterTemperauture は OutletWaterTemperature と同じ値を返す。</summary>
        [Fact]
        public void WaterPipe_OutletWaterTemperauture_ForwardsToOutletWaterTemperature()
        {
            var pipe = new WaterPipe(50, 0.05, WaterPipe.Material.CarbonSteel);
            pipe.SetPipeThermalConductivity(50);
            pipe.UpdateHeatFlow(7.0, 0.002, 30.0, 0.015);
            Assert.Equal(pipe.OutletWaterTemperature, pipe.OutletWaterTemperauture);
            IReadOnlyWaterPipe ro = pipe;
            Assert.Equal(pipe.OutletWaterTemperature, ro.OutletWaterTemperauture);
        }

        /// <summary>成層蓄熱槽の Upper/LowerOutletTemperarture は新名と同じ値を返す。</summary>
        [Fact]
        public void StratifiedTank_OutletTemperarture_ForwardsToOutletTemperature()
        {
            var tank = new MultipleStratifiedWaterTank(4.0, 25.0, 0.2, 3.8, 20);
            tank.InitializeTemperature(6.0);
            tank.ForecastState(15.0, 0.01, true);
            Assert.Equal(tank.UpperOutletTemperature, tank.UpperOutletTemperarture);
            Assert.Equal(tank.LowerOutletTemperature, tank.LowerOutletTemperarture);
            IReadOnlyMultipleStratifiedWaterTank ro = tank;
            Assert.Equal(tank.UpperOutletTemperature, ro.UpperOutletTemperarture);
            Assert.Equal(tank.LowerOutletTemperature, ro.LowerOutletTemperarture);
        }

        /// <summary>連結完全混合槽の WaterOutletTemperarture は新名と同じ値を返す。</summary>
        [Fact]
        public void ConnectedTank_WaterOutletTemperarture_ForwardsToWaterOutletTemperature()
        {
            var tank = new MultiConnectedWaterTank(new double[] { 1.0, 1.0, 1.0 });
            tank.InitializeTemperature(new double[] { 5.0, 10.0, 15.0 });
            Assert.Equal(tank.WaterOutletTemperature, tank.WaterOutletTemperarture);
            IReadOnlyMultiConnectedWaterTank ro = tank;
            Assert.Equal(tank.WaterOutletTemperature, ro.WaterOutletTemperarture);
        }

        /// <summary>Regulator.LinearCharactaristicWeight の取得・設定は新名と連動する。</summary>
        [Fact]
        public void Regulator_LinearCharactaristicWeight_ForwardsToLinearCharacteristicWeight()
        {
            var reg = new Regulator(0.03, 100, 50, 0.5);
            reg.LinearCharactaristicWeight = 0.3;
            Assert.Equal(0.3, reg.LinearCharacteristicWeight);
            reg.LinearCharacteristicWeight = 0.7;
            Assert.Equal(0.7, reg.LinearCharactaristicWeight);
        }

        /// <summary>GetGeometricCompfigulation は GetGeometricConfiguration と同じ結果を返す。</summary>
        [Fact]
        public void AirToWaterCoil_GetGeometricCompfigulation_ForwardsToGetGeometricConfiguration()
        {
            AirToWaterCrossFinHeatExchanger.GetGeometricConfiguration(
                0.2, 1.0, 0.6, 16, 8, 0.002, 0.0002, 0.012, 0.016,
                out double r1, out double a1, out double f1, out double d1, out double s1);
            AirToWaterCrossFinHeatExchanger.GetGeometricCompfigulation(
                0.2, 1.0, 0.6, 16, 8, 0.002, 0.0002, 0.012, 0.016,
                out double r2, out double a2, out double f2, out double d2, out double s2);
            Assert.Equal(r1, r2);
            Assert.Equal(a1, a2);
            Assert.Equal(f1, f2);
            Assert.Equal(d1, d2);
            Assert.Equal(s1, s2);
        }

        /// <summary>日射遮蔽物の Pulldowned の取得・設定は IsPulledDown と連動する。</summary>
        [Fact]
        public void ShadingDevices_Pulldowned_ForwardsToIsPulledDown()
        {
            IShadingDevice[] devices =
            {
                new NoShadingDevice(),
                new SimpleShadingDevice(SimpleShadingDevice.PredefinedDevice.BrightVenetianBlind),
                new VenetianBlind(25, 21, 0.05, 0.02, 0.6, 0.45),
            };
            foreach (var d in devices)
            {
                d.Pulldowned = true;
                Assert.True(d.IsPulledDown);
                d.IsPulledDown = false;
                Assert.False(d.Pulldowned);
            }
        }

        /// <summary>旧名のみを実装した利用者独自の IShadingDevice でも、新名が既定実装で使える。</summary>
        [Fact]
        public void ShadingDevice_LegacyImplementation_GetsIsPulledDownByDefault()
        {
            IShadingDevice legacy = new LegacyShadingDevice();
            legacy.IsPulledDown = true;
            Assert.True(((LegacyShadingDevice)legacy).Pulldowned);
            Assert.True(legacy.IsPulledDown);
        }

        /// <summary>4.0 以前の書き方（旧名のみを実装）の日射遮蔽物。</summary>
        private sealed class LegacyShadingDevice : IShadingDevice
        {
            public string Kind => "legacy";
            public bool Pulldowned { get; set; }
            public bool HasPropertyChanged => false;
            public double ProfileAngle { get; set; }
            public void ComputeOpticalProperties(bool isDiffuseIrradianceProperties, bool irradianceFromSideF,
                out double transmittance, out double reflectance)
            { transmittance = 1; reflectance = 0; }
        }
    }
}
