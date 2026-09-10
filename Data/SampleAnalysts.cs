using System.Collections.Generic;

/// <summary>
/// Static, in-memory roster of support analysts.
/// </summary>
/// <remarks>
/// Analysts are a fixed demo dataset (no persistence), so this is just a static list.
/// In a real system this would live in a database and be queryable.
/// </remarks>
public static class SampleAnalysts
{
    public static List<Analyst> All = new List<Analyst>()
    {
        new Analyst("A1", "Ana Torres"),
        new Analyst("A2", "Jose Menendez"),
        new Analyst("A3", "Mari Cobert"),
        new Analyst("A4", "Diego Diaz"),
        new Analyst("A5", "Elena Costa")
    };
}