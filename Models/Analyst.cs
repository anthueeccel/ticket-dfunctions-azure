/// <summary>
/// Value object describing a support agent that can be notified and can accept
/// a ticket. Analysts live in the static in-memory list in
/// <c>Data/SampleAnalysts</c>; there is no persistence for them.
/// </summary>
public class Analyst
{
        public string AnalystId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Analyst()
    {
    }

    public Analyst(string analystId, string name)
    {
        AnalystId = analystId;
        Name = name;
    }
}