/* HVACSystemModelTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * Licensed under the Apache License, Version 2.0 - see the accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.Core.HVAC.SystemModel;
using Popolo.Core.Physics;

namespace Popolo.Core.Tests.HVAC.SystemModel
{
    /// <summary>Unit tests for <see cref="HVACSystemModel"/>.</summary>
    /// <remarks>
    /// 二次側空調システムと熱源サブシステムには、挙動を固定したテスト用の実装を用いる。
    /// 二次側は供給水温に対して一定流量・一定温度差で還水を返し、熱源は能力 0（還水をそのまま
    /// 供給）とすることで、上限・下限温度でも負荷を処理できない（未処理負荷が生じる）状態を作る。
    /// </remarks>
    public class HVACSystemModelTests
    {
        private const double Cp = 4.186;

        /// <summary>供給水温 + 一定温度差で還水を返す二次側システム。</summary>
        private sealed class FixedLoadACSystem : IAirConditioningSystemModel
        {
            private readonly double flow, dT;
            private readonly BuildingThermalModel bModel;
            public FixedLoadACSystem(BuildingThermalModel bModel, double flow, double dT)
            { this.bModel = bModel; this.flow = flow; this.dT = dT; }

            public DateTime CurrentDateTime { get; set; }
            public double TimeStep { get; set; }
            public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(30, 0.015);
            public IReadOnlyBuildingThermalModel BuildingThermalModel => bModel;
            public double ChilledWaterSupplyTemperature { get; private set; }
            public double ChilledWaterReturnTemperature { get; private set; }
            public double ChilledWaterFlowRate { get; private set; }
            public double HotWaterSupplyTemperature { get; private set; }
            public double HotWaterReturnTemperature { get; private set; }
            public double HotWaterFlowRate { get; private set; }

            public void ForecastReturnWaterTemperature(double tcs, double ths)
            {
                ChilledWaterSupplyTemperature = tcs;
                HotWaterSupplyTemperature = ths;
                ChilledWaterFlowRate = HotWaterFlowRate = flow;
                ChilledWaterReturnTemperature = tcs + dT;
                HotWaterReturnTemperature = ths - dT;
                bModel.ForecastHeatTransfer();
                bModel.ForecastWaterTransfer();
            }

            public void FixState() { }
        }

        /// <summary>能力 0 の熱源（冷温水とも還水温度のまま供給し、常に過負荷）。</summary>
        private sealed class NoCapacitySource : IHeatSourceSubSystem
        {
            public HeatSourceSystemModel.OperatingMode SelectableMode
                => HeatSourceSystemModel.OperatingMode.Cooling | HeatSourceSystemModel.OperatingMode.Heating;
            public HeatSourceSystemModel.OperatingMode Mode { get; set; }
            public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(30, 0.015);
            public DateTime CurrentDateTime { get; set; }
            public double TimeStep { get; set; }
            public bool IsOverLoad_C { get; private set; }
            public bool IsOverLoad_H { get; private set; }
            public double HotWaterReturnTemperature { get; set; }
            public double HotWaterSupplyTemperatureSetpoint { get; set; }
            public double HotWaterSupplyTemperature { get; private set; }
            public double HotWaterFlowRate { get; private set; }
            public double MaxHotWaterFlowRate => 100;
            public double MinHotWaterFlowRatio => 0;
            public double ChilledWaterReturnTemperature { get; set; }
            public double ChilledWaterSupplyTemperatureSetpoint { get; set; }
            public double ChilledWaterSupplyTemperature { get; private set; }
            public double ChilledWaterFlowRate { get; private set; }
            public double MaxChilledWaterFlowRate => 100;
            public double MinChilledWaterFlowRatio => 0;

            public void ForecastSupplyWaterTemperature(double chilledWaterFlowRate, double hotWaterFlowRate)
            {
                ChilledWaterFlowRate = chilledWaterFlowRate;
                HotWaterFlowRate = hotWaterFlowRate;
                ChilledWaterSupplyTemperature = ChilledWaterReturnTemperature;
                HotWaterSupplyTemperature = HotWaterReturnTemperature;
                IsOverLoad_C = 0 < chilledWaterFlowRate;
                IsOverLoad_H = 0 < hotWaterFlowRate;
            }

            public void FixState() { }
            public void ShutOff() { IsOverLoad_C = IsOverLoad_H = false; }
        }

        private static BuildingThermalModel MakeBuilding()
        {
            var zone = new Zone("Room A", 120.0, 10.0);
            var wall = new Wall(12.0, new[] { new WallLayer("Concrete", 1.4, 1934, 0.15) });
            wall.ID = 0;
            var mRooms = new MultiRoom(1, new[] { zone }, new[] { wall }, Array.Empty<Window>());
            mRooms.AddZone(0, 0);
            mRooms.AddWall(0, 0, true);
            mRooms.SetOutsideWall(0, true, new Incline(0d, Math.PI / 2));

            var model = new BuildingThermalModel(new[] { mRooms });
            model.TimeStep = 3600;
            model.UpdateOutdoorCondition(
                new DateTime(2026, 7, 18, 12, 0, 0), new Sun(35.6812, 139.7671, 135.0), 30.0, 0.015, 0.0);
            return model;
        }

        private static HVACSystemModel MakeModel()
        {
            BuildingThermalModel bModel = MakeBuilding();
            var ac = new FixedLoadACSystem(bModel, 10.0, 5.0);
            var hs = new HeatSourceSystemModel(new IHeatSourceSubSystem[] { new NoCapacitySource() });
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.HeatingAndCooling);
            hs.SetChillingOperationSequence(0, 1);
            hs.SetHeatingOperationSequence(0, 1);
            hs.PipeHeatLossRate = 0;
            var model = new HVACSystemModel(bModel, new IAirConditioningSystemModel[] { ac }, hs);
            model.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            model.ChilledWaterUpperLimitTemperature = 15.0;
            model.HotWaterSupplyTemperatureSetpoint = 45.0;
            model.HotWaterLowerLimitTemperature = 35.0;
            return model;
        }

        /// <summary>
        /// 上限温度でも冷房負荷を処理できない場合、冷水供給温度は建物側を評価した上限温度になり、
        /// 未処理負荷は上限温度からの不足分として計上される。
        /// 旧実装は冷水供給温度を更新せず、前ステップ（初期値 7 °C）の値が残っていた。
        /// </summary>
        [Fact]
        public void Update_UnmetCoolingLoad_SupplyTemperatureIsUpperLimit()
        {
            HVACSystemModel model = MakeModel();
            model.Update();
            Assert.Equal(15.0, model.ChilledWaterSupplyTemperature, 10);
            // 能力 0 の熱源は還水（上限 15 °C + 5 K = 20 °C）をそのまま供給する
            Assert.Equal(10.0 * Cp * 5.0, model.RemainingCoolingLoad, 6);
        }

        /// <summary>
        /// 下限温度でも暖房負荷を処理できない場合、温水供給温度は建物側を評価した下限温度になる。
        /// 旧実装は温水供給温度を更新せず、前ステップ（初期値 45 °C）の値が残っていた。
        /// </summary>
        [Fact]
        public void Update_UnmetHeatingLoad_SupplyTemperatureIsLowerLimit()
        {
            HVACSystemModel model = MakeModel();
            model.Update();
            Assert.Equal(35.0, model.HotWaterSupplyTemperature, 10);
            Assert.Equal(10.0 * Cp * 5.0, model.RemainingHeatingLoad, 6);
        }
    }
}
