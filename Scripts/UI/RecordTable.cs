namespace ShooterLoop;

using System.Collections.Generic;
using Godot;

// Renders a score table as aligned, coloured bbcode rows.
//
// This exists because the two tables that show records -- the main menu's top-10 and the per-pilot
// one in character select -- each had their own inline format string, and neither lined up. The main
// menu padded its columns but the label was centre-aligned, so every row was shifted by its own
// length and the padding achieved nothing; character select didn't pad at all. Both then overflowed
// their panel and wrapped, which put the date on a line of its own and turned ten records into twenty
// ragged lines.
//
// Alignment here depends on the font being monospaced, which PixelFont is by construction: every
// glyph gets the same advance (see tools/gen_font.py). If the project font is ever swapped for a
// proportional one, this needs to become a GridContainer instead of padded text.
public static class RecordTable
{
    // Columns are coloured, not labelled -- a header row would cost a line the panels don't have.
    private const string RankColor = "#ffe066";
    private const string ScoreColor = "#7dfdfe";
    private const string RoundColor = "#c9a6ff";
    private const string DateColor = "#b8a040";

    /// <summary>
    /// Aligned rows for the first <paramref name="maxRows"/> records, or a placeholder if there are
    /// none. <paramref name="charBudget"/> is how many monospaced characters fit across the panel;
    /// the gap between columns tightens rather than letting a row wrap.
    /// </summary>
    public static string Build(IReadOnlyList<GameManager.ScoreRecord> records, int maxRows,
        int charBudget, string emptyText)
    {
        if (records == null || records.Count == 0) return emptyText;

        int rows = Mathf.Min(records.Count, maxRows);

        // Column widths come from the data, not from a guess. A table topping out at 987 shouldn't
        // carry the whitespace a 1,207,718 would need, and one that does need it must not overflow.
        int rankWidth = 0, scoreWidth = 0, roundWidth = 0, dateWidth = 0;
        var rank = new string[rows];
        var score = new string[rows];
        var round = new string[rows];
        var date = new string[rows];

        for (int i = 0; i < rows; i++)
        {
            rank[i] = (i + 1).ToString();
            score[i] = records[i].Score.ToString("N0");
            // Round 0 only ever comes from a save written before records tracked it (see
            // GameManager.ScoreRecord) -- shown as "R?" rather than a misleading "R0", since round
            // numbering starts at 1 and a real round 0 never happens.
            round[i] = records[i].Round > 0 ? $"R{records[i].Round}" : "R?";
            date[i] = records[i].Date;

            rankWidth = Mathf.Max(rankWidth, rank[i].Length);
            scoreWidth = Mathf.Max(scoreWidth, score[i].Length);
            roundWidth = Mathf.Max(roundWidth, round[i].Length);
            dateWidth = Mathf.Max(dateWidth, date[i].Length);
        }

        // Two spaces reads better; one still reads. Anything wider than the panel would wrap, which
        // is the failure this whole class exists to prevent, so the gap gives way first.
        int content = rankWidth + scoreWidth + roundWidth + dateWidth;
        int gap = content + 3 * 2 <= charBudget ? 2 : 1;
        string pad = new string(' ', gap);

        var lines = new string[rows];
        for (int i = 0; i < rows; i++)
        {
            lines[i] =
                $"[color={RankColor}]{rank[i].PadLeft(rankWidth)}[/color]{pad}" +
                $"[color={ScoreColor}]{score[i].PadLeft(scoreWidth)}[/color]{pad}" +
                $"[color={RoundColor}]{round[i].PadRight(roundWidth)}[/color]{pad}" +
                $"[color={DateColor}]{date[i]}[/color]";
        }
        return string.Join("\n", lines);
    }
}
