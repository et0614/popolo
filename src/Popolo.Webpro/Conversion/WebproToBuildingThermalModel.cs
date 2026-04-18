/* WebproToBuildingThermalModel.cs
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
using System.Collections.Generic;

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;

using Popolo.Webpro.Domain;
using Popolo.Webpro.Domain.Enums;
using PopoloOrientation = Popolo.Core.Climate.Incline.Orientation;
using WebproOrientation = Popolo.Webpro.Domain.Enums.Orientation;

namespace Popolo.Webpro.Conversion
{
  /// <summary>
  /// Converts a <see cref="WebproModel"/> into a Popolo.Core
  /// <see cref="BuildingThermalModel"/> suitable for thermal load calculation.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Only rooms listed in <see cref="WebproModel.AirConditionedRoomNames"/>
  /// are converted; unconditioned rooms (corridors, restrooms, etc. that are
  /// not listed in the <c>AirConditioningZone</c> JSON section) are ignored.
  /// </para>
  /// <para>
  /// <b>Structural assumptions</b> (reproducing the legacy Popolo v2.3
  /// WEBPRO reader):
  /// </para>
  /// <list type="bullet">
  ///   <item><description>All air-conditioned rooms share a single <c>MultiRooms</c>.</description></item>
  ///   <item><description>Each zone receives a fixed 6-layer floor construction
  ///     (vinyl / air / concrete / air / gypsum board / rock wool ceiling tile)
  ///     assigned as a loop wall.</description></item>
  ///   <item><description>Interior walls use an adjacent-space factor of
  ///     <see cref="WebproConversionConstants.AdjacentSpaceFactor"/>.</description></item>
  ///   <item><description>Ground walls use
  ///     <see cref="WebproConversionConstants.GroundWallConductance"/>.</description></item>
  /// </list>
  /// <para>
  /// <b>Heat gain:</b> Internal heat gain schedules (people, lights, plug
  /// load, ventilation) are <i>not</i> installed by this converter. Callers
  /// that need occupant / lighting schedules should attach
  /// <see cref="WebproHeatGainScheduler"/> instances to individual zones
  /// after conversion, using the <see cref="RoomNameToZone"/> property of
  /// the returned <see cref="ConversionResult"/>.
  /// </para>
  /// </remarks>
  public static class WebproToBuildingThermalModel
  {

    #region 床・天井の固定 6 層構成

    /// <summary>
    /// Creates the fixed 6-layer floor/ceiling construction used by every
    /// converted zone as a loop wall.
    /// </summary>
    /// <remarks>
    /// Reproduces the legacy Popolo v2.3 defaults: vinyl flooring (3 mm),
    /// air gap (50 mm), concrete (150 mm), air gap (50 mm), gypsum board
    /// (9 mm), rock-wool ceiling tile (15 mm).
    /// </remarks>
    private static WallLayer[] CreateDefaultFloorLayers()
    {
      return new WallLayer[]
      {
        new WallLayer("ビニル系床材", 0.190, 2000, 0.003),
        new AirGapLayer("非密閉中空層", false, 0.05),
        new WallLayer("コンクリート", 1.6, 2000, 0.150),
        new AirGapLayer("非密閉中空層", false, 0.05),
        new WallLayer("石膏ボード", 0.220, 830, 0.009),
        new WallLayer("ロックウール化粧吸音板", 0.064, 290, 0.015),
      };
    }

    #endregion

    #region 変換結果

    /// <summary>
    /// Output of <see cref="Convert(WebproModel)"/>.
    /// </summary>
    /// <remarks>
    /// Holds the generated <see cref="BuildingThermalModel"/> along with
    /// bookkeeping dictionaries that callers commonly need after conversion
    /// (e.g. to attach heat-gain schedulers to specific zones).
    /// </remarks>
    public sealed class ConversionResult
    {
      /// <summary>Gets the generated thermal model.</summary>
      public BuildingThermalModel Model { get; }

      /// <summary>Gets the single <see cref="MultiRooms"/> instance wrapped by <see cref="Model"/>.</summary>
      public MultiRooms MultiRooms { get; }

      /// <summary>Gets a mapping from WEBPRO room name to the <see cref="Zone"/> representing that room.</summary>
      public IReadOnlyDictionary<string, Zone> RoomNameToZone { get; }

      /// <summary>
      /// Gets the names of rooms whose <c>(BuildingType, roomType)</c> pair
      /// could not be resolved to a <see cref="WebproHeatGainScheduler.RoomType"/>
      /// and therefore received no automatic heat-gain scheduler.
      /// </summary>
      /// <remarks>
      /// Empty when <c>installHeatGainSchedulers</c> was false, or when all
      /// rooms resolved successfully. Callers can use this list to attach
      /// custom heat gains to unmapped rooms.
      /// </remarks>
      public IReadOnlyList<string> UnmappedRoomNames { get; }

      internal ConversionResult(
        BuildingThermalModel model,
        MultiRooms multiRooms,
        Dictionary<string, Zone> roomNameToZone,
        List<string> unmappedRoomNames)
      {
        Model = model;
        MultiRooms = multiRooms;
        RoomNameToZone = roomNameToZone;
        UnmappedRoomNames = unmappedRoomNames;
      }
    }

    #endregion

    #region エントリポイント

    /// <summary>
    /// Converts the given WEBPRO model into a
    /// <see cref="BuildingThermalModel"/>.
    /// </summary>
    /// <param name="model">Source WEBPRO model.</param>
    /// <param name="materials">
    /// Optional material catalog. Defaults to
    /// <see cref="MaterialCatalog.Default"/> when null.
    /// </param>
    /// <param name="glazings">
    /// Optional glazing catalog. Defaults to
    /// <see cref="GlazingCatalog.Default"/> when null.
    /// </param>
    /// <param name="roomTypeMapper">
    /// Optional room-type mapper used to resolve
    /// <c>(BuildingType, roomType)</c> pairs to
    /// <see cref="WebproHeatGainScheduler.RoomType"/>.
    /// Defaults to <see cref="RoomTypeMapper.Default"/> when null.
    /// </param>
    /// <param name="installHeatGainSchedulers">
    /// When <c>true</c> (default), a <see cref="WebproHeatGainScheduler"/>
    /// is installed on each converted zone based on the room's building and
    /// room types. When <c>false</c>, zones are created without schedulers
    /// and callers are expected to attach heat gains themselves via the
    /// <see cref="ConversionResult.RoomNameToZone"/> dictionary. Rooms whose
    /// <c>(BuildingType, roomType)</c> pair is not in the mapper are
    /// skipped silently (no exception) even when this flag is <c>true</c>;
    /// see <see cref="ConversionResult.UnmappedRoomNames"/> for the list of
    /// such rooms.
    /// </param>
    /// <returns>A <see cref="ConversionResult"/> bundling the model and lookup data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public static ConversionResult Convert(
      WebproModel model,
      MaterialCatalog? materials = null,
      GlazingCatalog? glazings = null,
      RoomTypeMapper? roomTypeMapper = null,
      bool installHeatGainSchedulers = true)
    {
      if (model is null) throw new ArgumentNullException(nameof(model));
      var mats = materials ?? MaterialCatalog.Default;
      var glz = glazings ?? GlazingCatalog.Default;
      var mapper = roomTypeMapper ?? RoomTypeMapper.Default;

      var zones = new List<Zone>();
      var walls = new List<Wall>();
      var windows = new List<Window>();
      var roomNameToZone = new Dictionary<string, Zone>();
      var unmappedRoomNames = new List<string>();
      var loopWalls = new Dictionary<Zone, Wall>();
      var envelopeWallsByZone = new Dictionary<Zone, List<(Wall wall, WebproWall webproWall, Incline incline)>>();
      var windowsByZone = new Dictionary<Zone, List<Window>>();

      foreach (string roomName in model.AirConditionedRoomNames)
      {
        if (!model.Rooms.TryGetValue(roomName, out var room)) continue;

        // Zone 生成
        var zone = BuildZone(roomName, room);
        zones.Add(zone);
        roomNameToZone[roomName] = zone;

        // 発熱スケジューラ(オプション)
        if (installHeatGainSchedulers)
        {
          if (mapper.TryGet(room.BuildingType, room.RoomType, out var schedulerType))
          {
            zone.AddHeatGain(new WebproHeatGainScheduler(schedulerType));
          }
          else
          {
            unmappedRoomNames.Add(roomName);
          }
        }

        // 床・天井 (loop wall)
        var loopWall = new Wall(room.RoomArea, CreateDefaultFloorLayers());
        walls.Add(loopWall);
        loopWalls[zone] = loopWall;

        // 外皮 (オプション)
        if (model.Envelopes.TryGetValue(roomName, out var envelope))
        {
          var wallList = new List<(Wall, WebproWall, Incline)>();
          var windowList = new List<Window>();

          foreach (var webproWall in envelope.Walls)
          {
            var incline = OrientationToIncline(webproWall.SurfaceOrientation);
            var wall = BuildWall(webproWall, model.WallConfigurations, mats);
            walls.Add(wall);
            wallList.Add((wall, webproWall, incline));

            foreach (var win in BuildWindowsForWall(webproWall, model.WindowConfigurations, glz, incline))
            {
              windows.Add(win);
              windowList.Add(win);
            }
          }

          envelopeWallsByZone[zone] = wallList;
          windowsByZone[zone] = windowList;
        }
      }

      // MultiRooms 生成
      var multiRooms = new MultiRooms(
        rmCount: 1,
        zones: zones.ToArray(),
        walls: walls.ToArray(),
        windows: windows.ToArray());
      for (int i = 0; i < zones.Count; i++)
        multiRooms.AddZone(0, zones[i]);

      // 床・天井を loop wall として追加
      foreach (var (zone, loopWall) in loopWalls)
        multiRooms.AddLoopWall(zone, loopWall);

      // 外皮の壁を追加し、境界条件を設定
      foreach (var (zone, wallList) in envelopeWallsByZone)
      {
        foreach (var (wall, webproWall, incline) in wallList)
        {
          // B 側をゾーン内表面に、F 側を屋外側に
          multiRooms.AddWall(zone, wall, isSideF: false);
          ApplyBoundaryCondition(multiRooms, wall, webproWall, incline);
        }
      }

      // 窓を追加
      foreach (var (zone, windowList) in windowsByZone)
        foreach (var window in windowList)
          multiRooms.AddWindow(zone, window);

      // BuildingThermalModel 生成
      var thermalModel = new BuildingThermalModel(new MultiRooms[] { multiRooms })
      {
        TimeStep = 3600,
      };
      thermalModel.InitializeAirState(24, 0.018);

      return new ConversionResult(thermalModel, multiRooms, roomNameToZone, unmappedRoomNames);
    }

    #endregion

    #region 個別生成処理

    /// <summary>Builds a Popolo <see cref="Zone"/> for a WEBPRO room.</summary>
    private static Zone BuildZone(string roomName, WebproRoom room)
    {
      double airMass = WebproConversionConstants.AirDensity * room.RoomArea * room.CeilingHeight;
      var zone = new Zone(roomName, airMass, room.RoomArea)
      {
        HeatCapacity = room.RoomArea * WebproConversionConstants.ZoneHeatCapacityRate,
      };
      return zone;
    }

    /// <summary>Builds a Popolo <see cref="Wall"/> from a WEBPRO wall DTO.</summary>
    private static Wall BuildWall(
      WebproWall webproWall,
      IReadOnlyDictionary<string, WebproWallConfigure> wallConfigurations,
      MaterialCatalog catalog)
    {
      // 壁の総面積 (窓含む)
      double totalArea = webproWall.Area ?? ((webproWall.Width ?? 1.0) * (webproWall.Height ?? 1.0));

      // 窓面積の合計を差し引く (WEBPRO 慣習)
      double windowArea = 0;
      foreach (var window in webproWall.Windows)
      {
        if (window.ID != WebproConversionConstants.NoWindowSentinel)
          windowArea += window.Number ?? 0;
      }

      // ゼロや負の壁面積を回避
      double netWallArea = Math.Max(0.1, totalArea - windowArea);

      // レイヤ構成
      if (!wallConfigurations.TryGetValue(webproWall.WallSpec, out var wallConf))
      {
        throw new InvalidOperationException(
          $"Wall spec '{webproWall.WallSpec}' is not defined in WallConfigurations.");
      }
      var layers = BuildWallLayers(wallConf, catalog);
      var wall = new Wall(netWallArea, layers);

      // 日射吸収率
      double absorptance = wallConf.SolarAbsorptionRatio
        ?? WebproConversionConstants.DefaultSolarAbsorptionRatio;
      wall.ShortWaveAbsorptanceF = absorptance;

      return wall;
    }

    /// <summary>Builds the ordered layer array for a WEBPRO wall configuration.</summary>
    private static WallLayer[] BuildWallLayers(WebproWallConfigure wallConf, MaterialCatalog catalog)
    {
      var layers = new WallLayer[wallConf.Layers.Count];
      for (int i = 0; i < wallConf.Layers.Count; i++)
      {
        var layer = wallConf.Layers[i];
        layers[i] = catalog.MakeWallLayer(layer.MaterialID, layer.Thickness);
      }
      return layers;
    }

    /// <summary>Builds the <see cref="Window"/> instances for a single WEBPRO wall.</summary>
    private static IEnumerable<Window> BuildWindowsForWall(
      WebproWall webproWall,
      IReadOnlyDictionary<string, WebproWindowConfigure> windowConfigurations,
      GlazingCatalog catalog,
      Incline incline)
    {
      foreach (var webproWindow in webproWall.Windows)
      {
        // sentinel "無" は窓なしを意味する
        if (webproWindow.ID == WebproConversionConstants.NoWindowSentinel) continue;

        if (!windowConfigurations.TryGetValue(webproWindow.ID, out var windowConf))
        {
          throw new InvalidOperationException(
            $"Window ID '{webproWindow.ID}' is not defined in WindowConfigurations.");
        }

        double area = webproWindow.Number ?? windowConf.Area;
        if (area <= 0) continue;

        yield return BuildWindow(windowConf, area, incline, webproWindow.HasBlind, catalog);
      }
    }

    /// <summary>
    /// Builds a single Popolo <see cref="Window"/> given a WEBPRO
    /// configuration entry and the placement's area, incline, and blind flag.
    /// </summary>
    /// <remarks>
    /// Reproduces the legacy Popolo v2.3 <c>WebproWindowJson.MakeWindow</c>
    /// logic: resolves (τ, U-value) from the configuration method, applies
    /// the two-pane absorptance-zero correction, subtracts surface
    /// resistance, and optionally installs a bright venetian blind.
    /// </remarks>
    private static Window BuildWindow(
      WebproWindowConfigure windowConf,
      double area,
      Incline incline,
      bool hasBlind,
      GlazingCatalog catalog)
    {
      // 両表面の総合熱伝達率の逆数 (室内 1/10 + 屋外 1/20 = 0.15)
      const double R_IO = 1.0 / 10.0 + 1.0 / 20.0;

      (double tau, double htCoef) = ResolveGlazingPerformance(windowConf, catalog);

      // 吸収率=0 の二重ガラス仮定で単層透過率を 2 層等価値に補正
      tau = 2.0 * tau / (1.0 + tau);
      double rho = 1.0 - tau;

      var window = new Window(
        area,
        new double[] { tau, tau },
        new double[] { rho, rho },
        incline);

      // 室内外の表面熱抵抗を差し引いた中空層の熱抵抗
      double adjustedHtCoef = htCoef / (1.0 - R_IO * htCoef);
      window.SetAirGapResistance(0, adjustedHtCoef);

      // ブラインドは BrightVenetianBlind 固定 (旧版踏襲)
      if (hasBlind)
      {
        window.SetShadingDevice(
          2,
          new SimpleShadingDevice(SimpleShadingDevice.PredefinedDevices.BrightVenetianBlind));
      }

      return window;
    }

    /// <summary>
    /// Resolves (τ, U-value) for a window configuration based on its
    /// <see cref="WebproWindowConfigure.Method"/>.
    /// </summary>
    private static (double tau, double htCoef) ResolveGlazingPerformance(
      WebproWindowConfigure windowConf, GlazingCatalog catalog)
    {
      switch (windowConf.Method)
      {
        case WindowInputMethod.WindowSpec:
          return (windowConf.WindowSolarHeatGainRate, windowConf.WindowHeatTransferCoefficient);

        case WindowInputMethod.FrameTypeAndGlazingSpec:
          return (windowConf.GlazingSolarHeatGainRate, windowConf.GlazingHeatTransferCoefficient);

        case WindowInputMethod.FrameAndGlazingType:
          {
            var perf = catalog.Get(windowConf.GlazingID);
            return (perf.SolarHeatGain, perf.HeatTransferCoefficient);
          }

        case WindowInputMethod.None:
        default:
          throw new InvalidOperationException(
            $"Cannot resolve glazing performance for method '{windowConf.Method}'.");
      }
    }

    /// <summary>
    /// Applies the appropriate boundary condition to the F-side of the given
    /// wall based on the WEBPRO <see cref="WebproWall.Type"/>.
    /// </summary>
    private static void ApplyBoundaryCondition(
      MultiRooms multiRooms, Wall wall, WebproWall webproWall, Incline incline)
    {
      switch (webproWall.Type)
      {
        case WallType.ExternalWall:
        case WallType.ShadingExternalWall:
          multiRooms.SetOutsideWall(wall, isSideF: true, incline);
          break;
        case WallType.GroundWall:
          multiRooms.SetGroundWall(
            wall, isSideF: true, WebproConversionConstants.GroundWallConductance);
          break;
        case WallType.InnerWall:
          multiRooms.UseAdjacentSpaceFactor(
            wall, isSideF: true, WebproConversionConstants.AdjacentSpaceFactor);
          break;
        default:
          throw new InvalidOperationException($"Unhandled wall type '{webproWall.Type}'.");
      }
    }

    #endregion

    #region 方位変換

    /// <summary>
    /// Converts a WEBPRO <see cref="WebproOrientation"/> to a Popolo.Core
    /// <see cref="Incline"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vertical (wall) orientations map to <c>verticalAngle = π/2</c>.
    /// Upper horizontal (roof) maps to <c>verticalAngle = 0</c>.
    /// Lower horizontal (floor) maps to <c>verticalAngle = π</c>, distinct
    /// from the roof case.
    /// </para>
    /// <para>
    /// Legacy <see cref="WebproOrientation.Shade"/> is treated as lower
    /// horizontal (shaded by definition);
    /// <see cref="WebproOrientation.Horizontal"/> as upper horizontal.
    /// </para>
    /// </remarks>
    public static Incline OrientationToIncline(WebproOrientation orientation)
    {
      switch (orientation)
      {
        case WebproOrientation.N: return new Incline(PopoloOrientation.N, Math.PI / 2);
        case WebproOrientation.NE: return new Incline(PopoloOrientation.NE, Math.PI / 2);
        case WebproOrientation.E: return new Incline(PopoloOrientation.E, Math.PI / 2);
        case WebproOrientation.SE: return new Incline(PopoloOrientation.SE, Math.PI / 2);
        case WebproOrientation.S: return new Incline(PopoloOrientation.S, Math.PI / 2);
        case WebproOrientation.SW: return new Incline(PopoloOrientation.SW, Math.PI / 2);
        case WebproOrientation.W: return new Incline(PopoloOrientation.W, Math.PI / 2);
        case WebproOrientation.NW: return new Incline(PopoloOrientation.NW, Math.PI / 2);
        case WebproOrientation.UpperHorizontal: return new Incline(PopoloOrientation.N, 0);
        case WebproOrientation.LowerHorizontal: return new Incline(PopoloOrientation.N, Math.PI);
        case WebproOrientation.Shade: return new Incline(PopoloOrientation.N, Math.PI);
        case WebproOrientation.Horizontal: return new Incline(PopoloOrientation.N, 0);
        default:
          throw new ArgumentOutOfRangeException(
            nameof(orientation), orientation, "Unhandled WEBPRO orientation.");
      }
    }

    #endregion
  }
}