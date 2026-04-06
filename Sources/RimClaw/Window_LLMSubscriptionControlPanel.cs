using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public class Window_LLMSubscriptionControlPanel : Window
    {
        private readonly CompLLMSubscriptionService subscription;
        private Vector2 clawScrollPos = Vector2.zero;
        private int selectedClawIndex = -1;
        private GraphRange range = GraphRange.Daily;

        private enum GraphRange
        {
            Daily,
            FifteenDay
        }

        public override Vector2 InitialSize => new Vector2(920f, 640f);

        public Window_LLMSubscriptionControlPanel(CompLLMSubscriptionService comp)
        {
            subscription = comp;
            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            absorbInputAroundWindow = true;
            draggable = true;
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

            Rect topRect = new Rect(inRect.x, inRect.y, inRect.width, 190f);
            Rect bottomRect = new Rect(inRect.x, topRect.yMax + 8f, inRect.width, inRect.height - topRect.height - 8f);
            Rect leftBottom = new Rect(bottomRect.x, bottomRect.y, inRect.width * 0.58f, bottomRect.height);
            Rect rightBottom = new Rect(leftBottom.xMax + 8f, bottomRect.y, inRect.width - leftBottom.width - 8f, bottomRect.height);

            DrawTop(topRect, snapshot);
            DrawConnectedClawGrid(leftBottom, snapshot);
            DrawClawDetail(rightBottom, snapshot);
        }

        private static void DrawTop(Rect rect, SubscriptionSnapshot snapshot)
        {
            Widgets.DrawMenuSection(rect);

            Rect left = new Rect(rect.x + 8f, rect.y + 8f, rect.width * 0.42f, rect.height - 16f);
            Rect right = new Rect(left.xMax + 8f, rect.y + 8f, rect.width - left.width - 24f, rect.height - 16f);

            Widgets.DrawBox(left);
            Widgets.Label(new Rect(left.x + 8f, left.y + 4f, left.width - 16f, 24f), "LLM Subscription");

            Widgets.DrawBoxSolid(new Rect(left.x + 8f, left.y + 30f, 40f, 40f), new Color(0.67f, 0.00f, 0.80f, 1f));
            Widgets.Label(new Rect(left.x + 56f, left.y + 32f, left.width - 64f, 22f), snapshot.ModelIdentifier);

            string stats5 =
                $"stats 5\n" +
                $"Price/M Tokens: {snapshot.PricePerMTokens:0.00}\n" +
                $"Speed Multiplier: x{snapshot.SpeedMultiplier:0.00}\n" +
                $"Live Throughput: {snapshot.LiveThroughput:0.0} TPS\n" +
                $"Max Capacity/Claw: {snapshot.MaxCapacityPerClaw:0.0} TPS\n" +
                $"Active Claws: {snapshot.ActiveClaws}\n" +
                $"Hourly Silver Rate: {snapshot.HourlySilverRate:0.00}\n" +
                $"Lifetime Silver Spent: {snapshot.LifetimeSilverSpent:0.00}\n" +
                $"Pending Silver Payment: {snapshot.PendingPayment:0.00}";

            Widgets.Label(new Rect(left.x + 8f, left.y + 74f, left.width - 16f, left.height - 78f), stats5);

            Widgets.DrawBox(right);
            Widgets.Label(new Rect(right.x + 8f, right.y + 4f, right.width - 16f, 22f), "Total Silver Consumption");
            DrawLineGraph(new Rect(right.x + 8f, right.y + 26f, right.width - 16f, right.height - 34f), snapshot.TotalSilverHistory, new Color(0.95f, 0.88f, 0.10f, 1f));
        }

        private void DrawConnectedClawGrid(Rect rect, SubscriptionSnapshot snapshot)
        {
            Widgets.DrawMenuSection(rect);
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), "Connected Claws");

            Rect scrollRect = new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, rect.height - 38f);
            int columns = 4;
            float cellW = (scrollRect.width - 16f) / columns;
            float cellH = 82f;
            int rows = Mathf.CeilToInt(snapshot.Claws.Count / (float)columns);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 18f, rows * cellH + 4f);

            Widgets.BeginScrollView(scrollRect, ref clawScrollPos, viewRect);

            for (int i = 0; i < snapshot.Claws.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Rect cell = new Rect(col * cellW, row * cellH, cellW - 6f, cellH - 6f);

                Widgets.DrawBox(cell);
                if (selectedClawIndex == i)
                {
                    Widgets.DrawHighlightSelected(cell);
                }

                SubscriptionClawSnapshot claw = snapshot.Claws[i];
                Widgets.DrawBoxSolid(new Rect(cell.x + 8f, cell.y + 8f, 32f, 32f), new Color(0.15f, 0.40f, 0.95f, 1f));
                Widgets.Label(new Rect(cell.x + 46f, cell.y + 8f, cell.width - 54f, 20f), claw.Claw.NameShortColored);
                Widgets.Label(new Rect(cell.x + 8f, cell.y + 46f, cell.width - 12f, 20f), subscription.GetAssignedGpuLabelFallback(claw.Claw));

                if (Widgets.ButtonInvisible(cell))
                {
                    selectedClawIndex = i;
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawClawDetail(Rect rect, SubscriptionSnapshot snapshot)
        {
            Widgets.DrawMenuSection(rect);
            if (selectedClawIndex < 0 || selectedClawIndex >= snapshot.Claws.Count)
            {
                Widgets.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 24f), "Select a claw to view stats 6.");
                return;
            }

            SubscriptionClawSnapshot selected = snapshot.Claws[selectedClawIndex];
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), "stats 6");

            float provided = subscription.GetAvailableTokenRateForClawfish(selected.Claw);
            string detail =
                $"{selected.Claw.NameShortColored}\n" +
                $"Current Token/s: {selected.CurrentTps:0.0} needed\n" +
                $"Provided Token/s: {provided:0.0}\n" +
                $"Silver Consumption/h: {selected.SilverPerHour:0.00}";
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, 88f), detail);

            Rect buttonDaily = new Rect(rect.x + 8f, rect.y + 118f, 70f, 24f);
            Rect button15 = new Rect(buttonDaily.xMax + 6f, buttonDaily.y, 78f, 24f);
            if (Widgets.ButtonText(buttonDaily, "Daily"))
            {
                range = GraphRange.Daily;
            }

            if (Widgets.ButtonText(button15, "15-Day"))
            {
                range = GraphRange.FifteenDay;
            }

            Rect graphRect = new Rect(rect.x + 8f, rect.y + 150f, rect.width - 16f, rect.height - 206f);
            DrawLineGraph(graphRect, GetSeriesForRange(selected.SilverHistory), new Color(0.95f, 0.88f, 0.10f, 1f));

            Rect disconnectRect = new Rect(rect.x + 8f, rect.yMax - 44f, rect.width - 16f, 32f);
            if (Widgets.ButtonText(disconnectRect, "Disconnect"))
            {
                subscription.DisconnectClaw(selected.Claw);
                selectedClawIndex = -1;
                Messages.Message($"{selected.Claw.NameShortColored} disconnected from LLM Subscription", MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        private List<float> GetSeriesForRange(List<float> source)
        {
            int desired = range == GraphRange.Daily ? 180 : 360;
            if (source == null || source.Count <= desired)
            {
                return source ?? new List<float>();
            }

            return source.GetRange(source.Count - desired, desired);
        }

        private static void DrawLineGraph(Rect rect, List<float> values, Color color)
        {
            Widgets.DrawBox(rect);
            if (values == null || values.Count < 2)
            {
                return;
            }

            float max = 1f;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];
                }
            }

            float step = (rect.width - 4f) / Mathf.Max(1, values.Count - 1);
            Vector2 prev = Vector2.zero;
            for (int i = 0; i < values.Count; i++)
            {
                float x = rect.x + 2f + i * step;
                float y = rect.yMax - 2f - (values[i] / max) * (rect.height - 4f);
                Vector2 cur = new Vector2(x, y);
                if (i > 0)
                {
                    Widgets.DrawLine(prev, cur, color, 2f);
                }

                prev = cur;
            }
        }
    }

    internal static class SubscriptionPanelExtensions
    {
        public static string GetAssignedGpuLabelFallback(this CompLLMSubscriptionService service, Pawn claw)
        {
            if (claw == null)
            {
                return "(no claw)";
            }

            CompClawfishTokenConnection conn = claw.TryGetComp<CompClawfishTokenConnection>();
            if (conn == null || !conn.IsConnected || conn.ConnectedSupplier == null)
            {
                return "Unassigned";
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
