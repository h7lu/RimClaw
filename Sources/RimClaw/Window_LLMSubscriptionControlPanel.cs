using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    [StaticConstructorOnStartup]
    public class Window_LLMSubscriptionControlPanel : Window
    {
        private static readonly Dictionary<Pawn, Texture2D> PortraitCache = new Dictionary<Pawn, Texture2D>(new PawnEqualityComparer());
        private static readonly Color BorderColor = new Color32(97, 108, 122, 255);
        private static readonly Color PanelBgColor = new Color32(21, 25, 29, 255);
        private static readonly Texture2D ModelSignTexture = ContentFinder<Texture2D>.Get("model_sign", reportFailure: false);
        private const float AvatarSize = 72f;
        private readonly CompLLMSubscriptionService subscription;
        private Vector2 clawScrollPos = Vector2.zero;
        private int selectedClawIndex = -1;
        private GraphRange range = GraphRange.Daily;

        private enum GraphRange
        {
            Daily,
            FifteenDay,
            All
        }

        public override Vector2 InitialSize => new Vector2(920f, 640f);

        public Window_LLMSubscriptionControlPanel(CompLLMSubscriptionService comp)
        {
            subscription = comp;
            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            absorbInputAroundWindow = false;
            draggable = true;
            optionalTitle = string.Empty;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (subscription?.parent == null || subscription.parent.Destroyed)
            {
                Close();
                return;
            }

            SubscriptionSnapshot snapshot = subscription.GetSnapshot();
            if (selectedClawIndex >= snapshot.Claws.Count)
            {
                selectedClawIndex = -1;
            }

            Text.Font = GameFont.Small;
            Widgets.DrawBoxSolid(inRect, PanelBgColor);

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x + 8f, inRect.y, inRect.width - 16f, 30f), "RimClaw_SubWindow_Title".Translate());
            Text.Font = oldFont;

            Rect contentRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, inRect.height - 30f);
            Rect topRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 228f);
            Rect bottomRect = new Rect(contentRect.x, topRect.yMax + 8f, contentRect.width, contentRect.height - topRect.height - 8f);
            Rect leftBottom = new Rect(bottomRect.x, bottomRect.y, contentRect.width * 0.58f, bottomRect.height);
            Rect rightBottom = new Rect(leftBottom.xMax + 8f, bottomRect.y, contentRect.width - leftBottom.width - 8f, bottomRect.height);

            DrawTop(topRect, snapshot);
            DrawConnectedClawGrid(leftBottom, snapshot);
            DrawClawDetail(rightBottom, snapshot);

            DrawLine(new Vector2(contentRect.x, topRect.yMax + 4f), new Vector2(contentRect.xMax, topRect.yMax + 4f));
            DrawLine(new Vector2(leftBottom.xMax - 6f, leftBottom.y), new Vector2(leftBottom.xMax - 6f, leftBottom.yMax));
        }

        private void DrawTop(Rect rect, SubscriptionSnapshot snapshot)
        {
            float splitWidth = (rect.width - 24f) * 0.5f;
            Rect left = new Rect(rect.x + 8f, rect.y + 8f, splitWidth, rect.height - 16f);
            Rect right = new Rect(left.xMax + 8f, rect.y + 8f, rect.width - splitWidth - 24f, rect.height - 16f);

            DrawLine(new Vector2(left.x + 8f, left.y + 24f), new Vector2(left.xMax - 8f, left.y + 24f));

            Rect modelIconRect = new Rect(left.x + 8f, left.y + 30f, 72f, 72f);
            DrawModelSign(modelIconRect, snapshot.ModelColor);
            Widgets.Label(new Rect(modelIconRect.xMax + 10f, left.y + 34f, left.width - modelIconRect.width - 26f, 26f), AbbreviateName(snapshot.ModelIdentifier, 24));

            string[] labels =
            {
                "RimClaw_SubWindow_Label_PricePerK".Translate(),
                "RimClaw_SubWindow_Label_WorkSpeed".Translate(),
                "RimClaw_SubWindow_Label_LiveIO".Translate(),
                "RimClaw_SubWindow_Label_LifetimeSpent".Translate(),
                "RimClaw_SubWindow_Label_ActiveClaws".Translate(),
                "RimClaw_SubWindow_Label_PerSecond".Translate(),
                "RimClaw_SubWindow_Label_CurrentHourCost".Translate(),
                "RimClaw_SubWindow_Label_PendingPayment".Translate()
            };

            string[] values =
            {
                $"{snapshot.PricePerKTokens:0.00}",
                $"x{snapshot.SpeedMultiplier:0.00}",
                $"{snapshot.LiveThroughput:0.0} TPS",
                $"{snapshot.LifetimeSilverSpent:0.00}",
                $"{snapshot.ActiveClaws}",
                $"{snapshot.PerSecondSilverRate:0.00}",
                $"{snapshot.CurrentHourSilverSpent:0.00}",
                $"{snapshot.PendingPayment:0.00}"
            };

            DrawTwoColumnStats(new Rect(left.x + 8f, left.y + 110f, left.width - 16f, left.height - 114f), labels, values);

            Widgets.Label(new Rect(right.x + 8f, right.y + 4f, right.width - 16f, 22f), "RimClaw_SubWindow_HourlySilver".Translate());
            DrawLine(new Vector2(right.x + 8f, right.y + 24f), new Vector2(right.xMax - 8f, right.y + 24f));
            Rect graphRect = new Rect(right.x + 8f, right.y + 26f, right.width - 16f, right.height - 62f);
            DrawLineGraph(graphRect, EnsureRenderableSeries(GetSeriesForRange(snapshot.HourlySilverHistory), snapshot.CurrentHourSilverSpent), new Color(0.95f, 0.88f, 0.10f, 1f), 2);
            DrawPlotRangeButtons(new Rect(right.x + 8f, right.yMax - 30f, right.width - 16f, 24f));
        }

        private void DrawConnectedClawGrid(Rect rect, SubscriptionSnapshot snapshot)
        {
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), "RimClaw_SubWindow_ConnectedClaws".Translate());
            DrawLine(new Vector2(rect.x + 8f, rect.y + 26f), new Vector2(rect.xMax - 8f, rect.y + 26f));

            Rect scrollRect = new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, rect.height - 38f);
            int columns = 4;
            float cellW = (scrollRect.width - 16f) / columns;
            float cellH = 108f;
            int rows = Mathf.CeilToInt(snapshot.Claws.Count / (float)columns);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 18f, rows * cellH + 4f);

            Widgets.BeginScrollView(scrollRect, ref clawScrollPos, viewRect);

            for (int i = 0; i < snapshot.Claws.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Rect cell = new Rect(col * cellW, row * cellH, cellW - 6f, cellH - 6f);

                DrawOutline(cell);
                if (selectedClawIndex == i)
                {
                    Widgets.DrawHighlightSelected(cell);
                }
                else if (Mouse.IsOver(cell))
                {
                    Widgets.DrawHighlight(cell);
                }

                SubscriptionClawSnapshot claw = snapshot.Claws[i];
                Rect portraitRect = new Rect(cell.x + (cellW - AvatarSize)/2, cell.y + 8f, AvatarSize, AvatarSize);
                DrawOutline(portraitRect);
                DrawClawPortrait(portraitRect, claw.Claw);

                TextAnchor oldAnchor = Text.Anchor;
                Color oldColor = GUI.color;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.87f, 0.58f, 0.28f, 1f);
                Widgets.Label(new Rect(cell.x + 2f, portraitRect.yMax + 4f, cell.width - 4f, 18f), AbbreviateName(claw.Claw?.LabelShortCap ?? claw.Claw?.LabelCap ?? string.Empty, 10));
                GUI.color = oldColor;
                Text.Anchor = oldAnchor;

                if (Widgets.ButtonInvisible(cell))
                {
                    selectedClawIndex = i;
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawClawDetail(Rect rect, SubscriptionSnapshot snapshot)
        {
            if (selectedClawIndex < 0 || selectedClawIndex >= snapshot.Claws.Count)
            {
                Widgets.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 24f), "RimClaw_SubWindow_SelectClaw".Translate());
                return;
            }

            SubscriptionClawSnapshot selected = snapshot.Claws[selectedClawIndex];
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), AbbreviateName(selected.Claw?.LabelShortCap ?? selected.Claw?.LabelCap ?? string.Empty, 18));
            DrawLine(new Vector2(rect.x + 8f, rect.y + 26f), new Vector2(rect.xMax - 8f, rect.y + 26f));

            float maxAvailable = subscription.GetAvailableTokenRateForClawfish(selected.Claw);
            string detail =
                "RimClaw_SubWindow_Detail_CurrentToken".Translate(selected.CurrentTps.ToString("0.0")) + "\n" +
                "RimClaw_SubWindow_Detail_ProvidedToken".Translate(selected.ProvidedTps.ToString("0.0"), maxAvailable.ToString("0.0")) + "\n" +
                "RimClaw_SubWindow_Detail_SilverPerSec".Translate(selected.SilverPerSecond.ToString("0.0000"));
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, 88f), detail);

            Rect buttonDaily = new Rect(rect.x + 8f, rect.y + 118f, 66f, 24f);
            Rect button15 = new Rect(buttonDaily.xMax + 6f, buttonDaily.y, 70f, 24f);
            Rect buttonAll = new Rect(button15.xMax + 6f, buttonDaily.y, 52f, 24f);
            if (range == GraphRange.Daily)
            {
                Widgets.DrawHighlightSelected(buttonDaily);
            }

            if (range == GraphRange.FifteenDay)
            {
                Widgets.DrawHighlightSelected(button15);
            }

            if (range == GraphRange.All)
            {
                Widgets.DrawHighlightSelected(buttonAll);
            }

            if (Widgets.ButtonText(buttonDaily, "RimClaw_SubWindow_Range_Daily".Translate()))
            {
                range = GraphRange.Daily;
            }

            if (Widgets.ButtonText(button15, "RimClaw_SubWindow_Range_15d".Translate()))
            {
                range = GraphRange.FifteenDay;
            }

            if (Widgets.ButtonText(buttonAll, "RimClaw_SubWindow_Range_All".Translate()))
            {
                range = GraphRange.All;
            }

            Rect graphRect = new Rect(rect.x + 8f, rect.y + 150f, rect.width - 16f, rect.height - 204f);
            List<float> clawSeries = GetSeriesForRange(selected.SilverPerSecondHistory);
            DrawLineGraph(graphRect, EnsureRenderableSeries(clawSeries, selected.SilverPerSecond), new Color(0.95f, 0.88f, 0.10f, 1f), 3);
            //DrawPlotRangeButtons(new Rect(rect.x + 8f, rect.yMax - 30f, rect.width - 16f, 24f));

            Rect disconnectRect = new Rect(rect.x + 8f, rect.yMax - 44f, rect.width - 16f, 32f);
            if (Widgets.ButtonText(disconnectRect, "RimClaw_SubWindow_Disconnect".Translate()))
            {
                subscription.DisconnectClaw(selected.Claw);
                selectedClawIndex = -1;
                Messages.Message("RimClaw_SubWindow_DisconnectMessage".Translate(selected.Claw.NameShortColored), MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        private List<float> GetSeriesForRange(List<float> source)
        {
            int desired;
            switch (range)
            {
                case GraphRange.Daily:
                    desired = 180;
                    break;
                case GraphRange.FifteenDay:
                    desired = 360;
                    break;
                case GraphRange.All:
                default:
                    desired = int.MaxValue;
                    break;
            }

            if (source == null || source.Count <= desired)
            {
                return source ?? new List<float>();
            }

            return source.GetRange(source.Count - desired, desired);
        }

        private static void DrawLineGraph(Rect rect, List<float> values, Color color, int axisDecimals)
        {
            ConsoleLineChartUtility.DrawSingleSeries(rect, values, color, BorderColor, "RimClaw_Chart_SilverPerSec".Translate(), axisDecimals);
        }

        private static List<float> EnsureRenderableSeries(List<float> source, float fallbackValue)
        {
            List<float> series = source ?? new List<float>();
            if (series.Count >= 2)
            {
                return series;
            }

            float value = series.Count == 1 ? series[0] : fallbackValue;
            return new List<float> { value, value };
        }

        private static void DrawModelSign(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;

            if (ModelSignTexture != null)
            {
                GUI.DrawTexture(rect, ModelSignTexture, ScaleMode.ScaleToFit, alphaBlend: true);
            }
            else
            {
                Widgets.DrawBoxSolid(rect, color);
            }

            GUI.color = oldColor;
        }

        private static void DrawClawPortrait(Rect rect, Pawn claw)
        {
            if (claw == null)
            {
                return;
            }

            Texture2D portrait = GetPortraitTexture(claw);
            if (portrait != null)
            {
                Color oldColor = GUI.color;
                GUI.color = ClawfishUtility.GetOrAssignColor(claw);
                Widgets.DrawTextureFitted(rect, portrait, 1f);
                GUI.color = oldColor;
                return;
            }

            Widgets.DrawBoxSolid(rect, ClawfishUtility.GetOrAssignColor(claw));
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

        private static void DrawTwoColumnStats(Rect rect, string[] labels, string[] values)
        {
            int count = Math.Min(labels.Length, values.Length);
            int rowsPerColumn = (count + 1) / 2;
            float colWidth = (rect.width - 16f) / 2f;
            float rowHeight = 24f;
            float labelWidth = 124f;

            for (int i = 0; i < count; i++)
            {
                int col = i / rowsPerColumn;
                int row = i % rowsPerColumn;
                float x = rect.x + col * (colWidth + 8f);
                float y = rect.y + row * rowHeight;

                Widgets.Label(new Rect(x, y, labelWidth, rowHeight), labels[i] + ":");
                Widgets.Label(new Rect(x + labelWidth, y, colWidth - labelWidth, rowHeight), values[i]);
            }
        }

        private static void DrawOutline(Rect rect)
        {
            DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y));
            DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.yMax));
            DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.x, rect.yMax));
            DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x, rect.y));
        }

        private static void DrawLine(Vector2 from, Vector2 to)
        {
            Widgets.DrawLine(from, to, BorderColor, 1f);
        }

        private void DrawPlotRangeButtons(Rect rect)
        {
            const float buttonWidth = 68f;
            const float spacing = 6f;

            string[] labels =
            {
                "RimClaw_SubWindow_Range_1d".Translate(),
                "RimClaw_SubWindow_Range_15d".Translate(),
                "RimClaw_SubWindow_Range_All".Translate()
            };

            GraphRange[] values =
            {
                GraphRange.Daily,
                GraphRange.FifteenDay,
                GraphRange.All
            };

            float totalWidth = labels.Length * buttonWidth + (labels.Length - 1) * spacing;
            float x = rect.x + Mathf.Max(0f, (rect.width - totalWidth) * 0.5f);

            for (int i = 0; i < labels.Length; i++)
            {
                Rect buttonRect = new Rect(x + i * (buttonWidth + spacing), rect.y, buttonWidth, rect.height);
                if (range == values[i])
                {
                    Widgets.DrawHighlightSelected(buttonRect);
                }

                if (Widgets.ButtonText(buttonRect, labels[i]))
                {
                    range = values[i];
                }
            }
        }

        private static string AbbreviateName(string value, int limit)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= limit)
            {
                return value;
            }

            if (limit <= 1)
            {
                return value.Substring(0, 1);
            }

            return value.Substring(0, limit - 1) + "…";
        }
    }

    internal static class SubscriptionPanelExtensions
    {
        public static string GetAssignedGpuLabelFallback(this CompLLMSubscriptionService service, Pawn claw)
        {
            if (claw == null)
            {
                return "RimClaw_SubWindow_NoClaw".Translate();
            }

            CompClawfishTokenConnection conn = claw.TryGetComp<CompClawfishTokenConnection>();
            if (conn == null || !conn.IsConnected || conn.ConnectedSupplier == null)
            {
                return "RimClaw_SubWindow_Unassigned".Translate();
            }

            CompHostComputerService host = conn.ConnectedSupplier.TryGetComp<CompHostComputerService>();
            if (host != null)
            {
                return host.GetAssignedGpuLabel(claw);
            }

            return conn.ConnectedSupplier.LabelShortCap;
        }
    }
}
