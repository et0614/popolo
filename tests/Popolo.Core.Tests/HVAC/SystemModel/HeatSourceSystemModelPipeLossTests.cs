/* HeatSourceSystemModelPipeLossTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * Licensed under the Apache License, Version 2.0 - see the accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.SystemModel;
using Popolo.Core.Physics;

namespace Popolo.Core.Tests.HVAC.SystemModel
{
    /// <summary>
    /// <see cref="HeatSourceSystemModel.PipeHeatLossRate"/> の配管熱損失補正のテスト。
    /// </summary>
    /// <remarks>
    /// 熱源サブシステムには、受け取った還水温度を記録するだけのテスト用実装を用いる。
    /// </remarks>
    public class HeatSourceSystemModelPipeLossTests
    {
        /// <summary>受け取った還水温度を記録する熱源（能力は十分で、常に設定値で供給する）。</summary>
        private sealed class RecordingSource : IHeatSourceSubSystem
        {
            public HeatSourceSystemModel.OperatingMode SelectableMode
                => HeatSourceSystemModel.OperatingMode.Cooling | HeatSourceSystemModel.OperatingMode.Heating;
            public HeatSourceSystemModel.OperatingMode Mode { get; set; }
            public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(30, 0.015);
            public DateTime CurrentDateTime { get; set; }
            public double TimeStep { get; set; }
            public bool IsOverLoad_C => false;
            public bool IsOverLoad_H => false;
            public double HotWaterReturnTemperature { get; set; }
            public double HotWaterSupplyTemperatureSetpoint { get; set; }
            public double HotWaterSupplyTemperature => HotWaterSupplyTemperatureSetpoint;
            public double HotWaterFlowRate { get; private set; }
            public double MaxHotWaterFlowRate => 100;
            public double MinHotWaterFlowRatio => 0;
            public double ChilledWaterReturnTemperature { get; set; }
            public double ChilledWaterSupplyTemperatureSetpoint { get; set; }
            public double ChilledWaterSupplyTemperature => ChilledWaterSupplyTemperatureSetpoint;
            public double ChilledWaterFlowRate { get; private set; }
            public double MaxChilledWaterFlowRate => 100;
            public double MinChilledWaterFlowRatio => 0;

            /// <summary>Forecast 時点で受け取っていた冷水・温水の還水温度。</summary>
            public double SeenChilledReturn { get; private set; } = double.NaN;
            public double SeenHotReturn { get; private set; } = double.NaN;

            public void ForecastSupplyWaterTemperature(double chilledWaterFlowRate, double hotWaterFlowRate)
            {
                ChilledWaterFlowRate = chilledWaterFlowRate;
                HotWaterFlowRate = hotWaterFlowRate;
                if (0 < chilledWaterFlowRate) SeenChilledReturn = ChilledWaterReturnTemperature;
                if (0 < hotWaterFlowRate) SeenHotReturn = HotWaterReturnTemperature;
            }

            public void FixState() { }
            public void ShutOff() { }
        }

        private static (HeatSourceSystemModel, RecordingSource) MakeModel(double pipeHeatLossRate)
        {
            var src = new RecordingSource();
            var hs = new HeatSourceSystemModel(new IHeatSourceSubSystem[] { src });
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.HeatingAndCooling);
            hs.SetChillingOperationSequence(0, 1);
            hs.SetHeatingOperationSequence(0, 1);
            hs.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            hs.HotWaterSupplyTemperatureSetpoint = 45.0;
            hs.PipeHeatLossRate = pipeHeatLossRate;
            return (hs, src);
        }

        /// <summary>
        /// 配管熱損失率 0.08 では、熱源に渡る還水温度は供給・還水温度差の 8% だけ設定値から遠ざかり、
        /// 熱源は二次側負荷の 1.08 倍を処理する。旧実装は補正値を公開プロパティにだけ格納し、
        /// 熱源には補正前の還水温度を渡していた。
        /// </summary>
        [Fact]
        public void PipeHeatLossRate_IsAppliedToHeatSourceInlet()
        {
            var (hs, src) = MakeModel(0.08);
            hs.ForecastSupplyWaterTemperature(10.0, 12.0, 10.0, 40.0);

            // 冷水: 12 + (12 − 7)·0.08 = 12.4 °C、温水: 40 − (45 − 40)·0.08 = 39.6 °C
            Assert.Equal(12.4, src.SeenChilledReturn, 12);
            Assert.Equal(39.6, src.SeenHotReturn, 12);
            // 公開プロパティと熱源への入力が一致する
            Assert.Equal(hs.ChilledWaterReturnTemperature, src.SeenChilledReturn, 12);
            Assert.Equal(hs.HotWaterReturnTemperature, src.SeenHotReturn, 12);
        }

        /// <summary>配管熱損失率 0 では補正されず、還水温度がそのまま熱源に渡る。</summary>
        [Fact]
        public void PipeHeatLossRate_Zero_PassesReturnTemperatureUnchanged()
        {
            var (hs, src) = MakeModel(0.0);
            hs.ForecastSupplyWaterTemperature(10.0, 12.0, 10.0, 40.0);
            Assert.Equal(12.0, src.SeenChilledReturn, 12);
            Assert.Equal(40.0, src.SeenHotReturn, 12);
        }
    }
}
