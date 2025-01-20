using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using WorkshopOptimizerPlugin.Data;
using WorkshopOptimizerPlugin.Optimizer;
using WorkshopOptimizerPlugin.Windows.Utils;
using WorkshopOptimizerPlugin.Persistence;

namespace WorkshopOptimizerPlugin.Windows.Tabs;

internal class ProducedTab : ITab
{
    private readonly UIDataSource uiDataSource;
    private readonly CommonInterfaceElements ifData;
    private readonly Dictionary<int, System.Numerics.Vector4> workshopColors = new();
    private int colorIndex = 0;

    private System.Numerics.Vector4 GetNextColor()
    {
        var colors = new System.Numerics.Vector4[]
        {
            new(0.9f, 0.7f, 0.7f, 0.3f),
            new(0.7f, 0.9f, 0.7f, 0.3f),
            new(0.7f, 0.7f, 0.9f, 0.3f),
            new(0.9f, 0.9f, 0.7f, 0.3f),
            new(0.9f, 0.7f, 0.9f, 0.3f),
            new(0.7f, 0.9f, 0.9f, 0.3f)
        };
        
        return colors[colorIndex++ % colors.Length];
    }

    private bool IsWorkshopEmpty(int[] workshop)
    {
        return workshop.All(step => step == -1);
    }

    private bool AreWorkshopsIdentical(int[] workshop1, int[] workshop2, int cycle)
    {
        if (IsWorkshopEmpty(workshop1) && IsWorkshopEmpty(workshop2))
        {
            return false;
        }
        
        for (int step = 0; step < Constants.MaxSteps; step++)
        {
            if (workshop1[step] == -1 && workshop2[step] == -1) continue;
            if (workshop1[step] != workshop2[step]) return false;
        }
        return true;
    }

    private void UpdateWorkshopColors(ProducedItemsAdaptor producedItems, int cycle)
    {
        workshopColors.Clear();
        colorIndex = 0;

        for (int w1 = 0; w1 < Constants.MaxWorkshops; w1++)
        {
            if (workshopColors.ContainsKey(w1)) continue;

            var workshop1 = new int[Constants.MaxSteps];
            for (int step = 0; step < Constants.MaxSteps; step++)
            {
                workshop1[step] = producedItems[cycle, w1, step];
            }

            if (IsWorkshopEmpty(workshop1)) continue;

            for (int w2 = w1 + 1; w2 < Constants.MaxWorkshops; w2++)
            {
                if (workshopColors.ContainsKey(w2)) continue;

                var workshop2 = new int[Constants.MaxSteps];
                for (int step = 0; step < Constants.MaxSteps; step++)
                {
                    workshop2[step] = producedItems[cycle, w2, step];
                }

                if (IsWorkshopEmpty(workshop2)) continue;

                if (AreWorkshopsIdentical(workshop1, workshop2, cycle))
                {
                    if (!workshopColors.ContainsKey(w1))
                    {
                        var color = GetNextColor();
                        workshopColors[w1] = color;
                        workshopColors[w2] = color;
                    }
                    else
                    {
                        workshopColors[w2] = workshopColors[w1];
                    }
                }
            }
        }
    }

    public ProducedTab(UIDataSource uiDataSource, CommonInterfaceElements ifData)
    {
        this.uiDataSource = uiDataSource;
        this.ifData = ifData;
    }

    public void OnOpen() { }

    public void Draw()
    {
        ifData.DrawBasicControls();
        var cycle = ifData.Cycle;
        var startGroove = ifData.GetStartGroove();
        ImGui.SameLine();
        ifData.DrawRestCycleCheckbox(cycle);
        
        var producedItems = ifData.IsCurrentSeason() ? uiDataSource.DataSource.CurrentProducedItems : uiDataSource.DataSource.PreviousProducedItems;
        bool hasAnyItems = false;
        for (var w = 0; w < Constants.MaxWorkshops; w++)
        {
            for (var s = 0; s < Constants.MaxSteps; s++)
            {
                if (producedItems[cycle, w, s] >= 0)
                {
                    hasAnyItems = true;
                    break;
                }
            }
            if (hasAnyItems) break;
        }

        if (hasAnyItems)
        {
            ImGui.SameLine();
            if (ImGui.Button($"Copy Cycle {cycle + 1} for Visland Import"))
            {
                var text = $"Cycle {cycle + 1}\n{GetAllWorkshopsVislandText(cycle)}";
                if (!string.IsNullOrEmpty(text))
                {
                    ImGui.SetClipboardText(text);
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Copies the schedule of all workshops for Cycle {cycle + 1} into the 'V(ery) Islands' plugin.\n" +
                               "1. Install the 'V(ery) Islands' plugin (Github: ffxiv_visland)\n" +
                               "2. With the Isleworks Agenda window open, a plugin window\n" +
                               "   named 'Workshop Optimizer' should appear\n" +
                               "3. Click the 'Import Recommendations From Clipboard' button\n" +
                               "4. Click 'Set Schedule: This Week' for the current week\n" +
                               "   or 'Set Schedule: Next Week' for the next week\n");
            }

            ImGui.SameLine();
            if (ImGui.Button("Copy All Cycles for Visland Import"))
            {
                var text = GetAllCyclesVislandText();
                if (!string.IsNullOrEmpty(text))
                {
                    ImGui.SetClipboardText(text);
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Copies the schedule of all workshops for all cycles that have items into the 'V(ery) Islands' plugin.\n" +
                               "1. Install the 'V(ery) Islands' plugin (Github: ffxiv_visland)\n" +
                               "2. With the Isleworks Agenda window open, a plugin window\n" +
                               "   named 'Workshop Optimizer' should appear\n" +
                               "3. Click the 'Import Recommendations From Clipboard' button\n" +
                               "4. Click 'Set Schedule: This Week' for the current week\n" +
                               "   or 'Set Schedule: Next Week' for the next week\n" +
                               "Note: You can import up to 5 cycles at once");
            }
        }

        ImGui.Spacing();
        DrawProducedTable(cycle, startGroove);
        ImGui.Spacing();
        DrawMaterialsTable(cycle);
    }

    private string GetWorkshopStepByStepText(List<Item> items)
    {
        if (items.Count == 0) return string.Empty;
        return string.Join(",", items.Select(item => ItemStaticData.Get(item.Id).Name));
    }

    private string GetWorkshopVislandText(List<Item> items)
    {
        if (items.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        int currentHour = 0;

        foreach (var item in items)
        {
            if (sb.Length > 0) 
            {
                sb.AppendLine();
            }
            
            var name = ItemStaticData.Get(item.Id).Name;
            if (name.StartsWith("Isleworks "))
                name = name.Substring(10);
            sb.Append($"[{currentHour}] {name}");

            currentHour += item.Hours;
        }
        return sb.ToString();
    }

    private string GetAllWorkshopsVislandText(int cycle)
    {
        var producedItems = ifData.IsCurrentSeason() ? uiDataSource.DataSource.CurrentProducedItems : uiDataSource.DataSource.PreviousProducedItems;
        var itemCache = ifData.IsCurrentSeason() ? uiDataSource.CurrentItemCache : uiDataSource.PreviousItemCache;
        var sb = new System.Text.StringBuilder();

        for (var w = 0; w < Constants.MaxWorkshops; w++)
        {
            var items = new List<Item>();
            for (var s = 0; s < Constants.MaxSteps; s++)
            {
                var id = producedItems[cycle, w, s];
                if (id >= 0)
                {
                    items.Add(itemCache[ItemStaticData.Get(id)]);
                }
            }
            if (items.Count > 0)
            {
                if (sb.Length > 0) 
                {
                    sb.AppendLine();
                    sb.AppendLine($"Workshop {w + 1}:");
                }
                else
                {
                    sb.AppendLine($"Workshop {w + 1}:");
                }
                sb.Append(GetWorkshopVislandText(items));
            }
        }
        return sb.ToString();
    }

    private string GetAllCyclesVislandText()
    {
        var producedItems = ifData.IsCurrentSeason() ? uiDataSource.DataSource.CurrentProducedItems : uiDataSource.DataSource.PreviousProducedItems;
        var sb = new System.Text.StringBuilder();

        for (var cycle = 0; cycle < Constants.MaxCycles; cycle++)
        {
            bool hasItems = false;
            for (var w = 0; w < Constants.MaxWorkshops && !hasItems; w++)
            {
                for (var s = 0; s < Constants.MaxSteps && !hasItems; s++)
                {
                    if (producedItems[cycle, w, s] >= 0)
                    {
                        hasItems = true;
                    }
                }
            }

            if (hasItems)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.AppendLine($"Cycle {cycle + 1}");
                sb.Append(GetAllWorkshopsVislandText(cycle));
            }
        }
        return sb.ToString();
    }

    private void DrawProducedTable(int cycle, Groove startGroove)
    {
        var tableFlags = ImGuiTableFlags.BordersV | 
                        ImGuiTableFlags.BordersOuterH | 
                        ImGuiTableFlags.RowBg | 
                        ImGuiTableFlags.Resizable |
                        ImGuiTableFlags.ScrollY |
                        ImGuiTableFlags.SizingFixedFit;

        var producedItems = ifData.IsCurrentSeason() ? uiDataSource.DataSource.CurrentProducedItems : uiDataSource.DataSource.PreviousProducedItems;
        bool hasAnyItems = false;
        for (var w = 0; w < Constants.MaxWorkshops; w++)
        {
            for (var s = 0; s < Constants.MaxSteps; s++)
            {
                if (producedItems[cycle, w, s] >= 0)
                {
                    hasAnyItems = true;
                    break;
                }
            }
            if (hasAnyItems) break;
        }

        if (hasAnyItems)
        {
            ImGui.Spacing();
        }

        if (!ImGui.BeginTable("Produced", 1 + Constants.MaxWorkshops, tableFlags)) { return; }

        var availableHeight = ImGui.GetContentRegionAvail().Y;
        ImGui.TableSetupScrollFreeze(0, 1);
        
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, 70);
        
        float availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - 60;
        float workshopWidth = availableWidth / Constants.MaxWorkshops;
        
        var itemCache = ifData.IsCurrentSeason() ? uiDataSource.CurrentItemCache : uiDataSource.PreviousItemCache;
        var items = new List<Item>[Constants.MaxWorkshops];
        
        for (var w = 0; w < Constants.MaxWorkshops; w++)
        {
            items[w] = new List<Item>();
            for (var step = 0; step < Constants.MaxSteps; step++)
            {
                var id = producedItems[cycle, w, step];
                if (id >= 0)
                {
                    var item = itemCache[ItemStaticData.Get(id)];
                    items[w].Add(item);
                }
            }
        }

        for (var i = 0; i < Constants.MaxWorkshops; i++)
        {
            ImGui.TableSetupColumn($"Workshop {i+1}", 
                ImGuiTableColumnFlags.WidthFixed, 
                workshopWidth);
        }

        ImGui.TableHeadersRow();
        for (var i = 0; i < Constants.MaxWorkshops; i++)
        {
            if (items[i].Count > 0)
            {
                ImGui.TableSetColumnIndex(i + 1);
                var headerPos = ImGui.GetCursorScreenPos();
                var headerWidth = ImGui.GetColumnWidth();
                
                ImGui.SetCursorScreenPos(headerPos);
                ImGui.Text($"Workshop {i+1}");
                
                ImGui.SameLine();
                ImGui.SetCursorScreenPos(new System.Numerics.Vector2(headerPos.X + headerWidth - 50, headerPos.Y - 3));
                if (ImGui.SmallButton($"Copy###{i}"))
                {
                    var stepByStepText = GetWorkshopStepByStepText(items[i]);
                    ImGui.SetClipboardText(stepByStepText);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Install and enable IslandWorkshopSearch (or my fork) plugin to use step-by-step search");
                }
            }
        }

        UpdateWorkshopColors(producedItems, cycle);

        var disabled = ifData.IsPreviousSeason() || ifData.RestCycles[cycle];
        var hours = new int[Constants.MaxWorkshops];

        for (var step = 0; step < Constants.MaxSteps; step++)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None, 27);
            ImGui.TableSetColumnIndex(0);
            ImGui.Text($"Step {step + 1}");
            for (var w = 0; w < Constants.MaxWorkshops; w++)
            {
                if ((step > 0) && (producedItems[cycle, w, step - 1] < 0)) { continue; }

                if (hours[w] >= Constants.MaxHours) { continue; }

                ImGui.TableSetColumnIndex(w + 1);

                if (workshopColors.TryGetValue(w, out var color))
                {
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.ColorConvertFloat4ToU32(color));
                }

                var thisId = producedItems[cycle, w, step];
                var thisItem = (thisId >= 0) ? itemCache[ItemStaticData.Get(thisId)] : null;

                ImGui.SetNextItemWidth(ImGui.GetColumnWidth() * 0.6f);

                if (disabled) { ImGui.BeginDisabled(); }
                if (ImGui.BeginCombo($"###{w} {step}", thisItem?.Name ?? ""))
                {
                    if (ImGui.Selectable(""))
                    {
                        producedItems[cycle, w, step] = -1;
                        for (var s = step + 1; s < Constants.MaxSteps; s++)
                        {
                            producedItems[cycle, w, s] = -1;
                        }
                        uiDataSource.DataChanged(cycle);
                    }

                    if (step == 0)
                    {
                        foreach (var item in ItemStaticData.GetAllItems().Where(i => i.IsValid()).OrderBy(i => i.Name))
                        {
                            if (ImGui.Selectable(item.Name))
                            {
                                producedItems[cycle, w, step] = (int)item.Id;
                                for (var s = step + 1; s < Constants.MaxSteps; s++)
                                {
                                    producedItems[cycle, w, s] = -1;
                                }
                                uiDataSource.DataChanged(cycle);
                            }
                        }
                    }
                    else
                    {
                        foreach (var item in CategoryMap.GetEfficientItems(ItemStaticData.Get((uint)producedItems[cycle, w, step - 1]).Categories).OrderBy(x => x.Name))
                        {
                            if ((hours[w] + item.Hours) > Constants.MaxHours)
                            {
                                continue;
                            }
                            if (ImGui.Selectable(item.Name))
                            {
                                producedItems[cycle, w, step] = (int)item.Id;
                                for (var s = step + 1; s < Constants.MaxSteps; s++)
                                {
                                    producedItems[cycle, w, s] = -1;
                                }
                                uiDataSource.DataChanged(cycle);
                            }
                        }
                    }
                    ImGui.EndCombo();
                }
                if (disabled) { ImGui.EndDisabled(); }

                if (thisItem != null)
                {
                    hours[w] += thisItem.Hours;

                    ImGui.SameLine();
                    ImGui.Text($"{thisItem.Hours}hs ");
                    
                    var (pattern, some) = thisItem.FindPattern(cycle);
                    ImGui.SameLine();
                    ImGui.Text(some ? pattern?.Name ?? " * " : " ? ");

                    var buttonWidth = 40;
                    ImGui.SameLine(ImGui.GetColumnWidth() - buttonWidth - 5);
                    if (ImGui.SmallButton($"Copy###{w}{step}"))
                    {
                        var fullName = ItemStaticData.Get(thisItem.Id).Name;
                        ImGui.SetClipboardText(fullName);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Install and enable IslandWorkshopSearch (or my fork) plugin to use quick search");
                    }
                }
            }
        }


        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, 27);
        ImGui.TableSetColumnIndex(0);
        ImGui.SetNextItemWidth(200);
        ImGui.Text("Hours");
        for (var w = 0; w < Constants.MaxWorkshops; w++)
        {
            ImGui.TableSetColumnIndex(w + 1);
            ImGui.SetNextItemWidth(200);
            ImGui.Text($"{hours[w]}hs");
        }

        var itemSets = new ItemSet[Constants.MaxWorkshops];
        for (var w = 0; w < Constants.MaxWorkshops; w++)
        {
            itemSets[w] = new ItemSet(items[w].ToArray());
        }

        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, 27);
        ImGui.TableSetColumnIndex(0);
        ImGui.SetNextItemWidth(200);
        ImGui.Text("Base Value");
        for (var w = 0; w < Constants.MaxWorkshops; w++)
        {
            ImGui.TableSetColumnIndex(w + 1);
            ImGui.SetNextItemWidth(200);
            ImGui.Text($"{itemSets[w].Value}");
        }

        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, 27);
        ImGui.TableSetColumnIndex(0);
        ImGui.SetNextItemWidth(200);
        ImGui.Text("Value");
        for (var w = 0; w < Constants.MaxWorkshops; w++)
        {
            ImGui.TableSetColumnIndex(w + 1);
            ImGui.SetNextItemWidth(200);
            ImGui.Text($"{itemSets[w].EffectiveValue(cycle) * startGroove.Multiplier():F2}");
        }

        var workshopItemSets = new WorkshopsItemSets(itemSets, cycle, startGroove);
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, 27);
        ImGui.TableSetColumnIndex(0);
        ImGui.SetNextItemWidth(200);
        ImGui.Text("Total Value");
        ImGui.TableNextColumn();
        ImGui.Text($"{workshopItemSets.EffectiveValue:F2}");

        ImGui.EndTable();
    }

    private void DrawMaterialsTable(int cycle)
    {
        if (!ImGui.BeginTable("Materials", 6)) { return; }

        ImGui.TableSetupColumn("Gatherable Material", ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("Rare Material", ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableHeadersRow();

        var itemCache = ifData.IsCurrentSeason() ? uiDataSource.CurrentItemCache : uiDataSource.PreviousItemCache;
        var producedItems = ifData.IsCurrentSeason() ? uiDataSource.DataSource.CurrentProducedItems : uiDataSource.DataSource.PreviousProducedItems;
        var gatherableMaterials = new Dictionary<Material, int>();
        var rareMaterials = new Dictionary<Material, int>();
        for (var step = 0; step < Constants.MaxSteps; step++)
        {
            for (var w = 0; w < Constants.MaxWorkshops; w++)
            {
                var id = producedItems[cycle, w, step];
                if (id < 0) { continue; }

                var item = itemCache[ItemStaticData.Get(id)];
                foreach (var m in item.Materials)
                {
                    var mats = (m.Material.Source == MaterialSource.Gatherable) ? gatherableMaterials : rareMaterials;
                    if (!mats.TryAdd(m.Material, m.Count))
                    {
                        mats[m.Material] += m.Count;
                    }
                }
            }
        }

        var sortedGatherableMaterials = gatherableMaterials.OrderBy(x => x.Key.Name).ToArray();
        var sortedRareMaterials = rareMaterials .OrderBy(x => x.Key.Name).ToArray();
        for (var i = 0; i < sortedGatherableMaterials.Length || i < sortedRareMaterials.Length; i++)
        {
            ImGui.TableNextRow();
            if (i < sortedGatherableMaterials.Length)
            {
                var it = sortedGatherableMaterials[i];
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(it.Key.Name);
                ImGui.TableNextColumn();
                ImGui.Text(it.Value.ToString());
            }
            if (i < sortedRareMaterials.Length)
            {
                var it = sortedRareMaterials[i];
                ImGui.TableSetColumnIndex(3);
                ImGui.Text(it.Key.Name);
                ImGui.TableNextColumn();
                ImGui.Text(it.Key.Source.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(it.Value.ToString());
            }
        }

        ImGui.EndTable();
    }
}

