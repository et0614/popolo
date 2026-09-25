/* JsonRoundTripDemo.cs
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

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.IO.Json;

namespace Popolo.Samples.Demos.IO
{
  /// <summary>
  /// Builds a minimal <see cref="BuildingThermalModel"/>, serializes it to
  /// JSON via <see cref="PopoloJsonSerializer"/>, deserializes it back, and
  /// prints a short comparison showing the model survived the round trip.
  /// </summary>
  /// <remarks>
  /// The test model is a single zone (120 m³ of air, 10 m² floor area) wrapped
  /// in one south-facing concrete wall. It is initialized with a Tokyo solar
  /// position and a spring outdoor condition so that serialized values
  /// (<see cref="BuildingThermalModel.CurrentDateTime"/>,
  /// <see cref="BuildingThermalModel.Sun"/>) are non-trivial.
  /// </remarks>
  public sealed class JsonRoundTripDemo : IDemo
  {
    public string Name => "io-json-roundtrip";
    public string Category => "IO";
    public string Description => "Build a tiny model, JSON-serialize, deserialize, compare.";

    public int Run(string[] args)
    {
      var original = BuildSampleModel();

      Console.WriteLine("Serializing model to JSON...");
      string json = PopoloJsonSerializer.Serialize(original);
      Console.WriteLine($"  JSON length: {json.Length} characters");

      Console.WriteLine("Deserializing...");
      var restored = PopoloJsonSerializer.Deserialize(json);

      Console.WriteLine();
      Console.WriteLine("Round-trip comparison");
      Console.WriteLine("  Property              Original               Restored");
      Console.WriteLine("  --------------------  ---------------------  ---------------------");
      PrintRow("TimeStep [s]", original.TimeStep, restored.TimeStep);
      PrintRow("CurrentDateTime", original.CurrentDateTime, restored.CurrentDateTime);
      PrintRow("MultiRoom.Length", original.MultiRoom.Length, restored.MultiRoom.Length);
      PrintRow("Sun.Latitude", original.Sun.Latitude, restored.Sun.Latitude);
      PrintRow("Sun.Longitude", original.Sun.Longitude, restored.Sun.Longitude);

      var origRoom = (MultiRoom)original.MultiRoom[0];
      var restRoom = (MultiRoom)restored.MultiRoom[0];
      PrintRow("RoomCount", origRoom.RoomCount, restRoom.RoomCount);
      PrintRow("ZoneCount", origRoom.ZoneCount, restRoom.ZoneCount);
      PrintRow("Walls.Length", origRoom.Walls.Length, restRoom.Walls.Length);
      PrintRow("OutsideWallRefs",
        origRoom.GetOutsideWallReferences().Length,
        restRoom.GetOutsideWallReferences().Length);
      return 0;
    }

    private static BuildingThermalModel BuildSampleModel()
    {
      var zone = new Zone("Office", airMass: 120, floorArea: 10);
      var layers = new WallLayer[]
      {
        new WallLayer("Concrete", thermalConductivity: 1.4,
                      volSpecificHeat: 1934, thickness: 0.15),
      };
      var wall = new Wall(12.0, layers) { ID = 0 };

      var room = new MultiRoom(1, new[] { zone }, new[] { wall }, Array.Empty<Window>());
      room.AddZone(0, 0);
      room.AddWall(0, 0, true);
      room.SetOutsideWall(0, true, new Incline(horizontalAngle: 0, verticalAngle: Math.PI / 2));

      var model = new BuildingThermalModel(new[] { room });
      model.TimeStep = 3600;
      model.UpdateOutdoorCondition(
        dTime: new DateTime(2026, 4, 20, 12, 0, 0),
        sun:   new Sun(latitude: 35.68, longitude: 139.77, standardLongitude: 135.0),
        temperature: 18.0, humidityRatio: 0.009, nocRadiation: 0.0);
      return model;
    }

    private static void PrintRow(string label, object original, object restored)
    {
      string o = FormatValue(original);
      string r = FormatValue(restored);
      string flag = o == r ? "" : "   *** differs ***";
      Console.WriteLine($"  {label,-20}  {o,-21}  {r,-21}{flag}");
    }

    private static string FormatValue(object v) => v switch
    {
      double d => d.ToString("F4"),
      DateTime t => t.ToString("yyyy-MM-dd HH:mm:ss"),
      _ => v.ToString() ?? "",
    };
  }
}
