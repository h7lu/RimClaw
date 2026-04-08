using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimClaw
{
    public static class ConsoleLineChartUtility
    {
        public static void DrawSingleSeries(Rect rect, List<float> values, Color lineColor, Color borderColor, string yUnitLabel, int axisDecimals = 0)
        {
            DrawOutline(rect, borderColor);
            if (values == null || values.Count < 2)
            {
                return;
            }

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;

            Rect plot = new Rect(rect.x + 42f, rect.y + 12f, rect.width - 50f, rect.height - 18f);
            float baselineY = plot.yMax - 8f;
            float drawableHeight = Mathf.Max(1f, plot.height - 12f);

            Widgets.DrawLine(new Vector2(plot.x, baselineY), new Vector2(plot.xMax, baselineY), borderColor, 1f);
            Widgets.Label(new Rect(rect.x + 4f, rect.y + 2f, 36f, 16f), yUnitLabel);
            Widgets.Label(new Rect(plot.x - 2f, baselineY - 10f, 22f, 16f), "RimClaw_Chart_Zero".Translate());

            float max = 0f;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];
                }
            }

            const int tickCount = 4;
            axisDecimals = Mathf.Clamp(axisDecimals, 0, 3);
            float scale = Mathf.Pow(10f, axisDecimals);
            float roundedMax = Mathf.Max(1f / scale, Mathf.Ceil(max * scale) / scale);
            float tickStep = drawableHeight / tickCount;
            for (int i = 1; i <= tickCount; i++)
            {
                float y = baselineY - i * tickStep;
                float tickValue = roundedMax * (i / (float)tickCount);
                Widgets.DrawLine(new Vector2(plot.x - 4f, y), new Vector2(plot.x + 2f, y), borderColor, 1f);
                Widgets.Label(new Rect(rect.x + 4f, y - 8f, 44f, 16f), tickValue.ToString($"F{axisDecimals}"));
            }

            Widgets.DrawLine(new Vector2(plot.x, rect.y + 2f), new Vector2(plot.x, baselineY), borderColor, 1f);
            Widgets.DrawLine(new Vector2(plot.xMax, rect.y + 2f), new Vector2(plot.xMax, baselineY), borderColor, 1f);
            Widgets.Label(new Rect(plot.x - 4f, rect.yMax - 16f, 24f, 14f), "RimClaw_Chart_Old".Translate());
            Widgets.Label(new Rect(plot.xMax - 20f, rect.yMax - 16f, 24f, 14f), "RimClaw_Chart_New".Translate());

            float step = plot.width / Mathf.Max(1, values.Count - 1);
            Vector2 prev = Vector2.zero;
            for (int i = 0; i < values.Count; i++)
            {
                float x = plot.x + i * step;
                float y = baselineY - (values[i] / roundedMax) * drawableHeight;
                Vector2 cur = new Vector2(x, y);
                if (i > 0)
                {
                    Widgets.DrawLine(prev, cur, lineColor, 2f);
                }

                prev = cur;
            }

            Text.Font = oldFont;
        }

        public static void DrawDualSeries(Rect rect, List<float> primary, List<float> secondary, Color primaryColor, Color secondaryColor, Color borderColor)
        {
            DrawOutline(rect, borderColor);

            int totalCount = Mathf.Min(primary?.Count ?? 0, secondary?.Count ?? 0);
            if (totalCount < 2)
            {
                return;
            }

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 4f, 120f, 22f), "RimClaw_Chart_TokenPerSec".Translate());
            Widgets.Label(new Rect(rect.xMax - 90f, rect.y + 4f, 84f, 22f), "RimClaw_Chart_HeatPerSec".Translate());

            Rect plot = new Rect(rect.x + 56f, rect.y + 26f, rect.width - 112f, rect.height - 34f);
            DrawLine(new Vector2(plot.x, plot.y), new Vector2(plot.x, plot.yMax), borderColor);
            DrawLine(new Vector2(plot.xMax, plot.y), new Vector2(plot.xMax, plot.yMax), borderColor);

            float zeroLineY = plot.yMax - 8f;
            Widgets.DrawLine(new Vector2(plot.x, zeroLineY), new Vector2(plot.xMax, zeroLineY), borderColor, 1f);
            Widgets.Label(new Rect(plot.x + 4f, zeroLineY - 10f, 24f, 16f), "RimClaw_Chart_Zero".Translate());

            float rawMaxPrimary = 1f;
            float rawMaxSecondary = 1f;
            for (int i = 0; i < totalCount; i++)
            {
                if (primary[i] > rawMaxPrimary)
                {
                    rawMaxPrimary = primary[i];
                }

                if (secondary[i] > rawMaxSecondary)
                {
                    rawMaxSecondary = secondary[i];
                }
            }

            const int ticks = 4;
            int primaryStep = Mathf.Max(1, Mathf.CeilToInt(rawMaxPrimary / ticks));
            int maxPrimary = primaryStep * ticks;

            int secondaryFromPrimary = Mathf.Max(1, Mathf.RoundToInt(maxPrimary / 100f));
            int secondaryNeeded = Mathf.Max(secondaryFromPrimary, Mathf.CeilToInt(rawMaxSecondary));
            int secondaryStep = Mathf.Max(1, Mathf.CeilToInt(secondaryNeeded / (float)ticks));
            int maxSecondary = secondaryStep * ticks;

            float baseline = zeroLineY;
            float drawableHeight = Mathf.Max(1f, plot.height * 0.8f - 14f);

            DrawGraduations(plot.x, plot, maxPrimary, true, drawableHeight, ticks, borderColor);
            DrawGraduations(plot.xMax, plot, maxSecondary, false, drawableHeight, ticks, borderColor);

            float step = plot.width / (totalCount - 1);
            Vector2 prevPrimary = Vector2.zero;
            Vector2 prevSecondary = Vector2.zero;
            for (int i = 0; i < totalCount; i++)
            {
                float x = plot.x + i * step;
                float yPrimary = baseline - (primary[i] / Mathf.Max(1f, maxPrimary)) * drawableHeight;
                float ySecondary = baseline - (secondary[i] / Mathf.Max(1f, maxSecondary)) * drawableHeight;

                Vector2 curPrimary = new Vector2(x, yPrimary);
                Vector2 curSecondary = new Vector2(x, ySecondary);

                if (i > 0)
                {
                    Widgets.DrawLine(prevPrimary, curPrimary, primaryColor, 2f);
                    Widgets.DrawLine(prevSecondary, curSecondary, secondaryColor, 2f);
                }

                prevPrimary = curPrimary;
                prevSecondary = curSecondary;
            }

            Text.Font = oldFont;
        }

        private static void DrawGraduations(float axisX, Rect plot, int maxValue, bool leftAxis, float drawableHeight, int ticks, Color borderColor)
        {
            float baseline = plot.yMax - 8f;
            for (int i = 1; i <= ticks; i++)
            {
                float y = baseline - (i / (float)ticks) * drawableHeight;
                if (leftAxis)
                {
                    Widgets.DrawLine(new Vector2(axisX - 4f, y), new Vector2(axisX + 2f, y), borderColor, 1f);
                    Widgets.Label(new Rect(axisX - 46f, y - 8f, 40f, 16f), Mathf.RoundToInt(maxValue * (i / (float)ticks)).ToString());
                }
                else
                {
                    Widgets.DrawLine(new Vector2(axisX - 2f, y), new Vector2(axisX + 4f, y), borderColor, 1f);
                    Widgets.Label(new Rect(axisX + 8f, y - 8f, 40f, 16f), Mathf.RoundToInt(maxValue * (i / (float)ticks)).ToString());
                }
            }
        }

        private static void DrawOutline(Rect rect, Color borderColor)
        {
            DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y), borderColor);
            DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.yMax), borderColor);
            DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.x, rect.yMax), borderColor);
            DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x, rect.y), borderColor);
        }

        private static void DrawLine(Vector2 from, Vector2 to, Color color)
        {
            Widgets.DrawLine(from, to, color, 1f);
        }
    }
}