namespace ShooterLoop;

// A 7-day bar chart of enemies killed per day, for the Stats screen's "Actividad reciente" section —
// the one chart backed by real time-series data (GameManager.GetRecentActivity), rather than a
// snapshot ratio like StatProgressRing. Bars snap to the game's 4px grid, same "stepped, not smooth"
// look as everything else Juice.cs draws by hand.
//
// Built and added via `new()` from StatsMenu rather than its own .tscn, same reasoning as
// StatProgressRing — no child nodes, nothing a scene file would add.
public partial class ActivityBarChart : Control
{
    private List<GameManager.DailyActivity> _data = new();
    private int _max = 1;

    public Color BarColor = Palette.OndaBlast;
    private static readonly Color EmptyBarColor = new(0.102f, 0.0588f, 0.1686f, 0.85f);
    private static readonly Color AxisTextColor = new(0.72f, 0.76f, 0.84f, 0.85f);

    private const float LabelRowHeight = 16f;

    public void SetData(List<GameManager.DailyActivity> data)
    {
        _data = data ?? new List<GameManager.DailyActivity>();
        _max = 1;
        foreach (var day in _data) _max = Mathf.Max(_max, day.EnemiesKilled);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_data.Count == 0) return;

        float chartHeight = Size.Y - LabelRowHeight;
        if (chartHeight <= 0f) return;

        float slot = Size.X / _data.Count;
        float barWidth = Mathf.Max(Juice.PixelSize, Mathf.Floor(slot * 0.5f / Juice.PixelSize) * Juice.PixelSize);

        var font = GetThemeDefaultFont();
        const int fontSize = 11;

        for (int i = 0; i < _data.Count; i++)
        {
            float centerX = slot * i + slot / 2f;
            int kills = _data[i].EnemiesKilled;
            float t = kills / (float)_max;

            // A day with 0 kills still gets a token sliver, so every column is visible and the chart
            // doesn't read as missing data for that day.
            float barHeight = kills > 0
                ? Mathf.Max(Juice.PixelSize * 2f, Mathf.Ceil(chartHeight * t / Juice.PixelSize) * Juice.PixelSize)
                : Juice.PixelSize;

            var rect = new Rect2(centerX - barWidth / 2f, chartHeight - barHeight, barWidth, barHeight);
            DrawRect(rect, kills > 0 ? BarColor : EmptyBarColor);

            string label = DayLabel(i);
            Vector2 textSize = font.GetStringSize(label, HorizontalAlignment.Center, -1f, fontSize);
            DrawString(font, new Vector2(centerX - textSize.X / 2f, Size.Y - 3f), label,
                HorizontalAlignment.Center, -1f, fontSize, AxisTextColor);
        }
    }

    // "Hoy" for the last bar, otherwise a short D/M so it never wraps at this width.
    private string DayLabel(int index)
    {
        int daysAgo = _data.Count - 1 - index;
        if (daysAgo == 0) return "Hoy";

        var date = DateTime.Today.AddDays(-daysAgo);
        return $"{date.Day}/{date.Month}";
    }
}
