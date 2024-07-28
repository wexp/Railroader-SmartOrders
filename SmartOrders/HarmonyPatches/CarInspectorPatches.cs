﻿namespace SmartOrders.HarmonyPatches;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using Game.Messages;
using HarmonyLib;
using JetBrains.Annotations;
using Model;
using Model.AI;
using SmartOrders.Extensions;
using UI.Builder;
using UI.CarInspector;
using UI.Common;
using UI.EngineControls;
using UnityEngine;

[PublicAPI]
[HarmonyPatch]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class CarInspectorPatches
{

    const float ADDITIONAL_WINDOW_HEIGHT_NEEDED = 95;
    private static Vector2? originalWindowSize;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CarInspector), "BuildContextualOrders")]
    public static void BuildContextualOrders(UIPanelBuilder builder, AutoEngineerPersistence persistence, Car ____car, Window ____window)
    {
        if (!SmartOrdersPlugin.Shared.IsEnabled)
        {
            return;
        }
        var locomotive = (BaseLocomotive)____car;
        var helper = new AutoEngineerOrdersHelper(locomotive, persistence);
        var mode2 = helper.Mode();

        if (mode2 == AutoEngineerMode.Yard)
        {
            if (originalWindowSize == null)
            {
                originalWindowSize = ____window.GetContentSize();
            }

            const float ADDITIONAL_WINDOW_HEIGHT_NEEDED = 100;
            ____window.SetContentSize(new Vector2(originalWindowSize.Value.x, originalWindowSize.Value.y + ADDITIONAL_WINDOW_HEIGHT_NEEDED));

            BuildwitchYardAIButtons(builder, locomotive, persistence, helper);
        }

        BuildHandbrakeAndAirHelperButons(builder, locomotive);

    }

    private static void BuildHandbrakeAndAirHelperButons(UIPanelBuilder builder, BaseLocomotive locomotive)
    {
        builder.AddField("",
          builder.ButtonStrip(strip =>
          {
              var cars = locomotive.EnumerateCoupled()!.ToList()!;

              if (cars.Any(c => c.air!.handbrakeApplied))
              {
                  strip.AddButton($"Release {TextSprites.HandbrakeWheel}", () => SmartOrdersUtility.ReleaseAllHandbrakes(cars))!
                      .Tooltip("Release handbrakes", $"Iterates over cars in this consist and releases {TextSprites.HandbrakeWheel}.");
              }

              if (cars.Any(c => c.EndAirSystemIssue()))
              {
                  strip.AddButton("Connect Air", () => SmartOrdersUtility.ConnectAir(cars))!
                      .Tooltip("Connect Consist Air", "Iterates over each car in this consist and connects gladhands and opens anglecocks.");
              }
          })!
       );
    }

    private static void BuildwitchYardAIButtons(UIPanelBuilder builder, BaseLocomotive locomotive, AutoEngineerPersistence persistence, AutoEngineerOrdersHelper helper)
    {
        Func<BaseLocomotive, string> getClearSwitchMode = (BaseLocomotive loco) =>
        {
            string defaultMode = "CLEAR_AHEAD";
            var clearSwitchMode = loco.KeyValueObject.Get("CLEAR_SWITCH_MODE");
            if (clearSwitchMode.IsNull)
            {
                return defaultMode;
            }

            return clearSwitchMode;
        };

        builder.AddField("Switches", builder.ButtonStrip(delegate (UIPanelBuilder builder)
        {
            builder.AddObserver(locomotive.KeyValueObject.Observe("CLEAR_SWITCH_MODE", delegate
            {
                builder.Rebuild();
            }, callInitial: false));

            builder.AddButtonSelectable("Approach\nAhead", getClearSwitchMode(locomotive) == "APPROACH_AHEAD", delegate
            {
                SmartOrdersUtility.DebugLog("updating switch mode to 'approach ahead'");
                locomotive.KeyValueObject.Set("CLEAR_SWITCH_MODE", "APPROACH_AHEAD");
            }).Height(60).Tooltip("Approach Ahead", "Approach but do not pass switches in front of the train. Choose the number of switches below");

            builder.AddObserver(locomotive.KeyValueObject.Observe("CLEAR_SWITCH_MODE", delegate
            {
                builder.Rebuild();
            }, callInitial: false));

            builder.AddButtonSelectable("Clear\nAhead", getClearSwitchMode(locomotive) == "CLEAR_AHEAD", delegate
            {
                SmartOrdersUtility.DebugLog("updating switch mode to 'clear ahead'");
                locomotive.KeyValueObject.Set("CLEAR_SWITCH_MODE", "CLEAR_AHEAD");
            }).Height(60).Tooltip("Clear Ahead", "Clear switches in front of the train. Choose the number of switches below");

            builder.AddButtonSelectable("Clear\nUnder", getClearSwitchMode(locomotive) == "CLEAR_UNDER", delegate
            {
                SmartOrdersUtility.DebugLog("updating switch mode to 'clear under'");
                locomotive.KeyValueObject.Set("CLEAR_SWITCH_MODE", "CLEAR_UNDER");
            }).Height(60).Tooltip("Clear Under", "Clear switches under the train. Choose the number of switches below");
        })).Height(60);


        builder.AddField("", builder.ButtonStrip(delegate (UIPanelBuilder builder)
        {
            builder.ButtonStrip(
            strip =>
            {
                    strip.AddButton("1", () => SmartOrdersUtility.Move(helper, 1, locomotive.KeyValueObject.Get("CLEAR_SWITCH_MODE"), locomotive, persistence))!
                        .Tooltip("1 switch", "Move 1 switch");

                    for (var i = 2; i <= 10; i++)
                    {
                    var numSwitches = i;
                        strip.AddButton($"{numSwitches}", () => SmartOrdersUtility.Move(helper, numSwitches, locomotive.KeyValueObject.Get("CLEAR_SWITCH_MODE"), locomotive, persistence))!
                            .Tooltip($"{numSwitches} switches", $"Move {numSwitches} switches");
                    }
                }, 4);
        }));
    }

}