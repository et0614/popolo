/* AHUSystemTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * Licensed under the Apache License, Version 2.0 - see the accompanying LICENSE file.
 */

using Xunit;
using Popolo.Core.HVAC.AirSide;
using Popolo.Core.HVAC.SystemModel;

namespace Popolo.Core.Tests.HVAC.SystemModel
{
    /// <summary>Unit tests for <see cref="AHUSystem"/> (control settings).</summary>
    /// <remarks>
    /// 制御設定（ControlZoneTemperature / ShutOff）が制御器の状態に正しく反映されることを確認する。
    /// これらのメソッドは建物モデルや空調機の計算を行わないため、建物モデルは null、
    /// 空調機は配列長のみを与えている（制御器は空調機の台数分生成される）。
    /// </remarks>
    public class AHUSystemTests
    {
        private static AHUSystem MakeCAVSystem()
        {
            var sys = new AHUSystem(null!, new AirHandlingUnit[1]);
            sys.SetCAV(0, new[]
            {
                new AHUSystem.VolumeController(0, 0, 1.0, 0.8, 0.3),
                new AHUSystem.VolumeController(1, 2, 1.0, 0.8, 0.3),
            });
            return sys;
        }

        /// <summary>
        /// CAV 制御で室温制御を指定すると、指定した CAV が担当するゾーン（室 1・ゾーン 2）が
        /// 制御対象となる。旧実装は対象を設定せず、既定の室 0・ゾーン 0 を制御していた。
        /// </summary>
        [Fact]
        public void ControlZoneTemperature_CAV_TargetsGivenZone()
        {
            var sys = MakeCAVSystem();
            sys.ControlZoneTemperature(0, 1, 26.0);

            AHUSystem.AHUController ctr = sys.Controllers[0];
            Assert.False(ctr.IsRATemperatureControl);
            Assert.Equal(26.0, ctr.SetpointTemperature);
            Assert.Equal(1, ctr.TargetRoomIndex);
            Assert.Equal(2, ctr.TargetZoneIndex);
        }

        /// <summary>
        /// CAV を停止した後に室温制御を指定すると、その CAV は運転状態に戻る（VAV と同じ扱い）。
        /// 旧実装は CAV の停止フラグを解除せず、全 CAV 停止で空調機が停止したままになっていた。
        /// </summary>
        [Fact]
        public void ControlZoneTemperature_CAV_ClearsShutOff()
        {
            var sys = MakeCAVSystem();
            sys.ShutOff(0, 0);
            sys.ShutOff(0, 1);
            Assert.True(sys.GetVolumeController(0, 1).IsShutOff);

            sys.ControlZoneTemperature(0, 1, 26.0);
            Assert.False(sys.GetVolumeController(0, 1).IsShutOff);
            Assert.True(sys.GetVolumeController(0, 0).IsShutOff);
        }

        /// <summary>VAV 制御では従来どおり、指定した VAV の設定温度と運転状態が更新される。</summary>
        [Fact]
        public void ControlZoneTemperature_VAV_SetsVolumeControllerSetpoint()
        {
            var sys = new AHUSystem(null!, new AirHandlingUnit[1]);
            sys.SetVAV(0, new[]
            {
                new AHUSystem.VolumeController(0, 0, 1.0, 0.8, 0.3),
                new AHUSystem.VolumeController(0, 1, 1.0, 0.8, 0.3),
            });
            sys.ShutOff(0, 1);
            sys.ControlZoneTemperature(0, 1, 25.0);

            Assert.False(sys.GetVolumeController(0, 1).IsShutOff);
            Assert.Equal(25.0, sys.GetVolumeController(0, 1).SetpointTemperature);
            Assert.True(sys.Controllers[0].IsRATemperatureControl);
        }
    }
}
