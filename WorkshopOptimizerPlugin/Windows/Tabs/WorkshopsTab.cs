using ImGuiNET;
using System;
using System.Linq;
using WorkshopOptimizerPlugin.Data;
using WorkshopOptimizerPlugin.Optimizer;
using WorkshopOptimizerPlugin.Windows.Utils;

namespace WorkshopOptimizerPlugin.Windows.Tabs;

internal class WorkshopsTab : ITab, IUIDataSourceListener
{
    private readonly Configuration configuration;
    private readonly UIDataSource uiDataSource;
    private CommonInterfaceElements ifData;
    private readonly IItemSetsCache[] itemSetsCaches;
    private Optimizer.Optimizer?[,] optimizers;

    public WorkshopsTab(Configuration configuration, UIDataSource uiDataSource, CommonInterfaceElements ifData, IItemSetsCache[] itemSetsCaches)
    {
        this.configuration = configuration;
        this.uiDataSource = uiDataSource;
        this.ifData = ifData;
        this.itemSetsCaches = itemSetsCaches;
        this.optimizers = new Optimizer.Optimizer?[Constants.MaxSeasons, Constants.MaxCycles];

        uiDataSource.AddListener(this);
    }

    public void OnDataChange(int cycle)
    {
        for (int i = cycle; i < Constants.MaxCycles; i++)
        {
            this.optimizers[Constants.CurrentSeason, i] = null;
        }
    }

    public void OnOptimizationParameterChange()
    {
        OnDataChange(0);
        for (int i = 0; i < Constants.MaxCycles; i++)
        {
            this.optimizers[Constants.PreviousSeason, i] = null;
        }
    }

    public void OnOpen() { }

    public void Draw()
    {
        ifData.DrawBasicControls(uiDataSource);
        var cycle = ifData.Cycle;
        var startGroove = ifData.GetStartGroove(uiDataSource);
        Func<Item, string> formatPattern = i => {
            var (pattern, some) = i.FindPattern(cycle);
            return some ? pattern?.Name ?? "*" : "?";
        };

        ImGui.SameLine();
        ifData.DrawFilteringControls(uiDataSource);
        ImGui.Spacing();

        var itemCache = ifData.IsCurrentSeason() ? uiDataSource.CurrentItemCache : uiDataSource.PreviousItemCache;
        var producedItems = ifData.IsCurrentSeason() ? uiDataSource.DataSource.CurrentProducedItems : uiDataSource.DataSource.PreviousProducedItems;
        var disabled = ifData.IsPreviousSeason();
        var optimizer = optimizers[ifData.Season, cycle];
        if (optimizer == null)
        {
            var options = new OptimizerOptions(configuration, ifData.StrictCycles ? Strictness.StrictDefaults : Strictness.RelaxedDefaults, ifData.RestCycles);
            optimizers[ifData.Season, cycle] = optimizer = new Optimizer.Optimizer(itemCache, cycle, startGroove, options);

        }
        var cWorkshopsItemSets = itemSetsCaches[ifData.Season].CachedWorkshopsItemSets[cycle];
        var progress = 0.0;
        if (cWorkshopsItemSets == null)
        {
            (cWorkshopsItemSets, progress) = optimizer.GenerateAllWorkshops();
            itemSetsCaches[ifData.Season].CachedWorkshopsItemSets[cycle] = cWorkshopsItemSets;
        }
        if (cWorkshopsItemSets == null)
        {
            var adjProgress = Math.Floor(progress * 20) * 5;
            ImGui.Text($"Calculating, please wait... {adjProgress:F0}%%");
        } else if (ImGui.BeginTable("Workshop Combinations", 6, ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Items", ImGuiTableColumnFlags.WidthFixed, 600);
            ImGui.TableSetupColumn("Patterns", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Hours", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Groove", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Set", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableHeadersRow();

            var top = ifData.Top;
            foreach (var workshopsItemSets in cWorkshopsItemSets)
            {
                if (top-- == 0) { break; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                for (int w = 0; w < Constants.MaxWorkshops; w++)
                {
                    ImGui.Text(string.Join("/", workshopsItemSets.ItemSets[w].Items.Select(i => i.Name)));
                }
                ImGui.TableNextColumn();
                for (int w = 0; w < Constants.MaxWorkshops; w++)
                {
                    ImGui.Text(string.Join("/", workshopsItemSets.ItemSets[w].Items.Select(i => formatPattern(i))));
                }
                ImGui.TableNextColumn();
                for (int w = 0; w < Constants.MaxWorkshops; w++)
                {
                    ImGui.Text(string.Join("/", workshopsItemSets.ItemSets[w].Items.Select(i => i.Hours)));
                }
                ImGui.TableNextColumn();
                ImGui.Text("");
                ImGui.Text(string.Format("{0:F2}", workshopsItemSets.EffectiveValue));
                ImGui.TableNextColumn();
                ImGui.Text("");
                ImGui.Text(string.Format("Groove: {0} -> {1}", startGroove, workshopsItemSets.EndGroove));
                ImGui.TableNextColumn();
                ImGui.Text("");
                if (disabled) { ImGui.BeginDisabled(); }
                if (ImGui.Button($"*###WC-*-{top}"))
                {
                    for (int w = 0; w < Constants.MaxWorkshops; w++)
                    {
                        var itemSet = workshopsItemSets.ItemSets[w];
                        for (int s = 0; s < Constants.MaxSteps; s++)
                        {
                            producedItems[cycle, w, s] = (s < itemSet.Items.Length) ? (int)itemSet.Items[s].Id : -1;
                        }
                    }
                    uiDataSource.DataChanged(cycle + 1);
                }
                if (disabled) { ImGui.EndDisabled(); }
            }
            ImGui.EndTable();
        }
    }
}
