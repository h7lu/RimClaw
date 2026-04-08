using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    [StaticConstructorOnStartup]
    public class Window_HostComputerControlPanel : Window
    {
        private enum PlotTimeRange
        {
            Recent1Hour,
            Recent1Day,
            Recent15Days,
            All
        }

        private static readonly Dictionary<Pawn, Texture2D> PortraitCache = new Dictionary<Pawn, Texture2D>(new PawnEqualityComparer());
        private static readonly Texture2D ModelSignTexture = ContentFinder<Texture2D>.Get("model_sign", reportFailure: false);
        private static readonly Color BorderColor = new Color32(97, 108, 122, 255);
        private static readonly Color PanelBgColor = new Color32(21, 25, 29, 255);
        private const float AvatarSize = 46f;

        private readonly CompHostComputerService host;
        private readonly Dictionary<int, Rect> gpuIconRects = new Dictionary<int, Rect>();
        private Vector2 gpuScrollPos = Vector2.zero;
        private Vector2 clawPoolScrollPos = Vector2.zero;
        private int selectedGpuIndex = -1;
        private Pawn draggingClaw;
        private PlotTimeRange selectedPlotTimeRange = PlotTimeRange.All;

        public override Vector2 InitialSize => new Vector2(980f, 740f);

        public Window_HostComputerControlPanel(CompHostComputerService hostComp)
        {
            host = hostComp;
            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            absorbInputAroundWindow = false;
            draggable = true;
            optionalTitle = string.Empty;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (host?.parent == null || host.parent.Destroyed)
            {
                Close();
                return;
            }

            HostComputerSnapshot snapshot = host.GetSnapshot();
            if (selectedGpuIndex >= snapshot.Gpus.Count)
            {
                selectedGpuIndex = -1;
            }

            Text.Font = GameFont.Small;

            Widgets.DrawBoxSolid(inRect, PanelBgColor);

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x + 8f, inRect.y, inRect.width - 16f, 30f), "RimClaw_HostWindow_Title".Translate());
            Text.Font = oldFont;

            Rect contentRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, inRect.height - 30f);
            Rect topRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 144f);
            Rect leftRect = new Rect(contentRect.x, topRect.yMax + 8f, 320f, contentRect.height - topRect.height - 8f);
            Rect rightRect = new Rect(leftRect.xMax + 8f, topRect.yMax + 8f, contentRect.width - leftRect.width - 8f, contentRect.height - topRect.height - 8f);

            DrawTopBar(topRect, snapshot);
            DrawLeftPane(leftRect, snapshot);
            DrawRightPane(rightRect, snapshot);

            DrawLine(new Vector2(contentRect.x, topRect.yMax + 4f), new Vector2(contentRect.xMax, topRect.yMax + 4f));
            DrawLine(new Vector2(leftRect.xMax - 6f, leftRect.y), new Vector2(leftRect.xMax - 6f, leftRect.yMax));

            DrawDraggingPreview();
        }

        private void DrawTopBar(Rect rect, HostComputerSnapshot snapshot)
        {
            float basePoolWidth = rect.width - 388f;
            float clawsWidth = Mathf.Max(180f, basePoolWidth * 0.64f);
            Rect clawsRect = new Rect(rect.xMax - clawsWidth - 8f, rect.y + 8f, clawsWidth, rect.height - 16f);
            DrawOutline(clawsRect);

            float desiredSummaryWidth = 420f * 1.1f;
            float maxSummaryWidth = Mathf.Max(200f, clawsRect.x - rect.x - 16f);
            Rect summaryRect = new Rect(rect.x + 8f, rect.y + 12f, Mathf.Min(desiredSummaryWidth, maxSummaryWidth), rect.height - 20f);
            DrawSection1Stats(summaryRect, snapshot);

            Widgets.Label(new Rect(clawsRect.x + 8f, clawsRect.y + 4f, clawsRect.width - 16f, 24f), "RimClaw_HostWindow_ConnectedClaws".Translate());

            float cellSize = 56f;
            float spacing = 6f;
            const int columns = 5;
            float rowHeight = cellSize + 20f;
            Rect scrollRect = new Rect(clawsRect.x + 8f, clawsRect.y + 28f, clawsRect.width - 16f, clawsRect.height - 36f);
            int rowCount = Mathf.Max(1, Mathf.CeilToInt(snapshot.ConnectedClaws.Count / (float)columns));
            float contentHeight = rowCount * rowHeight + 4f;
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(1f, scrollRect.width - 16f), contentHeight);

            Widgets.BeginScrollView(scrollRect, ref clawPoolScrollPos, viewRect);

            for (int i = 0; i < snapshot.ConnectedClaws.Count; i++)
            {
                Pawn claw = snapshot.ConnectedClaws[i];
                int col = i % columns;
                int row = i / columns;
                Rect cell = new Rect(4f + col * (cellSize + spacing), 2f + row * rowHeight, cellSize, cellSize);
                DrawOutline(cell);
                Rect portraitRect = new Rect(cell.x + 5f, cell.y + 3f, AvatarSize, AvatarSize);
                DrawClawPortrait(portraitRect, claw);

                TextAnchor oldAnchor = Text.Anchor;
                GameFont oldFont = Text.Font;
                string assignedLabel = host.GetAssignedGpuLabel(claw);
                bool isNone = assignedLabel == "RimClaw_Generic_None".Translate().ToString();
                Color oldColor = GUI.color;
                if (isNone)
                {
                    GUI.color = Color.red;
                }

                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(cell.x + 2f, cell.y + 1f, cell.width - 4f, 14f), AbbreviateName(assignedLabel, 8));
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;

                Rect labelRect = new Rect(cell.x - 10f, cell.yMax + 1f, cell.width + 20f, 18f);
                DrawCenteredShortName(labelRect, claw);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && cell.Contains(Event.current.mousePosition))
                {
                    draggingClaw = claw;
                    Event.current.Use();
                }

                if (draggingClaw == claw && Event.current.type == EventType.MouseDrag && Event.current.button == 0)
                {
                    Event.current.Use();
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawLeftPane(Rect rect, HostComputerSnapshot snapshot)
        {
            Rect titleRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 24f);
            Widgets.Label(titleRect, "RimClaw_HostWindow_GpuList".Translate());

            DrawLine(new Vector2(rect.x + 8f, titleRect.yMax + 2f), new Vector2(rect.xMax - 8f, titleRect.yMax + 2f));

            Rect listRect = new Rect(rect.x + 8f, titleRect.yMax + 6f, rect.width - 16f, rect.height - 18f - titleRect.height);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, snapshot.Gpus.Count * 100f + 8f);
            gpuIconRects.Clear();

            Widgets.BeginScrollView(listRect, ref gpuScrollPos, viewRect);

            for (int i = 0; i < snapshot.Gpus.Count; i++)
            {
                HostGpuSnapshot gpu = snapshot.Gpus[i];
                Rect row = new Rect(0f, i * 100f, viewRect.width, 96f);
                DrawGpuRow(row, i, gpu, snapshot, listRect);
            }

            Widgets.EndScrollView();
        }

        private void DrawGpuRow(Rect row, int index, HostGpuSnapshot gpu, HostComputerSnapshot snapshot, Rect listRect)
        {
            DrawOutline(row);
            if (selectedGpuIndex == index)
            {
                Widgets.DrawHighlightSelected(row);
            }

            if (Mouse.IsOver(row))
            {
                Widgets.DrawHighlight(row);
            }

            const float iconSize = 72f;
            Rect iconRect = new Rect(row.x + 12f, row.y + 12f, iconSize, iconSize);
            DrawModelSign(iconRect, ResolveModelTint(gpu));

            Rect iconScreenRect = new Rect(listRect.x + iconRect.x - gpuScrollPos.x, listRect.y + iconRect.y - gpuScrollPos.y, iconRect.width, iconRect.height);
            gpuIconRects[gpu.ThingId] = iconScreenRect;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && iconRect.Contains(Event.current.mousePosition))
            {
                selectedGpuIndex = index;
                OpenModelMenuForGpu(gpu, snapshot);
                Event.current.Use();
                return;
            }

            float textX = iconRect.xMax + 12f;
            Widgets.Label(new Rect(textX, row.y + 8f, row.width - textX - 8f, 20f), gpu.Name);
            Widgets.Label(new Rect(textX, row.y + 30f, row.width - textX - 8f, 18f), gpu.ModelName);
            Widgets.Label(new Rect(textX, row.y + 50f, row.width - textX - 8f, 18f), "RimClaw_HostWindow_InstancesRow".Translate(gpu.UsedInstances, gpu.TotalInstances));

            Rect usageBar = new Rect(textX, row.y + 72f, row.width - textX - 8f, 10f);
            DrawOutline(usageBar);
            float pct = Mathf.Clamp01(gpu.TotalUsageFraction);
            Color modelTint = ResolveModelTint(gpu);
            Widgets.DrawBoxSolid(new Rect(usageBar.x + 1f, usageBar.y + 1f, (usageBar.width - 2f) * pct, usageBar.height - 2f), modelTint);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition) && !iconRect.Contains(Event.current.mousePosition))
            {
                selectedGpuIndex = index;
                Event.current.Use();
            }

            if (draggingClaw != null && Event.current.type == EventType.MouseUp && row.Contains(Event.current.mousePosition))
            {
                if (!gpu.IsActive)
                {
                    Messages.Message("RimClaw_HostWindow_AssignInactiveGpu".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    draggingClaw = null;
                    Event.current.Use();
                    return;
                }

                if (host.AssignClawToGpu(draggingClaw, gpu.ThingId))
                {
                    Messages.Message("RimClaw_HostWindow_AssignSuccess".Translate(draggingClaw.NameShortColored, gpu.Name), MessageTypeDefOf.TaskCompletion, historical: false);
                }
                else
                {
                    Messages.Message("RimClaw_HostWindow_AssignFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }

                draggingClaw = null;
                Event.current.Use();
            }
        }

        private void DrawRightPane(Rect rect, HostComputerSnapshot snapshot)
        {
            if (selectedGpuIndex < 0 || selectedGpuIndex >= snapshot.Gpus.Count)
            {
                Widgets.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 24f), "RimClaw_HostWindow_SelectGpu".Translate());
                return;
            }

            HostGpuSnapshot gpu = snapshot.Gpus[selectedGpuIndex];

            Rect statsRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 148f);
            DrawStats3(statsRect, gpu, snapshot);

            Rect memoryBarRect = new Rect(rect.x + 8f, statsRect.yMax + 8f, rect.width - 16f, 19f);
            DrawMemoryBar(memoryBarRect, gpu);

            const float rangeButtonsHeight = 24f;
            Rect graphRect = new Rect(rect.x + 8f, memoryBarRect.yMax + 12f, rect.width - 16f, rect.height - (memoryBarRect.yMax - rect.y) - 28f - rangeButtonsHeight);
            DrawGraph(graphRect, gpu.TpsHistory, gpu.HeatHistory);

            Rect rangeRect = new Rect(rect.x + 8f, graphRect.yMax + 6f, rect.width - 16f, rangeButtonsHeight);
            DrawPlotRangeButtons(rangeRect);
        }

        private static void DrawStats3(Rect rect, HostGpuSnapshot gpu, HostComputerSnapshot snapshot)
        {
            string[] labels =
            {
                gpu.Name,
                "RimClaw_HostWindow_Label_Model".Translate(),
                "RimClaw_HostWindow_Label_WorkSpeed".Translate(),
                "RimClaw_HostWindow_Label_Instances".Translate(),
                "RimClaw_HostWindow_Label_HeatRate".Translate(),
                "RimClaw_HostWindow_Label_TotalUsage".Translate(),
                "RimClaw_HostWindow_Label_TokenIO".Translate(),
                "RimClaw_HostWindow_Label_ActiveTime".Translate()
            };

            string[] values =
            {
                string.Empty,
                gpu.ModelName,
                $"x{gpu.WorkSpeedMultiplier:0.00}",
                $"{gpu.UsedInstances}/{gpu.TotalInstances}",
                $"{gpu.HeatRate:0.00}/s",
                $"{(gpu.TotalUsageFraction * 100f):0}%",
                $"{gpu.UsedTps:0.0}/{gpu.CapacityTps:0.0} TPS",
                $"{snapshot.ActiveTimeSeconds:0}s"
            };

            int rowsPerColumn = 4;
            float colWidth = (rect.width - 24f) / 2f;
            float rowHeight = 28f;
            float labelWidth = 138f;

            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 20f), gpu.Name);
            for (int i = 1; i < labels.Length; i++)
            {
                int idx = i - 1;
                int col = idx / rowsPerColumn;
                int row = idx % rowsPerColumn;
                float x = rect.x + 8f + col * colWidth;
                float y = rect.y + 30f + row * rowHeight;

                Widgets.Label(new Rect(x, y, labelWidth, rowHeight), labels[i] + ":");
                Widgets.Label(new Rect(x + labelWidth, y, colWidth - labelWidth - 6f, rowHeight), values[i]);
            }
        }

        private static void DrawMemoryBar(Rect rect, HostGpuSnapshot gpu)
        {
            if (gpu.TotalInstances <= 0)
            {
                DrawOutline(rect);
                Widgets.Label(new Rect(rect.x + 6f, rect.y + 8f, rect.width - 12f, 24f), "RimClaw_HostWindow_NoRunnableInstances".Translate());
                return;
            }

            DrawOutline(rect);
            const float leftPad = 3f;
            const float rightPad = 3f;
            const float topPad = 4f;
            const float bottomPad = 4f;
            const float lineWidth = 1f;

            Rect inner = new Rect(rect.x + leftPad, rect.y + topPad, rect.width - leftPad - rightPad, rect.height - topPad - bottomPad);
            Widgets.DrawBoxSolid(inner, new Color(0.13f, 0.13f, 0.13f, 1f));

            int divisions = Mathf.Max(1, gpu.TotalInstances);
            float step = inner.width / divisions;
            Color fillColor = ResolveModelTint(gpu);
            Color lineColor = new Color(0.60f, 0.60f, 0.60f, 1f);

            for (int i = 0; i < divisions; i++)
            {
                float slotUsage = i < gpu.InstanceUsageFractions.Count ? Mathf.Clamp01(gpu.InstanceUsageFractions[i]) : 0f;
                if (slotUsage > 0f)
                {
                    float slotX = inner.x + i * step;
                    Rect fillRect = new Rect(slotX, inner.y, step * slotUsage, inner.height);
                    Widgets.DrawBoxSolid(fillRect, fillColor);
                }
            }

            for (int i = 1; i < divisions; i++)
            {
                float x = inner.x + i * step;
                Widgets.DrawLine(new Vector2(x, inner.y), new Vector2(x, inner.yMax), lineColor, lineWidth);
            }
        }

        private void DrawGraph(Rect rect, List<float> tps, List<float> heat)
        {
            int totalCount = Mathf.Min(tps.Count, heat.Count);
            GetHistorySlice(totalCount, out int startIndex, out int count);
            if (count < 2)
            {
                return;
            }

            List<float> tpsSlice = tps.GetRange(startIndex, count);
            List<float> heatSlice = heat.GetRange(startIndex, count);
            ConsoleLineChartUtility.DrawDualSeries(rect, tpsSlice, heatSlice, Color.green, new Color(1f, 0.5f, 0f, 1f), BorderColor);
        }

        private void DrawPlotRangeButtons(Rect rect)
        {
            const float buttonWidth = 110f;
            const float spacing = 8f;

            PlotTimeRange[] ranges =
            {
                PlotTimeRange.Recent1Hour,
                PlotTimeRange.Recent1Day,
                PlotTimeRange.Recent15Days,
                PlotTimeRange.All
            };

            string[] labels =
            {
                "RimClaw_HostWindow_Range_1h".Translate(),
                "RimClaw_HostWindow_Range_1d".Translate(),
                "RimClaw_HostWindow_Range_15d".Translate(),
                "RimClaw_HostWindow_Range_All".Translate()
            };

            float totalWidth = ranges.Length * buttonWidth + (ranges.Length - 1) * spacing;
            float x = rect.x + Mathf.Max(0f, (rect.width - totalWidth) * 0.5f);
            for (int i = 0; i < ranges.Length; i++)
            {
                Rect buttonRect = new Rect(x + i * (buttonWidth + spacing), rect.y, buttonWidth, rect.height);
                if (selectedPlotTimeRange == ranges[i])
                {
                    Widgets.DrawHighlightSelected(buttonRect);
                }

                if (Widgets.ButtonText(buttonRect, labels[i]))
                {
                    selectedPlotTimeRange = ranges[i];
                }
            }
        }

        private void OpenModelMenuForGpu(HostGpuSnapshot gpu, HostComputerSnapshot snapshot)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            options.Add(new FloatMenuOption("RimClaw_Generic_NoneParen".Translate(), delegate
            {
                if (!host.SetGpuModelForGpu(gpu.ThingId, -1))
                {
                    Messages.Message("RimClaw_HostWindow_SwitchModelFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
            }, (Thing)null, new Color32(100, 100, 100, 255), MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));

            foreach (HostModelOptionSnapshot option in snapshot.AvailableModels)
            {
                int diskThingId = option.DiskThingId;
                string label = option.ModelName;
                Color modelColor = option.ModelColor;

                options.Add(new FloatMenuOption(label, delegate
                {
                    if (!host.SetGpuModelForGpu(gpu.ThingId, diskThingId))
                    {
                        Messages.Message("RimClaw_HostWindow_SwitchModelFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    }
                }, (Thing)null, modelColor, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void GetHistorySlice(int totalCount, out int startIndex, out int count)
        {
            if (totalCount <= 0)
            {
                startIndex = 0;
                count = 0;
                return;
            }

            int wantedSamples = totalCount;
            switch (selectedPlotTimeRange)
            {
                case PlotTimeRange.Recent1Hour:
                    wantedSamples = Mathf.CeilToInt(2500f / 60f);
                    break;
                case PlotTimeRange.Recent1Day:
                    wantedSamples = Mathf.CeilToInt(60000f / 60f);
                    break;
                case PlotTimeRange.Recent15Days:
                    wantedSamples = Mathf.CeilToInt(15f * 60000f / 60f);
                    break;
                case PlotTimeRange.All:
                default:
                    wantedSamples = totalCount;
                    break;
            }

            count = Mathf.Clamp(wantedSamples, 1, totalCount);
            startIndex = Mathf.Max(0, totalCount - count);
        }

        private void DrawDraggingPreview()
        {
            if (draggingClaw == null)
            {
                return;
            }

            if (Event.current.type == EventType.MouseUp)
            {
                draggingClaw = null;
                return;
            }

            Vector2 mouse = Event.current.mousePosition;
            Rect dragRect = new Rect(mouse.x + 10f, mouse.y + 10f, 192f, 52f);
            Widgets.DrawBoxSolid(dragRect, new Color(0f, 0f, 0f, 0.7f));
            DrawOutline(dragRect);
            DrawClawPortrait(new Rect(dragRect.x + 4f, dragRect.y + 3f, 44f, 44f), draggingClaw);
            Widgets.Label(new Rect(dragRect.x + 54f, dragRect.y + 8f, dragRect.width - 60f, 18f), "RimClaw_HostWindow_AssignPrompt".Translate(AbbreviateName(draggingClaw.LabelShortCap ?? draggingClaw.LabelCap ?? string.Empty, 8)));
            Widgets.Label(new Rect(dragRect.x + 54f, dragRect.y + 26f, dragRect.width - 60f, 18f), host.GetAssignedGpuLabel(draggingClaw));
        }

        private static void DrawClawPortrait(Rect rect, Pawn pawn)
        {
            Texture2D portrait = GetPortraitTexture(pawn);
            if (portrait != null)
            {
                Color oldColor = GUI.color;
                GUI.color = ClawfishUtility.GetOrAssignColor(pawn);
                Widgets.DrawTextureFitted(rect, portrait, 1f);
                GUI.color = oldColor;
                return;
            }

            Widgets.DrawBoxSolid(rect, new Color(0.55f, 0.15f, 0.75f, 1f));
        }

        private static void DrawModelSign(Rect rect)
        {
            DrawModelSign(rect, Color.white);
        }

        private static void DrawModelSign(Rect rect, Color tint)
        {
            if (ModelSignTexture != null)
            {
                Color oldColor = GUI.color;
                GUI.color = tint;
                Widgets.DrawTextureFitted(rect, ModelSignTexture, 1f);
                GUI.color = oldColor;
                return;
            }

            Widgets.DrawBoxSolid(rect, tint);
        }

        private static Color ResolveModelTint(HostGpuSnapshot gpu)
        {
            if (!gpu.IsActive)
            {
                return new Color32(100, 100, 100, 255);
            }

            if (gpu.ModelColor.grayscale < 0.95f)
            {
                return gpu.ModelColor;
            }

            int hash = string.IsNullOrEmpty(gpu.ModelName) ? 0 : gpu.ModelName.GetHashCode();
            float hue = Mathf.Abs(hash % 1000) / 1000f;
            return Color.HSVToRGB(hue, 0.65f, 0.85f);
        }

        private static Texture2D GetPortraitTexture(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            if (PortraitCache.TryGetValue(pawn, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            string path = ClawfishUtility.GetPortraitSouthTexPath(pawn);
            Texture2D texture = ContentFinder<Texture2D>.Get(path, reportFailure: false);
            if (texture != null)
            {
                PortraitCache[pawn] = texture;
            }

            return texture;
        }

        private static void DrawSection1Stats(Rect rect, HostComputerSnapshot snapshot)
        {
            string[] labels =
            {
                "RimClaw_HostWindow_Stat_GpuMachines".Translate(),
                "RimClaw_HostWindow_Stat_RoomTemperature".Translate(),
                "RimClaw_HostWindow_Stat_TotalTokenIO".Translate(),
                "RimClaw_HostWindow_Stat_Power".Translate(),
                "RimClaw_HostWindow_Stat_ConnectedClaws".Translate(),
                "RimClaw_HostWindow_Stat_VramUsage".Translate(),
                "RimClaw_HostWindow_Stat_Models".Translate(),
                "RimClaw_HostWindow_Stat_Instances".Translate()
            };

            string[] values =
            {
                $"{snapshot.ActiveGpuCount}/{snapshot.TotalGpuCount}",
                $"{snapshot.RoomTemperature:0.0}C",
                $"{snapshot.TotalLiveTokenRate:0.0} TPS",
                $"{snapshot.TotalPowerConsumption:0} W",
                $"{snapshot.TotalConnectedClaws}",
                $"{snapshot.UsedVram}/{snapshot.TotalVram}",
                $"{snapshot.ModelCount}",
                $"{snapshot.UsedInstances}/{snapshot.TotalInstances}"
            };

            int rowsPerColumn = 4;
            float colWidth = (rect.width - 24f) / 2f;
            float rowHeight = 28f;
            float labelWidth = 130f;

            for (int i = 0; i < labels.Length; i++)
            {
                int col = i / rowsPerColumn;
                int row = i % rowsPerColumn;
                float x = rect.x + 8f + col * colWidth;
                float y = rect.y + 8f + row * rowHeight;

                Widgets.Label(new Rect(x, y, labelWidth, rowHeight), labels[i] + ":");
                Widgets.Label(new Rect(x + labelWidth, y, colWidth - labelWidth - 6f, rowHeight), values[i]);
            }
        }

        private static void DrawOutline(Rect rect)
        {
            DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y));
            DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.yMax));
            DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.x, rect.yMax));
            DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x, rect.y));
        }

        private static void DrawCenteredShortName(Rect rect, Pawn pawn)
        {
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, AbbreviateName(pawn?.LabelShortCap ?? pawn?.LabelCap ?? string.Empty, 8));
            Text.Anchor = oldAnchor;
        }

        private static string AbbreviateName(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text;
            }

            if (maxChars <= 3)
            {
                return text.Substring(0, maxChars);
            }

            return text.Substring(0, maxChars - 3) + "...";
        }

        private static void DrawGraduations(float axisX, Rect plot, int maxValue, bool leftAxis, float drawableHeight, int ticks)
        {
            Color tickColor = new Color(0.60f, 0.60f, 0.60f, 1f);
            float tickStart = leftAxis ? axisX : axisX - 5f;
            float tickEnd = leftAxis ? axisX + 5f : axisX;
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            for (int i = 0; i <= ticks; i++)
            {
                float ratio = i / (float)ticks;
                float y = plot.yMax - 8f - ratio * drawableHeight;
                Widgets.DrawLine(new Vector2(tickStart, y), new Vector2(tickEnd, y), tickColor, 1f);
                int labelValue = Mathf.RoundToInt(maxValue * ratio);
                string label = labelValue.ToString();
                float labelWidth = 64f;
                float labelX = leftAxis ? axisX - labelWidth + 34f : axisX + 6f;
                Widgets.Label(new Rect(labelX, y - 8f, labelWidth, 16f), label);
            }
            Text.Font = oldFont;
        }

        private static void DrawLine(Vector2 from, Vector2 to)
        {
            Widgets.DrawLine(from, to, BorderColor, 1f);
        }
    }
}
